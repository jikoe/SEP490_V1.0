-- Align the deployed checkout schema without changing existing order values.
-- Run on AgriSustain before 20261022_three_use_cases.sql.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET LOCK_TIMEOUT 5000;
BEGIN TRY
    BEGIN TRANSACTION;

    IF DB_NAME() <> N'AgriSustain'
        THROW 51010, 'Select the AgriSustain database before running this script.', 1;
    IF OBJECT_ID(N'dbo.Order', N'U') IS NULL
        OR COL_LENGTH(N'dbo.Order', N'RetailQuoteId') IS NULL
        OR OBJECT_ID(N'dbo.OrderItemBatchAllocation', N'U') IS NULL
        THROW 51011, 'The reviewed checkout schema baseline is missing.', 1;

    IF COL_LENGTH(N'dbo.Order', N'CheckoutRequestKey') IS NULL
        ALTER TABLE dbo.[Order] ADD CheckoutRequestKey VARCHAR(100) NULL;
    IF COL_LENGTH(N'dbo.Order', N'CheckoutCommittedAt') IS NULL
        ALTER TABLE dbo.[Order] ADD CheckoutCommittedAt DATETIME2(7) NULL;

    -- Dynamic SQL resolves newly added columns after the ALTER statements.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Order') AND name = N'UQ_Order_CheckoutRequest')
        EXEC(N'CREATE UNIQUE INDEX UQ_Order_CheckoutRequest
            ON dbo.[Order](BuyerUserId, CheckoutRequestKey)
            WHERE CheckoutRequestKey IS NOT NULL;');

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.Order') AND name = N'UX_Order_Quote')
        EXEC(N'CREATE UNIQUE INDEX UX_Order_Quote ON dbo.[Order](RetailQuoteId)
            WHERE RetailQuoteId IS NOT NULL;');

    IF OBJECT_ID(N'dbo.RewardPointEntry', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.RewardPointEntry
        (
            RewardPointEntryId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            UserId BIGINT NOT NULL REFERENCES dbo.[User](UserId),
            OrderId BIGINT NULL REFERENCES dbo.[Order](OrderId),
            EntryType VARCHAR(20) NOT NULL,
            Points INT NOT NULL,
            RequestKey VARCHAR(100) NOT NULL UNIQUE,
            AvailableDelta AS CONVERT(BIGINT, CASE
                WHEN EntryType IN ('Refund', 'Release', 'Earn') THEN CONVERT(BIGINT, Points)
                WHEN EntryType = 'Hold' THEN -CONVERT(BIGINT, Points)
                ELSE CONVERT(BIGINT, 0) END) PERSISTED,
            HeldDelta AS CONVERT(BIGINT, CASE
                WHEN EntryType = 'Hold' THEN CONVERT(BIGINT, Points)
                WHEN EntryType IN ('Release', 'Consume') THEN -CONVERT(BIGINT, Points)
                ELSE CONVERT(BIGINT, 0) END) PERSISTED,
            OccurredAt DATETIME2(7) NOT NULL DEFAULT SYSUTCDATETIME(),
            CONSTRAINT CK_RewardPointEntry_Points CHECK (Points > 0),
            CONSTRAINT CK_RewardPointEntry_Type CHECK
                (EntryType IN ('Earn', 'Hold', 'Release', 'Consume', 'Refund'))
        );
        CREATE INDEX IX_Reward_Order ON dbo.RewardPointEntry(OrderId, EntryType);
        CREATE INDEX IX_Reward_User ON dbo.RewardPointEntry(UserId);
        EXEC(N'CREATE TRIGGER dbo.TR_Reward_AppendOnly ON dbo.RewardPointEntry
            AFTER UPDATE, DELETE AS
            BEGIN
                SET NOCOUNT ON;
                THROW 51012, ''Reward point entries are append-only.'', 1;
            END;');
    END;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
