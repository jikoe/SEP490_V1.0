-- =========================================================================
-- SCRIPT CHẠY TRỰC TIẾP TRONG SSMS (SQL SERVER MANAGEMENT STUDIO)
-- Tự động tạo Database AgriSustain nếu chưa có và tự động USE AgriSustain
-- =========================================================================
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = N'AgriSustain')
BEGIN
    CREATE DATABASE [AgriSustain];
END
GO
USE [AgriSustain];
GO
-- Fresh install in an explicitly selected EMPTY database. No CREATE DATABASE/USE here.
-- Source: user-supplied AgriSustain.sql (46 BIGINT tables), plus approved migration.
-- SQLCMD: sqlcmd -S "(localdb)\MSSQLLocalDB" -d AgriSustain -E -b -f 65001 -v TargetDatabase="AgriSustain" -i AgriSustain.sql
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
IF EXISTS (SELECT 1 FROM sys.tables WHERE is_ms_shipped = 0)
    THROW 51001, 'Fresh install requires an empty database. Use the migration for an existing baseline.', 1;
GO
-- PHÂN HỆ 1: TÀI KHOẢN VÀ HỘI VIÊN (6 Bảng)
-- =========================================================================

-- 1. Bảng User
CREATE TABLE [User] (
    UserId BIGINT IDENTITY(1,1) PRIMARY KEY,
    Username NVARCHAR(100) NOT NULL UNIQUE,
    Email NVARCHAR(255) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(500) NOT NULL,
    FullName NVARCHAR(150) NOT NULL,
    PhoneNumber VARCHAR(20) NULL,
    AvatarUrl NVARCHAR(500) NULL,
    [Role] VARCHAR(50) NOT NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Active',
    IsEmailVerified BIT NOT NULL DEFAULT 0,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    FailedLoginCount INT NOT NULL DEFAULT 0,
    LockedUntil DATETIME2 NULL
);
GO

