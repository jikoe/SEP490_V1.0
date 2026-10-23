:On Error exit
/*
  Synthetic local demonstration data for UC-ADM-03, UC-ADM-05 and UC-RC-02.
  Run AFTER the reviewed 47-table schema/migration. SQLCMD variables are mandatory.
  This script inserts missing samples only: it never resets accounts, stock or carts.
  Every lookup uses a semantic seed key, not an assumed IDENTITY sequence.
  PasswordHash is deliberately unusable: the missing login module remains a mock.
  No reward balance, fee policy, accepted quote, payment or completed order is invented.

  sqlcmd -S "(localdb)\MSSQLLocalDB" -d AgriSustain -E -b -f 65001 ^
    -v TargetDatabase="AgriSustain" -i seed-three-uc-demo.sql
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET LOCK_TIMEOUT 15000;
IF DB_NAME() <> N'$(TargetDatabase)' OR DB_NAME() IN ('master','model','msdb','tempdb')
    THROW 51100, 'Select the explicitly named application database.', 1;
IF OBJECT_ID('dbo.RetailCheckoutQuote','U') IS NULL
    OR COL_LENGTH('dbo.DeliveryAddress','ProvinceCode') IS NULL
    THROW 51101, 'Apply the approved 47-table schema before seeding.', 1;

DECLARE @Now datetime2 = SYSUTCDATETIME();
DECLARE @Today date = CONVERT(date, @Now);
BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @LockResult int;
    EXEC @LockResult = sys.sp_getapplock @Resource=N'AgriSustain.ThreeUcDemoSeed',
        @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=15000;
    IF @LockResult < 0 THROW 51102, 'Another seed process holds the demonstration lock.', 1;

    DECLARE @Users TABLE
    (
        SeedKey nvarchar(100) PRIMARY KEY, FullName nvarchar(150),
        RoleName varchar(50), StatusName varchar(50), LockHours int, Failures int
    );
    INSERT INTO @Users VALUES
        (N'demo3uc_admin',N'Quản trị viên demo','SystemAdmin','Active',0,0),
        (N'demo3uc_admin_backup',N'Quản trị viên dự phòng','SystemAdmin','Active',0,0),
        (N'demo3uc_retail',N'Khách mua demo','RetailCustomer','Active',0,0),
        (N'demo3uc_retail_other',N'Khách mua đối chứng','RetailCustomer','Active',0,0),
        (N'demo3uc_locked',N'Tài khoản khóa tạm','RetailCustomer','Active',24,5),
        (N'demo3uc_support',N'Hỗ trợ khách hàng demo','CustomerSupport','Active',0,0),
        (N'demo3uc_farmer_green',N'HTX Rau Xanh','Farmer','Active',0,0),
        (N'demo3uc_farmer_gold',N'Nông trại Mùa Vàng','Farmer','Active',0,0),
        (N'demo3uc_wholesale',N'Doanh nghiệp thu mua','WholesaleBuyer','Active',0,0),
        (N'demo3uc_suspended',N'Tài khoản tạm ngưng','RetailCustomer','Suspended',0,0);
    INSERT INTO dbo.[User]
        (Username,Email,PasswordHash,FullName,PhoneNumber,Role,Status,
         IsEmailVerified,CreatedAt,FailedLoginCount,LockedUntil)
    SELECT s.SeedKey,s.SeedKey+N'@example.test',N'!DEMO-NO-LOGIN-HASH!',s.FullName,
        '0901234567',s.RoleName,s.StatusName,1,DATEADD(day,-10,@Now),s.Failures,
        CASE WHEN s.LockHours>0 THEN DATEADD(hour,s.LockHours,@Now) END
    FROM @Users s WHERE NOT EXISTS
        (SELECT 1 FROM dbo.[User] u WHERE u.Username=s.SeedKey);

    -- Only these two seller read models are supplied; farmer management is not implemented.
    INSERT INTO dbo.FarmerProfile
        (UserId,FarmName,RepresentativeName,Address,ProvinceCode,
         VerificationStatus,ApprovedAt,ReputationScore)
    SELECT u.UserId,u.FullName,N'Đại diện dữ liệu mẫu',N'Địa chỉ mô phỏng cho ba UC',
        '12','Approved',@Now,4.50
    FROM dbo.[User] u WHERE u.Username IN
        (N'demo3uc_farmer_green',N'demo3uc_farmer_gold')
      AND NOT EXISTS (SELECT 1 FROM dbo.FarmerProfile f WHERE f.UserId=u.UserId);

    DECLARE @Categories TABLE
    (SeedKey nvarchar(100) PRIMARY KEY,Name nvarchar(150),ParentKey nvarchar(100),
        Priority int,IsActive bit);
    INSERT INTO @Categories VALUES
        (N'vegetables',N'[DEMO] Rau củ',NULL,1,1),
        (N'fruit',N'[DEMO] Trái cây',NULL,2,1),
        (N'leaf',N'[DEMO] Rau ăn lá',N'vegetables',3,1),
        (N'unused',N'[DEMO] Danh mục chưa dùng',NULL,4,1),
        (N'hidden',N'[DEMO] Danh mục đang ẩn',NULL,5,0),
        (N'unpublished',N'[DEMO] Có sản phẩm chưa công bố',NULL,6,1);
    INSERT INTO dbo.Category(CategoryName,Description,Priority,IsActive)
    SELECT s.Name,N'DEMO-THREE-UC:'+s.SeedKey,s.Priority,s.IsActive
    FROM @Categories s WHERE s.ParentKey IS NULL AND NOT EXISTS
        (SELECT 1 FROM dbo.Category c WHERE c.Description=N'DEMO-THREE-UC:'+s.SeedKey);
    INSERT INTO dbo.Category(ParentCategoryId,CategoryName,Description,Priority,IsActive)
    SELECT parent.CategoryId,s.Name,N'DEMO-THREE-UC:'+s.SeedKey,s.Priority,s.IsActive
    FROM @Categories s JOIN dbo.Category parent
        ON parent.Description=N'DEMO-THREE-UC:'+s.ParentKey
    WHERE s.ParentKey IS NOT NULL AND NOT EXISTS
        (SELECT 1 FROM dbo.Category c WHERE c.Description=N'DEMO-THREE-UC:'+s.SeedKey);

    -- HarvestBatch requires a real CropSeason FK. These are static prerequisite fixtures.
    INSERT INTO dbo.CropSeason
        (FarmerProfileId,SeasonName,CropVariety,CultivatedArea,PlantingDate,
         EstimatedHarvestDate,ActualHarvestDate,EstimatedYield,ActualYield,Status)
    SELECT f.FarmerProfileId,N'DEMO-THREE-UC: season',N'Giống trồng mô phỏng',1,
        DATEADD(day,-60,@Today),@Today,@Today,500,500,'Harvested'
    FROM dbo.FarmerProfile f JOIN dbo.[User] u ON u.UserId=f.UserId
    WHERE u.Username IN (N'demo3uc_farmer_green',N'demo3uc_farmer_gold')
      AND NOT EXISTS (SELECT 1 FROM dbo.CropSeason cs
          WHERE cs.FarmerProfileId=f.FarmerProfileId
            AND cs.SeasonName=N'DEMO-THREE-UC: season');

    DECLARE @Products TABLE
    (SeedKey nvarchar(100) PRIMARY KEY,Name nvarchar(255),CategoryKey nvarchar(100),
        SellerKey nvarchar(100),Price decimal(18,2),TierQty decimal(18,3),
        TierPrice decimal(18,2),Approved varchar(50),Published bit);
    INSERT INTO @Products VALUES
        (N'greens',N'Cải ngọt hữu cơ',N'leaf',N'demo3uc_farmer_green',30000,5,25000,'Approved',1),
        (N'orange',N'Cam sành',N'fruit',N'demo3uc_farmer_gold',40000,10,35000,'Approved',1),
        (N'carrot',N'Cà rốt',N'vegetables',N'demo3uc_farmer_green',20000,NULL,NULL,'Approved',1),
        (N'pending',N'Rau mẫu chưa công bố',N'unpublished',N'demo3uc_farmer_green',15000,NULL,NULL,'Pending',0),
        (N'expired',N'Rau mẫu chỉ có lô hết hạn',N'leaf',N'demo3uc_farmer_green',10000,NULL,NULL,'Approved',1);
    INSERT INTO dbo.Product
        (CategoryId,FarmerProfileId,ProductName,Description,Unit,OriginProvince,
         BaseRetailPrice,WholesaleMinQuantity,WholesalePrice,ApprovalStatus,IsPublished,CreatedAt)
    SELECT c.CategoryId,f.FarmerProfileId,s.Name,N'DEMO-THREE-UC:'+s.SeedKey,N'kg',
        N'Thành phố Hồ Chí Minh',s.Price,s.TierQty,s.TierPrice,s.Approved,s.Published,@Now
    FROM @Products s JOIN dbo.Category c
        ON c.Description=N'DEMO-THREE-UC:'+s.CategoryKey
    JOIN dbo.[User] u ON u.Username=s.SellerKey
    JOIN dbo.FarmerProfile f ON f.UserId=u.UserId
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Product p
        WHERE p.Description=N'DEMO-THREE-UC:'+s.SeedKey);

    DECLARE @Batches TABLE
    (Code varchar(100) PRIMARY KEY,ProductKey nvarchar(100),Quantity decimal(18,3),ExpiryDays int);
    INSERT INTO @Batches VALUES
        ('DEMO3UC-RAU-FIRST',N'greens',3,10),
        ('DEMO3UC-RAU-SECOND',N'greens',20,20),
        ('DEMO3UC-CAM',N'orange',20,30),
        ('DEMO3UC-CAROT',N'carrot',20,30),
        ('DEMO3UC-EXPIRED',N'expired',10,-1);
    INSERT INTO dbo.HarvestBatch
        (ProductId,CropSeasonId,BatchCode,ActualHarvestDate,HarvestYield,
         ExpirationDate,Status,StockEnteredAt,AvailableQuantity,ReservedQuantity,QualityNotes)
    SELECT p.ProductId,cs.CropSeasonId,s.Code,@Today,s.Quantity,
        DATEADD(day,s.ExpiryDays,@Today),'Available',@Now,s.Quantity,0,
        N'DEMO-THREE-UC: static inventory prerequisite'
    FROM @Batches s JOIN dbo.Product p ON p.Description=N'DEMO-THREE-UC:'+s.ProductKey
    JOIN dbo.CropSeason cs ON cs.FarmerProfileId=p.FarmerProfileId
        AND cs.SeasonName=N'DEMO-THREE-UC: season'
    WHERE NOT EXISTS (SELECT 1 FROM dbo.HarvestBatch b WHERE b.BatchCode=s.Code);

    -- On a rerun, an existing buyer cart is not replenished after checkout.
    DECLARE @NewCarts TABLE(CartId bigint,UserId bigint);
    INSERT INTO dbo.Cart(UserId,UpdatedAt) OUTPUT inserted.CartId,inserted.UserId INTO @NewCarts
    SELECT u.UserId,@Now FROM dbo.[User] u
    WHERE u.Username IN (N'demo3uc_retail',N'demo3uc_retail_other')
      AND NOT EXISTS (SELECT 1 FROM dbo.Cart c WHERE c.UserId=u.UserId);
    INSERT INTO dbo.CartItem(CartId,ProductId,Quantity,IsSelected,AddedAt)
    SELECT c.CartId,p.ProductId,s.Quantity,s.Selected,@Now
    FROM @NewCarts c JOIN dbo.[User] u ON u.UserId=c.UserId
    CROSS APPLY (VALUES (N'greens',CONVERT(decimal(18,3),5),CONVERT(bit,1)),
        (N'orange',CONVERT(decimal(18,3),2),CONVERT(bit,1)),
        (N'carrot',CONVERT(decimal(18,3),1),CONVERT(bit,0))) s(ProductKey,Quantity,Selected)
    JOIN dbo.Product p ON p.Description=N'DEMO-THREE-UC:'+s.ProductKey
    WHERE u.Username=N'demo3uc_retail' OR s.ProductKey=N'greens';

    INSERT INTO dbo.DeliveryAddress
        (UserId,RecipientName,RecipientPhone,AddressDetail,ProvinceCity,District,Ward,
         IsDefault,Status,ProvinceCode,WardCode,AddressCatalogVersion)
    SELECT u.UserId,u.FullName,'0901234567',N'123 Đường Mẫu',N'Thành phố Hồ Chí Minh',
        NULL,N'Xã Thạnh An',1,'Active','12','268',NULL
    FROM dbo.[User] u WHERE u.Username IN (N'demo3uc_retail',N'demo3uc_retail_other')
      AND NOT EXISTS (SELECT 1 FROM dbo.DeliveryAddress a WHERE a.UserId=u.UserId);

    INSERT INTO dbo.Voucher
        (FarmerProfileId,VoucherCode,DiscountType,DiscountValue,MaxDiscountAmount,
         MinOrderAmount,UsageLimit,UsedCount,StartDate,EndDate,IsActive)
    SELECT NULL,'DEMO3UC-WELCOME10','Percent',10,50000,100000,100,0,
        DATEADD(day,-1,@Now),DATEADD(day,30,@Now),1
    WHERE NOT EXISTS (SELECT 1 FROM dbo.Voucher WHERE VoucherCode='DEMO3UC-WELCOME10');
    INSERT INTO dbo.Voucher
        (FarmerProfileId,VoucherCode,DiscountType,DiscountValue,MaxDiscountAmount,
         MinOrderAmount,UsageLimit,UsedCount,StartDate,EndDate,IsActive)
    SELECT f.FarmerProfileId,'DEMO3UC-RAUXANH20','Fixed',20000,NULL,100000,20,0,
        DATEADD(day,-1,@Now),DATEADD(day,30,@Now),1
    FROM dbo.FarmerProfile f JOIN dbo.[User] u ON u.UserId=f.UserId
    WHERE u.Username=N'demo3uc_farmer_green' AND NOT EXISTS
        (SELECT 1 FROM dbo.Voucher WHERE VoucherCode='DEMO3UC-RAUXANH20');

    COMMIT TRANSACTION;
    SELECT N'User' AS TableName,COUNT_BIG(*) AS SampleRows FROM dbo.[User]
        WHERE Username LIKE N'demo3uc[_]%'
    UNION ALL SELECT N'Category',COUNT_BIG(*) FROM dbo.Category
        WHERE Description LIKE N'DEMO-THREE-UC:%'
    UNION ALL SELECT N'Product',COUNT_BIG(*) FROM dbo.Product
        WHERE Description LIKE N'DEMO-THREE-UC:%'
    UNION ALL SELECT N'HarvestBatch',COUNT_BIG(*) FROM dbo.HarvestBatch
        WHERE BatchCode LIKE 'DEMO3UC-%';
END TRY
BEGIN CATCH
    IF XACT_STATE() <> 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
