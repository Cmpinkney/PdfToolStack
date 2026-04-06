namespace PdfToolStack.Infrastructure.Configuration;

/// <summary>
/// Configuration for Microsoft OneDrive OAuth integration.
/// Bind from appsettings.json "OneDrive" section.
/// </summary>
public sealed class OneDriveOptions
{
    public const string SectionName = "OneDrive";

    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;
    public string TenantId { get; set; } = "common";

    public string AuthorityUrl =>
        $"https://login.microsoftonline.com/{TenantId}/oauth2/v2.0";

    public string AuthorizationEndpoint => $"{AuthorityUrl}/authorize";
    public string TokenEndpoint => $"{AuthorityUrl}/token";

    public string[] Scopes { get; set; } =
        ["Files.Read", "Files.Read.All", "offline_access"];
}