-- 2. Bảng FarmerProfile (Quan hệ 1 - 0..1 với User)
CREATE TABLE FarmerProfile (
    FarmerProfileId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL UNIQUE,
    FarmName NVARCHAR(200) NOT NULL,
    RepresentativeName NVARCHAR(150) NOT NULL,
    ProductionScale NVARCHAR(200) NULL,
    BusinessLicenseNumber VARCHAR(100) NULL,
    [Address] NVARCHAR(500) NOT NULL,
    ProvinceCode VARCHAR(20) NOT NULL,
    CultivationZone NVARCHAR(200) NULL,
    GPSCoordinates VARCHAR(100) NULL,
    VerificationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedAt DATETIME2 NULL,
    RejectReason NVARCHAR(500) NULL,
    ReputationScore DECIMAL(5,2) NOT NULL DEFAULT 0.00,
    CONSTRAINT FK_FarmerProfile_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 3. Bảng WholesaleBuyerProfile (Quan hệ 1 - 0..1 với User)
CREATE TABLE WholesaleBuyerProfile (
    WholesaleBuyerProfileId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL UNIQUE,
    CompanyName NVARCHAR(255) NOT NULL,
    TaxCode VARCHAR(50) NOT NULL UNIQUE,
    RepresentativeName NVARCHAR(150) NOT NULL,
    BusinessType NVARCHAR(100) NULL,
    BusinessAddress NVARCHAR(500) NOT NULL,
    DocumentUrl NVARCHAR(500) NULL,
    VerificationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ApprovedAt DATETIME2 NULL,
    RejectReason NVARCHAR(500) NULL,
    CONSTRAINT FK_WholesaleBuyerProfile_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 4. Bảng DeliveryAddress (Quan hệ 1 - 0..* từ User)
CREATE TABLE DeliveryAddress (
    DeliveryAddressId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    RecipientName NVARCHAR(150) NOT NULL,
    RecipientPhone VARCHAR(20) NOT NULL,
    AddressDetail NVARCHAR(500) NOT NULL,
    ProvinceCity NVARCHAR(100) NOT NULL,
    District NVARCHAR(100) NOT NULL,
    Ward NVARCHAR(100) NOT NULL,
    IsDefault BIT NOT NULL DEFAULT 0,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Active',
    CONSTRAINT FK_DeliveryAddress_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 5. Bảng B2BSubscriptionPackage
CREATE TABLE B2BSubscriptionPackage (
    PackageId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PackageName NVARCHAR(150) NOT NULL,
    PackageDescription NVARCHAR(1000) NULL,
    DurationDays INT NOT NULL,
    Price DECIMAL(18,2) NOT NULL,
    UnlocksFarmerDirectContact BIT NOT NULL DEFAULT 0,
    CanDownloadContractTemplate BIT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    TransactionLimit INT NULL
);
GO

-- 6. Bảng B2BSubscription
CREATE TABLE B2BSubscription (
    B2BSubscriptionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    WholesaleBuyerProfileId BIGINT NOT NULL,
    PackageId BIGINT NOT NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    SubscriptionStatus VARCHAR(50) NOT NULL DEFAULT 'Active',
    AutoRenew BIT NOT NULL DEFAULT 0,
    PriceSnapshot DECIMAL(18,2) NOT NULL,
    LastRenewedAt DATETIME2 NULL,
    CONSTRAINT FK_B2BSubscription_WholesaleBuyer FOREIGN KEY (WholesaleBuyerProfileId) REFERENCES WholesaleBuyerProfile(WholesaleBuyerProfileId),
    CONSTRAINT FK_B2BSubscription_Package FOREIGN KEY (PackageId) REFERENCES B2BSubscriptionPackage(PackageId)
);
GO

-- =========================================================================
-- PHÂN HỆ 2: NÔNG TRẠI, SẢN PHẨM VÀ TRUY XUẤT (11 Bảng)
-- =========================================================================

-- 7. Bảng Category (Đệ quy 0..1 - 0..* danh mục cha con)
CREATE TABLE Category (
    CategoryId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ParentCategoryId BIGINT NULL,
    CategoryName NVARCHAR(150) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    [Priority] INT NOT NULL DEFAULT 0,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Category_ParentCategory FOREIGN KEY (ParentCategoryId) REFERENCES Category(CategoryId)
);
GO

-- 8. Bảng Product
CREATE TABLE Product (
    ProductId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CategoryId BIGINT NOT NULL,
    FarmerProfileId BIGINT NOT NULL,
    ProductName NVARCHAR(255) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    Unit NVARCHAR(50) NOT NULL,
    OriginProvince NVARCHAR(100) NULL,
    BaseRetailPrice DECIMAL(18,2) NOT NULL,
    WholesaleMinQuantity DECIMAL(18,3) NULL,
    WholesalePrice DECIMAL(18,2) NULL,
    ApprovalStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    IsPublished BIT NOT NULL DEFAULT 0,
    RejectionReason NVARCHAR(500) NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Product_Category FOREIGN KEY (CategoryId) REFERENCES Category(CategoryId),
    CONSTRAINT FK_Product_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 9. Bảng CropSeason
CREATE TABLE CropSeason (
    CropSeasonId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerProfileId BIGINT NOT NULL,
    SeasonName NVARCHAR(200) NOT NULL,
    CropVariety NVARCHAR(150) NOT NULL,
    CultivatedArea DECIMAL(18,3) NOT NULL,
    PlantingDate DATE NOT NULL,
    EstimatedHarvestDate DATE NOT NULL,
    ActualHarvestDate DATE NULL,
    EstimatedYield DECIMAL(18,3) NOT NULL,
    ActualYield DECIMAL(18,3) NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Planning',
    CONSTRAINT FK_CropSeason_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 10. Bảng FarmCertificate
CREATE TABLE FarmCertificate (
    FarmCertificateId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerProfileId BIGINT NOT NULL,
    ReviewedByUserId BIGINT NULL, -- User thẩm định (0..1)
    CertificateType NVARCHAR(100) NOT NULL,
    CertificateNumber VARCHAR(100) NOT NULL,
    IssuedBy NVARCHAR(200) NOT NULL,
    IssueDate DATE NOT NULL,
    ExpiryDate DATE NULL,
    DocumentUrl NVARCHAR(500) NOT NULL,
    VerificationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    RevocationReason NVARCHAR(500) NULL,
    ReviewedAt DATETIME2 NULL,
    CONSTRAINT FK_FarmCertificate_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId),
    CONSTRAINT FK_FarmCertificate_ReviewedByUser FOREIGN KEY (ReviewedByUserId) REFERENCES [User](UserId)
);
GO

-- 11. Bảng ProductImage
CREATE TABLE ProductImage (
    ProductImageId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    ImageUrl NVARCHAR(500) NOT NULL,
    DisplayOrder INT NOT NULL DEFAULT 0,
    IsPrimary BIT NOT NULL DEFAULT 0,
    AltText NVARCHAR(255) NULL,
    CONSTRAINT FK_ProductImage_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId)
);
GO

-- 12. Bảng PreOrderCampaign (Đã sửa điểm nghẽn 2: Liên kết Product 1 - 0..* và CropSeason 0..1 - 0..*)
CREATE TABLE PreOrderCampaign (
    PreOrderCampaignId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    CropSeasonId BIGINT NULL,
    CampaignName NVARCHAR(255) NOT NULL,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    TargetYield DECIMAL(18,3) NOT NULL,
    CommittedYield DECIMAL(18,3) NOT NULL DEFAULT 0,
    DepositPercentage DECIMAL(5,2) NOT NULL DEFAULT 0,
    PreOrderPrice DECIMAL(18,2) NOT NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Draft',
    CONSTRAINT FK_PreOrderCampaign_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_PreOrderCampaign_CropSeason FOREIGN KEY (CropSeasonId) REFERENCES CropSeason(CropSeasonId)
);
GO

-- 13. Bảng HarvestBatch (Đã sửa điểm nghẽn 2: Liên kết PreOrderCampaign 0..1 - 0..*)
CREATE TABLE HarvestBatch (
    HarvestBatchId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ProductId BIGINT NOT NULL,
    CropSeasonId BIGINT NOT NULL,
    PreOrderCampaignId BIGINT NULL,
    BatchCode VARCHAR(100) NOT NULL UNIQUE,
    EstimatedHarvestDate DATE NULL,
    ActualHarvestDate DATE NULL,
    HarvestYield DECIMAL(18,3) NOT NULL DEFAULT 0,
    ExpirationDate DATE NULL,
    QualityNotes NVARCHAR(500) NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Available',
    StockEnteredAt DATETIME2 NULL,
    ReservedQuantity DECIMAL(18,3) NOT NULL DEFAULT 0,
    AvailableQuantity DECIMAL(18,3) NOT NULL DEFAULT 0,
    CONSTRAINT FK_HarvestBatch_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_HarvestBatch_CropSeason FOREIGN KEY (CropSeasonId) REFERENCES CropSeason(CropSeasonId),
    CONSTRAINT FK_HarvestBatch_PreOrderCampaign FOREIGN KEY (PreOrderCampaignId) REFERENCES PreOrderCampaign(PreOrderCampaignId)
);
GO

-- 14. Bảng CultivationLog
CREATE TABLE CultivationLog (
    CultivationLogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CropSeasonId BIGINT NOT NULL,
    RecordedByUserId BIGINT NOT NULL, -- User ghi nhận (1 - 0..*)
    ActionDate DATETIME2 NOT NULL,
    ActionType NVARCHAR(100) NOT NULL,
    DetailDescription NVARCHAR(1000) NULL,
    InputMaterial NVARCHAR(255) NULL,
    Dosage NVARCHAR(150) NULL,
    PerformedBy NVARCHAR(150) NULL,
    ImageUrl NVARCHAR(500) NULL,
    RecordedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_CultivationLog_CropSeason FOREIGN KEY (CropSeasonId) REFERENCES CropSeason(CropSeasonId),
    CONSTRAINT FK_CultivationLog_RecordedByUser FOREIGN KEY (RecordedByUserId) REFERENCES [User](UserId)
);
GO

-- 15. Bảng TraceabilityQRCode (Quan hệ 1 - 0..1 với HarvestBatch)
CREATE TABLE TraceabilityQRCode (
    TraceabilityQRCodeId BIGINT IDENTITY(1,1) PRIMARY KEY,
    HarvestBatchId BIGINT NOT NULL UNIQUE,
    QRContentString NVARCHAR(500) NOT NULL UNIQUE,
    TargetUrl NVARCHAR(500) NOT NULL,
    ScanCount INT NOT NULL DEFAULT 0,
    IsRevoked BIT NOT NULL DEFAULT 0,
    GeneratedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_TraceabilityQRCode_HarvestBatch FOREIGN KEY (HarvestBatchId) REFERENCES HarvestBatch(HarvestBatchId)
);
GO

-- 16. Bảng RegionalPriceBenchmark
CREATE TABLE RegionalPriceBenchmark (
    BenchmarkId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CategoryId BIGINT NOT NULL,
    RegionName NVARCHAR(100) NOT NULL,
    ProvinceName NVARCHAR(100) NOT NULL,
    CommodityName NVARCHAR(150) NOT NULL,
    MinPrice DECIMAL(18,2) NOT NULL,
    MaxPrice DECIMAL(18,2) NOT NULL,
    AveragePrice DECIMAL(18,2) NOT NULL,
    RecordedDate DATE NOT NULL,
    CONSTRAINT FK_RegionalPriceBenchmark_Category FOREIGN KEY (CategoryId) REFERENCES Category(CategoryId)
);
GO

-- 17. Bảng InventoryMovement (Khóa ngoại tới OrderItemBatchAllocation được thêm ở cuối script)
CREATE TABLE InventoryMovement (
    InventoryMovementId BIGINT IDENTITY(1,1) PRIMARY KEY,
    HarvestBatchId BIGINT NOT NULL,
    OrderItemBatchAllocationId BIGINT NULL,
    RecordedByUserId BIGINT NULL, -- User ghi nhận (0..1)
    MovementType VARCHAR(50) NOT NULL,
    QuantityChange DECIMAL(18,3) NOT NULL,
    Reason NVARCHAR(500) NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_InventoryMovement_HarvestBatch FOREIGN KEY (HarvestBatchId) REFERENCES HarvestBatch(HarvestBatchId),
    CONSTRAINT FK_InventoryMovement_RecordedByUser FOREIGN KEY (RecordedByUserId) REFERENCES [User](UserId)
);
GO

-- =========================================================================
-- PHÂN HỆ 3: ĐƠN HÀNG VÀ GIAO NHẬN (12 Bảng)
-- =========================================================================

-- 18. Bảng Order
CREATE TABLE [Order] (
    OrderId BIGINT IDENTITY(1,1) PRIMARY KEY,
    BuyerUserId BIGINT NOT NULL, -- User đặt mua (1 - 0..*)
    OrderCode VARCHAR(100) NOT NULL UNIQUE,
    OrderType VARCHAR(50) NOT NULL,
    IsPreOrder BIT NOT NULL DEFAULT 0,
    OrderDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ExpectedDeliveryDate DATETIME2 NULL,
    ShippingAddress NVARCHAR(500) NOT NULL,
    TotalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DiscountAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeliveryFee DECIMAL(18,2) NOT NULL DEFAULT 0,
    FinalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    OrderStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ReservationExpiresAt DATETIME2 NULL,
    RecipientNameSnapshot NVARCHAR(150) NOT NULL,
    RecipientPhoneSnapshot VARCHAR(20) NOT NULL,
    CompletedAt DATETIME2 NULL,
    CONSTRAINT FK_Order_BuyerUser FOREIGN KEY (BuyerUserId) REFERENCES [User](UserId)
);
GO

-- 19. Bảng SellerOrder (Khóa ngoại B2BContractId được thêm ở cuối script)
CREATE TABLE SellerOrder (
    SellerOrderId BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderId BIGINT NOT NULL,
    FarmerProfileId BIGINT NOT NULL,
    B2BContractId BIGINT NULL, -- Đơn thực hiện hợp đồng (0..1)
    SellerOrderCode VARCHAR(100) NOT NULL UNIQUE,
    Subtotal DECIMAL(18,2) NOT NULL DEFAULT 0,
    DiscountShare DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeliveryFee DECIMAL(18,2) NOT NULL DEFAULT 0,
    FinalAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Pending',
    DeliveredAt DATETIME2 NULL,
    ReceivedAt DATETIME2 NULL,
    CONSTRAINT FK_SellerOrder_Order FOREIGN KEY (OrderId) REFERENCES [Order](OrderId),
    CONSTRAINT FK_SellerOrder_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 20. Bảng OrderItem (Khóa ngoại ContractLineId được thêm ở cuối script)
CREATE TABLE OrderItem (
    OrderItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SellerOrderId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    PreOrderCampaignId BIGINT NULL,
    ContractLineId BIGINT NULL, -- Thực hiện lượng cam kết (0..1)
    Quantity DECIMAL(18,3) NOT NULL,
    UnitPriceSnapshot DECIMAL(18,2) NOT NULL,
    UnitSnapshot NVARCHAR(50) NOT NULL,
    DiscountApplied DECIMAL(18,2) NOT NULL DEFAULT 0,
    SubTotal DECIMAL(18,2) NOT NULL,
    ItemStatus VARCHAR(50) NOT NULL DEFAULT 'Normal',
    CONSTRAINT FK_OrderItem_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId),
    CONSTRAINT FK_OrderItem_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_OrderItem_PreOrderCampaign FOREIGN KEY (PreOrderCampaignId) REFERENCES PreOrderCampaign(PreOrderCampaignId)
);
GO

-- 21. Bảng OrderItemBatchAllocation
CREATE TABLE OrderItemBatchAllocation (
    OrderItemBatchAllocationId BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderItemId BIGINT NOT NULL,
    HarvestBatchId BIGINT NOT NULL,
    AllocationSequence INT NOT NULL DEFAULT 1,
    AllocatedQuantity DECIMAL(18,3) NOT NULL,
    AllocationStatus VARCHAR(50) NOT NULL DEFAULT 'Reserved',
    ReservedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ExpiresAt DATETIME2 NULL,
    ReleasedAt DATETIME2 NULL,
    CONSTRAINT FK_BatchAllocation_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItem(OrderItemId),
    CONSTRAINT FK_BatchAllocation_HarvestBatch FOREIGN KEY (HarvestBatchId) REFERENCES HarvestBatch(HarvestBatchId)
);
GO

-- 22. Bảng Shipment (Khóa ngoại ContractMilestoneId được thêm ở cuối script)
CREATE TABLE Shipment (
    ShipmentId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SellerOrderId BIGINT NOT NULL,
    ContractMilestoneId BIGINT NULL,
    TrackingNumber VARCHAR(100) NULL UNIQUE,
    PlannedDeliveryAt DATETIME2 NULL,
    ShippedAt DATETIME2 NULL,
    DeliveredAt DATETIME2 NULL,
    ReceivedAt DATETIME2 NULL,
    ShipmentStatus VARCHAR(50) NOT NULL DEFAULT 'Preparing',
    CONSTRAINT FK_Shipment_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId)
);
GO

-- 23. Bảng ShipmentItem
CREATE TABLE ShipmentItem (
    ShipmentItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ShipmentId BIGINT NOT NULL,
    OrderItemBatchAllocationId BIGINT NOT NULL,
    ShippedQuantity DECIMAL(18,3) NOT NULL,
    ReceivedQuantity DECIMAL(18,3) NULL,
    AcceptedQuantity DECIMAL(18,3) NULL,
    QualityResult NVARCHAR(255) NULL,
    ReconciliationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ReconciledAt DATETIME2 NULL,
    CONSTRAINT FK_ShipmentItem_Shipment FOREIGN KEY (ShipmentId) REFERENCES Shipment(ShipmentId),
    CONSTRAINT FK_ShipmentItem_BatchAllocation FOREIGN KEY (OrderItemBatchAllocationId) REFERENCES OrderItemBatchAllocation(OrderItemBatchAllocationId)
);
GO

-- 24. Bảng Cart (Quan hệ 1 - 0..1 với User)
CREATE TABLE Cart (
    CartId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL UNIQUE,
    UpdatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_Cart_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 25. Bảng CartItem
CREATE TABLE CartItem (
    CartItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CartId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    PreOrderCampaignId BIGINT NULL,
    Quantity DECIMAL(18,3) NOT NULL,
    IsSelected BIT NOT NULL DEFAULT 1,
    AddedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_CartItem_Cart FOREIGN KEY (CartId) REFERENCES Cart(CartId),
    CONSTRAINT FK_CartItem_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_CartItem_PreOrderCampaign FOREIGN KEY (PreOrderCampaignId) REFERENCES PreOrderCampaign(PreOrderCampaignId)
);
GO

-- 26. Bảng ProductReview (Quan hệ 1 - 0..1 với OrderItem)
CREATE TABLE ProductReview (
    ProductReviewId BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderItemId BIGINT NOT NULL UNIQUE,
    UserId BIGINT NOT NULL,
    RatingStars TINYINT NOT NULL CHECK (RatingStars BETWEEN 1 AND 5),
    Comment NVARCHAR(1000) NULL,
    ReviewDate DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    IsPurchasedVerified BIT NOT NULL DEFAULT 1,
    ModerationStatus VARCHAR(50) NOT NULL DEFAULT 'Approved',
    ModerationReason NVARCHAR(500) NULL,
    CONSTRAINT FK_ProductReview_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItem(OrderItemId),
    CONSTRAINT FK_ProductReview_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 27. Bảng Voucher (Đã sửa điểm nghẽn 5: FarmerProfileId NULL nếu là voucher của Sàn)
CREATE TABLE Voucher (
    VoucherId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerProfileId BIGINT NULL,
    VoucherCode VARCHAR(100) NOT NULL UNIQUE,
    DiscountType VARCHAR(50) NOT NULL,
    DiscountValue DECIMAL(18,2) NOT NULL,
    MaxDiscountAmount DECIMAL(18,2) NULL,
    MinOrderAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    UsageLimit INT NULL,
    UsedCount INT NOT NULL DEFAULT 0,
    StartDate DATETIME2 NOT NULL,
    EndDate DATETIME2 NOT NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_Voucher_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 28. Bảng VoucherRedemption (Đã sửa điểm nghẽn 5: Áp dụng trên Order tổng và tùy chọn SellerOrder)
CREATE TABLE VoucherRedemption (
    VoucherRedemptionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    VoucherId BIGINT NOT NULL,
    OrderId BIGINT NOT NULL,
    SellerOrderId BIGINT NULL,
    DiscountSnapshot DECIMAL(18,2) NOT NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Reserved',
    ReservedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    AppliedAt DATETIME2 NULL,
    ReleasedAt DATETIME2 NULL,
    CONSTRAINT FK_VoucherRedemption_Voucher FOREIGN KEY (VoucherId) REFERENCES Voucher(VoucherId),
    CONSTRAINT FK_VoucherRedemption_Order FOREIGN KEY (OrderId) REFERENCES [Order](OrderId),
    CONSTRAINT FK_VoucherRedemption_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId)
);
GO

-- 29. Bảng OrderStatusHistory
CREATE TABLE OrderStatusHistory (
    OrderStatusHistoryId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SellerOrderId BIGINT NOT NULL,
    ChangedByUserId BIGINT NULL, -- User đổi trạng thái (0..1)
    OldStatus VARCHAR(50) NULL,
    NewStatus VARCHAR(50) NOT NULL,
    Reason NVARCHAR(500) NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_OrderStatusHistory_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId),
    CONSTRAINT FK_OrderStatusHistory_ChangedByUser FOREIGN KEY (ChangedByUserId) REFERENCES [User](UserId)
);
GO

-- =========================================================================
-- PHÂN HỆ 4: HỢP ĐỒNG BAO TIÊU (8 Bảng)
-- =========================================================================

-- 30. Bảng B2BContract
CREATE TABLE B2BContract (
    B2BContractId BIGINT IDENTITY(1,1) PRIMARY KEY,
    WholesaleBuyerProfileId BIGINT NOT NULL,
    FarmerProfileId BIGINT NOT NULL,
    ContractNumber VARCHAR(100) NOT NULL UNIQUE,
    DepositAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    PaymentTerms NVARCHAR(1000) NULL,
    DigitalSignatureStatus VARCHAR(50) NOT NULL DEFAULT 'Unsigned',
    SignedDate DATETIME2 NULL,
    [Status] VARCHAR(50) NOT NULL DEFAULT 'Draft',
    CurrentVersionNo INT NOT NULL DEFAULT 1,
    ValidFrom DATE NOT NULL,
    ValidUntil DATE NOT NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CompletedAt DATETIME2 NULL,
    CONSTRAINT FK_B2BContract_WholesaleBuyer FOREIGN KEY (WholesaleBuyerProfileId) REFERENCES WholesaleBuyerProfile(WholesaleBuyerProfileId),
    CONSTRAINT FK_B2BContract_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId)
);
GO

-- 31. Bảng ContractVersion
CREATE TABLE ContractVersion (
    ContractVersionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    B2BContractId BIGINT NOT NULL,
    ProposedByUserId BIGINT NOT NULL, -- User đề xuất phiên bản (1 - 0..*)
    VersionNo INT NOT NULL,
    TermsAndConditions NVARCHAR(MAX) NOT NULL,
    LineSnapshotJson NVARCHAR(MAX) NULL CHECK (LineSnapshotJson IS NULL OR ISJSON(LineSnapshotJson) = 1),
    DocumentHash VARCHAR(256) NULL,
    VersionStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    FeedbackReason NVARCHAR(500) NULL,
    CONSTRAINT FK_ContractVersion_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId),
    CONSTRAINT FK_ContractVersion_ProposedByUser FOREIGN KEY (ProposedByUserId) REFERENCES [User](UserId)
);
GO

-- 32. Bảng ContractLine (Đã sửa điểm nghẽn 3: Dòng cam kết sản lượng gắn trực tiếp B2BContract)
CREATE TABLE ContractLine (
    ContractLineId BIGINT IDENTITY(1,1) PRIMARY KEY,
    B2BContractId BIGINT NOT NULL,
    ProductId BIGINT NOT NULL,
    CropSeasonId BIGINT NULL,
    CommittedQuantity DECIMAL(18,3) NOT NULL,
    UnitSnapshot NVARCHAR(50) NOT NULL,
    QualitySpecification NVARCHAR(500) NULL,
    PriceType VARCHAR(50) NOT NULL,
    AgreedPrice DECIMAL(18,2) NULL,
    MinPrice DECIMAL(18,2) NULL,
    MaxPrice DECIMAL(18,2) NULL,
    PriceFormula NVARCHAR(255) NULL,
    CONSTRAINT FK_ContractLine_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId),
    CONSTRAINT FK_ContractLine_Product FOREIGN KEY (ProductId) REFERENCES Product(ProductId),
    CONSTRAINT FK_ContractLine_CropSeason FOREIGN KEY (CropSeasonId) REFERENCES CropSeason(CropSeasonId)
);
GO

