using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;
using System.Security.Claims;
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

        // ── Subscription status ───────────────────────────────────────────────────

        [HttpGet("status/{userId}")]
        public async Task<IActionResult> GetStatus(string userId)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });

            var status = await _service.GetStatusAsync(userId);
            return Ok(status);
        }

        // ── Subscription checkout (recurring) ─────────────────────────────────────

        [HttpPost("create-checkout")]
        [Authorize]
        public async Task<IActionResult> CreateCheckout([FromBody] CreateCheckoutDto dto)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });

            dto.UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? string.Empty;
            if (string.IsNullOrEmpty(dto.UserId))
                return Unauthorized();

            _logger.LogInformation(
                "Checkout initiated — UserId: {UserId}, PriceId: {PriceId}",
                dto.UserId, dto.PriceId);

            try
            {
                var url = await _service.CreateCheckoutSessionAsync(dto);

                if (string.IsNullOrEmpty(url))
                {
                    _logger.LogError("Checkout URL empty for UserId: {UserId}", dto.UserId);
                    return BadRequest(new { error = "Checkout session could not be created." });
                }

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

        // ── Add-on checkout (one-time payment) ────────────────────────────────────

        [HttpPost("create-addon-checkout")]
        [Authorize]
        public async Task<IActionResult> CreateAddonCheckout([FromBody] AddonCheckoutRequest request)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });

            request.UserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? User.FindFirst("sub")?.Value
                ?? string.Empty;
            if (string.IsNullOrEmpty(request.UserId))
                return Unauthorized();

            var validTypes = new[] { "large_file", "ai_day_pass", "ai_credit_pack", "batch_unlock" };
            if (!validTypes.Contains(request.AddonType))
                return BadRequest(new { error = $"Unknown addon type: {request.AddonType}" });

            _logger.LogInformation(
                "Addon checkout initiated — UserId: {UserId}, AddonType: {AddonType}, PriceId: {PriceId}",
                request.UserId, request.AddonType, request.PriceId);

            try
            {
                var safeMetadata = GetSafeAddonMetadata(request);
                var url = await _service.CreateOneTimeCheckoutAsync(
                    request.PriceId,
                    request.AddonType,
                    request.UserId,
                    request.Email,
                    request.SuccessUrl,
                    request.CancelUrl,
                    safeMetadata);

                if (string.IsNullOrEmpty(url))
                {
                    _logger.LogError("Addon checkout URL empty — UserId: {UserId}", request.UserId);
                    return BadRequest(new { error = "Addon checkout session could not be created." });
                }

                return Ok(new CheckoutResponseDto { Url = url });
            }
            catch (Stripe.StripeException ex)
            {
                _logger.LogError("Stripe error for addon: {Code} — {Message}", ex.StripeError?.Code, ex.Message);
                return BadRequest(new { error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError("Addon checkout error: {Message}", ex.Message);
                return BadRequest(new { error = ex.Message });
            }
        }

        private static Dictionary<string, string> GetSafeAddonMetadata(AddonCheckoutRequest request)
        {
            var metadata = new Dictionary<string, string>();

            if (request.AddonType == "batch_unlock" &&
                request.Metadata?.TryGetValue("pendingBatchId", out var pendingBatchId) == true &&
                Guid.TryParse(pendingBatchId, out _))
            {
                metadata["pendingBatchId"] = pendingBatchId;
            }

            return metadata;
        }

        // ── Portal ────────────────────────────────────────────────────────────────

        [HttpPost("create-portal")]
        public async Task<IActionResult> CreatePortal([FromBody] CreatePortalDto dto)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });

            try
            {
                var url = await _service.CreatePortalSessionAsync(dto);
                if (url == null)
                    return NotFound(new { error = "No active subscription found for this user." });
                return Ok(new CheckoutResponseDto { Url = url });
            }
            catch (Exception ex)
            {
                return BadRequest(ex.Message);
            }
        }

        // ── Webhook ───────────────────────────────────────────────────────────────

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
                _logger.LogError("Webhook error: {Message}", ex.Message);
                return BadRequest(ex.Message);
            }
        }

        // ── History ───────────────────────────────────────────────────────────────

        [HttpGet("history/{userId}")]
        public async Task<IActionResult> GetHistory(string userId)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });

            var history = await _service.GetDownloadHistoryAsync(userId);
            return Ok(history);
        }

        // ── Plans ─────────────────────────────────────────────────────────────────

        [HttpGet("plans")]
        public IActionResult GetPlans()
        {
            if (string.IsNullOrEmpty(_stripeOptions.ProMonthlyPriceIdV2))
                _logger.LogWarning("Stripe__ProMonthlyPriceIdV2 is not configured.");
            if (string.IsNullOrEmpty(_stripeOptions.ProYearlyPriceIdV2))
                _logger.LogWarning("Stripe__ProYearlyPriceIdV2 is not configured.");

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

        // ── Add-on plans ──────────────────────────────────────────────────────────

        [HttpGet("addons")]
        public IActionResult GetAddons()
        {
            return Ok(new
            {
                largeFile = new
                {
                    priceId = _stripeOptions.LargeFilePriceId,
                    amount = 199,
                    label = "$1.99"
                },
                aiDayPass = new
                {
                    priceId = _stripeOptions.AiDayPassPriceId,
                    amount = 499,
                    label = "$4.99"
                },
                aiCredits50 = new
                {
                    priceId = _stripeOptions.AiCredits50PriceId,
                    amount = 999,
                    label = "$9.99"
                },
                batchUnlock = new
                {
                    priceId = _stripeOptions.BatchUnlockPriceId,
                    amount = 499,
                    label = "$4.99"
                }
            });
        }
    }
}
