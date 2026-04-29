namespace PdfToolStack.Domain.Entities
{
    public class Team
    {
        public int Id { get; set; }
        public string OwnerUserId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int MaxSeats { get; set; } = 5;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<TeamMember> Members { get; set; } = new List<TeamMember>();
        public ICollection<TeamInvite> Invites { get; set; } = new List<TeamInvite>();
    }
}