-- 33. Bảng ContractMilestone
CREATE TABLE ContractMilestone (
    ContractMilestoneId BIGINT IDENTITY(1,1) PRIMARY KEY,
    B2BContractId BIGINT NOT NULL,
    ContractLineId BIGINT NULL,
    MilestoneTitle NVARCHAR(200) NOT NULL,
    MilestoneType VARCHAR(50) NOT NULL,
    DueDate DATE NOT NULL,
    Amount DECIMAL(18,2) NOT NULL DEFAULT 0,
    DeliveryQuantity DECIMAL(18,3) NULL,
    FulfillmentStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    PaymentStatus VARCHAR(50) NOT NULL DEFAULT 'Unpaid',
    DeliveredQuantity DECIMAL(18,3) NOT NULL DEFAULT 0,
    AcceptedQuantity DECIMAL(18,3) NOT NULL DEFAULT 0,
    PaidAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    CONSTRAINT FK_ContractMilestone_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId),
    CONSTRAINT FK_ContractMilestone_ContractLine FOREIGN KEY (ContractLineId) REFERENCES ContractLine(ContractLineId)
);
GO

-- 34. Bảng ContractSignature
CREATE TABLE ContractSignature (
    ContractSignatureId BIGINT IDENTITY(1,1) PRIMARY KEY,
    ContractVersionId BIGINT NOT NULL,
    SignedByUserId BIGINT NOT NULL, -- User xác nhận ký (1 - 0..*)
    PartyRole VARCHAR(50) NOT NULL,
    ConfirmationMethod VARCHAR(50) NOT NULL,
    ConfirmedAt DATETIME2 NULL,
    ConfirmationStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    CONSTRAINT FK_ContractSignature_ContractVersion FOREIGN KEY (ContractVersionId) REFERENCES ContractVersion(ContractVersionId),
    CONSTRAINT FK_ContractSignature_SignedByUser FOREIGN KEY (SignedByUserId) REFERENCES [User](UserId)
);
GO

