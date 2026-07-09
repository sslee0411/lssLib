// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Favorites/FavoriteTagService.cs
//  역할: Tag 즐겨찾기(핀 고정) 상태를 관리하고 monitor.json 에 영구 저장한다.
//        MonitorSettingsLoader.Settings.FavoriteTagKeys 를 직접 읽고/쓰므로
//        별도 캐시가 없다 — 초기화 순서(설정 로드 시점)에 의존하지 않는
//        가장 단순하고 안전한 구조.
//  MN-EX-05: 신규
//  생성: 2026-07-08
// ══════════════════════════════════════════════════════════

using IIoT.Monitor.Core.Config;
using IIoT.Monitor.Models;

namespace IIoT.Monitor.Core.Favorites;

/// <summary>Tag 즐겨찾기(핀 고정) 관리 서비스 (DI 싱글턴).</summary>
public sealed class FavoriteTagService
{
    private readonly MonitorSettingsLoader _settingsLoader;

    public FavoriteTagService(MonitorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    /// <summary>
    /// 새로 생성된 LiveTagRow 에 저장된 즐겨찾기 상태를 적용한다.
    /// LiveTagAggregator 가 새 행을 만들 때마다 호출한다.
    /// </summary>
    public void ApplyFavoriteState(LiveTagRow row)
    {
        row.IsFavorite = _settingsLoader.Settings.FavoriteTagKeys.Contains(row.RowKey);
    }

    /// <summary>즐겨찾기 상태를 토글하고 monitor.json 에 즉시 저장한다.</summary>
    public async Task ToggleAsync(LiveTagRow row)
    {
        var keys = _settingsLoader.Settings.FavoriteTagKeys;

        if (keys.Contains(row.RowKey))
        {
            keys.Remove(row.RowKey);
            row.IsFavorite = false;
        }
        else
        {
            keys.Add(row.RowKey);
            row.IsFavorite = true;
        }

        await _settingsLoader.SaveAsync();
    }
}
