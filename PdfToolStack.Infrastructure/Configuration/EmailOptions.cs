namespace PdfToolStack.Infrastructure.Configuration
{
    public class EmailOptions
    {
        public const string SectionName = "Resend";
        public string ApiKey { get; set; } = string.Empty;
        public string FromEmail { get; set; } = string.Empty;
        public string FromName { get; set; } = string.Empty;
    }
}