-- 35. Bảng ContractTerminationRequest
CREATE TABLE ContractTerminationRequest (
    ContractTerminationRequestId BIGINT IDENTITY(1,1) PRIMARY KEY,
    B2BContractId BIGINT NOT NULL,
    RequestedByUserId BIGINT NOT NULL, -- User yêu cầu chấm dứt (1 - 0..*)
    Reason NVARCHAR(1000) NOT NULL,
    EvidenceUrl NVARCHAR(500) NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    RequestStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    SettlementAmount DECIMAL(18,2) NULL,
    ResolvedAt DATETIME2 NULL,
    CONSTRAINT FK_TerminationRequest_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId),
    CONSTRAINT FK_TerminationRequest_RequestedByUser FOREIGN KEY (RequestedByUserId) REFERENCES [User](UserId)
);
GO

-- 36. Bảng Invoice (Đã sửa điểm nghẽn 1: Hóa đơn theo SellerOrder hoặc ContractMilestone)
CREATE TABLE Invoice (
    InvoiceId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SellerOrderId BIGINT NULL,
    ContractMilestoneId BIGINT NULL,
    InvoiceNumber VARCHAR(100) NOT NULL UNIQUE,
    IssuedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DueAt DATETIME2 NULL,
    TotalAmount DECIMAL(18,2) NOT NULL,
    InvoiceStatus VARCHAR(50) NOT NULL DEFAULT 'Issued',
    CONSTRAINT FK_Invoice_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId),
    CONSTRAINT FK_Invoice_ContractMilestone FOREIGN KEY (ContractMilestoneId) REFERENCES ContractMilestone(ContractMilestoneId)
);
GO

