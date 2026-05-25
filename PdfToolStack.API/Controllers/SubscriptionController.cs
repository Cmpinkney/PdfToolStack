using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;
using System.Security.Claims;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Configuration;
using PdfToolStack.Infrastructure.Services;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubscriptionController : ControllerBase
    {
        private readonly SubscriptionService? _service;
        private readonly StripeOptions _stripeOptions;
        private readonly BillingCatalogOptions _billingCatalogOptions;
        private readonly IProductContext _productContext;
        private readonly IConfiguration _config;
        private readonly ILogger<SubscriptionController> _logger;

        public SubscriptionController(
            IOptions<StripeOptions> stripeOptions,
            IOptions<BillingCatalogOptions> billingCatalogOptions,
            IProductContext productContext,
            IConfiguration config,
            ILogger<SubscriptionController> logger,
            SubscriptionService? service = null)
        {
            _service = service;
            _stripeOptions = stripeOptions.Value;
            _billingCatalogOptions = billingCatalogOptions.Value;
            _productContext = productContext;
            _config = config;
            _logger = logger;
        }

        // ── Subscription status ───────────────────────────────────────────────────

        [HttpGet("status/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetStatus(string userId)
        {
            var callerId = GetCallerId();
            if (string.IsNullOrEmpty(callerId))
                return Unauthorized();

            if (!IsCallerAuthorizedForUser(callerId, userId))
                return Forbid();

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

            dto.UserId = GetCallerId() ?? string.Empty;
            if (string.IsNullOrEmpty(dto.UserId))
                return Unauthorized();

            if (!IsSafeUrl(dto.SuccessUrl) || !IsSafeUrl(dto.CancelUrl))
                return BadRequest(new { error = "Invalid redirect URL." });

            if (string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                _logger.LogError("Stripe__SecretKey is not configured.");
                return StatusCode(503, new { error = "Subscription checkout is not configured." });
            }

            // Validate the price ID is one of the configured subscription prices
            if (!IsValidSubscriptionPriceId(dto.PriceId))
            {
                _logger.LogWarning(
                    "Checkout rejected — invalid PriceId: {PriceId}, UserId: {UserId}",
                    dto.PriceId, dto.UserId);
                return BadRequest(new { error = "Invalid pricing option." });
            }

            dto.PlanType = GetSubscriptionPlanType(dto.PriceId);
            dto.BillingInterval = GetSubscriptionBillingInterval(dto.PriceId);
            dto.ProductType = GetSubscriptionProductType(dto.PriceId);
            dto.EntitlementType = "subscription";

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
                return BadRequest(new { error = "Payment provider error. Please try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Checkout error for UserId: {UserId}", dto.UserId);
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        // ── Add-on checkout (one-time payment) ────────────────────────────────────

        [HttpPost("create-addon-checkout")]
        [Authorize]
        public async Task<IActionResult> CreateAddonCheckout([FromBody] AddonCheckoutRequest request)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });

            request.UserId = GetCallerId() ?? string.Empty;
            if (string.IsNullOrEmpty(request.UserId))
                return Unauthorized();

            if (!IsSafeUrl(request.SuccessUrl) || !IsSafeUrl(request.CancelUrl))
                return BadRequest(new { error = "Invalid redirect URL." });

            if (string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                _logger.LogError("Stripe__SecretKey is not configured.");
                return StatusCode(503, new { error = "Add-on checkout is not configured." });
            }

            var validTypes = new[] { "large_file", "ai_day_pass", "ai_credit_pack", "batch_unlock" };
            if (!validTypes.Contains(request.AddonType))
                return BadRequest(new { error = $"Unknown addon type: {request.AddonType}" });

            // Validate the price ID matches the configured price for this addon type
            var expectedPriceId = GetExpectedAddonPriceId(request.AddonType);
            if (string.IsNullOrWhiteSpace(expectedPriceId) ||
                !string.Equals(request.PriceId, expectedPriceId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Addon checkout rejected — invalid PriceId: {PriceId} for AddonType: {AddonType}, UserId: {UserId}",
                    request.PriceId, request.AddonType, request.UserId);
                return BadRequest(new { error = "Invalid pricing option." });
            }

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
                return BadRequest(new { error = "Payment provider error. Please try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Addon checkout error for UserId: {UserId}", request.UserId);
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        // ── Portal ────────────────────────────────────────────────────────────────

        [HttpPost("create-portal")]
        [Authorize]
        public async Task<IActionResult> CreatePortal([FromBody] CreatePortalDto dto)
        {
            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });

            var userId = GetCallerId() ?? string.Empty;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized();

            dto.UserId = userId;

            if (!IsSafeUrl(dto.ReturnUrl))
                return BadRequest(new { error = "Invalid redirect URL." });

            if (string.IsNullOrWhiteSpace(_stripeOptions.SecretKey))
            {
                _logger.LogError("Stripe__SecretKey is not configured.");
                return StatusCode(503, new { error = "Billing portal is not configured." });
            }

            try
            {
                var url = await _service.CreatePortalSessionAsync(dto);
                if (url == null)
                {
                    _logger.LogWarning("No subscription found for user {UserId}", userId);
                    return NotFound(new { error = "No Stripe subscription found for this account." });
                }

                if (string.IsNullOrWhiteSpace(url))
                {
                    _logger.LogWarning("Billing portal unavailable for user {UserId}: missing Stripe customer record.", userId);
                    return BadRequest(new { error = "Billing not available: missing Stripe customer record." });
                }

                return Ok(new CheckoutResponseDto { Url = url });
            }
            catch (Stripe.StripeException ex)
            {
                _logger.LogError("Stripe billing portal error for user {UserId}: {Code} — {Message}", userId, ex.StripeError?.Code, ex.Message);
                return BadRequest(new { error = "Payment provider error. Please try again." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Billing portal error for user {UserId}", userId);
                return StatusCode(500, new { error = "An unexpected error occurred." });
            }
        }

        // ── History ───────────────────────────────────────────────────────────────

        [HttpGet("history/{userId}")]
        [Authorize]
        public async Task<IActionResult> GetHistory(string userId)
        {
            var callerId = GetCallerId();
            if (string.IsNullOrEmpty(callerId))
                return Unauthorized();

            if (!IsCallerAuthorizedForUser(callerId, userId))
                return Forbid();

            if (_service == null)
                return StatusCode(503, new { error = "Database not configured." });

            var history = await _service.GetDownloadHistoryAsync(userId);
            return Ok(history);
        }

        // ── Plans ─────────────────────────────────────────────────────────────────

        [HttpGet("plans")]
        public IActionResult GetPlans()
        {
            var plans = _billingCatalogOptions.GetSubscriptionPlans()
                .ToDictionary(plan => plan.Id, StringComparer.OrdinalIgnoreCase);

            var monthly = GetPlanResponse(plans, "monthly");
            var yearly = GetPlanResponse(plans, "yearly");
            var teamsMonthly = GetPlanResponse(plans, "teamsMonthly");
            var teamsYearly = GetPlanResponse(plans, "teamsYearly");

            return Ok(new
            {
                monthly,
                yearly,
                teams = teamsMonthly,
                teamsMonthly,
                teamsYearly
            });
        }

        // ── Add-on plans ──────────────────────────────────────────────────────────

        [HttpGet("addons")]
        public IActionResult GetAddons()
        {
            var addOns = _billingCatalogOptions.GetAddOns()
                .ToDictionary(addOn => addOn.Id, StringComparer.OrdinalIgnoreCase);

            return Ok(new
            {
                largeFile = GetAddOnResponse(addOns, "largeFile"),
                aiDayPass = GetAddOnResponse(addOns, "aiDayPass"),
                aiCredits50 = GetAddOnResponse(addOns, "aiCredits50"),
                batchUnlock = GetAddOnResponse(addOns, "batchUnlock")
            });
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private string? GetCallerId() =>
            User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? User.FindFirst("sub")?.Value;

        private bool IsCallerAuthorizedForUser(string callerId, string userId)
        {
            if (callerId == userId) return true;

            var adminIds = (_config["AdminUserIds"] ?? string.Empty)
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            return adminIds.Contains(callerId);
        }

        private bool IsSafeUrl(string url) =>
            _productContext.IsAllowedReturnUrl(url);

        private object GetPlanResponse(
            IReadOnlyDictionary<string, BillingCatalogPlanOptions> plans,
            string id)
        {
            var plan = plans.TryGetValue(id, out var configuredPlan)
                ? configuredPlan
                : BillingCatalogOptions.DefaultSubscriptionPlans()
                    .First(defaultPlan => defaultPlan.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            var priceId = ResolveStripePriceId(plan.StripePriceKey);
            LogMissingStripePrice(plan.StripePriceKey, priceId);

            return new
            {
                priceId,
                amount = plan.Amount,
                label = plan.Label
            };
        }

        private object? GetAddOnResponse(
            IReadOnlyDictionary<string, BillingCatalogAddonOptions> addOns,
            string id)
        {
            var addOn = addOns.TryGetValue(id, out var configuredAddOn)
                ? configuredAddOn
                : BillingCatalogOptions.DefaultAddOns()
                    .First(defaultAddOn => defaultAddOn.Id.Equals(id, StringComparison.OrdinalIgnoreCase));

            if (!addOn.IsPublic)
                return null;

            return new
            {
                priceId = ResolveStripePriceId(addOn.StripePriceKey),
                amount = addOn.Amount,
                label = addOn.Label
            };
        }

        private void LogMissingStripePrice(string stripePriceKey, string priceId)
        {
            if (string.IsNullOrWhiteSpace(priceId))
                _logger.LogWarning("Stripe__{StripePriceKey} is not configured.", stripePriceKey);
        }

        private string ResolveStripePriceId(string stripePriceKey) =>
            stripePriceKey switch
            {
                nameof(StripeOptions.ProMonthlyPriceId) => _stripeOptions.ProMonthlyPriceId,
                nameof(StripeOptions.ProYearlyPriceId) => _stripeOptions.ProYearlyPriceId,
                nameof(StripeOptions.ProMonthlyPriceIdV2) => _stripeOptions.ProMonthlyPriceIdV2,
                nameof(StripeOptions.ProYearlyPriceIdV2) => _stripeOptions.ProYearlyPriceIdV2,
                nameof(StripeOptions.TeamsMonthlyPriceId) => _stripeOptions.TeamsMonthlyPriceId,
                nameof(StripeOptions.TeamsYearlyPriceId) => _stripeOptions.TeamsYearlyPriceId,
                nameof(StripeOptions.AiCredits50PriceId) => _stripeOptions.AiCredits50PriceId,
                nameof(StripeOptions.AiCredits200PriceId) => _stripeOptions.AiCredits200PriceId,
                nameof(StripeOptions.AiDayPassPriceId) => _stripeOptions.AiDayPassPriceId,
                nameof(StripeOptions.BatchUnlockPriceId) => _stripeOptions.BatchUnlockPriceId,
                nameof(StripeOptions.LargeFilePriceId) => _stripeOptions.LargeFilePriceId,
                _ => string.Empty
            };

        private bool IsValidSubscriptionPriceId(string priceId)
        {
            if (string.IsNullOrWhiteSpace(priceId)) return false;

            var allowed = new HashSet<string>(StringComparer.Ordinal);
            AddIfSet(allowed, _stripeOptions.ProMonthlyPriceIdV2);
            AddIfSet(allowed, _stripeOptions.ProYearlyPriceIdV2);
            AddIfSet(allowed, _stripeOptions.TeamsMonthlyPriceId);
            AddIfSet(allowed, _stripeOptions.TeamsYearlyPriceId);
            // Legacy price IDs kept for existing subscriber flows
            AddIfSet(allowed, _stripeOptions.ProMonthlyPriceId);
            AddIfSet(allowed, _stripeOptions.ProYearlyPriceId);

            return allowed.Contains(priceId);
        }

        private string GetSubscriptionProductType(string priceId)
        {
            if (string.Equals(priceId, _stripeOptions.ProMonthlyPriceIdV2, StringComparison.Ordinal) ||
                string.Equals(priceId, _stripeOptions.ProMonthlyPriceId, StringComparison.Ordinal))
                return "pro_monthly";

            if (string.Equals(priceId, _stripeOptions.ProYearlyPriceIdV2, StringComparison.Ordinal) ||
                string.Equals(priceId, _stripeOptions.ProYearlyPriceId, StringComparison.Ordinal))
                return "pro_annual";

            if (string.Equals(priceId, _stripeOptions.TeamsMonthlyPriceId, StringComparison.Ordinal))
                return "teams_monthly";

            if (string.Equals(priceId, _stripeOptions.TeamsYearlyPriceId, StringComparison.Ordinal))
                return "teams_annual";

            return "subscription";
        }

        private string GetSubscriptionPlanType(string priceId)
        {
            if (string.Equals(priceId, _stripeOptions.TeamsMonthlyPriceId, StringComparison.Ordinal) ||
                string.Equals(priceId, _stripeOptions.TeamsYearlyPriceId, StringComparison.Ordinal))
                return "teams";

            return "pro";
        }

        private string GetSubscriptionBillingInterval(string priceId)
        {
            if (string.Equals(priceId, _stripeOptions.ProYearlyPriceIdV2, StringComparison.Ordinal) ||
                string.Equals(priceId, _stripeOptions.ProYearlyPriceId, StringComparison.Ordinal) ||
                string.Equals(priceId, _stripeOptions.TeamsYearlyPriceId, StringComparison.Ordinal))
                return "annual";

            return "monthly";
        }

        private string? GetExpectedAddonPriceId(string addonType) =>
            addonType switch
            {
                "large_file"     => _stripeOptions.LargeFilePriceId,
                "ai_day_pass"    => _stripeOptions.AiDayPassPriceId,
                "ai_credit_pack" => _stripeOptions.AiCredits50PriceId,
                "batch_unlock"   => _stripeOptions.BatchUnlockPriceId,
                _                => null
            };

        private static void AddIfSet(HashSet<string> set, string? value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                set.Add(value);
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
    }
}
