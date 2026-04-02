using System.Net.Http.Json;

namespace PdfToolStack.Web.Services
{
    public class PaymentService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<PaymentService> _logger;

        // Free tier limit for current testing UX
        public const long FreeTierMaxBytes = 102400; // 100 KB

        // Paid tier limit — 500MB
        public const long PaidTierMaxBytes = 524288000;

        // Optional: visible test mode flag for UI copy
        public const bool IsTestModeEnabled = true;

        public PaymentService(
            HttpClient httpClient,
            ILogger<PaymentService> logger)
        {
            _httpClient = httpClient;
            _logger = logger;
        }

        public bool IsFileTooLargeForFree(long fileSizeBytes)
        {
            return fileSizeBytes > FreeTierMaxBytes;
        }

        public bool IsFileTooLargeForPaid(long fileSizeBytes)
        {
            return fileSizeBytes > PaidTierMaxBytes;
        }

        public string FormatFileSize(long bytes)
        {
            if (bytes < 1024)
                return $"{bytes} B";
            if (bytes < 1024 * 1024)
                return $"{Math.Round((double)bytes / 1024, 1)} KB";
            return $"{Math.Round((double)bytes / (1024 * 1024), 1)} MB";
        }

        public async Task<string?> CreateCheckoutSessionAsync(
            string jobId,
            string toolType,
            string successUrl,
            string cancelUrl,
            long amountCents = 99,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var request = new
                {
                    jobId,
                    toolType,
                    productName = "PDFToolstack Large File Processing",
                    productDescription =
                        $"Process PDF files up to 500MB — " +
                        $"one-time payment",
                    amountCents,
                    successUrl,
                    cancelUrl
                };

                var response = await _httpClient.PostAsJsonAsync(
                    "api/payment/create-checkout-session",
                    request,
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content
                        .ReadFromJsonAsync<CheckoutSessionResult>(
                            cancellationToken: cancellationToken);
                    return result?.Url;
                }

                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error creating checkout session");
                return null;
            }
        }

        public async Task<bool> VerifyPaymentAsync(
            string sessionId,
            CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await _httpClient.PostAsJsonAsync(
                    "api/payment/verify-session",
                    new { sessionId },
                    cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content
                        .ReadFromJsonAsync<VerifySessionResult>(
                            cancellationToken: cancellationToken);
                    return result?.IsPaid ?? false;
                }

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error verifying payment");
                return false;
            }
        }

        private class CheckoutSessionResult
        {
            public string? SessionId { get; set; }
            public string? Url { get; set; }
        }

        private class VerifySessionResult
        {
            public bool IsPaid { get; set; }
            public string? JobId { get; set; }
        }
    }
}