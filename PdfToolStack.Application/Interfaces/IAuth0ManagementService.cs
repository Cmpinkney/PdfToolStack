namespace PdfToolStack.Application.Interfaces
{
    public interface IAuth0ManagementService
    {
        Task DeleteUserAsync(string userId, CancellationToken cancellationToken = default);
    }
}