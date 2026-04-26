namespace PdfToolStack.Application.DTOs
{
    public class SubscriptionStatusDto
    {
        public bool IsActive { get; set; }
        public string PlanType { get; set; } = "free";
        public string Status { get; set; } = "none";
        public DateTime? CurrentPeriodEnd { get; set; }
        public bool CancelAtPeriodEnd { get; set; }
        public bool HasTeams => IsActive && PlanType == "teams";
        public bool HasPro => IsActive &&
            (PlanType == "monthly" || PlanType == "yearly" || HasTeams);
    }

    public class CreateCheckoutDto
    {
        public string PriceId { get; set; } = string.Empty;
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class CreatePortalDto
    {
        public string UserId { get; set; } = string.Empty;
        public string ReturnUrl { get; set; } = string.Empty;
    }

    public class CheckoutResponseDto
    {
        public string Url { get; set; } = string.Empty;
    }

    public class DownloadHistoryDto
    {
        public int Id { get; set; }
        public string FileName { get; set; } = string.Empty;
        public string ToolType { get; set; } = string.Empty;
        public long FileSizeBytes { get; set; }
        public DateTime ProcessedAt { get; set; }
    }

    public class AiUsageDto
    {
        public int Used { get; set; }
        public int Limit { get; set; }
        public int Remaining => Limit - Used;
        public double Percentage => (double)Used / Limit * 100;
    }
}