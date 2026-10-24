-- DEMO FIXTURES: preserves the supplied dataset; does not implement production workflows.
-- Runs once per database. Does not reset passwords, disable triggers or assume identity values.
-- Demo accounts use password 123456. Keep demo data out of production.
-- Requires the schema included in Update_DB.sql; run the merged file for initial setup.
USE [AgriSustain];
GO
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
IF DB_NAME() <> N'AgriSustain' THROW 51110, 'Select AgriSustain.', 1;
IF TRY_CONVERT(BIT, SESSION_CONTEXT(N'AgriSustain.SeedDemo')) = 0
BEGIN
    COMMIT TRANSACTION;
    PRINT N'Demo seed disabled; schema only.';
    RETURN;
END;
DECLARE @SeedLock INT;
EXEC @SeedLock = sys.sp_getapplock @Resource=N'AgriSustain:demo:20261022', @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
IF @SeedLock < 0 THROW 51111, 'Could not acquire demo seed lock.', 1;
IF OBJECT_ID(N'dbo.__AgriSustainSeedHistory', N'U') IS NULL
    CREATE TABLE dbo.__AgriSustainSeedHistory (SeedName VARCHAR(100) PRIMARY KEY, AppliedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME());
IF EXISTS (SELECT 1 FROM dbo.__AgriSustainSeedHistory WHERE SeedName='demo-20261022-v1')
BEGIN
    COMMIT TRANSACTION;
    PRINT N'Demo seed already applied; no rows changed.';
    RETURN;
END;
IF EXISTS (SELECT 1 FROM dbo.[User] WHERE Username IN ('admin','guest_user','verification_officer','content_moderator','customer_support','promotion_staff','finance_staff','payment_gateway','gmail_service','farmer1','farmer2','farmer3','farmer4','farmer5','wholesale1','wholesale2','wholesale3','wholesale4','wholesale5','retail1','retail2','retail3','retail4','retail5'))
    THROW 51112, 'Demo usernames already exist without a seed marker. Existing accounts were not overwritten.', 1;
