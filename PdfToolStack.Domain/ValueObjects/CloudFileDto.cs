namespace PdfToolStack.Domain.ValueObjects;

/// <summary>
/// Represents a file available for import from a cloud storage provider.
/// </summary>
/// 
public sealed record CloudFileDto(
    string Name,
    string DownloadUrl,
    long SizeBytes);
