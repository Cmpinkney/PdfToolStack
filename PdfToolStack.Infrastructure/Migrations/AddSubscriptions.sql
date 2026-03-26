CREATE TABLE UserSubscriptions (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(256) NOT NULL,
    Email NVARCHAR(256) NOT NULL,
    StripeCustomerId NVARCHAR(100) NOT NULL,
    StripeSubscriptionId NVARCHAR(100) NOT NULL,
    PlanType NVARCHAR(20) NOT NULL,
    Status NVARCHAR(20) NOT NULL,
    CurrentPeriodStart DATETIME2 NOT NULL,
    CurrentPeriodEnd DATETIME2 NOT NULL,
    CancelAtPeriodEnd BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);

CREATE TABLE DownloadHistory (
    Id INT IDENTITY(1,1) PRIMARY KEY,
    UserId NVARCHAR(256) NOT NULL,
    FileName NVARCHAR(500) NOT NULL,
    ToolType NVARCHAR(100) NOT NULL,
    FileSizeBytes BIGINT NOT NULL,
    ProcessedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    DownloadUrl NVARCHAR(1000) NULL
);

CREATE INDEX IX_UserSubscriptions_UserId
    ON UserSubscriptions(UserId);

CREATE INDEX IX_UserSubscriptions_StripeCustomerId
    ON UserSubscriptions(StripeCustomerId);

CREATE INDEX IX_DownloadHistory_UserId
    ON DownloadHistory(UserId);