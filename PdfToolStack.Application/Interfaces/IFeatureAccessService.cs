namespace PdfToolStack.Application.Interfaces
{
    public interface IFeatureAccessService
    {
        Task<bool> HasProAccessAsync(string userId);
        Task<bool> HasLargeFileUnlockAsync(string userId);
        Task<bool> HasAiDayPassAsync(string userId);
        Task<int> GetAiDayPassUsesRemainingAsync(string userId);
        Task<int> GetAiCreditsRemainingAsync(string userId);
        Task<bool> HasBatchUnlockAsync(string userId);

        Task ConsumeLargeFileUnlockAsync(string userId);
        Task ConsumeAiDayPassUseAsync(string userId);
        Task ConsumeBatchUnlockAsync(string userId);
    }
}