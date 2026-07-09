// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Connection/CollectorConnection.cs
//  역할: 등록된 Collector 1개(CollectorEndpoint)에 대한
//        ① REST GET /api/devices 최초 스냅샷 조회 (+ CollectorId 자동 동기화)
//        ② SignalR HubConnection 생명주기 관리 (연결/재연결/종료)
//        ③ "TagValue" 이벤트 구독 → LiveTagAggregator 로 전달 (MN-02)
//        ④ "AlarmChanged" 이벤트 구독 → AlarmAggregator 로 전달 (MN-03)
//        ⑤ AcknowledgeAsync() — 이 Collector 로만 ACK 요청 전송 (MN-03)
//  MN-01B: 신규
//  MN-02: "TagValue" 이벤트 구독 추가 (onTagValue 콜백)
//  MN-03: "AlarmChanged" 이벤트 구독 + AcknowledgeAsync() 추가
//  FIX: CS0246 HttpClient 못 찾음 — using System.Net.Http; 누락 추가
//  생성: 2026-07-07 / 수정: 2026-07-07 (MN-03)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using lssLib.Log;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;

namespace IIoT.Monitor.Core.Connection;

/// <summary>
/// 등록된 Collector 1개에 대한 연결 상태를 관리한다.
/// <para>
/// StartAsync() 호출 시:
///  ① HttpClient 로 GET /api/devices 를 1회 조회하여 실제 CollectorId 를 확인하고,
///     로컬 <see cref="CollectorEndpoint.Id"/> 와 다르면 자동으로 교정한다(자동 동기화).
///  ② SignalR HubConnection 을 생성하고 자동 재연결(WithAutomaticReconnect)과 함께 시작한다.
///  ③④ "TagValue"/"AlarmChanged" 이벤트를 구독하여 각각의 Aggregator 로 전달한다.
/// </para>
/// </summary>
public sealed class CollectorConnection : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorEndpoint           _endpoint;
    private readonly Action<string, string>      _onCollectorIdResolved;
    private readonly Action<string, JsonElement> _onTagValue;
    private readonly Action<string, JsonElement> _onAlarmChanged;
    private readonly HttpClient                  _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private HubConnection? _hub;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // §2 ─ 공개 프로퍼티 ───────────────────────────────────

    /// <summary>이 연결이 대상으로 하는 Collector 접속 정보</summary>
    public CollectorEndpoint Endpoint => _endpoint;

    // §3 ─ 생성자 ──────────────────────────────────────────

    /// <param name="endpoint">대상 Collector 접속 정보</param>
    /// <param name="onCollectorIdResolved">CollectorId 자동 동기화 발생 시 호출(oldId, newId)</param>
    /// <param name="onTagValue">★ MN-02: "TagValue" 이벤트 수신 시 호출(collectorId, payload)</param>
    /// <param name="onAlarmChanged">★ MN-03: "AlarmChanged" 이벤트 수신 시 호출(collectorId, payload)</param>
    public CollectorConnection(
        CollectorEndpoint           endpoint,
        Action<string, string>      onCollectorIdResolved,
        Action<string, JsonElement> onTagValue,
        Action<string, JsonElement> onAlarmChanged)
    {
        _endpoint              = endpoint;
        _onCollectorIdResolved = onCollectorIdResolved;
        _onTagValue            = onTagValue;
        _onAlarmChanged        = onAlarmChanged;
    }

    // §4 ─ 시작 ────────────────────────────────────────────

    public async Task StartAsync()
    {
        _endpoint.StatusText = "연결 중...";

        // ① REST 스냅샷 조회 → CollectorId 자동 동기화 (Hub 연결 전에 완료되어야
        //    아래 ②에서 구독하는 콜백에 최종 확정된 CollectorId 가 전달된다)
        await _TrySyncCollectorIdAsync();

        // ② SignalR Hub 연결
        _hub = new HubConnectionBuilder()
            .WithUrl(_endpoint.HubUrl)
            .WithAutomaticReconnect(new[]
            {
                TimeSpan.FromSeconds(1),
                TimeSpan.FromSeconds(3),
                TimeSpan.FromSeconds(5),
                TimeSpan.FromSeconds(10)
            })
            .Build();

        _hub.Reconnecting += _ =>
        {
            _endpoint.StatusText = "재연결 중...";
            return Task.CompletedTask;
        };
        _hub.Reconnected += _ =>
        {
            _endpoint.StatusText = "연결됨";
            return Task.CompletedTask;
        };
        _hub.Closed += ex =>
        {
            _endpoint.StatusText = ex is null ? "연결 종료" : $"오류: {ex.Message}";
            return Task.CompletedTask;
        };

        // ★ MN-02/MN-03: TagValue/AlarmChanged 이벤트 구독 — collectorId(연결 출처)를 함께 전달
        _hub.On<JsonElement>("TagValue",     data => _onTagValue(_endpoint.Id, data));
        _hub.On<JsonElement>("AlarmChanged", data => _onAlarmChanged(_endpoint.Id, data));

        try
        {
            await _hub.StartAsync();
            _endpoint.StatusText = "연결됨";
            LogManager.Instance.Info("CollectorConnection",
                $"[{_endpoint.Name}] Hub 연결 성공 — {_endpoint.HubUrl}");
        }
        catch (Exception ex)
        {
            _endpoint.StatusText = $"연결 실패: {ex.Message}";
            LogManager.Instance.Warn("CollectorConnection",
                $"[{_endpoint.Name}] Hub 연결 실패(자동 재시도 없음 — WithAutomaticReconnect는 " +
                $"최초 연결 성공 후에만 동작): {ex.Message}");
        }
    }

    // §5 ─ CollectorId 자동 동기화 ─────────────────────────

    private async Task _TrySyncCollectorIdAsync()
    {
        var url = $"http://{_endpoint.Host}:{_endpoint.Port}/api/devices";

        try
        {
            var snapshot = await _http.GetFromJsonAsync<List<DeviceSnapshotDto>>(url, _jsonOpts);

            if (snapshot is not { Count: > 0 })
                return;

            var actualId = snapshot[0].CollectorId;

            if (string.IsNullOrWhiteSpace(actualId) || actualId == _endpoint.Id)
                return;

            var oldId = _endpoint.Id;
            _endpoint.Id = actualId;
            _onCollectorIdResolved(oldId, actualId);

            LogManager.Instance.Info("CollectorConnection",
                $"[{_endpoint.Name}] CollectorId 자동 동기화: '{oldId}' → '{actualId}'");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("CollectorConnection",
                $"[{_endpoint.Name}] REST 스냅샷 조회 실패(CollectorId 자동 동기화 생략): {ex.Message}");
        }
    }

    // §6 ─ 알람 ACK 요청 (MN-03) ───────────────────────────

    /// <summary>
    /// 이 Collector 로만 ACK 요청을 전송한다("발생 출처로만 전송" 원칙, MN-03 설계).
    /// ★ C-EX-12(Collector 측 후속 작업) 완료 전까지는 Hub 에 해당 서버 메서드가
    /// 없어 HubException 이 발생할 수 있음 — 이 경우 경고 로그만 남기고 UI는
    /// 계속 정상 동작한다(예외를 상위로 전파하지 않음).
    /// </summary>
    public async Task AcknowledgeAsync(string alarmKey)
    {
        if (_hub is not { State: HubConnectionState.Connected })
        {
            LogManager.Instance.Warn("CollectorConnection",
                $"[{_endpoint.Name}] ACK 전송 불가 — Hub 미연결 상태 (alarmKey={alarmKey})");
            return;
        }

        try
        {
            await _hub.InvokeAsync("AcknowledgeAlarm", alarmKey);
        }
        catch (Exception ex)
        {
            // C-EX-12 미완료 시 여기서 HubException("Method does not exist") 발생 — 정상적인 과도기 상태
            LogManager.Instance.Warn("CollectorConnection",
                $"[{_endpoint.Name}] AcknowledgeAlarm 호출 실패 — Collector 측 C-EX-12 미완료 가능성: {ex.Message}");
        }
    }

    // §7 ─ 종료 ────────────────────────────────────────────

    public async Task StopAsync()
    {
        if (_hub is not null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }
        _endpoint.StatusText = "미연결";
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _http.Dispose();
    }
}