-- 37. Bảng InvoiceLine (Đã sửa điểm nghẽn 1: Liên kết OrderItem hoặc ShipmentItem)
CREATE TABLE InvoiceLine (
    InvoiceLineId BIGINT IDENTITY(1,1) PRIMARY KEY,
    InvoiceId BIGINT NOT NULL,
    OrderItemId BIGINT NULL,
    ShipmentItemId BIGINT NULL,
    Quantity DECIMAL(18,3) NOT NULL,
    UnitPriceSnapshot DECIMAL(18,2) NOT NULL,
    LineAmount DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_InvoiceLine_Invoice FOREIGN KEY (InvoiceId) REFERENCES Invoice(InvoiceId),
    CONSTRAINT FK_InvoiceLine_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItem(OrderItemId),
    CONSTRAINT FK_InvoiceLine_ShipmentItem FOREIGN KEY (ShipmentItemId) REFERENCES ShipmentItem(ShipmentItemId)
);
GO

-- =========================================================================
-- PHÂN HỆ 5: THANH TOÁN, QUYẾT TOÁN VÀ KHIẾU NẠI (9 Bảng)
-- =========================================================================

-- 38. Bảng PaymentTransaction (Đã sửa điểm nghẽn 4: Thêm B2BContractId cho đặt cọc hợp đồng)
CREATE TABLE PaymentTransaction (
    PaymentTransactionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    OrderId BIGINT NULL,
    B2BSubscriptionId BIGINT NULL,
    ContractMilestoneId BIGINT NULL,
    B2BContractId BIGINT NULL,
    TransactionCode VARCHAR(100) NOT NULL UNIQUE,
    GatewayProvider VARCHAR(100) NOT NULL,
    Amount DECIMAL(18,2) NOT NULL,
    PaymentType VARCHAR(50) NOT NULL,
    PaymentStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    WebhookPayload NVARCHAR(MAX) NULL,
    PaidAt DATETIME2 NULL,
    PaymentPurpose VARCHAR(100) NOT NULL,
    RequestedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    ExpiresAt DATETIME2 NULL,
    ReconciledAt DATETIME2 NULL,
    CONSTRAINT FK_PaymentTransaction_Order FOREIGN KEY (OrderId) REFERENCES [Order](OrderId),
    CONSTRAINT FK_PaymentTransaction_B2BSubscription FOREIGN KEY (B2BSubscriptionId) REFERENCES B2BSubscription(B2BSubscriptionId),
    CONSTRAINT FK_PaymentTransaction_ContractMilestone FOREIGN KEY (ContractMilestoneId) REFERENCES ContractMilestone(ContractMilestoneId),
    CONSTRAINT FK_PaymentTransaction_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId)
);
GO

