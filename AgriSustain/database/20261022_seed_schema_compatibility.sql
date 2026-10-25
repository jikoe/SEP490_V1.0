-- Add missing tables/columns used by the supplied full demo dataset.
-- Existing values and column types are preserved; new legacy fields stay NULL.
-- Table definitions were exported from the current EF model without connecting to SQL.
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET ARITHABORT ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET NUMERIC_ROUNDABORT OFF;
SET LOCK_TIMEOUT 15000;
BEGIN TRY
BEGIN TRANSACTION;
IF DB_NAME() <> N'AgriSustain' THROW 51100, 'Select AgriSustain before running this script.', 1;
DECLARE @NewTables TABLE (Name SYSNAME PRIMARY KEY);
IF OBJECT_ID(N'dbo.[B2BSubscriptionPackage]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [B2BSubscriptionPackage] (
    [PackageId] bigint NOT NULL IDENTITY,
    [PackageName] nvarchar(150) NOT NULL,
    [PackageDescription] nvarchar(1000) NULL,
    [DurationDays] int NOT NULL,
    [Price] decimal(18,2) NOT NULL,
    [UnlocksFarmerDirectContact] bit NOT NULL,
    [CanDownloadContractTemplate] bit NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    [TransactionLimit] int NULL,
    CONSTRAINT [PK__B2BSubsc__322035CC29DFA4C4] PRIMARY KEY ([PackageId])
);');
INSERT @NewTables VALUES (N'B2BSubscriptionPackage');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[B2BSubscriptionPackage]', N'CanDownloadContractTemplate') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscriptionPackage] ADD [CanDownloadContractTemplate] bit NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscriptionPackage]', N'DurationDays') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscriptionPackage] ADD [DurationDays] int NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscriptionPackage]', N'IsActive') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscriptionPackage] ADD [IsActive] bit NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscriptionPackage]', N'PackageDescription') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscriptionPackage] ADD [PackageDescription] nvarchar(1000) NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscriptionPackage]', N'PackageName') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscriptionPackage] ADD [PackageName] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscriptionPackage]', N'Price') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscriptionPackage] ADD [Price] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscriptionPackage]', N'TransactionLimit') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscriptionPackage] ADD [TransactionLimit] int NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscriptionPackage]', N'UnlocksFarmerDirectContact') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscriptionPackage] ADD [UnlocksFarmerDirectContact] bit NULL;');
END;
IF OBJECT_ID(N'dbo.[BusinessPolicy]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [BusinessPolicy] (
    [PolicyId] tinyint NOT NULL,
    [PayoutDelayHours] int NOT NULL DEFAULT 48,
    CONSTRAINT [PK_BusinessPolicy] PRIMARY KEY ([PolicyId])
);');
INSERT @NewTables VALUES (N'BusinessPolicy');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[BusinessPolicy]', N'PayoutDelayHours') IS NULL
EXEC(N'ALTER TABLE dbo.[BusinessPolicy] ADD [PayoutDelayHours] int NULL;');
END;
IF OBJECT_ID(N'dbo.[Category]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [Category] (
    [CategoryId] bigint NOT NULL IDENTITY,
    [ParentCategoryId] bigint NULL,
    [CategoryName] nvarchar(150) NOT NULL,
    [Description] nvarchar(500) NULL,
    [Priority] int NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK__Category__19093A0B9F2880E0] PRIMARY KEY ([CategoryId]),
    CONSTRAINT [FK_Category_ParentCategory] FOREIGN KEY ([ParentCategoryId]) REFERENCES [Category] ([CategoryId])
);');
INSERT @NewTables VALUES (N'Category');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[Category]', N'CategoryName') IS NULL
EXEC(N'ALTER TABLE dbo.[Category] ADD [CategoryName] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[Category]', N'Description') IS NULL
EXEC(N'ALTER TABLE dbo.[Category] ADD [Description] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[Category]', N'IsActive') IS NULL
EXEC(N'ALTER TABLE dbo.[Category] ADD [IsActive] bit NULL;');
IF COL_LENGTH(N'dbo.[Category]', N'ParentCategoryId') IS NULL
EXEC(N'ALTER TABLE dbo.[Category] ADD [ParentCategoryId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Category]', N'Priority') IS NULL
EXEC(N'ALTER TABLE dbo.[Category] ADD [Priority] int NULL;');
END;
IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [User] (
    [UserId] bigint NOT NULL IDENTITY,
    [Username] nvarchar(100) NOT NULL,
    [Email] nvarchar(255) NOT NULL,
    [PasswordHash] nvarchar(500) NOT NULL,
    [FullName] nvarchar(150) NOT NULL,
    [PhoneNumber] varchar(20) NULL,
    [AvatarUrl] nvarchar(500) NULL,
    [Role] varchar(50) NOT NULL,
    [Status] varchar(50) NOT NULL DEFAULT ''Active'',
    [IsEmailVerified] bit NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [FailedLoginCount] int NOT NULL,
    [LockedUntil] datetime2 NULL,
    CONSTRAINT [PK__User__1788CC4C1EE96F0D] PRIMARY KEY ([UserId])
);');
INSERT @NewTables VALUES (N'User');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[User]', N'AvatarUrl') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [AvatarUrl] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[User]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[User]', N'Email') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [Email] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[User]', N'FailedLoginCount') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [FailedLoginCount] int NULL;');
IF COL_LENGTH(N'dbo.[User]', N'FullName') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [FullName] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[User]', N'IsEmailVerified') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [IsEmailVerified] bit NULL;');
IF COL_LENGTH(N'dbo.[User]', N'LockedUntil') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [LockedUntil] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[User]', N'PasswordHash') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [PasswordHash] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[User]', N'PhoneNumber') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [PhoneNumber] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[User]', N'Role') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [Role] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[User]', N'Status') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [Status] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[User]', N'Username') IS NULL
EXEC(N'ALTER TABLE dbo.[User] ADD [Username] nvarchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[CategorySettings]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [CategorySettings] (
    [CategoryId] bigint NOT NULL,
    [IsArchived] bit NOT NULL,
    [ImageBytes] varbinary(max) NULL,
    [ImageContentType] nvarchar(30) NULL,
    CONSTRAINT [PK_CategorySettings] PRIMARY KEY ([CategoryId]),
    CONSTRAINT [FK_CategorySettings_Category_CategoryId] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([CategoryId]) ON DELETE CASCADE
);');
INSERT @NewTables VALUES (N'CategorySettings');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[CategorySettings]', N'ImageBytes') IS NULL
EXEC(N'ALTER TABLE dbo.[CategorySettings] ADD [ImageBytes] varbinary(max) NULL;');
IF COL_LENGTH(N'dbo.[CategorySettings]', N'ImageContentType') IS NULL
EXEC(N'ALTER TABLE dbo.[CategorySettings] ADD [ImageContentType] nvarchar(30) NULL;');
IF COL_LENGTH(N'dbo.[CategorySettings]', N'IsArchived') IS NULL
EXEC(N'ALTER TABLE dbo.[CategorySettings] ADD [IsArchived] bit NULL;');
END;
IF OBJECT_ID(N'dbo.[CommissionPolicy]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [CommissionPolicy] (
    [CommissionPolicyId] bigint NOT NULL IDENTITY,
    [CategoryId] bigint NOT NULL,
    [ApplicableOrderType] varchar(50) NOT NULL,
    [CommissionRate] decimal(5,2) NOT NULL,
    [EffectiveDate] date NOT NULL,
    [Note] nvarchar(500) NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK__Commissi__ACB43F57B11EE7AE] PRIMARY KEY ([CommissionPolicyId]),
    CONSTRAINT [FK_CommissionPolicy_Category] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([CategoryId])
);');
INSERT @NewTables VALUES (N'CommissionPolicy');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[CommissionPolicy]', N'ApplicableOrderType') IS NULL
EXEC(N'ALTER TABLE dbo.[CommissionPolicy] ADD [ApplicableOrderType] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[CommissionPolicy]', N'CategoryId') IS NULL
EXEC(N'ALTER TABLE dbo.[CommissionPolicy] ADD [CategoryId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CommissionPolicy]', N'CommissionRate') IS NULL
EXEC(N'ALTER TABLE dbo.[CommissionPolicy] ADD [CommissionRate] decimal(5,2) NULL;');
IF COL_LENGTH(N'dbo.[CommissionPolicy]', N'EffectiveDate') IS NULL
EXEC(N'ALTER TABLE dbo.[CommissionPolicy] ADD [EffectiveDate] date NULL;');
IF COL_LENGTH(N'dbo.[CommissionPolicy]', N'IsActive') IS NULL
EXEC(N'ALTER TABLE dbo.[CommissionPolicy] ADD [IsActive] bit NULL;');
IF COL_LENGTH(N'dbo.[CommissionPolicy]', N'Note') IS NULL
EXEC(N'ALTER TABLE dbo.[CommissionPolicy] ADD [Note] nvarchar(500) NULL;');
END;
IF OBJECT_ID(N'dbo.[RegionalPriceBenchmark]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [RegionalPriceBenchmark] (
    [BenchmarkId] bigint NOT NULL IDENTITY,
    [CategoryId] bigint NOT NULL,
    [RegionName] nvarchar(100) NOT NULL,
    [ProvinceName] nvarchar(100) NOT NULL,
    [CommodityName] nvarchar(150) NOT NULL,
    [MinPrice] decimal(18,2) NOT NULL,
    [MaxPrice] decimal(18,2) NOT NULL,
    [AveragePrice] decimal(18,2) NOT NULL,
    [RecordedDate] date NOT NULL,
    CONSTRAINT [PK__Regional__2CE9029CE70A5E0A] PRIMARY KEY ([BenchmarkId]),
    CONSTRAINT [FK_RegionalPriceBenchmark_Category] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([CategoryId])
);');
INSERT @NewTables VALUES (N'RegionalPriceBenchmark');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[RegionalPriceBenchmark]', N'AveragePrice') IS NULL
EXEC(N'ALTER TABLE dbo.[RegionalPriceBenchmark] ADD [AveragePrice] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[RegionalPriceBenchmark]', N'CategoryId') IS NULL
EXEC(N'ALTER TABLE dbo.[RegionalPriceBenchmark] ADD [CategoryId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RegionalPriceBenchmark]', N'CommodityName') IS NULL
EXEC(N'ALTER TABLE dbo.[RegionalPriceBenchmark] ADD [CommodityName] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[RegionalPriceBenchmark]', N'MaxPrice') IS NULL
EXEC(N'ALTER TABLE dbo.[RegionalPriceBenchmark] ADD [MaxPrice] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[RegionalPriceBenchmark]', N'MinPrice') IS NULL
EXEC(N'ALTER TABLE dbo.[RegionalPriceBenchmark] ADD [MinPrice] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[RegionalPriceBenchmark]', N'ProvinceName') IS NULL
EXEC(N'ALTER TABLE dbo.[RegionalPriceBenchmark] ADD [ProvinceName] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[RegionalPriceBenchmark]', N'RecordedDate') IS NULL
EXEC(N'ALTER TABLE dbo.[RegionalPriceBenchmark] ADD [RecordedDate] date NULL;');
IF COL_LENGTH(N'dbo.[RegionalPriceBenchmark]', N'RegionName') IS NULL
EXEC(N'ALTER TABLE dbo.[RegionalPriceBenchmark] ADD [RegionName] nvarchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[AuditLog]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [AuditLog] (
    [AuditLogId] bigint NOT NULL IDENTITY,
    [PerformedByUserId] bigint NULL,
    [EntityType] varchar(100) NOT NULL,
    [Action] varchar(100) NOT NULL,
    [OldValue] nvarchar(max) NULL,
    [NewValue] nvarchar(max) NULL,
    [Reason] nvarchar(500) NULL,
    [EntityId] bigint NULL,
    [TraceId] nvarchar(64) NULL,
    [OccurredAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__AuditLog__EB5F6CBDEBB68346] PRIMARY KEY ([AuditLogId]),
    CONSTRAINT [FK_AuditLog_PerformedByUser] FOREIGN KEY ([PerformedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'AuditLog');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[AuditLog]', N'Action') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [Action] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[AuditLog]', N'EntityId') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [EntityId] bigint NULL;');
IF COL_LENGTH(N'dbo.[AuditLog]', N'EntityType') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [EntityType] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[AuditLog]', N'NewValue') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [NewValue] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[AuditLog]', N'OccurredAt') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [OccurredAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[AuditLog]', N'OldValue') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [OldValue] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[AuditLog]', N'PerformedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [PerformedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[AuditLog]', N'Reason') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [Reason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[AuditLog]', N'TraceId') IS NULL
EXEC(N'ALTER TABLE dbo.[AuditLog] ADD [TraceId] nvarchar(64) NULL;');
END;
IF OBJECT_ID(N'dbo.[Cart]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [Cart] (
    [CartId] bigint NOT NULL IDENTITY,
    [UserId] bigint NOT NULL,
    [UpdatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__Cart__51BCD7B7477C1058] PRIMARY KEY ([CartId]),
    CONSTRAINT [FK_Cart_User] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'Cart');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[Cart]', N'UpdatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Cart] ADD [UpdatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Cart]', N'UserId') IS NULL
EXEC(N'ALTER TABLE dbo.[Cart] ADD [UserId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[DeliveryAddress]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [DeliveryAddress] (
    [DeliveryAddressId] bigint NOT NULL IDENTITY,
    [UserId] bigint NOT NULL,
    [RecipientName] nvarchar(150) NOT NULL,
    [RecipientPhone] varchar(20) NOT NULL,
    [AddressDetail] nvarchar(500) NOT NULL,
    [ProvinceCity] nvarchar(100) NOT NULL,
    [District] nvarchar(100) NULL,
    [Ward] nvarchar(100) NOT NULL,
    [ProvinceCode] varchar(20) NULL,
    [WardCode] varchar(20) NULL,
    [AddressCatalogVersion] varchar(100) NULL,
    [IsDefault] bit NOT NULL,
    [Status] varchar(50) NOT NULL DEFAULT ''Active'',
    CONSTRAINT [PK__Delivery__4EBD54FCAEA037AF] PRIMARY KEY ([DeliveryAddressId]),
    CONSTRAINT [FK_DeliveryAddress_User] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'DeliveryAddress');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'AddressCatalogVersion') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [AddressCatalogVersion] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'AddressDetail') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [AddressDetail] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'District') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [District] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'IsDefault') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [IsDefault] bit NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'ProvinceCity') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [ProvinceCity] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'ProvinceCode') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [ProvinceCode] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'RecipientName') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [RecipientName] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'RecipientPhone') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [RecipientPhone] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'Status') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [Status] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'UserId') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [UserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'Ward') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [Ward] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[DeliveryAddress]', N'WardCode') IS NULL
EXEC(N'ALTER TABLE dbo.[DeliveryAddress] ADD [WardCode] varchar(20) NULL;');
END;
IF OBJECT_ID(N'dbo.[FarmerProfile]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [FarmerProfile] (
    [FarmerProfileId] bigint NOT NULL IDENTITY,
    [UserId] bigint NOT NULL,
    [FarmName] nvarchar(200) NOT NULL,
    [RepresentativeName] nvarchar(150) NOT NULL,
    [ProductionScale] nvarchar(200) NULL,
    [BusinessLicenseNumber] varchar(100) NULL,
    [Address] nvarchar(500) NOT NULL,
    [ProvinceCode] varchar(20) NOT NULL,
    [CultivationZone] nvarchar(200) NULL,
    [GPSCoordinates] varchar(100) NULL,
    [VerificationStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [ApprovedAt] datetime2 NULL,
    [RejectReason] nvarchar(500) NULL,
    [ReputationScore] decimal(5,2) NOT NULL,
    CONSTRAINT [PK__FarmerPr__688AB36DFA9EF075] PRIMARY KEY ([FarmerProfileId]),
    CONSTRAINT [FK_FarmerProfile_User] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'FarmerProfile');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'Address') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [Address] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'ApprovedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [ApprovedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'BusinessLicenseNumber') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [BusinessLicenseNumber] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'CultivationZone') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [CultivationZone] nvarchar(200) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'FarmName') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [FarmName] nvarchar(200) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'GPSCoordinates') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [GPSCoordinates] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'ProductionScale') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [ProductionScale] nvarchar(200) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'ProvinceCode') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [ProvinceCode] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'RejectReason') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [RejectReason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'RepresentativeName') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [RepresentativeName] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'ReputationScore') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [ReputationScore] decimal(5,2) NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'UserId') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [UserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'VerificationStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerProfile] ADD [VerificationStatus] varchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[Notification]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [Notification] (
    [NotificationId] bigint NOT NULL IDENTITY,
    [UserId] bigint NOT NULL,
    [Title] nvarchar(255) NOT NULL,
    [Content] nvarchar(max) NOT NULL,
    [NotificationType] varchar(50) NOT NULL,
    [IsRead] bit NOT NULL,
    [SentAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [DeliveryChannel] varchar(50) NOT NULL,
    [DeliveryStatus] varchar(50) NOT NULL DEFAULT ''Sent'',
    [RetryCount] int NOT NULL,
    [RelatedRecordType] varchar(100) NULL,
    [RelatedRecordId] bigint NULL,
    [PayloadJson] nvarchar(max) NULL,
    CONSTRAINT [PK__Notifica__20CF2E12BBC7A3FA] PRIMARY KEY ([NotificationId]),
    CONSTRAINT [FK_Notification_User] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'Notification');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[Notification]', N'Content') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [Content] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'DeliveryChannel') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [DeliveryChannel] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'DeliveryStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [DeliveryStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'IsRead') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [IsRead] bit NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'NotificationType') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [NotificationType] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'PayloadJson') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [PayloadJson] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'RelatedRecordId') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [RelatedRecordId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'RelatedRecordType') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [RelatedRecordType] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'RetryCount') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [RetryCount] int NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'SentAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [SentAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'Title') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [Title] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[Notification]', N'UserId') IS NULL
