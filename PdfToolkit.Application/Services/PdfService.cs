using PdfToolkit.Application.DTOs;
using PdfToolkit.Application.Factories;
using PdfToolkit.Domain.Entities;
using PdfToolkit.Domain.Enums;
using PdfToolkit.Domain.Interfaces;

namespace PdfToolkit.Application.Services
{
    public class PdfService : IPdfService
    {
        private readonly PdfProcessorFactory _factory;
        private readonly IJobRepository _jobRepository;

        public PdfService(
            PdfProcessorFactory factory,
            IJobRepository jobRepository)
        {
            _factory = factory;
            _jobRepository = jobRepository;
        }

        public async Task<ProcessResponse> ProcessAsync(
    ProcessRequest request,
    CancellationToken cancellationToken = default)
        {
            var job = new PdfJob
            {
                Id = request.JobId,
                ToolType = request.ToolType,
                Status = JobStatus.Processing,
                OriginalFileName = request.FileName,
                FileSizeBytes = request.FileSizeBytes
            };

            await _jobRepository.CreateAsync(job);

            try
            {
                var strategy = _factory.GetStrategy(request.ToolType);
                var result = await strategy.ExecuteAsync(
                    request, cancellationToken);

                // Clear input bytes from memory immediately after processing
                request.FileBytes = Array.Empty<byte>();

                job.Status = result.IsSuccess
                    ? JobStatus.Complete
                    : JobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = result.ErrorMessage;

                await _jobRepository.UpdateAsync(job);

                return new ProcessResponse
                {
                    JobId = job.Id,
                    IsSuccess = result.IsSuccess,
                    ErrorMessage = result.ErrorMessage,
                    OriginalSizeBytes = result.OriginalSizeBytes,
                    OutputSizeBytes = result.OutputSizeBytes,
                    OutputBytes = result.OutputBytes
                };
            }
            catch (Exception ex)
            {
                job.Status = JobStatus.Failed;
                job.ErrorMessage = ex.Message;
                job.CompletedAt = DateTime.UtcNow;
                await _jobRepository.UpdateAsync(job);

                return new ProcessResponse
                {
                    JobId = job.Id,
                    IsSuccess = false,
                    ErrorMessage = $"Error: {ex.Message} | " +
                                  $"Inner: {ex.InnerException?.Message} | " +
                                  $"Type: {ex.GetType().Name}"
                };
            }
        }

        public async Task<JobStatusResponse> GetJobStatusAsync(
            Guid jobId,
            CancellationToken cancellationToken = default)
        {
            var job = await _jobRepository.GetByIdAsync(jobId);

            if (job == null)
                return new JobStatusResponse
                {
                    JobId = jobId,
                    Status = JobStatus.Failed,
                    ErrorMessage = "Job not found."
                };

            return new JobStatusResponse
            {
                JobId = job.Id,
                Status = job.Status,
                DownloadUrl = job.OutputBlobUrl,
                ErrorMessage = job.ErrorMessage,
                CreatedAt = job.CreatedAt,
                CompletedAt = job.CompletedAt
            };
        }
    }
}
