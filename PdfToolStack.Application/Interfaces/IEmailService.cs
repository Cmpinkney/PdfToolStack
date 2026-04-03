namespace PdfToolStack.Application.Interfaces
{
    public interface IEmailService
    {
        Task SendWelcomeEmailAsync(string toEmail, string name);
        Task SendProWelcomeEmailAsync(string toEmail, string name);
        Task SendWaitlistConfirmationAsync(string toEmail, string toolName);
        Task SendContactFormEmailAsync(string fromEmail, string name, string message);
    }
}