-- 39. Bảng DisputeTicket
CREATE TABLE DisputeTicket (
    DisputeTicketId BIGINT IDENTITY(1,1) PRIMARY KEY,
    SubmittedByUserId BIGINT NOT NULL, -- User gửi khiếu nại (1 - 0..*)
    AssignedStaffUserId BIGINT NULL,   -- Nhân viên được phân công (0..1 - 0..*)
    SellerOrderId BIGINT NULL,
    B2BContractId BIGINT NULL,
    DisputeReason NVARCHAR(255) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    TicketStatus VARCHAR(50) NOT NULL DEFAULT 'Open',
    FaultParty VARCHAR(50) NULL,
    RefundAmount DECIMAL(18,2) NULL,
    ResolutionNotes NVARCHAR(1000) NULL,
    ResolvedAt DATETIME2 NULL,
    CreatedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DecisionAt DATETIME2 NULL,
    CONSTRAINT FK_DisputeTicket_SubmittedByUser FOREIGN KEY (SubmittedByUserId) REFERENCES [User](UserId),
    CONSTRAINT FK_DisputeTicket_AssignedStaffUser FOREIGN KEY (AssignedStaffUserId) REFERENCES [User](UserId),
    CONSTRAINT FK_DisputeTicket_SellerOrder FOREIGN KEY (SellerOrderId) REFERENCES SellerOrder(SellerOrderId),
    CONSTRAINT FK_DisputeTicket_B2BContract FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId)
);
GO

-- 40. Bảng DisputeEvidence
CREATE TABLE DisputeEvidence (
    DisputeEvidenceId BIGINT IDENTITY(1,1) PRIMARY KEY,
    DisputeTicketId BIGINT NOT NULL,
    SubmittedByUserId BIGINT NOT NULL, -- User nộp bằng chứng (1 - 0..*)
    FileUrl NVARCHAR(500) NOT NULL,
    [Description] NVARCHAR(500) NULL,
    SubmittedAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_DisputeEvidence_DisputeTicket FOREIGN KEY (DisputeTicketId) REFERENCES DisputeTicket(DisputeTicketId),
    CONSTRAINT FK_DisputeEvidence_SubmittedByUser FOREIGN KEY (SubmittedByUserId) REFERENCES [User](UserId)
);
GO

-- 41. Bảng RefundTransaction
CREATE TABLE RefundTransaction (
    RefundTransactionId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PaymentTransactionId BIGINT NOT NULL,
    DisputeTicketId BIGINT NULL,
    ContractTerminationRequestId BIGINT NULL,
    ProcessedByUserId BIGINT NOT NULL, -- User xử lý hoàn tiền (1 - 0..*)
    RefundCode VARCHAR(100) NOT NULL UNIQUE,
    Amount DECIMAL(18,2) NOT NULL,
    Reason NVARCHAR(500) NULL,
    RefundStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    ProcessedAt DATETIME2 NULL,
    GatewayReference VARCHAR(150) NULL,
    CONSTRAINT FK_RefundTransaction_PaymentTransaction FOREIGN KEY (PaymentTransactionId) REFERENCES PaymentTransaction(PaymentTransactionId),
    CONSTRAINT FK_RefundTransaction_DisputeTicket FOREIGN KEY (DisputeTicketId) REFERENCES DisputeTicket(DisputeTicketId),
    CONSTRAINT FK_RefundTransaction_TerminationRequest FOREIGN KEY (ContractTerminationRequestId) REFERENCES ContractTerminationRequest(ContractTerminationRequestId),
    CONSTRAINT FK_RefundTransaction_ProcessedByUser FOREIGN KEY (ProcessedByUserId) REFERENCES [User](UserId)
);
GO

-- 42. Bảng CommissionPolicy
CREATE TABLE CommissionPolicy (
    CommissionPolicyId BIGINT IDENTITY(1,1) PRIMARY KEY,
    CategoryId BIGINT NOT NULL,
    ApplicableOrderType VARCHAR(50) NOT NULL,
    CommissionRate DECIMAL(5,2) NOT NULL,
    EffectiveDate DATE NOT NULL,
    Note NVARCHAR(500) NULL,
    IsActive BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_CommissionPolicy_Category FOREIGN KEY (CategoryId) REFERENCES Category(CategoryId)
);
GO

-- 43. Bảng FarmerPayout
CREATE TABLE FarmerPayout (
    FarmerPayoutId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerProfileId BIGINT NOT NULL,
    ApprovedByUserId BIGINT NULL, -- User duyệt quyết toán (0..1 - 0..*)
    PayoutCode VARCHAR(100) NOT NULL UNIQUE,
    Amount DECIMAL(18,2) NOT NULL,
    DeductedCommission DECIMAL(18,2) NOT NULL DEFAULT 0,
    NetAmount DECIMAL(18,2) NOT NULL,
    PayoutStatus VARCHAR(50) NOT NULL DEFAULT 'Pending',
    BankTransferRef VARCHAR(150) NULL,
    ProcessedAt DATETIME2 NULL,
    ApprovedAt DATETIME2 NULL,
    EligibleAt DATETIME2 NULL,
    CONSTRAINT FK_FarmerPayout_FarmerProfile FOREIGN KEY (FarmerProfileId) REFERENCES FarmerProfile(FarmerProfileId),
    CONSTRAINT FK_FarmerPayout_ApprovedByUser FOREIGN KEY (ApprovedByUserId) REFERENCES [User](UserId)
);
GO

-- 44. Bảng PayoutItem
CREATE TABLE PayoutItem (
    PayoutItemId BIGINT IDENTITY(1,1) PRIMARY KEY,
    FarmerPayoutId BIGINT NOT NULL,
    OrderItemId BIGINT NULL,
    ContractMilestoneId BIGINT NULL,
    CommissionPolicyId BIGINT NULL,
    GrossAmount DECIMAL(18,2) NOT NULL,
    CommissionRateSnapshot DECIMAL(5,2) NOT NULL DEFAULT 0,
    CommissionAmount DECIMAL(18,2) NOT NULL DEFAULT 0,
    NetAmount DECIMAL(18,2) NOT NULL,
    SourceAmountSettled DECIMAL(18,2) NOT NULL,
    CONSTRAINT FK_PayoutItem_FarmerPayout FOREIGN KEY (FarmerPayoutId) REFERENCES FarmerPayout(FarmerPayoutId),
    CONSTRAINT FK_PayoutItem_OrderItem FOREIGN KEY (OrderItemId) REFERENCES OrderItem(OrderItemId),
    CONSTRAINT FK_PayoutItem_ContractMilestone FOREIGN KEY (ContractMilestoneId) REFERENCES ContractMilestone(ContractMilestoneId),
    CONSTRAINT FK_PayoutItem_CommissionPolicy FOREIGN KEY (CommissionPolicyId) REFERENCES CommissionPolicy(CommissionPolicyId)
);
GO

-- 45. Bảng Notification
CREATE TABLE [Notification] (
    NotificationId BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId BIGINT NOT NULL,
    Title NVARCHAR(255) NOT NULL,
    Content NVARCHAR(MAX) NOT NULL,
    NotificationType VARCHAR(50) NOT NULL,
    IsRead BIT NOT NULL DEFAULT 0,
    SentAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    DeliveryChannel VARCHAR(50) NOT NULL,
    DeliveryStatus VARCHAR(50) NOT NULL DEFAULT 'Sent',
    RetryCount INT NOT NULL DEFAULT 0,
    RelatedRecordType VARCHAR(100) NULL,
    CONSTRAINT FK_Notification_User FOREIGN KEY (UserId) REFERENCES [User](UserId)
);
GO

