// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Connection/CollectorConnectionManager.cs
//  역할: 등록된 Collector 목록(CollectorEndpoint[])과 실제 연결(CollectorConnection)을
//        CollectorId 기준 1:1로 동기화 관리한다 (DI 싱글턴).
//        [Collector 관리] 탭에서 목록이 변경될 때마다 SyncFromEndpointsAsync() 호출.
//  MN-01B: 신규
//  생성: 2026-07-07
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Models;
using lssLib.Log;

namespace IIoT.Monitor.Core.Connection;

/// <summary>
/// CollectorId → CollectorConnection 1:1 매핑 관리자 (DI 싱글턴).
/// <para>
/// 등록된 Collector 목록이 바뀔 때마다(<see cref="SyncFromEndpointsAsync"/>) 호출하면
/// 새로 추가된 항목은 연결을 시작하고, 삭제되거나 비활성화된 항목은 연결을 종료한다.
/// </para>
/// </summary>
public sealed class CollectorConnectionManager : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly MonitorSettingsLoader _settingsLoader;
    private readonly Dictionary<string, CollectorConnection> _connections = new();

    // §2 ─ 생성자 ──────────────────────────────────────────

    public CollectorConnectionManager(MonitorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §3 ─ 동기화 ──────────────────────────────────────────

    /// <summary>
    /// 등록된 Collector 목록과 현재 연결 상태를 동기화합니다.
    /// <para>
    /// - <c>Enabled=true</c> 이고 아직 연결이 없는 항목 → 신규 연결 시작<br/>
    /// - 목록에서 사라졌거나 <c>Enabled=false</c> 로 바뀐 항목 → 연결 종료 및 제거
    /// </para>
    /// 이미 연결 중인 항목은 그대로 유지한다(재연결하지 않음).
    /// </summary>
    public async Task SyncFromEndpointsAsync(IEnumerable<CollectorEndpoint> endpoints)
    {
        var desired = endpoints.Where(e => e.Enabled).ToDictionary(e => e.Id);

        // ① 목록에서 사라졌거나 비활성화된 연결 정리
        foreach (var key in _connections.Keys.Except(desired.Keys).ToList())
        {
            await _connections[key].StopAsync();
            await _connections[key].DisposeAsync();
            _connections.Remove(key);

            LogManager.Instance.Info("CollectorConnectionManager",
                $"Collector[{key}] 연결 해제 (목록에서 제거되었거나 비활성화됨)");
        }

        // ② 신규 항목 연결 시작
        foreach (var (id, endpoint) in desired)
        {
            if (_connections.ContainsKey(id))
                continue;

            var conn = new CollectorConnection(
                endpoint,
                onCollectorIdResolved: (oldId, newId) => _OnCollectorIdResolved(oldId, newId));

            _connections[id] = conn;

            // fire-and-forget — 개별 Collector 연결 실패가 다른 Collector나 UI를 막지 않도록
            _ = conn.StartAsync();
        }
    }

    /// <summary>
    /// CollectorConnection 이 자동 동기화로 Id 를 변경했을 때 호출됨.
    /// Dictionary 키를 갱신하고 monitor.json 을 즉시 저장한다
    /// (다음 실행부터는 올바른 Id 로 로드되어 재동기화가 필요 없음).
    /// </summary>
    private void _OnCollectorIdResolved(string oldId, string newId)
    {
        if (_connections.TryGetValue(oldId, out var conn))
        {
            _connections.Remove(oldId);
            _connections[newId] = conn;
        }

        // endpoint.Id 는 이미 CollectorConnection 내부에서 갱신된 동일 참조 객체이므로
        // Settings.Collectors 목록의 값도 이미 반영되어 있음 — 저장만 수행.
        _ = _settingsLoader.SaveAsync();
    }

    // §4 ─ 정리 ────────────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        foreach (var conn in _connections.Values)
            await conn.DisposeAsync();

        _connections.Clear();
    }
}