DECLARE @Now DATETIME2 = SYSUTCDATETIME();
DECLARE @DefaultPwdHash NVARCHAR(500) = '$2a$10$YakKiuFdNqaE8p6lcWbonuoYKv8eqBc5UcPVH4P2rlTuQgbd8hZbW';
IF NOT EXISTS (SELECT 1 FROM dbo.BusinessPolicy WHERE PolicyId = 1)
INSERT dbo.[BusinessPolicy] ([PolicyId], [PayoutDelayHours])
VALUES (1, 48);
DECLARE @Map_User TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'admin', N'admin@agrisustain.vn', @DefaultPwdHash, N'Quản Trị Viên Hệ Thống', '0901000001', N'https://cdn.agrisustain.vn/avatars/admin.png', 'SystemAdmin', 'Active', 1, 0);
INSERT @Map_User VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'guest_user', N'guest@agrisustain.vn', @DefaultPwdHash, N'Tài Khoản Khách Vãng Lai', '0901000002', N'https://cdn.agrisustain.vn/avatars/guest.png', 'Guest', 'Active', 1, 0);
INSERT @Map_User VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'verification_officer', N'verify@agrisustain.vn', @DefaultPwdHash, N'Chuyên Viên Thẩm Định', '0901000003', N'https://cdn.agrisustain.vn/avatars/verify.png', 'VerificationOfficer', 'Active', 1, 0);
INSERT @Map_User VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'content_moderator', N'moderator@agrisustain.vn', @DefaultPwdHash, N'Kiểm Duyệt Viên Nội Dung', '0901000004', N'https://cdn.agrisustain.vn/avatars/mod.png', 'ContentModerator', 'Active', 1, 0);
INSERT @Map_User VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'customer_support', N'support@agrisustain.vn', @DefaultPwdHash, N'Nhân Viên Hỗ Trợ CSKH', '0901000005', N'https://cdn.agrisustain.vn/avatars/support.png', 'CustomerSupport', 'Active', 1, 0);
INSERT @Map_User VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'promotion_staff', N'promo@agrisustain.vn', @DefaultPwdHash, N'Nhân Viên Khuyến Mãi', '0901000006', N'https://cdn.agrisustain.vn/avatars/promo.png', 'PromotionStaff', 'Active', 1, 0);
INSERT @Map_User VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'finance_staff', N'finance@agrisustain.vn', @DefaultPwdHash, N'Nhân Viên Tài Chính Kế Toán', '0901000007', N'https://cdn.agrisustain.vn/avatars/finance.png', 'FinanceStaff', 'Active', 1, 0);
INSERT @Map_User VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'payment_gateway', N'gateway@agrisustain.vn', @DefaultPwdHash, N'Cổng Thanh Toán Tự Động', '0901000008', N'https://cdn.agrisustain.vn/avatars/gateway.png', 'Payment Gateway', 'Active', 1, 0);
INSERT @Map_User VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'gmail_service', N'gmail.api@agrisustain.vn', @DefaultPwdHash, N'Dịch Vụ Thông Báo Gmail API', '0901000009', N'https://cdn.agrisustain.vn/avatars/gmail.png', 'Gmail Notification Service API', 'Active', 1, 0);
INSERT @Map_User VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'farmer1', N'farmer1@agrisustain.vn', @DefaultPwdHash, N'Nguyễn Văn Nông (Đà Lạt)', '0912000001', N'https://cdn.agrisustain.vn/avatars/f1.png', 'Farmer', 'Active', 1, 0);
INSERT @Map_User VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'farmer2', N'farmer2@agrisustain.vn', @DefaultPwdHash, N'Trần Thị Mùa (Tiền Giang)', '0912000002', N'https://cdn.agrisustain.vn/avatars/f2.png', 'Farmer', 'Active', 1, 0);
INSERT @Map_User VALUES (11, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'farmer3', N'farmer3@agrisustain.vn', @DefaultPwdHash, N'Lê Hoàng Lúa (An Giang)', '0912000003', N'https://cdn.agrisustain.vn/avatars/f3.png', 'Farmer', 'Active', 1, 0);
INSERT @Map_User VALUES (12, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'farmer4', N'farmer4@agrisustain.vn', @DefaultPwdHash, N'Phạm Văn Cam (Hưng Yên)', '0912000004', N'https://cdn.agrisustain.vn/avatars/f4.png', 'Farmer', 'Active', 1, 0);
INSERT @Map_User VALUES (13, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'farmer5', N'farmer5@agrisustain.vn', @DefaultPwdHash, N'Hoàng Minh Cà Phê (Đắk Lắk)', '0912000005', N'https://cdn.agrisustain.vn/avatars/f5.png', 'Farmer', 'Active', 1, 0);
INSERT @Map_User VALUES (14, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'wholesale1', N'wholesale1@agrisustain.vn', @DefaultPwdHash, N'Công Ty Thực Phẩm Xanh Á Châu', '0923000001', N'https://cdn.agrisustain.vn/avatars/w1.png', 'WholesaleBuyer', 'Active', 1, 0);
INSERT @Map_User VALUES (15, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'wholesale2', N'wholesale2@agrisustain.vn', @DefaultPwdHash, N'Hệ Thống Siêu Thị Nông Sản Việt', '0923000002', N'https://cdn.agrisustain.vn/avatars/w2.png', 'WholesaleBuyer', 'Active', 1, 0);
INSERT @Map_User VALUES (16, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'wholesale3', N'wholesale3@agrisustain.vn', @DefaultPwdHash, N'Chuỗi Cửa Hàng Organic Mart', '0923000003', N'https://cdn.agrisustain.vn/avatars/w3.png', 'WholesaleBuyer', 'Active', 1, 0);
INSERT @Map_User VALUES (17, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'wholesale4', N'wholesale4@agrisustain.vn', @DefaultPwdHash, N'Tập Đoàn Xuất Khẩu Nông Lâm', '0923000004', N'https://cdn.agrisustain.vn/avatars/w4.png', 'WholesaleBuyer', 'Active', 1, 0);
INSERT @Map_User VALUES (18, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'wholesale5', N'wholesale5@agrisustain.vn', @DefaultPwdHash, N'Công Ty Chế Biến Trái Cây Miền Nam', '0923000005', N'https://cdn.agrisustain.vn/avatars/w5.png', 'WholesaleBuyer', 'Active', 1, 0);
INSERT @Map_User VALUES (19, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'retail1', N'retail1@agrisustain.vn', @DefaultPwdHash, N'Đỗ Mai Phương', '0934000001', N'https://cdn.agrisustain.vn/avatars/r1.png', 'RetailCustomer', 'Active', 1, 0);
INSERT @Map_User VALUES (20, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'retail2', N'retail2@agrisustain.vn', @DefaultPwdHash, N'Vũ Quốc Tuấn', '0934000002', N'https://cdn.agrisustain.vn/avatars/r2.png', 'RetailCustomer', 'Active', 1, 0);
INSERT @Map_User VALUES (21, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'retail3', N'retail3@agrisustain.vn', @DefaultPwdHash, N'Ngô Thanh Hà', '0934000003', N'https://cdn.agrisustain.vn/avatars/r3.png', 'RetailCustomer', 'Active', 1, 0);
INSERT @Map_User VALUES (22, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'retail4', N'retail4@agrisustain.vn', @DefaultPwdHash, N'Bùi Anh Đức', '0934000004', N'https://cdn.agrisustain.vn/avatars/r4.png', 'RetailCustomer', 'Active', 1, 0);
INSERT @Map_User VALUES (23, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[User] ([Username], [Email], [PasswordHash], [FullName], [PhoneNumber], [AvatarUrl], [Role], [Status], [IsEmailVerified], [FailedLoginCount])
VALUES (N'retail5', N'retail5@agrisustain.vn', @DefaultPwdHash, N'Đặng Thu Thảo', '0934000005', N'https://cdn.agrisustain.vn/avatars/r5.png', 'RetailCustomer', 'Active', 1, 0);
INSERT @Map_User VALUES (24, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_FarmerProfile TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[FarmerProfile] ([UserId], [FarmName], [RepresentativeName], [ProductionScale], [BusinessLicenseNumber], [Address], [ProvinceCode], [CultivationZone], [GPSCoordinates], [VerificationStatus], [ApprovedAt], [ReputationScore])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 10), N'Nông Trại Rau Sạch Đà Lạt Gap', N'Nguyễn Văn Nông', N'15 Hecta nhà màng', 'BL-DL-001', N'Xã Xuân Thọ, TP. Đà Lạt, Lâm Đồng', 'LD-67', N'Vùng chuyên canh rau công nghệ cao', '11.9404,108.4583', 'Approved', @Now, 4.85);
INSERT @Map_FarmerProfile VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmerProfile] ([UserId], [FarmName], [RepresentativeName], [ProductionScale], [BusinessLicenseNumber], [Address], [ProvinceCode], [CultivationZone], [GPSCoordinates], [VerificationStatus], [ApprovedAt], [ReputationScore])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 11), N'Hợp Tác Xã Trái Cây Cái Bè', N'Trần Thị Mùa', N'30 Hecta vườn cây ăn trái', 'BL-TG-002', N'Huyện Cái Bè, Tiền Giang', 'TG-82', N'Vùng cây ăn trái Đồng Bằng Sông Cửu Long', '10.3340,106.0260', 'Approved', @Now, 4.90);
INSERT @Map_FarmerProfile VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmerProfile] ([UserId], [FarmName], [RepresentativeName], [ProductionScale], [BusinessLicenseNumber], [Address], [ProvinceCode], [CultivationZone], [GPSCoordinates], [VerificationStatus], [ApprovedAt], [ReputationScore])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 12), N'Cánh Đồng Lúa Sinh Thái An Giang', N'Lê Hoàng Lúa', N'50 Hecta lúa hữu cơ', 'BL-AG-003', N'Huyện Thoại Sơn, An Giang', 'AG-89', N'Vùng lúa chất lượng cao Tứ Giác Long Xuyên', '10.2580,105.2670', 'Approved', @Now, 4.75);
INSERT @Map_FarmerProfile VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmerProfile] ([UserId], [FarmName], [RepresentativeName], [ProductionScale], [BusinessLicenseNumber], [Address], [ProvinceCode], [CultivationZone], [GPSCoordinates], [VerificationStatus], [ApprovedAt], [ReputationScore])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 13), N'Vườn Cây Ăn Quả Đặc Sản Hưng Yên', N'Phạm Văn Cam', N'12 Hecta nhãn và cam', 'BL-HY-004', N'Huyện Khoái Châu, Hưng Yên', 'HY-33', N'Vùng bãi bồi ven sông Hồng', '20.8200,105.9700', 'Approved', @Now, 4.80);
INSERT @Map_FarmerProfile VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmerProfile] ([UserId], [FarmName], [RepresentativeName], [ProductionScale], [BusinessLicenseNumber], [Address], [ProvinceCode], [CultivationZone], [GPSCoordinates], [VerificationStatus], [ApprovedAt], [ReputationScore])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 14), N'Nông Trại Cà Phê Bền Vững Buôn Ma Thuột', N'Hoàng Minh Cà Phê', N'25 Hecta cà phê xen canh hồ tiêu', 'BL-DLK-005', N'Huyện Cư Mgar, Đắk Lắk', 'DLK-66', N'Cao nguyên đất đỏ Bazan', '12.8000,108.0800', 'Approved', @Now, 4.95);
INSERT @Map_FarmerProfile VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_WholesaleBuyerProfile TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[WholesaleBuyerProfile] ([UserId], [CompanyName], [TaxCode], [RepresentativeName], [BusinessType], [BusinessAddress], [DocumentUrl], [VerificationStatus], [ApprovedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 15), N'Công Ty Thực Phẩm Xanh Á Châu', '0101234561', N'Lê Quản Lý 1', N'Phân phối thực phẩm', N'KCN Tân Bình, TP. Hồ Chí Minh', N'https://cdn.agrisustain.vn/docs/wb1.pdf', 'Approved', @Now);
INSERT @Map_WholesaleBuyerProfile VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[WholesaleBuyerProfile] ([UserId], [CompanyName], [TaxCode], [RepresentativeName], [BusinessType], [BusinessAddress], [DocumentUrl], [VerificationStatus], [ApprovedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 16), N'Hệ Thống Siêu Thị Nông Sản Việt', '0101234562', N'Lê Quản Lý 2', N'Chuỗi siêu thị bán lẻ', N'Quận Cầu Giấy, TP. Hà Nội', N'https://cdn.agrisustain.vn/docs/wb2.pdf', 'Approved', @Now);
INSERT @Map_WholesaleBuyerProfile VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[WholesaleBuyerProfile] ([UserId], [CompanyName], [TaxCode], [RepresentativeName], [BusinessType], [BusinessAddress], [DocumentUrl], [VerificationStatus], [ApprovedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 17), N'Chuỗi Cửa Hàng Organic Mart', '0101234563', N'Lê Quản Lý 3', N'Cửa hàng thực phẩm hữu cơ', N'Quận Hải Châu, TP. Đà Nẵng', N'https://cdn.agrisustain.vn/docs/wb3.pdf', 'Approved', @Now);
INSERT @Map_WholesaleBuyerProfile VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[WholesaleBuyerProfile] ([UserId], [CompanyName], [TaxCode], [RepresentativeName], [BusinessType], [BusinessAddress], [DocumentUrl], [VerificationStatus], [ApprovedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 18), N'Tập Đoàn Xuất Khẩu Nông Lâm', '0101234564', N'Lê Quản Lý 4', N'Xuất nhập khẩu nông sản', N'Quận 1, TP. Hồ Chí Minh', N'https://cdn.agrisustain.vn/docs/wb4.pdf', 'Approved', @Now);
INSERT @Map_WholesaleBuyerProfile VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[WholesaleBuyerProfile] ([UserId], [CompanyName], [TaxCode], [RepresentativeName], [BusinessType], [BusinessAddress], [DocumentUrl], [VerificationStatus], [ApprovedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 19), N'Công Ty Chế Biến Trái Cây Miền Nam', '0101234565', N'Lê Quản Lý 5', N'Nhà máy chế biến nông sản', N'KCN Sóng Thần, Bình Dương', N'https://cdn.agrisustain.vn/docs/wb5.pdf', 'Approved', @Now);
INSERT @Map_WholesaleBuyerProfile VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_DeliveryAddress TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[DeliveryAddress] ([UserId], [RecipientName], [RecipientPhone], [AddressDetail], [ProvinceCity], [District], [Ward], [ProvinceCode], [WardCode], [AddressCatalogVersion], [IsDefault], [Status])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), N'Đỗ Mai Phương', '0934000001', N'Số 12 Nguyễn Trãi', N'Hà Nội', N'Quận Thanh Xuân', N'Phường Thượng Đình', 'HN-01', 'W-001', '2026.1', 1, 'Active');
INSERT @Map_DeliveryAddress VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DeliveryAddress] ([UserId], [RecipientName], [RecipientPhone], [AddressDetail], [ProvinceCity], [District], [Ward], [ProvinceCode], [WardCode], [AddressCatalogVersion], [IsDefault], [Status])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), N'Vũ Quốc Tuấn', '0934000002', N'Số 45 Lê Lợi', N'TP. Hồ Chí Minh', N'Quận 1', N'Phường Bến Nghé', 'HCM-79', 'W-002', '2026.1', 1, 'Active');
INSERT @Map_DeliveryAddress VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DeliveryAddress] ([UserId], [RecipientName], [RecipientPhone], [AddressDetail], [ProvinceCity], [District], [Ward], [ProvinceCode], [WardCode], [AddressCatalogVersion], [IsDefault], [Status])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 22), N'Ngô Thanh Hà', '0934000003', N'Số 88 Nguyễn Văn Linh', N'Đà Nẵng', N'Quận Hải Châu', N'Phường Nam Dương', 'DN-48', 'W-003', '2026.1', 1, 'Active');
INSERT @Map_DeliveryAddress VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DeliveryAddress] ([UserId], [RecipientName], [RecipientPhone], [AddressDetail], [ProvinceCity], [District], [Ward], [ProvinceCode], [WardCode], [AddressCatalogVersion], [IsDefault], [Status])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 23), N'Bùi Anh Đức', '0934000004', N'Số 102 Lạch Tray', N'Hải Phòng', N'Quận Ngô Quyền', N'Phường Lạch Tray', 'HP-31', 'W-004', '2026.1', 1, 'Active');
INSERT @Map_DeliveryAddress VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DeliveryAddress] ([UserId], [RecipientName], [RecipientPhone], [AddressDetail], [ProvinceCity], [District], [Ward], [ProvinceCode], [WardCode], [AddressCatalogVersion], [IsDefault], [Status])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 24), N'Đặng Thu Thảo', '0934000005', N'Số 19 Đại lộ Hòa Bình', N'Cần Thơ', N'Quận Ninh Kiều', N'Phường Tân An', 'CT-92', 'W-005', '2026.1', 1, 'Active');
INSERT @Map_DeliveryAddress VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_B2BSubscriptionPackage TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[B2BSubscriptionPackage] ([PackageName], [PackageDescription], [DurationDays], [Price], [UnlocksFarmerDirectContact], [CanDownloadContractTemplate], [IsActive], [TransactionLimit])
VALUES (N'Gói Khởi Đầu B2B (1 Tháng)', N'Dành cho doanh nghiệp nhỏ thử nghiệm kết nối nông trại', 30, 1000000, 1, 0, 1, 10);
INSERT @Map_B2BSubscriptionPackage VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BSubscriptionPackage] ([PackageName], [PackageDescription], [DurationDays], [Price], [UnlocksFarmerDirectContact], [CanDownloadContractTemplate], [IsActive], [TransactionLimit])
VALUES (N'Gói Tiêu Chuẩn B2B (3 Tháng)', N'Mở khóa liên hệ và tải mẫu hợp đồng bao tiêu chuẩn', 90, 2500000, 1, 1, 1, 30);
INSERT @Map_B2BSubscriptionPackage VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BSubscriptionPackage] ([PackageName], [PackageDescription], [DurationDays], [Price], [UnlocksFarmerDirectContact], [CanDownloadContractTemplate], [IsActive], [TransactionLimit])
VALUES (N'Gói Chuyên Nghiệp B2B (6 Tháng)', N'Hỗ trợ ký kết hợp đồng bao tiêu quy mô vừa', 180, 4500000, 1, 1, 1, 100);
INSERT @Map_B2BSubscriptionPackage VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BSubscriptionPackage] ([PackageName], [PackageDescription], [DurationDays], [Price], [UnlocksFarmerDirectContact], [CanDownloadContractTemplate], [IsActive], [TransactionLimit])
VALUES (N'Gói Doanh Nghiệp B2B (1 Năm)', N'Tối ưu chi phí cho chuỗi siêu thị và nhà phân phối', 365, 8000000, 1, 1, 1, 500);
INSERT @Map_B2BSubscriptionPackage VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BSubscriptionPackage] ([PackageName], [PackageDescription], [DurationDays], [Price], [UnlocksFarmerDirectContact], [CanDownloadContractTemplate], [IsActive], [TransactionLimit])
VALUES (N'Gói Tập Đoàn VIP (2 Năm)', N'Không giới hạn số lượng giao dịch hợp đồng bao tiêu', 730, 15000000, 1, 1, 1, NULL);
INSERT @Map_B2BSubscriptionPackage VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_B2BSubscription TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[B2BSubscription] ([WholesaleBuyerProfileId], [PackageId], [StartDate], [EndDate], [SubscriptionStatus], [AutoRenew], [PriceSnapshot], [LastRenewedAt], [UnlocksFarmerDirectContactSnapshot], [CanDownloadContractTemplateSnapshot], [TransactionLimitSnapshot], [DurationDaysSnapshot])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 1), (SELECT NewId FROM @Map_B2BSubscriptionPackage WHERE SeedId = 1), DATEADD(DAY, -10, @Now), DATEADD(DAY, 20, @Now), 'Active', 1, 1000000, @Now, 1, 0, 10, 30);
INSERT @Map_B2BSubscription VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BSubscription] ([WholesaleBuyerProfileId], [PackageId], [StartDate], [EndDate], [SubscriptionStatus], [AutoRenew], [PriceSnapshot], [LastRenewedAt], [UnlocksFarmerDirectContactSnapshot], [CanDownloadContractTemplateSnapshot], [TransactionLimitSnapshot], [DurationDaysSnapshot])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 2), (SELECT NewId FROM @Map_B2BSubscriptionPackage WHERE SeedId = 2), DATEADD(DAY, -15, @Now), DATEADD(DAY, 75, @Now), 'Active', 1, 2500000, @Now, 1, 1, 30, 90);
INSERT @Map_B2BSubscription VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BSubscription] ([WholesaleBuyerProfileId], [PackageId], [StartDate], [EndDate], [SubscriptionStatus], [AutoRenew], [PriceSnapshot], [LastRenewedAt], [UnlocksFarmerDirectContactSnapshot], [CanDownloadContractTemplateSnapshot], [TransactionLimitSnapshot], [DurationDaysSnapshot])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 3), (SELECT NewId FROM @Map_B2BSubscriptionPackage WHERE SeedId = 3), DATEADD(DAY, -20, @Now), DATEADD(DAY, 160, @Now), 'Active', 0, 4500000, @Now, 1, 1, 100, 180);
INSERT @Map_B2BSubscription VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BSubscription] ([WholesaleBuyerProfileId], [PackageId], [StartDate], [EndDate], [SubscriptionStatus], [AutoRenew], [PriceSnapshot], [LastRenewedAt], [UnlocksFarmerDirectContactSnapshot], [CanDownloadContractTemplateSnapshot], [TransactionLimitSnapshot], [DurationDaysSnapshot])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 4), (SELECT NewId FROM @Map_B2BSubscriptionPackage WHERE SeedId = 4), DATEADD(DAY, -30, @Now), DATEADD(DAY, 335, @Now), 'Active', 1, 8000000, @Now, 1, 1, 500, 365);
INSERT @Map_B2BSubscription VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BSubscription] ([WholesaleBuyerProfileId], [PackageId], [StartDate], [EndDate], [SubscriptionStatus], [AutoRenew], [PriceSnapshot], [LastRenewedAt], [UnlocksFarmerDirectContactSnapshot], [CanDownloadContractTemplateSnapshot], [TransactionLimitSnapshot], [DurationDaysSnapshot])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 5), (SELECT NewId FROM @Map_B2BSubscriptionPackage WHERE SeedId = 5), DATEADD(DAY, -5, @Now), DATEADD(DAY, 725, @Now), 'Active', 1, 15000000, @Now, 1, 1, NULL, 730);
INSERT @Map_B2BSubscription VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_Category TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[Category] ([ParentCategoryId], [CategoryName], [Description], [Priority], [IsActive])
VALUES (NULL, N'Rau Củ Quả Sạch', N'Các loại rau củ canh tác theo hướng hữu cơ và VietGAP', 1, 1);
INSERT @Map_Category VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Category] ([ParentCategoryId], [CategoryName], [Description], [Priority], [IsActive])
VALUES (NULL, N'Trái Cây Đặc Sản', N'Trái cây tươi theo mùa từ các vùng chuyên canh', 2, 1);
INSERT @Map_Category VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Category] ([ParentCategoryId], [CategoryName], [Description], [Priority], [IsActive])
VALUES (NULL, N'Lương Thực - Ngũ Cốc', N'Gạo sinh thái, các loại hạt và ngũ cốc nguyên cám', 3, 1);
INSERT @Map_Category VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Category] ([ParentCategoryId], [CategoryName], [Description], [Priority], [IsActive])
VALUES (NULL, N'Cây Công Nghiệp & Đồ Uống', N'Cà phê, hồ tiêu, trà và nguyên liệu pha chế', 4, 1);
INSERT @Map_Category VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Category] ([ParentCategoryId], [CategoryName], [Description], [Priority], [IsActive])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 1), N'Rau Ăn Lá Thủy Canh', N'Rau xà lách, cải bó xôi trồng nhà màng Đà Lạt', 5, 1);
INSERT @Map_Category VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_Product TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[Product] ([CategoryId], [FarmerProfileId], [ProductName], [Description], [Unit], [OriginProvince], [BaseRetailPrice], [WholesaleMinQuantity], [WholesalePrice], [ApprovalStatus], [IsPublished])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 1), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 1), N'Xà Lách Thủy Tinh Đà Lạt VietGAP', N'Xà lách giòn ngọt trồng nhà màng công nghệ Israel', N'kg', N'Lâm Đồng', 45000, 50, 32000, 'Approved', 0);
INSERT @Map_Product VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Product] ([CategoryId], [FarmerProfileId], [ProductName], [Description], [Unit], [OriginProvince], [BaseRetailPrice], [WholesaleMinQuantity], [WholesalePrice], [ApprovalStatus], [IsPublished])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 2), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 2), N'Xoài Cát Hòa Lộc Cái Bè Loại 1', N'Xoài chín cây tự nhiên, thơm ngọt đậm đà', N'kg', N'Tiền Giang', 85000, 100, 65000, 'Approved', 0);
INSERT @Map_Product VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Product] ([CategoryId], [FarmerProfileId], [ProductName], [Description], [Unit], [OriginProvince], [BaseRetailPrice], [WholesaleMinQuantity], [WholesalePrice], [ApprovalStatus], [IsPublished])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 3), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 3), N'Gạo ST25 Lúa Tôm Sinh Thái', N'Gạo dẻo thơm đạt chuẩn xuất khẩu châu Âu', N'kg', N'An Giang', 38000, 200, 28000, 'Approved', 0);
INSERT @Map_Product VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Product] ([CategoryId], [FarmerProfileId], [ProductName], [Description], [Unit], [OriginProvince], [BaseRetailPrice], [WholesaleMinQuantity], [WholesalePrice], [ApprovalStatus], [IsPublished])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 2), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 4), N'Cam Vinh Hưng Yên Canh Tác Hữu Cơ', N'Cam mọng nước, vị ngọt thanh, không thuốc bảo vệ thực vật', N'kg', N'Hưng Yên', 55000, 100, 40000, 'Approved', 0);
INSERT @Map_Product VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Product] ([CategoryId], [FarmerProfileId], [ProductName], [Description], [Unit], [OriginProvince], [BaseRetailPrice], [WholesaleMinQuantity], [WholesalePrice], [ApprovalStatus], [IsPublished])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 4), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 5), N'Cà Phê Robusta Honey Đắk Lắk', N'Hạt cà phê chín đỏ 100% sơ chế phương pháp Honey', N'kg', N'Đắk Lắk', 160000, 50, 125000, 'Approved', 0);
INSERT @Map_Product VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_ProductImage TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ProductImage] ([ProductId], [ImageUrl], [DisplayOrder], [IsPrimary], [AltText])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 1), N'https://cdn.agrisustain.vn/products/xalach-1.jpg', 1, 1, N'Ảnh chính Xà lách thủy tinh Đà Lạt');
INSERT @Map_ProductImage VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ProductImage] ([ProductId], [ImageUrl], [DisplayOrder], [IsPrimary], [AltText])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 2), N'https://cdn.agrisustain.vn/products/xoai-1.jpg', 1, 1, N'Ảnh chính Xoài Cát Hòa Lộc');
INSERT @Map_ProductImage VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ProductImage] ([ProductId], [ImageUrl], [DisplayOrder], [IsPrimary], [AltText])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 3), N'https://cdn.agrisustain.vn/products/gaost25-1.jpg', 1, 1, N'Ảnh chính Gạo ST25 sinh thái');
INSERT @Map_ProductImage VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ProductImage] ([ProductId], [ImageUrl], [DisplayOrder], [IsPrimary], [AltText])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 4), N'https://cdn.agrisustain.vn/products/cam-1.jpg', 1, 1, N'Ảnh chính Cam hữu cơ Hưng Yên');
INSERT @Map_ProductImage VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ProductImage] ([ProductId], [ImageUrl], [DisplayOrder], [IsPrimary], [AltText])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 5), N'https://cdn.agrisustain.vn/products/caphe-1.jpg', 1, 1, N'Ảnh chính Cà phê Robusta Honey');
INSERT @Map_ProductImage VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.Product SET IsPublished = 1 WHERE ProductId IN (SELECT NewId FROM @Map_Product WHERE SeedId IN (1, 2, 3, 4, 5));
DECLARE @Map_CropSeason TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[CropSeason] ([FarmerProfileId], [ProductId], [SeasonName], [CropVariety], [CultivatedArea], [PlantingDate], [EstimatedHarvestDate], [ActualHarvestDate], [EstimatedYield], [ActualYield], [YieldUnit], [Status])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 1), (SELECT NewId FROM @Map_Product WHERE SeedId = 1), N'Vụ Đông Xuân Đà Lạt 2026', N'Xà lách thủy tinh Rijk Zwaan', 5.000, '2026-01-01', '2026-12-01', '2026-06-01', 20000.000, 19500.000, N'kg', 'Growing');
INSERT @Map_CropSeason VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CropSeason] ([FarmerProfileId], [ProductId], [SeasonName], [CropVariety], [CultivatedArea], [PlantingDate], [EstimatedHarvestDate], [ActualHarvestDate], [EstimatedYield], [ActualYield], [YieldUnit], [Status])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 2), (SELECT NewId FROM @Map_Product WHERE SeedId = 2), N'Vụ Xoài Chính Vụ Tiền Giang 2026', N'Xoài Cát Hòa Lộc thuần chủng', 12.000, '2026-01-15', '2026-12-15', '2026-06-15', 35000.000, 34000.000, N'kg', 'Growing');
INSERT @Map_CropSeason VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CropSeason] ([FarmerProfileId], [ProductId], [SeasonName], [CropVariety], [CultivatedArea], [PlantingDate], [EstimatedHarvestDate], [ActualHarvestDate], [EstimatedYield], [ActualYield], [YieldUnit], [Status])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 3), (SELECT NewId FROM @Map_Product WHERE SeedId = 3), N'Vụ Lúa Đông Xuân An Giang 2026', N'Giống lúa ST25 xác nhận cấp 1', 25.000, '2026-02-01', '2026-12-20', '2026-07-01', 80000.000, 78000.000, N'kg', 'Growing');
INSERT @Map_CropSeason VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CropSeason] ([FarmerProfileId], [ProductId], [SeasonName], [CropVariety], [CultivatedArea], [PlantingDate], [EstimatedHarvestDate], [ActualHarvestDate], [EstimatedYield], [ActualYield], [YieldUnit], [Status])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 4), (SELECT NewId FROM @Map_Product WHERE SeedId = 4), N'Vụ Cam Thu Đông Hưng Yên 2026', N'Cam Vinh ghép mắt chuẩn', 8.000, '2026-02-10', '2026-12-25', '2026-07-10', 25000.000, 24500.000, N'kg', 'Growing');
INSERT @Map_CropSeason VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CropSeason] ([FarmerProfileId], [ProductId], [SeasonName], [CropVariety], [CultivatedArea], [PlantingDate], [EstimatedHarvestDate], [ActualHarvestDate], [EstimatedYield], [ActualYield], [YieldUnit], [Status])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 5), (SELECT NewId FROM @Map_Product WHERE SeedId = 5), N'Vụ Thu Hoạch Cà Phê Đắk Lắk 2026', N'Robusta TR4 chín muộn', 15.000, '2026-01-05', '2026-12-30', '2026-06-20', 40000.000, 39000.000, N'kg', 'Growing');
INSERT @Map_CropSeason VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_FarmCertificate TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[FarmCertificate] ([FarmerProfileId], [ReviewedByUserId], [CertificateType], [CertificateNumber], [IssuedBy], [IssueDate], [ExpiryDate], [DocumentUrl], [VerificationStatus], [ReviewedAt])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 3), N'VietGAP', 'VG-2026-0001', N'Trung Tâm Chất Lượng Nông Lâm Thủy Sản Vùng 3', '2025-05-01', '2028-05-01', N'https://cdn.agrisustain.vn/certs/vg1.pdf', 'Verified', @Now);
INSERT @Map_FarmCertificate VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmCertificate] ([FarmerProfileId], [ReviewedByUserId], [CertificateType], [CertificateNumber], [IssuedBy], [IssueDate], [ExpiryDate], [DocumentUrl], [VerificationStatus], [ReviewedAt])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 3), N'GlobalGAP', 'GG-2026-0002', N'Tổ Chức Chứng Nhận Quốc Tế SGS', '2025-06-01', '2028-06-01', N'https://cdn.agrisustain.vn/certs/gg2.pdf', 'Verified', @Now);
INSERT @Map_FarmCertificate VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmCertificate] ([FarmerProfileId], [ReviewedByUserId], [CertificateType], [CertificateNumber], [IssuedBy], [IssueDate], [ExpiryDate], [DocumentUrl], [VerificationStatus], [ReviewedAt])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 3), N'USDA Organic', 'US-2026-0003', N'Control Union Vietnam', '2025-07-01', '2028-07-01', N'https://cdn.agrisustain.vn/certs/uo3.pdf', 'Verified', @Now);
INSERT @Map_FarmCertificate VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmCertificate] ([FarmerProfileId], [ReviewedByUserId], [CertificateType], [CertificateNumber], [IssuedBy], [IssueDate], [ExpiryDate], [DocumentUrl], [VerificationStatus], [ReviewedAt])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 3), N'VietGAP', 'VG-2026-0004', N'Chi Cục Quản Lý Chất Lượng Nông Lâm Sản Hưng Yên', '2025-08-01', '2028-08-01', N'https://cdn.agrisustain.vn/certs/vg4.pdf', 'Verified', @Now);
INSERT @Map_FarmCertificate VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmCertificate] ([FarmerProfileId], [ReviewedByUserId], [CertificateType], [CertificateNumber], [IssuedBy], [IssueDate], [ExpiryDate], [DocumentUrl], [VerificationStatus], [ReviewedAt])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 3), N'4C & Rainforest Alliance', 'RA-2026-0005', N'Tổ Chức Chứng Nhận Cà Phê Bền Vững Quốc Tế', '2025-09-01', '2028-09-01', N'https://cdn.agrisustain.vn/certs/ra5.pdf', 'Verified', @Now);
INSERT @Map_FarmCertificate VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_PreOrderCampaign TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[PreOrderCampaign] ([ProductId], [CropSeasonId], [CampaignName], [StartDate], [EndDate], [TargetYield], [CommittedYield], [DepositPercentage], [PreOrderPrice], [Status])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 1), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 1), N'Đặt Trước Xà Lách Thủy Tinh Đầu Mùa', DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 1000.000, 0, 20.00, 40000, 'Active');
INSERT @Map_PreOrderCampaign VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PreOrderCampaign] ([ProductId], [CropSeasonId], [CampaignName], [StartDate], [EndDate], [TargetYield], [CommittedYield], [DepositPercentage], [PreOrderPrice], [Status])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 2), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 2), N'Đặt Trước Xoài Cát Hòa Lộc Xuất Khẩu', DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 2000.000, 0, 25.00, 75000, 'Active');
INSERT @Map_PreOrderCampaign VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PreOrderCampaign] ([ProductId], [CropSeasonId], [CampaignName], [StartDate], [EndDate], [TargetYield], [CommittedYield], [DepositPercentage], [PreOrderPrice], [Status])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 3), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 3), N'Đặt Trước Gạo ST25 Lúa Mới', DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 5000.000, 0, 30.00, 34000, 'Active');
INSERT @Map_PreOrderCampaign VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PreOrderCampaign] ([ProductId], [CropSeasonId], [CampaignName], [StartDate], [EndDate], [TargetYield], [CommittedYield], [DepositPercentage], [PreOrderPrice], [Status])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 4), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 4), N'Đặt Trước Cam Vinh Hưng Yên Chín Cây', DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 1500.000, 0, 20.00, 48000, 'Active');
INSERT @Map_PreOrderCampaign VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PreOrderCampaign] ([ProductId], [CropSeasonId], [CampaignName], [StartDate], [EndDate], [TargetYield], [CommittedYield], [DepositPercentage], [PreOrderPrice], [Status])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 5), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 5), N'Đặt Trước Cà Phê Robusta Honey Vụ Mới', DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 2000.000, 0, 50.00, 145000, 'Active');
INSERT @Map_PreOrderCampaign VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_HarvestBatch TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[HarvestBatch] ([ProductId], [CropSeasonId], [PreOrderCampaignId], [BatchCode], [EstimatedHarvestDate], [QualityNotes], [Status], [HarvestYield], [AvailableQuantity], [ReservedQuantity])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 1), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 1), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 1), 'BATCH-DL-001', '2026-06-01', N'Đạt chuẩn độ giòn, không dư lượng nitrat', 'Planned', 0, 0, 0);
INSERT @Map_HarvestBatch VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[HarvestBatch] ([ProductId], [CropSeasonId], [PreOrderCampaignId], [BatchCode], [EstimatedHarvestDate], [QualityNotes], [Status], [HarvestYield], [AvailableQuantity], [ReservedQuantity])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 2), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 2), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 2), 'BATCH-TG-002', '2026-06-15', N'Độ ngọt Brix > 18%, quả đều đẹp', 'Planned', 0, 0, 0);
INSERT @Map_HarvestBatch VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[HarvestBatch] ([ProductId], [CropSeasonId], [PreOrderCampaignId], [BatchCode], [EstimatedHarvestDate], [QualityNotes], [Status], [HarvestYield], [AvailableQuantity], [ReservedQuantity])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 3), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 3), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 3), 'BATCH-AG-003', '2026-07-01', N'Độ ẩm hạt 14%, tỷ lệ tấm < 5%', 'Planned', 0, 0, 0);
INSERT @Map_HarvestBatch VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[HarvestBatch] ([ProductId], [CropSeasonId], [PreOrderCampaignId], [BatchCode], [EstimatedHarvestDate], [QualityNotes], [Status], [HarvestYield], [AvailableQuantity], [ReservedQuantity])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 4), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 4), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 4), 'BATCH-HY-004', '2026-07-10', N'Quả mọng, vỏ mỏng vàng óng', 'Planned', 0, 0, 0);
INSERT @Map_HarvestBatch VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[HarvestBatch] ([ProductId], [CropSeasonId], [PreOrderCampaignId], [BatchCode], [EstimatedHarvestDate], [QualityNotes], [Status], [HarvestYield], [AvailableQuantity], [ReservedQuantity])
VALUES ((SELECT NewId FROM @Map_Product WHERE SeedId = 5), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 5), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 5), 'BATCH-DLK-005', '2026-06-20', N'Hạt sàng 18, độ ẩm 12.5%, hương trái cây chín', 'Planned', 0, 0, 0);
INSERT @Map_HarvestBatch VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET HarvestYield = 1000, AvailableQuantity = 1000, ReservedQuantity = 0, ActualHarvestDate = '2026-06-01', ExpirationDate = '2027-06-01', StockEnteredAt = @Now, Status = 'Available' WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 1);
DECLARE @Map_InventoryMovement TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [MovementType], [QuantityChange], [RequestKey], [RecordedByUserId], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 1), 'StockIn', 1000.000, 'STOCKIN-BATCH-1', (SELECT NewId FROM @Map_User WHERE SeedId = 10), N'Thu hoạch đợt 1 nhập kho', @Now);
INSERT @Map_InventoryMovement VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET HarvestYield = 1000, AvailableQuantity = 1000, ReservedQuantity = 0, ActualHarvestDate = '2026-06-15', ExpirationDate = '2027-06-15', StockEnteredAt = @Now, Status = 'Available' WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 2);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [MovementType], [QuantityChange], [RequestKey], [RecordedByUserId], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 2), 'StockIn', 1000.000, 'STOCKIN-BATCH-2', (SELECT NewId FROM @Map_User WHERE SeedId = 11), N'Thu hoạch đợt 1 nhập kho', @Now);
INSERT @Map_InventoryMovement VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET HarvestYield = 1000, AvailableQuantity = 1000, ReservedQuantity = 0, ActualHarvestDate = '2026-07-01', ExpirationDate = '2027-07-01', StockEnteredAt = @Now, Status = 'Available' WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 3);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [MovementType], [QuantityChange], [RequestKey], [RecordedByUserId], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 3), 'StockIn', 1000.000, 'STOCKIN-BATCH-3', (SELECT NewId FROM @Map_User WHERE SeedId = 12), N'Thu hoạch đợt 1 nhập kho', @Now);
INSERT @Map_InventoryMovement VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET HarvestYield = 1000, AvailableQuantity = 1000, ReservedQuantity = 0, ActualHarvestDate = '2026-07-10', ExpirationDate = '2027-07-10', StockEnteredAt = @Now, Status = 'Available' WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 4);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [MovementType], [QuantityChange], [RequestKey], [RecordedByUserId], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 4), 'StockIn', 1000.000, 'STOCKIN-BATCH-4', (SELECT NewId FROM @Map_User WHERE SeedId = 13), N'Thu hoạch đợt 1 nhập kho', @Now);
INSERT @Map_InventoryMovement VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET HarvestYield = 1000, AvailableQuantity = 1000, ReservedQuantity = 0, ActualHarvestDate = '2026-06-20', ExpirationDate = '2027-06-20', StockEnteredAt = @Now, Status = 'Available' WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 5);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [MovementType], [QuantityChange], [RequestKey], [RecordedByUserId], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 5), 'StockIn', 1000.000, 'STOCKIN-BATCH-5', (SELECT NewId FROM @Map_User WHERE SeedId = 14), N'Thu hoạch đợt 1 nhập kho', @Now);
INSERT @Map_InventoryMovement VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_CultivationLog TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[CultivationLog] ([CropSeasonId], [RecordedByUserId], [ActionDate], [ActionType], [DetailDescription], [InputMaterial], [Dosage], [PerformedBy], [ImageUrl])
VALUES ((SELECT NewId FROM @Map_CropSeason WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 10), DATEADD(DAY, -60, @Now), N'Gieo hạt & Chăm sóc dinh dưỡng', N'Pha dung dịch thủy canh chuẩn EC 1.8', N'Dinh dưỡng Hydro Umat', N'1.8 mS/cm', N'Kỹ sư Nguyễn Văn Nông', N'https://cdn.agrisustain.vn/logs/log1.jpg');
INSERT @Map_CultivationLog VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CultivationLog] ([CropSeasonId], [RecordedByUserId], [ActionDate], [ActionType], [DetailDescription], [InputMaterial], [Dosage], [PerformedBy], [ImageUrl])
VALUES ((SELECT NewId FROM @Map_CropSeason WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 11), DATEADD(DAY, -55, @Now), N'Bao trái phòng côn trùng', N'Sử dụng túi giấy chuyên dụng bao trái xoài non', N'Túi bao trái 2 lớp', N'1 túi/trái', N'Tổ kỹ thuật HTX Cái Bè', N'https://cdn.agrisustain.vn/logs/log2.jpg');
INSERT @Map_CultivationLog VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CultivationLog] ([CropSeasonId], [RecordedByUserId], [ActionDate], [ActionType], [DetailDescription], [InputMaterial], [Dosage], [PerformedBy], [ImageUrl])
VALUES ((SELECT NewId FROM @Map_CropSeason WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 12), DATEADD(DAY, -50, @Now), N'Bón phân hữu cơ vi sinh', N'Bón thúc đợt 2 giúp lúa đẻ nhánh khỏe', N'Phân hữu cơ Quế Lâm', N'300 kg/ha', N'Lê Hoàng Lúa', N'https://cdn.agrisustain.vn/logs/log3.jpg');
INSERT @Map_CultivationLog VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CultivationLog] ([CropSeasonId], [RecordedByUserId], [ActionDate], [ActionType], [DetailDescription], [InputMaterial], [Dosage], [PerformedBy], [ImageUrl])
VALUES ((SELECT NewId FROM @Map_CropSeason WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 13), DATEADD(DAY, -45, @Now), N'Tỉa cành tạo tán & Bẫy sinh học', N'Treo bẫy Pheromone dẫn dụ ruồi vàng đục quả', N'Bẫy sinh học Ento-Pro', N'40 bẫy/ha', N'Phạm Văn Cam', N'https://cdn.agrisustain.vn/logs/log4.jpg');
INSERT @Map_CultivationLog VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CultivationLog] ([CropSeasonId], [RecordedByUserId], [ActionDate], [ActionType], [DetailDescription], [InputMaterial], [Dosage], [PerformedBy], [ImageUrl])
VALUES ((SELECT NewId FROM @Map_CropSeason WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 14), DATEADD(DAY, -40, @Now), N'Tưới tiết kiệm nhỏ giọt', N'Tưới nước kết hợp chế phẩm Trichoderma bảo vệ rễ', N'Nấm đối kháng Trichoderma', N'5 kg/ha', N'Hoàng Minh Cà Phê', N'https://cdn.agrisustain.vn/logs/log5.jpg');
INSERT @Map_CultivationLog VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_TraceabilityQRCode TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[TraceabilityQRCode] ([HarvestBatchId], [QRContentString], [TargetUrl], [ScanCount], [IsRevoked])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 1), N'AGRI-QR-2026-BATCH-DL-001', N'https://agrisustain.vn/trace/BATCH-DL-001', 125, 0);
INSERT @Map_TraceabilityQRCode VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[TraceabilityQRCode] ([HarvestBatchId], [QRContentString], [TargetUrl], [ScanCount], [IsRevoked])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 2), N'AGRI-QR-2026-BATCH-TG-002', N'https://agrisustain.vn/trace/BATCH-TG-002', 240, 0);
INSERT @Map_TraceabilityQRCode VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[TraceabilityQRCode] ([HarvestBatchId], [QRContentString], [TargetUrl], [ScanCount], [IsRevoked])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 3), N'AGRI-QR-2026-BATCH-AG-003', N'https://agrisustain.vn/trace/BATCH-AG-003', 310, 0);
INSERT @Map_TraceabilityQRCode VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[TraceabilityQRCode] ([HarvestBatchId], [QRContentString], [TargetUrl], [ScanCount], [IsRevoked])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 4), N'AGRI-QR-2026-BATCH-HY-004', N'https://agrisustain.vn/trace/BATCH-HY-004', 95, 0);
INSERT @Map_TraceabilityQRCode VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[TraceabilityQRCode] ([HarvestBatchId], [QRContentString], [TargetUrl], [ScanCount], [IsRevoked])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 5), N'AGRI-QR-2026-BATCH-DLK-005', N'https://agrisustain.vn/trace/BATCH-DLK-005', 420, 0);
INSERT @Map_TraceabilityQRCode VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_RegionalPriceBenchmark TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[RegionalPriceBenchmark] ([CategoryId], [RegionName], [ProvinceName], [CommodityName], [MinPrice], [MaxPrice], [AveragePrice], [RecordedDate])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 1), N'Tây Nguyên', N'Lâm Đồng', N'Xà lách thủy tinh Đà Lạt', 30000, 50000, 40000, '2026-10-01');
INSERT @Map_RegionalPriceBenchmark VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RegionalPriceBenchmark] ([CategoryId], [RegionName], [ProvinceName], [CommodityName], [MinPrice], [MaxPrice], [AveragePrice], [RecordedDate])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 2), N'Đồng Bằng Sông Cửu Long', N'Tiền Giang', N'Xoài Cát Hòa Lộc', 60000, 90000, 75000, '2026-10-01');
INSERT @Map_RegionalPriceBenchmark VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RegionalPriceBenchmark] ([CategoryId], [RegionName], [ProvinceName], [CommodityName], [MinPrice], [MaxPrice], [AveragePrice], [RecordedDate])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 3), N'Đồng Bằng Sông Cửu Long', N'An Giang', N'Gạo ST25 Lúa Tôm', 26000, 40000, 33000, '2026-10-01');
INSERT @Map_RegionalPriceBenchmark VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RegionalPriceBenchmark] ([CategoryId], [RegionName], [ProvinceName], [CommodityName], [MinPrice], [MaxPrice], [AveragePrice], [RecordedDate])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 2), N'Đồng Bằng Sông Hồng', N'Hưng Yên', N'Cam Vinh Hưng Yên', 38000, 60000, 49000, '2026-10-01');
INSERT @Map_RegionalPriceBenchmark VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RegionalPriceBenchmark] ([CategoryId], [RegionName], [ProvinceName], [CommodityName], [MinPrice], [MaxPrice], [AveragePrice], [RecordedDate])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 4), N'Tây Nguyên', N'Đắk Lắk', N'Cà phê nhân/rang Robusta Honey', 120000, 170000, 145000, '2026-10-01');
INSERT @Map_RegionalPriceBenchmark VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_CommissionPolicy TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[CommissionPolicy] ([CategoryId], [ApplicableOrderType], [CommissionRate], [EffectiveDate], [Note], [IsActive])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 1), 'Retail', 5.00, '2026-01-01', N'Hoa hồng 5% cho mặt hàng rau củ bán lẻ', 1);
INSERT @Map_CommissionPolicy VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CommissionPolicy] ([CategoryId], [ApplicableOrderType], [CommissionRate], [EffectiveDate], [Note], [IsActive])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 2), 'Retail', 6.00, '2026-01-01', N'Hoa hồng 6% cho trái cây đặc sản bán lẻ', 1);
INSERT @Map_CommissionPolicy VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CommissionPolicy] ([CategoryId], [ApplicableOrderType], [CommissionRate], [EffectiveDate], [Note], [IsActive])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 3), 'Retail', 4.00, '2026-01-01', N'Hoa hồng 4% cho mặt hàng gạo và lương thực', 1);
INSERT @Map_CommissionPolicy VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CommissionPolicy] ([CategoryId], [ApplicableOrderType], [CommissionRate], [EffectiveDate], [Note], [IsActive])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 4), 'Wholesale', 3.00, '2026-01-01', N'Hoa hồng ưu đãi 3% cho hợp đồng sỉ cây công nghiệp', 1);
INSERT @Map_CommissionPolicy VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CommissionPolicy] ([CategoryId], [ApplicableOrderType], [CommissionRate], [EffectiveDate], [Note], [IsActive])
VALUES ((SELECT NewId FROM @Map_Category WHERE SeedId = 5), 'PreOrder', 4.50, '2026-01-01', N'Hoa hồng 4.5% cho đơn đặt trước nông sản nhà màng', 1);
INSERT @Map_CommissionPolicy VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_Cart TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[Cart] ([UserId], [UpdatedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), @Now);
INSERT @Map_Cart VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Cart] ([UserId], [UpdatedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), @Now);
INSERT @Map_Cart VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Cart] ([UserId], [UpdatedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 22), @Now);
INSERT @Map_Cart VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Cart] ([UserId], [UpdatedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 23), @Now);
INSERT @Map_Cart VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Cart] ([UserId], [UpdatedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 24), @Now);
INSERT @Map_Cart VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_CartItem TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[CartItem] ([CartId], [ProductId], [Quantity], [HarvestBatchId], [IsSelected], [AddedAt])
VALUES ((SELECT NewId FROM @Map_Cart WHERE SeedId = 1), (SELECT NewId FROM @Map_Product WHERE SeedId = 1), 2.000, (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 1), 1, @Now);
INSERT @Map_CartItem VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CartItem] ([CartId], [ProductId], [Quantity], [HarvestBatchId], [IsSelected], [AddedAt])
VALUES ((SELECT NewId FROM @Map_Cart WHERE SeedId = 2), (SELECT NewId FROM @Map_Product WHERE SeedId = 2), 3.000, (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 2), 1, @Now);
INSERT @Map_CartItem VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CartItem] ([CartId], [ProductId], [Quantity], [HarvestBatchId], [IsSelected], [AddedAt])
VALUES ((SELECT NewId FROM @Map_Cart WHERE SeedId = 3), (SELECT NewId FROM @Map_Product WHERE SeedId = 3), 10.000, (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 3), 1, @Now);
INSERT @Map_CartItem VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CartItem] ([CartId], [ProductId], [Quantity], [HarvestBatchId], [IsSelected], [AddedAt])
VALUES ((SELECT NewId FROM @Map_Cart WHERE SeedId = 4), (SELECT NewId FROM @Map_Product WHERE SeedId = 4), 5.000, (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 4), 1, @Now);
INSERT @Map_CartItem VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[CartItem] ([CartId], [ProductId], [Quantity], [HarvestBatchId], [IsSelected], [AddedAt])
VALUES ((SELECT NewId FROM @Map_Cart WHERE SeedId = 5), (SELECT NewId FROM @Map_Product WHERE SeedId = 5), 1.000, (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 5), 1, @Now);
INSERT @Map_CartItem VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_Voucher TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[Voucher] ([FarmerProfileId], [VoucherCode], [DiscountType], [DiscountValue], [MaxDiscountAmount], [MinOrderAmount], [UsageLimit], [UsedCount], [StartDate], [EndDate], [IsActive])
VALUES (NULL, 'AGRISUSTAIN10K', 'FixedAmount', 10000, 10000, 50000, 100, 0, DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 1);
INSERT @Map_Voucher VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Voucher] ([FarmerProfileId], [VoucherCode], [DiscountType], [DiscountValue], [MaxDiscountAmount], [MinOrderAmount], [UsageLimit], [UsedCount], [StartDate], [EndDate], [IsActive])
VALUES (NULL, 'AGRISUSTAIN20K', 'FixedAmount', 20000, 20000, 100000, 100, 0, DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 1);
INSERT @Map_Voucher VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Voucher] ([FarmerProfileId], [VoucherCode], [DiscountType], [DiscountValue], [MaxDiscountAmount], [MinOrderAmount], [UsageLimit], [UsedCount], [StartDate], [EndDate], [IsActive])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 1), 'DALATFARM5K', 'FixedAmount', 5000, 5000, 40000, 50, 0, DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 1);
INSERT @Map_Voucher VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Voucher] ([FarmerProfileId], [VoucherCode], [DiscountType], [DiscountValue], [MaxDiscountAmount], [MinOrderAmount], [UsageLimit], [UsedCount], [StartDate], [EndDate], [IsActive])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 2), 'CAIBEFARM15K', 'FixedAmount', 15000, 15000, 80000, 50, 0, DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 1);
INSERT @Map_Voucher VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Voucher] ([FarmerProfileId], [VoucherCode], [DiscountType], [DiscountValue], [MaxDiscountAmount], [MinOrderAmount], [UsageLimit], [UsedCount], [StartDate], [EndDate], [IsActive])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 3), 'ST25RICE10P', 'Percentage', 10.00, 30000, 100000, 50, 0, DATEADD(DAY, -30, @Now), DATEADD(DAY, 60, @Now), 1);
INSERT @Map_Voucher VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_B2BContract TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[B2BContract] ([WholesaleBuyerProfileId], [FarmerProfileId], [ContractNumber], [DepositAmount], [PaymentTerms], [DigitalSignatureStatus], [Status], [CurrentVersionNo], [ValidFrom], [ValidUntil])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 1), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 1), 'B2B-2026-0001', 5000000, N'Đặt cọc 5 triệu, thanh toán phần còn lại trong 7 ngày sau nghiệm thu', 'Unsigned', 'Draft', 1, '2026-01-01', '2026-12-31');
INSERT @Map_B2BContract VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BContract] ([WholesaleBuyerProfileId], [FarmerProfileId], [ContractNumber], [DepositAmount], [PaymentTerms], [DigitalSignatureStatus], [Status], [CurrentVersionNo], [ValidFrom], [ValidUntil])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 2), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 2), 'B2B-2026-0002', 5000000, N'Đặt cọc 5 triệu, thanh toán ngay khi nhận hàng tại kho siêu thị', 'Unsigned', 'Draft', 1, '2026-01-01', '2026-12-31');
INSERT @Map_B2BContract VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BContract] ([WholesaleBuyerProfileId], [FarmerProfileId], [ContractNumber], [DepositAmount], [PaymentTerms], [DigitalSignatureStatus], [Status], [CurrentVersionNo], [ValidFrom], [ValidUntil])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 3), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 3), 'B2B-2026-0003', 5000000, N'Đặt cọc 5 triệu, quyết toán theo từng đợt giao gạo', 'Unsigned', 'Draft', 1, '2026-01-01', '2026-12-31');
INSERT @Map_B2BContract VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BContract] ([WholesaleBuyerProfileId], [FarmerProfileId], [ContractNumber], [DepositAmount], [PaymentTerms], [DigitalSignatureStatus], [Status], [CurrentVersionNo], [ValidFrom], [ValidUntil])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 4), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 4), 'B2B-2026-0004', 5000000, N'Đặt cọc 5 triệu, thanh toán qua chuyển khoản doanh nghiệp', 'Unsigned', 'Draft', 1, '2026-01-01', '2026-12-31');
INSERT @Map_B2BContract VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[B2BContract] ([WholesaleBuyerProfileId], [FarmerProfileId], [ContractNumber], [DepositAmount], [PaymentTerms], [DigitalSignatureStatus], [Status], [CurrentVersionNo], [ValidFrom], [ValidUntil])
VALUES ((SELECT NewId FROM @Map_WholesaleBuyerProfile WHERE SeedId = 5), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 5), 'B2B-2026-0005', 5000000, N'Đặt cọc 5 triệu, nghiệm thu chất lượng hạt tại nhà máy', 'Unsigned', 'Draft', 1, '2026-01-01', '2026-12-31');
INSERT @Map_B2BContract VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_ContractVersion TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ContractVersion] ([B2BContractId], [ProposedByUserId], [VersionNo], [TermsAndConditions], [VersionStatus])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 15), 1, N'Điều khoản bao tiêu 500kg Xà lách thủy tinh Đà Lạt đạt chuẩn VietGAP.', 'Pending');
INSERT @Map_ContractVersion VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractVersion] ([B2BContractId], [ProposedByUserId], [VersionNo], [TermsAndConditions], [VersionStatus])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 16), 1, N'Điều khoản bao tiêu 500kg Xoài Cát Hòa Lộc loại 1 đạt chuẩn GlobalGAP.', 'Pending');
INSERT @Map_ContractVersion VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractVersion] ([B2BContractId], [ProposedByUserId], [VersionNo], [TermsAndConditions], [VersionStatus])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 17), 1, N'Điều khoản bao tiêu 500kg Gạo ST25 Lúa Tôm đạt chuẩn hữu cơ.', 'Pending');
INSERT @Map_ContractVersion VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractVersion] ([B2BContractId], [ProposedByUserId], [VersionNo], [TermsAndConditions], [VersionStatus])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 18), 1, N'Điều khoản bao tiêu 500kg Cam Vinh Hưng Yên canh tác hữu cơ.', 'Pending');
INSERT @Map_ContractVersion VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractVersion] ([B2BContractId], [ProposedByUserId], [VersionNo], [TermsAndConditions], [VersionStatus])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 19), 1, N'Điều khoản bao tiêu 500kg Cà phê Robusta Honey Đắk Lắk.', 'Pending');
INSERT @Map_ContractVersion VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_ContractLine TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ContractLine] ([B2BContractId], [ContractVersionId], [ProductId], [CropSeasonId], [CommittedQuantity], [UnitSnapshot], [QualitySpecification], [PriceType], [AgreedPrice])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 1), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 1), (SELECT NewId FROM @Map_Product WHERE SeedId = 1), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 1), 500.000, N'kg', N'Xà lách tươi nguyên cây, đóng thùng lạnh 10kg', 'Fixed', 32000);
INSERT @Map_ContractLine VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractLine] ([B2BContractId], [ContractVersionId], [ProductId], [CropSeasonId], [CommittedQuantity], [UnitSnapshot], [QualitySpecification], [PriceType], [AgreedPrice])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 2), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 2), (SELECT NewId FROM @Map_Product WHERE SeedId = 2), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 2), 500.000, N'kg', N'Xoài loại 1 từ 350g-450g/quả, không dập xước', 'Fixed', 65000);
INSERT @Map_ContractLine VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractLine] ([B2BContractId], [ContractVersionId], [ProductId], [CropSeasonId], [CommittedQuantity], [UnitSnapshot], [QualitySpecification], [PriceType], [AgreedPrice])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 3), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 3), (SELECT NewId FROM @Map_Product WHERE SeedId = 3), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 3), 500.000, N'kg', N'Gạo đóng bao 25kg, hút chân không', 'Fixed', 28000);
INSERT @Map_ContractLine VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractLine] ([B2BContractId], [ContractVersionId], [ProductId], [CropSeasonId], [CommittedQuantity], [UnitSnapshot], [QualitySpecification], [PriceType], [AgreedPrice])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 4), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 4), (SELECT NewId FROM @Map_Product WHERE SeedId = 4), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 4), 500.000, N'kg', N'Cam tươi cắt cuống, đóng thùng carton 20kg', 'Fixed', 40000);
INSERT @Map_ContractLine VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractLine] ([B2BContractId], [ContractVersionId], [ProductId], [CropSeasonId], [CommittedQuantity], [UnitSnapshot], [QualitySpecification], [PriceType], [AgreedPrice])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 5), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 5), (SELECT NewId FROM @Map_Product WHERE SeedId = 5), (SELECT NewId FROM @Map_CropSeason WHERE SeedId = 5), 500.000, N'kg', N'Cà phê nhân/rang đóng bao đay 60kg chuẩn xuất khẩu', 'Fixed', 125000);
INSERT @Map_ContractLine VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_ContractMilestone TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ContractMilestone] ([B2BContractId], [ContractVersionId], [ContractLineId], [MilestoneTitle], [MilestoneType], [DueDate], [Amount], [DeliveryQuantity], [FulfillmentStatus], [PaymentStatus], [DeliveredQuantity], [AcceptedQuantity], [PaidAmount])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 1), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 1), (SELECT NewId FROM @Map_ContractLine WHERE SeedId = 1), N'Đợt giao hàng 1 - 100kg Xà lách', 'DeliveryAndPayment', '2026-11-15', 3200000, 100.000, 'Pending', 'Unpaid', 0, 0, 0);
INSERT @Map_ContractMilestone VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractMilestone] ([B2BContractId], [ContractVersionId], [ContractLineId], [MilestoneTitle], [MilestoneType], [DueDate], [Amount], [DeliveryQuantity], [FulfillmentStatus], [PaymentStatus], [DeliveredQuantity], [AcceptedQuantity], [PaidAmount])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 2), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 2), (SELECT NewId FROM @Map_ContractLine WHERE SeedId = 2), N'Đợt giao hàng 1 - 100kg Xoài Cát Hòa Lộc', 'DeliveryAndPayment', '2026-11-20', 6500000, 100.000, 'Pending', 'Unpaid', 0, 0, 0);
INSERT @Map_ContractMilestone VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractMilestone] ([B2BContractId], [ContractVersionId], [ContractLineId], [MilestoneTitle], [MilestoneType], [DueDate], [Amount], [DeliveryQuantity], [FulfillmentStatus], [PaymentStatus], [DeliveredQuantity], [AcceptedQuantity], [PaidAmount])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 3), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 3), (SELECT NewId FROM @Map_ContractLine WHERE SeedId = 3), N'Đợt giao hàng 1 - 100kg Gạo ST25', 'DeliveryAndPayment', '2026-11-25', 2800000, 100.000, 'Pending', 'Unpaid', 0, 0, 0);
INSERT @Map_ContractMilestone VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractMilestone] ([B2BContractId], [ContractVersionId], [ContractLineId], [MilestoneTitle], [MilestoneType], [DueDate], [Amount], [DeliveryQuantity], [FulfillmentStatus], [PaymentStatus], [DeliveredQuantity], [AcceptedQuantity], [PaidAmount])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 4), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 4), (SELECT NewId FROM @Map_ContractLine WHERE SeedId = 4), N'Đợt giao hàng 1 - 100kg Cam Vinh', 'DeliveryAndPayment', '2026-11-28', 4000000, 100.000, 'Pending', 'Unpaid', 0, 0, 0);
INSERT @Map_ContractMilestone VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractMilestone] ([B2BContractId], [ContractVersionId], [ContractLineId], [MilestoneTitle], [MilestoneType], [DueDate], [Amount], [DeliveryQuantity], [FulfillmentStatus], [PaymentStatus], [DeliveredQuantity], [AcceptedQuantity], [PaidAmount])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 5), (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 5), (SELECT NewId FROM @Map_ContractLine WHERE SeedId = 5), N'Đợt giao hàng 1 - 100kg Cà Phê Robusta', 'DeliveryAndPayment', '2026-11-30', 12500000, 100.000, 'Pending', 'Unpaid', 0, 0, 0);
INSERT @Map_ContractMilestone VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @LineJson1 NVARCHAR(MAX) = (SELECT ProductId, CommittedQuantity, AgreedPrice FROM dbo.ContractLine WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 1) ORDER BY ContractLineId FOR JSON PATH);
UPDATE dbo.ContractVersion SET LineSnapshotJson = @LineJson1, DocumentHash = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @LineJson1), 2) WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 1);
DECLARE @LineJson2 NVARCHAR(MAX) = (SELECT ProductId, CommittedQuantity, AgreedPrice FROM dbo.ContractLine WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 2) ORDER BY ContractLineId FOR JSON PATH);
UPDATE dbo.ContractVersion SET LineSnapshotJson = @LineJson2, DocumentHash = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @LineJson2), 2) WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 2);
DECLARE @LineJson3 NVARCHAR(MAX) = (SELECT ProductId, CommittedQuantity, AgreedPrice FROM dbo.ContractLine WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 3) ORDER BY ContractLineId FOR JSON PATH);
UPDATE dbo.ContractVersion SET LineSnapshotJson = @LineJson3, DocumentHash = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @LineJson3), 2) WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 3);
DECLARE @LineJson4 NVARCHAR(MAX) = (SELECT ProductId, CommittedQuantity, AgreedPrice FROM dbo.ContractLine WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 4) ORDER BY ContractLineId FOR JSON PATH);
UPDATE dbo.ContractVersion SET LineSnapshotJson = @LineJson4, DocumentHash = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @LineJson4), 2) WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 4);
DECLARE @LineJson5 NVARCHAR(MAX) = (SELECT ProductId, CommittedQuantity, AgreedPrice FROM dbo.ContractLine WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 5) ORDER BY ContractLineId FOR JSON PATH);
UPDATE dbo.ContractVersion SET LineSnapshotJson = @LineJson5, DocumentHash = CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', @LineJson5), 2) WHERE ContractVersionId = (SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 5);
DECLARE @Map_ContractSignature TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 10), 'Farmer', 'OTP_Email', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 15), 'WholesaleBuyer', 'DigitalCert', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 11), 'Farmer', 'OTP_Email', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 16), 'WholesaleBuyer', 'DigitalCert', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 12), 'Farmer', 'OTP_Email', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 17), 'WholesaleBuyer', 'DigitalCert', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 13), 'Farmer', 'OTP_Email', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 18), 'WholesaleBuyer', 'DigitalCert', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 14), 'Farmer', 'OTP_Email', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractSignature] ([ContractVersionId], [SignedByUserId], [PartyRole], [ConfirmationMethod], [ConfirmedAt], [ConfirmationStatus])
VALUES ((SELECT NewId FROM @Map_ContractVersion WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 19), 'WholesaleBuyer', 'DigitalCert', @Now, 'Confirmed');
INSERT @Map_ContractSignature VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.ContractVersion SET VersionStatus = 'Signed' WHERE ContractVersionId IN (SELECT NewId FROM @Map_ContractVersion WHERE SeedId IN (1, 2, 3, 4, 5));
UPDATE dbo.B2BContract SET DigitalSignatureStatus = 'FullySigned', SignedDate = @Now, [Status] = 'Active' WHERE B2BContractId IN (SELECT NewId FROM @Map_B2BContract WHERE SeedId IN (1, 2, 3, 4, 5));
DECLARE @Map_ContractTerminationRequest TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ContractTerminationRequest] ([B2BContractId], [RequestedByUserId], [Reason], [EvidenceUrl], [RequestedAt], [RequestStatus], [SettlementAmount], [ResolvedAt])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 15), N'Đề xuất điều chỉnh lịch giao do kho lạnh bảo trì (Đã thương lượng lại)', N'https://cdn.agrisustain.vn/term/t1.pdf', DATEADD(DAY, -3, @Now), 'Cancelled', 0, @Now);
INSERT @Map_ContractTerminationRequest VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractTerminationRequest] ([B2BContractId], [RequestedByUserId], [Reason], [EvidenceUrl], [RequestedAt], [RequestStatus], [SettlementAmount], [ResolvedAt])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 16), N'Tạm hoãn đợt giao hàng do thay đổi kế hoạch trưng bày (Đã rút yêu cầu)', N'https://cdn.agrisustain.vn/term/t2.pdf', DATEADD(DAY, -3, @Now), 'Cancelled', 0, @Now);
INSERT @Map_ContractTerminationRequest VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractTerminationRequest] ([B2BContractId], [RequestedByUserId], [Reason], [EvidenceUrl], [RequestedAt], [RequestStatus], [SettlementAmount], [ResolvedAt])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 17), N'Xin hủy sớm một phần sản lượng (Không được chấp thuận, tiếp tục hợp đồng)', N'https://cdn.agrisustain.vn/term/t3.pdf', DATEADD(DAY, -3, @Now), 'Rejected', 0, @Now);
INSERT @Map_ContractTerminationRequest VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractTerminationRequest] ([B2BContractId], [RequestedByUserId], [Reason], [EvidenceUrl], [RequestedAt], [RequestStatus], [SettlementAmount], [ResolvedAt])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 18), N'Yêu cầu thanh lý sớm do nhầm mã kho nhận (Đã rút yêu cầu)', N'https://cdn.agrisustain.vn/term/t4.pdf', DATEADD(DAY, -3, @Now), 'Cancelled', 0, @Now);
INSERT @Map_ContractTerminationRequest VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractTerminationRequest] ([B2BContractId], [RequestedByUserId], [Reason], [EvidenceUrl], [RequestedAt], [RequestStatus], [SettlementAmount], [ResolvedAt])
VALUES ((SELECT NewId FROM @Map_B2BContract WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 19), N'Đề xuất đổi sang vụ sau (Hai bên thống nhất giữ nguyên hợp đồng hiện tại)', N'https://cdn.agrisustain.vn/term/t5.pdf', DATEADD(DAY, -3, @Now), 'Rejected', 0, @Now);
INSERT @Map_ContractTerminationRequest VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Q1 UNIQUEIDENTIFIER = '11111111-1111-1111-1111-111111111111';
DECLARE @Q2 UNIQUEIDENTIFIER = '22222222-2222-2222-2222-222222222222';
DECLARE @Q3 UNIQUEIDENTIFIER = '33333333-3333-3333-3333-333333333333';
DECLARE @Q4 UNIQUEIDENTIFIER = '44444444-4444-4444-4444-444444444444';
DECLARE @Q5 UNIQUEIDENTIFIER = '55555555-5555-5555-5555-555555555555';
INSERT dbo.[RetailCheckoutQuote] ([QuoteId], [BuyerUserId], [AcceptedSnapshotJson], [Fingerprint], [CreatedAt], [ExpiresAt])
VALUES (@Q1, (SELECT NewId FROM @Map_User WHERE SeedId = 20), JSON_MODIFY(N'{"productId":1,"qty":10,"unitPrice":45000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 1)), CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', JSON_MODIFY(N'{"productId":1,"qty":10,"unitPrice":45000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 1))), 2), DATEADD(HOUR, -1, @Now), DATEADD(MINUTE, -30, @Now));
INSERT dbo.[RetailCheckoutQuote] ([QuoteId], [BuyerUserId], [AcceptedSnapshotJson], [Fingerprint], [CreatedAt], [ExpiresAt])
VALUES (@Q2, (SELECT NewId FROM @Map_User WHERE SeedId = 21), JSON_MODIFY(N'{"productId":2,"qty":10,"unitPrice":85000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 2)), CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', JSON_MODIFY(N'{"productId":2,"qty":10,"unitPrice":85000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 2))), 2), DATEADD(HOUR, -1, @Now), DATEADD(MINUTE, -30, @Now));
INSERT dbo.[RetailCheckoutQuote] ([QuoteId], [BuyerUserId], [AcceptedSnapshotJson], [Fingerprint], [CreatedAt], [ExpiresAt])
VALUES (@Q3, (SELECT NewId FROM @Map_User WHERE SeedId = 22), JSON_MODIFY(N'{"productId":3,"qty":10,"unitPrice":38000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 3)), CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', JSON_MODIFY(N'{"productId":3,"qty":10,"unitPrice":38000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 3))), 2), DATEADD(HOUR, -1, @Now), DATEADD(MINUTE, -30, @Now));
INSERT dbo.[RetailCheckoutQuote] ([QuoteId], [BuyerUserId], [AcceptedSnapshotJson], [Fingerprint], [CreatedAt], [ExpiresAt])
VALUES (@Q4, (SELECT NewId FROM @Map_User WHERE SeedId = 23), JSON_MODIFY(N'{"productId":4,"qty":10,"unitPrice":55000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 4)), CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', JSON_MODIFY(N'{"productId":4,"qty":10,"unitPrice":55000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 4))), 2), DATEADD(HOUR, -1, @Now), DATEADD(MINUTE, -30, @Now));
INSERT dbo.[RetailCheckoutQuote] ([QuoteId], [BuyerUserId], [AcceptedSnapshotJson], [Fingerprint], [CreatedAt], [ExpiresAt])
VALUES (@Q5, (SELECT NewId FROM @Map_User WHERE SeedId = 24), JSON_MODIFY(N'{"productId":5,"qty":10,"unitPrice":160000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 5)), CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', JSON_MODIFY(N'{"productId":5,"qty":10,"unitPrice":160000}', '$.productId', (SELECT NewId FROM @Map_Product WHERE SeedId = 5))), 2), DATEADD(HOUR, -1, @Now), DATEADD(MINUTE, -30, @Now));
DECLARE @Map_Order TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [RetailQuoteId], [DeliveryMethod], [PaymentMethod], [VoucherCodeSnapshot], [VoucherDiscount], [PointDiscount], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), 'ORD-RET-2026-001', 'Retail', 0, DATEADD(DAY, -10, @Now), DATEADD(DAY, -7, @Now), N'Số 12 Nguyễn Trãi, Thanh Xuân, Hà Nội', 450000, 15000, 20000, 455000, 'Pending', N'Đỗ Mai Phương', '0934000001', @Q1, 'FreshExpress', 'PayOS', 'AGRISUSTAIN10K', 10000, 5000, 50, 'CHK-RET-001', DATEADD(MINUTE, 15, DATEADD(DAY, -10, @Now)), DATEADD(DAY, -10, @Now));
INSERT @Map_Order VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [RetailQuoteId], [DeliveryMethod], [PaymentMethod], [VoucherCodeSnapshot], [VoucherDiscount], [PointDiscount], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), 'ORD-RET-2026-002', 'Retail', 0, DATEADD(DAY, -10, @Now), DATEADD(DAY, -7, @Now), N'Số 45 Lê Lợi, Quận 1, TP.HCM', 850000, 15000, 20000, 855000, 'Pending', N'Vũ Quốc Tuấn', '0934000002', @Q2, 'FreshExpress', 'PayOS', 'AGRISUSTAIN10K', 10000, 5000, 50, 'CHK-RET-002', DATEADD(MINUTE, 15, DATEADD(DAY, -10, @Now)), DATEADD(DAY, -10, @Now));
INSERT @Map_Order VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [RetailQuoteId], [DeliveryMethod], [PaymentMethod], [VoucherCodeSnapshot], [VoucherDiscount], [PointDiscount], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 22), 'ORD-RET-2026-003', 'Retail', 0, DATEADD(DAY, -10, @Now), DATEADD(DAY, -7, @Now), N'Số 88 Nguyễn Văn Linh, Hải Châu, Đà Nẵng', 380000, 15000, 20000, 385000, 'Pending', N'Ngô Thanh Hà', '0934000003', @Q3, 'Standard', 'PayOS', 'AGRISUSTAIN10K', 10000, 5000, 50, 'CHK-RET-003', DATEADD(MINUTE, 15, DATEADD(DAY, -10, @Now)), DATEADD(DAY, -10, @Now));
INSERT @Map_Order VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [RetailQuoteId], [DeliveryMethod], [PaymentMethod], [VoucherCodeSnapshot], [VoucherDiscount], [PointDiscount], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 23), 'ORD-RET-2026-004', 'Retail', 0, DATEADD(DAY, -10, @Now), DATEADD(DAY, -7, @Now), N'Số 102 Lạch Tray, Ngô Quyền, Hải Phòng', 550000, 15000, 20000, 555000, 'Pending', N'Bùi Anh Đức', '0934000004', @Q4, 'Standard', 'PayOS', 'AGRISUSTAIN10K', 10000, 5000, 50, 'CHK-RET-004', DATEADD(MINUTE, 15, DATEADD(DAY, -10, @Now)), DATEADD(DAY, -10, @Now));
INSERT @Map_Order VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [RetailQuoteId], [DeliveryMethod], [PaymentMethod], [VoucherCodeSnapshot], [VoucherDiscount], [PointDiscount], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 24), 'ORD-RET-2026-005', 'Retail', 0, DATEADD(DAY, -10, @Now), DATEADD(DAY, -7, @Now), N'Số 19 Đại lộ Hòa Bình, Ninh Kiều, Cần Thơ', 1600000, 15000, 20000, 1605000, 'Pending', N'Đặng Thu Thảo', '0934000005', @Q5, 'FreshExpress', 'PayOS', 'AGRISUSTAIN10K', 10000, 5000, 50, 'CHK-RET-005', DATEADD(MINUTE, 15, DATEADD(DAY, -10, @Now)), DATEADD(DAY, -10, @Now));
INSERT @Map_Order VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [DeliveryMethod], [PaymentMethod], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), 'ORD-PRE-2026-001', 'PreOrder', 1, DATEADD(DAY, -2, @Now), DATEADD(DAY, 30, @Now), N'Số 12 Nguyễn Trãi, Thanh Xuân, Hà Nội', 200000, 0, 15000, 215000, 'Pending', N'Đỗ Mai Phương', '0934000001', 'Standard', 'PayOS', 0, 'CHK-PRE-001', DATEADD(MINUTE, 15, DATEADD(DAY, -2, @Now)), DATEADD(DAY, -2, @Now));
INSERT @Map_Order VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [DeliveryMethod], [PaymentMethod], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), 'ORD-PRE-2026-002', 'PreOrder', 1, DATEADD(DAY, -2, @Now), DATEADD(DAY, 30, @Now), N'Số 45 Lê Lợi, Quận 1, TP.HCM', 375000, 0, 15000, 390000, 'Pending', N'Vũ Quốc Tuấn', '0934000002', 'Standard', 'PayOS', 0, 'CHK-PRE-002', DATEADD(MINUTE, 15, DATEADD(DAY, -2, @Now)), DATEADD(DAY, -2, @Now));
INSERT @Map_Order VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [DeliveryMethod], [PaymentMethod], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 22), 'ORD-PRE-2026-003', 'PreOrder', 1, DATEADD(DAY, -2, @Now), DATEADD(DAY, 30, @Now), N'Số 88 Nguyễn Văn Linh, Hải Châu, Đà Nẵng', 170000, 0, 15000, 185000, 'Pending', N'Ngô Thanh Hà', '0934000003', 'Standard', 'PayOS', 0, 'CHK-PRE-003', DATEADD(MINUTE, 15, DATEADD(DAY, -2, @Now)), DATEADD(DAY, -2, @Now));
INSERT @Map_Order VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [DeliveryMethod], [PaymentMethod], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 23), 'ORD-PRE-2026-004', 'PreOrder', 1, DATEADD(DAY, -2, @Now), DATEADD(DAY, 30, @Now), N'Số 102 Lạch Tray, Ngô Quyền, Hải Phòng', 240000, 0, 15000, 255000, 'Pending', N'Bùi Anh Đức', '0934000004', 'Standard', 'PayOS', 0, 'CHK-PRE-004', DATEADD(MINUTE, 15, DATEADD(DAY, -2, @Now)), DATEADD(DAY, -2, @Now));
INSERT @Map_Order VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Order] ([BuyerUserId], [OrderCode], [OrderType], [IsPreOrder], [OrderDate], [ExpectedDeliveryDate], [ShippingAddress], [TotalAmount], [DiscountAmount], [DeliveryFee], [FinalAmount], [OrderStatus], [RecipientNameSnapshot], [RecipientPhoneSnapshot], [DeliveryMethod], [PaymentMethod], [RedeemedPoints], [CheckoutRequestKey], [ReservationExpiresAt], [CheckoutCommittedAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 24), 'ORD-PRE-2026-005', 'PreOrder', 1, DATEADD(DAY, -2, @Now), DATEADD(DAY, 30, @Now), N'Số 19 Đại lộ Hòa Bình, Ninh Kiều, Cần Thơ', 725000, 0, 15000, 740000, 'Pending', N'Đặng Thu Thảo', '0934000005', 'Standard', 'PayOS', 0, 'CHK-PRE-005', DATEADD(MINUTE, 15, DATEADD(DAY, -2, @Now)), DATEADD(DAY, -2, @Now));
INSERT @Map_Order VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_SellerOrder TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 1), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 1), N'Nông Trại Rau Sạch Đà Lạt Gap', 'SO-RET-2026-001', 450000, 15000, 20000, 455000, 'Pending');
INSERT @Map_SellerOrder VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 2), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 2), N'Hợp Tác Xã Trái Cây Cái Bè', 'SO-RET-2026-002', 850000, 15000, 20000, 855000, 'Pending');
INSERT @Map_SellerOrder VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 3), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 3), N'Cánh Đồng Lúa Sinh Thái An Giang', 'SO-RET-2026-003', 380000, 15000, 20000, 385000, 'Pending');
INSERT @Map_SellerOrder VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 4), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 4), N'Vườn Cây Ăn Quả Đặc Sản Hưng Yên', 'SO-RET-2026-004', 550000, 15000, 20000, 555000, 'Pending');
INSERT @Map_SellerOrder VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 5), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 5), N'Nông Trại Cà Phê Bền Vững Buôn Ma Thuột', 'SO-RET-2026-005', 1600000, 15000, 20000, 1605000, 'Pending');
INSERT @Map_SellerOrder VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 6), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 1), N'Nông Trại Rau Sạch Đà Lạt Gap', 'SO-PRE-2026-001', 200000, 0, 15000, 215000, 'Pending');
INSERT @Map_SellerOrder VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 7), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 2), N'Hợp Tác Xã Trái Cây Cái Bè', 'SO-PRE-2026-002', 375000, 0, 15000, 390000, 'Pending');
INSERT @Map_SellerOrder VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 8), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 3), N'Cánh Đồng Lúa Sinh Thái An Giang', 'SO-PRE-2026-003', 170000, 0, 15000, 185000, 'Pending');
INSERT @Map_SellerOrder VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 9), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 4), N'Vườn Cây Ăn Quả Đặc Sản Hưng Yên', 'SO-PRE-2026-004', 240000, 0, 15000, 255000, 'Pending');
INSERT @Map_SellerOrder VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[SellerOrder] ([OrderId], [FarmerProfileId], [SellerNameSnapshot], [SellerOrderCode], [Subtotal], [DiscountShare], [DeliveryFee], [FinalAmount], [Status])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 10), (SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 5), N'Nông Trại Cà Phê Bền Vững Buôn Ma Thuột', 'SO-PRE-2026-005', 725000, 0, 15000, 740000, 'Pending');
INSERT @Map_SellerOrder VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_OrderItem TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 1), (SELECT NewId FROM @Map_Product WHERE SeedId = 1), NULL, N'Xà Lách Thủy Tinh Đà Lạt VietGAP', 10.000, 45000, N'kg', 0, 450000, 'Normal');
INSERT @Map_OrderItem VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 2), (SELECT NewId FROM @Map_Product WHERE SeedId = 2), NULL, N'Xoài Cát Hòa Lộc Cái Bè Loại 1', 10.000, 85000, N'kg', 0, 850000, 'Normal');
INSERT @Map_OrderItem VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 3), (SELECT NewId FROM @Map_Product WHERE SeedId = 3), NULL, N'Gạo ST25 Lúa Tôm Sinh Thái', 10.000, 38000, N'kg', 0, 380000, 'Normal');
INSERT @Map_OrderItem VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 4), (SELECT NewId FROM @Map_Product WHERE SeedId = 4), NULL, N'Cam Vinh Hưng Yên Canh Tác Hữu Cơ', 10.000, 55000, N'kg', 0, 550000, 'Normal');
INSERT @Map_OrderItem VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 5), (SELECT NewId FROM @Map_Product WHERE SeedId = 5), NULL, N'Cà Phê Robusta Honey Đắk Lắk', 10.000, 160000, N'kg', 0, 1600000, 'Normal');
INSERT @Map_OrderItem VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 6), (SELECT NewId FROM @Map_Product WHERE SeedId = 1), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 1), N'Xà Lách Thủy Tinh Đà Lạt VietGAP', 5.000, 40000, N'kg', 0, 200000, 'Normal');
INSERT @Map_OrderItem VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 7), (SELECT NewId FROM @Map_Product WHERE SeedId = 2), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 2), N'Xoài Cát Hòa Lộc Cái Bè Loại 1', 5.000, 75000, N'kg', 0, 375000, 'Normal');
INSERT @Map_OrderItem VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 8), (SELECT NewId FROM @Map_Product WHERE SeedId = 3), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 3), N'Gạo ST25 Lúa Tôm Sinh Thái', 5.000, 34000, N'kg', 0, 170000, 'Normal');
INSERT @Map_OrderItem VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 9), (SELECT NewId FROM @Map_Product WHERE SeedId = 4), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 4), N'Cam Vinh Hưng Yên Canh Tác Hữu Cơ', 5.000, 48000, N'kg', 0, 240000, 'Normal');
INSERT @Map_OrderItem VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderItem] ([SellerOrderId], [ProductId], [PreOrderCampaignId], [ProductNameSnapshot], [Quantity], [UnitPriceSnapshot], [UnitSnapshot], [DiscountApplied], [SubTotal], [ItemStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 10), (SELECT NewId FROM @Map_Product WHERE SeedId = 5), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 5), N'Cà Phê Robusta Honey Đắk Lắk', 5.000, 145000, N'kg', 0, 725000, 'Normal');
INSERT @Map_OrderItem VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_RewardPointEntry TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), NULL, 'Earn', 500, 'REWARD-EARN-U20', @Now);
INSERT @Map_RewardPointEntry VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), NULL, 'Earn', 500, 'REWARD-EARN-U21', @Now);
INSERT @Map_RewardPointEntry VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 22), NULL, 'Earn', 500, 'REWARD-EARN-U22', @Now);
INSERT @Map_RewardPointEntry VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 23), NULL, 'Earn', 500, 'REWARD-EARN-U23', @Now);
INSERT @Map_RewardPointEntry VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 24), NULL, 'Earn', 500, 'REWARD-EARN-U24', @Now);
INSERT @Map_RewardPointEntry VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), (SELECT NewId FROM @Map_Order WHERE SeedId = 1), 'Hold', 50, 'REWARD-HOLD-O1', @Now);
INSERT @Map_RewardPointEntry VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), (SELECT NewId FROM @Map_Order WHERE SeedId = 2), 'Hold', 50, 'REWARD-HOLD-O2', @Now);
INSERT @Map_RewardPointEntry VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 22), (SELECT NewId FROM @Map_Order WHERE SeedId = 3), 'Hold', 50, 'REWARD-HOLD-O3', @Now);
INSERT @Map_RewardPointEntry VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 23), (SELECT NewId FROM @Map_Order WHERE SeedId = 4), 'Hold', 50, 'REWARD-HOLD-O4', @Now);
INSERT @Map_RewardPointEntry VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 24), (SELECT NewId FROM @Map_Order WHERE SeedId = 5), 'Hold', 50, 'REWARD-HOLD-O5', @Now);
INSERT @Map_RewardPointEntry VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_VoucherRedemption TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[VoucherRedemption] ([VoucherId], [OrderId], [SellerOrderId], [DiscountSnapshot], [Status], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_Voucher WHERE SeedId = 1), (SELECT NewId FROM @Map_Order WHERE SeedId = 1), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 1), 10000, 'Reserved', @Now, DATEADD(DAY, 2, @Now));
INSERT @Map_VoucherRedemption VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[VoucherRedemption] ([VoucherId], [OrderId], [SellerOrderId], [DiscountSnapshot], [Status], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_Voucher WHERE SeedId = 1), (SELECT NewId FROM @Map_Order WHERE SeedId = 2), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 2), 10000, 'Reserved', @Now, DATEADD(DAY, 2, @Now));
INSERT @Map_VoucherRedemption VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[VoucherRedemption] ([VoucherId], [OrderId], [SellerOrderId], [DiscountSnapshot], [Status], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_Voucher WHERE SeedId = 1), (SELECT NewId FROM @Map_Order WHERE SeedId = 3), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 3), 10000, 'Reserved', @Now, DATEADD(DAY, 2, @Now));
INSERT @Map_VoucherRedemption VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[VoucherRedemption] ([VoucherId], [OrderId], [SellerOrderId], [DiscountSnapshot], [Status], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_Voucher WHERE SeedId = 1), (SELECT NewId FROM @Map_Order WHERE SeedId = 4), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 4), 10000, 'Reserved', @Now, DATEADD(DAY, 2, @Now));
INSERT @Map_VoucherRedemption VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[VoucherRedemption] ([VoucherId], [OrderId], [SellerOrderId], [DiscountSnapshot], [Status], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_Voucher WHERE SeedId = 1), (SELECT NewId FROM @Map_Order WHERE SeedId = 5), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 5), 10000, 'Reserved', @Now, DATEADD(DAY, 2, @Now));
INSERT @Map_VoucherRedemption VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.VoucherRedemption SET [Status] = 'Applied', AppliedAt = @Now WHERE VoucherRedemptionId IN (SELECT NewId FROM @Map_VoucherRedemption WHERE SeedId IN (1, 2, 3, 4, 5));
UPDATE dbo.Voucher SET UsedCount = UsedCount + 5 WHERE VoucherId = (SELECT NewId FROM @Map_Voucher WHERE SeedId = 1);
DECLARE @HoldExpiry DATETIME2 = DATEADD(DAY, 5, @Now);
DECLARE @Map_OrderItemBatchAllocation TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[OrderItemBatchAllocation] ([OrderItemId], [HarvestBatchId], [AllocationSequence], [AllocatedQuantity], [AllocationStatus], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 1), (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 1), 1, 10.000, 'Reserved', @Now, @HoldExpiry);
INSERT @Map_OrderItemBatchAllocation VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET AvailableQuantity = AvailableQuantity - 10.000, ReservedQuantity = ReservedQuantity + 10.000 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 1);
INSERT dbo.[OrderItemBatchAllocation] ([OrderItemId], [HarvestBatchId], [AllocationSequence], [AllocatedQuantity], [AllocationStatus], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 2), (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 2), 1, 10.000, 'Reserved', @Now, @HoldExpiry);
INSERT @Map_OrderItemBatchAllocation VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET AvailableQuantity = AvailableQuantity - 10.000, ReservedQuantity = ReservedQuantity + 10.000 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 2);
INSERT dbo.[OrderItemBatchAllocation] ([OrderItemId], [HarvestBatchId], [AllocationSequence], [AllocatedQuantity], [AllocationStatus], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 3), (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 3), 1, 10.000, 'Reserved', @Now, @HoldExpiry);
INSERT @Map_OrderItemBatchAllocation VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET AvailableQuantity = AvailableQuantity - 10.000, ReservedQuantity = ReservedQuantity + 10.000 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 3);
INSERT dbo.[OrderItemBatchAllocation] ([OrderItemId], [HarvestBatchId], [AllocationSequence], [AllocatedQuantity], [AllocationStatus], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 4), (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 4), 1, 10.000, 'Reserved', @Now, @HoldExpiry);
INSERT @Map_OrderItemBatchAllocation VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET AvailableQuantity = AvailableQuantity - 10.000, ReservedQuantity = ReservedQuantity + 10.000 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 4);
INSERT dbo.[OrderItemBatchAllocation] ([OrderItemId], [HarvestBatchId], [AllocationSequence], [AllocatedQuantity], [AllocationStatus], [ReservedAt], [ExpiresAt])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 5), (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 5), 1, 10.000, 'Reserved', @Now, @HoldExpiry);
INSERT @Map_OrderItemBatchAllocation VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.HarvestBatch SET AvailableQuantity = AvailableQuantity - 10.000, ReservedQuantity = ReservedQuantity + 10.000 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 5);
DECLARE @Map_PreOrderReservation TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[PreOrderReservation] ([OrderItemId], [PreOrderCampaignId], [Quantity], [ReservationStatus], [ExpiresAt], [CreatedAt], [PriceSnapshot], [DepositPercentageSnapshot], [UnitSnapshot], [EstimatedHarvestDateSnapshot], [RequestKey])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 6), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 1), 5, 'Reserved', @HoldExpiry, @Now, 40000, 20, N'kg', CONVERT(DATE, DATEADD(DAY, 30, @Now)), 'PRE-HOLD-OI6');
INSERT @Map_PreOrderReservation VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PreOrderCampaign SET CommittedYield = CommittedYield + 5 WHERE PreOrderCampaignId = (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 1);
INSERT dbo.[PreOrderReservation] ([OrderItemId], [PreOrderCampaignId], [Quantity], [ReservationStatus], [ExpiresAt], [CreatedAt], [PriceSnapshot], [DepositPercentageSnapshot], [UnitSnapshot], [EstimatedHarvestDateSnapshot], [RequestKey])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 7), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 2), 5, 'Reserved', @HoldExpiry, @Now, 75000, 25, N'kg', CONVERT(DATE, DATEADD(DAY, 30, @Now)), 'PRE-HOLD-OI7');
INSERT @Map_PreOrderReservation VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PreOrderCampaign SET CommittedYield = CommittedYield + 5 WHERE PreOrderCampaignId = (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 2);
INSERT dbo.[PreOrderReservation] ([OrderItemId], [PreOrderCampaignId], [Quantity], [ReservationStatus], [ExpiresAt], [CreatedAt], [PriceSnapshot], [DepositPercentageSnapshot], [UnitSnapshot], [EstimatedHarvestDateSnapshot], [RequestKey])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 8), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 3), 5, 'Reserved', @HoldExpiry, @Now, 34000, 30, N'kg', CONVERT(DATE, DATEADD(DAY, 30, @Now)), 'PRE-HOLD-OI8');
INSERT @Map_PreOrderReservation VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PreOrderCampaign SET CommittedYield = CommittedYield + 5 WHERE PreOrderCampaignId = (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 3);
INSERT dbo.[PreOrderReservation] ([OrderItemId], [PreOrderCampaignId], [Quantity], [ReservationStatus], [ExpiresAt], [CreatedAt], [PriceSnapshot], [DepositPercentageSnapshot], [UnitSnapshot], [EstimatedHarvestDateSnapshot], [RequestKey])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 9), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 4), 5, 'Reserved', @HoldExpiry, @Now, 48000, 20, N'kg', CONVERT(DATE, DATEADD(DAY, 30, @Now)), 'PRE-HOLD-OI9');
INSERT @Map_PreOrderReservation VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PreOrderCampaign SET CommittedYield = CommittedYield + 5 WHERE PreOrderCampaignId = (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 4);
INSERT dbo.[PreOrderReservation] ([OrderItemId], [PreOrderCampaignId], [Quantity], [ReservationStatus], [ExpiresAt], [CreatedAt], [PriceSnapshot], [DepositPercentageSnapshot], [UnitSnapshot], [EstimatedHarvestDateSnapshot], [RequestKey])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 10), (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 5), 5, 'Reserved', @HoldExpiry, @Now, 145000, 50, N'kg', CONVERT(DATE, DATEADD(DAY, 30, @Now)), 'PRE-HOLD-OI10');
INSERT @Map_PreOrderReservation VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PreOrderCampaign SET CommittedYield = CommittedYield + 5 WHERE PreOrderCampaignId = (SELECT NewId FROM @Map_PreOrderCampaign WHERE SeedId = 5);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 1);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 2);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 3);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 4);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 5);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 6);
UPDATE dbo.SellerOrder SET Status = 'Confirmed' WHERE SellerOrderId = (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 6);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 7);
UPDATE dbo.SellerOrder SET Status = 'Confirmed' WHERE SellerOrderId = (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 7);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 8);
UPDATE dbo.SellerOrder SET Status = 'Confirmed' WHERE SellerOrderId = (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 8);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 9);
UPDATE dbo.SellerOrder SET Status = 'Confirmed' WHERE SellerOrderId = (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 9);
UPDATE dbo.[Order] SET OrderStatus = CASE WHEN IsPreOrder = 1 THEN 'Confirmed' ELSE 'PendingPayment' END WHERE OrderId = (SELECT NewId FROM @Map_Order WHERE SeedId = 10);
UPDATE dbo.SellerOrder SET Status = 'Confirmed' WHERE SellerOrderId = (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 10);
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), (SELECT NewId FROM @Map_Order WHERE SeedId = 1), 'Consume', 50, 'REWARD-CONSUME-O1', @Now);
INSERT @Map_RewardPointEntry VALUES (11, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), (SELECT NewId FROM @Map_Order WHERE SeedId = 2), 'Consume', 50, 'REWARD-CONSUME-O2', @Now);
INSERT @Map_RewardPointEntry VALUES (12, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 22), (SELECT NewId FROM @Map_Order WHERE SeedId = 3), 'Consume', 50, 'REWARD-CONSUME-O3', @Now);
INSERT @Map_RewardPointEntry VALUES (13, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 23), (SELECT NewId FROM @Map_Order WHERE SeedId = 4), 'Consume', 50, 'REWARD-CONSUME-O4', @Now);
INSERT @Map_RewardPointEntry VALUES (14, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RewardPointEntry] ([UserId], [OrderId], [EntryType], [Points], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 24), (SELECT NewId FROM @Map_Order WHERE SeedId = 5), 'Consume', 50, 'REWARD-CONSUME-O5', @Now);
INSERT @Map_RewardPointEntry VALUES (15, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.OrderItemBatchAllocation SET AllocationStatus = 'Dispatched' WHERE OrderItemBatchAllocationId = (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 1);
UPDATE dbo.HarvestBatch SET ReservedQuantity = ReservedQuantity - 10 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 1);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [OrderItemBatchAllocationId], [MovementType], [QuantityChange], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 1), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 1), 'Dispatch', -10, 'DEMO-DISPATCH-1', @Now);
INSERT @Map_InventoryMovement VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.OrderItemBatchAllocation SET AllocationStatus = 'Dispatched' WHERE OrderItemBatchAllocationId = (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 2);
UPDATE dbo.HarvestBatch SET ReservedQuantity = ReservedQuantity - 10 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 2);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [OrderItemBatchAllocationId], [MovementType], [QuantityChange], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 2), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 2), 'Dispatch', -10, 'DEMO-DISPATCH-2', @Now);
INSERT @Map_InventoryMovement VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.OrderItemBatchAllocation SET AllocationStatus = 'Dispatched' WHERE OrderItemBatchAllocationId = (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 3);
UPDATE dbo.HarvestBatch SET ReservedQuantity = ReservedQuantity - 10 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 3);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [OrderItemBatchAllocationId], [MovementType], [QuantityChange], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 3), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 3), 'Dispatch', -10, 'DEMO-DISPATCH-3', @Now);
INSERT @Map_InventoryMovement VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.OrderItemBatchAllocation SET AllocationStatus = 'Dispatched' WHERE OrderItemBatchAllocationId = (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 4);
UPDATE dbo.HarvestBatch SET ReservedQuantity = ReservedQuantity - 10 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 4);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [OrderItemBatchAllocationId], [MovementType], [QuantityChange], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 4), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 4), 'Dispatch', -10, 'DEMO-DISPATCH-4', @Now);
INSERT @Map_InventoryMovement VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.OrderItemBatchAllocation SET AllocationStatus = 'Dispatched' WHERE OrderItemBatchAllocationId = (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 5);
UPDATE dbo.HarvestBatch SET ReservedQuantity = ReservedQuantity - 10 WHERE HarvestBatchId = (SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 5);
INSERT dbo.[InventoryMovement] ([HarvestBatchId], [OrderItemBatchAllocationId], [MovementType], [QuantityChange], [RequestKey], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_HarvestBatch WHERE SeedId = 5), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 5), 'Dispatch', -10, 'DEMO-DISPATCH-5', @Now);
INSERT @Map_InventoryMovement VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @DeliveredTime DATETIME2 = DATEADD(DAY, -5, @Now);
DECLARE @Map_Shipment TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[Shipment] ([SellerOrderId], [TrackingNumber], [PlannedDeliveryAt], [ShippedAt], [DeliveredAt], [ReceivedAt], [ShipmentStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 1), 'TRK-VN-2026-0001', DATEADD(DAY, -7, @Now), DATEADD(DAY, -6, @Now), @DeliveredTime, @DeliveredTime, 'Preparing');
INSERT @Map_Shipment VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Shipment] ([SellerOrderId], [TrackingNumber], [PlannedDeliveryAt], [ShippedAt], [DeliveredAt], [ReceivedAt], [ShipmentStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 2), 'TRK-VN-2026-0002', DATEADD(DAY, -7, @Now), DATEADD(DAY, -6, @Now), @DeliveredTime, @DeliveredTime, 'Preparing');
INSERT @Map_Shipment VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Shipment] ([SellerOrderId], [TrackingNumber], [PlannedDeliveryAt], [ShippedAt], [DeliveredAt], [ReceivedAt], [ShipmentStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 3), 'TRK-VN-2026-0003', DATEADD(DAY, -7, @Now), DATEADD(DAY, -6, @Now), @DeliveredTime, @DeliveredTime, 'Preparing');
INSERT @Map_Shipment VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Shipment] ([SellerOrderId], [TrackingNumber], [PlannedDeliveryAt], [ShippedAt], [DeliveredAt], [ReceivedAt], [ShipmentStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 4), 'TRK-VN-2026-0004', DATEADD(DAY, -7, @Now), DATEADD(DAY, -6, @Now), @DeliveredTime, @DeliveredTime, 'Preparing');
INSERT @Map_Shipment VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Shipment] ([SellerOrderId], [TrackingNumber], [PlannedDeliveryAt], [ShippedAt], [DeliveredAt], [ReceivedAt], [ShipmentStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 5), 'TRK-VN-2026-0005', DATEADD(DAY, -7, @Now), DATEADD(DAY, -6, @Now), @DeliveredTime, @DeliveredTime, 'Preparing');
INSERT @Map_Shipment VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_ShipmentItem TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ShipmentItem] ([ShipmentId], [OrderItemBatchAllocationId], [ShippedQuantity], [ReceivedQuantity], [AcceptedQuantity], [QualityResult], [ReconciliationStatus], [ReconciledAt])
VALUES ((SELECT NewId FROM @Map_Shipment WHERE SeedId = 1), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 1), 10.000, 10.000, 10.000, N'Hàng tươi nguyên vẹn 100%', 'Reconciled', @DeliveredTime);
INSERT @Map_ShipmentItem VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ShipmentItem] ([ShipmentId], [OrderItemBatchAllocationId], [ShippedQuantity], [ReceivedQuantity], [AcceptedQuantity], [QualityResult], [ReconciliationStatus], [ReconciledAt])
VALUES ((SELECT NewId FROM @Map_Shipment WHERE SeedId = 2), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 2), 10.000, 10.000, 10.000, N'Xoài chín đều, không dập', 'Reconciled', @DeliveredTime);
INSERT @Map_ShipmentItem VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ShipmentItem] ([ShipmentId], [OrderItemBatchAllocationId], [ShippedQuantity], [ReceivedQuantity], [AcceptedQuantity], [QualityResult], [ReconciliationStatus], [ReconciledAt])
VALUES ((SELECT NewId FROM @Map_Shipment WHERE SeedId = 3), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 3), 10.000, 10.000, 10.000, N'Bao gạo nguyên niêm phong', 'Reconciled', @DeliveredTime);
INSERT @Map_ShipmentItem VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ShipmentItem] ([ShipmentId], [OrderItemBatchAllocationId], [ShippedQuantity], [ReceivedQuantity], [AcceptedQuantity], [QualityResult], [ReconciliationStatus], [ReconciledAt])
VALUES ((SELECT NewId FROM @Map_Shipment WHERE SeedId = 4), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 4), 10.000, 10.000, 10.000, N'Cam tươi mọng nước đạt chuẩn', 'Reconciled', @DeliveredTime);
INSERT @Map_ShipmentItem VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ShipmentItem] ([ShipmentId], [OrderItemBatchAllocationId], [ShippedQuantity], [ReceivedQuantity], [AcceptedQuantity], [QualityResult], [ReconciliationStatus], [ReconciledAt])
VALUES ((SELECT NewId FROM @Map_Shipment WHERE SeedId = 5), (SELECT NewId FROM @Map_OrderItemBatchAllocation WHERE SeedId = 5), 10.000, 10.000, 10.000, N'Cà phê thơm đúng mô tả', 'Reconciled', @DeliveredTime);
INSERT @Map_ShipmentItem VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.Shipment SET ShipmentStatus = 'Completed' WHERE ShipmentId IN (SELECT NewId FROM @Map_Shipment WHERE SeedId IN (1, 2, 3, 4, 5));
UPDATE dbo.SellerOrder SET [Status] = 'Completed', DeliveredAt = @DeliveredTime, ReceivedAt = @DeliveredTime WHERE SellerOrderId IN (SELECT NewId FROM @Map_SellerOrder WHERE SeedId IN (1, 2, 3, 4, 5));
UPDATE dbo.[Order] SET OrderStatus = 'Completed', CompletedAt = @DeliveredTime WHERE OrderId IN (SELECT NewId FROM @Map_Order WHERE SeedId IN (1, 2, 3, 4, 5));
DECLARE @Map_OrderStatusHistory TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[OrderStatusHistory] ([SellerOrderId], [ChangedByUserId], [OldStatus], [NewStatus], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 10), 'Pending', 'Completed', N'Khách hàng đã nhận và nghiệm thu đơn hàng #1', @DeliveredTime);
INSERT @Map_OrderStatusHistory VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderStatusHistory] ([SellerOrderId], [ChangedByUserId], [OldStatus], [NewStatus], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 11), 'Pending', 'Completed', N'Khách hàng đã nhận và nghiệm thu đơn hàng #2', @DeliveredTime);
INSERT @Map_OrderStatusHistory VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderStatusHistory] ([SellerOrderId], [ChangedByUserId], [OldStatus], [NewStatus], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 12), 'Pending', 'Completed', N'Khách hàng đã nhận và nghiệm thu đơn hàng #3', @DeliveredTime);
INSERT @Map_OrderStatusHistory VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderStatusHistory] ([SellerOrderId], [ChangedByUserId], [OldStatus], [NewStatus], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 13), 'Pending', 'Completed', N'Khách hàng đã nhận và nghiệm thu đơn hàng #4', @DeliveredTime);
INSERT @Map_OrderStatusHistory VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[OrderStatusHistory] ([SellerOrderId], [ChangedByUserId], [OldStatus], [NewStatus], [Reason], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 14), 'Pending', 'Completed', N'Khách hàng đã nhận và nghiệm thu đơn hàng #5', @DeliveredTime);
INSERT @Map_OrderStatusHistory VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_ProductReview TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ProductReview] ([OrderItemId], [UserId], [RatingStars], [Comment], [ReviewDate], [IsPurchasedVerified], [ModerationStatus])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 20), 5, N'Xà lách thủy tinh Đà Lạt rất giòn và tươi, đóng gói cẩn thận!', @Now, 1, 'Approved');
INSERT @Map_ProductReview VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ProductReview] ([OrderItemId], [UserId], [RatingStars], [Comment], [ReviewDate], [IsPurchasedVerified], [ModerationStatus])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 21), 5, N'Xoài Cát Hòa Lộc ngọt lịm, hương thơm đặc trưng chuẩn Cái Bè.', @Now, 1, 'Approved');
INSERT @Map_ProductReview VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ProductReview] ([OrderItemId], [UserId], [RatingStars], [Comment], [ReviewDate], [IsPurchasedVerified], [ModerationStatus])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 22), 5, N'Gạo ST25 nấu cơm rất dẻo và thơm, cả gia đình đều thích.', @Now, 1, 'Approved');
INSERT @Map_ProductReview VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ProductReview] ([OrderItemId], [UserId], [RatingStars], [Comment], [ReviewDate], [IsPurchasedVerified], [ModerationStatus])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 23), 4, N'Cam Vinh Hưng Yên mọng nước, vị ngọt thanh rất ngon.', @Now, 1, 'Approved');
INSERT @Map_ProductReview VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ProductReview] ([OrderItemId], [UserId], [RatingStars], [Comment], [ReviewDate], [IsPurchasedVerified], [ModerationStatus])
VALUES ((SELECT NewId FROM @Map_OrderItem WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 24), 5, N'Cà phê Robusta Honey rang mộc thơm nồng, hậu vị ngọt sâu.', @Now, 1, 'Approved');
INSERT @Map_ProductReview VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_Invoice TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[Invoice] ([SellerOrderId], [InvoiceNumber], [IssuedAt], [DueAt], [TotalAmount], [InvoiceStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 1), 'INV-RET-2026-0001', DATEADD(DAY, -6, @Now), DATEADD(DAY, 1, @Now), 450000, 'Draft');
INSERT @Map_Invoice VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Invoice] ([SellerOrderId], [InvoiceNumber], [IssuedAt], [DueAt], [TotalAmount], [InvoiceStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 2), 'INV-RET-2026-0002', DATEADD(DAY, -6, @Now), DATEADD(DAY, 1, @Now), 850000, 'Draft');
INSERT @Map_Invoice VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Invoice] ([SellerOrderId], [InvoiceNumber], [IssuedAt], [DueAt], [TotalAmount], [InvoiceStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 3), 'INV-RET-2026-0003', DATEADD(DAY, -6, @Now), DATEADD(DAY, 1, @Now), 380000, 'Draft');
INSERT @Map_Invoice VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Invoice] ([SellerOrderId], [InvoiceNumber], [IssuedAt], [DueAt], [TotalAmount], [InvoiceStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 4), 'INV-RET-2026-0004', DATEADD(DAY, -6, @Now), DATEADD(DAY, 1, @Now), 550000, 'Draft');
INSERT @Map_Invoice VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Invoice] ([SellerOrderId], [InvoiceNumber], [IssuedAt], [DueAt], [TotalAmount], [InvoiceStatus])
VALUES ((SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 5), 'INV-RET-2026-0005', DATEADD(DAY, -6, @Now), DATEADD(DAY, 1, @Now), 1600000, 'Draft');
INSERT @Map_Invoice VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_InvoiceLine TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[InvoiceLine] ([InvoiceId], [OrderItemId], [Quantity], [UnitPriceSnapshot], [LineAmount])
VALUES ((SELECT NewId FROM @Map_Invoice WHERE SeedId = 1), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 1), 10.000, 45000, 450000);
INSERT @Map_InvoiceLine VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[InvoiceLine] ([InvoiceId], [OrderItemId], [Quantity], [UnitPriceSnapshot], [LineAmount])
VALUES ((SELECT NewId FROM @Map_Invoice WHERE SeedId = 2), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 2), 10.000, 85000, 850000);
INSERT @Map_InvoiceLine VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[InvoiceLine] ([InvoiceId], [OrderItemId], [Quantity], [UnitPriceSnapshot], [LineAmount])
VALUES ((SELECT NewId FROM @Map_Invoice WHERE SeedId = 3), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 3), 10.000, 38000, 380000);
INSERT @Map_InvoiceLine VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[InvoiceLine] ([InvoiceId], [OrderItemId], [Quantity], [UnitPriceSnapshot], [LineAmount])
VALUES ((SELECT NewId FROM @Map_Invoice WHERE SeedId = 4), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 4), 10.000, 55000, 550000);
INSERT @Map_InvoiceLine VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[InvoiceLine] ([InvoiceId], [OrderItemId], [Quantity], [UnitPriceSnapshot], [LineAmount])
VALUES ((SELECT NewId FROM @Map_Invoice WHERE SeedId = 5), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 5), 10.000, 160000, 1600000);
INSERT @Map_InvoiceLine VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.Invoice SET InvoiceStatus = 'Issued' WHERE InvoiceId IN (SELECT NewId FROM @Map_Invoice WHERE SeedId IN (1, 2, 3, 4, 5));
DECLARE @Map_PaymentTransaction TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 1), NULL, 'PAY-RET-2026-001', 'VNPay', 455000, 'OnlineBanking', 'Pending', 'OrderPayment', 'REQ-PAY-RET-001');
INSERT @Map_PaymentTransaction VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 2), NULL, 'PAY-RET-2026-002', 'VNPay', 855000, 'OnlineBanking', 'Pending', 'OrderPayment', 'REQ-PAY-RET-002');
INSERT @Map_PaymentTransaction VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 3), NULL, 'PAY-RET-2026-003', 'MoMo', 385000, 'EWallet', 'Pending', 'OrderPayment', 'REQ-PAY-RET-003');
INSERT @Map_PaymentTransaction VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 4), NULL, 'PAY-RET-2026-004', 'VNPay', 555000, 'OnlineBanking', 'Pending', 'OrderPayment', 'REQ-PAY-RET-004');
INSERT @Map_PaymentTransaction VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 5), NULL, 'PAY-RET-2026-005', 'MoMo', 1605000, 'EWallet', 'Pending', 'OrderPayment', 'REQ-PAY-RET-005');
INSERT @Map_PaymentTransaction VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 6), NULL, 'PAY-PRE-2026-001', 'VNPay', 100000, 'OnlineBanking', 'Pending', 'OrderPayment', 'REQ-PAY-PRE-001');
INSERT @Map_PaymentTransaction VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 7), NULL, 'PAY-PRE-2026-002', 'VNPay', 150000, 'OnlineBanking', 'Pending', 'OrderPayment', 'REQ-PAY-PRE-002');
INSERT @Map_PaymentTransaction VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 8), NULL, 'PAY-PRE-2026-003', 'MoMo', 100000, 'EWallet', 'Pending', 'OrderPayment', 'REQ-PAY-PRE-003');
INSERT @Map_PaymentTransaction VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 9), NULL, 'PAY-PRE-2026-004', 'VNPay', 100000, 'OnlineBanking', 'Pending', 'OrderPayment', 'REQ-PAY-PRE-004');
INSERT @Map_PaymentTransaction VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES ((SELECT NewId FROM @Map_Order WHERE SeedId = 10), NULL, 'PAY-PRE-2026-005', 'MoMo', 400000, 'EWallet', 'Pending', 'OrderPayment', 'REQ-PAY-PRE-005');
INSERT @Map_PaymentTransaction VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES (NULL, (SELECT NewId FROM @Map_B2BContract WHERE SeedId = 1), 'PAY-B2B-2026-001', 'VNPay', 5000000, 'BankTransfer', 'Pending', 'ContractDeposit', 'REQ-PAY-B2B-001');
INSERT @Map_PaymentTransaction VALUES (11, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES (NULL, (SELECT NewId FROM @Map_B2BContract WHERE SeedId = 2), 'PAY-B2B-2026-002', 'VNPay', 5000000, 'BankTransfer', 'Pending', 'ContractDeposit', 'REQ-PAY-B2B-002');
INSERT @Map_PaymentTransaction VALUES (12, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES (NULL, (SELECT NewId FROM @Map_B2BContract WHERE SeedId = 3), 'PAY-B2B-2026-003', 'VNPay', 5000000, 'BankTransfer', 'Pending', 'ContractDeposit', 'REQ-PAY-B2B-003');
INSERT @Map_PaymentTransaction VALUES (13, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES (NULL, (SELECT NewId FROM @Map_B2BContract WHERE SeedId = 4), 'PAY-B2B-2026-004', 'VNPay', 5000000, 'BankTransfer', 'Pending', 'ContractDeposit', 'REQ-PAY-B2B-004');
INSERT @Map_PaymentTransaction VALUES (14, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentTransaction] ([OrderId], [B2BContractId], [TransactionCode], [GatewayProvider], [Amount], [PaymentType], [PaymentStatus], [PaymentPurpose], [RequestKey])
VALUES (NULL, (SELECT NewId FROM @Map_B2BContract WHERE SeedId = 5), 'PAY-B2B-2026-005', 'VNPay', 5000000, 'BankTransfer', 'Pending', 'ContractDeposit', 'REQ-PAY-B2B-005');
INSERT @Map_PaymentTransaction VALUES (15, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Hash64 CHAR(64) = REPLICATE('9', 64);
DECLARE @Map_PaymentGatewayEvent TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-RET-001', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 1), 'VNP-REF-RET-001', 455000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-RET-001', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 1);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-RET-002', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 2), 'VNP-REF-RET-002', 855000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-RET-002', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 2);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('MoMo', 'EVT-RET-003', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 3), 'MOMO-REF-RET-003', 385000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'MOMO-REF-RET-003', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 3);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-RET-004', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 4), 'VNP-REF-RET-004', 555000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-RET-004', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 4);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('MoMo', 'EVT-RET-005', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 5), 'MOMO-REF-RET-005', 1605000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'MOMO-REF-RET-005', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 5);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-PRE-001', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 6), 'VNP-REF-PRE-001', 100000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (6, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-PRE-001', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 6);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-PRE-002', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 7), 'VNP-REF-PRE-002', 150000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (7, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-PRE-002', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 7);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('MoMo', 'EVT-PRE-003', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 8), 'MOMO-REF-PRE-003', 100000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (8, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'MOMO-REF-PRE-003', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 8);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-PRE-004', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 9), 'VNP-REF-PRE-004', 100000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (9, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-PRE-004', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 9);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('MoMo', 'EVT-PRE-005', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 10), 'MOMO-REF-PRE-005', 400000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (10, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'MOMO-REF-PRE-005', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 10);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-B2B-001', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 11), 'VNP-REF-B2B-001', 5000000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (11, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-B2B-001', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 11);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-B2B-002', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 12), 'VNP-REF-B2B-002', 5000000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (12, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-B2B-002', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 12);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-B2B-003', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 13), 'VNP-REF-B2B-003', 5000000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (13, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-B2B-003', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 13);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-B2B-004', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 14), 'VNP-REF-B2B-004', 5000000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (14, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-B2B-004', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 14);
INSERT dbo.[PaymentGatewayEvent] ([GatewayProvider], [ProviderEventId], [PaymentTransactionId], [ProviderTransactionReference], [Amount], [EventStatus], [PayloadHash], [ReceivedAt])
VALUES ('VNPay', 'EVT-B2B-005', (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 15), 'VNP-REF-B2B-005', 5000000, 'Succeeded', @Hash64, @Now);
INSERT @Map_PaymentGatewayEvent VALUES (15, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.PaymentTransaction SET PaymentStatus = 'Succeeded', ProviderTransactionReference = 'VNP-REF-B2B-005', PaidAt = @Now WHERE PaymentTransactionId = (SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 15);
UPDATE dbo.PreOrderReservation SET ReservationStatus = 'Confirmed' WHERE PreOrderReservationId = (SELECT NewId FROM @Map_PreOrderReservation WHERE SeedId = 1);
UPDATE dbo.PreOrderReservation SET ReservationStatus = 'Confirmed' WHERE PreOrderReservationId = (SELECT NewId FROM @Map_PreOrderReservation WHERE SeedId = 2);
UPDATE dbo.PreOrderReservation SET ReservationStatus = 'Confirmed' WHERE PreOrderReservationId = (SELECT NewId FROM @Map_PreOrderReservation WHERE SeedId = 3);
UPDATE dbo.PreOrderReservation SET ReservationStatus = 'Confirmed' WHERE PreOrderReservationId = (SELECT NewId FROM @Map_PreOrderReservation WHERE SeedId = 4);
UPDATE dbo.PreOrderReservation SET ReservationStatus = 'Confirmed' WHERE PreOrderReservationId = (SELECT NewId FROM @Map_PreOrderReservation WHERE SeedId = 5);
DECLARE @Map_PaymentInvoiceAllocation TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[PaymentInvoiceAllocation] ([PaymentTransactionId], [InvoiceId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 1), (SELECT NewId FROM @Map_Invoice WHERE SeedId = 1), 450000, 'ALLOC-INV-001');
INSERT @Map_PaymentInvoiceAllocation VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentInvoiceAllocation] ([PaymentTransactionId], [InvoiceId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 2), (SELECT NewId FROM @Map_Invoice WHERE SeedId = 2), 850000, 'ALLOC-INV-002');
INSERT @Map_PaymentInvoiceAllocation VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentInvoiceAllocation] ([PaymentTransactionId], [InvoiceId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 3), (SELECT NewId FROM @Map_Invoice WHERE SeedId = 3), 380000, 'ALLOC-INV-003');
INSERT @Map_PaymentInvoiceAllocation VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentInvoiceAllocation] ([PaymentTransactionId], [InvoiceId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 4), (SELECT NewId FROM @Map_Invoice WHERE SeedId = 4), 550000, 'ALLOC-INV-004');
INSERT @Map_PaymentInvoiceAllocation VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PaymentInvoiceAllocation] ([PaymentTransactionId], [InvoiceId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 5), (SELECT NewId FROM @Map_Invoice WHERE SeedId = 5), 1600000, 'ALLOC-INV-005');
INSERT @Map_PaymentInvoiceAllocation VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.Invoice SET InvoiceStatus = 'Paid' WHERE InvoiceId IN (SELECT NewId FROM @Map_Invoice WHERE SeedId IN (1, 2, 3, 4, 5));
DECLARE @Map_ContractDepositAllocation TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[ContractDepositAllocation] ([PaymentTransactionId], [ContractMilestoneId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 11), (SELECT NewId FROM @Map_ContractMilestone WHERE SeedId = 1), 2000000, 'DEP-ALLOC-M1');
INSERT @Map_ContractDepositAllocation VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractDepositAllocation] ([PaymentTransactionId], [ContractMilestoneId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 12), (SELECT NewId FROM @Map_ContractMilestone WHERE SeedId = 2), 2000000, 'DEP-ALLOC-M2');
INSERT @Map_ContractDepositAllocation VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractDepositAllocation] ([PaymentTransactionId], [ContractMilestoneId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 13), (SELECT NewId FROM @Map_ContractMilestone WHERE SeedId = 3), 2000000, 'DEP-ALLOC-M3');
INSERT @Map_ContractDepositAllocation VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractDepositAllocation] ([PaymentTransactionId], [ContractMilestoneId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 14), (SELECT NewId FROM @Map_ContractMilestone WHERE SeedId = 4), 2000000, 'DEP-ALLOC-M4');
INSERT @Map_ContractDepositAllocation VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[ContractDepositAllocation] ([PaymentTransactionId], [ContractMilestoneId], [Amount], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 15), (SELECT NewId FROM @Map_ContractMilestone WHERE SeedId = 5), 2000000, 'DEP-ALLOC-M5');
INSERT @Map_ContractDepositAllocation VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_DisputeTicket TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[DisputeTicket] ([SubmittedByUserId], [AssignedStaffUserId], [SellerOrderId], [B2BContractId], [DisputeReason], [Description], [TicketStatus], [FaultParty], [RefundAmount], [ResolutionNotes], [ResolvedAt], [DecisionAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), (SELECT NewId FROM @Map_User WHERE SeedId = 5), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 1), NULL, N'Hỗ trợ hoàn tiền chênh lệch khuyến mãi nhỏ', N'Khách phản ánh cần hỗ trợ hoàn 20,000đ phí vận chuyển/khuyến mãi', 'Resolved', 'Platform', 20000, N'Đã đồng ý hoàn 20,000đ cho khách hàng', @Now, @Now);
INSERT @Map_DisputeTicket VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DisputeTicket] ([SubmittedByUserId], [AssignedStaffUserId], [SellerOrderId], [B2BContractId], [DisputeReason], [Description], [TicketStatus], [FaultParty], [RefundAmount], [ResolutionNotes], [ResolvedAt], [DecisionAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), (SELECT NewId FROM @Map_User WHERE SeedId = 5), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 2), NULL, N'Trái xoài hơi xanh hơn dự kiến', N'Khách muốn chín sẵn để ăn ngay, đã hướng dẫn ủ chín tự nhiên', 'Resolved', 'None', 20000, N'Hỗ trợ thiện chí 20,000đ cho khách', @Now, @Now);
INSERT @Map_DisputeTicket VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DisputeTicket] ([SubmittedByUserId], [AssignedStaffUserId], [SellerOrderId], [B2BContractId], [DisputeReason], [Description], [TicketStatus], [FaultParty], [RefundAmount], [ResolutionNotes], [ResolvedAt], [DecisionAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 22), (SELECT NewId FROM @Map_User WHERE SeedId = 5), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 3), NULL, N'Giao hàng trễ 1 tiếng so với hẹn', N'Shipper kẹt xe giờ cao điểm tại Đà Nẵng', 'Resolved', 'Carrier', 20000, N'Hoàn 20,000đ phí dịch vụ giao hàng', @Now, @Now);
INSERT @Map_DisputeTicket VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DisputeTicket] ([SubmittedByUserId], [AssignedStaffUserId], [SellerOrderId], [B2BContractId], [DisputeReason], [Description], [TicketStatus], [FaultParty], [RefundAmount], [ResolutionNotes], [ResolvedAt], [DecisionAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 23), (SELECT NewId FROM @Map_User WHERE SeedId = 5), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 4), NULL, N'Thùng carton hơi móp góc ngoài', N'Cam bên trong không bị ảnh hưởng nhưng vỏ hộp hơi móp', 'Resolved', 'Carrier', 20000, N'Hoàn thiện chí 20,000đ', @Now, @Now);
INSERT @Map_DisputeTicket VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DisputeTicket] ([SubmittedByUserId], [AssignedStaffUserId], [SellerOrderId], [B2BContractId], [DisputeReason], [Description], [TicketStatus], [FaultParty], [RefundAmount], [ResolutionNotes], [ResolvedAt], [DecisionAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 24), (SELECT NewId FROM @Map_User WHERE SeedId = 5), (SELECT NewId FROM @Map_SellerOrder WHERE SeedId = 5), NULL, N'Tư vấn thêm cách pha cà phê Honey', N'Khách mở ticket hỏi hướng dẫn chiết xuất Espresso và nhận ưu đãi', 'Resolved', 'None', 20000, N'Tặng hoàn tiền 20,000đ tri ân', @Now, @Now);
INSERT @Map_DisputeTicket VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @ReqTime DATETIME2 = DATEADD(HOUR, -12, @Now);
DECLARE @Map_DisputeEvidenceRequest TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[DisputeEvidenceRequest] ([DisputeTicketId], [RequestedPartyUserId], [RequestedByUserId], [RequestedAt], [RequestStatus], [Instructions])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 20), (SELECT NewId FROM @Map_User WHERE SeedId = 5), @ReqTime, 'Pending', N'Vui lòng gửi ảnh chụp hóa đơn hoặc kiện hàng nhận được (#1)');
INSERT @Map_DisputeEvidenceRequest VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DisputeEvidenceRequest] ([DisputeTicketId], [RequestedPartyUserId], [RequestedByUserId], [RequestedAt], [RequestStatus], [Instructions])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 21), (SELECT NewId FROM @Map_User WHERE SeedId = 5), @ReqTime, 'Pending', N'Vui lòng gửi ảnh chụp thực tế sản phẩm xoài khi mở hộp (#2)');
INSERT @Map_DisputeEvidenceRequest VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DisputeEvidenceRequest] ([DisputeTicketId], [RequestedPartyUserId], [RequestedByUserId], [RequestedAt], [RequestStatus], [Instructions])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 22), (SELECT NewId FROM @Map_User WHERE SeedId = 5), @ReqTime, 'Pending', N'Vui lòng gửi ảnh chụp trạng thái nhận hàng trên ứng dụng (#3)');
INSERT @Map_DisputeEvidenceRequest VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DisputeEvidenceRequest] ([DisputeTicketId], [RequestedPartyUserId], [RequestedByUserId], [RequestedAt], [RequestStatus], [Instructions])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 23), (SELECT NewId FROM @Map_User WHERE SeedId = 5), @ReqTime, 'Pending', N'Vui lòng gửi ảnh chụp góc thùng carton bị móp (#4)');
INSERT @Map_DisputeEvidenceRequest VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[DisputeEvidenceRequest] ([DisputeTicketId], [RequestedPartyUserId], [RequestedByUserId], [RequestedAt], [RequestStatus], [Instructions])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 24), (SELECT NewId FROM @Map_User WHERE SeedId = 5), @ReqTime, 'Pending', N'Vui lòng gửi ảnh chụp bao bì lô cà phê đã nhận (#5)');
INSERT @Map_DisputeEvidenceRequest VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_DisputeEvidence TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[DisputeEvidence] ([DisputeTicketId], [SubmittedByUserId], [EvidenceRequestId], [FileUrl], [Description], [SubmittedAt])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 20), (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 1), N'https://cdn.agrisustain.vn/evidence/ev1.jpg', N'Ảnh chụp kiện hàng đơn #1', DATEADD(HOUR, -10, @Now));
INSERT @Map_DisputeEvidence VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.DisputeEvidenceRequest SET RequestStatus = 'Responded', RespondedAt = DATEADD(HOUR, -10, @Now) WHERE EvidenceRequestId = (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 1);
INSERT dbo.[DisputeEvidence] ([DisputeTicketId], [SubmittedByUserId], [EvidenceRequestId], [FileUrl], [Description], [SubmittedAt])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 21), (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 2), N'https://cdn.agrisustain.vn/evidence/ev2.jpg', N'Ảnh chụp kiện hàng đơn #2', DATEADD(HOUR, -10, @Now));
INSERT @Map_DisputeEvidence VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.DisputeEvidenceRequest SET RequestStatus = 'Responded', RespondedAt = DATEADD(HOUR, -10, @Now) WHERE EvidenceRequestId = (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 2);
INSERT dbo.[DisputeEvidence] ([DisputeTicketId], [SubmittedByUserId], [EvidenceRequestId], [FileUrl], [Description], [SubmittedAt])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 22), (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 3), N'https://cdn.agrisustain.vn/evidence/ev3.jpg', N'Ảnh chụp kiện hàng đơn #3', DATEADD(HOUR, -10, @Now));
INSERT @Map_DisputeEvidence VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.DisputeEvidenceRequest SET RequestStatus = 'Responded', RespondedAt = DATEADD(HOUR, -10, @Now) WHERE EvidenceRequestId = (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 3);
INSERT dbo.[DisputeEvidence] ([DisputeTicketId], [SubmittedByUserId], [EvidenceRequestId], [FileUrl], [Description], [SubmittedAt])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 23), (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 4), N'https://cdn.agrisustain.vn/evidence/ev4.jpg', N'Ảnh chụp kiện hàng đơn #4', DATEADD(HOUR, -10, @Now));
INSERT @Map_DisputeEvidence VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.DisputeEvidenceRequest SET RequestStatus = 'Responded', RespondedAt = DATEADD(HOUR, -10, @Now) WHERE EvidenceRequestId = (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 4);
INSERT dbo.[DisputeEvidence] ([DisputeTicketId], [SubmittedByUserId], [EvidenceRequestId], [FileUrl], [Description], [SubmittedAt])
VALUES ((SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 24), (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 5), N'https://cdn.agrisustain.vn/evidence/ev5.jpg', N'Ảnh chụp kiện hàng đơn #5', DATEADD(HOUR, -10, @Now));
INSERT @Map_DisputeEvidence VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.DisputeEvidenceRequest SET RequestStatus = 'Responded', RespondedAt = DATEADD(HOUR, -10, @Now) WHERE EvidenceRequestId = (SELECT NewId FROM @Map_DisputeEvidenceRequest WHERE SeedId = 5);
DECLARE @Map_RefundTransaction TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[RefundTransaction] ([PaymentTransactionId], [PaymentInvoiceAllocationId], [DisputeTicketId], [ProcessedByUserId], [RefundCode], [Amount], [Reason], [RefundStatus], [ProcessedAt], [GatewayReference], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 1), (SELECT NewId FROM @Map_PaymentInvoiceAllocation WHERE SeedId = 1), (SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'RFD-2026-0001', 20000, N'Hoàn tiền thiện chí theo Ticket #1', 'Succeeded', @Now, 'GW-RFD-0001', 'REQ-RFD-0001');
INSERT @Map_RefundTransaction VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RefundTransaction] ([PaymentTransactionId], [PaymentInvoiceAllocationId], [DisputeTicketId], [ProcessedByUserId], [RefundCode], [Amount], [Reason], [RefundStatus], [ProcessedAt], [GatewayReference], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 2), (SELECT NewId FROM @Map_PaymentInvoiceAllocation WHERE SeedId = 2), (SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'RFD-2026-0002', 20000, N'Hoàn tiền thiện chí theo Ticket #2', 'Succeeded', @Now, 'GW-RFD-0002', 'REQ-RFD-0002');
INSERT @Map_RefundTransaction VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RefundTransaction] ([PaymentTransactionId], [PaymentInvoiceAllocationId], [DisputeTicketId], [ProcessedByUserId], [RefundCode], [Amount], [Reason], [RefundStatus], [ProcessedAt], [GatewayReference], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 3), (SELECT NewId FROM @Map_PaymentInvoiceAllocation WHERE SeedId = 3), (SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'RFD-2026-0003', 20000, N'Hoàn tiền thiện chí theo Ticket #3', 'Succeeded', @Now, 'GW-RFD-0003', 'REQ-RFD-0003');
INSERT @Map_RefundTransaction VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RefundTransaction] ([PaymentTransactionId], [PaymentInvoiceAllocationId], [DisputeTicketId], [ProcessedByUserId], [RefundCode], [Amount], [Reason], [RefundStatus], [ProcessedAt], [GatewayReference], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 4), (SELECT NewId FROM @Map_PaymentInvoiceAllocation WHERE SeedId = 4), (SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'RFD-2026-0004', 20000, N'Hoàn tiền thiện chí theo Ticket #4', 'Succeeded', @Now, 'GW-RFD-0004', 'REQ-RFD-0004');
INSERT @Map_RefundTransaction VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[RefundTransaction] ([PaymentTransactionId], [PaymentInvoiceAllocationId], [DisputeTicketId], [ProcessedByUserId], [RefundCode], [Amount], [Reason], [RefundStatus], [ProcessedAt], [GatewayReference], [RequestKey])
VALUES ((SELECT NewId FROM @Map_PaymentTransaction WHERE SeedId = 5), (SELECT NewId FROM @Map_PaymentInvoiceAllocation WHERE SeedId = 5), (SELECT NewId FROM @Map_DisputeTicket WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'RFD-2026-0005', 20000, N'Hoàn tiền thiện chí theo Ticket #5', 'Succeeded', @Now, 'GW-RFD-0005', 'REQ-RFD-0005');
INSERT @Map_RefundTransaction VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_FarmerPayout TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[FarmerPayout] ([FarmerProfileId], [ApprovedByUserId], [PayoutCode], [Amount], [DeductedCommission], [NetAmount], [PayoutStatus], [EligibleAt], [RequestKey])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 1), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'PAYOUT-2026-0001', 400000, 20000, 380000, 'Pending', DATEADD(DAY, -2, @Now), 'REQ-PO-0001');
INSERT @Map_FarmerPayout VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmerPayout] ([FarmerProfileId], [ApprovedByUserId], [PayoutCode], [Amount], [DeductedCommission], [NetAmount], [PayoutStatus], [EligibleAt], [RequestKey])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 2), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'PAYOUT-2026-0002', 800000, 48000, 752000, 'Pending', DATEADD(DAY, -2, @Now), 'REQ-PO-0002');
INSERT @Map_FarmerPayout VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmerPayout] ([FarmerProfileId], [ApprovedByUserId], [PayoutCode], [Amount], [DeductedCommission], [NetAmount], [PayoutStatus], [EligibleAt], [RequestKey])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 3), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'PAYOUT-2026-0003', 350000, 14000, 336000, 'Pending', DATEADD(DAY, -2, @Now), 'REQ-PO-0003');
INSERT @Map_FarmerPayout VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmerPayout] ([FarmerProfileId], [ApprovedByUserId], [PayoutCode], [Amount], [DeductedCommission], [NetAmount], [PayoutStatus], [EligibleAt], [RequestKey])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 4), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'PAYOUT-2026-0004', 500000, 30000, 470000, 'Pending', DATEADD(DAY, -2, @Now), 'REQ-PO-0004');
INSERT @Map_FarmerPayout VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[FarmerPayout] ([FarmerProfileId], [ApprovedByUserId], [PayoutCode], [Amount], [DeductedCommission], [NetAmount], [PayoutStatus], [EligibleAt], [RequestKey])
VALUES ((SELECT NewId FROM @Map_FarmerProfile WHERE SeedId = 5), (SELECT NewId FROM @Map_User WHERE SeedId = 7), 'PAYOUT-2026-0005', 1500000, 45000, 1455000, 'Pending', DATEADD(DAY, -2, @Now), 'REQ-PO-0005');
INSERT @Map_FarmerPayout VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_PayoutItem TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[PayoutItem] ([FarmerPayoutId], [OrderItemId], [CommissionPolicyId], [InvoiceLineId], [GrossAmount], [CommissionRateSnapshot], [CommissionAmount], [NetAmount], [SourceAmountSettled])
VALUES ((SELECT NewId FROM @Map_FarmerPayout WHERE SeedId = 1), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 1), (SELECT NewId FROM @Map_CommissionPolicy WHERE SeedId = 1), (SELECT NewId FROM @Map_InvoiceLine WHERE SeedId = 1), 400000, 5, 20000, 380000, 400000);
INSERT @Map_PayoutItem VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PayoutItem] ([FarmerPayoutId], [OrderItemId], [CommissionPolicyId], [InvoiceLineId], [GrossAmount], [CommissionRateSnapshot], [CommissionAmount], [NetAmount], [SourceAmountSettled])
VALUES ((SELECT NewId FROM @Map_FarmerPayout WHERE SeedId = 2), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 2), (SELECT NewId FROM @Map_CommissionPolicy WHERE SeedId = 2), (SELECT NewId FROM @Map_InvoiceLine WHERE SeedId = 2), 800000, 6, 48000, 752000, 800000);
INSERT @Map_PayoutItem VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PayoutItem] ([FarmerPayoutId], [OrderItemId], [CommissionPolicyId], [InvoiceLineId], [GrossAmount], [CommissionRateSnapshot], [CommissionAmount], [NetAmount], [SourceAmountSettled])
VALUES ((SELECT NewId FROM @Map_FarmerPayout WHERE SeedId = 3), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 3), (SELECT NewId FROM @Map_CommissionPolicy WHERE SeedId = 3), (SELECT NewId FROM @Map_InvoiceLine WHERE SeedId = 3), 350000, 4, 14000, 336000, 350000);
INSERT @Map_PayoutItem VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PayoutItem] ([FarmerPayoutId], [OrderItemId], [CommissionPolicyId], [InvoiceLineId], [GrossAmount], [CommissionRateSnapshot], [CommissionAmount], [NetAmount], [SourceAmountSettled])
VALUES ((SELECT NewId FROM @Map_FarmerPayout WHERE SeedId = 4), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 4), (SELECT NewId FROM @Map_CommissionPolicy WHERE SeedId = 2), (SELECT NewId FROM @Map_InvoiceLine WHERE SeedId = 4), 500000, 6, 30000, 470000, 500000);
INSERT @Map_PayoutItem VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[PayoutItem] ([FarmerPayoutId], [OrderItemId], [CommissionPolicyId], [InvoiceLineId], [GrossAmount], [CommissionRateSnapshot], [CommissionAmount], [NetAmount], [SourceAmountSettled])
VALUES ((SELECT NewId FROM @Map_FarmerPayout WHERE SeedId = 5), (SELECT NewId FROM @Map_OrderItem WHERE SeedId = 5), (SELECT NewId FROM @Map_CommissionPolicy WHERE SeedId = 4), (SELECT NewId FROM @Map_InvoiceLine WHERE SeedId = 5), 1500000, 3, 45000, 1455000, 1500000);
INSERT @Map_PayoutItem VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
UPDATE dbo.FarmerPayout SET PayoutStatus = 'Approved', ApprovedAt = @Now WHERE FarmerPayoutId IN (SELECT NewId FROM @Map_FarmerPayout WHERE SeedId IN (1, 2, 3, 4, 5));
UPDATE dbo.FarmerPayout SET PayoutStatus = 'Processing' WHERE FarmerPayoutId IN (SELECT NewId FROM @Map_FarmerPayout WHERE SeedId IN (1, 2, 3, 4, 5));
UPDATE dbo.FarmerPayout
    SET PayoutStatus = 'Paid',
        ProcessedAt = @Now,
        BankTransferRef = CONCAT('BANK-TX-', FarmerPayoutId)
    WHERE FarmerPayoutId IN (SELECT NewId FROM @Map_FarmerPayout WHERE SeedId IN (1, 2, 3, 4, 5));
