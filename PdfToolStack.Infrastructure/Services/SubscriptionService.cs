using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Data;
using Stripe;
using Stripe.Checkout;

namespace PdfToolStack.Infrastructure.Services
{
    public class SubscriptionService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly IEmailService _emailService;
        private readonly IServiceProvider _serviceProvider;

        public SubscriptionService(
            AppDbContext db,
            IConfiguration config,
            IEmailService emailService,
            IServiceProvider serviceProvider)
        {
            _db = db;
            _config = config;
            _emailService = emailService;
            _serviceProvider = serviceProvider;
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
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new() { Price = dto.PriceId, Quantity = 1 }
                },
                Mode = "subscription",
                SuccessUrl = dto.SuccessUrl + "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = dto.CancelUrl,
                CustomerEmail = dto.Email,
                Metadata = new Dictionary<string, string>
                {
                    { "userId", dto.UserId },
                    { "checkout_type", "subscription" }
                }
            };

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
            string cancelUrl)
        {
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
                Metadata = new Dictionary<string, string>
                {
                    { "userId", userId },
                    { "checkout_type", "addon" },
                    { "addon_type", addonType }
                }
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

            var options = new Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = sub.StripeCustomerId,
                ReturnUrl = dto.ReturnUrl
            };

            var service = new Stripe.BillingPortal.SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }

        // ── Webhook ───────────────────────────────────────────────────────────────

        public async Task HandleWebhookAsync(string json, string signature)
        {
            var webhookSecret = _config["Stripe:WebhookSecret"]!;
            var stripeEvent = EventUtility.ConstructEvent(json, signature, webhookSecret);

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    await HandleCheckoutCompletedAsync(stripeEvent);
                    break;
                case "customer.subscription.updated":
                    await HandleSubscriptionUpdatedAsync(stripeEvent);
                    break;
                case "customer.subscription.deleted":
                    await HandleSubscriptionDeletedAsync(stripeEvent);
                    break;
            }
        }

        private async Task HandleCheckoutCompletedAsync(Event stripeEvent)
        {
            var session = stripeEvent.Data.Object as Stripe.Checkout.Session;
            if (session == null) return;

            // Route by checkout_type in metadata — never assume SubscriptionId exists
            var checkoutType = session.Metadata.TryGetValue("checkout_type", out var ct)
                ? ct : "subscription";

            if (checkoutType == "addon")
            {
                await HandleAddonPurchaseAsync(session);
            }
            else
            {
                // Only try to fetch SubscriptionId for actual subscription checkouts
                if (string.IsNullOrEmpty(session.SubscriptionId)) return;
                await HandleSubscriptionPurchaseAsync(session);
            }
        }

        private async Task HandleSubscriptionPurchaseAsync(Stripe.Checkout.Session session)
        {
            var subService = new Stripe.SubscriptionService();
            var stripeSub = await subService.GetAsync(session.SubscriptionId);

            var userId = session.Metadata["userId"];
            var planType = stripeSub.Items.Data[0].Price.Recurring?.Interval == "month"
                ? "monthly" : "yearly";

            var existing = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.UserId == userId);

            if (existing != null)
            {
                existing.StripeSubscriptionId = stripeSub.Id;
                existing.StripeCustomerId = stripeSub.CustomerId;
                existing.PlanType = planType;
                existing.Status = stripeSub.Status;
                existing.CurrentPeriodStart = stripeSub.Items.Data[0].CurrentPeriodStart;
                existing.CurrentPeriodEnd = stripeSub.Items.Data[0].CurrentPeriodEnd;
                existing.CancelAtPeriodEnd = stripeSub.CancelAtPeriodEnd;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = userId,
                    Email = session.CustomerEmail ?? string.Empty,
                    StripeCustomerId = stripeSub.CustomerId,
                    StripeSubscriptionId = stripeSub.Id,
                    PlanType = planType,
                    Status = stripeSub.Status,
                    CurrentPeriodStart = stripeSub.Items.Data[0].CurrentPeriodStart,
                    CurrentPeriodEnd = stripeSub.Items.Data[0].CurrentPeriodEnd,
                    CancelAtPeriodEnd = stripeSub.CancelAtPeriodEnd,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            // Referral reward
            var referralService = _serviceProvider.GetService(typeof(IReferralService)) as IReferralService;
            if (referralService != null)
                await referralService.ConvertReferralAsync(userId, session.CustomerEmail ?? string.Empty);

            // Welcome email
            var email = session.CustomerEmail ?? string.Empty;
            if (!string.IsNullOrEmpty(email))
                await _emailService.SendProWelcomeEmailAsync(email, email.Split('@')[0]);
        }

        private async Task HandleAddonPurchaseAsync(Stripe.Checkout.Session session)
        {
            var userId = session.Metadata.TryGetValue("userId", out var uid) ? uid : string.Empty;
            var addonType = session.Metadata.TryGetValue("addon_type", out var at) ? at : string.Empty;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(addonType)) return;

            // Idempotency — webhook may fire more than once for the same session
            if (await _db.OneTimePurchases.AnyAsync(p => p.StripeSessionId == session.Id)) return;

            DateTime? expiry = addonType switch
            {
                "ai_day_pass" => DateTime.UtcNow.AddHours(24),
                "large_file" => DateTime.UtcNow.AddDays(7),
                "batch_unlock" => DateTime.UtcNow.AddDays(7),
                "ai_credit_pack" => null,
                _ => DateTime.UtcNow.AddDays(7)
            };

            var uses = addonType switch
            {
                "ai_day_pass" => 20,
                "ai_credit_pack" => 50,
                _ => 1    // large_file, batch_unlock: single use
            };

            _db.OneTimePurchases.Add(new OneTimePurchase
            {
                UserId = userId,
                StripeSessionId = session.Id,
                PurchaseType = addonType,
                UsesRemaining = uses,
                IsConsumed = false,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = expiry
            });

            // AI credit packs also top up AiCreditPurchases so AiUsageService can draw from them
            if (addonType == "ai_credit_pack")
            {
                var aiUsageService = _serviceProvider.GetService<IAiUsageService>();
                if (aiUsageService != null)
                    await aiUsageService.RecordCreditPurchaseAsync(userId, session.Id, uses);
            }

            await _db.SaveChangesAsync();
        }

        private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent)
        {
            var stripeSub = stripeEvent.Data.Object as Stripe.Subscription;
            if (stripeSub == null) return;

            var sub = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id);

            if (sub == null) return;

            sub.Status = stripeSub.Status;
            sub.CurrentPeriodStart = stripeSub.Items.Data[0].CurrentPeriodStart;
            sub.CurrentPeriodEnd = stripeSub.Items.Data[0].CurrentPeriodEnd;
            sub.CancelAtPeriodEnd = stripeSub.CancelAtPeriodEnd;
            sub.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private async Task HandleSubscriptionDeletedAsync(Event stripeEvent)
        {
            var stripeSub = stripeEvent.Data.Object as Stripe.Subscription;
            if (stripeSub == null) return;

            var sub = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.StripeSubscriptionId == stripeSub.Id);

            if (sub == null) return;

            sub.Status = "canceled";
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
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
