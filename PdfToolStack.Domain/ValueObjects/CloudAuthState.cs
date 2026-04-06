namespace PdfToolStack.Domain.ValueObjects;

public sealed class CloudAuthState
{
    public string Provider { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ReturnUrl { get; set; } = string.Empty;
    public string? CodeVerifier { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
}