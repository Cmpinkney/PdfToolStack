using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Application.Services
{
    public sealed class PaywallService : IPaywallService
    {
        private const long FreeFileLimitBytes = 25 * 1024 * 1024;
        private const long CoreFileLimitBytes = 100 * 1024 * 1024;
        private const long ProFileLimitBytes = 500 * 1024 * 1024;

        private const int FreeAiUsesPerTool = 5;
        private const int CoreAiUsesPerMonth = 50;

        public PaywallResult CanProcessFile(
            SubscriptionPlan plan,
            long fileSizeBytes)
        {
            var limit = plan switch
            {
                SubscriptionPlan.Pro => ProFileLimitBytes,
                SubscriptionPlan.Core => CoreFileLimitBytes,
                _ => FreeFileLimitBytes
            };

            if (fileSizeBytes <= limit)
                return PaywallResult.Allow();

            return PaywallResult.Deny(
                PaywallReason.FileTooLarge,
                plan == SubscriptionPlan.Free
                    ? "This file is larger than the free 25MB limit. Upgrade or unlock this file."
                    : "This file is larger than your current plan allows.",
                limit,
                fileSizeBytes);
        }

        public PaywallResult CanUseBatchProcessing(
            SubscriptionPlan plan)
        {
            if (plan == SubscriptionPlan.Pro)
                return PaywallResult.Allow();

            return PaywallResult.Deny(
                PaywallReason.BatchRequiresPro,
                "Batch processing is a Pro feature.");
        }

        public PaywallResult CanUseAiTool(
            SubscriptionPlan plan,
            int currentToolUsageCount)
        {
            if (plan == SubscriptionPlan.Pro)
                return PaywallResult.Allow();

            if (plan == SubscriptionPlan.Core &&
                currentToolUsageCount < CoreAiUsesPerMonth)
                return PaywallResult.Allow();

            if (plan == SubscriptionPlan.Free &&
                currentToolUsageCount < FreeAiUsesPerTool)
                return PaywallResult.Allow();

            return PaywallResult.Deny(
                PaywallReason.AiLimitReached,
                "You’ve reached your free AI limit for this tool.");
        }
    }
}
