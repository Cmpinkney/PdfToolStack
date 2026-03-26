using PdfToolkit.Domain.Entities;

namespace PdfToolkit.Domain.Interfaces
{
    public interface IJobRepository
    {
        Task<PdfJob> CreateAsync(PdfJob job);
        Task<PdfJob?> GetByIdAsync(Guid id);
        Task UpdateAsync(PdfJob job);
        Task DeleteAsync(Guid id);
    }
}