DECLARE @Map_Notification TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[Notification] ([UserId], [Title], [Content], [NotificationType], [IsRead], [SentAt], [DeliveryChannel], [DeliveryStatus], [RetryCount], [RelatedRecordType], [RelatedRecordId], [PayloadJson])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 10), N'Hợp đồng bao tiêu đã ký kết', N'Hợp đồng B2B-2026-0001 đã được ký đầy đủ bởi hai bên.', 'ContractSigned', 1, @Now, 'Email', 'Sent', 0, 'B2BContract', (SELECT NewId FROM @Map_B2BContract WHERE SeedId = 1), JSON_MODIFY(N'{"contractId":1}', '$.contractId', (SELECT NewId FROM @Map_B2BContract WHERE SeedId = 1)));
INSERT @Map_Notification VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Notification] ([UserId], [Title], [Content], [NotificationType], [IsRead], [SentAt], [DeliveryChannel], [DeliveryStatus], [RetryCount], [RelatedRecordType], [RelatedRecordId], [PayloadJson])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 11), N'Quyết toán nông trại thành công', N'Kỳ quyết toán PAYOUT-2026-0002 đã được chuyển khoản.', 'PayoutPaid', 1, @Now, 'Email', 'Sent', 0, 'FarmerPayout', (SELECT NewId FROM @Map_FarmerPayout WHERE SeedId = 2), JSON_MODIFY(N'{"payoutId":2}', '$.payoutId', (SELECT NewId FROM @Map_FarmerPayout WHERE SeedId = 2)));
INSERT @Map_Notification VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Notification] ([UserId], [Title], [Content], [NotificationType], [IsRead], [SentAt], [DeliveryChannel], [DeliveryStatus], [RetryCount], [RelatedRecordType], [RelatedRecordId], [PayloadJson])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 20), N'Đơn hàng giao thành công', N'Đơn hàng ORD-RET-2026-001 của bạn đã hoàn tất.', 'OrderCompleted', 1, @Now, 'InApp', 'Sent', 0, 'Order', (SELECT NewId FROM @Map_Order WHERE SeedId = 1), JSON_MODIFY(N'{"orderId":1}', '$.orderId', (SELECT NewId FROM @Map_Order WHERE SeedId = 1)));
INSERT @Map_Notification VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Notification] ([UserId], [Title], [Content], [NotificationType], [IsRead], [SentAt], [DeliveryChannel], [DeliveryStatus], [RetryCount], [RelatedRecordType], [RelatedRecordId], [PayloadJson])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 21), N'Xác nhận giữ chỗ Đặt trước', N'Đơn đặt trước ORD-PRE-2026-002 đã nhận tiền cọc.', 'PreOrderConfirmed', 0, @Now, 'Email', 'Sent', 0, 'Order', (SELECT NewId FROM @Map_Order WHERE SeedId = 7), JSON_MODIFY(N'{"orderId":7}', '$.orderId', (SELECT NewId FROM @Map_Order WHERE SeedId = 7)));
INSERT @Map_Notification VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[Notification] ([UserId], [Title], [Content], [NotificationType], [IsRead], [SentAt], [DeliveryChannel], [DeliveryStatus], [RetryCount], [RelatedRecordType], [RelatedRecordId], [PayloadJson])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 15), N'Kích hoạt gói hội viên B2B', N'Gói Khởi Đầu B2B của bạn đã được kích hoạt thành công.', 'SubscriptionActive', 1, @Now, 'InApp', 'Sent', 0, 'B2BSubscription', (SELECT NewId FROM @Map_B2BSubscription WHERE SeedId = 1), JSON_MODIFY(N'{"subId":1}', '$.subId', (SELECT NewId FROM @Map_B2BSubscription WHERE SeedId = 1)));
INSERT @Map_Notification VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
DECLARE @Map_AuditLog TABLE (SeedId INT PRIMARY KEY, NewId BIGINT NOT NULL);
INSERT dbo.[AuditLog] ([PerformedByUserId], [EntityType], [Action], [OldValue], [NewValue], [Reason], [EntityId], [TraceId], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 1), 'BusinessPolicy', 'CREATE', NULL, N'{"PayoutDelayHours":48}', N'Khởi tạo cấu hình chính sách quyết toán mặc định', 1, 'TRACE-0001', @Now);
INSERT @Map_AuditLog VALUES (1, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[AuditLog] ([PerformedByUserId], [EntityType], [Action], [OldValue], [NewValue], [Reason], [EntityId], [TraceId], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 3), 'FarmCertificate', 'VERIFY', N'{"Status":"Pending"}', N'{"Status":"Verified"}', N'Thẩm định hợp lệ hồ sơ VietGAP Đà Lạt', (SELECT NewId FROM @Map_FarmCertificate WHERE SeedId = 1), 'TRACE-0002', @Now);
INSERT @Map_AuditLog VALUES (2, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[AuditLog] ([PerformedByUserId], [EntityType], [Action], [OldValue], [NewValue], [Reason], [EntityId], [TraceId], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 4), 'ProductReview', 'MODERATE', N'{"Status":"Pending"}', N'{"Status":"Approved"}', N'Nội dung đánh giá hợp lệ, đúng thực tế mua hàng', (SELECT NewId FROM @Map_ProductReview WHERE SeedId = 1), 'TRACE-0003', @Now);
INSERT @Map_AuditLog VALUES (3, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[AuditLog] ([PerformedByUserId], [EntityType], [Action], [OldValue], [NewValue], [Reason], [EntityId], [TraceId], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 6), 'Voucher', 'PUBLISH', NULL, N'{"Code":"AGRISUSTAIN10K"}', N'Phát hành mã khuyến mãi toàn sàn tháng 10/2026', (SELECT NewId FROM @Map_Voucher WHERE SeedId = 1), 'TRACE-0004', @Now);
INSERT @Map_AuditLog VALUES (4, CONVERT(BIGINT, SCOPE_IDENTITY()));
INSERT dbo.[AuditLog] ([PerformedByUserId], [EntityType], [Action], [OldValue], [NewValue], [Reason], [EntityId], [TraceId], [OccurredAt])
VALUES ((SELECT NewId FROM @Map_User WHERE SeedId = 7), 'FarmerPayout', 'APPROVE_AND_PAY', N'{"Status":"Pending"}', N'{"Status":"Paid"}', N'Duyệt và giải ngân kỳ quyết toán nông trại #1', (SELECT NewId FROM @Map_FarmerPayout WHERE SeedId = 1), 'TRACE-0005', @Now);
INSERT @Map_AuditLog VALUES (5, CONVERT(BIGINT, SCOPE_IDENTITY()));
IF EXISTS (SELECT 1 FROM dbo.HarvestBatch WHERE AvailableQuantity < 0 OR ReservedQuantity < 0)
    THROW 51113, 'Invalid negative inventory quantity.', 1;
INSERT dbo.__AgriSustainSeedHistory (SeedName) VALUES ('demo-20261022-v1');
COMMIT TRANSACTION;
PRINT N'Demo data inserted successfully; existing account passwords were preserved.';
END TRY
BEGIN CATCH
IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
THROW;
END CATCH;
