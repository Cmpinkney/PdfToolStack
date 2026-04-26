namespace PdfToolStack.Application.DTOs
{
    public class TeamMemberUsageDto
    {
        public string UserId { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime JoinedAt { get; set; }
        public int AiUsedThisMonth { get; set; }
        public int DocsThisMonth { get; set; }
    }
}