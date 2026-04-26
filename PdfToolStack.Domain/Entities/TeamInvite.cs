namespace PdfToolStack.Domain.Entities
{
    public class TeamInvite
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public string Email { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? AcceptedAt { get; set; }
        public bool IsAccepted => AcceptedAt.HasValue;

        public Team Team { get; set; } = null!;
    }
}