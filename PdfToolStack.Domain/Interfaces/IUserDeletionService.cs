namespace PdfToolStack.Domain.Interfaces
{
    public interface IUserDeletionService
    {
        Task DeleteUserDataAsync(string userId, CancellationToken cancellationToken = default);
    }
}