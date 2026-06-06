using Azure.Storage.Blobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PdfToolStack.Application.Interfaces;
using PdfToolStack.Domain.Interfaces;
using PdfToolStack.Infrastructure.Data;
using Stripe;

namespace PdfToolStack.Infrastructure.Services
{
    public class UserDeletionService : IUserDeletionService
    {
        private readonly AppDbContext _db;
        private readonly IAuth0ManagementService _auth0;
        private readonly IConfiguration _config;
        private readonly ILogger<UserDeletionService> _logger;

        public UserDeletionService(
            AppDbContext db,
            IAuth0ManagementService auth0,
            IConfiguration config,
            ILogger<UserDeletionService> logger)
        {
            _db = db;
            _auth0 = auth0;
            _config = config;
            _logger = logger;
        }

        public async Task DeleteUserDataAsync(
            string userId,
            CancellationToken cancellationToken = default)
        {
            _logger.LogInformation(
                "GDPR deletion started for user {UserId}", userId);

            // 1 — Cancel Stripe subscription
            await CancelStripeSubscriptionAsync(
                userId, cancellationToken);

            // 2 — Delete Azure Blob Storage files
            await DeleteUserBlobsAsync(
                userId, cancellationToken);

            // 3 — Delete SQL data
            await DeleteSqlDataAsync(
                userId, cancellationToken);

            // 4 — Delete Auth0 account (last — point of no return)
            await _auth0.DeleteUserAsync(
                userId, cancellationToken);

            _logger.LogInformation(
                "GDPR deletion complete for user {UserId}", userId);
        }

        private async Task CancelStripeSubscriptionAsync(
    string userId,
    CancellationToken cancellationToken)
        {
            var sub = await _db.UserSubscriptions
                .Where(s => s.UserId == userId &&
                            s.Status == "active")
                .FirstOrDefaultAsync(cancellationToken);

            if (sub == null) return;

            try
            {
                StripeConfiguration.ApiKey =
                    _config["Stripe:SecretKey"];

                var options = new SubscriptionCancelOptions();
                var service = new Stripe.SubscriptionService();
                await service.CancelAsync(
                    sub.StripeSubscriptionId,
                    options,
                    cancellationToken: cancellationToken);

                _logger.LogInformation(
                    "Stripe subscription cancelled for {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Stripe cancel failed for {UserId}: {Error}",
                    userId, ex.Message);
            }
        }

        private async Task DeleteUserBlobsAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            var connStr = _config[
                "AzureStorage:ConnectionString"];
            var container = _config[
                "AzureStorage:ContainerName"] ?? "pdf-outputs";

            if (string.IsNullOrEmpty(connStr)) return;

            try
            {
                var client = new BlobContainerClient(
                    connStr, container);

                await foreach (var blob in client
                    .GetBlobsAsync(
                        cancellationToken: cancellationToken))
                {
                    if (blob.Name.StartsWith(userId))
                    {
                        await client.DeleteBlobIfExistsAsync(
                            blob.Name,
                            cancellationToken: cancellationToken);
                    }
                }

                _logger.LogInformation(
                    "Blobs deleted for {UserId}", userId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    "Blob delete failed for {UserId}: {Error}",
                    userId, ex.Message);
            }
        }

        private async Task DeleteSqlDataAsync(
            string userId,
            CancellationToken cancellationToken)
        {
            var history = await _db.DownloadHistory
                .Where(d => d.UserId == userId)
                .ToListAsync(cancellationToken);
            _db.DownloadHistory.RemoveRange(history);

            var aiLogs = await _db.AiUsageLogs
                .Where(a => a.UserId == userId)
                .ToListAsync(cancellationToken);
            _db.AiUsageLogs.RemoveRange(aiLogs);

            var subscriptions = await _db.UserSubscriptions
                .Where(s => s.UserId == userId)
                .ToListAsync(cancellationToken);
            _db.UserSubscriptions.RemoveRange(subscriptions);

            var fraudAnalyses = await _db.FraudAnalyses
                .Where(f => f.UserId == userId)
                .ToListAsync(cancellationToken);
            _db.FraudAnalyses.RemoveRange(fraudAnalyses);

            var apiKeys = await _db.ApiKeys
                .Where(k => k.UserId == userId)
                .ToListAsync(cancellationToken);
            _db.ApiKeys.RemoveRange(apiKeys);

            var referrals = await _db.Referrals
                .Where(r => r.ReferrerId == userId || r.ReferredUserId == userId)
                .ToListAsync(cancellationToken);
            _db.Referrals.RemoveRange(referrals);

            var aiCreditPurchases = await _db.AiCreditPurchases
                .Where(a => a.UserId == userId)
                .ToListAsync(cancellationToken);
            _db.AiCreditPurchases.RemoveRange(aiCreditPurchases);

            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "SQL data deleted for {UserId}", userId);
        }
    }
}