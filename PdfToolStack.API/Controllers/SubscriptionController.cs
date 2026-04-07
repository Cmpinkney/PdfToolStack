using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Infrastructure.Services;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly SubscriptionService? _service;
        private readonly StripeOptions _stripeOptions;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
    IOptions<StripeOptions> stripeOptions,
    ILogger<SubscriptionController> logger,
    SubscriptionService? service = null)
        {
            _service = service;
            _stripeOptions = stripeOptions.Value;
            _logger = logger;
        }

        [HttpGet("status/{userId}")]
        public async Task<IActionResult> GetStatus(string userId)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });
            var status = await _service.GetStatusAsync(userId);
            return Ok(status);
        }

        [HttpPost("create-checkout")]
        public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutDto dto)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });
            try
            {
                var url = await _service.CreateCheckoutSessionAsync(dto);
                return Ok(new CheckoutResponseDto { Url = url });
            }
            catch (Stripe.StripeException ex)
            {
                _logger.LogError("Stripe error: {Code} — {Message}", ex.StripeError?.Code, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError("Checkout error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        [HttpPost("create-portal")]
        public async Task<IActionResult> CreatePortal([FromBody] CreatePortalDto dto)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });
            try
            {
                var url = await _service.CreatePortalSessionAsync(dto);
                return Ok(new CheckoutResponseDto { Url = url });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpPost("webhook")]
        public async Task<IActionResult> Webhook()
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();
            try
            {
                await _service.HandleWebhookAsync(json, signature);
                return Ok();
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetHistory(string userId)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });
            var history = await _service.GetDownloadHistoryAsync(userId);
            return Ok(history);
        }

        [HttpGet("plans")]
        public IActionResult GetPlans()
        {
            return Ok(new
            {
                monthly = new
                {
                    priceId = _stripeOptions.ProMonthlyPriceIdV2,
                    amount = 1900,
                    label = "$19 / month"
                },
                yearly = new
                {
                    priceId = _stripeOptions.ProYearlyPriceIdV2,
                    amount = 15000,
                    label = "$150 / year"
                }
            });
        }
    }
}