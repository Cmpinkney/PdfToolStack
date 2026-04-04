namespace PdfToolStack.Application.Interfaces
{
    public interface IAiUsageService
    {
        Task<(bool Allowed, int Used, int Limit)> CheckAndLogAsync(
            string userId, string feature,
            string model, string planType);

        Task<(int Used, int Limit)> GetUsageAsync(
            string userId, string planType);
    }
}