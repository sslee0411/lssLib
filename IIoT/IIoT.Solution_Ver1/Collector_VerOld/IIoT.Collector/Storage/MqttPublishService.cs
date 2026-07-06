// ══════════════════════════════════════════════════════════
//  IIoT.Collector · Storage/MqttPublishService.cs
//  역할: TagValueUpdatedEvent / AlarmChangedEvent → MQTT 브로커 발행
//        lssLib.Net.MqttTransport 사용 (추가 NuGet 패키지 없음)
//
//  ━━━ MQTT 브로커 환경 구성 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//
//  [설치 — Mosquitto (Windows, 권장)]
//  1. https://mosquitto.org/download/ → Windows 인스톨러 다운로드
//  2. 설치 후 서비스 자동 시작 (기본 포트 1883)
//  3. 테스트:
//     mosquitto_sub -t "iiot/#" -v      ← 수신 터미널
//     mosquitto_pub -t "iiot/test" -m "hello"  ← 발행 터미널
//
//  [설치 — Docker]
//  docker run -d --name mosquitto -p 1883:1883 eclipse-mosquitto
//
//  [설치 — EMQX (대시보드 포함)]
//  docker run -d --name emqx -p 1883:1883 -p 18083:18083 emqx/emqx
//  브라우저 → http://localhost:18083 (admin/public)
//
//  ━━━ settings.json MQTT 설정 ━━━━━━━━━━━━━━━━━━━━━━━━━━
//  {
//    "Storage": {
//      "Mqtt": {
//        "Enabled": true,
//        "BrokerHost": "localhost",
//        "BrokerPort": 1883,
//        "TopicPrefix": "iiot",
//        "QoS": 1
//      }
//    }
//  }
//
//  ━━━ MQTT 토픽 구조 ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  Tag 값:  {TopicPrefix}/{PlcId}/{TagId}
//    예)    iiot/PLC-01/T001
//
//  알람:   {TopicPrefix}/alarm/{PlcId}/{TagId}
//    예)    iiot/alarm/PLC-01/T001
//
//  ━━━ 페이로드 포맷 (JSON) ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  Tag 값:
//    { "tagId":"T001", "plcId":"PLC-01", "rawValue":1234.5,
//      "engValue":12.3, "unit":"bar", "quality":"Good",
//      "ts":"2026-06-29T12:00:00.000Z" }
//
//  알람:
//    { "alarmKey":"T001:HH", "tagId":"T001", "tagName":"압력",
//      "level":"HH", "status":"Active", "message":"압력 위험",
//      "engValue":95.2, "ts":"2026-06-29T12:00:00.000Z" }
//
//  ━━━ Monitor 수신 예시 (C-11 SignalR 완성 전 테스트용) ━━
//  mosquitto_sub -t "iiot/#" -v
//  → iiot/PLC-01/T001 {"tagId":"T001",...}
//  → iiot/alarm/PLC-01/T001 {"alarmKey":"T001:HH",...}
//  ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
//  C-10: 신규
//  생성: 2026-06-29
// ══════════════════════════════════════════════════════════

using IIoT.Collector.Core.Config;
using IIoT.Collector.Core.Events;
using lssLib.Log;
using lssLib.Messaging;
using lssLib.Net;
using System.Text;
using System.Text.Json;

namespace IIoT.Collector.Storage;

/// <summary>
/// MQTT 발행 서비스 (DI 싱글턴).
/// <para>
/// settings.json 의 Storage.Mqtt.Enabled = true 일 때만 활성화된다.
/// false 이면 Initialize() 에서 즉시 반환하여 수집에 영향을 주지 않는다.
/// </para>
/// <para>
/// <b>lssLib.Net.MqttTransport</b> 사용:
/// MQTTnet 등 외부 패키지 없이 BCL TCP 기반 직접 구현을 사용한다.
/// MQTT 3.1.1 표준, QoS 0/1 지원.
/// </para>
/// </summary>
public sealed class MqttPublishService : IAsyncDisposable
{
    // §1 ─ 필드 ────────────────────────────────────────────

    private readonly CollectorSettingsLoader _settingsLoader;

    private MqttTransport? _transport;
    private MqttDeviceConfig? _mqttCfg;

    private IDisposable? _tagValueSub;
    private IDisposable? _alarmSub;

    private bool _isEnabled;

    // §2 ─ 생성자 ──────────────────────────────────────────

    public MqttPublishService(CollectorSettingsLoader settingsLoader)
    {
        _settingsLoader = settingsLoader;
    }

    // §3 ─ 초기화 ──────────────────────────────────────────

