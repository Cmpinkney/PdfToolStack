using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Configuration;
using PdfToolStack.Infrastructure.Services;
using PdfToolStack.Domain.ValueObjects;
using System.Security.Cryptography;

namespace PdfToolStack.API.Controllers;

/// <summary>
/// Handles all cloud storage integrations server-side.
///
/// OneDrive OAuth flow:
///   1. GET  /api/cloud/onedrive/auth-url  → Blazor calls this, gets Microsoft login URL
///   2. User logs in at Microsoft, grants permission
///   3. GET  /api/cloud/onedrive/callback  → Microsoft redirects here with auth code
///                                           API exchanges code for token, lists PDFs,
///                                           redirects back to Blazor with file list
///   4. GET  /api/cloud/download           → Blazor calls this to download chosen file
///
/// Google Drive and Dropbox use client-side SDKs for picking
/// but route all downloads through /api/cloud/download.
/// </summary>
[ApiController]
[Route("api/cloud")]
public sealed class CloudStorageController : ControllerBase
{
    private readonly ICloudStorageService _oneDriveService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CloudStorageController> _logger;
    private readonly ICloudAuthStateStore _cloudAuthStateStore;

    // Short-lived in-memory token store keyed by session ID.
    // Entries expire implicitly when count exceeds limit.
    // Production: replace with IDistributedCache (Redis).

    public CloudStorageController(
    ICloudStorageService oneDriveService,
    IHttpClientFactory httpClientFactory,
    ILogger<CloudStorageController> logger,
    ICloudAuthStateStore cloudAuthStateStore)
    {
        _oneDriveService = oneDriveService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _cloudAuthStateStore = cloudAuthStateStore;
    }

    // ── Step 1: Generate auth URL ─────────────────────────────────────────

    [HttpGet("onedrive/auth-url")]
    [AllowAnonymous]
    public async Task<IActionResult> GetOneDriveAuthUrl(
    [FromQuery] string returnUrl,
    CancellationToken ct)
    {
        if (!IsSafeReturnUrl(returnUrl))
            return BadRequest("Invalid returnUrl.");

        var (codeVerifier, codeChallenge) = OneDriveService.GeneratePkce();
        var state = GenerateSecureState();

        await _cloudAuthStateStore.SaveAsync(new CloudAuthState
        {
            Provider = "onedrive",
            State = state,
            ReturnUrl = returnUrl,
            CodeVerifier = codeVerifier,
            CreatedAtUtc = DateTimeOffset.UtcNow
        }, ct);

        var redirectUri = BuildCallbackUri();
        var authUrl = _oneDriveService.GetAuthorizationUrl(
            state, redirectUri, codeChallenge);

        return Ok(new { authUrl });
    }

    // ── Step 2: Handle Microsoft OAuth callback ───────────────────────────

