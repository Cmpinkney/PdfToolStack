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
        public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
        public DbSet<Referral> Referrals => Set<Referral>();

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

            modelBuilder.Entity<ApiKey>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.KeyHash)
                    .IsRequired().HasMaxLength(512);
                entity.Property(e => e.KeyPrefix)
                    .IsRequired().HasMaxLength(32);
                entity.Property(e => e.Name)
                    .IsRequired().HasMaxLength(100);
                entity.Property(e => e.UserId)
                    .IsRequired().HasMaxLength(256);
                entity.HasIndex(e => e.KeyHash).IsUnique();
                entity.HasIndex(e => e.UserId);
            });

            modelBuilder.Entity<Referral>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.ReferrerId)
                    .IsRequired().HasMaxLength(256);
                entity.Property(e => e.ReferralCode)
                    .IsRequired().HasMaxLength(16);
                entity.HasIndex(e => e.ReferralCode).IsUnique();
                entity.HasIndex(e => e.ReferrerId);
                entity.HasIndex(e => e.ReferredUserId);
            });
        }
    }
}
