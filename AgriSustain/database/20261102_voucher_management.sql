/*
  AgriSustain – Platform vouchers (Report 3: UC-PS-01 Manage Promotions and Vouchers,
  UC-RC-02 checkout; rules BR-PV-01..12, GBR-17).
  Date: 02/11/2026

  Adds to dbo.[Voucher] (existing data is kept):
    [VoucherName]       display name of the voucher (BR-PV, field 7); existing rows take their code
    [Description]       optional description, at most 500 characters (field 15)
    [PerCustomerLimit]  uses allowed per customer, default 1 (BR-PV-03)

  How to run: open SSMS, select database AgriSustain, run the whole file (F5).
  Safe to run more than once.
*/
USE [AgriSustain];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.Voucher', N'VoucherName') IS NULL
    ALTER TABLE dbo.[Voucher] ADD [VoucherName] nvarchar(150) NULL;

IF COL_LENGTH(N'dbo.Voucher', N'Description') IS NULL
    ALTER TABLE dbo.[Voucher] ADD [Description] nvarchar(500) NULL;

IF COL_LENGTH(N'dbo.Voucher', N'PerCustomerLimit') IS NULL
    ALTER TABLE dbo.[Voucher] ADD [PerCustomerLimit] int NOT NULL
        CONSTRAINT [DF_Voucher_PerCustomerLimit] DEFAULT 1;
GO

-- Existing vouchers have no name yet: use their code so the column can become required.
UPDATE dbo.[Voucher] SET [VoucherName] = [VoucherCode] WHERE [VoucherName] IS NULL;

IF EXISTS (SELECT 1 FROM sys.columns
           WHERE object_id = OBJECT_ID(N'dbo.Voucher') AND name = N'VoucherName' AND is_nullable = 1)
    ALTER TABLE dbo.[Voucher] ALTER COLUMN [VoucherName] nvarchar(150) NOT NULL;

IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_Voucher_PerCustomerLimit')
    ALTER TABLE dbo.[Voucher] ADD CONSTRAINT [CK_Voucher_PerCustomerLimit] CHECK ([PerCustomerLimit] >= 1);

COMMIT TRANSACTION;
GO
