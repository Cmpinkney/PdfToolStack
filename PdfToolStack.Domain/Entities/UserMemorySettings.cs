namespace PdfToolStack.Domain.Entities;

public class UserMemorySettings
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public bool MemoryEnabled { get; set; } = false;          // OFF by default
    public DateTime EnabledAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
