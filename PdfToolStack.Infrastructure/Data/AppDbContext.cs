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
        public DbSet<AiCreditPurchase> AiCreditPurchases => Set<AiCreditPurchase>();
        public DbSet<OneTimePurchase> OneTimePurchases => Set<OneTimePurchase>();
        public DbSet<PendingBatchJob> PendingBatchJobs => Set<PendingBatchJob>();
        public DbSet<Team> Teams => Set<Team>();
        public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
        public DbSet<TeamInvite> TeamInvites => Set<TeamInvite>();

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

            modelBuilder.Entity<PendingBatchJob>(entity =>
            {
                entity.HasKey(e => e.PendingBatchId);
                entity.Property(e => e.UserId).HasMaxLength(256);
                entity.Property(e => e.PendingAccessToken).IsRequired().HasMaxLength(128);
                entity.Property(e => e.PaymentSessionId).HasMaxLength(256);
                entity.Property(e => e.OriginalFileNames).IsRequired();
                entity.Property(e => e.StoredFileReferences).IsRequired();
                entity.Property(e => e.ErrorMessage).HasMaxLength(2000);
                entity.Property(e => e.Status).IsRequired();
                entity.Property(e => e.ToolType).IsRequired();
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.PendingAccessToken).IsUnique();
                entity.HasIndex(e => e.PaymentSessionId);
                entity.HasIndex(e => e.Status);
                entity.HasIndex(e => e.ExpiresAtUtc);
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

            modelBuilder.Entity<AiCreditPurchase>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId)
                    .IsRequired().HasMaxLength(256);
                entity.Property(e => e.StripeSessionId)
                    .IsRequired().HasMaxLength(256);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.StripeSessionId).IsUnique();
                entity.HasIndex(e => e.ExpiresAt);
            });

            modelBuilder.Entity<Team>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.OwnerUserId).IsRequired().HasMaxLength(128);
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.HasIndex(e => e.OwnerUserId);
            });

            modelBuilder.Entity<TeamMember>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.UserId).IsRequired().HasMaxLength(128);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Name).HasMaxLength(100);
                entity.Property(e => e.Role).IsRequired().HasMaxLength(20);
                entity.HasIndex(e => new { e.TeamId, e.UserId }).IsUnique();
                entity.HasOne(e => e.Team)
                      .WithMany(t => t.Members)
                      .HasForeignKey(e => e.TeamId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<TeamInvite>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
                entity.Property(e => e.Token).IsRequired().HasMaxLength(128);
                entity.HasIndex(e => e.Token).IsUnique();
                entity.HasIndex(e => new { e.TeamId, e.Email });
                entity.HasOne(e => e.Team)
                      .WithMany(t => t.Invites)
                      .HasForeignKey(e => e.TeamId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
