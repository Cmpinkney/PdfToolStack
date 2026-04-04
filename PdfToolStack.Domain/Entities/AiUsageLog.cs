namespace PdfToolStack.Domain.Entities
{
    public class AiUsageLog
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Feature { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime UsedAt { get; set; } = DateTime.UtcNow;
    }
}