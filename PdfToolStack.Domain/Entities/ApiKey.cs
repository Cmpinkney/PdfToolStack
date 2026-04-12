namespace PdfToolStack.Domain.Entities
{
    public class ApiKey
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string KeyHash { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; } = true;
        public int RequestsThisMonth { get; set; } = 0;
        public int MonthlyLimit { get; set; } = 1000;
        public DateTime CurrentMonthStart { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
        public DateTime? RevokedAt { get; set; }
    }
}