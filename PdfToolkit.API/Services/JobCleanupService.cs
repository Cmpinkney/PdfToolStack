using PdfToolkit.Infrastructure.Data;
using PdfToolkit.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace PdfToolkit.API.Services
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
            _logger.LogInformation(
                "Job cleanup service started");

            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupOldJobsAsync(stoppingToken);
                await Task.Delay(_interval, stoppingToken);
            }
        }

        private async Task CleanupOldJobsAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider
                    .GetRequiredService<AppDbContext>();

                var cutoff = DateTime.UtcNow.AddHours(-24);

                var oldJobs = await db.PdfJobs
                    .Where(j => j.CreatedAt < cutoff)
                    .ToListAsync(cancellationToken);

                if (oldJobs.Any())
                {
                    db.PdfJobs.RemoveRange(oldJobs);
                    await db.SaveChangesAsync(cancellationToken);

                    _logger.LogInformation(
                        "Cleaned up {Count} jobs older than 24 hours",
                        oldJobs.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error during job cleanup");
            }
        }
    }
}