    [HttpGet("onedrive/callback")]
    [AllowAnonymous]
    public async Task<IActionResult> OneDriveCallback(
    [FromQuery] string? code,
    [FromQuery] string state,
    [FromQuery] string? error,
    CancellationToken ct)
    {
        // Debugging
        _logger.LogInformation(
            "OneDrive callback hit. Code present: {HasCode}, Error: {Error}, State: {State}",
            !string.IsNullOrWhiteSpace(code),
            error,
            state);

        var authState = await _cloudAuthStateStore.GetAsync("onedrive", state, ct);

        // Debugging
        _logger.LogInformation(
            "OneDrive callback auth state found. ReturnUrl: {ReturnUrl}, HasVerifier: {HasVerifier}",
            authState?.ReturnUrl,
            !string.IsNullOrWhiteSpace(authState?.CodeVerifier));

        if (authState is null)
        {
            _logger.LogWarning("OneDrive callback: invalid or expired state");
            return BadRequest("Invalid state parameter.");
        }

        var returnUrl = authState.ReturnUrl;
        var codeVerifier = authState.CodeVerifier ?? string.Empty;

        if (!string.IsNullOrEmpty(error))
        {
            _logger.LogWarning("OneDrive auth denied: {Error}", error);
            await _cloudAuthStateStore.RemoveAsync("onedrive", state, ct);
            return Redirect($"{returnUrl}?cloud_error=auth_denied");
        }

        if (string.IsNullOrEmpty(code))
        {
            _logger.LogWarning("OneDrive callback: no code received");
            await _cloudAuthStateStore.RemoveAsync("onedrive", state, ct);
            return Redirect($"{returnUrl}?cloud_error=token_failed");
        }

        var redirectUri = BuildCallbackUri();

        var token = await _oneDriveService.ExchangeCodeForTokenAsync(
            code, redirectUri, codeVerifier, ct);

        // Debugging
        _logger.LogInformation(
            "OneDrive token exchange success: {Success}",
            !string.IsNullOrWhiteSpace(token));

        if (string.IsNullOrEmpty(token))
        {
            _logger.LogWarning("OneDrive callback: token exchange failed");
            await _cloudAuthStateStore.RemoveAsync("onedrive", state, ct);
            return Redirect($"{returnUrl}?cloud_error=token_failed");
        }

        var files = await _oneDriveService.ListPdfFilesAsync(token, ct);

        // Debugging
        _logger.LogInformation(
             "OneDrive files returned: {Count}",
                files.Count);

        await _cloudAuthStateStore.RemoveAsync("onedrive", state, ct);

        if (files.Count == 0)
            return Redirect($"{returnUrl}?cloud_error=no_pdfs");

        var filesJson = System.Text.Json.JsonSerializer.Serialize(
            files.Select(f => new
            {
                name = f.Name,
                url = f.DownloadUrl,
                size = f.SizeBytes
            }));

        var encodedFiles = Uri.EscapeDataString(
            Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(filesJson)));

        return Redirect(
            $"{returnUrl}?cloud_provider=onedrive&cloud_files={encodedFiles}");
    }

    // ── Shared: Proxy file download ───────────────────────────────────────

    [HttpGet("download")]
    public async Task<IActionResult> Download(
        [FromQuery] string url,
        [FromQuery] string? token,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(url))
            return BadRequest("url is required.");

        if (!IsAllowedCloudUrl(url))
        {
            _logger.LogWarning(
                "Rejected download from disallowed host: {Url}",
                url.Length > 80 ? url[..80] : url);
            return BadRequest("URL is not from an allowed cloud provider.");
        }

        try
        {
            var client = _httpClientFactory.CreateClient("CloudProxy");
            using var request = new HttpRequestMessage(HttpMethod.Get, url);

            if (!string.IsNullOrWhiteSpace(token))
                request.Headers.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue(
                        "Bearer", token);

            using var cts = CancellationTokenSource
                .CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(30));

            var response = await client.SendAsync(request, cts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Cloud download returned {Status}",
                    (int)response.StatusCode);
                return StatusCode((int)response.StatusCode,
                    "Cloud provider returned an error.");
            }

            var bytes = await response.Content.ReadAsByteArrayAsync(cts.Token);

            if (bytes.Length == 0)
                return BadRequest("Downloaded file is empty.");

            return File(bytes, "application/pdf");
        }
        catch (OperationCanceledException)
        {
            return StatusCode(408, "Download timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloud download proxy error");
            return StatusCode(500, "Download failed.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────

    private string BuildCallbackUri()
    {
        var req = HttpContext.Request;
        return $"{req.Scheme}://{req.Host}/api/cloud/onedrive/callback";
    }

    private static bool IsAllowedCloudUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return false;

        var allowed = new[]
        {
            "www.googleapis.com",
            "googleapis.com",
            "graph.microsoft.com",
            "onedrive.live.com",
            "storage.live.com",
            "api.onedrive.com",
            "dl.dropboxusercontent.com",
            "content.dropboxapi.com"
        };

        return allowed.Any(host =>
            uri.Host.Equals(host, StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith("." + host, StringComparison.OrdinalIgnoreCase));
    }

    private static string GenerateSecureState()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    private bool IsSafeReturnUrl(string returnUrl)
    {
        if (string.IsNullOrWhiteSpace(returnUrl))
            return false;

        if (!Uri.TryCreate(returnUrl, UriKind.Absolute, out var uri))
            return false;

        var allowedHosts = new[]
        {
        "localhost"
        // later add production web host here, e.g. "pdftoolstack.com"
    };

        return allowedHosts.Any(h =>
            uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase));
    }
}