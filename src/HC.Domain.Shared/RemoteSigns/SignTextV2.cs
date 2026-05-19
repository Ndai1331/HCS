using System;
using System.Diagnostics;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace HC.RemoteSigns;

/// <summary>
/// REMOTE_CA (TAG-style) REST signing client calling /api/v2/pdf/sign/originaldata with HMAC-SHA256 Authorization.
/// </summary>
public sealed class SignTextV2
{
    private const string DefaultSignPath = "/api/v2/pdf/sign/originaldata";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _apiKey;
    private readonly string _secret;
    private readonly string _baseUri;
    private readonly TimeSpan _timeout;
    private readonly ILogger<SignTextV2>? _logger;

    public SignTextV2(
        string apiKey,
        string secret,
        string baseUriWithoutPath,
        TimeSpan? requestTimeout = null,
        ILogger<SignTextV2>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            throw new ArgumentException("Api key is required.", nameof(apiKey));
        }

        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Secret is required.", nameof(secret));
        }

        if (string.IsNullOrWhiteSpace(baseUriWithoutPath))
        {
            throw new ArgumentException("Base URI is required.", nameof(baseUriWithoutPath));
        }

        _apiKey = apiKey.Trim();
        _secret = secret.Trim();
        _baseUri = NormalizeRemoteSignBaseUri(baseUriWithoutPath);
        _timeout = requestTimeout ?? TimeSpan.FromSeconds(100);
        _logger = logger;
    }

    public async Task<byte[]?> SignatureAsync(PdfSignRequest body, CancellationToken cancellationToken = default)
    {
        var url = _baseUri + DefaultSignPath;
        var bodyJson = JsonSerializer.Serialize(body, JsonOptions);

        var now = DateTimeOffset.UtcNow;
        var dateHeader = now.ToString("r");
        var timestamp = now.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");

        byte[] secretKeyByteArray;
        try
        {
            secretKeyByteArray = Convert.FromBase64String(_secret);
        }
        catch (FormatException ex)
        {
            _logger?.LogError(ex, "[SIGN_V2] Secret must be Base64-encoded for REMOTE_CA HMAC.");
            throw;
        }

        var uriObj = new Uri(url);
        const string method = "POST";
        var scheme = uriObj.Scheme;
        var defaultPort = scheme == Uri.UriSchemeHttps ? 443 : 80;
        var hostPortUrl = $"{uriObj.Host}:{(uriObj.IsDefaultPort ? defaultPort : uriObj.Port)}";
        var hostPortStd = $"{uriObj.Host}:{defaultPort}";
        var link = uriObj.AbsolutePath;

        const string contentTypeWithCharset = "application/json; charset=utf-8";
        const string contentTypePlain = "application/json";
        var contentTypes = new[] { contentTypeWithCharset, contentTypePlain };
        var nls = new[] { "\n", "\r\n" };
        var hostPorts = new[] { hostPortUrl, hostPortStd };

        // Avoid hanging indefinitely on unreachable hosts; overall request bound by HttpClient.Timeout.
        var connectCaps = Math.Clamp(_timeout.TotalSeconds / 4.0, 5.0, 45.0);
        var connectTimeout = TimeSpan.FromSeconds(connectCaps);

        _logger?.LogInformation(
            "[SIGN_V2] Starting REMOTE_CA sign | Url={Url} | PayloadJsonChars≈{Len} | HttpTimeoutSeconds={Total}s | ConnectTimeoutSeconds={Conn}s",
            url,
            bodyJson.Length,
            _timeout.TotalSeconds,
            connectTimeout.TotalSeconds);

        using var handler = new SocketsHttpHandler
        {
            ConnectTimeout = connectTimeout,
        };

        using var http = new HttpClient(handler, disposeHandler: true) { Timeout = _timeout };

        var attemptIdx = 0;
        foreach (var ct in contentTypes)
        {
            foreach (var nl in nls)
            {
                foreach (var hp in hostPorts)
                {
                    attemptIdx++;
                    var signatureRaw =
                        method + nl
                               + scheme + nl
                               + hp + nl
                               + link + nl
                               + ct + nl
                               + _apiKey + nl
                               + nonce + nl
                               + dateHeader + nl
                               + bodyJson + nl;

                    byte[] digestBytes;
                    using (var algorithm = new HMACSHA256(secretKeyByteArray))
                    {
                        digestBytes = algorithm.ComputeHash(Encoding.UTF8.GetBytes(signatureRaw));
                    }

                    var digest = Convert.ToBase64String(digestBytes);
                    var authorizationValue = $"HmacSHA256 {_apiKey}:{nonce}:{digest}:{timestamp}";

                    using var request = new HttpRequestMessage(HttpMethod.Post, url);
                    request.Headers.TryAddWithoutValidation("Date", dateHeader);
                    request.Headers.TryAddWithoutValidation("Authorization", authorizationValue);
                    request.Headers.Accept.Add(
                        new MediaTypeWithQualityHeaderValue("application/json") { CharSet = Encoding.UTF8.WebName });

                    request.Content = new StringContent(bodyJson, Encoding.UTF8, "application/json");
                    request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(ct);

                    string respText = string.Empty;
                    var watch = Stopwatch.StartNew();
                    try
                    {
                        _logger?.LogDebug(
                            "[SIGN_V2] Attempt {Attempt} POST | Content={ContentTypeKey} HostPort={HostPort}",
                            attemptIdx,
                            ct == contentTypePlain ? "json" : "json-charset",
                            hp);

                        using var resp =
                            await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                      .ConfigureAwait(false);
                        watch.Stop();
                        respText = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                        _logger?.LogInformation(
                            "[SIGN_V2] Attempt {Attempt} completed in {ElapsedMs}ms | Status={Status}",
                            attemptIdx,
                            watch.ElapsedMilliseconds,
                            resp.StatusCode);

                        if (resp.IsSuccessStatusCode)
                        {
                            var signResp = JsonSerializer.Deserialize<PdfSignResponse>(respText)
                                           ?? new PdfSignResponse { status = -1, error = "Cannot parse response" };

                            if (signResp.status == 0 && !string.IsNullOrWhiteSpace(signResp.obj))
                            {
                                return Convert.FromBase64String(signResp.obj);
                            }

                            _logger?.LogWarning(
                                "[SIGN_V2] Attempt {Attempt} HTTP OK but payload status={SignStatus}, error={Error}",
                                attemptIdx,
                                signResp.status,
                                signResp.error);
                            continue;
                        }

                        var snippet = respText.Length > 500 ? respText[..500] + "…" : respText;
                        _logger?.LogWarning(
                            "[SIGN_V2] Attempt {Attempt} failed | Status={Status} | BodySnippet={Snippet}",
                            attemptIdx,
                            resp.StatusCode,
                            snippet);

                        if ((int)resp.StatusCode == 401)
                        {
                            _logger?.LogWarning(
                                "[SIGN_V2] Unauthorized (401). Date={Date} AuthPrefix={AuthPrefix}",
                                dateHeader,
                                authorizationValue[..Math.Min(80, authorizationValue.Length)]);
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        watch.Stop();
                        _logger?.LogWarning(ex,
                            "[SIGN_V2] Attempt {Attempt} cancelled/timed out after {ElapsedMs}ms (check endpoint, port, TLS http vs https, firewall)",
                            attemptIdx,
                            watch.ElapsedMilliseconds);
                        throw;
                    }
                }
            }
        }

        _logger?.LogError("[SIGN_V2] All signature/header combinations failed for {Url}", url);
        return null;
    }

    /// <summary>
    /// Ensures a valid absolute URI. Host-only values (e.g. <c>178.88.11.15</c> or <c>178.88.11.15:8443</c>)
    /// are prefixed with <c>http://</c>; trailing slashes are trimmed.
    /// </summary>
    internal static string NormalizeRemoteSignBaseUri(string raw)
    {
        var trimmed = raw.Trim().TrimEnd('/');
        if (string.IsNullOrEmpty(trimmed))
        {
            throw new ArgumentException("Base URI is required.", nameof(raw));
        }

        var withScheme =
            trimmed.StartsWith(Uri.UriSchemeHttp + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase)
            || trimmed.StartsWith(Uri.UriSchemeHttps + Uri.SchemeDelimiter, StringComparison.OrdinalIgnoreCase)
                ? trimmed
                : Uri.UriSchemeHttp + Uri.SchemeDelimiter + trimmed;

        if (!Uri.TryCreate(withScheme, UriKind.Absolute, out var absolute) ||
            string.IsNullOrWhiteSpace(absolute.Host))
        {
            throw new ArgumentException(
                $"Invalid REMOTE_CA API endpoint: '{raw}'. Use a full URL or host with optional port, e.g. https://178.88.11.15 or http://178.88.11.15:8443.",
                nameof(raw));
        }

        return withScheme.TrimEnd('/');
    }
}
