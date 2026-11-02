/*
  AgriSustain – Bank accounts (Report 3: UC-AUTH-03 Bank Account, UC-FIN-08 Provide Refund Account,
  UC-FIN-04 payout recipient; rules BR-BA-01..06, BR-RF-05).
  Date: 30/10/2026 – Author: DuND

  Adds (existing data is kept):
    dbo.[SupportedBank]      supported banks + 12 Vietnamese banks
    dbo.[UserBankAccount]    default bank account of a user (Farmer: payout account; buyers: refund account),
                             with VerificationStatus (bank lookup, BR-BA-06) and QrImageUrl (QR added after saving)
    dbo.[RefundTransaction]  ReceiverBankCode, ReceiverAccountNumber, ReceiverAccountHolder, ReceiverAccountProvidedAt
    dbo.[FarmerPayout]       RecipientBankCode, RecipientAccountNumber, RecipientAccountHolder

  How to run: open SSMS, select database AgriSustain, run the whole file (F5).
  Safe to run more than once. Demo accounts are added only for demo users that exist.
*/
USE [AgriSustain];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.SupportedBank', N'U') IS NULL
    CREATE TABLE dbo.[SupportedBank] (
        [BankCode] varchar(20) NOT NULL,
        [ShortName] nvarchar(50) NOT NULL,
        [FullName] nvarchar(200) NOT NULL,
        [IsActive] bit NOT NULL CONSTRAINT [DF_SupportedBank_IsActive] DEFAULT CAST(1 AS bit),
        [DisplayOrder] int NOT NULL CONSTRAINT [DF_SupportedBank_DisplayOrder] DEFAULT 0,
        CONSTRAINT [PK_SupportedBank] PRIMARY KEY ([BankCode])
    );

MERGE dbo.[SupportedBank] AS t
USING (VALUES
    ('VCB',  N'Vietcombank', N'Ngân hàng TMCP Ngoại thương Việt Nam', 1),
    ('BIDV', N'BIDV',        N'Ngân hàng TMCP Đầu tư và Phát triển Việt Nam', 2),
    ('ICB',  N'VietinBank',  N'Ngân hàng TMCP Công thương Việt Nam', 3),
    ('VBA',  N'Agribank',    N'Ngân hàng Nông nghiệp và Phát triển Nông thôn Việt Nam', 4),
    ('TCB',  N'Techcombank', N'Ngân hàng TMCP Kỹ thương Việt Nam', 5),
    ('MB',   N'MBBank',      N'Ngân hàng TMCP Quân đội', 6),
    ('ACB',  N'ACB',         N'Ngân hàng TMCP Á Châu', 7),
    ('VPB',  N'VPBank',      N'Ngân hàng TMCP Việt Nam Thịnh Vượng', 8),
    ('TPB',  N'TPBank',      N'Ngân hàng TMCP Tiên Phong', 9),
    ('STB',  N'Sacombank',   N'Ngân hàng TMCP Sài Gòn Thương Tín', 10),
    ('VIB',  N'VIB',         N'Ngân hàng TMCP Quốc tế Việt Nam', 11),
    ('HDB',  N'HDBank',      N'Ngân hàng TMCP Phát triển TP. Hồ Chí Minh', 12)
) AS s ([BankCode], [ShortName], [FullName], [DisplayOrder])
ON t.[BankCode] = s.[BankCode]
WHEN NOT MATCHED THEN
    INSERT ([BankCode], [ShortName], [FullName], [DisplayOrder])
    VALUES (s.[BankCode], s.[ShortName], s.[FullName], s.[DisplayOrder]);

