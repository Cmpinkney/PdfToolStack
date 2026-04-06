using PdfToolStack.Domain.ValueObjects;

namespace PdfToolStack.Domain.Interfaces;

/// <summary>
/// Contract for cloud storage provider integrations.
/// All OAuth is handled server-side — no browser tokens.
/// </summary>
public interface ICloudStorageService
{
    /// <summary>
    /// Returns the OAuth authorization URL including PKCE challenge.
    /// </summary>
    string GetAuthorizationUrl(
        string state, string redirectUri, string codeChallenge);

    /// <summary>
    /// Exchanges an authorization code for an access token using PKCE verifier.
    /// </summary>
    Task<string?> ExchangeCodeForTokenAsync(
        string code,
        string redirectUri,
        string codeVerifier,
        CancellationToken ct = default);

    /// <summary>
    /// Lists PDF files available for the authenticated user.
    /// </summary>
    Task<IReadOnlyList<CloudFileDto>> ListPdfFilesAsync(
        string accessToken, CancellationToken ct = default);

    /// <summary>
    /// Downloads a file and returns its raw bytes.
    /// </summary>
    Task<byte[]?> DownloadFileAsync(
        string downloadUrl, string? accessToken,
        CancellationToken ct = default);
}