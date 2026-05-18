namespace lssLib.Net;


/// <summary>HTTP REST API 통신 장비 설정.</summary>
/// <example><code>
/// var cfg = new HttpDeviceConfig(6, "REST-API", "http://192.168.1.10:8080")
/// {
///     WriteEndpoint    = "/api/command",
///     ReadEndpoint     = "/api/status",
///     ContentType      = "application/json",
///     PeriodicInterval = TimeSpan.FromSeconds(1),
///     // SequenceMode = 0 (Parallel) ← 기본값
///     // SequenceMode = 2  → 최대 2개 동시 GET 요청 슬라이딩 윈도우
/// };
/// </code></example>
public sealed class HttpDeviceConfig : NetDeviceConfig
{
    /// <summary>
    /// 전송 계층 유형. NetTransportType 열거형으로 구분됩니다.
    /// </summary>
    public override NetTransportType TransportType => NetTransportType.Http;

    /// <summary>
    /// Address 
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// 쓰기 엔드포인트. BaseUrl 뒤에 붙는 경로입니다. 
    /// 예: "/api/write", "/command" 등. GET/POST 등 HTTP 메서드와는 별개로, 
    /// 쓰기 요청 시 이 엔드포인트로 POST 요청이 전송됩니다.
    /// </summary>
    public string WriteEndpoint { get; set; } = "/api/write";

    /// <summary>
    /// 읽기 엔드포인트. BaseUrl 뒤에 붙는 경로입니다.
    /// </summary>
    public string ReadEndpoint { get; set; } = "/api/read";

    /// <summary>
    /// HTTP 요청 본문의 콘텐츠 유형(Content-Type)입니다. 
    /// 기본값은 "application/octet-stream"입니다.
    /// </summary>
    public string ContentType { get; set; } = "application/octet-stream";

    /// <summary>
    /// Bearer 토큰 인증을 사용하는 경우, 
    /// 이 속성에 토큰 문자열을 설정할 수 있습니다.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// HTTP 요청 타임아웃 시간입니다. 기본값은 10초입니다. 
    /// 이 시간 내에 응답이 수신되지 않으면 요청이 실패로 간주됩니다.
    /// </summary>
    public TimeSpan HttpTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public HttpDeviceConfig(int deviceId, string deviceName, string baseUrl)
        : base(deviceId, deviceName)
    {
        BaseUrl = baseUrl;
        IsRetryEnabled = true;
        RetryTarget = RetryTarget.ConnectAndWrite;
        SequenceMode = NetDeviceConfig.SequenceModes.Parallel;  // 0: 병렬
        PeriodicInterval = TimeSpan.FromMilliseconds(500);
        RequestTimeout = TimeSpan.FromSeconds(10);
        HeartbeatInterval = TimeSpan.Zero;
    }

    public override string ToString()
        => base.ToString() + $" | {BaseUrl}";
}
