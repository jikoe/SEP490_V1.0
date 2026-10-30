/*
  AgriSustain – UC-AUTH-03 Manage Profile: thêm các trường hồ sơ theo Report 3 (FR 6.2.1, FR 3.2).
  Ngày: 29/10/2026 – Người thực hiện: DuND

  Thêm (tất cả cho phép NULL, không đổi/xóa dữ liệu cũ):
    dbo.[User]           Gender           varchar(10)    'Male' | 'Female' | 'Other'
                         DateOfBirth      date           không ở tương lai
    dbo.[FarmerProfile]  CultivatedArea   decimal(10,2)  diện tích canh tác (ha) > 0
                         ProductionModel  varchar(20)    'Individual' (Cá nhân) | 'Cooperative' (Hợp tác xã)
                         ShortDescription nvarchar(300)  mô tả ngắn

  Cách chạy: mở SSMS, chọn database AgriSustain, chạy toàn bộ file (F5).
  Chạy lại nhiều lần vẫn an toàn (kiểm tra cột/ràng buộc trước khi thêm).
  BẮT BUỘC chạy trước khi dùng code mới: thiếu cột thì mọi truy vấn bảng User sẽ lỗi "Invalid column name".
*/
USE [AgriSustain];
GO
SET XACT_ABORT ON;
BEGIN TRANSACTION;

IF COL_LENGTH(N'dbo.[User]', N'Gender') IS NULL
    ALTER TABLE dbo.[User] ADD [Gender] varchar(10) NULL;
IF COL_LENGTH(N'dbo.[User]', N'DateOfBirth') IS NULL
    ALTER TABLE dbo.[User] ADD [DateOfBirth] date NULL;

IF COL_LENGTH(N'dbo.[FarmerProfile]', N'CultivatedArea') IS NULL
    ALTER TABLE dbo.[FarmerProfile] ADD [CultivatedArea] decimal(10,2) NULL;
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'ProductionModel') IS NULL
    ALTER TABLE dbo.[FarmerProfile] ADD [ProductionModel] varchar(20) NULL;
IF COL_LENGTH(N'dbo.[FarmerProfile]', N'ShortDescription') IS NULL
    ALTER TABLE dbo.[FarmerProfile] ADD [ShortDescription] nvarchar(300) NULL;

COMMIT TRANSACTION;
GO

-- Ràng buộc chạy ở batch riêng vì cột vừa được thêm ở batch trên.
IF OBJECT_ID(N'dbo.CK_User_Gender', N'C') IS NULL
    ALTER TABLE dbo.[User] WITH CHECK ADD CONSTRAINT [CK_User_Gender]
        CHECK ([Gender] IS NULL OR [Gender] IN ('Male', 'Female', 'Other'));
IF OBJECT_ID(N'dbo.CK_User_DateOfBirth', N'C') IS NULL
    ALTER TABLE dbo.[User] WITH CHECK ADD CONSTRAINT [CK_User_DateOfBirth]
        CHECK ([DateOfBirth] IS NULL OR [DateOfBirth] <= CAST(SYSUTCDATETIME() AS date));
IF OBJECT_ID(N'dbo.CK_FarmerProfile_CultivatedArea', N'C') IS NULL
    ALTER TABLE dbo.[FarmerProfile] WITH CHECK ADD CONSTRAINT [CK_FarmerProfile_CultivatedArea]
        CHECK ([CultivatedArea] IS NULL OR [CultivatedArea] > 0);
IF OBJECT_ID(N'dbo.CK_FarmerProfile_ProductionModel', N'C') IS NULL
    ALTER TABLE dbo.[FarmerProfile] WITH CHECK ADD CONSTRAINT [CK_FarmerProfile_ProductionModel]
        CHECK ([ProductionModel] IS NULL OR [ProductionModel] IN ('Individual', 'Cooperative'));
GO

PRINT N'20261029_profile_fields.sql: đã thêm Gender, DateOfBirth (User) và CultivatedArea, ProductionModel, ShortDescription (FarmerProfile).';
GO