-- 46. Bảng AuditLog
CREATE TABLE AuditLog (
    AuditLogId BIGINT IDENTITY(1,1) PRIMARY KEY,
    PerformedByUserId BIGINT NULL, -- Người thực hiện hoặc NULL nếu là Hệ thống (0..1 - 0..*)
    EntityType VARCHAR(100) NOT NULL,
    [Action] VARCHAR(100) NOT NULL,
    OldValue NVARCHAR(MAX) NULL,
    NewValue NVARCHAR(MAX) NULL,
    Reason NVARCHAR(500) NULL,
    OccurredAt DATETIME2 NOT NULL DEFAULT SYSDATETIME(),
    CONSTRAINT FK_AuditLog_PerformedByUser FOREIGN KEY (PerformedByUserId) REFERENCES [User](UserId)
);
GO

-- =========================================================================
-- RÀNG BUỘC KHÓA NGOẠI LIÊN PHÂN HỆ (CROSS-MODULE FOREIGN KEYS)
-- =========================================================================

-- 1. InventoryMovement (Phân hệ 2) -> OrderItemBatchAllocation (Phân hệ 3)
ALTER TABLE InventoryMovement
ADD CONSTRAINT FK_InventoryMovement_BatchAllocation
FOREIGN KEY (OrderItemBatchAllocationId) REFERENCES OrderItemBatchAllocation(OrderItemBatchAllocationId);
GO

-- 2. SellerOrder (Phân hệ 3) -> B2BContract (Phân hệ 4)
ALTER TABLE SellerOrder
ADD CONSTRAINT FK_SellerOrder_B2BContract
FOREIGN KEY (B2BContractId) REFERENCES B2BContract(B2BContractId);
GO

-- 3. OrderItem (Phân hệ 3) -> ContractLine (Phân hệ 4)
ALTER TABLE OrderItem
ADD CONSTRAINT FK_OrderItem_ContractLine
FOREIGN KEY (ContractLineId) REFERENCES ContractLine(ContractLineId);
GO

