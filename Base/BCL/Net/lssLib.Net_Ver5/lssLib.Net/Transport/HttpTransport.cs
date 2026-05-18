// ══════════════════════════════════════════════════════════════════════
//  lssLib.Net · Transport/HttpTransport.cs  [v5.1]
//  SequenceMode = 0 (Parallel) — HTTP 비연결 기본값
// ══════════════════════════════════════════════════════════════════════

using System.Net.Http;
using System.Net.Http.Headers;

namespace lssLib.Net;

/// <summary>HTTP REST API 전송 계층.</summary>
public sealed class HttpTransport : NetTransportBase
{
    #region §1 ─ 필드

    private readonly string _strBaseUrl;
    private readonly string _strWriteEndpoint;
    private readonly string _strReadEndpoint;
    private readonly string _strContentType;
    private readonly string? _strBearerToken;
    private readonly TimeSpan _httpTimeout;

    private static readonly HttpClient _sharedClient = new();

    #endregion

    #region §2 ─ 생성자 / 팩토리

    public HttpTransport(string baseUrl,
        string writeEndpoint = "/api/write",
        string readEndpoint = "/api/read",
        string contentType = "application/octet-stream",
        string? bearerToken = null,
        TimeSpan? httpTimeout = null)
    {
        _strBaseUrl = baseUrl.TrimEnd('/');
        _strWriteEndpoint = writeEndpoint;
        _strReadEndpoint = readEndpoint;
        _strContentType = contentType;
        _strBearerToken = bearerToken;
        _httpTimeout = httpTimeout ?? TimeSpan.FromSeconds(10);
    }

    public static HttpTransport FromConfig(HttpDeviceConfig cfg)
        => new(cfg.BaseUrl, cfg.WriteEndpoint, cfg.ReadEndpoint,
               cfg.ContentType, cfg.BearerToken, cfg.HttpTimeout)
        { LogSource = cfg.DeviceName };

    #endregion

    #region §3 ─ NetTransportBase 구현

    protected override async Task ConnectCoreAsync(CancellationToken ct)
    {
        using var req = BuildRequest(HttpMethod.Get, _strReadEndpoint, null);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_httpTimeout);
        var resp = await _sharedClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    protected override Task DisconnectCoreAsync(CancellationToken ct)
        => Task.CompletedTask;

    protected override async Task WriteCoreAsync(byte[] data, CancellationToken ct)
    {
        using var req = BuildRequest(HttpMethod.Post, _strWriteEndpoint, data);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_httpTimeout);
        var resp = await _sharedClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
    }

    protected override async Task<byte[]> ReadCoreAsync(int length, CancellationToken ct)
    {
        using var req = BuildRequest(HttpMethod.Get, _strReadEndpoint, null);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(_httpTimeout);
        var resp = await _sharedClient.SendAsync(req, timeoutCts.Token).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    protected override void DisposeCore() { }

    #endregion

    #region §4 ─ 헬퍼

    private HttpRequestMessage BuildRequest(HttpMethod method, string endpoint, byte[]? body)
    {
        var req = new HttpRequestMessage(method, $"{_strBaseUrl}{endpoint}");
        if (_strBearerToken is not null)
            req.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", _strBearerToken);
        if (body is not null)
            req.Content = new ByteArrayContent(body)
            {
                Headers = { ContentType = new MediaTypeHeaderValue(_strContentType) }
            };
        return req;
    }

    #endregion
}