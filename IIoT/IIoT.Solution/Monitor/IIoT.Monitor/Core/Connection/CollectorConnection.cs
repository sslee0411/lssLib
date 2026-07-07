// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Connection/CollectorConnection.cs
//  역할: 등록된 Collector 1개(CollectorEndpoint)에 대한
//        ① REST GET /api/devices 최초 스냅샷 조회 (+ CollectorId 자동 동기화)
//        ② SignalR HubConnection 생명주기 관리 (연결/재연결/종료)
//        실제 Tag/알람 이벤트 구독(On<T>)은 MN-02/MN-03에서 추가한다.
//  MN-01B: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Models;
using lssLib.Log;
using Microsoft.AspNetCore.SignalR.Client;
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
/// </para>
/// <para>
/// REST 조회가 실패해도(Collector 미실행 등) SignalR 연결 시도는 계속 진행한다 —
/// 두 단계는 서로 독립적이며, 어느 한쪽의 실패가 다른 쪽을 막지 않는다.
/// </para>
/// </summary>
public sealed class CollectorConnection : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorEndpoint      _endpoint;
    private readonly Action<string, string> _onCollectorIdResolved;
    private readonly HttpClient             _http = new() { Timeout = TimeSpan.FromSeconds(5) };

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
    /// <param name="onCollectorIdResolved">
    /// CollectorId 자동 동기화 발생 시 호출되는 콜백 (oldId, newId).
    /// CollectorConnectionManager 가 내부 Dictionary 키 갱신 + monitor.json 저장에 사용.
    /// </param>
    public CollectorConnection(CollectorEndpoint endpoint, Action<string, string> onCollectorIdResolved)
    {
        _endpoint              = endpoint;
        _onCollectorIdResolved = onCollectorIdResolved;
    }

    // §4 ─ 시작 ────────────────────────────────────────────

    public async Task StartAsync()
    {
        _endpoint.StatusText = "연결 중...";

        // ① REST 스냅샷 조회 → CollectorId 자동 동기화
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

    /// <summary>
    /// GET /api/devices 를 1회 조회하여 실제 CollectorId 를 확인하고,
    /// 로컬 Id 와 다르면 자동으로 교정한다.
    /// ★ 사용자가 Id 를 비워두거나 잘못 입력해도 최초 연결 성공 시 자동으로 바로잡힌다.
    /// </summary>
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
            // REST 조회 실패는 치명적이지 않음 — Collector 미실행 상태일 수 있으므로
            // SignalR 연결 시도는 계속 진행한다.
            LogManager.Instance.Warn("CollectorConnection",
                $"[{_endpoint.Name}] REST 스냅샷 조회 실패(CollectorId 자동 동기화 생략): {ex.Message}");
        }
    }

    // §6 ─ 종료 ────────────────────────────────────────────

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
