using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
            StripeConfiguration.ApiKey =
                _config["Stripe:SecretKey"];
        }

        public async Task<SubscriptionStatusDto> GetStatusAsync(string userId)
        {
            // Admin bypass
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

            // Normal database lookup
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

        public async Task<string> CreateCheckoutSessionAsync(
            CreateCheckoutDto dto)
        {
            var options = new SessionCreateOptions
            {
                PaymentMethodTypes = new List<string> { "card" },
                LineItems = new List<SessionLineItemOptions>
                {
                    new()
                    {
                        Price = dto.PriceId,
                        Quantity = 1
                    }
                },
                Mode = "subscription",
                SuccessUrl = dto.SuccessUrl +
                    "?session_id={CHECKOUT_SESSION_ID}",
                CancelUrl = dto.CancelUrl,
                CustomerEmail = dto.Email,
                Metadata = new Dictionary<string, string>
                {
                    { "userId", dto.UserId }
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }

        public async Task<string> CreatePortalSessionAsync(
            CreatePortalDto dto)
        {
            var sub = await _db.UserSubscriptions
                .Where(s => s.UserId == dto.UserId)
                .OrderByDescending(s => s.CreatedAt)
                .FirstOrDefaultAsync();

            if (sub == null)
                throw new Exception(
                    "No subscription found for user.");

            var options = new
                Stripe.BillingPortal.SessionCreateOptions
            {
                Customer = sub.StripeCustomerId,
                ReturnUrl = dto.ReturnUrl
            };

            var service =
                new Stripe.BillingPortal.SessionService();
            var session = await service.CreateAsync(options);
            return session.Url;
        }

        public async Task HandleWebhookAsync(
            string json, string signature)
        {
            var webhookSecret =
                _config["Stripe:WebhookSecret"]!;
            var stripeEvent =
                EventUtility.ConstructEvent(
                    json, signature, webhookSecret);

            switch (stripeEvent.Type)
            {
                case "checkout.session.completed":
                    await HandleCheckoutCompletedAsync(
                        stripeEvent);
                    break;
                case "customer.subscription.updated":
                    await HandleSubscriptionUpdatedAsync(
                        stripeEvent);
                    break;
                case "customer.subscription.deleted":
                    await HandleSubscriptionDeletedAsync(
                        stripeEvent);
                    break;
            }
        }

        private async Task HandleCheckoutCompletedAsync(
            Event stripeEvent)
        {
            var session = stripeEvent.Data.Object
                as Stripe.Checkout.Session;
            if (session == null) return;

            var subService = new
                Stripe.SubscriptionService();
            var stripeSub = await subService.GetAsync(
                session.SubscriptionId);

            var userId =
                session.Metadata["userId"];
            var planType =
                stripeSub.Items.Data[0].Price.Recurring
                    .Interval == "month"
                    ? "monthly" : "yearly";

            var existing = await _db.UserSubscriptions
                .FirstOrDefaultAsync(
                    s => s.UserId == userId);

            if (existing != null)
            {
                existing.StripeSubscriptionId =
                    stripeSub.Id;
                existing.StripeCustomerId =
                    stripeSub.CustomerId;
                existing.PlanType = planType;
                existing.Status = stripeSub.Status;
                existing.CurrentPeriodStart =
                    stripeSub.Items.Data[0].CurrentPeriodStart;
                existing.CurrentPeriodEnd =
                    stripeSub.Items.Data[0].CurrentPeriodEnd;
                existing.CancelAtPeriodEnd =
                    stripeSub.CancelAtPeriodEnd;
                existing.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _db.UserSubscriptions.Add(
                    new UserSubscription
                    {
                        UserId = userId,
                        Email = session.CustomerEmail
                            ?? string.Empty,
                        StripeCustomerId =
                            stripeSub.CustomerId,
                        StripeSubscriptionId = stripeSub.Id,
                        PlanType = planType,
                        Status = stripeSub.Status,
                        CurrentPeriodStart =
                            stripeSub.Items.Data[0].CurrentPeriodStart,
                        CurrentPeriodEnd =
                            stripeSub.Items.Data[0].CurrentPeriodEnd,
                        CancelAtPeriodEnd =
                            stripeSub.CancelAtPeriodEnd,
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    });
            }

            await _db.SaveChangesAsync();

            // After saving the subscription — trigger referral reward
            var referralService = _serviceProvider
                .GetService(typeof(IReferralService)) as IReferralService;
            if (referralService != null)
            {
                await referralService.ConvertReferralAsync(
                    userId,
                    session.CustomerEmail ?? string.Empty);
            }

            // Send Pro welcome email
            var customerEmail = session.CustomerEmail ?? string.Empty;
            var customerName = customerEmail.Split('@')[0];

            if (!string.IsNullOrEmpty(customerEmail))
            {
                await _emailService.SendProWelcomeEmailAsync(
                    customerEmail, customerName);
            }
        }

        private async Task HandleSubscriptionUpdatedAsync(
            Event stripeEvent)
        {
            var stripeSub = stripeEvent.Data.Object
                as Stripe.Subscription;
            if (stripeSub == null) return;

            var sub = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s =>
                    s.StripeSubscriptionId == stripeSub.Id);

            if (sub == null) return;

            sub.Status = stripeSub.Status;
            sub.CurrentPeriodStart =
                stripeSub.Items.Data[0].CurrentPeriodStart;
            sub.CurrentPeriodEnd =
                stripeSub.Items.Data[0].CurrentPeriodEnd;
            sub.CancelAtPeriodEnd =
                stripeSub.CancelAtPeriodEnd;
            sub.UpdatedAt = DateTime.UtcNow;

            await _db.SaveChangesAsync();
        }

        private async Task HandleSubscriptionDeletedAsync(
            Event stripeEvent)
        {
            var stripeSub = stripeEvent.Data.Object
                as Stripe.Subscription;
            if (stripeSub == null) return;

            var sub = await _db.UserSubscriptions
                .FirstOrDefaultAsync(s =>
                    s.StripeSubscriptionId == stripeSub.Id);

            if (sub == null) return;

            sub.Status = "canceled";
            sub.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }

        public async Task TrackDownloadAsync(
            string userId,
            string fileName,
            string toolType,
            long fileSizeBytes)
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

        public async Task<List<DownloadHistoryDto>>
            GetDownloadHistoryAsync(string userId)
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

        public async Task<string> CreateOneTimeCheckoutAsync(string priceId, string userId, string email, string baseUrl)
        {
            var options = new SessionCreateOptions
            {
                Mode = "payment",
                CustomerEmail = email,
                SuccessUrl = $"{baseUrl}/account?checkout=success",
                CancelUrl = $"{baseUrl}/pricing",
                LineItems = new List<SessionLineItemOptions>
            {
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            },
                Metadata = new Dictionary<string, string>
                {
                    ["userId"] = userId
                }
            };

            var service = new SessionService();
            var session = await service.CreateAsync(options);

            return session.Url;
        }
    }
}