EXEC(N'ALTER TABLE dbo.[Notification] ADD [UserId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[RetailCheckoutQuote]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [RetailCheckoutQuote] (
    [QuoteId] uniqueidentifier NOT NULL,
    [BuyerUserId] bigint NOT NULL,
    [AcceptedSnapshotJson] nvarchar(max) NOT NULL,
    [Fingerprint] char(64) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [ExpiresAt] datetime2 NOT NULL,
    CONSTRAINT [PK__RetailCh__AF9688C15E30EAE0] PRIMARY KEY ([QuoteId]),
    CONSTRAINT [AK_RetailCheckoutQuote_BuyerUserId_QuoteId] UNIQUE ([BuyerUserId], [QuoteId]),
    CONSTRAINT [FK_RetailQuote_Buyer] FOREIGN KEY ([BuyerUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'RetailCheckoutQuote');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[RetailCheckoutQuote]', N'AcceptedSnapshotJson') IS NULL
EXEC(N'ALTER TABLE dbo.[RetailCheckoutQuote] ADD [AcceptedSnapshotJson] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[RetailCheckoutQuote]', N'BuyerUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[RetailCheckoutQuote] ADD [BuyerUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RetailCheckoutQuote]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[RetailCheckoutQuote] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[RetailCheckoutQuote]', N'ExpiresAt') IS NULL
EXEC(N'ALTER TABLE dbo.[RetailCheckoutQuote] ADD [ExpiresAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[RetailCheckoutQuote]', N'Fingerprint') IS NULL
EXEC(N'ALTER TABLE dbo.[RetailCheckoutQuote] ADD [Fingerprint] char(64) NULL;');
END;
IF OBJECT_ID(N'dbo.[WholesaleBuyerProfile]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [WholesaleBuyerProfile] (
    [WholesaleBuyerProfileId] bigint NOT NULL IDENTITY,
    [UserId] bigint NOT NULL,
    [CompanyName] nvarchar(255) NOT NULL,
    [TaxCode] varchar(50) NOT NULL,
    [RepresentativeName] nvarchar(150) NOT NULL,
    [BusinessType] nvarchar(100) NULL,
    [BusinessAddress] nvarchar(500) NOT NULL,
    [DocumentUrl] nvarchar(500) NULL,
    [VerificationStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [ApprovedAt] datetime2 NULL,
    [RejectReason] nvarchar(500) NULL,
    CONSTRAINT [PK__Wholesal__5F36EBC606ECC49B] PRIMARY KEY ([WholesaleBuyerProfileId]),
    CONSTRAINT [FK_WholesaleBuyerProfile_User] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'WholesaleBuyerProfile');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'ApprovedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [ApprovedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'BusinessAddress') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [BusinessAddress] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'BusinessType') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [BusinessType] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'CompanyName') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [CompanyName] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'DocumentUrl') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [DocumentUrl] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'RejectReason') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [RejectReason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'RepresentativeName') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [RepresentativeName] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'TaxCode') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [TaxCode] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'UserId') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [UserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[WholesaleBuyerProfile]', N'VerificationStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[WholesaleBuyerProfile] ADD [VerificationStatus] varchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[FarmCertificate]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [FarmCertificate] (
    [FarmCertificateId] bigint NOT NULL IDENTITY,
    [FarmerProfileId] bigint NOT NULL,
    [ReviewedByUserId] bigint NULL,
    [CertificateType] nvarchar(100) NOT NULL,
    [CertificateNumber] varchar(100) NOT NULL,
    [IssuedBy] nvarchar(200) NOT NULL,
    [IssueDate] date NOT NULL,
    [ExpiryDate] date NULL,
    [DocumentUrl] nvarchar(500) NOT NULL,
    [VerificationStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [RevocationReason] nvarchar(500) NULL,
    [ReviewedAt] datetime2 NULL,
    CONSTRAINT [PK__FarmCert__2BDF0082B0EF348B] PRIMARY KEY ([FarmCertificateId]),
    CONSTRAINT [FK_FarmCertificate_FarmerProfile] FOREIGN KEY ([FarmerProfileId]) REFERENCES [FarmerProfile] ([FarmerProfileId]),
    CONSTRAINT [FK_FarmCertificate_ReviewedByUser] FOREIGN KEY ([ReviewedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'FarmCertificate');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'CertificateNumber') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [CertificateNumber] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'CertificateType') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [CertificateType] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'DocumentUrl') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [DocumentUrl] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'ExpiryDate') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [ExpiryDate] date NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'FarmerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [FarmerProfileId] bigint NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'IssueDate') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [IssueDate] date NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'IssuedBy') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [IssuedBy] nvarchar(200) NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'ReviewedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [ReviewedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'ReviewedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [ReviewedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'RevocationReason') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [RevocationReason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[FarmCertificate]', N'VerificationStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmCertificate] ADD [VerificationStatus] varchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[FarmerPayout]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [FarmerPayout] (
    [FarmerPayoutId] bigint NOT NULL IDENTITY,
    [FarmerProfileId] bigint NOT NULL,
    [ApprovedByUserId] bigint NULL,
    [PayoutCode] varchar(100) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [DeductedCommission] decimal(18,2) NOT NULL,
    [NetAmount] decimal(18,2) NOT NULL,
    [PayoutStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [BankTransferRef] varchar(150) NULL,
    [ProcessedAt] datetime2 NULL,
    [ApprovedAt] datetime2 NULL,
    [EligibleAt] datetime2 NULL,
    [RequestKey] varchar(100) NOT NULL,
    CONSTRAINT [PK__FarmerPa__80AAF9C346B5F80F] PRIMARY KEY ([FarmerPayoutId]),
    CONSTRAINT [FK_FarmerPayout_ApprovedByUser] FOREIGN KEY ([ApprovedByUserId]) REFERENCES [User] ([UserId]),
    CONSTRAINT [FK_FarmerPayout_FarmerProfile] FOREIGN KEY ([FarmerProfileId]) REFERENCES [FarmerProfile] ([FarmerProfileId])
);');
INSERT @NewTables VALUES (N'FarmerPayout');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'Amount') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [Amount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'ApprovedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [ApprovedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'ApprovedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [ApprovedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'BankTransferRef') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [BankTransferRef] varchar(150) NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'DeductedCommission') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [DeductedCommission] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'EligibleAt') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [EligibleAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'FarmerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [FarmerProfileId] bigint NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'NetAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [NetAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'PayoutCode') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [PayoutCode] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'PayoutStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [PayoutStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'ProcessedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [ProcessedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[FarmerPayout]', N'RequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[FarmerPayout] ADD [RequestKey] varchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[Product]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [Product] (
    [ProductId] bigint NOT NULL IDENTITY,
    [CategoryId] bigint NOT NULL,
    [FarmerProfileId] bigint NOT NULL,
    [ProductName] nvarchar(255) NOT NULL,
    [Description] nvarchar(max) NULL,
    [Unit] nvarchar(50) NOT NULL,
    [OriginProvince] nvarchar(100) NULL,
    [BaseRetailPrice] decimal(18,2) NOT NULL,
    [WholesaleMinQuantity] decimal(18,3) NULL,
    [WholesalePrice] decimal(18,2) NULL,
    [ApprovalStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [IsPublished] bit NOT NULL,
    [RejectionReason] nvarchar(500) NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__Product__B40CC6CD4E506CFE] PRIMARY KEY ([ProductId]),
    CONSTRAINT [FK_Product_Category] FOREIGN KEY ([CategoryId]) REFERENCES [Category] ([CategoryId]),
    CONSTRAINT [FK_Product_FarmerProfile] FOREIGN KEY ([FarmerProfileId]) REFERENCES [FarmerProfile] ([FarmerProfileId])
);');
INSERT @NewTables VALUES (N'Product');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[Product]', N'ApprovalStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [ApprovalStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'BaseRetailPrice') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [BaseRetailPrice] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'CategoryId') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [CategoryId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'Description') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [Description] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'FarmerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [FarmerProfileId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'IsPublished') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [IsPublished] bit NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'OriginProvince') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [OriginProvince] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'ProductName') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [ProductName] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'RejectionReason') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [RejectionReason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'Unit') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [Unit] nvarchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'WholesaleMinQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [WholesaleMinQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[Product]', N'WholesalePrice') IS NULL
EXEC(N'ALTER TABLE dbo.[Product] ADD [WholesalePrice] decimal(18,2) NULL;');
END;
IF OBJECT_ID(N'dbo.[Voucher]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [Voucher] (
    [VoucherId] bigint NOT NULL IDENTITY,
    [FarmerProfileId] bigint NULL,
    [VoucherCode] varchar(100) NOT NULL,
    [DiscountType] varchar(50) NOT NULL,
    [DiscountValue] decimal(18,2) NOT NULL,
    [MaxDiscountAmount] decimal(18,2) NULL,
    [MinOrderAmount] decimal(18,2) NOT NULL,
    [UsageLimit] int NULL,
    [UsedCount] int NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [IsActive] bit NOT NULL DEFAULT CAST(1 AS bit),
    CONSTRAINT [PK__Voucher__3AEE7921FD2C713E] PRIMARY KEY ([VoucherId]),
    CONSTRAINT [FK_Voucher_FarmerProfile] FOREIGN KEY ([FarmerProfileId]) REFERENCES [FarmerProfile] ([FarmerProfileId])
);');
INSERT @NewTables VALUES (N'Voucher');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[Voucher]', N'DiscountType') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [DiscountType] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'DiscountValue') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [DiscountValue] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'EndDate') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [EndDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'FarmerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [FarmerProfileId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'IsActive') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [IsActive] bit NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'MaxDiscountAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [MaxDiscountAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'MinOrderAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [MinOrderAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'StartDate') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [StartDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'UsageLimit') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [UsageLimit] int NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'UsedCount') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [UsedCount] int NULL;');
IF COL_LENGTH(N'dbo.[Voucher]', N'VoucherCode') IS NULL
EXEC(N'ALTER TABLE dbo.[Voucher] ADD [VoucherCode] varchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [Order] (
    [OrderId] bigint NOT NULL IDENTITY,
    [BuyerUserId] bigint NOT NULL,
    [OrderCode] varchar(100) NOT NULL,
    [OrderType] varchar(50) NOT NULL,
    [IsPreOrder] bit NOT NULL,
    [OrderDate] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [ExpectedDeliveryDate] datetime2 NULL,
    [ShippingAddress] nvarchar(500) NOT NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [DiscountAmount] decimal(18,2) NOT NULL,
    [DeliveryFee] decimal(18,2) NOT NULL,
    [FinalAmount] decimal(18,2) NOT NULL,
    [OrderStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [ReservationExpiresAt] datetime2 NULL,
    [RecipientNameSnapshot] nvarchar(150) NOT NULL,
    [RecipientPhoneSnapshot] varchar(20) NOT NULL,
    [CompletedAt] datetime2 NULL,
    [RetailQuoteId] uniqueidentifier NULL,
    [DeliveryMethod] varchar(50) NULL,
    [PaymentMethod] varchar(50) NULL,
    [OrderNotes] nvarchar(300) NULL,
    [VoucherCodeSnapshot] varchar(100) NULL,
    [VoucherDiscount] decimal(18,2) NULL,
    [PointDiscount] decimal(18,2) NULL,
    [RedeemedPoints] int NULL,
    [ProvinceCodeSnapshot] varchar(20) NULL,
    [ProvinceNameSnapshot] nvarchar(100) NULL,
    [WardCodeSnapshot] varchar(20) NULL,
    [WardNameSnapshot] nvarchar(100) NULL,
    [AddressDetailSnapshot] nvarchar(255) NULL,
    [AddressCatalogVersionSnapshot] varchar(100) NULL,
    [CheckoutRequestKey] varchar(100) NULL,
    [CheckoutCommittedAt] datetime2 NULL,
    CONSTRAINT [PK__Order__C3905BCFB2FB2F0F] PRIMARY KEY ([OrderId]),
    CONSTRAINT [FK_Order_BuyerUser] FOREIGN KEY ([BuyerUserId]) REFERENCES [User] ([UserId]),
    CONSTRAINT [FK_Order_RetailQuote_Buyer] FOREIGN KEY ([BuyerUserId], [RetailQuoteId]) REFERENCES [RetailCheckoutQuote] ([BuyerUserId], [QuoteId])
);');
INSERT @NewTables VALUES (N'Order');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[Order]', N'AddressCatalogVersionSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [AddressCatalogVersionSnapshot] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'AddressDetailSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [AddressDetailSnapshot] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'BuyerUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [BuyerUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'CheckoutCommittedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [CheckoutCommittedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'CheckoutRequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [CheckoutRequestKey] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'CompletedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [CompletedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'DeliveryFee') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [DeliveryFee] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'DeliveryMethod') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [DeliveryMethod] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'DiscountAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [DiscountAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'ExpectedDeliveryDate') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [ExpectedDeliveryDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'FinalAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [FinalAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'IsPreOrder') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [IsPreOrder] bit NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'OrderCode') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [OrderCode] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'OrderDate') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [OrderDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'OrderNotes') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [OrderNotes] nvarchar(300) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'OrderStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [OrderStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'OrderType') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [OrderType] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'PaymentMethod') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [PaymentMethod] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'PointDiscount') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [PointDiscount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'ProvinceCodeSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [ProvinceCodeSnapshot] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'ProvinceNameSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [ProvinceNameSnapshot] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'RecipientNameSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [RecipientNameSnapshot] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'RecipientPhoneSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [RecipientPhoneSnapshot] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'RedeemedPoints') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [RedeemedPoints] int NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'ReservationExpiresAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [ReservationExpiresAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'RetailQuoteId') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [RetailQuoteId] uniqueidentifier NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'ShippingAddress') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [ShippingAddress] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'TotalAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [TotalAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'VoucherCodeSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [VoucherCodeSnapshot] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'VoucherDiscount') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [VoucherDiscount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'WardCodeSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [WardCodeSnapshot] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[Order]', N'WardNameSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[Order] ADD [WardNameSnapshot] nvarchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[B2BContract]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [B2BContract] (
    [B2BContractId] bigint NOT NULL IDENTITY,
    [WholesaleBuyerProfileId] bigint NOT NULL,
    [FarmerProfileId] bigint NOT NULL,
    [ContractNumber] varchar(100) NOT NULL,
    [DepositAmount] decimal(18,2) NOT NULL,
    [PaymentTerms] nvarchar(1000) NULL,
    [DigitalSignatureStatus] varchar(50) NOT NULL DEFAULT ''Unsigned'',
    [SignedDate] datetime2 NULL,
    [Status] varchar(50) NOT NULL DEFAULT ''Draft'',
    [CurrentVersionNo] int NOT NULL DEFAULT 1,
    [ValidFrom] date NOT NULL,
    [ValidUntil] date NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [CompletedAt] datetime2 NULL,
    CONSTRAINT [PK__B2BContr__59105F1E7BBB5CF4] PRIMARY KEY ([B2BContractId]),
    CONSTRAINT [FK_B2BContract_FarmerProfile] FOREIGN KEY ([FarmerProfileId]) REFERENCES [FarmerProfile] ([FarmerProfileId]),
    CONSTRAINT [FK_B2BContract_WholesaleBuyer] FOREIGN KEY ([WholesaleBuyerProfileId]) REFERENCES [WholesaleBuyerProfile] ([WholesaleBuyerProfileId])
);');
INSERT @NewTables VALUES (N'B2BContract');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[B2BContract]', N'CompletedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [CompletedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'ContractNumber') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [ContractNumber] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'CurrentVersionNo') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [CurrentVersionNo] int NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'DepositAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [DepositAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'DigitalSignatureStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [DigitalSignatureStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'FarmerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [FarmerProfileId] bigint NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'PaymentTerms') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [PaymentTerms] nvarchar(1000) NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'SignedDate') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [SignedDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'Status') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [Status] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'ValidFrom') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [ValidFrom] date NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'ValidUntil') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [ValidUntil] date NULL;');
IF COL_LENGTH(N'dbo.[B2BContract]', N'WholesaleBuyerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BContract] ADD [WholesaleBuyerProfileId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[B2BSubscription]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [B2BSubscription] (
    [B2BSubscriptionId] bigint NOT NULL IDENTITY,
    [WholesaleBuyerProfileId] bigint NOT NULL,
    [PackageId] bigint NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [SubscriptionStatus] varchar(50) NOT NULL DEFAULT ''Active'',
    [AutoRenew] bit NOT NULL,
    [PriceSnapshot] decimal(18,2) NOT NULL,
    [LastRenewedAt] datetime2 NULL,
    [UnlocksFarmerDirectContactSnapshot] bit NOT NULL,
    [CanDownloadContractTemplateSnapshot] bit NOT NULL,
    [TransactionLimitSnapshot] int NULL,
    [DurationDaysSnapshot] int NOT NULL,
    CONSTRAINT [PK__B2BSubsc__EE8E09CC4C9F4139] PRIMARY KEY ([B2BSubscriptionId]),
    CONSTRAINT [FK_B2BSubscription_Package] FOREIGN KEY ([PackageId]) REFERENCES [B2BSubscriptionPackage] ([PackageId]),
    CONSTRAINT [FK_B2BSubscription_WholesaleBuyer] FOREIGN KEY ([WholesaleBuyerProfileId]) REFERENCES [WholesaleBuyerProfile] ([WholesaleBuyerProfileId])
);');
INSERT @NewTables VALUES (N'B2BSubscription');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'AutoRenew') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [AutoRenew] bit NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'CanDownloadContractTemplateSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [CanDownloadContractTemplateSnapshot] bit NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'DurationDaysSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [DurationDaysSnapshot] int NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'EndDate') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [EndDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'LastRenewedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [LastRenewedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'PackageId') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [PackageId] bigint NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'PriceSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [PriceSnapshot] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'StartDate') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [StartDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'SubscriptionStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [SubscriptionStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'TransactionLimitSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [TransactionLimitSnapshot] int NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'UnlocksFarmerDirectContactSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [UnlocksFarmerDirectContactSnapshot] bit NULL;');
IF COL_LENGTH(N'dbo.[B2BSubscription]', N'WholesaleBuyerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[B2BSubscription] ADD [WholesaleBuyerProfileId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[CropSeason]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [CropSeason] (
    [CropSeasonId] bigint NOT NULL IDENTITY,
    [FarmerProfileId] bigint NOT NULL,
    [SeasonName] nvarchar(200) NOT NULL,
    [CropVariety] nvarchar(150) NOT NULL,
    [CultivatedArea] decimal(18,3) NOT NULL,
    [PlantingDate] date NOT NULL,
    [EstimatedHarvestDate] date NOT NULL,
    [ActualHarvestDate] date NULL,
    [EstimatedYield] decimal(18,3) NOT NULL,
    [ActualYield] decimal(18,3) NULL,
    [Status] varchar(50) NOT NULL DEFAULT ''Planning'',
    [ProductId] bigint NOT NULL,
    [YieldUnit] nvarchar(50) NOT NULL,
    CONSTRAINT [PK__CropSeas__675AF5007DF309C1] PRIMARY KEY ([CropSeasonId]),
    CONSTRAINT [FK_CropSeason_FarmerProfile] FOREIGN KEY ([FarmerProfileId]) REFERENCES [FarmerProfile] ([FarmerProfileId]),
    CONSTRAINT [FK_CropSeason_Product] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([ProductId])
);');
INSERT @NewTables VALUES (N'CropSeason');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[CropSeason]', N'ActualHarvestDate') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [ActualHarvestDate] date NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'ActualYield') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [ActualYield] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'CropVariety') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [CropVariety] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'CultivatedArea') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [CultivatedArea] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'EstimatedHarvestDate') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [EstimatedHarvestDate] date NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'EstimatedYield') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [EstimatedYield] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'FarmerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [FarmerProfileId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'PlantingDate') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [PlantingDate] date NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'ProductId') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [ProductId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'SeasonName') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [SeasonName] nvarchar(200) NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'Status') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [Status] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[CropSeason]', N'YieldUnit') IS NULL
EXEC(N'ALTER TABLE dbo.[CropSeason] ADD [YieldUnit] nvarchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[ProductImage]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ProductImage] (
    [ProductImageId] bigint NOT NULL IDENTITY,
    [ProductId] bigint NOT NULL,
    [ImageUrl] nvarchar(500) NOT NULL,
    [DisplayOrder] int NOT NULL,
    [IsPrimary] bit NOT NULL,
    [AltText] nvarchar(255) NULL,
    CONSTRAINT [PK__ProductI__07B2B1B88DF54735] PRIMARY KEY ([ProductImageId]),
    CONSTRAINT [FK_ProductImage_Product] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([ProductId])
);');
INSERT @NewTables VALUES (N'ProductImage');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ProductImage]', N'AltText') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductImage] ADD [AltText] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[ProductImage]', N'DisplayOrder') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductImage] ADD [DisplayOrder] int NULL;');
IF COL_LENGTH(N'dbo.[ProductImage]', N'ImageUrl') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductImage] ADD [ImageUrl] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[ProductImage]', N'IsPrimary') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductImage] ADD [IsPrimary] bit NULL;');
IF COL_LENGTH(N'dbo.[ProductImage]', N'ProductId') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductImage] ADD [ProductId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[RewardPointEntry]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [RewardPointEntry] (
    [RewardPointEntryId] bigint NOT NULL IDENTITY,
    [UserId] bigint NOT NULL,
    [OrderId] bigint NULL,
    [EntryType] varchar(20) NOT NULL,
    [Points] int NOT NULL,
    [RequestKey] varchar(100) NOT NULL,
    [AvailableDelta] AS (CONVERT([bigint],case when [EntryType]=''Refund'' OR [EntryType]=''Release'' OR [EntryType]=''Earn'' then [Points] when [EntryType]=''Hold'' then  -CONVERT([bigint],[Points]) else (0) end)) PERSISTED,
    [HeldDelta] AS (CONVERT([bigint],case when [EntryType]=''Hold'' then [Points] when [EntryType]=''Release'' OR [EntryType]=''Consume'' then  -CONVERT([bigint],[Points]) else (0) end)) PERSISTED,
    [OccurredAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__RewardPo__3B7DF33DB11A3529] PRIMARY KEY ([RewardPointEntryId]),
    CONSTRAINT [FK__RewardPoi__Order__6F4A8121] FOREIGN KEY ([OrderId]) REFERENCES [Order] ([OrderId]),
    CONSTRAINT [FK__RewardPoi__UserI__6E565CE8] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'RewardPointEntry');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[RewardPointEntry]', N'AvailableDelta') IS NULL
EXEC(N'ALTER TABLE dbo.[RewardPointEntry] ADD [AvailableDelta] AS (CONVERT([bigint],case when [EntryType]=''Refund'' OR [EntryType]=''Release'' OR [EntryType]=''Earn'' then [Points] when [EntryType]=''Hold'' then  -CONVERT([bigint],[Points]) else (0) end)) PERSISTED;');
IF COL_LENGTH(N'dbo.[RewardPointEntry]', N'EntryType') IS NULL
EXEC(N'ALTER TABLE dbo.[RewardPointEntry] ADD [EntryType] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[RewardPointEntry]', N'HeldDelta') IS NULL
EXEC(N'ALTER TABLE dbo.[RewardPointEntry] ADD [HeldDelta] AS (CONVERT([bigint],case when [EntryType]=''Hold'' then [Points] when [EntryType]=''Release'' OR [EntryType]=''Consume'' then  -CONVERT([bigint],[Points]) else (0) end)) PERSISTED;');
IF COL_LENGTH(N'dbo.[RewardPointEntry]', N'OccurredAt') IS NULL
EXEC(N'ALTER TABLE dbo.[RewardPointEntry] ADD [OccurredAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[RewardPointEntry]', N'OrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[RewardPointEntry] ADD [OrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RewardPointEntry]', N'Points') IS NULL
EXEC(N'ALTER TABLE dbo.[RewardPointEntry] ADD [Points] int NULL;');
IF COL_LENGTH(N'dbo.[RewardPointEntry]', N'RequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[RewardPointEntry] ADD [RequestKey] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[RewardPointEntry]', N'UserId') IS NULL
EXEC(N'ALTER TABLE dbo.[RewardPointEntry] ADD [UserId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[ContractTerminationRequest]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ContractTerminationRequest] (
    [ContractTerminationRequestId] bigint NOT NULL IDENTITY,
    [B2BContractId] bigint NOT NULL,
    [RequestedByUserId] bigint NOT NULL,
    [Reason] nvarchar(1000) NOT NULL,
    [EvidenceUrl] nvarchar(500) NULL,
    [RequestedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [RequestStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [SettlementAmount] decimal(18,2) NULL,
    [ResolvedAt] datetime2 NULL,
    CONSTRAINT [PK__Contract__B98B7390DB0B51CD] PRIMARY KEY ([ContractTerminationRequestId]),
    CONSTRAINT [FK_TerminationRequest_B2BContract] FOREIGN KEY ([B2BContractId]) REFERENCES [B2BContract] ([B2BContractId]),
    CONSTRAINT [FK_TerminationRequest_RequestedByUser] FOREIGN KEY ([RequestedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'ContractTerminationRequest');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ContractTerminationRequest]', N'B2BContractId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractTerminationRequest] ADD [B2BContractId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractTerminationRequest]', N'EvidenceUrl') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractTerminationRequest] ADD [EvidenceUrl] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[ContractTerminationRequest]', N'Reason') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractTerminationRequest] ADD [Reason] nvarchar(1000) NULL;');
IF COL_LENGTH(N'dbo.[ContractTerminationRequest]', N'RequestStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractTerminationRequest] ADD [RequestStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ContractTerminationRequest]', N'RequestedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractTerminationRequest] ADD [RequestedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[ContractTerminationRequest]', N'RequestedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractTerminationRequest] ADD [RequestedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractTerminationRequest]', N'ResolvedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractTerminationRequest] ADD [ResolvedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[ContractTerminationRequest]', N'SettlementAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractTerminationRequest] ADD [SettlementAmount] decimal(18,2) NULL;');
END;
IF OBJECT_ID(N'dbo.[ContractVersion]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ContractVersion] (
    [ContractVersionId] bigint NOT NULL IDENTITY,
    [B2BContractId] bigint NOT NULL,
    [ProposedByUserId] bigint NOT NULL,
    [VersionNo] int NOT NULL,
    [TermsAndConditions] nvarchar(max) NOT NULL,
    [LineSnapshotJson] nvarchar(max) NULL,
    [DocumentHash] varchar(256) NULL,
    [VersionStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [FeedbackReason] nvarchar(500) NULL,
    CONSTRAINT [PK__Contract__54F2943327C0B491] PRIMARY KEY ([ContractVersionId]),
    CONSTRAINT [FK_ContractVersion_B2BContract] FOREIGN KEY ([B2BContractId]) REFERENCES [B2BContract] ([B2BContractId]),
    CONSTRAINT [FK_ContractVersion_ProposedByUser] FOREIGN KEY ([ProposedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'ContractVersion');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ContractVersion]', N'B2BContractId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [B2BContractId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractVersion]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[ContractVersion]', N'DocumentHash') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [DocumentHash] varchar(256) NULL;');
IF COL_LENGTH(N'dbo.[ContractVersion]', N'FeedbackReason') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [FeedbackReason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[ContractVersion]', N'LineSnapshotJson') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [LineSnapshotJson] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[ContractVersion]', N'ProposedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [ProposedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractVersion]', N'TermsAndConditions') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [TermsAndConditions] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[ContractVersion]', N'VersionNo') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [VersionNo] int NULL;');
IF COL_LENGTH(N'dbo.[ContractVersion]', N'VersionStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractVersion] ADD [VersionStatus] varchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[SellerOrder]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [SellerOrder] (
    [SellerOrderId] bigint NOT NULL IDENTITY,
    [OrderId] bigint NOT NULL,
    [FarmerProfileId] bigint NOT NULL,
    [SellerNameSnapshot] nvarchar(200) NULL,
    [B2BContractId] bigint NULL,
    [SellerOrderCode] varchar(100) NOT NULL,
    [Subtotal] decimal(18,2) NOT NULL,
    [DiscountShare] decimal(18,2) NOT NULL,
    [DeliveryFee] decimal(18,2) NOT NULL,
    [FinalAmount] decimal(18,2) NOT NULL,
    [Status] varchar(50) NOT NULL DEFAULT ''Pending'',
    [DeliveredAt] datetime2 NULL,
    [ReceivedAt] datetime2 NULL,
    CONSTRAINT [PK__SellerOr__EE1F8C1422E642FE] PRIMARY KEY ([SellerOrderId]),
    CONSTRAINT [FK_SellerOrder_B2BContract] FOREIGN KEY ([B2BContractId]) REFERENCES [B2BContract] ([B2BContractId]),
    CONSTRAINT [FK_SellerOrder_FarmerProfile] FOREIGN KEY ([FarmerProfileId]) REFERENCES [FarmerProfile] ([FarmerProfileId]),
    CONSTRAINT [FK_SellerOrder_Order] FOREIGN KEY ([OrderId]) REFERENCES [Order] ([OrderId])
);');
INSERT @NewTables VALUES (N'SellerOrder');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[SellerOrder]', N'B2BContractId') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [B2BContractId] bigint NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'DeliveredAt') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [DeliveredAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'DeliveryFee') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [DeliveryFee] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'DiscountShare') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [DiscountShare] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'FarmerProfileId') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [FarmerProfileId] bigint NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'FinalAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [FinalAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'OrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [OrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'ReceivedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [ReceivedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'SellerNameSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [SellerNameSnapshot] nvarchar(200) NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'SellerOrderCode') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [SellerOrderCode] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'Status') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [Status] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[SellerOrder]', N'Subtotal') IS NULL
EXEC(N'ALTER TABLE dbo.[SellerOrder] ADD [Subtotal] decimal(18,2) NULL;');
END;
IF OBJECT_ID(N'dbo.[CultivationLog]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [CultivationLog] (
    [CultivationLogId] bigint NOT NULL IDENTITY,
    [CropSeasonId] bigint NOT NULL,
    [RecordedByUserId] bigint NOT NULL,
    [ActionDate] datetime2 NOT NULL,
    [ActionType] nvarchar(100) NOT NULL,
    [DetailDescription] nvarchar(1000) NULL,
    [InputMaterial] nvarchar(255) NULL,
    [Dosage] nvarchar(150) NULL,
    [PerformedBy] nvarchar(150) NULL,
    [ImageUrl] nvarchar(500) NULL,
    [RecordedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [CorrectsLogId] bigint NULL,
    [CorrectionReason] nvarchar(500) NULL,
    CONSTRAINT [PK__Cultivat__60C48ADFB5E91593] PRIMARY KEY ([CultivationLogId]),
    CONSTRAINT [FK_CultivationLog_Correction] FOREIGN KEY ([CorrectsLogId]) REFERENCES [CultivationLog] ([CultivationLogId]),
    CONSTRAINT [FK_CultivationLog_CropSeason] FOREIGN KEY ([CropSeasonId]) REFERENCES [CropSeason] ([CropSeasonId]),
    CONSTRAINT [FK_CultivationLog_RecordedByUser] FOREIGN KEY ([RecordedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'CultivationLog');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[CultivationLog]', N'ActionDate') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [ActionDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'ActionType') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [ActionType] nvarchar(100) NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'CorrectionReason') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [CorrectionReason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'CorrectsLogId') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [CorrectsLogId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'CropSeasonId') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [CropSeasonId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'DetailDescription') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [DetailDescription] nvarchar(1000) NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'Dosage') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [Dosage] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'ImageUrl') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [ImageUrl] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'InputMaterial') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [InputMaterial] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'PerformedBy') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [PerformedBy] nvarchar(150) NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'RecordedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [RecordedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[CultivationLog]', N'RecordedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[CultivationLog] ADD [RecordedByUserId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[PreOrderCampaign]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [PreOrderCampaign] (
    [PreOrderCampaignId] bigint NOT NULL IDENTITY,
    [ProductId] bigint NOT NULL,
    [CropSeasonId] bigint NOT NULL,
    [CampaignName] nvarchar(255) NOT NULL,
    [StartDate] datetime2 NOT NULL,
    [EndDate] datetime2 NOT NULL,
    [TargetYield] decimal(18,3) NOT NULL,
    [CommittedYield] decimal(18,3) NOT NULL,
    [DepositPercentage] decimal(5,2) NOT NULL,
    [PreOrderPrice] decimal(18,2) NOT NULL,
    [Status] varchar(50) NOT NULL DEFAULT ''Draft'',
    CONSTRAINT [PK__PreOrder__718DDA50E6D25429] PRIMARY KEY ([PreOrderCampaignId]),
    CONSTRAINT [FK_PreOrderCampaign_CropSeason] FOREIGN KEY ([CropSeasonId]) REFERENCES [CropSeason] ([CropSeasonId]),
    CONSTRAINT [FK_PreOrderCampaign_Product] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([ProductId])
);');
INSERT @NewTables VALUES (N'PreOrderCampaign');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'CampaignName') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [CampaignName] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'CommittedYield') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [CommittedYield] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'CropSeasonId') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [CropSeasonId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'DepositPercentage') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [DepositPercentage] decimal(5,2) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'EndDate') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [EndDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'PreOrderPrice') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [PreOrderPrice] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'ProductId') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [ProductId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'StartDate') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [StartDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'Status') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [Status] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderCampaign]', N'TargetYield') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderCampaign] ADD [TargetYield] decimal(18,3) NULL;');
END;
IF OBJECT_ID(N'dbo.[ContractLine]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ContractLine] (
    [ContractLineId] bigint NOT NULL IDENTITY,
    [B2BContractId] bigint NOT NULL,
    [ProductId] bigint NOT NULL,
    [CropSeasonId] bigint NULL,
    [CommittedQuantity] decimal(18,3) NOT NULL,
    [UnitSnapshot] nvarchar(50) NOT NULL,
    [QualitySpecification] nvarchar(500) NULL,
    [PriceType] varchar(50) NOT NULL,
    [AgreedPrice] decimal(18,2) NULL,
    [MinPrice] decimal(18,2) NULL,
    [MaxPrice] decimal(18,2) NULL,
    [PriceFormula] nvarchar(255) NULL,
    [ContractVersionId] bigint NOT NULL,
    CONSTRAINT [PK__Contract__905F6E87DE77B4DB] PRIMARY KEY ([ContractLineId]),
    CONSTRAINT [FK_ContractLine_B2BContract] FOREIGN KEY ([B2BContractId]) REFERENCES [B2BContract] ([B2BContractId]),
    CONSTRAINT [FK_ContractLine_CropSeason] FOREIGN KEY ([CropSeasonId]) REFERENCES [CropSeason] ([CropSeasonId]),
    CONSTRAINT [FK_ContractLine_Product] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([ProductId]),
    CONSTRAINT [FK_ContractLine_Version] FOREIGN KEY ([ContractVersionId]) REFERENCES [ContractVersion] ([ContractVersionId])
);');
INSERT @NewTables VALUES (N'ContractLine');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ContractLine]', N'AgreedPrice') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [AgreedPrice] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'B2BContractId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [B2BContractId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'CommittedQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [CommittedQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'ContractVersionId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [ContractVersionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'CropSeasonId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [CropSeasonId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'MaxPrice') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [MaxPrice] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'MinPrice') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [MinPrice] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'PriceFormula') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [PriceFormula] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'PriceType') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [PriceType] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'ProductId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [ProductId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'QualitySpecification') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [QualitySpecification] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[ContractLine]', N'UnitSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractLine] ADD [UnitSnapshot] nvarchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[ContractSignature]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ContractSignature] (
    [ContractSignatureId] bigint NOT NULL IDENTITY,
    [ContractVersionId] bigint NOT NULL,
    [SignedByUserId] bigint NOT NULL,
    [PartyRole] varchar(50) NOT NULL,
    [ConfirmationMethod] varchar(50) NOT NULL,
    [ConfirmedAt] datetime2 NULL,
    [ConfirmationStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    CONSTRAINT [PK__Contract__6A8CC220260D5F11] PRIMARY KEY ([ContractSignatureId]),
    CONSTRAINT [FK_ContractSignature_ContractVersion] FOREIGN KEY ([ContractVersionId]) REFERENCES [ContractVersion] ([ContractVersionId]),
    CONSTRAINT [FK_ContractSignature_SignedByUser] FOREIGN KEY ([SignedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'ContractSignature');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ContractSignature]', N'ConfirmationMethod') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractSignature] ADD [ConfirmationMethod] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ContractSignature]', N'ConfirmationStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractSignature] ADD [ConfirmationStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ContractSignature]', N'ConfirmedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractSignature] ADD [ConfirmedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[ContractSignature]', N'ContractVersionId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractSignature] ADD [ContractVersionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractSignature]', N'PartyRole') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractSignature] ADD [PartyRole] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ContractSignature]', N'SignedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractSignature] ADD [SignedByUserId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[DisputeTicket]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [DisputeTicket] (
    [DisputeTicketId] bigint NOT NULL IDENTITY,
    [SubmittedByUserId] bigint NOT NULL,
    [AssignedStaffUserId] bigint NULL,
    [SellerOrderId] bigint NULL,
    [B2BContractId] bigint NULL,
    [DisputeReason] nvarchar(255) NOT NULL,
    [Description] nvarchar(max) NULL,
    [TicketStatus] varchar(50) NOT NULL DEFAULT ''Open'',
    [FaultParty] varchar(50) NULL,
    [RefundAmount] decimal(18,2) NULL,
    [ResolutionNotes] nvarchar(1000) NULL,
    [ResolvedAt] datetime2 NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [DecisionAt] datetime2 NULL,
    CONSTRAINT [PK__DisputeT__3C3F9CFEDBA79541] PRIMARY KEY ([DisputeTicketId]),
    CONSTRAINT [FK_DisputeTicket_AssignedStaffUser] FOREIGN KEY ([AssignedStaffUserId]) REFERENCES [User] ([UserId]),
    CONSTRAINT [FK_DisputeTicket_B2BContract] FOREIGN KEY ([B2BContractId]) REFERENCES [B2BContract] ([B2BContractId]),
    CONSTRAINT [FK_DisputeTicket_SellerOrder] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrder] ([SellerOrderId]),
    CONSTRAINT [FK_DisputeTicket_SubmittedByUser] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'DisputeTicket');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'AssignedStaffUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [AssignedStaffUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'B2BContractId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [B2BContractId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'DecisionAt') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [DecisionAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'Description') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [Description] nvarchar(max) NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'DisputeReason') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [DisputeReason] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'FaultParty') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [FaultParty] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'RefundAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [RefundAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'ResolutionNotes') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [ResolutionNotes] nvarchar(1000) NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'ResolvedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [ResolvedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'SellerOrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [SellerOrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'SubmittedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [SubmittedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeTicket]', N'TicketStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeTicket] ADD [TicketStatus] varchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[OrderStatusHistory]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [OrderStatusHistory] (
    [OrderStatusHistoryId] bigint NOT NULL IDENTITY,
    [SellerOrderId] bigint NOT NULL,
    [ChangedByUserId] bigint NULL,
    [OldStatus] varchar(50) NULL,
    [NewStatus] varchar(50) NOT NULL,
    [Reason] nvarchar(500) NULL,
    [OccurredAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__OrderSta__D16EDBC3AF9F5DB0] PRIMARY KEY ([OrderStatusHistoryId]),
    CONSTRAINT [FK_OrderStatusHistory_ChangedByUser] FOREIGN KEY ([ChangedByUserId]) REFERENCES [User] ([UserId]),
    CONSTRAINT [FK_OrderStatusHistory_SellerOrder] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrder] ([SellerOrderId])
);');
INSERT @NewTables VALUES (N'OrderStatusHistory');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[OrderStatusHistory]', N'ChangedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderStatusHistory] ADD [ChangedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[OrderStatusHistory]', N'NewStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderStatusHistory] ADD [NewStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[OrderStatusHistory]', N'OccurredAt') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderStatusHistory] ADD [OccurredAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[OrderStatusHistory]', N'OldStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderStatusHistory] ADD [OldStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[OrderStatusHistory]', N'Reason') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderStatusHistory] ADD [Reason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[OrderStatusHistory]', N'SellerOrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderStatusHistory] ADD [SellerOrderId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[VoucherRedemption]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [VoucherRedemption] (
    [VoucherRedemptionId] bigint NOT NULL IDENTITY,
    [VoucherId] bigint NOT NULL,
    [OrderId] bigint NOT NULL,
    [SellerOrderId] bigint NULL,
    [DiscountSnapshot] decimal(18,2) NOT NULL,
    [Status] varchar(50) NOT NULL DEFAULT ''Reserved'',
    [ReservedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [AppliedAt] datetime2 NULL,
    [ReleasedAt] datetime2 NULL,
    [ExpiresAt] datetime2 NULL,
    CONSTRAINT [PK__VoucherR__140B18225B5923B2] PRIMARY KEY ([VoucherRedemptionId]),
    CONSTRAINT [FK_VoucherRedemption_Order] FOREIGN KEY ([OrderId]) REFERENCES [Order] ([OrderId]),
    CONSTRAINT [FK_VoucherRedemption_SellerOrder] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrder] ([SellerOrderId]),
    CONSTRAINT [FK_VoucherRedemption_Voucher] FOREIGN KEY ([VoucherId]) REFERENCES [Voucher] ([VoucherId])
);');
INSERT @NewTables VALUES (N'VoucherRedemption');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'AppliedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [AppliedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'DiscountSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [DiscountSnapshot] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'ExpiresAt') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [ExpiresAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'OrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [OrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'ReleasedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [ReleasedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'ReservedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [ReservedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'SellerOrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [SellerOrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'Status') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [Status] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[VoucherRedemption]', N'VoucherId') IS NULL
EXEC(N'ALTER TABLE dbo.[VoucherRedemption] ADD [VoucherId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[HarvestBatch]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [HarvestBatch] (
    [HarvestBatchId] bigint NOT NULL IDENTITY,
    [ProductId] bigint NOT NULL,
    [CropSeasonId] bigint NOT NULL,
    [PreOrderCampaignId] bigint NULL,
    [BatchCode] varchar(100) NOT NULL,
    [EstimatedHarvestDate] date NULL,
    [ActualHarvestDate] date NULL,
    [HarvestYield] decimal(18,3) NOT NULL,
    [ExpirationDate] date NULL,
    [QualityNotes] nvarchar(500) NULL,
    [Status] varchar(50) NOT NULL DEFAULT ''Planned'',
    [StockEnteredAt] datetime2 NULL,
    [ReservedQuantity] decimal(18,3) NOT NULL,
    [AvailableQuantity] decimal(18,3) NOT NULL,
    CONSTRAINT [PK__HarvestB__619519FBF28D519C] PRIMARY KEY ([HarvestBatchId]),
    CONSTRAINT [AK_HarvestBatch_ProductId_HarvestBatchId] UNIQUE ([ProductId], [HarvestBatchId]),
    CONSTRAINT [FK_HarvestBatch_CropSeason] FOREIGN KEY ([CropSeasonId]) REFERENCES [CropSeason] ([CropSeasonId]),
    CONSTRAINT [FK_HarvestBatch_PreOrderCampaign] FOREIGN KEY ([PreOrderCampaignId]) REFERENCES [PreOrderCampaign] ([PreOrderCampaignId]),
    CONSTRAINT [FK_HarvestBatch_Product] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([ProductId])
);');
INSERT @NewTables VALUES (N'HarvestBatch');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'ActualHarvestDate') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [ActualHarvestDate] date NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'AvailableQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [AvailableQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'BatchCode') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [BatchCode] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'CropSeasonId') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [CropSeasonId] bigint NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'EstimatedHarvestDate') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [EstimatedHarvestDate] date NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'ExpirationDate') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [ExpirationDate] date NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'HarvestYield') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [HarvestYield] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'PreOrderCampaignId') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [PreOrderCampaignId] bigint NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'ProductId') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [ProductId] bigint NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'QualityNotes') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [QualityNotes] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'ReservedQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [ReservedQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'Status') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [Status] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[HarvestBatch]', N'StockEnteredAt') IS NULL
EXEC(N'ALTER TABLE dbo.[HarvestBatch] ADD [StockEnteredAt] datetime2 NULL;');
END;
IF OBJECT_ID(N'dbo.[ContractMilestone]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ContractMilestone] (
    [ContractMilestoneId] bigint NOT NULL IDENTITY,
    [B2BContractId] bigint NOT NULL,
    [ContractLineId] bigint NULL,
    [MilestoneTitle] nvarchar(200) NOT NULL,
    [MilestoneType] varchar(50) NOT NULL,
    [DueDate] date NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [DeliveryQuantity] decimal(18,3) NULL,
    [FulfillmentStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [PaymentStatus] varchar(50) NOT NULL DEFAULT ''Unpaid'',
    [DeliveredQuantity] decimal(18,3) NOT NULL,
    [AcceptedQuantity] decimal(18,3) NOT NULL,
    [PaidAmount] decimal(18,2) NOT NULL,
    [ContractVersionId] bigint NOT NULL,
    CONSTRAINT [PK__Contract__9CD091DC5D46733A] PRIMARY KEY ([ContractMilestoneId]),
    CONSTRAINT [FK_ContractMilestone_B2BContract] FOREIGN KEY ([B2BContractId]) REFERENCES [B2BContract] ([B2BContractId]),
    CONSTRAINT [FK_ContractMilestone_ContractLine] FOREIGN KEY ([ContractLineId]) REFERENCES [ContractLine] ([ContractLineId]),
    CONSTRAINT [FK_Milestone_Version] FOREIGN KEY ([ContractVersionId]) REFERENCES [ContractVersion] ([ContractVersionId])
);');
INSERT @NewTables VALUES (N'ContractMilestone');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'AcceptedQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [AcceptedQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'Amount') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [Amount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'B2BContractId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [B2BContractId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'ContractLineId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [ContractLineId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'ContractVersionId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [ContractVersionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'DeliveredQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [DeliveredQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'DeliveryQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [DeliveryQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'DueDate') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [DueDate] date NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'FulfillmentStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [FulfillmentStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'MilestoneTitle') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [MilestoneTitle] nvarchar(200) NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'MilestoneType') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [MilestoneType] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'PaidAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [PaidAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[ContractMilestone]', N'PaymentStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractMilestone] ADD [PaymentStatus] varchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[OrderItem]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [OrderItem] (
    [OrderItemId] bigint NOT NULL IDENTITY,
    [SellerOrderId] bigint NOT NULL,
    [ProductId] bigint NOT NULL,
    [PreOrderCampaignId] bigint NULL,
    [ContractLineId] bigint NULL,
    [ProductNameSnapshot] nvarchar(255) NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [UnitPriceSnapshot] decimal(18,2) NOT NULL,
    [UnitSnapshot] nvarchar(50) NOT NULL,
    [DiscountApplied] decimal(18,2) NOT NULL,
    [SubTotal] decimal(18,2) NOT NULL,
    [ItemStatus] varchar(50) NOT NULL DEFAULT ''Normal'',
    CONSTRAINT [PK__OrderIte__57ED0681BE5BD3A4] PRIMARY KEY ([OrderItemId]),
    CONSTRAINT [FK_OrderItem_ContractLine] FOREIGN KEY ([ContractLineId]) REFERENCES [ContractLine] ([ContractLineId]),
    CONSTRAINT [FK_OrderItem_PreOrderCampaign] FOREIGN KEY ([PreOrderCampaignId]) REFERENCES [PreOrderCampaign] ([PreOrderCampaignId]),
    CONSTRAINT [FK_OrderItem_Product] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([ProductId]),
    CONSTRAINT [FK_OrderItem_SellerOrder] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrder] ([SellerOrderId])
);');
INSERT @NewTables VALUES (N'OrderItem');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[OrderItem]', N'ContractLineId') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [ContractLineId] bigint NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'DiscountApplied') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [DiscountApplied] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'ItemStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [ItemStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'PreOrderCampaignId') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [PreOrderCampaignId] bigint NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'ProductId') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [ProductId] bigint NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'ProductNameSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [ProductNameSnapshot] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'Quantity') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [Quantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'SellerOrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [SellerOrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'SubTotal') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [SubTotal] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'UnitPriceSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [UnitPriceSnapshot] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[OrderItem]', N'UnitSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItem] ADD [UnitSnapshot] nvarchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[DisputeEvidenceRequest]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [DisputeEvidenceRequest] (
    [EvidenceRequestId] bigint NOT NULL IDENTITY,
    [DisputeTicketId] bigint NOT NULL,
    [RequestedPartyUserId] bigint NOT NULL,
    [RequestedByUserId] bigint NOT NULL,
    [RequestedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [DueAt] AS (dateadd(hour,(48),[RequestedAt])) PERSISTED,
    [RequestStatus] varchar(20) NOT NULL DEFAULT ''Pending'',
    [RespondedAt] datetime2 NULL,
    [Instructions] nvarchar(1000) NULL,
    CONSTRAINT [PK__DisputeE__86E44B409DCF502C] PRIMARY KEY ([EvidenceRequestId]),
    CONSTRAINT [FK__DisputeEv__Dispu__76EBA2E9] FOREIGN KEY ([DisputeTicketId]) REFERENCES [DisputeTicket] ([DisputeTicketId]),
    CONSTRAINT [FK__DisputeEv__Reque__77DFC722] FOREIGN KEY ([RequestedPartyUserId]) REFERENCES [User] ([UserId]),
    CONSTRAINT [FK__DisputeEv__Reque__78D3EB5B] FOREIGN KEY ([RequestedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'DisputeEvidenceRequest');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[DisputeEvidenceRequest]', N'DisputeTicketId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidenceRequest] ADD [DisputeTicketId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidenceRequest]', N'DueAt') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidenceRequest] ADD [DueAt] AS (dateadd(hour,(48),[RequestedAt])) PERSISTED;');
IF COL_LENGTH(N'dbo.[DisputeEvidenceRequest]', N'Instructions') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidenceRequest] ADD [Instructions] nvarchar(1000) NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidenceRequest]', N'RequestStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidenceRequest] ADD [RequestStatus] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidenceRequest]', N'RequestedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidenceRequest] ADD [RequestedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidenceRequest]', N'RequestedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidenceRequest] ADD [RequestedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidenceRequest]', N'RequestedPartyUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidenceRequest] ADD [RequestedPartyUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidenceRequest]', N'RespondedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidenceRequest] ADD [RespondedAt] datetime2 NULL;');
END;
IF OBJECT_ID(N'dbo.[CartItem]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [CartItem] (
    [CartItemId] bigint NOT NULL IDENTITY,
    [CartId] bigint NOT NULL,
    [ProductId] bigint NOT NULL,
    [HarvestBatchId] bigint NULL,
    [PreOrderCampaignId] bigint NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [IsSelected] bit NOT NULL DEFAULT CAST(1 AS bit),
    [AddedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__CartItem__488B0B0A9A476B47] PRIMARY KEY ([CartItemId]),
    CONSTRAINT [FK_CartItem_Cart] FOREIGN KEY ([CartId]) REFERENCES [Cart] ([CartId]),
    CONSTRAINT [FK_CartItem_PreOrderCampaign] FOREIGN KEY ([PreOrderCampaignId]) REFERENCES [PreOrderCampaign] ([PreOrderCampaignId]),
    CONSTRAINT [FK_CartItem_Product] FOREIGN KEY ([ProductId]) REFERENCES [Product] ([ProductId]),
    CONSTRAINT [FK_CartItem_Product_HarvestBatch] FOREIGN KEY ([ProductId], [HarvestBatchId]) REFERENCES [HarvestBatch] ([ProductId], [HarvestBatchId])
);');
INSERT @NewTables VALUES (N'CartItem');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[CartItem]', N'AddedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[CartItem] ADD [AddedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[CartItem]', N'CartId') IS NULL
EXEC(N'ALTER TABLE dbo.[CartItem] ADD [CartId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CartItem]', N'HarvestBatchId') IS NULL
EXEC(N'ALTER TABLE dbo.[CartItem] ADD [HarvestBatchId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CartItem]', N'IsSelected') IS NULL
EXEC(N'ALTER TABLE dbo.[CartItem] ADD [IsSelected] bit NULL;');
IF COL_LENGTH(N'dbo.[CartItem]', N'PreOrderCampaignId') IS NULL
EXEC(N'ALTER TABLE dbo.[CartItem] ADD [PreOrderCampaignId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CartItem]', N'ProductId') IS NULL
EXEC(N'ALTER TABLE dbo.[CartItem] ADD [ProductId] bigint NULL;');
IF COL_LENGTH(N'dbo.[CartItem]', N'Quantity') IS NULL
EXEC(N'ALTER TABLE dbo.[CartItem] ADD [Quantity] decimal(18,3) NULL;');
END;
IF OBJECT_ID(N'dbo.[TraceabilityQRCode]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [TraceabilityQRCode] (
    [TraceabilityQRCodeId] bigint NOT NULL IDENTITY,
    [HarvestBatchId] bigint NOT NULL,
    [QRContentString] nvarchar(500) NOT NULL,
    [TargetUrl] nvarchar(500) NOT NULL,
    [ScanCount] int NOT NULL,
    [IsRevoked] bit NOT NULL,
    [GeneratedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__Traceabi__EC0CD854820BDC60] PRIMARY KEY ([TraceabilityQRCodeId]),
    CONSTRAINT [FK_TraceabilityQRCode_HarvestBatch] FOREIGN KEY ([HarvestBatchId]) REFERENCES [HarvestBatch] ([HarvestBatchId])
);');
INSERT @NewTables VALUES (N'TraceabilityQRCode');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[TraceabilityQRCode]', N'GeneratedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[TraceabilityQRCode] ADD [GeneratedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[TraceabilityQRCode]', N'HarvestBatchId') IS NULL
EXEC(N'ALTER TABLE dbo.[TraceabilityQRCode] ADD [HarvestBatchId] bigint NULL;');
IF COL_LENGTH(N'dbo.[TraceabilityQRCode]', N'IsRevoked') IS NULL
EXEC(N'ALTER TABLE dbo.[TraceabilityQRCode] ADD [IsRevoked] bit NULL;');
IF COL_LENGTH(N'dbo.[TraceabilityQRCode]', N'QRContentString') IS NULL
EXEC(N'ALTER TABLE dbo.[TraceabilityQRCode] ADD [QRContentString] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[TraceabilityQRCode]', N'ScanCount') IS NULL
EXEC(N'ALTER TABLE dbo.[TraceabilityQRCode] ADD [ScanCount] int NULL;');
IF COL_LENGTH(N'dbo.[TraceabilityQRCode]', N'TargetUrl') IS NULL
EXEC(N'ALTER TABLE dbo.[TraceabilityQRCode] ADD [TargetUrl] nvarchar(500) NULL;');
END;
IF OBJECT_ID(N'dbo.[Invoice]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [Invoice] (
    [InvoiceId] bigint NOT NULL IDENTITY,
    [SellerOrderId] bigint NULL,
    [ContractMilestoneId] bigint NULL,
    [InvoiceNumber] varchar(100) NOT NULL,
    [IssuedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [DueAt] datetime2 NULL,
    [TotalAmount] decimal(18,2) NOT NULL,
    [InvoiceStatus] varchar(50) NOT NULL DEFAULT ''Draft'',
    [B2BSubscriptionId] bigint NULL,
    CONSTRAINT [PK__Invoice__D796AAB526E64347] PRIMARY KEY ([InvoiceId]),
    CONSTRAINT [FK_Invoice_ContractMilestone] FOREIGN KEY ([ContractMilestoneId]) REFERENCES [ContractMilestone] ([ContractMilestoneId]),
    CONSTRAINT [FK_Invoice_SellerOrder] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrder] ([SellerOrderId]),
    CONSTRAINT [FK_Invoice_Subscription] FOREIGN KEY ([B2BSubscriptionId]) REFERENCES [B2BSubscription] ([B2BSubscriptionId])
);');
INSERT @NewTables VALUES (N'Invoice');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[Invoice]', N'B2BSubscriptionId') IS NULL
EXEC(N'ALTER TABLE dbo.[Invoice] ADD [B2BSubscriptionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Invoice]', N'ContractMilestoneId') IS NULL
EXEC(N'ALTER TABLE dbo.[Invoice] ADD [ContractMilestoneId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Invoice]', N'DueAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Invoice] ADD [DueAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Invoice]', N'InvoiceNumber') IS NULL
EXEC(N'ALTER TABLE dbo.[Invoice] ADD [InvoiceNumber] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[Invoice]', N'InvoiceStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[Invoice] ADD [InvoiceStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Invoice]', N'IssuedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Invoice] ADD [IssuedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Invoice]', N'SellerOrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[Invoice] ADD [SellerOrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Invoice]', N'TotalAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[Invoice] ADD [TotalAmount] decimal(18,2) NULL;');
END;
IF OBJECT_ID(N'dbo.[PaymentTransaction]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [PaymentTransaction] (
    [PaymentTransactionId] bigint NOT NULL IDENTITY,
    [OrderId] bigint NULL,
    [B2BSubscriptionId] bigint NULL,
    [ContractMilestoneId] bigint NULL,
    [B2BContractId] bigint NULL,
    [TransactionCode] varchar(100) NOT NULL,
    [GatewayProvider] varchar(100) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [PaymentType] varchar(50) NOT NULL,
    [PaymentStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [WebhookPayload] nvarchar(max) NULL,
    [PaidAt] datetime2 NULL,
    [PaymentPurpose] varchar(100) NOT NULL,
    [RequestedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [ExpiresAt] datetime2 NULL,
    [ReconciledAt] datetime2 NULL,
    [RequestKey] varchar(100) NOT NULL,
    [ProviderTransactionReference] varchar(150) NULL,
    CONSTRAINT [PK__PaymentT__C22AEFE0CB95DD00] PRIMARY KEY ([PaymentTransactionId]),
    CONSTRAINT [FK_PaymentTransaction_B2BContract] FOREIGN KEY ([B2BContractId]) REFERENCES [B2BContract] ([B2BContractId]),
    CONSTRAINT [FK_PaymentTransaction_B2BSubscription] FOREIGN KEY ([B2BSubscriptionId]) REFERENCES [B2BSubscription] ([B2BSubscriptionId]),
    CONSTRAINT [FK_PaymentTransaction_ContractMilestone] FOREIGN KEY ([ContractMilestoneId]) REFERENCES [ContractMilestone] ([ContractMilestoneId]),
    CONSTRAINT [FK_PaymentTransaction_Order] FOREIGN KEY ([OrderId]) REFERENCES [Order] ([OrderId])
);');
INSERT @NewTables VALUES (N'PaymentTransaction');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'Amount') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [Amount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'B2BContractId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [B2BContractId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'B2BSubscriptionId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [B2BSubscriptionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'ContractMilestoneId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [ContractMilestoneId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'ExpiresAt') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [ExpiresAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'GatewayProvider') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [GatewayProvider] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'OrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [OrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'PaidAt') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [PaidAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'PaymentPurpose') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [PaymentPurpose] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'PaymentStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [PaymentStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'PaymentType') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [PaymentType] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'ProviderTransactionReference') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [ProviderTransactionReference] varchar(150) NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'ReconciledAt') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [ReconciledAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'RequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [RequestKey] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'RequestedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [RequestedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'TransactionCode') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [TransactionCode] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[PaymentTransaction]', N'WebhookPayload') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentTransaction] ADD [WebhookPayload] nvarchar(max) NULL;');
END;
IF OBJECT_ID(N'dbo.[Shipment]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [Shipment] (
    [ShipmentId] bigint NOT NULL IDENTITY,
    [SellerOrderId] bigint NOT NULL,
    [ContractMilestoneId] bigint NULL,
    [TrackingNumber] varchar(100) NULL,
    [PlannedDeliveryAt] datetime2 NULL,
    [ShippedAt] datetime2 NULL,
    [DeliveredAt] datetime2 NULL,
    [ReceivedAt] datetime2 NULL,
    [ShipmentStatus] varchar(50) NOT NULL DEFAULT ''Preparing'',
    CONSTRAINT [PK__Shipment__5CAD37ED58E69320] PRIMARY KEY ([ShipmentId]),
    CONSTRAINT [FK_Shipment_ContractMilestone] FOREIGN KEY ([ContractMilestoneId]) REFERENCES [ContractMilestone] ([ContractMilestoneId]),
    CONSTRAINT [FK_Shipment_SellerOrder] FOREIGN KEY ([SellerOrderId]) REFERENCES [SellerOrder] ([SellerOrderId])
);');
INSERT @NewTables VALUES (N'Shipment');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[Shipment]', N'ContractMilestoneId') IS NULL
EXEC(N'ALTER TABLE dbo.[Shipment] ADD [ContractMilestoneId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Shipment]', N'DeliveredAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Shipment] ADD [DeliveredAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Shipment]', N'PlannedDeliveryAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Shipment] ADD [PlannedDeliveryAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Shipment]', N'ReceivedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Shipment] ADD [ReceivedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Shipment]', N'SellerOrderId') IS NULL
EXEC(N'ALTER TABLE dbo.[Shipment] ADD [SellerOrderId] bigint NULL;');
IF COL_LENGTH(N'dbo.[Shipment]', N'ShipmentStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[Shipment] ADD [ShipmentStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[Shipment]', N'ShippedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[Shipment] ADD [ShippedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[Shipment]', N'TrackingNumber') IS NULL
EXEC(N'ALTER TABLE dbo.[Shipment] ADD [TrackingNumber] varchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[OrderItemBatchAllocation]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [OrderItemBatchAllocation] (
    [OrderItemBatchAllocationId] bigint NOT NULL IDENTITY,
    [OrderItemId] bigint NOT NULL,
    [HarvestBatchId] bigint NOT NULL,
    [AllocationSequence] int NOT NULL DEFAULT 1,
    [AllocatedQuantity] decimal(18,3) NOT NULL,
    [AllocationStatus] varchar(50) NOT NULL DEFAULT ''Reserved'',
    [ReservedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [ExpiresAt] datetime2 NULL,
    [ReleasedAt] datetime2 NULL,
    CONSTRAINT [PK__OrderIte__51B745BBDEE9E233] PRIMARY KEY ([OrderItemBatchAllocationId]),
    CONSTRAINT [FK_BatchAllocation_HarvestBatch] FOREIGN KEY ([HarvestBatchId]) REFERENCES [HarvestBatch] ([HarvestBatchId]),
    CONSTRAINT [FK_BatchAllocation_OrderItem] FOREIGN KEY ([OrderItemId]) REFERENCES [OrderItem] ([OrderItemId])
);');
INSERT @NewTables VALUES (N'OrderItemBatchAllocation');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[OrderItemBatchAllocation]', N'AllocatedQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItemBatchAllocation] ADD [AllocatedQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[OrderItemBatchAllocation]', N'AllocationSequence') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItemBatchAllocation] ADD [AllocationSequence] int NULL;');
IF COL_LENGTH(N'dbo.[OrderItemBatchAllocation]', N'AllocationStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItemBatchAllocation] ADD [AllocationStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[OrderItemBatchAllocation]', N'ExpiresAt') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItemBatchAllocation] ADD [ExpiresAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[OrderItemBatchAllocation]', N'HarvestBatchId') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItemBatchAllocation] ADD [HarvestBatchId] bigint NULL;');
IF COL_LENGTH(N'dbo.[OrderItemBatchAllocation]', N'OrderItemId') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItemBatchAllocation] ADD [OrderItemId] bigint NULL;');
IF COL_LENGTH(N'dbo.[OrderItemBatchAllocation]', N'ReleasedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItemBatchAllocation] ADD [ReleasedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[OrderItemBatchAllocation]', N'ReservedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[OrderItemBatchAllocation] ADD [ReservedAt] datetime2 NULL;');
END;
IF OBJECT_ID(N'dbo.[PreOrderReservation]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [PreOrderReservation] (
    [PreOrderReservationId] bigint NOT NULL IDENTITY,
    [OrderItemId] bigint NOT NULL,
    [PreOrderCampaignId] bigint NOT NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [ReservationStatus] varchar(20) NOT NULL DEFAULT ''Reserved'',
    [ExpiresAt] datetime2 NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [PriceSnapshot] decimal(18,2) NOT NULL,
    [DepositPercentageSnapshot] decimal(5,2) NOT NULL,
    [UnitSnapshot] nvarchar(50) NOT NULL,
    [EstimatedHarvestDateSnapshot] date NOT NULL,
    [RequestKey] varchar(100) NOT NULL,
    CONSTRAINT [PK__PreOrder__941AE6E1E1B8D689] PRIMARY KEY ([PreOrderReservationId]),
    CONSTRAINT [FK__PreOrderR__Order__4A18FC72] FOREIGN KEY ([OrderItemId]) REFERENCES [OrderItem] ([OrderItemId]),
    CONSTRAINT [FK__PreOrderR__PreOr__4B0D20AB] FOREIGN KEY ([PreOrderCampaignId]) REFERENCES [PreOrderCampaign] ([PreOrderCampaignId])
);');
INSERT @NewTables VALUES (N'PreOrderReservation');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'DepositPercentageSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [DepositPercentageSnapshot] decimal(5,2) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'EstimatedHarvestDateSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [EstimatedHarvestDateSnapshot] date NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'ExpiresAt') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [ExpiresAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'OrderItemId') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [OrderItemId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'PreOrderCampaignId') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [PreOrderCampaignId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'PriceSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [PriceSnapshot] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'Quantity') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [Quantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'RequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [RequestKey] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'ReservationStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [ReservationStatus] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[PreOrderReservation]', N'UnitSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[PreOrderReservation] ADD [UnitSnapshot] nvarchar(50) NULL;');
END;
IF OBJECT_ID(N'dbo.[ProductReview]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ProductReview] (
    [ProductReviewId] bigint NOT NULL IDENTITY,
    [OrderItemId] bigint NOT NULL,
    [UserId] bigint NOT NULL,
    [RatingStars] tinyint NOT NULL,
    [Comment] nvarchar(1000) NULL,
    [ReviewDate] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [IsPurchasedVerified] bit NOT NULL DEFAULT CAST(1 AS bit),
    [ModerationStatus] varchar(50) NOT NULL DEFAULT ''Approved'',
    [ModerationReason] nvarchar(500) NULL,
    CONSTRAINT [PK__ProductR__39631880A597FAE7] PRIMARY KEY ([ProductReviewId]),
    CONSTRAINT [FK_ProductReview_OrderItem] FOREIGN KEY ([OrderItemId]) REFERENCES [OrderItem] ([OrderItemId]),
    CONSTRAINT [FK_ProductReview_User] FOREIGN KEY ([UserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'ProductReview');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ProductReview]', N'Comment') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductReview] ADD [Comment] nvarchar(1000) NULL;');
