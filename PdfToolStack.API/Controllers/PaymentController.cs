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
            try
            {
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
                            PriceData = new SessionLineItemPriceDataOptions
                            {
                                UnitAmount = request.AmountCents,
                                Currency = _stripeOptions.Currency,
                                ProductData = new SessionLineItemPriceDataProductDataOptions
                                {
                                    Name = request.ProductName,
                                    Description = request.ProductDescription
                                }
                            },
                            Quantity = 1
                        }
                    },
                    Mode = "payment",
                    SuccessUrl = request.SuccessUrl,
                    CancelUrl = request.CancelUrl,
                    Metadata = new Dictionary<string, string>
                    {
                        { "jobId", request.JobId },
                        { "toolType", request.ToolType }
                    }
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
    }

    public class CreateSessionRequest
    {
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