IF OBJECT_ID(N'dbo.UserBankAccount', N'U') IS NULL
    CREATE TABLE dbo.[UserBankAccount] (
        [UserBankAccountId] bigint NOT NULL IDENTITY(1,1),
        [UserId] bigint NOT NULL,
        [BankCode] varchar(20) NOT NULL,
        [AccountNumber] varchar(19) NOT NULL,
        [AccountHolder] nvarchar(100) NOT NULL,
        [IsDefault] bit NOT NULL CONSTRAINT [DF_UserBankAccount_IsDefault] DEFAULT CAST(1 AS bit),
        [VerifiedAt] datetime2 NULL,
        [PayoutHoldUntil] datetime2 NULL,
        [VerificationStatus] varchar(20) NOT NULL CONSTRAINT [DF_UserBankAccount_VerificationStatus] DEFAULT 'Unverified',
        [QrImageUrl] nvarchar(500) NULL,
        [CreatedAt] datetime2 NOT NULL CONSTRAINT [DF_UserBankAccount_CreatedAt] DEFAULT SYSUTCDATETIME(),
        [UpdatedAt] datetime2 NULL,
        CONSTRAINT [PK_UserBankAccount] PRIMARY KEY ([UserBankAccountId]),
        CONSTRAINT [FK_UserBankAccount_User] FOREIGN KEY ([UserId]) REFERENCES dbo.[User] ([UserId]),
        CONSTRAINT [FK_UserBankAccount_SupportedBank] FOREIGN KEY ([BankCode]) REFERENCES dbo.[SupportedBank] ([BankCode]),
        CONSTRAINT [CK_UserBankAccount_AccountNumber] CHECK (LEN([AccountNumber]) BETWEEN 6 AND 19 AND [AccountNumber] NOT LIKE '%[^0-9]%'),
        CONSTRAINT [CK_UserBankAccount_VerificationStatus] CHECK ([VerificationStatus] IN ('Verified', 'Unverified')),
        CONSTRAINT [CK_UserBankAccount_AccountHolder] CHECK (LEN(LTRIM(RTRIM([AccountHolder]))) > 0)
    );
IF COL_LENGTH(N'dbo.UserBankAccount', N'VerifiedAt') IS NULL
    ALTER TABLE dbo.[UserBankAccount] ADD [VerifiedAt] datetime2 NULL;
IF COL_LENGTH(N'dbo.UserBankAccount', N'PayoutHoldUntil') IS NULL
    ALTER TABLE dbo.[UserBankAccount] ADD [PayoutHoldUntil] datetime2 NULL;
IF COL_LENGTH(N'dbo.UserBankAccount', N'VerificationStatus') IS NULL
    ALTER TABLE dbo.[UserBankAccount] ADD [VerificationStatus] varchar(20) NOT NULL
        CONSTRAINT [DF_UserBankAccount_VerificationStatus] DEFAULT 'Unverified' WITH VALUES;
IF COL_LENGTH(N'dbo.UserBankAccount', N'QrImageUrl') IS NULL
    ALTER TABLE dbo.[UserBankAccount] ADD [QrImageUrl] nvarchar(500) NULL;

IF COL_LENGTH(N'dbo.RefundTransaction', N'ReceiverBankCode') IS NULL
    ALTER TABLE dbo.[RefundTransaction] ADD [ReceiverBankCode] varchar(20) NULL;
IF COL_LENGTH(N'dbo.RefundTransaction', N'ReceiverAccountNumber') IS NULL
    ALTER TABLE dbo.[RefundTransaction] ADD [ReceiverAccountNumber] varchar(19) NULL;
IF COL_LENGTH(N'dbo.RefundTransaction', N'ReceiverAccountHolder') IS NULL
    ALTER TABLE dbo.[RefundTransaction] ADD [ReceiverAccountHolder] nvarchar(100) NULL;
IF COL_LENGTH(N'dbo.RefundTransaction', N'ReceiverAccountProvidedAt') IS NULL
    ALTER TABLE dbo.[RefundTransaction] ADD [ReceiverAccountProvidedAt] datetime2 NULL;

IF COL_LENGTH(N'dbo.FarmerPayout', N'RecipientBankCode') IS NULL
    ALTER TABLE dbo.[FarmerPayout] ADD [RecipientBankCode] varchar(20) NULL;
IF COL_LENGTH(N'dbo.FarmerPayout', N'RecipientAccountNumber') IS NULL
    ALTER TABLE dbo.[FarmerPayout] ADD [RecipientAccountNumber] varchar(19) NULL;
