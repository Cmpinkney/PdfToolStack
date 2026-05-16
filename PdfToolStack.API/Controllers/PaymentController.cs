using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Stripe;
using Stripe.Checkout;
using PdfToolStack.API.Configuration;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentController : ControllerBase
    {
        private readonly StripeOptions _stripeOptions;
        private readonly ILogger<PaymentController> _logger;

        public PaymentController(
            IOptions<StripeOptions> stripeOptions,
            ILogger<PaymentController> logger)
        {
            _stripeOptions = stripeOptions.Value;
            _logger = logger;
            StripeConfiguration.ApiKey =
                _stripeOptions.SecretKey;
        }

        // POST api/payment/create-checkout-session
        [HttpPost("create-checkout-session")]
        public async Task<IActionResult> CreateCheckoutSession(
            [FromBody] CreateSessionRequest request)
        {
            if (!IsSafeUrl(request.SuccessUrl) || !IsSafeUrl(request.CancelUrl))
                return BadRequest(new { error = "Invalid redirect URL." });

            if (string.IsNullOrWhiteSpace(_stripeOptions.LargeFilePriceId))
            {
                _logger.LogError("Stripe__LargeFilePriceId is not configured.");
                return StatusCode(503, new { error = "Large file checkout is not configured." });
            }

            try
            {
                var metadata = new Dictionary<string, string>
                {
                    { "jobId", request.JobId },
                    { "toolType", request.ToolType },
                    { "checkout_type", "addon" },
                    { "product_type", "large_file" },
                    { "entitlement_type", "large_file_unlock" }
                };

                if (!string.IsNullOrWhiteSpace(request.UserId))
                    metadata["userId"] = request.UserId;

                var options = new SessionCreateOptions
                {
                    PaymentMethodTypes = new List<string>
                    {
                        "card"
                    },
                    LineItems = new List<SessionLineItemOptions>
                    {
                        new SessionLineItemOptions
                        {
                            Price = _stripeOptions.LargeFilePriceId,
                            Quantity = 1
                        }
                    },
                    Mode = "payment",
                    SuccessUrl = request.SuccessUrl,
                    CancelUrl = request.CancelUrl,
                    Metadata = metadata
                };

                var service = new SessionService();
                var session = await service.CreateAsync(options);

                _logger.LogInformation(
                    "Stripe session created: {SessionId}",
                    session.Id);

                return Ok(new
                {
                    sessionId = session.Id,
                    url = session.Url
                });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex,
                    "Stripe error: {Message}", ex.Message);
                return BadRequest(new
                {
                    error = ex.Message
                });
            }
        }

        // POST api/payment/verify-session
        [HttpPost("verify-session")]
        public async Task<IActionResult> VerifySession(
            [FromBody] VerifySessionRequest request)
        {
            try
            {
                var service = new SessionService();
                var session = await service.GetAsync(
                    request.SessionId);

                if (session.PaymentStatus == "paid")
                {
                    _logger.LogInformation(
                        "Payment verified for session: {SessionId}",
                        request.SessionId);

                    return Ok(new
                    {
                        isPaid = true,
                        jobId = session.Metadata
                            .GetValueOrDefault("jobId"),
                        toolType = session.Metadata
                            .GetValueOrDefault("toolType")
                    });
                }

                return Ok(new { isPaid = false });
            }
            catch (StripeException ex)
            {
                _logger.LogError(ex,
                    "Stripe verification error: {Message}",
                    ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        // GET api/payment/publishable-key
        [HttpGet("publishable-key")]
        public IActionResult GetPublishableKey()
        {
            return Ok(new
            {
                publishableKey = _stripeOptions.PublishableKey
            });
        }

        private static bool IsSafeUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
                return false;
            var allowed = new[] { "localhost", "pdftoolstack.com", "www.pdftoolstack.com" };
            return allowed.Any(h => uri.Host.Equals(h, StringComparison.OrdinalIgnoreCase));
        }
    }

    public class CreateSessionRequest
    {
        public string UserId { get; set; } = string.Empty;
        public string JobId { get; set; } = string.Empty;
        public string ToolType { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public string ProductDescription { get; set; }
            = string.Empty;
        public long AmountCents { get; set; } = 99;
        public string SuccessUrl { get; set; } = string.Empty;
        public string CancelUrl { get; set; } = string.Empty;
    }

    public class VerifySessionRequest
    {
        public string SessionId { get; set; } = string.Empty;
    }
}
