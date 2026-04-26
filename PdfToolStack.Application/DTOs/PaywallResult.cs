using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Application.DTOs
{
    public sealed class PaywallResult
    {
        public bool Allowed { get; set; }
        public PaywallReason Reason { get; set; } = PaywallReason.None;
        public string Message { get; set; } = string.Empty;
        public long FreeLimitBytes { get; set; }
        public long RequiredBytes { get; set; }

        public static PaywallResult Allow() => new()
        {
            Allowed = true,
            Reason = PaywallReason.None
        };

        public static PaywallResult Deny(
            PaywallReason reason,
            string message,
            long freeLimitBytes = 0,
            long requiredBytes = 0) => new()
            {
                Allowed = false,
                Reason = reason,
                Message = message,
                FreeLimitBytes = freeLimitBytes,
                RequiredBytes = requiredBytes
            };
    }
}