IF COL_LENGTH(N'dbo.FarmerPayout', N'RecipientAccountHolder') IS NULL
    ALTER TABLE dbo.[FarmerPayout] ADD [RecipientAccountHolder] nvarchar(100) NULL;

COMMIT TRANSACTION;
GO

-- Constraints, indexes and demo data run in a separate batch because the columns were just added.
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF OBJECT_ID(N'dbo.FK_RefundTransaction_ReceiverBank', N'F') IS NULL
    ALTER TABLE dbo.[RefundTransaction] WITH CHECK ADD CONSTRAINT [FK_RefundTransaction_ReceiverBank]
        FOREIGN KEY ([ReceiverBankCode]) REFERENCES dbo.[SupportedBank] ([BankCode]);
IF OBJECT_ID(N'dbo.CK_RefundTransaction_ReceiverAccount', N'C') IS NULL
    ALTER TABLE dbo.[RefundTransaction] WITH CHECK ADD CONSTRAINT [CK_RefundTransaction_ReceiverAccount] CHECK (
        ([ReceiverBankCode] IS NULL AND [ReceiverAccountNumber] IS NULL AND [ReceiverAccountHolder] IS NULL AND [ReceiverAccountProvidedAt] IS NULL)
        OR ([ReceiverBankCode] IS NOT NULL AND [ReceiverAccountHolder] IS NOT NULL AND [ReceiverAccountProvidedAt] IS NOT NULL
            AND LEN([ReceiverAccountNumber]) BETWEEN 6 AND 19 AND [ReceiverAccountNumber] NOT LIKE '%[^0-9]%'));
IF OBJECT_ID(N'dbo.FK_FarmerPayout_RecipientBank', N'F') IS NULL
    ALTER TABLE dbo.[FarmerPayout] WITH CHECK ADD CONSTRAINT [FK_FarmerPayout_RecipientBank]
        FOREIGN KEY ([RecipientBankCode]) REFERENCES dbo.[SupportedBank] ([BankCode]);
IF OBJECT_ID(N'dbo.CK_FarmerPayout_RecipientAccount', N'C') IS NULL
    ALTER TABLE dbo.[FarmerPayout] WITH CHECK ADD CONSTRAINT [CK_FarmerPayout_RecipientAccount] CHECK (
        ([RecipientBankCode] IS NULL AND [RecipientAccountNumber] IS NULL AND [RecipientAccountHolder] IS NULL)
        OR ([RecipientBankCode] IS NOT NULL AND [RecipientAccountHolder] IS NOT NULL
            AND LEN([RecipientAccountNumber]) BETWEEN 6 AND 19 AND [RecipientAccountNumber] NOT LIKE '%[^0-9]%'));

IF OBJECT_ID(N'dbo.CK_UserBankAccount_VerificationStatus', N'C') IS NULL
    ALTER TABLE dbo.[UserBankAccount] WITH CHECK ADD CONSTRAINT [CK_UserBankAccount_VerificationStatus]
        CHECK ([VerificationStatus] IN ('Verified', 'Unverified'));
