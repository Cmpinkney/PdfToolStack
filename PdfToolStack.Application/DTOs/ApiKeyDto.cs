namespace PdfToolStack.Application.DTOs
{
    public class ApiKeyDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int RequestsThisMonth { get; set; }
        public int MonthlyLimit { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastUsedAt { get; set; }
    }

    public class CreateApiKeyResponseDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string KeyPrefix { get; set; } = string.Empty;
        public string RawKey { get; set; } = string.Empty;
        public int MonthlyLimit { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}