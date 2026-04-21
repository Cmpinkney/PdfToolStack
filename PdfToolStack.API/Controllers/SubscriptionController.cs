using Microsoft.AspNetCore.Authorization;
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
                    amount = 15200,
                    label = "$152 / year"
                },
                teams = new
                {
                    priceId = _stripeOptions.TeamsMonthlyPriceId,
                    amount = 2900,
                    label = "$29 / month"
                }
            });
        }

        [HttpPost("create-addon-checkout")]
        public async Task<IActionResult> CreateAddonCheckout([FromBody] AddonCheckoutRequest request)
        {
            var userId = User.FindFirst("sub")?.Value;
            var email = User.FindFirst("email")?.Value;

            if (userId == null || email == null)
                return Unauthorized();

            var baseUrl = $"{Request.Scheme}://{Request.Host}";

            var url = await _service.CreateOneTimeCheckoutAsync(
                request.PriceId,
                userId,
                email,
                baseUrl
            );

            return Ok(new { url });
        }

        [HttpGet("addons")]
        public IActionResult GetAddons([FromServices] IOptions<StripeOptions> stripeOptions)
        {
            var options = stripeOptions.Value;

            return Ok(new
            {
                largeFile = new
                {
                    priceId = options.LargeFilePriceId,
                    amount = 199,
                    label = "$1.99"
                },
                aiDayPass = new
                {
                    priceId = options.AiDayPassPriceId,
                    amount = 499,
                    label = "$4.99"
                },
                aiCredits50 = new
                {
                    priceId = options.AiCredits50PriceId,
                    amount = 999,
                    label = "$9.99"
                },
                batchUnlock = new
                {
                    priceId = options.BatchUnlockPriceId,
                    amount = 499,
                    label = "$4.99"
                }
            });
        }
    }
}