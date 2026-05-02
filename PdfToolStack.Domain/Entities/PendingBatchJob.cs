using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Domain.Entities
{
    public class PendingBatchJob
    {
        public Guid PendingBatchId { get; set; } = Guid.NewGuid();
        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddHours(1);
        public PendingBatchStatus Status { get; set; } = PendingBatchStatus.PendingPayment;
        public ToolType ToolType { get; set; }
        public int FileCount { get; set; }
        public string OriginalFileNames { get; set; } = string.Empty;
        public string StoredFileReferences { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string PendingAccessToken { get; set; } = string.Empty;
        public string PaymentSessionId { get; set; } = string.Empty;
        public bool IsUsed { get; set; }
        public string? ErrorMessage { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
    }
}
