using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Application.DTOs
{
    public sealed class PendingBatchCreateResponse
    {
        public Guid PendingBatchId { get; set; }
        public string PendingAccessToken { get; set; } = string.Empty;
        public int FileCount { get; set; }
        public DateTime ExpiresAtUtc { get; set; }
    }

    public sealed class PendingBatchStatusResponse
    {
        public Guid PendingBatchId { get; set; }
        public PendingBatchStatus Status { get; set; }
        public ToolType ToolType { get; set; }
        public int FileCount { get; set; }
        public List<string> FileNames { get; set; } = new();
        public bool IsPaid { get; set; }
        public bool IsAuthorized { get; set; }
        public bool IsUsed { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
