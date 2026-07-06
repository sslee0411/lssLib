// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Notification/NotificationService.cs
//  역할: 알람 알림 발송 — 이메일(SmtpClient) / SMS·Webhook(REST POST)
//        settings.json Notification 섹션 기준으로 실제 발송 여부 결정
//  C-14: 신규
//  생성: 2026-07-05
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using lssLib.Log;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace IIoT.Collector.Notification;

/// <summary>
/// 알람 알림 발송 서비스 (DI 싱글턴).
/// <para>
/// Notification.Enabled = false 이면 이메일 발송을 생략한다.<br/>
/// Notification.Webhook.Enabled = false 이면 Webhook 발송을 생략한다.<br/>
/// 두 경우 모두 예외를 던지지 않고 경고 로그만 남긴다 (알림 실패가 수집 파이프라인에 영향 없도록).
/// </para>
/// </summary>
public sealed class NotificationService
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorSettingsLoader _settingsLoader;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public NotificationService(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §3 ─ 이메일 발송 ─────────────────────────────────────

    /// <summary>
    /// 이메일을 발송합니다. toAddresses 는 쉼표(,) 또는 세미콜론(;)으로 구분된 다중 주소 가능.
    /// </summary>
    public async Task SendEmailAsync(string toAddresses, string subject, string body, CancellationToken ct = default)
    {
        var notif = _settingsLoader.Settings.Notification;

        if (!notif.Enabled)
        {
            LogManager.Instance.Info("Notify", "알림 비활성화 상태 — 이메일 발송 생략");
            return;
        }

        if (string.IsNullOrWhiteSpace(toAddresses)) return;

        var recipients = toAddresses
            .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (recipients.Length == 0) return;

        try
        {
            var smtp = notif.Smtp;

            using var client = new SmtpClient(smtp.Host, smtp.Port)
            {
                EnableSsl = smtp.UseSsl,
                Credentials = new NetworkCredential(smtp.User, smtp.Password)
            };

            using var msg = new MailMessage
            {
                From = new MailAddress(smtp.FromAddress, smtp.FromName),
                Subject = subject,
                Body = body,
                IsBodyHtml = false
            };
            foreach (var addr in recipients) msg.To.Add(addr);

            await client.SendMailAsync(msg, ct);

            LogManager.Instance.Info("Notify",
                $"이메일 발송 완료 → {string.Join(",", recipients)} : {subject}");
        }
        catch (Exception ex)
        {
            // 알림 실패가 수집 파이프라인을 중단시키지 않도록 예외를 삼키고 로그만 남긴다
            LogManager.Instance.Warn("Notify", $"이메일 발송 실패: {ex.Message}");
        }
    }

    // §4 ─ Webhook(SMS 등) 발송 ────────────────────────────

    /// <summary>
    /// Webhook 으로 알림 메시지를 POST 합니다.
    /// target 은 전화번호 또는 서비스별 수신자 식별자 (본문에 그대로 포함되어 전송됨).
    /// </summary>
    public async Task SendWebhookAsync(string target, string message, CancellationToken ct = default)
    {
        var wh = _settingsLoader.Settings.Notification.Webhook;

        if (!wh.Enabled || string.IsNullOrWhiteSpace(wh.Url))
        {
            LogManager.Instance.Info("Notify", "Webhook 비활성화 상태 — 발송 생략");
            return;
        }

        if (string.IsNullOrWhiteSpace(target)) return;

        try
        {
            using var http = new HttpClient();

            var payload = JsonSerializer.Serialize(new
            {
                target,
                message,
                ts = DateTimeOffset.UtcNow
            });

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            var resp = await http.PostAsync(wh.Url, content, ct);

            if (!resp.IsSuccessStatusCode)
                LogManager.Instance.Warn("Notify", $"Webhook 발송 실패: HTTP {(int)resp.StatusCode}");
            else
                LogManager.Instance.Info("Notify", $"Webhook 발송 완료 → {target}");
        }
        catch (Exception ex)
        {
            LogManager.Instance.Warn("Notify", $"Webhook 발송 실패: {ex.Message}");
        }
    }
}