    /// <summary>
    /// MQTT 브로커에 연결하고 EventBus 구독을 시작합니다.
    /// Enabled=false 이면 즉시 반환합니다.
    /// App.xaml.cs 에서 FlowEngine.StartAsync() 이후 호출.
    /// </summary>
    public async Task InitializeAsync()
    {
        var s = _settingsLoader.Settings.Storage.Mqtt;

        if (!s.Enabled)
        {
            LogManager.Instance.Info("MQTT",
                "MQTT 발행 비활성화 (settings.json Mqtt.Enabled=false)");
            _isEnabled = false;
            return;
        }

        try
        {
            _mqttCfg = new MqttDeviceConfig(
                deviceId:   1,
                deviceName: "IIoT.Collector",
                brokerHost: s.BrokerHost,
                brokerPort: s.BrokerPort)
            {
                ClientId  = s.ClientId ?? $"iiot-collector-{Environment.MachineName}",
                QoS       = s.QoS,
                Username  = s.Username,
                Password  = s.Password,
                // ★ PublishTopic 은 WriteCoreAsync 에서 사용되는 기본 토픽
                //   실제로는 SendPublishAsync 직접 호출로 토픽을 동적으로 지정
                PublishTopic = $"{s.TopicPrefix}/collector"
            };

            _transport = new MqttTransport(_mqttCfg);
            await _transport.ConnectAsync(CancellationToken.None);

            _isEnabled = true;

            // EventBus 구독 시작
            _tagValueSub = EventBus.Instance.Subscribe<TagValueUpdatedEvent>(_OnTagValue);
            _alarmSub    = EventBus.Instance.Subscribe<AlarmChangedEvent>(_OnAlarmChanged);

            LogManager.Instance.Info("MQTT",
                $"MQTT 브로커 연결 완료: {s.BrokerHost}:{s.BrokerPort} " +
                $"(ClientId={_mqttCfg.ClientId}, QoS={s.QoS})");
        }
        catch (Exception ex)
        {
            _isEnabled = false;
            LogManager.Instance.Warn("MQTT",
                $"MQTT 브로커 연결 실패 — 발행 비활성화 (수집은 계속): {ex.Message}");
        }
    }

    // §4 ─ Tag 값 발행 ─────────────────────────────────────

    private void _OnTagValue(TagValueUpdatedEvent e)
    {
        if (!_isEnabled || _transport is null) return;

        var s     = _settingsLoader.Settings.Storage.Mqtt;
        var topic = $"{s.TopicPrefix}/{e.PlcId}/{e.Value.TagId}";

        var payload = new
        {
            tagId    = e.Value.TagId,
            plcId    = e.PlcId,
            rawValue = e.Value.RawValue is double d ? d : 0.0,
            engValue = e.EngValue,
            unit     = e.Unit,
            quality  = e.Value.Quality.ToString(),
            ts       = e.Value.Timestamp.ToString("O")
        };

        _ = _PublishAsync(topic, payload);
    }

    // §5 ─ 알람 발행 ───────────────────────────────────────

    private void _OnAlarmChanged(AlarmChangedEvent e)
    {
        if (!_isEnabled || _transport is null) return;

        var s     = _settingsLoader.Settings.Storage.Mqtt;
        var topic = $"{s.TopicPrefix}/alarm/{e.PlcId}/{e.TagId}";

        var payload = new
        {
            alarmKey = e.AlarmKey,
            tagId    = e.TagId,
            tagName  = e.TagName,
            plcId    = e.PlcId,
            level    = e.Level.ToString(),
            status   = e.Status.ToString(),
            message  = e.Message,
            engValue = e.CurrentEngValue,
            ts       = e.OccurredAt.ToString("O")
        };

        _ = _PublishAsync(topic, payload);
    }

    // §6 ─ 발행 공통 로직 ──────────────────────────────────

    /// <summary>
    /// JSON 직렬화 후 MQTT 발행 (fire-and-forget).
    /// 실패 시 경고 로그만 남기고 수집에 영향 없음.
    /// </summary>
    private async Task _PublishAsync(string topic, object payload)
    {
        try
        {
            if (_transport is null || !_isEnabled) return;

            var json  = JsonSerializer.Serialize(payload);
            var bytes = Encoding.UTF8.GetBytes(json);

            // lssLib.Net MqttTransport — WriteAsync 는 설정된 PublishTopic 으로 전송
            // 동적 토픽은 MqttTransport 내부의 SendPublishAsync 를 직접 활용하기 위해
            // PublishTopic 을 교체 후 전송하는 방식 사용
            // ★ MqttDeviceConfig.PublishTopic 은 참조 타입 — 변경 후 복원
            var original = _mqttCfg!.PublishTopic;
            _mqttCfg.PublishTopic = topic;
            await _transport.WriteAsync(bytes, CancellationToken.None);
            _mqttCfg.PublishTopic = original;
        }
        catch (Exception ex)
        {
            // 발행 실패는 수집에 영향 없음 — 경고만 기록
            LogManager.Instance.Warn("MQTT", $"발행 실패 [{topic}]: {ex.Message}");
            _isEnabled = false; // 연결 끊김 — 재연결은 C-12 Auto-Reconnect 에서 처리
        }
    }

    // §7 ─ 리소스 해제 ─────────────────────────────────────

    public async ValueTask DisposeAsync()
    {
        _tagValueSub?.Dispose();
        _alarmSub?.Dispose();

        if (_transport is not null)
        {
            try { await _transport.DisconnectAsync(CancellationToken.None); }
            catch { /* 종료 중 오류 무시 */ }
        }

        _isEnabled = false;
        LogManager.Instance.Info("MQTT", "MQTT 서비스 종료 완료");
    }
}
