namespace PdfToolStack.Domain.Entities;

public class DocumentMemory
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;  // invoice / contract / general
    public string AiSummary { get; set; } = string.Empty;     // 2-3 sentence summary
    public string MetadataJson { get; set; } = string.Empty;  // extracted fields JSON
    public long FileSizeBytes { get; set; }
    public DateTime StoredAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }                   // 7 days free / 365 days Pro
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
}
