using Microsoft.EntityFrameworkCore;
using PdfToolStack.Infrastructure.Data;
using PdfToolStack.Infrastructure.Storage;

namespace PdfToolStack.API.Services
{
    public class JobCleanupService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<JobCleanupService> _logger;
        private readonly TimeSpan _interval = TimeSpan.FromHours(1);

        public JobCleanupService(
            IServiceScopeFactory scopeFactory,
            ILogger<JobCleanupService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            _logger.LogInformation("Job cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupOldJobsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task CleanupOldJobsAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                var blobStorage = scope.ServiceProvider.GetService<IBlobStorageService>();

                var cutoff = DateTime.UtcNow.AddHours(-24);

                var oldJobs = await db.PdfJobs
                    .Where(j => j.CreatedAt < cutoff)
                    .ToListAsync(cancellationToken);

                if (!oldJobs.Any())
                    return;

                // Delete output blobs before removing DB records so we never
                // lose the URL reference before the blob is gone.
                if (blobStorage != null)
                {
                    var deletedCount = 0;
                    foreach (var job in oldJobs)
                    {
                        if (string.IsNullOrWhiteSpace(job.OutputBlobUrl))
                            continue;

                        try
                        {
                            await blobStorage.DeleteAsync(
                                job.OutputBlobUrl, cancellationToken);
                            deletedCount++;
                        }
                        catch (Exception ex)
                        {
                            // Log and continue — a failed blob delete must not
                            // block the DB record from being removed.
                            _logger.LogWarning(
                                ex,
                                "Failed to delete output blob for job {JobId}. " +
                                "BlobUrl: {BlobUrl}",
                                job.Id, job.OutputBlobUrl);
                        }
                    }

                    if (deletedCount > 0)
                        _logger.LogInformation(
                            "Deleted {Count} output blobs during cleanup",
                            deletedCount);
                }

                db.PdfJobs.RemoveRange(oldJobs);
                await db.SaveChangesAsync(cancellationToken);

                _logger.LogInformation(
                    "Cleaned up {Count} jobs older than 24 hours",
                    oldJobs.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during job cleanup");
            }
        }
    }
}
