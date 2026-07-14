// ══════════════════════════════════════════════════════════
//  IIoT.Manager · Core/Notification/TrayService.cs
//  역할: 트레이 상주 — 최소화 시 트레이로 숨김, 더블클릭/메뉴로 복원, 메뉴로 종료
//        System.Windows.Forms.NotifyIcon 사용 (WPF엔 트레이 아이콘 API가 없음).
//        ★ 주의: 이 파일에서는 "using System.Windows.Forms;" 만 단독 사용 —
//          System.Windows(WPF) 와 겹치는 타입명(MessageBox 등) 모호 참조 방지
//          (Monitor 버그 #12 교훈 — csproj 는 <Using Remove> 로 전역 using 제거됨)
//  MG-EX-01: 신규 (Monitor MN-EX-01/03 TrayNotificationService 패턴 이식)
//  MG-EX-02: NotifyEvent() 추가 — 경고 이벤트 발생 시 사운드+트레이 풍선 알림
//  생성: 2026-07-09 / 수정: 2026-07-09 (MG-EX-02)
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using System.Media;
using System.Windows.Forms;

namespace IIoT.Manager.Core.Notification;

/// <summary>
/// 트레이 상주 서비스 (DI 싱글턴).
/// <para>
/// App.xaml.cs 에서 <see cref="Initialize"/> 호출.
/// MainWindow 는 <see cref="RestoreRequested"/>/<see cref="ExitRequested"/> 를
/// 구독하여 창 표시/종료를 처리한다.
/// NotifyIcon 이벤트는 UI 스레드에서 발생 — 별도 마샬링 불필요.
/// </para>
/// </summary>
public sealed class TrayService : IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private NotifyIcon? _icon;

    // §2 ─ 이벤트 ──────────────────────────────────────────

    /// <summary>트레이 아이콘 더블클릭 또는 컨텍스트메뉴 "열기" 선택 시 발행</summary>
    public event Action? RestoreRequested;

    /// <summary>컨텍스트메뉴 "종료" 선택 시 발행</summary>
    public event Action? ExitRequested;

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>트레이 아이콘을 생성하고 표시한다. 앱 시작 시 1회 호출.</summary>
    public void Initialize()
    {
        try
        {
            var menu = new ContextMenuStrip();
            menu.Items.Add("열기", null, (_, _) => RestoreRequested?.Invoke());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("종료", null, (_, _) => ExitRequested?.Invoke());

            _icon = new NotifyIcon
            {
                Icon             = System.Drawing.SystemIcons.Application, // 별도 .ico 리소스 불필요
                Visible          = true,
                Text             = "IIoT.Manager — 통합 관리",
                ContextMenuStrip = menu
            };

            // 더블클릭 시 창 복원
            _icon.DoubleClick += (_, _) => RestoreRequested?.Invoke();

            LogManager.Instance.Info("Tray", "트레이 아이콘 초기화 완료");
        }
        catch (Exception ex)
        {
            // 트레이 생성 실패는 치명적이지 않음(예: 헤드리스 환경) — 앱은 계속 동작
            LogManager.Instance.Warn("Tray", $"트레이 아이콘 초기화 실패: {ex.Message}");
        }
    }

    // §4 ─ 알림 (MG-EX-02) ─────────────────────────────────

    /// <summary>
    /// 이벤트 알림 — 트레이 풍선 + (경고 시) 사운드.
    /// 자동복구·비정상 종료·스케줄 실패 등 중요 이벤트에 사용.
    /// </summary>
    /// <param name="title">풍선 제목</param>
    /// <param name="message">풍선 본문</param>
    /// <param name="warning">true = 경고음 + 경고 아이콘 / false = 무음 + 정보 아이콘</param>
    public void NotifyEvent(string title, string message, bool warning)
    {
        try
        {
            if (warning)
                SystemSounds.Exclamation.Play();

            var icon = warning ? ToolTipIcon.Warning : ToolTipIcon.Info;
            _icon?.ShowBalloonTip(4000, title, message, icon);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("Tray", $"이벤트 알림 표시 실패: {ex.Message}");
        }
    }

    // §5 ─ 정리 ────────────────────────────────────────────

    /// <summary>트레이 아이콘 정리 (미정리 시 유령 아이콘 잔류 — Monitor 교훈).</summary>
    public void Dispose()
    {
        if (_icon is not null)
        {
            _icon.Visible = false;
            _icon.Dispose();
            _icon = null;
        }
    }
}
