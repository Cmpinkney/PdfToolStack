using PdfToolkit.Application.DTOs;

namespace PdfToolkit.Application.Services
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
