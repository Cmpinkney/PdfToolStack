using PdfToolStack.Application.DTOs;

namespace PdfToolStack.Application.Services
{
    public interface IPdfService
    {
        Task<ProcessResponse> ProcessAsync(
            ProcessRequest request,
            CancellationToken cancellationToken = default);

        Task<JobStatusResponse> GetJobStatusAsync(
            Guid jobId,
            CancellationToken cancellationToken = default);
    }
}
