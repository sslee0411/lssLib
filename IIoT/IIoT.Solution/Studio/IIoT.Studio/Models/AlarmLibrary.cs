// ══════════════════════════════════════════════════════════
//  IIoT.Studio · Models/AlarmLibrary.cs
//  역할: 알람 라이브러리 항목 모델
//        HH / H / L / LL 4단계 임계값 설정
//  S-07: 초기 구현
//  생성: 2026-06-15
// ══════════════════════════════════════════════════════════

using CommunityToolkit.Mvvm.ComponentModel;

namespace IIoT.Studio.Models;

/// <summary>
/// 알람 라이브러리 항목.
/// Tag 에서 Id 로 참조.
/// HH(상상한) / H(상한) / L(하한) / LL(하하한) 4단계 임계값 설정.
/// </summary>
public partial class AlarmEntry : ObservableObject
{
    // §1 ─ 식별 ───────────────────────────────────────────────

    public Guid Id { get; } = Guid.NewGuid();

    // §2 ─ 기본 정보 ──────────────────────────────────────────

    [ObservableProperty]
    private string _name = "새 알람";

    [ObservableProperty]
    private string _description = string.Empty;

    // §3 ─ HH (상상한) ────────────────────────────────────────

    /// <summary>HH 알람 활성화</summary>
    [ObservableProperty]
    private bool _hhEnabled;

    /// <summary>HH 임계값</summary>
    [ObservableProperty]
    private double _hhValue = 100;

    /// <summary>HH 알람 메시지</summary>
    [ObservableProperty]
    private string _hhMessage = "상상한 초과";

    // §4 ─ H (상한) ───────────────────────────────────────────

    [ObservableProperty]
    private bool _hEnabled;

    [ObservableProperty]
    private double _hValue = 90;

    [ObservableProperty]
    private string _hMessage = "상한 초과";

    // §5 ─ L (하한) ───────────────────────────────────────────

    [ObservableProperty]
    private bool _lEnabled;

    [ObservableProperty]
    private double _lValue = 10;

    [ObservableProperty]
    private string _lMessage = "하한 미달";

    // §6 ─ LL (하하한) ────────────────────────────────────────

    [ObservableProperty]
    private bool _llEnabled;

    [ObservableProperty]
    private double _llValue;

    [ObservableProperty]
    private string _llMessage = "하하한 미달";

    // §7 ─ 딜레이 설정 ────────────────────────────────────────

    /// <summary>알람 딜레이 (ms) — 일시적 스파이크 무시</summary>
    [ObservableProperty]
    private int _delayMs;

    /// <summary>복귀 딜레이 (ms) — 복귀 후 확인 대기</summary>
    [ObservableProperty]
    private int _recoveryDelayMs;

    // §8 ─ 미리보기 텍스트 ────────────────────────────────────

    /// <summary>목록에 표시할 활성 단계 요약</summary>
    public string ActiveLevelsPreview
    {
        get
        {
            var levels = new List<string>();
            if (HhEnabled) levels.Add($"HH({HhValue})");
            if (HEnabled)  levels.Add($"H({HValue})");
            if (LEnabled)  levels.Add($"L({LValue})");
            if (LlEnabled) levels.Add($"LL({LlValue})");
            return levels.Count > 0
                ? string.Join(" / ", levels)
                : "설정 없음";
        }
    }
}
