// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Core/Engine/AlarmShelvingService.cs
//  역할: 유지보수 등으로 특정 Tag 의 알람 "알림"만 일시 억제
//        (C-19 PauseCollection 과 다름 — 폴링/저장/화면 표시는 계속되고
//         이메일·Webhook·에스컬레이션 발송만 건너뜀)
//  C-EX-05: 신규
//  생성: 2026-07-06
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using System.Collections.Concurrent;

namespace IIoT.Collector.Core.Engine;

/// <summary>알람 억제(Shelving) 관리자 (DI 싱글턴).</summary>
public sealed class AlarmShelvingService
{
    /// <summary>TagId → 억제 해제 시각(UTC)</summary>
    private readonly ConcurrentDictionary<string, DateTimeOffset> _shelved = new();

    /// <summary>
    /// Tag 알람을 지정 시간(분) 동안 억제합니다. 이미 억제 중이면 시간을 갱신합니다.
    /// </summary>
    public void Shelve(string tagId, int minutes)
    {
        var until = DateTimeOffset.UtcNow.AddMinutes(minutes);
        _shelved[tagId] = until;
        LogManager.Instance.Info("Shelving", $"[{tagId}] 알람 억제 시작 — {until:yyyy-MM-dd HH:mm} 까지");
    }

    /// <summary>억제를 즉시 해제합니다.</summary>
    public void Unshelve(string tagId)
    {
        if (_shelved.TryRemove(tagId, out _))
            LogManager.Instance.Info("Shelving", $"[{tagId}] 알람 억제 해제");
    }

    /// <summary>현재 억제 중인지 확인합니다 (만료 시 자동으로 false 처리 + 정리).</summary>
    public bool IsShelved(string tagId)
    {
        if (!_shelved.TryGetValue(tagId, out var until)) return false;

        if (DateTimeOffset.UtcNow >= until)
        {
            _shelved.TryRemove(tagId, out _);
            return false;
        }

        return true;
    }

    /// <summary>현재 억제 중인 TagId → 해제 예정 시각 전체 조회 (UI 표시용)</summary>
    public IReadOnlyDictionary<string, DateTimeOffset> GetAll() => _shelved;
}