-- 4. Shipment (Phân hệ 3) -> ContractMilestone (Phân hệ 4)
ALTER TABLE Shipment
ADD CONSTRAINT FK_Shipment_ContractMilestone
FOREIGN KEY (ContractMilestoneId) REFERENCES ContractMilestone(ContractMilestoneId);
GO
GO
/* SQLCMD mode: pass -v TargetDatabase="AgriSustain" and select the same database with -d.
   Upgrade ONLY the supplied 46-table BIGINT schema. No legacy data is rewritten.
   Re-runnable; all schema changes commit together. Read ../README.md for decisions.
   DB-06 request-key/unique-quote and DB-09 reward tables are deliberately deferred.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;


IF OBJECT_ID(N'dbo.[User]', N'U') IS NULL
    OR OBJECT_ID(N'dbo.Category', N'U') IS NULL
    OR OBJECT_ID(N'dbo.[Order]', N'U') IS NULL
    OR OBJECT_ID(N'dbo.HarvestBatch', N'U') IS NULL
    OR OBJECT_ID(N'dbo.CartItem', N'U') IS NULL
    OR OBJECT_ID(N'dbo.OrderItemBatchAllocation', N'U') IS NULL
    OR OBJECT_ID(N'dbo.VoucherRedemption', N'U') IS NULL
    OR OBJECT_ID(N'dbo.AuditLog', N'U') IS NULL
    THROW 51001, 'Expected the reviewed User/Category/Order 46-table schema, not Accounts/Categories/Orders.', 1;

IF NOT EXISTS (SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID(N'dbo.[User]') AND name = N'UserId'
      AND TYPE_NAME(user_type_id) = N'bigint')
    THROW 51002, 'Expected BIGINT UserId from the supplied baseline.', 1;

BEGIN TRY
    BEGIN TRANSACTION;

    IF EXISTS (SELECT 1 FROM dbo.HarvestBatch
        WHERE AvailableQuantity < 0 OR ReservedQuantity < 0)
        THROW 51003, 'Reconcile negative batch quantities before migration.', 1;

    -- DB-08: the snapshot includes FEFO allocations, NOT a stock reservation.
    IF OBJECT_ID(N'dbo.RetailCheckoutQuote', N'U') IS NULL
        EXEC(N'CREATE TABLE dbo.RetailCheckoutQuote
        (
            QuoteId uniqueidentifier NOT NULL CONSTRAINT PK_RetailCheckoutQuote PRIMARY KEY,
            BuyerUserId bigint NOT NULL,
            AcceptedSnapshotJson nvarchar(max) NOT NULL,
            Fingerprint char(64) NOT NULL,
            CreatedAt datetime2 NOT NULL
                CONSTRAINT DF_RetailQuote_Created DEFAULT SYSUTCDATETIME(),
            ExpiresAt datetime2 NOT NULL,
            CONSTRAINT CK_RetailQuote_Json CHECK (ISJSON(AcceptedSnapshotJson) = 1),
            CONSTRAINT CK_RetailQuote_Expiry CHECK (ExpiresAt > CreatedAt),
            CONSTRAINT FK_RetailQuote_Buyer FOREIGN KEY (BuyerUserId)
                REFERENCES dbo.[User](UserId)
        )');

    -- DB-07: NULL means an unknown legacy snapshot, never invented historical values.
    -- DB-09: point fields are order snapshots, not a balance/loyalty implementation.
    CREATE TABLE #ColumnsToAdd
    (
        TableName sysname NOT NULL,
        ColumnName sysname NOT NULL,
        ColumnDefinition nvarchar(128) NOT NULL
    );
    INSERT INTO #ColumnsToAdd (TableName, ColumnName, ColumnDefinition) VALUES
        (N'Order', N'RetailQuoteId', N'uniqueidentifier NULL'),
        (N'Order', N'DeliveryMethod', N'varchar(50) NULL'),
        (N'Order', N'PaymentMethod', N'varchar(50) NULL'),
        (N'Order', N'OrderNotes', N'nvarchar(300) NULL'),
        (N'Order', N'VoucherCodeSnapshot', N'varchar(100) NULL'),
        (N'Order', N'VoucherDiscount', N'decimal(18,2) NULL'),
        (N'Order', N'PointDiscount', N'decimal(18,2) NULL'),
        (N'Order', N'RedeemedPoints', N'int NULL'),
        (N'Order', N'ProvinceCodeSnapshot', N'varchar(20) NULL'),
        (N'Order', N'ProvinceNameSnapshot', N'nvarchar(100) NULL'),
        (N'Order', N'WardCodeSnapshot', N'varchar(20) NULL'),
        (N'Order', N'WardNameSnapshot', N'nvarchar(100) NULL'),
        (N'Order', N'AddressDetailSnapshot', N'nvarchar(255) NULL'),
        (N'Order', N'AddressCatalogVersionSnapshot', N'varchar(100) NULL'),
        (N'SellerOrder', N'SellerNameSnapshot', N'nvarchar(200) NULL'),
        (N'OrderItem', N'ProductNameSnapshot', N'nvarchar(255) NULL'),
        (N'DeliveryAddress', N'ProvinceCode', N'varchar(20) NULL'),
        (N'DeliveryAddress', N'WardCode', N'varchar(20) NULL'),
        (N'DeliveryAddress', N'AddressCatalogVersion', N'varchar(100) NULL'),
        (N'VoucherRedemption', N'ExpiresAt', N'datetime2 NULL'),
        (N'AuditLog', N'EntityId', N'bigint NULL'),
        (N'AuditLog', N'TraceId', N'nvarchar(64) NULL');

    DECLARE @TableName sysname, @ColumnName sysname, @Definition nvarchar(128);
    DECLARE @Sql nvarchar(max);
    DECLARE column_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT TableName, ColumnName, ColumnDefinition FROM #ColumnsToAdd;
    OPEN column_cursor;
    FETCH NEXT FROM column_cursor INTO @TableName, @ColumnName, @Definition;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF COL_LENGTH(N'dbo.' + QUOTENAME(@TableName), @ColumnName) IS NULL
        BEGIN
            SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName)
                + N' ADD ' + QUOTENAME(@ColumnName) + N' ' + @Definition;
            EXEC sys.sp_executesql @Sql;
        END;
        FETCH NEXT FROM column_cursor INTO @TableName, @ColumnName, @Definition;
    END;
    CLOSE column_cursor;
    DEALLOCATE column_cursor;
    DROP TABLE #ColumnsToAdd;

    -- Two-level province/ward addresses. Preserve existing District text verbatim.
    IF EXISTS (SELECT 1 FROM sys.columns
        WHERE object_id = OBJECT_ID(N'dbo.DeliveryAddress') AND name = N'District'
          AND is_nullable = 0)
        ALTER TABLE dbo.DeliveryAddress ALTER COLUMN District nvarchar(100) NULL;

    IF OBJECT_ID(N'dbo.FK_Order_RetailQuote', N'F') IS NULL
        EXEC(N'ALTER TABLE dbo.[Order] WITH CHECK ADD CONSTRAINT FK_Order_RetailQuote
            FOREIGN KEY (RetailQuoteId) REFERENCES dbo.RetailCheckoutQuote(QuoteId)');

    -- DB-11. Conditional stock updates and transactions remain application duties.
    IF OBJECT_ID(N'dbo.CK_HarvestBatch_Nonnegative', N'C') IS NULL
        ALTER TABLE dbo.HarvestBatch WITH CHECK ADD CONSTRAINT CK_HarvestBatch_Nonnegative
            CHECK (AvailableQuantity >= 0 AND ReservedQuantity >= 0);

    -- DB-05 B: existing OrderItemBatchAllocation supports multiple batches per line.
    -- FEFO allocation/revalidation is application logic; no CartItem batch column.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.HarvestBatch') AND name = N'IX_HarvestBatch_Fefo')
        CREATE INDEX IX_HarvestBatch_Fefo
            ON dbo.HarvestBatch(ProductId, [Status], ExpirationDate, HarvestBatchId)
            INCLUDE (AvailableQuantity, ReservedQuantity, BatchCode);

    -- DB-10/12: supports quota queries. It does not itself reserve/release a voucher.
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.VoucherRedemption')
          AND name = N'IX_VoucherRedemption_Quota')
        EXEC(N'CREATE INDEX IX_VoucherRedemption_Quota
            ON dbo.VoucherRedemption(VoucherId, [Status], ExpiresAt) INCLUDE (OrderId)');
    IF NOT EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.RetailCheckoutQuote')
          AND name = N'IX_RetailQuote_Buyer_Expiry')
        EXEC(N'CREATE INDEX IX_RetailQuote_Buyer_Expiry
            ON dbo.RetailCheckoutQuote(BuyerUserId, ExpiresAt)');

    -- DB-15: change defaults only for timestamps read/written by these three UCs.
    -- Existing values and DATE-only harvest/expiration fields are never converted.
    DECLARE @DefaultName sysname;
    DECLARE default_cursor CURSOR LOCAL FAST_FORWARD FOR
        SELECT t.name, c.name, d.name
        FROM sys.default_constraints d
        JOIN sys.columns c ON c.object_id = d.parent_object_id
            AND c.column_id = d.parent_column_id
        JOIN sys.tables t ON t.object_id = c.object_id
        JOIN sys.schemas s ON s.schema_id = t.schema_id
        WHERE s.name = N'dbo' AND LOWER(d.definition) LIKE N'%sysdatetime()%'
          AND t.name IN (N'User', N'Product', N'Cart', N'CartItem', N'InventoryMovement',
              N'Order', N'OrderItemBatchAllocation', N'VoucherRedemption', N'AuditLog');
    OPEN default_cursor;
    FETCH NEXT FROM default_cursor INTO @TableName, @ColumnName, @DefaultName;
    WHILE @@FETCH_STATUS = 0
    BEGIN
        SET @Sql = N'ALTER TABLE dbo.' + QUOTENAME(@TableName)
            + N' DROP CONSTRAINT ' + QUOTENAME(@DefaultName) + N'; ALTER TABLE dbo.'
            + QUOTENAME(@TableName) + N' ADD CONSTRAINT ' + QUOTENAME(@DefaultName)
            + N' DEFAULT SYSUTCDATETIME() FOR ' + QUOTENAME(@ColumnName);
        EXEC sys.sp_executesql @Sql;
        FETCH NEXT FROM default_cursor INTO @TableName, @ColumnName, @DefaultName;
    END;
    CLOSE default_cursor;
    DEALLOCATE default_cursor;

    -- Category: existing NO ACTION FKs already block deletion with products/other refs.
    -- Application must use IsActive=false if products exist and audit successful DELETE.
    -- Do not add image/archive/name-unique/Version columns or constraints (DB-01..04).
    -- DB-14 auth sessions, DB-16 staff permission matrix and subscription commission
    -- integration are external contracts; do not invent schema or rewrite legacy roles.
    -- Retail writers explicitly supply OrderType=Retail and OrderStatus=PendingPayment;
    -- leave the shared Pending default intact for other UCs.
    COMMIT TRANSACTION;
    SELECT DB_NAME() AS DatabaseName, N'20261018_approved_three_uc' AS Migration,
        N'Applied' AS Result;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
