using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Domain.ValueObjects;
using PdfToolStack.Infrastructure.Configuration;

namespace PdfToolStack.Infrastructure.Services;

/// <summary>
/// Microsoft OneDrive implementation of ICloudStorageService.
/// Uses OAuth2 Authorization Code flow with PKCE — fully server-side.
/// No browser tokens. No MSAL. No popup conflicts.
/// </summary>
public sealed class OneDriveService : ICloudStorageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly OneDriveOptions _options;
    private readonly ILogger<OneDriveService> _logger;

    private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

    public OneDriveService(
        IHttpClientFactory httpClientFactory,
        IOptions<OneDriveOptions> options,
        ILogger<OneDriveService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string GetAuthorizationUrl(
        string state, string redirectUri, string codeChallenge)
    {
        var queryParams = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["response_type"] = "code",
            ["redirect_uri"] = redirectUri,
            ["scope"] = string.Join(" ", _options.Scopes),
            ["state"] = state,
            ["response_mode"] = "query",
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256"
        };

        var query = string.Join("&", queryParams.Select(kv =>
            $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));

        return $"{_options.AuthorizationEndpoint}?{query}";
    }

    /// <inheritdoc />
    public async Task<string?> ExchangeCodeForTokenAsync(
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("OneDrive");

        var formData = new Dictionary<string, string>
        {
            ["client_id"] = _options.ClientId,
            ["code"] = code,
            ["redirect_uri"] = redirectUri,
            ["grant_type"] = "authorization_code",
            ["scope"] = string.Join(" ", _options.Scopes),
            ["code_verifier"] = codeVerifier
        };

        // Only include client_secret if configured
        // Public clients (mobile/SPA) use PKCE only — no secret
        if (!string.IsNullOrWhiteSpace(_options.ClientSecret))
            formData["client_secret"] = _options.ClientSecret;

        using var body = new FormUrlEncodedContent(formData);

        try
        {
            var httpResponse = await client.PostAsync(
                _options.TokenEndpoint, body, ct);

            var responseContent = await httpResponse.Content
                .ReadAsStringAsync(ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OneDrive token exchange failed {Status}: {Body}",
                    (int)httpResponse.StatusCode, responseContent);
                return null;
            }

            using var doc = JsonDocument.Parse(responseContent);
            return doc.RootElement
                .GetProperty("access_token")
                .GetString();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OneDrive token exchange error");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CloudFileDto>> ListPdfFilesAsync(
    string accessToken, CancellationToken ct = default)
    {
        var client = _httpClientFactory.CreateClient("OneDrive");
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            var url = $"{GraphBaseUrl}/me/drive/root/children" +
                      "?$select=name,size,file,@microsoft.graph.downloadUrl" +
                      "&$top=200";

            var httpResponse = await client.GetAsync(url, ct);
            var responseContent = await httpResponse.Content.ReadAsStringAsync(ct);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OneDrive file list failed {Status}: {Body}",
                    (int)httpResponse.StatusCode, responseContent);
                return [];
            }

            using var doc = JsonDocument.Parse(responseContent);
            var files = new List<CloudFileDto>();

            foreach (var item in doc.RootElement.GetProperty("value").EnumerateArray())
            {
                var hasFile = item.TryGetProperty("file", out var fileEl);

                var name = item.TryGetProperty("name", out var nameEl)
                    ? nameEl.GetString() ?? string.Empty
                    : string.Empty;

                var mimeType = hasFile &&
                               fileEl.TryGetProperty("mimeType", out var mimeEl)
                    ? mimeEl.GetString() ?? string.Empty
                    : string.Empty;

                _logger.LogInformation(
                    "OneDrive item: Name={Name}, HasFile={HasFile}, MimeType={MimeType}",
                    name, hasFile, mimeType);

                // Skip folders
                if (!hasFile)
                    continue;

                var isPdfByName = name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
                var isPdfByMime = string.Equals(
                    mimeType, "application/pdf", StringComparison.OrdinalIgnoreCase);

                if (!isPdfByName && !isPdfByMime)
                    continue;

                var size = item.TryGetProperty("size", out var sizeEl)
                    ? sizeEl.GetInt64()
                    : 0;

                if (!item.TryGetProperty("@microsoft.graph.downloadUrl", out var urlEl))
                {
                    _logger.LogInformation("Skipping {Name}: no download URL", name);
                    continue;
                }

                var downloadUrl = urlEl.GetString() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(downloadUrl))
                {
                    _logger.LogInformation("Skipping {Name}: empty download URL", name);
                    continue;
                }

                files.Add(new CloudFileDto(name, downloadUrl, size));
            }

            _logger.LogInformation("OneDrive PDF files found: {Count}", files.Count);
            return files;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OneDrive file list error");
            return [];
        }
    }

    /// <inheritdoc />
    public async Task<byte[]?> DownloadFileAsync(
        string downloadUrl,
        string? accessToken,
        CancellationToken ct = default)
    {
        if (!IsAllowedOneDriveUrl(downloadUrl))
        {
            _logger.LogWarning(
                "Rejected download from disallowed URL: {Url}",
                downloadUrl.Length > 80 ? downloadUrl[..80] : downloadUrl);
            return null;
        }

        var client = _httpClientFactory.CreateClient("OneDrive");

        if (!string.IsNullOrEmpty(accessToken))
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

        try
        {
            using var cts = CancellationTokenSource
                .CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var httpResponse = await client.GetAsync(
                downloadUrl, cts.Token);

            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "OneDrive download failed {Status}",
                    (int)httpResponse.StatusCode);
                return null;
            }

            return await httpResponse.Content
                .ReadAsByteArrayAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("OneDrive download timed out");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OneDrive download error");
            return null;
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    public static (string CodeVerifier, string CodeChallenge) GeneratePkce()
    {
        var verifierBytes = new byte[32];
        RandomNumberGenerator.Fill(verifierBytes);
        var codeVerifier = Base64UrlEncode(verifierBytes);

        var challengeHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(codeVerifier));
        var codeChallenge = Base64UrlEncode(challengeHash);

        return (codeVerifier, codeChallenge);
    }

    public static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static bool IsAllowedOneDriveUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var allowed = new[]
        {
            "onedrive.live.com",
            "api.onedrive.com",
            "graph.microsoft.com",
            "storage.live.com"
        };

        return allowed.Any(host =>
            uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
    }
}