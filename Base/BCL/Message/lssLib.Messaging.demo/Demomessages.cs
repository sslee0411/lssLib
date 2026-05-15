using lssLib.Messaging;

namespace lssLib.messaging.demo
{

    // ══════════════════════════════════════════════════════════
    //  §1 이벤트 메시지 — EventBus 발행/구독용
    // ══════════════════════════════════════════════════════════

    /// <summary>센서 데이터 이벤트</summary>
    public record SensorDataEvent(
    int DeviceId,
    float Temperature,
    float Humidity,
    int Battery) : EventMessage;

    /// <summary>네트워크 상태 변경 이벤트</summary>
    public record NetworkStatusEvent(
        bool IsConnected,
        string Host,
        int Latency = 0) : EventMessage;

    /// <summary>알람 발생 이벤트</summary>
    public record AlarmEvent(
        string Source,
        string Message,
        bool IsCritical = false) : EventMessage;

    /// <summary>시스템 상태 이벤트</summary>
    public record SystemStatusEvent(
        string Component,
        string Status,
        string Detail = "") : EventMessage;


    // ══════════════════════════════════════════════════════════
    //  §2 커맨드 — CommandQueue 처리용
    // ══════════════════════════════════════════════════════════

    /// <summary>데이터 처리 커맨드 (Normal)</summary>
    public class ProcessDataCommand : CommandBase
    {
        private readonly int _id;
        private readonly float _value;
        private readonly int _delayMs;

        public override CommandPriority Priority => CommandPriority.Normal;

        public ProcessDataCommand(int id, float value, int delayMs = 300)
        {
            _id = id;
            _value = value;
            _delayMs = delayMs;
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(_delayMs, ct);   // 처리 시뮬레이션
                                              //    LogManager.Instance.Info("ProcessData",
                                              //        $"데이터 처리 완료 — ID={_id}  값={_value:F2}");
        }
    }

    /// <summary>파일 저장 커맨드 (Low)</summary>
    public class SaveFileCommand : CommandBase
    {
        private readonly string _fileName;
        private readonly int _delayMs;

        public override CommandPriority Priority => CommandPriority.Low;

        public SaveFileCommand(string fileName, int delayMs = 500)
        {
            _fileName = fileName;
            _delayMs = delayMs;
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(_delayMs, ct);   // 파일 I/O 시뮬레이션
                                              //    LogManager.Instance.Info("SaveFile",
                                              //        $"파일 저장 완료 — {_fileName}");
        }
    }

    /// <summary>알림 전송 커맨드 (High)</summary>
    public class SendNotificationCommand : CommandBase
    {
        private readonly string _message;
        private readonly int _delayMs;

        public override CommandPriority Priority => CommandPriority.High;

        public SendNotificationCommand(string message, int delayMs = 150)
        {
            _message = message;
            _delayMs = delayMs;
        }

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(_delayMs, ct);
            //    LogManager.Instance.Info("Notification",
            //        $"알림 전송 완료 — {_message}");
        }
    }

    /// <summary>비상 정지 커맨드 (Critical)</summary>
    public class EmergencyStopCommand : CommandBase
    {
        private readonly string _reason;

        public override CommandPriority Priority => CommandPriority.Critical;

        public EmergencyStopCommand(string reason) => _reason = reason;

        public override async Task ExecuteAsync(CancellationToken ct)
        {
            await Task.Delay(50, ct);
            //    LogManager.Instance.Fatal("EmergencyStop",
            //        $"비상 정지 실행 — 사유: {_reason}");
        }
    }
}