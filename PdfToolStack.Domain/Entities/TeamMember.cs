namespace PdfToolStack.Domain.Entities
{
    public class TeamMember
    {
        public int Id { get; set; }
        public int TeamId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = "member"; // "admin" | "member"
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        public Team Team { get; set; } = null!;
    }
}