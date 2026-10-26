-- Run after the 20261018 baseline upgrade, before checkout schema alignment.
-- Nullable additions preserve legacy cart items and inventory movements.
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
        THROW 51020, 'Select the AgriSustain database before running this script.', 1;
    IF OBJECT_ID(N'dbo.CartItem', N'U') IS NULL
        OR OBJECT_ID(N'dbo.InventoryMovement', N'U') IS NULL
        THROW 51021, 'The reviewed cart/inventory schema baseline is missing.', 1;

    IF COL_LENGTH(N'dbo.CartItem', N'HarvestBatchId') IS NULL
        ALTER TABLE dbo.CartItem ADD HarvestBatchId BIGINT NULL;
    IF COL_LENGTH(N'dbo.InventoryMovement', N'RequestKey') IS NULL
        ALTER TABLE dbo.InventoryMovement ADD RequestKey VARCHAR(100) NULL;

    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.InventoryMovement')
            AND name = N'UX_InventoryMovement_RequestKey')
        EXEC(N'CREATE UNIQUE INDEX UX_InventoryMovement_RequestKey
            ON dbo.InventoryMovement(RequestKey) WHERE RequestKey IS NOT NULL;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
