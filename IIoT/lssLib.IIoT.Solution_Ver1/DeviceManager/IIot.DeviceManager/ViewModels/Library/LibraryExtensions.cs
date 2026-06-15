// ══════════════════════════════════════════════════════════
//  IIoT.DeviceManager · ViewModels/Library/LibraryExtensions.cs
//  역할: 라이브러리 ViewModel 에 SelectById 확장 추가
//        트리 노드 선택 시 연결된 항목을 라이브러리에서 자동 선택
//  Phase 5R: 트리 ↔ 라이브러리 연계 핵심
// ══════════════════════════════════════════════════════════

using IIoT.DeviceManager.Core.Config;
using IIoT.DeviceManager.Core.DataModel;
using lssLib.Log;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace IIoT.DeviceManager.ViewModels.Library;

/// <summary>
/// ScaleLibraryViewModel 확장 — SelectById 추가.
/// Phase 5R: 기존 ScaleLibraryViewModel.cs 에 아래 메서드 추가하거나
///           이 파일의 partial 클래스로 확장합니다.
///
/// ★ 기존 ScaleLibraryViewModel.cs 에 아래 두 가지를 추가하세요:
///   1. class 선언에 partial 키워드 추가
///   2. 이 파일 참조 (또는 직접 메서드 추가)
/// </summary>
public partial class ScaleLibraryViewModel
{
    /// <summary>
    /// 지정 ID 의 스케일 항목을 선택합니다.
    /// Sensor 노드 선택 시 트리에서 자동 호출됩니다.
    /// </summary>
    /// <param name="id">ScaleConfig.Id</param>
    public void SelectById(string? id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item is not null)
            SelectedItem = item;
    }

    /// <summary>현재 선택된 스케일의 ID (없으면 null)</summary>
    public string? SelectedId => SelectedItem?.Id;
}

/// <summary>
/// AlarmLibraryViewModel 확장 — SelectById 추가.
/// ★ 기존 AlarmLibraryViewModel.cs 에 partial 키워드 추가 필요.
/// </summary>
public partial class AlarmLibraryViewModel
{
    /// <summary>
    /// 지정 ID 의 알람 규칙 항목을 선택합니다.
    /// Sensor 노드 선택 시 트리에서 자동 호출됩니다.
    /// </summary>
    public void SelectById(string? id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item is not null)
            SelectedItem = item;
    }

    public string? SelectedId => SelectedItem?.Id;
}

/// <summary>
/// CommLibraryViewModel 확장 — SelectById 추가.
/// ★ 기존 CommLibraryViewModel.cs 에 partial 키워드 추가 필요.
/// </summary>
public partial class CommLibraryViewModel
{
    /// <summary>
    /// 지정 ID 의 통신 설정 항목을 선택합니다.
    /// Device 노드 선택 시 트리에서 자동 호출됩니다.
    /// </summary>
    public void SelectById(string? id)
    {
        if (string.IsNullOrEmpty(id)) return;
        var item = Items.FirstOrDefault(i => i.Id == id);
        if (item is not null)
            SelectedItem = item;
    }

    public string? SelectedId => SelectedItem?.Id;
}