-- Accounts saved before the bank lookup existed: a Farmer account with VerifiedAt counts as Verified.
UPDATE dbo.[UserBankAccount] SET [VerificationStatus] = 'Verified'
WHERE [VerifiedAt] IS NOT NULL AND [VerificationStatus] <> 'Verified';

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_UserBankAccount_UserId' AND object_id = OBJECT_ID(N'dbo.UserBankAccount'))
    CREATE INDEX [IX_UserBankAccount_UserId] ON dbo.[UserBankAccount] ([UserId]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_UserBankAccount_DefaultPerUser' AND object_id = OBJECT_ID(N'dbo.UserBankAccount'))
    CREATE UNIQUE INDEX [UX_UserBankAccount_DefaultPerUser] ON dbo.[UserBankAccount] ([UserId]) WHERE ([IsDefault] = 1);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'UX_UserBankAccount_UserBankNumber' AND object_id = OBJECT_ID(N'dbo.UserBankAccount'))
    CREATE UNIQUE INDEX [UX_UserBankAccount_UserBankNumber] ON dbo.[UserBankAccount] ([UserId], [BankCode], [AccountNumber]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_RefundTransaction_ReceiverBankCode' AND object_id = OBJECT_ID(N'dbo.RefundTransaction'))
    CREATE INDEX [IX_RefundTransaction_ReceiverBankCode] ON dbo.[RefundTransaction] ([ReceiverBankCode]);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_FarmerPayout_RecipientBankCode' AND object_id = OBJECT_ID(N'dbo.FarmerPayout'))
    CREATE INDEX [IX_FarmerPayout_RecipientBankCode] ON dbo.[FarmerPayout] ([RecipientBankCode]);

-- Demo accounts (only for demo users that exist and have no account yet).
INSERT dbo.[UserBankAccount] ([UserId], [BankCode], [AccountNumber], [AccountHolder], [IsDefault], [VerifiedAt], [VerificationStatus])
SELECT u.UserId, v.BankCode, v.AccountNumber, v.AccountHolder, 1, SYSUTCDATETIME(), 'Verified'
FROM (VALUES
    (N'farmer1',    'VBA',  '5300205111111',  N'NGUYEN VAN NONG'),
    (N'farmer2',    'VBA',  '7400205222222',  N'TRAN THI MUA'),
    (N'farmer3',    'BIDV', '65010003333333', N'LE HOANG LUA'),
    (N'farmer4',    'VCB',  '0451000444444',  N'PHAM VAN CAM'),
    (N'farmer5',    'ICB',  '107005555555',   N'HOANG MINH CA PHE'),
    (N'wholesale1', 'BIDV', '21510001234567', N'CONG TY THUC PHAM XANH A CHAU'),
    (N'wholesale2', 'ICB',  '108001234567',   N'HE THONG SIEU THI NONG SAN VIET'),
    (N'wholesale3', 'VPB',  '123456789',      N'CHUOI CUA HANG ORGANIC MART'),
    (N'wholesale4', 'STB',  '060123456789',   N'TAP DOAN XUAT KHAU NONG LAM'),
    (N'wholesale5', 'TPB',  '00012345678',    N'CONG TY CHE BIEN TRAI CAY MIEN NAM'),
    (N'retail1',    'VCB',  '1023456789',     N'DO MAI PHUONG'),
    (N'retail2',    'TCB',  '19034567890',    N'VU QUOC TUAN'),
    (N'retail3',    'MB',   '0934000003',     N'NGO THANH HA'),
    (N'retail4',    'ACB',  '24681357',       N'BUI ANH DUC'),
    (N'retail5',    'VBA',  '1500205123456',  N'DANG THU THAO')
) AS v (Username, BankCode, AccountNumber, AccountHolder)
JOIN dbo.[User] AS u ON u.Username = v.Username
WHERE NOT EXISTS (SELECT 1 FROM dbo.[UserBankAccount] AS a WHERE a.UserId = u.UserId);

-- Succeeded refunds without a receiving account: copy the complaint submitter's default account.
UPDATE r SET
    r.ReceiverBankCode = a.BankCode,
    r.ReceiverAccountNumber = a.AccountNumber,
    r.ReceiverAccountHolder = a.AccountHolder,
    r.ReceiverAccountProvidedAt = SYSUTCDATETIME()
FROM dbo.[RefundTransaction] AS r
JOIN dbo.[DisputeTicket] AS t ON t.DisputeTicketId = r.DisputeTicketId
JOIN dbo.[UserBankAccount] AS a ON a.UserId = t.SubmittedByUserId AND a.IsDefault = 1
WHERE r.ReceiverBankCode IS NULL AND r.RefundStatus = 'Succeeded';

COMMIT TRANSACTION;
GO

PRINT N'20261030_bank_accounts.sql: SupportedBank, UserBankAccount, refund receiver and payout recipient columns are ready.';
GO
