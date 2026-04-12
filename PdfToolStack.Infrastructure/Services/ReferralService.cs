using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Data;
using Stripe;
using System.Security.Cryptography;
using System.Text;

namespace PdfToolStack.Infrastructure.Services
{
    public class ReferralService : IReferralService
    {
        private readonly AppDbContext _db;
        private readonly IConfiguration _config;
        private readonly ILogger<ReferralService> _logger;

        public ReferralService(
            AppDbContext db,
            IConfiguration config,
            ILogger<ReferralService> logger)
        {
            _db = db;
            _config = config;
            _logger = logger;
        }

        public async Task<string> GetOrCreateReferralCodeAsync(
            string userId,
            CancellationToken ct = default)
        {
            var existing = await _db.Referrals
                .Where(r => r.ReferrerId == userId
                    && r.ReferredUserId == null)
                .OrderByDescending(r => r.CreatedAt)
                .FirstOrDefaultAsync(ct);

            if (existing != null)
                return existing.ReferralCode;

            // Generate short unique code from userId
            var hash = SHA256.HashData(
                Encoding.UTF8.GetBytes(
                    userId + DateTime.UtcNow.Ticks));
            var code = Convert.ToBase64String(hash)
                .Replace("+", "")
                .Replace("/", "")
                .Replace("=", "")[..8]
                .ToUpper();

            var referral = new Referral
            {
                ReferrerId = userId,
                ReferralCode = code,
                Status = ReferralStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _db.Referrals.Add(referral);
            await _db.SaveChangesAsync(ct);

            return code;
        }

        public async Task<Referral?> GetReferralByCodeAsync(
            string code,
            CancellationToken ct = default)
        {
            return await _db.Referrals
                .FirstOrDefaultAsync(r =>
                    r.ReferralCode == code, ct);
        }

        public async Task TrackClickAsync(
            string code, string? referredUserId,
            CancellationToken ct = default)
        {
            var referral = await _db.Referrals
                .FirstOrDefaultAsync(r =>
                    r.ReferralCode == code &&
                    r.Status == ReferralStatus.Pending, ct);

            if (referral == null) return;

            // Associate with user if they just signed up
            if (!string.IsNullOrEmpty(referredUserId) &&
                string.IsNullOrEmpty(referral.ReferredUserId) &&
                referredUserId != referral.ReferrerId)
            {
                referral.ReferredUserId = referredUserId;
                await _db.SaveChangesAsync(ct);
            }
        }

        public async Task ConvertReferralAsync(
            string referredUserId,
            string referredEmail,
            CancellationToken ct = default)
        {
            // Find a pending referral for this user
            var referral = await _db.Referrals
                .FirstOrDefaultAsync(r =>
                    r.ReferredUserId == referredUserId &&
                    r.Status == ReferralStatus.Pending, ct);

            if (referral == null) return;

            referral.Status = ReferralStatus.Converted;
            referral.ReferredEmail = referredEmail;
            referral.ConvertedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            // Reward the referrer with a free month via Stripe coupon
            await RewardReferrerAsync(referral, ct);
        }

        public async Task<List<Referral>> GetReferrerStatsAsync(
            string referrerId,
            CancellationToken ct = default)
        {
            return await _db.Referrals
                .Where(r => r.ReferrerId == referrerId)
                .OrderByDescending(r => r.CreatedAt)
                .ToListAsync(ct);
        }

        private async Task RewardReferrerAsync(
            Referral referral,
            CancellationToken ct)
        {
            try
            {
                StripeConfiguration.ApiKey =
                    _config["Stripe:SecretKey"];

                // Find referrer's Stripe customer ID
                var referrerSub = await _db.UserSubscriptions
                    .Where(s => s.UserId == referral.ReferrerId
                        && s.Status == "active")
                    .FirstOrDefaultAsync(ct);

                if (referrerSub == null)
                {
                    _logger.LogInformation(
                        "Referrer {Id} has no active sub — " +
                        "reward queued",
                        referral.ReferrerId);
                    return;
                }

                // Apply a 1-month coupon to referrer's subscription
                var couponService = new CouponService();
                var coupon = await couponService.CreateAsync(
                    new CouponCreateOptions
                    {
                        Duration = "once",
                        PercentOff = 100,
                        Name = "Referral reward — 1 free month"
                    }, cancellationToken: ct);

                var subService = new Stripe.SubscriptionService();
                await subService.UpdateAsync(
                    referrerSub.StripeSubscriptionId,
                    new SubscriptionUpdateOptions
                    {
                        Discounts = new List<SubscriptionDiscountOptions>
                        {
                            new SubscriptionDiscountOptions
                            {
                                Coupon = coupon.Id
                            }
                        }
                    },
                    cancellationToken: ct);

                referral.Status = ReferralStatus.Rewarded;
                referral.RewardedAt = DateTime.UtcNow;
                referral.StripeDiscountId = coupon.Id;
                await _db.SaveChangesAsync(ct);

                _logger.LogInformation(
                    "Referral reward applied for {ReferrerId}",
                    referral.ReferrerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to reward referrer {Id}",
                    referral.ReferrerId);
            }
        }
    }
}