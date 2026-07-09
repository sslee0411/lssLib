// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Connection/CollectorConnection.cs
//  역할: 등록된 Collector 1개(CollectorEndpoint)에 대한
//        ① REST GET /api/devices 최초 스냅샷 조회 (+ CollectorId 자동 동기화)
//        ② SignalR HubConnection 생명주기 관리 (연결/재연결/종료)
//        ③ "TagValue" 이벤트 구독 → LiveTagAggregator 로 전달 (MN-02)
//        ④ "AlarmChanged" 이벤트 구독 → AlarmAggregator 로 전달 (MN-03)
//        ⑤ AcknowledgeAsync() — 이 Collector 로만 ACK 요청 전송 (MN-03)
//        ⑥ 연결 끊김/복구 디바운스 알림 (MN-EX-08)
//  MN-01B: 신규
//  MN-02: "TagValue" 이벤트 구독 추가 (onTagValue 콜백)
//  MN-03: "AlarmChanged" 이벤트 구독 + AcknowledgeAsync() 추가
//  FIX(1): CS0246 HttpClient 못 찾음 — using System.Net.Http; 누락 추가
//  FIX(2): _endpoint.StatusText/_endpoint.Id 를 백그라운드 스레드에서 직접
//          수정하고 있어(HubConnection 콜백·HttpClient 응답은 UI 스레드가 아님)
//          크로스 스레드 오류 및 화면 미갱신 위험이 있었음 — 모든 변경을
//          Dispatcher.Invoke 로 마샬링하는 _SetStatus/_SetId 헬퍼로 통일.
//  MN-EX-08: onConnectionIssue/onConnectionRecovered 콜백 추가.
//            WithAutomaticReconnect 는 최대 4회(1/3/5/10초) 재시도하는 동안
//            Reconnecting 이벤트를 매번 발생시키는데, 그때마다 알림을 보내면
//            한 번의 "끊김"에 최대 4번 중복 알림이 발생한다 — _issueAlerted
//            플래그로 "끊김 진입" 1회, "복구" 1회만 알리도록 디바운스한다.
//  생성: 2026-07-07 / 수정: 2026-07-08 (MN-EX-08)
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using lssLib.Log;
using Microsoft.AspNetCore.SignalR.Client;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Windows;

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
/// <para>
/// ★ 모든 <see cref="CollectorEndpoint"/> 프로퍼티 변경은 백그라운드 스레드(HTTP 응답,
/// SignalR 콜백)에서 발생할 수 있으므로 반드시 UI Dispatcher 로 마샬링한다
/// (<see cref="_SetStatus"/> / <see cref="_SetId"/> 헬퍼를 통해서만 변경).
/// </para>
/// </summary>
public sealed class CollectorConnection : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorEndpoint            _endpoint;
    private readonly Action<string, string>       _onCollectorIdResolved;
    private readonly Action<string, JsonElement>  _onTagValue;
    private readonly Action<string, JsonElement>  _onAlarmChanged;
    private readonly Action<CollectorEndpoint>    _onConnectionIssue;
    private readonly Action<CollectorEndpoint>    _onConnectionRecovered;
    private readonly HttpClient                   _http = new() { Timeout = TimeSpan.FromSeconds(5) };

    private HubConnection? _hub;

    /// <summary>★ MN-EX-08: 현재 "끊김" 알림이 이미 발생한 상태인지(중복 알림 방지)</summary>
    private bool _issueAlerted;

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    // §2 ─ 공개 프로퍼티 ───────────────────────────────────

    /// <summary>이 연결이 대상으로 하는 Collector 접속 정보</summary>
    public CollectorEndpoint Endpoint => _endpoint;

    /// <summary>
    /// ★ FIX: 이 연결이 실제로 StartAsync() 시점에 사용한 HubUrl.
    /// SignalR HubConnection 은 빌드 시점의 URL로 고정되므로, 이후 사용자가
    /// Host/Port 를 수정해도 이미 만들어진 연결에는 반영되지 않는다.
    /// CollectorConnectionManager 가 이 값과 현재 Endpoint.HubUrl 을 비교하여
    /// 변경 여부를 감지하고, 다르면 연결을 재시작한다.
    /// </summary>
    public string? StartedHubUrl { get; private set; }

    // §3 ─ 생성자 ──────────────────────────────────────────

    /// <param name="endpoint">대상 Collector 접속 정보</param>
    /// <param name="onCollectorIdResolved">CollectorId 자동 동기화 발생 시 호출(oldId, newId)</param>
    /// <param name="onTagValue">★ MN-02: "TagValue" 이벤트 수신 시 호출(collectorId, payload)</param>
    /// <param name="onAlarmChanged">★ MN-03: "AlarmChanged" 이벤트 수신 시 호출(collectorId, payload)</param>
    /// <param name="onConnectionIssue">
    /// ★ MN-EX-08: 연결이 "끊김" 상태로 진입할 때 1회만 호출(재시도 중 중복 호출 없음)
    /// </param>
    /// <param name="onConnectionRecovered">
    /// ★ MN-EX-08: 끊김 상태였다가 재연결에 성공했을 때 1회만 호출
    /// </param>
    public CollectorConnection(
        CollectorEndpoint           endpoint,
        Action<string, string>      onCollectorIdResolved,
        Action<string, JsonElement> onTagValue,
        Action<string, JsonElement> onAlarmChanged,
        Action<CollectorEndpoint>   onConnectionIssue,
        Action<CollectorEndpoint>   onConnectionRecovered)
    {
        _endpoint               = endpoint;
        _onCollectorIdResolved  = onCollectorIdResolved;
        _onTagValue             = onTagValue;
        _onAlarmChanged         = onAlarmChanged;
        _onConnectionIssue      = onConnectionIssue;
        _onConnectionRecovered  = onConnectionRecovered;
    }

    // §4 ─ UI 스레드 안전 setter ────────────────────────────

    /// <summary>_endpoint.StatusText 를 UI 스레드에서 안전하게 변경한다.</summary>
    private void _SetStatus(string text)
        => Application.Current?.Dispatcher.Invoke(() => _endpoint.StatusText = text);

    /// <summary>_endpoint.Id 를 UI 스레드에서 안전하게 변경한다.</summary>
    private void _SetId(string id)
        => Application.Current?.Dispatcher.Invoke(() => _endpoint.Id = id);

    // §5 ─ 시작 ────────────────────────────────────────────

    public async Task StartAsync()
    {
        _SetStatus("연결 중...");

        // ① REST 스냅샷 조회 → CollectorId 자동 동기화 (Hub 연결 전에 완료되어야
        //    아래 ②에서 구독하는 콜백에 최종 확정된 CollectorId 가 전달된다)
        await _TrySyncCollectorIdAsync();

        // ② SignalR Hub 연결
        // ★ FIX: 실제 이 연결에서 사용하는 URL을 기록 (Host/Port 변경 감지용)
        StartedHubUrl = _endpoint.HubUrl;

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

        // ★ MN-EX-08: Reconnecting 은 재시도마다(최대 4회) 발생하므로
        //   _issueAlerted 로 "끊김 진입" 시점 1회만 알림
        _hub.Reconnecting += _ =>
        {
            _SetStatus("재연결 중...");
            if (!_issueAlerted)
            {
                _issueAlerted = true;
                _onConnectionIssue(_endpoint);
            }
            return Task.CompletedTask;
        };
        _hub.Reconnected += _ =>
        {
            _SetStatus("연결됨");
            if (_issueAlerted)
            {
                _issueAlerted = false;
                _onConnectionRecovered(_endpoint);
            }
            return Task.CompletedTask;
        };
        _hub.Closed += ex =>
        {
            _SetStatus(ex is null ? "연결 종료" : $"오류: {ex.Message}");
            // 재시도를 모두 소진하고 영구 종료된 경우 — Reconnecting 이 먼저 발생해
            // 이미 알림이 나갔을 것이므로 여기서는 별도 알림 없음(중복 방지)
            return Task.CompletedTask;
        };

        // ★ MN-02/MN-03: TagValue/AlarmChanged 이벤트 구독 — collectorId(연결 출처)를 함께 전달
        _hub.On<JsonElement>("TagValue",     data => _onTagValue(_endpoint.Id, data));
        _hub.On<JsonElement>("AlarmChanged", data => _onAlarmChanged(_endpoint.Id, data));

        try
        {
            await _hub.StartAsync();
            _SetStatus("연결됨");
            LogManager.Instance.Info("CollectorConnection",
                $"[{_endpoint.Name}] Hub 연결 성공 — {_endpoint.HubUrl}");
        }
        catch (Exception ex)
        {
            _SetStatus($"연결 실패: {ex.Message}");
            LogManager.Instance.Warn("CollectorConnection",
                $"[{_endpoint.Name}] Hub 연결 실패(자동 재시도 없음 — WithAutomaticReconnect는 " +
                $"최초 연결 성공 후에만 동작): {ex.Message}");

            // ★ MN-EX-08: 최초 연결 자체가 실패한 경우도 "끊김"으로 간주하여 1회 알림
            if (!_issueAlerted)
            {
                _issueAlerted = true;
                _onConnectionIssue(_endpoint);
            }
        }
    }

    // §6 ─ CollectorId 자동 동기화 ─────────────────────────

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
            _SetId(actualId);
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

    // §7 ─ 알람 ACK 요청 (MN-03) ───────────────────────────

    /// <summary>
    /// 이 Collector 로만 ACK 요청을 전송한다("발생 출처로만 전송" 원칙, MN-03 설계).
    /// ★ C-EX-12(Collector 측 완료) 이후에는 정상 동작한다. 완료 전에는 HubException
    /// 이 발생할 수 있으며, 이 경우 경고 로그만 남기고 UI는 계속 정상 동작한다.
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
            LogManager.Instance.Warn("CollectorConnection",
                $"[{_endpoint.Name}] AcknowledgeAlarm 호출 실패: {ex.Message}");
        }
    }

    // §8 ─ 종료 ────────────────────────────────────────────

    public async Task StopAsync()
    {
        if (_hub is not null)
        {
            await _hub.StopAsync();
            await _hub.DisposeAsync();
            _hub = null;
        }
        _SetStatus("미연결");
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _http.Dispose();
    }
}
