-- Run after 20261022_checkout_schema_alignment.sql.
-- Checkout addresses now use Goship city/district/ward codes (three levels).
-- Nullable additions keep saved addresses and historical orders without a district code.
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
    IF OBJECT_ID(N'dbo.DeliveryAddress', N'U') IS NULL
        OR OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
        THROW 51021, 'The reviewed checkout schema baseline is missing.', 1;

    IF COL_LENGTH(N'dbo.DeliveryAddress', N'DistrictCode') IS NULL
        ALTER TABLE dbo.DeliveryAddress ADD DistrictCode VARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.[Order]', N'DistrictCodeSnapshot') IS NULL
        ALTER TABLE dbo.[Order] ADD DistrictCodeSnapshot VARCHAR(20) NULL;
    IF COL_LENGTH(N'dbo.[Order]', N'DistrictNameSnapshot') IS NULL
        ALTER TABLE dbo.[Order] ADD DistrictNameSnapshot NVARCHAR(100) NULL;

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
