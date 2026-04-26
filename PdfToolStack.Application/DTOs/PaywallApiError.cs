using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Application.DTOs
{
    public sealed class PaywallApiError
    {
        public bool Allowed { get; set; }
        public PaywallReason Reason { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
