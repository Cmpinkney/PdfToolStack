using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using PdfToolStack.API.Configuration;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Data;
using Stripe;
using Stripe.Checkout;

namespace PdfToolStack.API.Controllers
{
    [ApiController]
    [Route("api/stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly StripeOptions _stripeOptions;
        private readonly IEmailService _emailService;
        private readonly IReferralService? _referralService;
        private readonly ILogger<StripeWebhookController> _logger;

        public StripeWebhookController(
            AppDbContext db,
            IOptions<StripeOptions> stripeOptions,
            IEmailService emailService,
            ILogger<StripeWebhookController> logger,
            IReferralService? referralService = null)
        {
            _db = db;
            _stripeOptions = stripeOptions.Value;
            _emailService = emailService;
            _logger = logger;
            _referralService = referralService;

            StripeConfiguration.ApiKey = _stripeOptions.SecretKey;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signature = Request.Headers["Stripe-Signature"].ToString();

            if (string.IsNullOrWhiteSpace(signature))
                return BadRequest("Missing Stripe signature.");

            Event stripeEvent;
            try
            {
                stripeEvent = EventUtility.ConstructEvent(
                    json,
                    signature,
                    _stripeOptions.WebhookSecret);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Invalid Stripe webhook signature.");
                return BadRequest("Invalid webhook signature.");
            }

            try
            {
                switch (stripeEvent.Type)
                {
                    case "checkout.session.completed":
                        await HandleCheckoutSessionCompletedAsync(stripeEvent);
                        break;

                    case "customer.subscription.updated":
                        await HandleSubscriptionUpdatedAsync(stripeEvent);
                        break;

                    case "customer.subscription.deleted":
                        await HandleSubscriptionDeletedAsync(stripeEvent);
                        break;

                    case "charge.refunded":
                        await HandleChargeRefundedAsync(stripeEvent);
                        break;

                    case "charge.dispute.created":
                        await HandleChargeDisputeCreatedAsync(stripeEvent);
                        break;
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing Stripe webhook.");
                return BadRequest(ex.Message);
            }
        }

        private async Task HandleCheckoutSessionCompletedAsync(Event stripeEvent)
        {
            var session = stripeEvent.Data.Object as Session;
            if (session == null)
                return;

            if (string.Equals(session.Mode, "subscription", StringComparison.OrdinalIgnoreCase))
            {
                await HandleSubscriptionCheckoutCompletedAsync(session);
                return;
            }

            if (string.Equals(session.Mode, "payment", StringComparison.OrdinalIgnoreCase))
            {
                await HandleOneTimePaymentCompletedAsync(session);
            }
        }

        private async Task HandleSubscriptionCheckoutCompletedAsync(Session session)
        {
            if (string.IsNullOrWhiteSpace(session.SubscriptionId))
                return;

            if (!session.Metadata.TryGetValue("userId", out var userId) || string.IsNullOrWhiteSpace(userId))
                return;

            var subscriptionService = new Stripe.SubscriptionService();
            var stripeSubscription = await subscriptionService.GetAsync(session.SubscriptionId);

            var priceId = stripeSubscription.Items.Data[0].Price.Id;
            var interval = stripeSubscription.Items.Data[0].Price.Recurring?.Interval;
            var planType = priceId == _stripeOptions.TeamsMonthlyPriceId
                ? "teams"
                : interval == "month" ? "monthly" : "yearly";

            var existing = await _db.UserSubscriptions.FirstOrDefaultAsync(x => x.UserId == userId);

            if (existing != null)
            {
                existing.StripeSubscriptionId = stripeSubscription.Id;
                existing.StripeCustomerId = stripeSubscription.CustomerId;
                existing.PlanType = planType;
                existing.Status = stripeSubscription.Status;
                existing.CurrentPeriodStart = stripeSubscription.Items.Data[0].CurrentPeriodStart;
                existing.CurrentPeriodEnd = stripeSubscription.Items.Data[0].CurrentPeriodEnd;
                existing.CancelAtPeriodEnd = stripeSubscription.CancelAtPeriodEnd;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.UserSubscriptions.Add(new UserSubscription
                {
                    UserId = userId,
                    Email = session.CustomerEmail ?? string.Empty,
                    StripeCustomerId = stripeSubscription.CustomerId,
                    StripeSubscriptionId = stripeSubscription.Id,
                    PlanType = planType,
                    Status = stripeSubscription.Status,
                    CurrentPeriodStart = stripeSubscription.Items.Data[0].CurrentPeriodStart,
                    CurrentPeriodEnd = stripeSubscription.Items.Data[0].CurrentPeriodEnd,
                    CancelAtPeriodEnd = stripeSubscription.CancelAtPeriodEnd,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                });
            }

            await _db.SaveChangesAsync();

            if (_referralService != null)
            {
                await _referralService.ConvertReferralAsync(userId, session.CustomerEmail ?? string.Empty);
            }

            if (!string.IsNullOrWhiteSpace(session.CustomerEmail))
            {
                var customerName = session.CustomerEmail.Split('@')[0];
                await _emailService.SendProWelcomeEmailAsync(session.CustomerEmail, customerName);
            }
        }

        private async Task HandleOneTimePaymentCompletedAsync(Session session)
        {
            if (!session.Metadata.TryGetValue("userId", out var userId) || string.IsNullOrWhiteSpace(userId))
                return;

            var sessionService = new SessionService();
            var fullSession = await sessionService.GetAsync(
                session.Id,
                new SessionGetOptions
                {
                    Expand = new List<string> { "line_items.data.price" }
                });

            var priceId = fullSession.LineItems?.Data?.FirstOrDefault()?.Price?.Id;
            if (string.IsNullOrWhiteSpace(priceId))
                return;

            var existingPurchase = await _db.OneTimePurchases
                .FirstOrDefaultAsync(x => x.StripeSessionId == session.Id);

            if (existingPurchase != null)
                return;

            if (priceId == _stripeOptions.AiCredits50PriceId)
            {
                var existingCredit = await _db.AiCreditPurchases
                    .FirstOrDefaultAsync(x => x.StripeSessionId == session.Id);

                if (existingCredit == null)
                {
                    _db.AiCreditPurchases.Add(new AiCreditPurchase
                    {
                        UserId = userId,
                        StripeSessionId = session.Id,
                        CreditsAdded = 50,
                        CreditsUsed = 0,
                        PurchasedAt = DateTime.UtcNow,
                        ExpiresAt = DateTime.MaxValue
                    });

                    await _db.SaveChangesAsync();
                }

                return;
            }

            if (priceId == _stripeOptions.LargeFilePriceId)
            {
                _db.OneTimePurchases.Add(new OneTimePurchase
                {
                    UserId = userId,
                    PurchaseType = "LargeFileUnlock",
                    StripeSessionId = session.Id,
                    CreatedAt = DateTime.UtcNow,
                    UsesRemaining = 1
                });

                await _db.SaveChangesAsync();
                return;
            }

            if (priceId == _stripeOptions.AiDayPassPriceId)
            {
                _db.OneTimePurchases.Add(new OneTimePurchase
                {
                    UserId = userId,
                    PurchaseType = "AiDayPass",
                    StripeSessionId = session.Id,
                    CreatedAt = DateTime.UtcNow,
                    ExpiresAt = DateTime.UtcNow.AddHours(24),
                    UsesRemaining = 20
                });

                await _db.SaveChangesAsync();
                return;
            }

            if (priceId == _stripeOptions.BatchUnlockPriceId)
            {
                _db.OneTimePurchases.Add(new OneTimePurchase
                {
                    UserId = userId,
                    PurchaseType = "BatchUnlock",
                    StripeSessionId = session.Id,
                    CreatedAt = DateTime.UtcNow,
                    UsesRemaining = 1
                });

                if (session.Metadata.TryGetValue("pendingBatchId", out var pendingBatchIdText) &&
                    Guid.TryParse(pendingBatchIdText, out var pendingBatchId))
                {
                    var pendingBatch = await _db.PendingBatchJobs
                        .FirstOrDefaultAsync(x => x.PendingBatchId == pendingBatchId);

                    if (pendingBatch != null &&
                        pendingBatch.UserId == userId &&
                        pendingBatch.Status == PendingBatchStatus.PendingPayment &&
                        pendingBatch.ExpiresAtUtc > DateTime.UtcNow &&
                        !pendingBatch.IsUsed)
                    {
                        pendingBatch.Status = PendingBatchStatus.Paid;
                        pendingBatch.PaymentSessionId = session.Id;
                    }
                }

                await _db.SaveChangesAsync();
            }
        }

        private async Task HandleSubscriptionUpdatedAsync(Event stripeEvent)
        {
            var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
            if (stripeSubscription == null)
                return;

            var sub = await _db.UserSubscriptions
                .FirstOrDefaultAsync(x => x.StripeSubscriptionId == stripeSubscription.Id);

            if (sub == null)
                return;

            sub.Status = stripeSubscription.Status;
            sub.CurrentPeriodStart = stripeSubscription.Items.Data[0].CurrentPeriodStart;
            sub.CurrentPeriodEnd = stripeSubscription.Items.Data[0].CurrentPeriodEnd;
            sub.CancelAtPeriodEnd = stripeSubscription.CancelAtPeriodEnd;
            sub.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private async Task HandleSubscriptionDeletedAsync(Event stripeEvent)
        {
            var stripeSubscription = stripeEvent.Data.Object as Stripe.Subscription;
            if (stripeSubscription == null)
                return;

            var sub = await _db.UserSubscriptions
                .FirstOrDefaultAsync(x => x.StripeSubscriptionId == stripeSubscription.Id);

            if (sub == null)
                return;

            sub.Status = "canceled";
            sub.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private async Task HandleChargeRefundedAsync(Event stripeEvent)
        {
            var charge = stripeEvent.Data.Object as Charge;
            if (charge?.CustomerId == null)
                return;

            _logger.LogWarning(
                "Charge refunded: {ChargeId}, Customer: {CustomerId}",
                charge.Id, charge.CustomerId);

            var sub = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.StripeCustomerId == charge.CustomerId);

            if (sub == null)
                return;

            sub.Status = "canceled";
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogInformation(
                "Subscription downgraded to Free for user {UserId} after charge refund",
                sub.UserId);
        }

        private async Task HandleChargeDisputeCreatedAsync(Event stripeEvent)
        {
            var dispute = stripeEvent.Data.Object as Dispute;
            if (dispute?.ChargeId == null)
                return;

            _logger.LogWarning(
                "Dispute created: {DisputeId}, ChargeId: {ChargeId}",
                dispute.Id, dispute.ChargeId);

            var chargeService = new ChargeService();
            var charge = await chargeService.GetAsync(dispute.ChargeId);
            if (charge?.CustomerId == null)
                return;

            var sub = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s => s.StripeCustomerId == charge.CustomerId);

            if (sub == null)
                return;

            sub.Status = "disputed";
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            _logger.LogWarning(
                "Subscription suspended for user {UserId} after dispute {DisputeId}",
                sub.UserId, dispute.Id);
        }
    }
}