IF COL_LENGTH(N'dbo.[ProductReview]', N'IsPurchasedVerified') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductReview] ADD [IsPurchasedVerified] bit NULL;');
IF COL_LENGTH(N'dbo.[ProductReview]', N'ModerationReason') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductReview] ADD [ModerationReason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[ProductReview]', N'ModerationStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductReview] ADD [ModerationStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ProductReview]', N'OrderItemId') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductReview] ADD [OrderItemId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ProductReview]', N'RatingStars') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductReview] ADD [RatingStars] tinyint NULL;');
IF COL_LENGTH(N'dbo.[ProductReview]', N'ReviewDate') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductReview] ADD [ReviewDate] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[ProductReview]', N'UserId') IS NULL
EXEC(N'ALTER TABLE dbo.[ProductReview] ADD [UserId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[DisputeEvidence]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [DisputeEvidence] (
    [DisputeEvidenceId] bigint NOT NULL IDENTITY,
    [DisputeTicketId] bigint NOT NULL,
    [SubmittedByUserId] bigint NOT NULL,
    [FileUrl] nvarchar(500) NOT NULL,
    [Description] nvarchar(500) NULL,
    [SubmittedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [EvidenceRequestId] bigint NULL,
    CONSTRAINT [PK__DisputeE__B2461231997E7DB9] PRIMARY KEY ([DisputeEvidenceId]),
    CONSTRAINT [FK_DisputeEvidence_DisputeTicket] FOREIGN KEY ([DisputeTicketId]) REFERENCES [DisputeTicket] ([DisputeTicketId]),
    CONSTRAINT [FK_DisputeEvidence_SubmittedByUser] FOREIGN KEY ([SubmittedByUserId]) REFERENCES [User] ([UserId]),
    CONSTRAINT [FK_Evidence_Request] FOREIGN KEY ([EvidenceRequestId]) REFERENCES [DisputeEvidenceRequest] ([EvidenceRequestId])
);');
INSERT @NewTables VALUES (N'DisputeEvidence');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[DisputeEvidence]', N'Description') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidence] ADD [Description] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidence]', N'DisputeTicketId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidence] ADD [DisputeTicketId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidence]', N'EvidenceRequestId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidence] ADD [EvidenceRequestId] bigint NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidence]', N'FileUrl') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidence] ADD [FileUrl] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidence]', N'SubmittedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidence] ADD [SubmittedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[DisputeEvidence]', N'SubmittedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[DisputeEvidence] ADD [SubmittedByUserId] bigint NULL;');
END;
IF OBJECT_ID(N'dbo.[ContractDepositAllocation]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ContractDepositAllocation] (
    [ContractDepositAllocationId] bigint NOT NULL IDENTITY,
    [PaymentTransactionId] bigint NOT NULL,
    [ContractMilestoneId] bigint NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [RequestKey] varchar(100) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__Contract__07935F1B509A7C51] PRIMARY KEY ([ContractDepositAllocationId]),
    CONSTRAINT [AK_ContractDepositAllocation_ContractDepositAllocationId_PaymentTransactionId] UNIQUE ([ContractDepositAllocationId], [PaymentTransactionId]),
    CONSTRAINT [FK__ContractD__Contr__5772F790] FOREIGN KEY ([ContractMilestoneId]) REFERENCES [ContractMilestone] ([ContractMilestoneId]),
    CONSTRAINT [FK__ContractD__Payme__567ED357] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransaction] ([PaymentTransactionId])
);');
INSERT @NewTables VALUES (N'ContractDepositAllocation');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ContractDepositAllocation]', N'Amount') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractDepositAllocation] ADD [Amount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[ContractDepositAllocation]', N'ContractMilestoneId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractDepositAllocation] ADD [ContractMilestoneId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractDepositAllocation]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractDepositAllocation] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[ContractDepositAllocation]', N'PaymentTransactionId') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractDepositAllocation] ADD [PaymentTransactionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ContractDepositAllocation]', N'RequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[ContractDepositAllocation] ADD [RequestKey] varchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[PaymentGatewayEvent]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [PaymentGatewayEvent] (
    [PaymentGatewayEventId] bigint NOT NULL IDENTITY,
    [GatewayProvider] varchar(100) NOT NULL,
    [ProviderEventId] varchar(150) NOT NULL,
    [PaymentTransactionId] bigint NOT NULL,
    [ProviderTransactionReference] varchar(150) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [EventStatus] varchar(20) NOT NULL,
    [PayloadHash] char(64) NOT NULL,
    [ReceivedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__PaymentG__A143F7694B7E708A] PRIMARY KEY ([PaymentGatewayEventId]),
    CONSTRAINT [FK__PaymentGa__Payme__67A95F59] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransaction] ([PaymentTransactionId])
);');
INSERT @NewTables VALUES (N'PaymentGatewayEvent');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[PaymentGatewayEvent]', N'Amount') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentGatewayEvent] ADD [Amount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[PaymentGatewayEvent]', N'EventStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentGatewayEvent] ADD [EventStatus] varchar(20) NULL;');
IF COL_LENGTH(N'dbo.[PaymentGatewayEvent]', N'GatewayProvider') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentGatewayEvent] ADD [GatewayProvider] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[PaymentGatewayEvent]', N'PayloadHash') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentGatewayEvent] ADD [PayloadHash] char(64) NULL;');
IF COL_LENGTH(N'dbo.[PaymentGatewayEvent]', N'PaymentTransactionId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentGatewayEvent] ADD [PaymentTransactionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PaymentGatewayEvent]', N'ProviderEventId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentGatewayEvent] ADD [ProviderEventId] varchar(150) NULL;');
IF COL_LENGTH(N'dbo.[PaymentGatewayEvent]', N'ProviderTransactionReference') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentGatewayEvent] ADD [ProviderTransactionReference] varchar(150) NULL;');
IF COL_LENGTH(N'dbo.[PaymentGatewayEvent]', N'ReceivedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentGatewayEvent] ADD [ReceivedAt] datetime2 NULL;');
END;
IF OBJECT_ID(N'dbo.[InventoryMovement]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [InventoryMovement] (
    [InventoryMovementId] bigint NOT NULL IDENTITY,
    [HarvestBatchId] bigint NOT NULL,
    [OrderItemBatchAllocationId] bigint NULL,
    [RecordedByUserId] bigint NULL,
    [MovementType] varchar(50) NOT NULL,
    [QuantityChange] decimal(18,3) NOT NULL,
    [Reason] nvarchar(500) NULL,
    [OccurredAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    [RequestKey] varchar(100) NOT NULL,
    CONSTRAINT [PK__Inventor__DB9462B66F1AE9CB] PRIMARY KEY ([InventoryMovementId]),
    CONSTRAINT [FK_InventoryMovement_BatchAllocation] FOREIGN KEY ([OrderItemBatchAllocationId]) REFERENCES [OrderItemBatchAllocation] ([OrderItemBatchAllocationId]),
    CONSTRAINT [FK_InventoryMovement_HarvestBatch] FOREIGN KEY ([HarvestBatchId]) REFERENCES [HarvestBatch] ([HarvestBatchId]),
    CONSTRAINT [FK_InventoryMovement_RecordedByUser] FOREIGN KEY ([RecordedByUserId]) REFERENCES [User] ([UserId])
);');
INSERT @NewTables VALUES (N'InventoryMovement');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[InventoryMovement]', N'HarvestBatchId') IS NULL
EXEC(N'ALTER TABLE dbo.[InventoryMovement] ADD [HarvestBatchId] bigint NULL;');
IF COL_LENGTH(N'dbo.[InventoryMovement]', N'MovementType') IS NULL
EXEC(N'ALTER TABLE dbo.[InventoryMovement] ADD [MovementType] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[InventoryMovement]', N'OccurredAt') IS NULL
EXEC(N'ALTER TABLE dbo.[InventoryMovement] ADD [OccurredAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[InventoryMovement]', N'OrderItemBatchAllocationId') IS NULL
EXEC(N'ALTER TABLE dbo.[InventoryMovement] ADD [OrderItemBatchAllocationId] bigint NULL;');
IF COL_LENGTH(N'dbo.[InventoryMovement]', N'QuantityChange') IS NULL
EXEC(N'ALTER TABLE dbo.[InventoryMovement] ADD [QuantityChange] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[InventoryMovement]', N'Reason') IS NULL
EXEC(N'ALTER TABLE dbo.[InventoryMovement] ADD [Reason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[InventoryMovement]', N'RecordedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[InventoryMovement] ADD [RecordedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[InventoryMovement]', N'RequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[InventoryMovement] ADD [RequestKey] varchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[ShipmentItem]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [ShipmentItem] (
    [ShipmentItemId] bigint NOT NULL IDENTITY,
    [ShipmentId] bigint NOT NULL,
    [OrderItemBatchAllocationId] bigint NOT NULL,
    [ShippedQuantity] decimal(18,3) NOT NULL,
    [ReceivedQuantity] decimal(18,3) NULL,
    [AcceptedQuantity] decimal(18,3) NULL,
    [QualityResult] nvarchar(255) NULL,
    [ReconciliationStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [ReconciledAt] datetime2 NULL,
    CONSTRAINT [PK__Shipment__E46A9F6E289320EF] PRIMARY KEY ([ShipmentItemId]),
    CONSTRAINT [FK_ShipmentItem_BatchAllocation] FOREIGN KEY ([OrderItemBatchAllocationId]) REFERENCES [OrderItemBatchAllocation] ([OrderItemBatchAllocationId]),
    CONSTRAINT [FK_ShipmentItem_Shipment] FOREIGN KEY ([ShipmentId]) REFERENCES [Shipment] ([ShipmentId])
);');
INSERT @NewTables VALUES (N'ShipmentItem');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[ShipmentItem]', N'AcceptedQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[ShipmentItem] ADD [AcceptedQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[ShipmentItem]', N'OrderItemBatchAllocationId') IS NULL
EXEC(N'ALTER TABLE dbo.[ShipmentItem] ADD [OrderItemBatchAllocationId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ShipmentItem]', N'QualityResult') IS NULL
EXEC(N'ALTER TABLE dbo.[ShipmentItem] ADD [QualityResult] nvarchar(255) NULL;');
IF COL_LENGTH(N'dbo.[ShipmentItem]', N'ReceivedQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[ShipmentItem] ADD [ReceivedQuantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[ShipmentItem]', N'ReconciledAt') IS NULL
EXEC(N'ALTER TABLE dbo.[ShipmentItem] ADD [ReconciledAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[ShipmentItem]', N'ReconciliationStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[ShipmentItem] ADD [ReconciliationStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[ShipmentItem]', N'ShipmentId') IS NULL
EXEC(N'ALTER TABLE dbo.[ShipmentItem] ADD [ShipmentId] bigint NULL;');
IF COL_LENGTH(N'dbo.[ShipmentItem]', N'ShippedQuantity') IS NULL
EXEC(N'ALTER TABLE dbo.[ShipmentItem] ADD [ShippedQuantity] decimal(18,3) NULL;');
END;
IF OBJECT_ID(N'dbo.[PaymentInvoiceAllocation]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [PaymentInvoiceAllocation] (
    [PaymentInvoiceAllocationId] bigint NOT NULL IDENTITY,
    [PaymentTransactionId] bigint NOT NULL,
    [InvoiceId] bigint NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [ContractDepositAllocationId] bigint NULL,
    [RequestKey] varchar(100) NOT NULL,
    [CreatedAt] datetime2 NOT NULL DEFAULT ((sysutcdatetime())),
    CONSTRAINT [PK__PaymentI__B20509AC543F2142] PRIMARY KEY ([PaymentInvoiceAllocationId]),
    CONSTRAINT [AK_PaymentInvoiceAllocation_PaymentInvoiceAllocationId_PaymentTransactionId] UNIQUE ([PaymentInvoiceAllocationId], [PaymentTransactionId]),
    CONSTRAINT [FK__PaymentIn__Contr__60FC61CA] FOREIGN KEY ([ContractDepositAllocationId]) REFERENCES [ContractDepositAllocation] ([ContractDepositAllocationId]),
    CONSTRAINT [FK__PaymentIn__Invoi__5F141958] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId]),
    CONSTRAINT [FK__PaymentIn__Payme__5E1FF51F] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransaction] ([PaymentTransactionId])
);');
INSERT @NewTables VALUES (N'PaymentInvoiceAllocation');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[PaymentInvoiceAllocation]', N'Amount') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentInvoiceAllocation] ADD [Amount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[PaymentInvoiceAllocation]', N'ContractDepositAllocationId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentInvoiceAllocation] ADD [ContractDepositAllocationId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PaymentInvoiceAllocation]', N'CreatedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentInvoiceAllocation] ADD [CreatedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[PaymentInvoiceAllocation]', N'InvoiceId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentInvoiceAllocation] ADD [InvoiceId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PaymentInvoiceAllocation]', N'PaymentTransactionId') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentInvoiceAllocation] ADD [PaymentTransactionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PaymentInvoiceAllocation]', N'RequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[PaymentInvoiceAllocation] ADD [RequestKey] varchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[InvoiceLine]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [InvoiceLine] (
    [InvoiceLineId] bigint NOT NULL IDENTITY,
    [InvoiceId] bigint NOT NULL,
    [OrderItemId] bigint NULL,
    [ShipmentItemId] bigint NULL,
    [Quantity] decimal(18,3) NOT NULL,
    [UnitPriceSnapshot] decimal(18,2) NOT NULL,
    [LineAmount] decimal(18,2) NOT NULL,
    CONSTRAINT [PK__InvoiceL__0D760AD96EA72F75] PRIMARY KEY ([InvoiceLineId]),
    CONSTRAINT [FK_InvoiceLine_Invoice] FOREIGN KEY ([InvoiceId]) REFERENCES [Invoice] ([InvoiceId]),
    CONSTRAINT [FK_InvoiceLine_OrderItem] FOREIGN KEY ([OrderItemId]) REFERENCES [OrderItem] ([OrderItemId]),
    CONSTRAINT [FK_InvoiceLine_ShipmentItem] FOREIGN KEY ([ShipmentItemId]) REFERENCES [ShipmentItem] ([ShipmentItemId])
);');
INSERT @NewTables VALUES (N'InvoiceLine');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[InvoiceLine]', N'InvoiceId') IS NULL
EXEC(N'ALTER TABLE dbo.[InvoiceLine] ADD [InvoiceId] bigint NULL;');
IF COL_LENGTH(N'dbo.[InvoiceLine]', N'LineAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[InvoiceLine] ADD [LineAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[InvoiceLine]', N'OrderItemId') IS NULL
EXEC(N'ALTER TABLE dbo.[InvoiceLine] ADD [OrderItemId] bigint NULL;');
IF COL_LENGTH(N'dbo.[InvoiceLine]', N'Quantity') IS NULL
EXEC(N'ALTER TABLE dbo.[InvoiceLine] ADD [Quantity] decimal(18,3) NULL;');
IF COL_LENGTH(N'dbo.[InvoiceLine]', N'ShipmentItemId') IS NULL
EXEC(N'ALTER TABLE dbo.[InvoiceLine] ADD [ShipmentItemId] bigint NULL;');
IF COL_LENGTH(N'dbo.[InvoiceLine]', N'UnitPriceSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[InvoiceLine] ADD [UnitPriceSnapshot] decimal(18,2) NULL;');
END;
IF OBJECT_ID(N'dbo.[RefundTransaction]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [RefundTransaction] (
    [RefundTransactionId] bigint NOT NULL IDENTITY,
    [PaymentTransactionId] bigint NOT NULL,
    [DisputeTicketId] bigint NULL,
    [ContractTerminationRequestId] bigint NULL,
    [ProcessedByUserId] bigint NOT NULL,
    [RefundCode] varchar(100) NOT NULL,
    [Amount] decimal(18,2) NOT NULL,
    [Reason] nvarchar(500) NULL,
    [RefundStatus] varchar(50) NOT NULL DEFAULT ''Pending'',
    [ProcessedAt] datetime2 NULL,
    [GatewayReference] varchar(150) NULL,
    [PaymentInvoiceAllocationId] bigint NULL,
    [ContractDepositAllocationId] bigint NULL,
    [RequestKey] varchar(100) NOT NULL,
    CONSTRAINT [PK__RefundTr__4745915E127673BE] PRIMARY KEY ([RefundTransactionId]),
    CONSTRAINT [FK_RefundTransaction_DisputeTicket] FOREIGN KEY ([DisputeTicketId]) REFERENCES [DisputeTicket] ([DisputeTicketId]),
    CONSTRAINT [FK_RefundTransaction_PaymentTransaction] FOREIGN KEY ([PaymentTransactionId]) REFERENCES [PaymentTransaction] ([PaymentTransactionId]),
    CONSTRAINT [FK_RefundTransaction_ProcessedByUser] FOREIGN KEY ([ProcessedByUserId]) REFERENCES [User] ([UserId]),
    CONSTRAINT [FK_RefundTransaction_TerminationRequest] FOREIGN KEY ([ContractTerminationRequestId]) REFERENCES [ContractTerminationRequest] ([ContractTerminationRequestId]),
    CONSTRAINT [FK_Refund_AllocatedPayment] FOREIGN KEY ([PaymentInvoiceAllocationId], [PaymentTransactionId]) REFERENCES [PaymentInvoiceAllocation] ([PaymentInvoiceAllocationId], [PaymentTransactionId]),
    CONSTRAINT [FK_Refund_DepositPayment] FOREIGN KEY ([ContractDepositAllocationId], [PaymentTransactionId]) REFERENCES [ContractDepositAllocation] ([ContractDepositAllocationId], [PaymentTransactionId])
);');
INSERT @NewTables VALUES (N'RefundTransaction');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'Amount') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [Amount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'ContractDepositAllocationId') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [ContractDepositAllocationId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'ContractTerminationRequestId') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [ContractTerminationRequestId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'DisputeTicketId') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [DisputeTicketId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'GatewayReference') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [GatewayReference] varchar(150) NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'PaymentInvoiceAllocationId') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [PaymentInvoiceAllocationId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'PaymentTransactionId') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [PaymentTransactionId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'ProcessedAt') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [ProcessedAt] datetime2 NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'ProcessedByUserId') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [ProcessedByUserId] bigint NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'Reason') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [Reason] nvarchar(500) NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'RefundCode') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [RefundCode] varchar(100) NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'RefundStatus') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [RefundStatus] varchar(50) NULL;');
IF COL_LENGTH(N'dbo.[RefundTransaction]', N'RequestKey') IS NULL
EXEC(N'ALTER TABLE dbo.[RefundTransaction] ADD [RequestKey] varchar(100) NULL;');
END;
IF OBJECT_ID(N'dbo.[PayoutItem]', N'U') IS NULL
BEGIN
EXEC(N'CREATE TABLE [PayoutItem] (
    [PayoutItemId] bigint NOT NULL IDENTITY,
    [FarmerPayoutId] bigint NOT NULL,
    [OrderItemId] bigint NULL,
    [ContractMilestoneId] bigint NULL,
    [CommissionPolicyId] bigint NULL,
    [GrossAmount] decimal(18,2) NOT NULL,
    [CommissionRateSnapshot] decimal(5,2) NOT NULL,
    [CommissionAmount] decimal(18,2) NOT NULL,
    [NetAmount] decimal(18,2) NOT NULL,
    [SourceAmountSettled] decimal(18,2) NOT NULL,
    [InvoiceLineId] bigint NOT NULL,
    CONSTRAINT [PK__PayoutIt__3414EE9E7CA0AC22] PRIMARY KEY ([PayoutItemId]),
    CONSTRAINT [FK_PayoutItem_CommissionPolicy] FOREIGN KEY ([CommissionPolicyId]) REFERENCES [CommissionPolicy] ([CommissionPolicyId]),
    CONSTRAINT [FK_PayoutItem_ContractMilestone] FOREIGN KEY ([ContractMilestoneId]) REFERENCES [ContractMilestone] ([ContractMilestoneId]),
    CONSTRAINT [FK_PayoutItem_FarmerPayout] FOREIGN KEY ([FarmerPayoutId]) REFERENCES [FarmerPayout] ([FarmerPayoutId]),
    CONSTRAINT [FK_PayoutItem_InvoiceLine] FOREIGN KEY ([InvoiceLineId]) REFERENCES [InvoiceLine] ([InvoiceLineId]),
    CONSTRAINT [FK_PayoutItem_OrderItem] FOREIGN KEY ([OrderItemId]) REFERENCES [OrderItem] ([OrderItemId])
);');
INSERT @NewTables VALUES (N'PayoutItem');
END;
ELSE
BEGIN
IF COL_LENGTH(N'dbo.[PayoutItem]', N'CommissionAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [CommissionAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'CommissionPolicyId') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [CommissionPolicyId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'CommissionRateSnapshot') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [CommissionRateSnapshot] decimal(5,2) NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'ContractMilestoneId') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [ContractMilestoneId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'FarmerPayoutId') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [FarmerPayoutId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'GrossAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [GrossAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'InvoiceLineId') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [InvoiceLineId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'NetAmount') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [NetAmount] decimal(18,2) NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'OrderItemId') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [OrderItemId] bigint NULL;');
IF COL_LENGTH(N'dbo.[PayoutItem]', N'SourceAmountSettled') IS NULL
EXEC(N'ALTER TABLE dbo.[PayoutItem] ADD [SourceAmountSettled] decimal(18,2) NULL;');
END;
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'AuditLog')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[AuditLog]') AND name = N'IX_AuditLog_PerformedByUserId')
EXEC(N'CREATE INDEX [IX_AuditLog_PerformedByUserId] ON [AuditLog] ([PerformedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'B2BContract')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[B2BContract]') AND name = N'IX_B2BContract_FarmerProfileId')
EXEC(N'CREATE INDEX [IX_B2BContract_FarmerProfileId] ON [B2BContract] ([FarmerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'B2BContract')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[B2BContract]') AND name = N'IX_B2BContract_WholesaleBuyerProfileId')
EXEC(N'CREATE INDEX [IX_B2BContract_WholesaleBuyerProfileId] ON [B2BContract] ([WholesaleBuyerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'B2BContract')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[B2BContract]') AND name = N'UQ__B2BContr__C51D43DA3D1BBBD9')
EXEC(N'CREATE UNIQUE INDEX [UQ__B2BContr__C51D43DA3D1BBBD9] ON [B2BContract] ([ContractNumber]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'B2BSubscription')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[B2BSubscription]') AND name = N'IX_B2BSubscription_PackageId')
EXEC(N'CREATE INDEX [IX_B2BSubscription_PackageId] ON [B2BSubscription] ([PackageId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'B2BSubscription')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[B2BSubscription]') AND name = N'UX_Subscription_Active')
EXEC(N'CREATE UNIQUE INDEX [UX_Subscription_Active] ON [B2BSubscription] ([WholesaleBuyerProfileId]) WHERE ([SubscriptionStatus]=''Active'');');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'B2BSubscriptionPackage')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[B2BSubscriptionPackage]') AND name = N'UQ_B2BSubscriptionPackage_PackageName')
EXEC(N'CREATE UNIQUE INDEX [UQ_B2BSubscriptionPackage_PackageName] ON [B2BSubscriptionPackage] ([PackageName]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Cart')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Cart]') AND name = N'UQ__Cart__1788CC4D31B80BAE')
EXEC(N'CREATE UNIQUE INDEX [UQ__Cart__1788CC4D31B80BAE] ON [Cart] ([UserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CartItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CartItem]') AND name = N'IX_CartItem_PreOrderCampaignId')
EXEC(N'CREATE INDEX [IX_CartItem_PreOrderCampaignId] ON [CartItem] ([PreOrderCampaignId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CartItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CartItem]') AND name = N'IX_CartItem_ProductId_HarvestBatchId')
EXEC(N'CREATE INDEX [IX_CartItem_ProductId_HarvestBatchId] ON [CartItem] ([ProductId], [HarvestBatchId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CartItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CartItem]') AND name = N'UX_Cart_PreOrder')
EXEC(N'CREATE UNIQUE INDEX [UX_Cart_PreOrder] ON [CartItem] ([CartId], [ProductId], [PreOrderCampaignId]) WHERE ([HarvestBatchId] IS NULL AND [PreOrderCampaignId] IS NOT NULL);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CartItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CartItem]') AND name = N'UX_CartItem_Cart_Product_Batch')
EXEC(N'CREATE UNIQUE INDEX [UX_CartItem_Cart_Product_Batch] ON [CartItem] ([CartId], [ProductId], [HarvestBatchId]) WHERE ([HarvestBatchId] IS NOT NULL);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Category')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Category]') AND name = N'UX_Category_Parent_Name')
EXEC(N'CREATE UNIQUE INDEX [UX_Category_Parent_Name] ON [Category] ([ParentCategoryId], [CategoryName]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CommissionPolicy')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CommissionPolicy]') AND name = N'IX_CommissionPolicy_CategoryId')
EXEC(N'CREATE INDEX [IX_CommissionPolicy_CategoryId] ON [CommissionPolicy] ([CategoryId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractDepositAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractDepositAllocation]') AND name = N'IX_ContractDepositAllocation_ContractMilestoneId')
EXEC(N'CREATE INDEX [IX_ContractDepositAllocation_ContractMilestoneId] ON [ContractDepositAllocation] ([ContractMilestoneId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractDepositAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractDepositAllocation]') AND name = N'IX_ContractDepositAllocation_PaymentTransactionId')
EXEC(N'CREATE INDEX [IX_ContractDepositAllocation_PaymentTransactionId] ON [ContractDepositAllocation] ([PaymentTransactionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractDepositAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractDepositAllocation]') AND name = N'UQ__Contract__F6AC419D33161667')
EXEC(N'CREATE UNIQUE INDEX [UQ__Contract__F6AC419D33161667] ON [ContractDepositAllocation] ([RequestKey]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractDepositAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractDepositAllocation]') AND name = N'UQ_DepositAllocation_Payment')
EXEC(N'CREATE UNIQUE INDEX [UQ_DepositAllocation_Payment] ON [ContractDepositAllocation] ([ContractDepositAllocationId], [PaymentTransactionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractLine')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractLine]') AND name = N'IX_ContractLine_B2BContractId')
EXEC(N'CREATE INDEX [IX_ContractLine_B2BContractId] ON [ContractLine] ([B2BContractId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractLine')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractLine]') AND name = N'IX_ContractLine_ContractVersionId')
EXEC(N'CREATE INDEX [IX_ContractLine_ContractVersionId] ON [ContractLine] ([ContractVersionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractLine')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractLine]') AND name = N'IX_ContractLine_CropSeasonId')
EXEC(N'CREATE INDEX [IX_ContractLine_CropSeasonId] ON [ContractLine] ([CropSeasonId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractLine')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractLine]') AND name = N'IX_ContractLine_ProductId')
EXEC(N'CREATE INDEX [IX_ContractLine_ProductId] ON [ContractLine] ([ProductId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractMilestone')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractMilestone]') AND name = N'IX_ContractMilestone_B2BContractId')
EXEC(N'CREATE INDEX [IX_ContractMilestone_B2BContractId] ON [ContractMilestone] ([B2BContractId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractMilestone')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractMilestone]') AND name = N'IX_ContractMilestone_ContractLineId')
EXEC(N'CREATE INDEX [IX_ContractMilestone_ContractLineId] ON [ContractMilestone] ([ContractLineId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractMilestone')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractMilestone]') AND name = N'IX_ContractMilestone_ContractVersionId')
EXEC(N'CREATE INDEX [IX_ContractMilestone_ContractVersionId] ON [ContractMilestone] ([ContractVersionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractSignature')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractSignature]') AND name = N'IX_ContractSignature_SignedByUserId')
EXEC(N'CREATE INDEX [IX_ContractSignature_SignedByUserId] ON [ContractSignature] ([SignedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractSignature')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractSignature]') AND name = N'UX_Signature_Party')
EXEC(N'CREATE UNIQUE INDEX [UX_Signature_Party] ON [ContractSignature] ([ContractVersionId], [PartyRole]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractSignature')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractSignature]') AND name = N'UX_Signature_User')
EXEC(N'CREATE UNIQUE INDEX [UX_Signature_User] ON [ContractSignature] ([ContractVersionId], [SignedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractTerminationRequest')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractTerminationRequest]') AND name = N'IX_ContractTerminationRequest_RequestedByUserId')
EXEC(N'CREATE INDEX [IX_ContractTerminationRequest_RequestedByUserId] ON [ContractTerminationRequest] ([RequestedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractTerminationRequest')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractTerminationRequest]') AND name = N'UX_Termination_Pending')
EXEC(N'CREATE UNIQUE INDEX [UX_Termination_Pending] ON [ContractTerminationRequest] ([B2BContractId]) WHERE ([RequestStatus]=''Pending'');');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractVersion')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractVersion]') AND name = N'IX_ContractVersion_ProposedByUserId')
EXEC(N'CREATE INDEX [IX_ContractVersion_ProposedByUserId] ON [ContractVersion] ([ProposedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ContractVersion')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ContractVersion]') AND name = N'UQ_ContractVersion_Contract_VersionNo')
EXEC(N'CREATE UNIQUE INDEX [UQ_ContractVersion_Contract_VersionNo] ON [ContractVersion] ([B2BContractId], [VersionNo]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CropSeason')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CropSeason]') AND name = N'IX_CropSeason_FarmerProfileId')
EXEC(N'CREATE INDEX [IX_CropSeason_FarmerProfileId] ON [CropSeason] ([FarmerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CropSeason')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CropSeason]') AND name = N'IX_CropSeason_ProductId')
EXEC(N'CREATE INDEX [IX_CropSeason_ProductId] ON [CropSeason] ([ProductId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CultivationLog')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CultivationLog]') AND name = N'IX_CultivationLog_CorrectsLogId')
EXEC(N'CREATE INDEX [IX_CultivationLog_CorrectsLogId] ON [CultivationLog] ([CorrectsLogId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CultivationLog')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CultivationLog]') AND name = N'IX_CultivationLog_CropSeasonId')
EXEC(N'CREATE INDEX [IX_CultivationLog_CropSeasonId] ON [CultivationLog] ([CropSeasonId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'CultivationLog')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[CultivationLog]') AND name = N'IX_CultivationLog_RecordedByUserId')
EXEC(N'CREATE INDEX [IX_CultivationLog_RecordedByUserId] ON [CultivationLog] ([RecordedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DeliveryAddress')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DeliveryAddress]') AND name = N'UX_DeliveryAddress_OneDefaultPerUser')
EXEC(N'CREATE UNIQUE INDEX [UX_DeliveryAddress_OneDefaultPerUser] ON [DeliveryAddress] ([UserId]) WHERE ([IsDefault]=(1));');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeEvidence')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeEvidence]') AND name = N'IX_DisputeEvidence_DisputeTicketId')
EXEC(N'CREATE INDEX [IX_DisputeEvidence_DisputeTicketId] ON [DisputeEvidence] ([DisputeTicketId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeEvidence')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeEvidence]') AND name = N'IX_DisputeEvidence_EvidenceRequestId')
EXEC(N'CREATE INDEX [IX_DisputeEvidence_EvidenceRequestId] ON [DisputeEvidence] ([EvidenceRequestId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeEvidence')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeEvidence]') AND name = N'IX_DisputeEvidence_SubmittedByUserId')
EXEC(N'CREATE INDEX [IX_DisputeEvidence_SubmittedByUserId] ON [DisputeEvidence] ([SubmittedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeEvidenceRequest')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeEvidenceRequest]') AND name = N'IX_DisputeEvidenceRequest_DisputeTicketId')
EXEC(N'CREATE INDEX [IX_DisputeEvidenceRequest_DisputeTicketId] ON [DisputeEvidenceRequest] ([DisputeTicketId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeEvidenceRequest')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeEvidenceRequest]') AND name = N'IX_DisputeEvidenceRequest_RequestedByUserId')
EXEC(N'CREATE INDEX [IX_DisputeEvidenceRequest_RequestedByUserId] ON [DisputeEvidenceRequest] ([RequestedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeEvidenceRequest')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeEvidenceRequest]') AND name = N'IX_DisputeEvidenceRequest_RequestedPartyUserId')
EXEC(N'CREATE INDEX [IX_DisputeEvidenceRequest_RequestedPartyUserId] ON [DisputeEvidenceRequest] ([RequestedPartyUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeTicket')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeTicket]') AND name = N'IX_DisputeTicket_AssignedStaffUserId')
EXEC(N'CREATE INDEX [IX_DisputeTicket_AssignedStaffUserId] ON [DisputeTicket] ([AssignedStaffUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeTicket')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeTicket]') AND name = N'IX_DisputeTicket_B2BContractId')
EXEC(N'CREATE INDEX [IX_DisputeTicket_B2BContractId] ON [DisputeTicket] ([B2BContractId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeTicket')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeTicket]') AND name = N'IX_DisputeTicket_SellerOrderId')
EXEC(N'CREATE INDEX [IX_DisputeTicket_SellerOrderId] ON [DisputeTicket] ([SellerOrderId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'DisputeTicket')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[DisputeTicket]') AND name = N'IX_DisputeTicket_SubmittedByUserId')
EXEC(N'CREATE INDEX [IX_DisputeTicket_SubmittedByUserId] ON [DisputeTicket] ([SubmittedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmCertificate')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmCertificate]') AND name = N'IX_FarmCertificate_FarmerProfileId')
EXEC(N'CREATE INDEX [IX_FarmCertificate_FarmerProfileId] ON [FarmCertificate] ([FarmerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmCertificate')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmCertificate]') AND name = N'IX_FarmCertificate_ReviewedByUserId')
EXEC(N'CREATE INDEX [IX_FarmCertificate_ReviewedByUserId] ON [FarmCertificate] ([ReviewedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmCertificate')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmCertificate]') AND name = N'UQ_FarmCertificate_Type_Number')
EXEC(N'CREATE UNIQUE INDEX [UQ_FarmCertificate_Type_Number] ON [FarmCertificate] ([CertificateType], [CertificateNumber]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmerPayout')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmerPayout]') AND name = N'IX_FarmerPayout_ApprovedByUserId')
EXEC(N'CREATE INDEX [IX_FarmerPayout_ApprovedByUserId] ON [FarmerPayout] ([ApprovedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmerPayout')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmerPayout]') AND name = N'IX_FarmerPayout_FarmerProfileId')
EXEC(N'CREATE INDEX [IX_FarmerPayout_FarmerProfileId] ON [FarmerPayout] ([FarmerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmerPayout')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmerPayout]') AND name = N'UQ__FarmerPa__3F2306E3E98F526C')
EXEC(N'CREATE UNIQUE INDEX [UQ__FarmerPa__3F2306E3E98F526C] ON [FarmerPayout] ([PayoutCode]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmerPayout')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmerPayout]') AND name = N'UQ__FarmerPa__F6AC419D6087B004')
EXEC(N'CREATE UNIQUE INDEX [UQ__FarmerPa__F6AC419D6087B004] ON [FarmerPayout] ([RequestKey]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmerPayout')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmerPayout]') AND name = N'UX_Payout_BankRef')
EXEC(N'CREATE UNIQUE INDEX [UX_Payout_BankRef] ON [FarmerPayout] ([BankTransferRef]) WHERE ([BankTransferRef] IS NOT NULL);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'FarmerProfile')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[FarmerProfile]') AND name = N'UQ__FarmerPr__1788CC4D8B016AF1')
EXEC(N'CREATE UNIQUE INDEX [UQ__FarmerPr__1788CC4D8B016AF1] ON [FarmerProfile] ([UserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'HarvestBatch')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[HarvestBatch]') AND name = N'IX_HarvestBatch_CropSeasonId')
EXEC(N'CREATE INDEX [IX_HarvestBatch_CropSeasonId] ON [HarvestBatch] ([CropSeasonId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'HarvestBatch')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[HarvestBatch]') AND name = N'IX_HarvestBatch_Fefo')
EXEC(N'CREATE INDEX [IX_HarvestBatch_Fefo] ON [HarvestBatch] ([ProductId], [Status], [ExpirationDate], [HarvestBatchId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'HarvestBatch')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[HarvestBatch]') AND name = N'IX_HarvestBatch_PreOrderCampaignId')
EXEC(N'CREATE INDEX [IX_HarvestBatch_PreOrderCampaignId] ON [HarvestBatch] ([PreOrderCampaignId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'HarvestBatch')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[HarvestBatch]') AND name = N'UQ__HarvestB__B22ADA8E8FADDF47')
EXEC(N'CREATE UNIQUE INDEX [UQ__HarvestB__B22ADA8E8FADDF47] ON [HarvestBatch] ([BatchCode]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'HarvestBatch')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[HarvestBatch]') AND name = N'UQ_HarvestBatch_Product_Batch')
EXEC(N'CREATE UNIQUE INDEX [UQ_HarvestBatch_Product_Batch] ON [HarvestBatch] ([ProductId], [HarvestBatchId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'InventoryMovement')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[InventoryMovement]') AND name = N'IX_InventoryMovement_HarvestBatchId')
EXEC(N'CREATE INDEX [IX_InventoryMovement_HarvestBatchId] ON [InventoryMovement] ([HarvestBatchId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'InventoryMovement')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[InventoryMovement]') AND name = N'IX_InventoryMovement_RecordedByUserId')
EXEC(N'CREATE INDEX [IX_InventoryMovement_RecordedByUserId] ON [InventoryMovement] ([RecordedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'InventoryMovement')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[InventoryMovement]') AND name = N'UQ__Inventor__F6AC419DCEE7C1CE')
EXEC(N'CREATE UNIQUE INDEX [UQ__Inventor__F6AC419DCEE7C1CE] ON [InventoryMovement] ([RequestKey]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'InventoryMovement')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[InventoryMovement]') AND name = N'UX_Movement_Dispatch')
EXEC(N'CREATE UNIQUE INDEX [UX_Movement_Dispatch] ON [InventoryMovement] ([OrderItemBatchAllocationId]) WHERE ([MovementType]=''Dispatch'' AND [OrderItemBatchAllocationId] IS NOT NULL);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Invoice')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Invoice]') AND name = N'IX_Invoice_B2BSubscriptionId')
EXEC(N'CREATE INDEX [IX_Invoice_B2BSubscriptionId] ON [Invoice] ([B2BSubscriptionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Invoice')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Invoice]') AND name = N'IX_Invoice_ContractMilestoneId')
EXEC(N'CREATE INDEX [IX_Invoice_ContractMilestoneId] ON [Invoice] ([ContractMilestoneId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Invoice')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Invoice]') AND name = N'IX_Invoice_SellerOrderId')
EXEC(N'CREATE INDEX [IX_Invoice_SellerOrderId] ON [Invoice] ([SellerOrderId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Invoice')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Invoice]') AND name = N'UQ__Invoice__D776E9815502D38A')
EXEC(N'CREATE UNIQUE INDEX [UQ__Invoice__D776E9815502D38A] ON [Invoice] ([InvoiceNumber]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'InvoiceLine')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[InvoiceLine]') AND name = N'IX_InvoiceLine_InvoiceId')
EXEC(N'CREATE INDEX [IX_InvoiceLine_InvoiceId] ON [InvoiceLine] ([InvoiceId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'InvoiceLine')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[InvoiceLine]') AND name = N'IX_InvoiceLine_OrderItemId')
EXEC(N'CREATE INDEX [IX_InvoiceLine_OrderItemId] ON [InvoiceLine] ([OrderItemId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'InvoiceLine')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[InvoiceLine]') AND name = N'IX_InvoiceLine_ShipmentItemId')
EXEC(N'CREATE INDEX [IX_InvoiceLine_ShipmentItemId] ON [InvoiceLine] ([ShipmentItemId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Notification')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Notification]') AND name = N'IX_Notification_UserId')
EXEC(N'CREATE INDEX [IX_Notification_UserId] ON [Notification] ([UserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Order')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = N'IX_Order_BuyerUserId_RetailQuoteId')
EXEC(N'CREATE INDEX [IX_Order_BuyerUserId_RetailQuoteId] ON [Order] ([BuyerUserId], [RetailQuoteId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Order')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = N'UQ__Order__999B5229A8D719A9')
EXEC(N'CREATE UNIQUE INDEX [UQ__Order__999B5229A8D719A9] ON [Order] ([OrderCode]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Order')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = N'UQ_Order_CheckoutRequest')
EXEC(N'CREATE UNIQUE INDEX [UQ_Order_CheckoutRequest] ON [Order] ([BuyerUserId], [CheckoutRequestKey]) WHERE [CheckoutRequestKey] IS NOT NULL;');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Order')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Order]') AND name = N'UX_Order_Quote')
EXEC(N'CREATE UNIQUE INDEX [UX_Order_Quote] ON [Order] ([RetailQuoteId]) WHERE ([RetailQuoteId] IS NOT NULL);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'OrderItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[OrderItem]') AND name = N'IX_OrderItem_ContractLineId')
EXEC(N'CREATE INDEX [IX_OrderItem_ContractLineId] ON [OrderItem] ([ContractLineId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'OrderItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[OrderItem]') AND name = N'IX_OrderItem_PreOrderCampaignId')
EXEC(N'CREATE INDEX [IX_OrderItem_PreOrderCampaignId] ON [OrderItem] ([PreOrderCampaignId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'OrderItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[OrderItem]') AND name = N'IX_OrderItem_ProductId')
EXEC(N'CREATE INDEX [IX_OrderItem_ProductId] ON [OrderItem] ([ProductId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'OrderItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[OrderItem]') AND name = N'IX_OrderItem_SellerOrderId')
EXEC(N'CREATE INDEX [IX_OrderItem_SellerOrderId] ON [OrderItem] ([SellerOrderId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'OrderItemBatchAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[OrderItemBatchAllocation]') AND name = N'IX_OrderItemBatchAllocation_HarvestBatchId')
EXEC(N'CREATE INDEX [IX_OrderItemBatchAllocation_HarvestBatchId] ON [OrderItemBatchAllocation] ([HarvestBatchId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'OrderItemBatchAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[OrderItemBatchAllocation]') AND name = N'UX_Allocation_Item_Batch')
EXEC(N'CREATE UNIQUE INDEX [UX_Allocation_Item_Batch] ON [OrderItemBatchAllocation] ([OrderItemId], [HarvestBatchId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'OrderStatusHistory')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[OrderStatusHistory]') AND name = N'IX_OrderStatusHistory_ChangedByUserId')
EXEC(N'CREATE INDEX [IX_OrderStatusHistory_ChangedByUserId] ON [OrderStatusHistory] ([ChangedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'OrderStatusHistory')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[OrderStatusHistory]') AND name = N'IX_OrderStatusHistory_SellerOrderId')
EXEC(N'CREATE INDEX [IX_OrderStatusHistory_SellerOrderId] ON [OrderStatusHistory] ([SellerOrderId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentGatewayEvent')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentGatewayEvent]') AND name = N'IX_PaymentGatewayEvent_PaymentTransactionId')
EXEC(N'CREATE INDEX [IX_PaymentGatewayEvent_PaymentTransactionId] ON [PaymentGatewayEvent] ([PaymentTransactionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentGatewayEvent')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentGatewayEvent]') AND name = N'UQ_GatewayEvent')
EXEC(N'CREATE UNIQUE INDEX [UQ_GatewayEvent] ON [PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentInvoiceAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentInvoiceAllocation]') AND name = N'IX_PaymentAllocation_Invoice')
EXEC(N'CREATE INDEX [IX_PaymentAllocation_Invoice] ON [PaymentInvoiceAllocation] ([InvoiceId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentInvoiceAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentInvoiceAllocation]') AND name = N'IX_PaymentAllocation_Payment')
EXEC(N'CREATE INDEX [IX_PaymentAllocation_Payment] ON [PaymentInvoiceAllocation] ([PaymentTransactionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentInvoiceAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentInvoiceAllocation]') AND name = N'IX_PaymentInvoiceAllocation_ContractDepositAllocationId')
EXEC(N'CREATE INDEX [IX_PaymentInvoiceAllocation_ContractDepositAllocationId] ON [PaymentInvoiceAllocation] ([ContractDepositAllocationId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentInvoiceAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentInvoiceAllocation]') AND name = N'UQ__PaymentI__F6AC419D49E398A7')
EXEC(N'CREATE UNIQUE INDEX [UQ__PaymentI__F6AC419D49E398A7] ON [PaymentInvoiceAllocation] ([RequestKey]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentInvoiceAllocation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentInvoiceAllocation]') AND name = N'UQ_PaymentAllocation_Payment')
EXEC(N'CREATE UNIQUE INDEX [UQ_PaymentAllocation_Payment] ON [PaymentInvoiceAllocation] ([PaymentInvoiceAllocationId], [PaymentTransactionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentTransaction]') AND name = N'IX_PaymentTransaction_B2BContractId')
EXEC(N'CREATE INDEX [IX_PaymentTransaction_B2BContractId] ON [PaymentTransaction] ([B2BContractId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentTransaction]') AND name = N'IX_PaymentTransaction_B2BSubscriptionId')
EXEC(N'CREATE INDEX [IX_PaymentTransaction_B2BSubscriptionId] ON [PaymentTransaction] ([B2BSubscriptionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentTransaction]') AND name = N'IX_PaymentTransaction_ContractMilestoneId')
EXEC(N'CREATE INDEX [IX_PaymentTransaction_ContractMilestoneId] ON [PaymentTransaction] ([ContractMilestoneId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentTransaction]') AND name = N'IX_PaymentTransaction_OrderId')
EXEC(N'CREATE INDEX [IX_PaymentTransaction_OrderId] ON [PaymentTransaction] ([OrderId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentTransaction]') AND name = N'UQ__PaymentT__D85E7026882D368C')
EXEC(N'CREATE UNIQUE INDEX [UQ__PaymentT__D85E7026882D368C] ON [PaymentTransaction] ([TransactionCode]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentTransaction]') AND name = N'UQ_Payment_Request')
EXEC(N'CREATE UNIQUE INDEX [UQ_Payment_Request] ON [PaymentTransaction] ([GatewayProvider], [RequestKey]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PaymentTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PaymentTransaction]') AND name = N'UX_Payment_ProviderRef')
EXEC(N'CREATE UNIQUE INDEX [UX_Payment_ProviderRef] ON [PaymentTransaction] ([GatewayProvider], [ProviderTransactionReference]) WHERE ([ProviderTransactionReference] IS NOT NULL);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PayoutItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PayoutItem]') AND name = N'IX_PayoutItem_CommissionPolicyId')
EXEC(N'CREATE INDEX [IX_PayoutItem_CommissionPolicyId] ON [PayoutItem] ([CommissionPolicyId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PayoutItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PayoutItem]') AND name = N'IX_PayoutItem_ContractMilestoneId')
EXEC(N'CREATE INDEX [IX_PayoutItem_ContractMilestoneId] ON [PayoutItem] ([ContractMilestoneId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PayoutItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PayoutItem]') AND name = N'IX_PayoutItem_FarmerPayoutId')
EXEC(N'CREATE INDEX [IX_PayoutItem_FarmerPayoutId] ON [PayoutItem] ([FarmerPayoutId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PayoutItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PayoutItem]') AND name = N'IX_PayoutItem_InvoiceLineId')
EXEC(N'CREATE INDEX [IX_PayoutItem_InvoiceLineId] ON [PayoutItem] ([InvoiceLineId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PayoutItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PayoutItem]') AND name = N'IX_PayoutItem_OrderItemId')
EXEC(N'CREATE INDEX [IX_PayoutItem_OrderItemId] ON [PayoutItem] ([OrderItemId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PreOrderCampaign')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PreOrderCampaign]') AND name = N'IX_PreOrderCampaign_CropSeasonId')
EXEC(N'CREATE INDEX [IX_PreOrderCampaign_CropSeasonId] ON [PreOrderCampaign] ([CropSeasonId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PreOrderCampaign')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PreOrderCampaign]') AND name = N'IX_PreOrderCampaign_ProductId')
EXEC(N'CREATE INDEX [IX_PreOrderCampaign_ProductId] ON [PreOrderCampaign] ([ProductId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PreOrderReservation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PreOrderReservation]') AND name = N'IX_PreOrder_Capacity')
EXEC(N'CREATE INDEX [IX_PreOrder_Capacity] ON [PreOrderReservation] ([PreOrderCampaignId], [ReservationStatus]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PreOrderReservation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PreOrderReservation]') AND name = N'UQ__PreOrder__57ED068071EAEEA3')
EXEC(N'CREATE UNIQUE INDEX [UQ__PreOrder__57ED068071EAEEA3] ON [PreOrderReservation] ([OrderItemId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'PreOrderReservation')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[PreOrderReservation]') AND name = N'UQ__PreOrder__F6AC419D6B0D8361')
EXEC(N'CREATE UNIQUE INDEX [UQ__PreOrder__F6AC419D6B0D8361] ON [PreOrderReservation] ([RequestKey]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Product')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Product]') AND name = N'IX_Product_CategoryId')
EXEC(N'CREATE INDEX [IX_Product_CategoryId] ON [Product] ([CategoryId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Product')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Product]') AND name = N'IX_Product_FarmerProfileId')
EXEC(N'CREATE INDEX [IX_Product_FarmerProfileId] ON [Product] ([FarmerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ProductImage')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ProductImage]') AND name = N'UX_ProductImage_OnePrimaryPerProduct')
EXEC(N'CREATE UNIQUE INDEX [UX_ProductImage_OnePrimaryPerProduct] ON [ProductImage] ([ProductId]) WHERE ([IsPrimary]=(1));');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ProductReview')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ProductReview]') AND name = N'IX_ProductReview_UserId')
EXEC(N'CREATE INDEX [IX_ProductReview_UserId] ON [ProductReview] ([UserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ProductReview')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ProductReview]') AND name = N'UQ__ProductR__57ED0680AE799D83')
EXEC(N'CREATE UNIQUE INDEX [UQ__ProductR__57ED0680AE799D83] ON [ProductReview] ([OrderItemId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'IX_RefundTransaction_ContractDepositAllocationId_PaymentTransactionId')
EXEC(N'CREATE INDEX [IX_RefundTransaction_ContractDepositAllocationId_PaymentTransactionId] ON [RefundTransaction] ([ContractDepositAllocationId], [PaymentTransactionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'IX_RefundTransaction_ContractTerminationRequestId')
EXEC(N'CREATE INDEX [IX_RefundTransaction_ContractTerminationRequestId] ON [RefundTransaction] ([ContractTerminationRequestId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'IX_RefundTransaction_DisputeTicketId')
EXEC(N'CREATE INDEX [IX_RefundTransaction_DisputeTicketId] ON [RefundTransaction] ([DisputeTicketId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'IX_RefundTransaction_PaymentInvoiceAllocationId_PaymentTransactionId')
EXEC(N'CREATE INDEX [IX_RefundTransaction_PaymentInvoiceAllocationId_PaymentTransactionId] ON [RefundTransaction] ([PaymentInvoiceAllocationId], [PaymentTransactionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'IX_RefundTransaction_PaymentTransactionId')
EXEC(N'CREATE INDEX [IX_RefundTransaction_PaymentTransactionId] ON [RefundTransaction] ([PaymentTransactionId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'IX_RefundTransaction_ProcessedByUserId')
EXEC(N'CREATE INDEX [IX_RefundTransaction_ProcessedByUserId] ON [RefundTransaction] ([ProcessedByUserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'UQ__RefundTr__7DFE7F497899A6E4')
EXEC(N'CREATE UNIQUE INDEX [UQ__RefundTr__7DFE7F497899A6E4] ON [RefundTransaction] ([RefundCode]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'UQ__RefundTr__F6AC419D4C5118D2')
EXEC(N'CREATE UNIQUE INDEX [UQ__RefundTr__F6AC419D4C5118D2] ON [RefundTransaction] ([RequestKey]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RefundTransaction')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RefundTransaction]') AND name = N'UX_Refund_ProviderRef')
EXEC(N'CREATE UNIQUE INDEX [UX_Refund_ProviderRef] ON [RefundTransaction] ([GatewayReference]) WHERE ([GatewayReference] IS NOT NULL);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RegionalPriceBenchmark')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RegionalPriceBenchmark]') AND name = N'IX_RegionalPriceBenchmark_CategoryId')
EXEC(N'CREATE INDEX [IX_RegionalPriceBenchmark_CategoryId] ON [RegionalPriceBenchmark] ([CategoryId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RetailCheckoutQuote')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RetailCheckoutQuote]') AND name = N'IX_RetailQuote_Buyer_Expiry')
EXEC(N'CREATE INDEX [IX_RetailQuote_Buyer_Expiry] ON [RetailCheckoutQuote] ([BuyerUserId], [ExpiresAt]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RetailCheckoutQuote')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RetailCheckoutQuote]') AND name = N'UQ_RetailCheckoutQuote_Buyer_Quote')
EXEC(N'CREATE UNIQUE INDEX [UQ_RetailCheckoutQuote_Buyer_Quote] ON [RetailCheckoutQuote] ([BuyerUserId], [QuoteId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RewardPointEntry')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RewardPointEntry]') AND name = N'IX_Reward_Order')
EXEC(N'CREATE INDEX [IX_Reward_Order] ON [RewardPointEntry] ([OrderId], [EntryType]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RewardPointEntry')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RewardPointEntry]') AND name = N'IX_Reward_User')
EXEC(N'CREATE INDEX [IX_Reward_User] ON [RewardPointEntry] ([UserId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'RewardPointEntry')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[RewardPointEntry]') AND name = N'UQ__RewardPo__F6AC419D35FB1DC3')
EXEC(N'CREATE UNIQUE INDEX [UQ__RewardPo__F6AC419D35FB1DC3] ON [RewardPointEntry] ([RequestKey]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'SellerOrder')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[SellerOrder]') AND name = N'IX_SellerOrder_B2BContractId')
EXEC(N'CREATE INDEX [IX_SellerOrder_B2BContractId] ON [SellerOrder] ([B2BContractId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'SellerOrder')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[SellerOrder]') AND name = N'IX_SellerOrder_FarmerProfileId')
EXEC(N'CREATE INDEX [IX_SellerOrder_FarmerProfileId] ON [SellerOrder] ([FarmerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'SellerOrder')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[SellerOrder]') AND name = N'UQ__SellerOr__784ABE9CCA75C310')
EXEC(N'CREATE UNIQUE INDEX [UQ__SellerOr__784ABE9CCA75C310] ON [SellerOrder] ([SellerOrderCode]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'SellerOrder')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[SellerOrder]') AND name = N'UQ_SellerOrder_Order_Farmer')
EXEC(N'CREATE UNIQUE INDEX [UQ_SellerOrder_Order_Farmer] ON [SellerOrder] ([OrderId], [FarmerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Shipment')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Shipment]') AND name = N'IX_Shipment_ContractMilestoneId')
EXEC(N'CREATE INDEX [IX_Shipment_ContractMilestoneId] ON [Shipment] ([ContractMilestoneId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Shipment')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Shipment]') AND name = N'IX_Shipment_SellerOrderId')
EXEC(N'CREATE INDEX [IX_Shipment_SellerOrderId] ON [Shipment] ([SellerOrderId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Shipment')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Shipment]') AND name = N'UX_Shipment_TrackingNumber_NotNull')
EXEC(N'CREATE UNIQUE INDEX [UX_Shipment_TrackingNumber_NotNull] ON [Shipment] ([TrackingNumber]) WHERE ([TrackingNumber] IS NOT NULL);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ShipmentItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ShipmentItem]') AND name = N'IX_ShipmentItem_OrderItemBatchAllocationId')
EXEC(N'CREATE INDEX [IX_ShipmentItem_OrderItemBatchAllocationId] ON [ShipmentItem] ([OrderItemBatchAllocationId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'ShipmentItem')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[ShipmentItem]') AND name = N'UX_Shipment_Allocation')
EXEC(N'CREATE UNIQUE INDEX [UX_Shipment_Allocation] ON [ShipmentItem] ([ShipmentId], [OrderItemBatchAllocationId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'TraceabilityQRCode')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[TraceabilityQRCode]') AND name = N'UQ__Traceabi__1DCFB785E9DC0DAD')
EXEC(N'CREATE UNIQUE INDEX [UQ__Traceabi__1DCFB785E9DC0DAD] ON [TraceabilityQRCode] ([QRContentString]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'TraceabilityQRCode')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[TraceabilityQRCode]') AND name = N'UQ__Traceabi__619519FAC08107BD')
EXEC(N'CREATE UNIQUE INDEX [UQ__Traceabi__619519FAC08107BD] ON [TraceabilityQRCode] ([HarvestBatchId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'User')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[User]') AND name = N'UQ__User__536C85E46FD72E95')
EXEC(N'CREATE UNIQUE INDEX [UQ__User__536C85E46FD72E95] ON [User] ([Username]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'User')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[User]') AND name = N'UQ__User__A9D10534209BB63E')
EXEC(N'CREATE UNIQUE INDEX [UQ__User__A9D10534209BB63E] ON [User] ([Email]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Voucher')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Voucher]') AND name = N'IX_Voucher_FarmerProfileId')
EXEC(N'CREATE INDEX [IX_Voucher_FarmerProfileId] ON [Voucher] ([FarmerProfileId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'Voucher')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[Voucher]') AND name = N'UQ__Voucher__7F0ABCA9ACEF6638')
EXEC(N'CREATE UNIQUE INDEX [UQ__Voucher__7F0ABCA9ACEF6638] ON [Voucher] ([VoucherCode]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'VoucherRedemption')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[VoucherRedemption]') AND name = N'IX_VoucherRedemption_OrderId')
EXEC(N'CREATE INDEX [IX_VoucherRedemption_OrderId] ON [VoucherRedemption] ([OrderId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'VoucherRedemption')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[VoucherRedemption]') AND name = N'IX_VoucherRedemption_Quota')
EXEC(N'CREATE INDEX [IX_VoucherRedemption_Quota] ON [VoucherRedemption] ([VoucherId], [Status], [ExpiresAt]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'VoucherRedemption')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[VoucherRedemption]') AND name = N'IX_VoucherRedemption_SellerOrderId')
EXEC(N'CREATE INDEX [IX_VoucherRedemption_SellerOrderId] ON [VoucherRedemption] ([SellerOrderId]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'VoucherRedemption')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[VoucherRedemption]') AND name = N'UX_Voucher_Order')
EXEC(N'CREATE UNIQUE INDEX [UX_Voucher_Order] ON [VoucherRedemption] ([VoucherId], [OrderId], [SellerOrderId]) WHERE [SellerOrderId] IS NOT NULL;');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'WholesaleBuyerProfile')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[WholesaleBuyerProfile]') AND name = N'UQ__Wholesal__12945A2895F1085B')
EXEC(N'CREATE UNIQUE INDEX [UQ__Wholesal__12945A2895F1085B] ON [WholesaleBuyerProfile] ([TaxCode]);');
IF EXISTS (SELECT 1 FROM @NewTables WHERE Name = N'WholesaleBuyerProfile')
AND NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'dbo.[WholesaleBuyerProfile]') AND name = N'UQ__Wholesal__1788CC4D5A8DFD28')
EXEC(N'CREATE UNIQUE INDEX [UQ__Wholesal__1788CC4D5A8DFD28] ON [WholesaleBuyerProfile] ([UserId]);');
IF OBJECT_ID(N'dbo.TR_Reward_AppendOnly', N'TR') IS NULL
EXEC(N'CREATE TRIGGER dbo.TR_Reward_AppendOnly ON dbo.RewardPointEntry
AFTER UPDATE, DELETE AS BEGIN
SET NOCOUNT ON;
THROW 51012, ''Reward point entries are append-only.'', 1;
END;');
COMMIT TRANSACTION;
END TRY
BEGIN CATCH
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
