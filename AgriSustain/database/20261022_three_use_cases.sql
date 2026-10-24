-- Run against the reviewed AgriSustain database only after confirming its schema baseline.
-- This additive migration is never executed automatically by the application.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.Category', N'U') IS NULL
    THROW 51000, 'The AgriSustain schema baseline is missing.', 1;

IF COL_LENGTH(N'dbo.Order', N'CheckoutRequestKey') IS NULL
    OR COL_LENGTH(N'dbo.Order', N'RetailQuoteId') IS NULL
    OR COL_LENGTH(N'dbo.RewardPointEntry', N'AvailableDelta') IS NULL
    OR OBJECT_ID(N'dbo.RetailCheckoutQuote', N'U') IS NULL
    THROW 51001, 'Checkout schema differs from the scaffolded model. Review before deployment.', 1;

IF EXISTS
(
    SELECT ParentCategoryId, CategoryName
    FROM dbo.Category
    GROUP BY ParentCategoryId, CategoryName
    HAVING COUNT(*) > 1
)
    THROW 51002, 'Duplicate sibling category names must be resolved before migration.', 1;

IF OBJECT_ID(N'dbo.CategorySettings', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.CategorySettings
    (
        CategoryId BIGINT NOT NULL,
        IsArchived BIT NOT NULL CONSTRAINT DF_CategorySettings_IsArchived DEFAULT 0,
        ImageBytes VARBINARY(MAX) NULL,
        ImageContentType NVARCHAR(30) NULL,
        CONSTRAINT PK_CategorySettings PRIMARY KEY (CategoryId),
        CONSTRAINT FK_CategorySettings_Category FOREIGN KEY (CategoryId)
            REFERENCES dbo.Category(CategoryId) ON DELETE CASCADE,
        CONSTRAINT CK_CategorySettings_ImageSize CHECK
            (ImageBytes IS NULL OR DATALENGTH(ImageBytes) <= 5242880),
        CONSTRAINT CK_CategorySettings_ImageType CHECK
            ((ImageBytes IS NULL AND ImageContentType IS NULL)
            OR (ImageBytes IS NOT NULL AND ImageContentType IN (N'image/png', N'image/jpeg')))
    );
END;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Category')
        AND name = N'UX_Category_Parent_Name'
)
    CREATE UNIQUE INDEX UX_Category_Parent_Name ON dbo.Category(ParentCategoryId, CategoryName);

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Order')
        AND name = N'UQ_Order_CheckoutRequest'
)
    CREATE UNIQUE INDEX UQ_Order_CheckoutRequest ON dbo.[Order](BuyerUserId, CheckoutRequestKey)
        WHERE CheckoutRequestKey IS NOT NULL;

IF NOT EXISTS
(
    SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.Order')
        AND name = N'UX_Order_Quote'
)
    CREATE UNIQUE INDEX UX_Order_Quote ON dbo.[Order](RetailQuoteId)
        WHERE RetailQuoteId IS NOT NULL;

COMMIT TRANSACTION;
