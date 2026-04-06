using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using System.Net.Http.Json;

namespace PdfToolStack.Web.Services;

/// <summary>
/// Manages cloud storage picker interactions for Blazor components.
/// 
/// Single Responsibility: handles the cloud picker state machine —
/// getting auth URLs, parsing callback parameters, and downloading files.
/// The UI concerns stay in FileUpload.razor; this service has no UI knowledge.
/// </summary>
public sealed class CloudPickerService
{
    private readonly HttpClient _http;
    private readonly NavigationManager _navigation;
    private readonly IJSRuntime _js;

    public CloudPickerService(
        HttpClient http,
        NavigationManager navigation,
        IJSRuntime js)
    {
        _http = http;
        _navigation = navigation;
        _js = js;
    }

    // ── OneDrive ──────────────────────────────────────────────────────────

    /// <summary>
    /// Initiates OneDrive auth by getting the Microsoft OAuth URL from the API
    /// and navigating the user to it. The API callback will redirect back to
    /// the current page with file data in query parameters.
    /// </summary>
    public async Task OpenOneDriveAsync()
    {
        var currentUrl = _navigation.Uri;
        var encodedReturn = Uri.EscapeDataString(currentUrl);

        var response = await _http.GetFromJsonAsync<AuthUrlResponse>(
            $"api/cloud/onedrive/auth-url?returnUrl={encodedReturn}");

        Console.WriteLine($"OneDrive auth response null? {response is null}");
        Console.WriteLine($"OneDrive auth URL: {response?.AuthUrl}");

        if (string.IsNullOrWhiteSpace(response?.AuthUrl))
            throw new InvalidOperationException("OneDrive auth URL was empty.");

        _navigation.NavigateTo(response.AuthUrl, forceLoad: true);
    }

    /// <summary>
    /// Checks if the current URL contains OneDrive callback parameters.
    /// Call this in OnInitializedAsync of pages that use FileUpload.
    /// </summary>
    public bool HasPendingOneDriveCallback(string currentUrl)
    {
        if (string.IsNullOrWhiteSpace(currentUrl))
            return false;

        return currentUrl.Contains("cloud_provider=onedrive", StringComparison.OrdinalIgnoreCase)
            || currentUrl.Contains("cloud_files=", StringComparison.OrdinalIgnoreCase)
            || currentUrl.Contains("cloud_error=", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses the cloud files from the callback URL query parameters.
    /// Returns null if parameters are missing or invalid.
    /// </summary>
    public IReadOnlyList<CloudFileInfo>? ParseCallbackFiles(string currentUrl)
    {
        var uri = new Uri(currentUrl);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);

        var encodedFiles = query["cloud_files"];
        if (string.IsNullOrEmpty(encodedFiles)) return null;

        try
        {
            var json = Encoding.UTF8.GetString(
                Convert.FromBase64String(
                    Uri.UnescapeDataString(encodedFiles)));

            return JsonSerializer.Deserialize<List<CloudFileInfo>>(
                json,
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? [];
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Downloads a cloud file through the API proxy and returns its bytes.
    /// Works for OneDrive, Google Drive, and Dropbox.
    /// </summary>
    public async Task<byte[]?> DownloadFileAsync(
        string url, string? accessToken = null)
    {
        var encodedUrl = Uri.EscapeDataString(url);
        var tokenParam = string.IsNullOrEmpty(accessToken)
            ? string.Empty
            : $"&token={Uri.EscapeDataString(accessToken)}";

        try
        {
            var response = await _http.GetAsync(
                $"api/cloud/download?url={encodedUrl}{tokenParam}");

            if (!response.IsSuccessStatusCode) return null;

            return await response.Content.ReadAsByteArrayAsync();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Removes cloud callback parameters from the URL without page reload.
    /// Call this after successfully processing the callback to clean up the URL.
    /// </summary>
    public async Task CleanCallbackUrlAsync()
    {
        var uri = new Uri(_navigation.Uri);
        var cleanUrl = uri.GetLeftPart(UriPartial.Path);
        await _js.InvokeVoidAsync(
            "history.replaceState", null, string.Empty, cleanUrl);
    }

    // ── Google Drive (client-side picker, server-side download) ──────────

    /// <summary>
    /// Opens the Google Drive picker via JS interop.
    /// Returns the selected file info, or null if cancelled.
    /// </summary>
    public async Task<CloudFileInfo?> OpenGoogleDriveAsync()
    {
        try
        {
            return await _js.InvokeAsync<CloudFileInfo?>(
                "cloudPickers.openGoogleDrive");
        }
        catch
        {
            return null;
        }
    }

    // ── Dropbox (client-side picker, server-side download) ────────────────

    /// <summary>
    /// Opens the Dropbox picker via JS interop.
    /// Returns the selected file info, or null if cancelled.
    /// </summary>
    public async Task<CloudFileInfo?> OpenDropboxAsync()
    {
        try
        {
            return await _js.InvokeAsync<CloudFileInfo?>(
                "cloudPickers.openDropbox");
        }
        catch
        {
            return null;
        }
    }

    // ── DTOs ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Represents a file returned by any cloud storage provider.
    /// Used both as a JS interop return type and as a parsed callback model.
    /// </summary>
    public sealed record CloudFileInfo(
        string Name,
        string Url,
        long Size,
        string? AccessToken = null);
}

public sealed class AuthUrlResponse
{
    public string? AuthUrl { get; set; }
}