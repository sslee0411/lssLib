// ══════════════════════════════════════════════════════════
//  IIoT.Monitor · Core/Notification/TrayNotificationService.cs
//  역할: ① 새 알람 발생 시 시스템 사운드 재생 + Windows 트레이 풍선 알림 표시
//        ② MN-EX-03: 트레이 상주 — 더블클릭/컨텍스트메뉴로 창 열기, 메뉴로 종료
//        System.Windows.Forms.NotifyIcon 사용 (WPF엔 트레이 아이콘 API가 없음).
//        ★ 주의: 이 파일에서는 "using System.Windows.Forms;" 만 단독 사용 —
//          System.Windows(WPF) 네임스페이스와 겹치는 타입명(MessageBox 등)이
//          있어 같은 파일에서 함께 열면 모호한 참조 컴파일 오류가 발생한다.
//  MN-EX-01: 신규
//  MN-EX-03: RestoreRequested/ExitRequested 이벤트 + 컨텍스트메뉴 추가
//  생성: 2026-07-08 / 수정: 2026-07-08 (MN-EX-03)
// ══════════════════════════════════════════════════════════

using lssLib.Log;
using System.Media;
using System.Windows.Forms;

namespace IIoT.Monitor.Core.Notification;

/// <summary>
/// 알람 발생 알림(사운드+트레이 풍선) + 트레이 상주 동작을 담당하는 DI 싱글턴.
/// <para>
/// App.xaml.cs 에서 <see cref="Initialize"/> 호출 후, AlarmAggregator.NewAlarmCreated
/// 이벤트에 <see cref="NotifyNewAlarm"/> 를 구독시켜 사용한다.
/// MainWindow 는 <see cref="RestoreRequested"/>/<see cref="ExitRequested"/> 를
/// 구독하여 창 표시/종료를 처리한다.
/// </para>
/// </summary>
public sealed class TrayNotificationService : IDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private NotifyIcon? _icon;

    // §2 ─ 이벤트 (MN-EX-03) ───────────────────────────────

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
                Icon            = System.Drawing.SystemIcons.Information, // 별도 .ico 리소스 불필요(시스템 기본 아이콘)
                Visible         = true,
                Text            = "IIoT.Monitor",
                ContextMenuStrip = menu
            };

            // ★ MN-EX-03: 더블클릭 시 창 복원
            _icon.DoubleClick += (_, _) => RestoreRequested?.Invoke();

            LogManager.Instance.Info("TrayNotification", "트레이 아이콘 초기화 완료");
        }
        catch (Exception ex)
        {
            // 트레이 아이콘 생성 실패는 치명적이지 않음(예: 헤드리스 환경) — 앱은 계속 동작
            LogManager.Instance.Warn("TrayNotification", $"트레이 아이콘 초기화 실패: {ex.Message}");
        }
    }

    // §4 ─ 알림 ────────────────────────────────────────────

    /// <summary>새 알람 발생 시 사운드 재생 + 트레이 풍선 알림을 표시한다.</summary>
    /// <param name="level">알람 레벨 (HH/H/L/LL) — 풍선 아이콘 색상 결정에 사용</param>
    /// <param name="title">풍선 알림 제목</param>
    /// <param name="message">풍선 알림 본문</param>
    public void NotifyNewAlarm(string level, string title, string message)
    {
        try
        {
            // HH/H(위험·경고) 는 경고음, L/LL(주의·참고) 은 낮은 알림음
            if (level is "HH" or "H")
                SystemSounds.Exclamation.Play();
            else
                SystemSounds.Asterisk.Play();

            var icon = level is "HH" or "H" ? ToolTipIcon.Warning : ToolTipIcon.Info;

            _icon?.ShowBalloonTip(4000, title, message, icon);
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("TrayNotification", $"알람 알림 표시 실패: {ex.Message}");
        }
    }

    // §5 ─ 정리 ────────────────────────────────────────────

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
