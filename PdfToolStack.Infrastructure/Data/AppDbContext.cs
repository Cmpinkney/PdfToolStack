using Microsoft.EntityFrameworkCore;
using PdfToolStack.Domain.Entities;

namespace PdfToolStack.Infrastructure.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options)
            : base(options)
        {
        }

        public DbSet<PdfJob> PdfJobs => Set<PdfJob>();
        public DbSet<UserSubscription> UserSubscriptions => Set<UserSubscription>();
        public DbSet<DownloadHistory> DownloadHistory => Set<DownloadHistory>();
        public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<PdfJob>(entity =>
            {
                entity.HasKey(e => e.Id);

                entity.Property(e => e.OriginalFileName)
                    .IsRequired()
                    .HasMaxLength(255);

                entity.Property(e => e.OutputBlobUrl)
                    .HasMaxLength(1024);

                entity.Property(e => e.ErrorMessage)
                    .HasMaxLength(2000);

                entity.Property(e => e.ToolType)
                    .IsRequired();

                entity.Property(e => e.Status)
                    .IsRequired();

                // Index for fast job lookups
                entity.HasIndex(e => e.CreatedAt);
                entity.HasIndex(e => e.Status);
            });
        }
    }
}
