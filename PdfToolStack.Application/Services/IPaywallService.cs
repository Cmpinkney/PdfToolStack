using PdfToolStack.Application.DTOs;
using PdfToolStack.Domain.Enums;

namespace PdfToolStack.Application.Services
{
    public interface IPaywallService
    {
        PaywallResult CanProcessFile(
            SubscriptionPlan plan,
            long fileSizeBytes);

        PaywallResult CanUseBatchProcessing(
            SubscriptionPlan plan);

        PaywallResult CanUseAiTool(
            SubscriptionPlan plan,
            int currentToolUsageCount);
    }
}
