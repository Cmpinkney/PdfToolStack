using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Infrastructure.Data;
using Stripe;
using Stripe.Checkout;

namespace PdfToolStack.Infrastructure.Services
{
    public class SubscriptionService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<SubscriptionService> _logger;

        public SubscriptionService(
            AppDbContext db,
            IConfiguration config,
            ILogger<SubscriptionService> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
            StripeConfiguration.ApiKey = _config["Stripe:SecretKey"];
        }

        // ── Status ────────────────────────────────────────────────────────────────

        public async Task<SubscriptionStatusDto> GetStatusAsync(string userId)
        {
            var adminIds = _config["AdminUserIds"]?.Split(',',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? Array.Empty<string>();

            if (adminIds.Contains(userId))
            {
                return new SubscriptionStatusDto
                {
                    IsActive = true,
                    PlanType = "monthly",
                    Status = "active",
                    CurrentPeriodEnd = DateTime.UtcNow.AddYears(10),
                    CancelAtPeriodEnd = false
                };
            }

            var sub = await _db.UserSubscriptions
                .Where(s => s.UserId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (sub == null || !sub.IsActive)
                return new SubscriptionStatusDto();

            return new SubscriptionStatusDto
            {
                IsActive = true,
                PlanType = sub.PlanType,
                Status = sub.Status,
                CurrentPeriodEnd = sub.CurrentPeriodEnd,
                CancelAtPeriodEnd = sub.CancelAtPeriodEnd
            };
        }

        // ── Subscription checkout (recurring) ─────────────────────────────────────

        public async Task<string> CreateCheckoutSessionAsync(CreateCheckoutDto dto)
        {
            var existingCustomerId = await _db.UserSubscriptions
                .AsNoTracking()
                .Where(s => s.UserId == dto.UserId && s.StripeCustomerId != string.Empty)
                .OrderByDescending(s => s.CreatedAt)
                .Select(s => s.StripeCustomerId)
                .FirstOrDefaultAsync();

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new() { Price = dto.PriceId, Quantity = 1 }
                },
                Mode = "subscription",
                SuccessUrl = dto.SuccessUrl + (dto.SuccessUrl.Contains('?') ? "&" : "?")
                             + "session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = dto.CancelUrl,
                Metadata = new Dictionary<string, string>
                {
                    { "userId", dto.UserId },
                    { "checkout_type", "subscription" },
                    { "plan_type", dto.PlanType },
                    { "billing_interval", dto.BillingInterval },
                    { "product_type", dto.ProductType },
                    { "entitlement_type", string.IsNullOrWhiteSpace(dto.EntitlementType) ? "subscription" : dto.EntitlementType }
                }
            };

            if (!string.IsNullOrWhiteSpace(existingCustomerId))
                options.Customer = existingCustomerId;
            else
                options.CustomerEmail = dto.Email;

            var service = new SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }

        // ── One-time addon checkout ────────────────────────────────────────────────

        /// <summary>
        /// Creates a Stripe Checkout session with Mode = "payment" (one-time, not recurring).
        /// The addonType is stored in metadata so the webhook can grant the right entitlement.
        /// </summary>
        public async Task<string> CreateOneTimeCheckoutAsync(
            string priceId,
            string addonType,
            string userId,
            string email,
            string successUrl,
            string cancelUrl,
            Dictionary<string, string>? metadata = null)
        {
            var sessionMetadata = new Dictionary<string, string>();

            if (metadata != null)
            {
                foreach (var item in metadata)
                {
                    sessionMetadata[item.Key] = item.Value;
                }
            }

            sessionMetadata["userId"] = userId;
            sessionMetadata["checkout_type"] = "addon";
            sessionMetadata["addon_type"] = addonType;
            sessionMetadata["product_type"] = addonType;
            sessionMetadata["entitlement_type"] = addonType switch
            {
                "large_file" => "large_file_unlock",
                "ai_day_pass" => "ai_day_pass",
                "ai_credit_pack" => "ai_credits",
                "batch_unlock" => "batch_unlock",
                _ => "one_time"
            };

            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new() { Price = priceId, Quantity = 1 }
                },
                Mode = "payment",   // ← critical: one-time, NOT "subscription"
                SuccessUrl = successUrl + (successUrl.Contains('?') ? "&" : "?")
                             + "session_id={CHECKOUT_SESSION_ID}&addon=" + addonType,
                CancelUrl = cancelUrl,
                CustomerEmail = email,
                Metadata = sessionMetadata
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }

        // ── Portal ────────────────────────────────────────────────────────────────

        public async Task<string?> CreatePortalSessionAsync(CreatePortalDto dto)
        {
            var sub = await _db.UserSubscriptions
                .Where(s => s.UserId == dto.UserId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (sub == null)
                return null;

            if (string.IsNullOrEmpty(sub.StripeCustomerId))
            {
                _logger.LogError("Missing StripeCustomerId for user {UserId}", dto.UserId);
                return string.Empty;
            }

            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = sub.StripeCustomerId,
                ReturnUrl = dto.ReturnUrl
            };

            var service = new Stripe.BillingPortal.SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }

        // ── OneTimePurchase entitlement helpers ───────────────────────────────────

        public async Task<bool> HasActivePurchaseAsync(string userId, string purchaseType)
        {
            var now = DateTime.UtcNow;
            return await _db.OneTimePurchases.AnyAsync(p =>
                p.UserId == userId &&
                p.PurchaseType == purchaseType &&
                !p.IsConsumed &&
                p.UsesRemaining > 0 &&
                (p.ExpiresAt == null || p.ExpiresAt > now));
        }

        /// <summary>
        /// Decrements UsesRemaining by 1. Marks IsConsumed when it hits 0.
        /// Call after a successful tool run, not before.
        /// </summary>
        public async Task ConsumeOneTimeUseAsync(string userId, string purchaseType)
        {
            var now = DateTime.UtcNow;
            var purchase = await _db.OneTimePurchases
                .Where(p =>
                    p.UserId == userId &&
                    p.PurchaseType == purchaseType &&
                    !p.IsConsumed &&
                    p.UsesRemaining > 0 &&
                    (p.ExpiresAt == null || p.ExpiresAt > now))
                .OrderBy(p => p.CreatedAt)
                .FirstOrDefaultAsync();

            if (purchase == null) return;

            purchase.UsesRemaining -= 1;
            if (purchase.UsesRemaining <= 0)
                purchase.IsConsumed = true;

            await _db.SaveChangesAsync();
        }

        // ── History ───────────────────────────────────────────────────────────────

        public async Task TrackDownloadAsync(string userId, string fileName, string toolType, long fileSizeBytes)
        {
            _db.DownloadHistory.Add(new DownloadHistory
            {
                UserId = userId,
                FileName = fileName,
                ToolType = toolType,
                FileSizeBytes = fileSizeBytes,
                ProcessedAt = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }

        public async Task<List<DownloadHistoryDto>> GetDownloadHistoryAsync(string userId)
        {
            return await _db.DownloadHistory
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.ProcessedAt)
                .Take(50)
                .Select(d => new DownloadHistoryDto
                {
                    Id = d.Id,
                    FileName = d.FileName,
                    ToolType = d.ToolType,
                    FileSizeBytes = d.FileSizeBytes,
                    ProcessedAt = d.ProcessedAt
                })
                .ToListAsync();
        }
    }
}
