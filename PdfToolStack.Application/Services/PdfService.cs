using PdfToolStack.Application.DTOs;
using PdfToolStack.Application.Factories;
using PdfToolStack.Domain.Entities;
using PdfToolStack.Domain.Enums;
using PdfToolStack.Domain.Interfaces;
using Microsoft.Extensions.Logging;

namespace PdfToolStack.Application.Services
{
    public class PdfService : IPdfService
    {
        private const string MissingDocxMessage =
            "The DOCX file could not be generated. Please try again.";

        private readonly PdfProcessorFactory _factory;
        private readonly IJobRepository _jobRepository;
        private readonly ILogger<PdfService> _logger;

        public PdfService(
            PdfProcessorFactory factory,
            IJobRepository jobRepository,
            ILogger<PdfService> logger)
        {
            _factory = factory;
            _jobRepository = jobRepository;
            _logger = logger;
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

                if (request.ToolType == ToolType.PdfToWord)
                {
                    var outputBytesLength = result.OutputBytes?.Length ?? 0;
                    _logger.LogDebug(
                        "[PdfToWordDiag] PdfService ProcessingResult job={JobId} success={IsSuccess} outputSizeBytes={OutputSizeBytes} outputBytesLength={OutputBytesLength}",
                        request.JobId,
                        result.IsSuccess,
                        result.OutputSizeBytes,
                        outputBytesLength);

                    if (result.IsSuccess && outputBytesLength == 0)
                    {
                        _logger.LogError(
                            "PDF to Word job {JobId} reported success with missing DOCX bytes.",
                            request.JobId);

                        result = ProcessingResult.Failure(MissingDocxMessage);
                    }
                }

                job.Status = result.IsSuccess
                    ? JobStatus.Complete
                    : JobStatus.Failed;
                job.CompletedAt = DateTime.UtcNow;
                job.ErrorMessage = result.ErrorMessage;

                await _jobRepository.UpdateAsync(job);

                var response = new ProcessResponse
                {
                    JobId = job.Id,
                    IsSuccess = result.IsSuccess,
                    ErrorMessage = result.ErrorMessage,
                    OriginalSizeBytes = result.OriginalSizeBytes,
                    OutputSizeBytes = result.OutputSizeBytes,
                    OutputBytes = result.OutputBytes
                };

                if (request.ToolType == ToolType.PdfToWord)
                {
                    _logger.LogDebug(
                        "[PdfToWordDiag] PdfService ProcessResponse job={JobId} outputBytesLength={OutputBytesLength}",
                        response.JobId,
                        response.OutputBytes?.Length ?? 0);
                }

                return response;
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
