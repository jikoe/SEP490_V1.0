/* SQLCMD, run after fresh install or migration. Synthetic rows are rolled back.
   Validates schema/SQL behavior; does not test the app's still-mocked persistence.
*/
SET NOCOUNT ON;
SET XACT_ABORT OFF;
IF DB_NAME() <> N'$(TargetDatabase)'
    THROW 52000, 'Unexpected verification database.', 1;

IF (SELECT COUNT(*) FROM sys.tables WHERE is_ms_shipped = 0) <> 47
    THROW 52001, 'Expected 46 original tables plus RetailCheckoutQuote.', 1;
IF COL_LENGTH(N'dbo.Category', N'ImageStorageKey') IS NOT NULL
    OR COL_LENGTH(N'dbo.Category', N'NormalizedName') IS NOT NULL
    OR COL_LENGTH(N'dbo.Category', N'Version') IS NOT NULL
    OR COL_LENGTH(N'dbo.Category', N'IsArchived') IS NOT NULL
    OR COL_LENGTH(N'dbo.[User]', N'Version') IS NOT NULL
    OR COL_LENGTH(N'dbo.CartItem', N'HarvestBatchId') IS NOT NULL
    OR COL_LENGTH(N'dbo.[Order]', N'RetailRequestKey') IS NOT NULL
    OR OBJECT_ID(N'dbo.RewardAccount', N'U') IS NOT NULL
    OR OBJECT_ID(N'dbo.RewardPointReservation', N'U') IS NOT NULL
    THROW 52002, 'An excluded or deferred schema change is present.', 1;
IF EXISTS (SELECT 1 FROM sys.foreign_keys WHERE is_disabled = 1 OR is_not_trusted = 1)
    OR EXISTS (SELECT 1 FROM sys.check_constraints WHERE is_disabled = 1 OR is_not_trusted = 1)
    THROW 52003, 'Constraints must remain enabled and trusted.', 1;
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.DeliveryAddress')
    AND name = N'District' AND is_nullable = 0)
    THROW 52004, 'District must not be mandatory for two-level addresses.', 1;
IF EXISTS (SELECT 1 FROM sys.default_constraints d JOIN sys.tables t
    ON t.object_id = d.parent_object_id
    WHERE t.name IN (N'User', N'Product', N'Cart', N'CartItem', N'InventoryMovement',
        N'Order', N'OrderItemBatchAllocation', N'VoucherRedemption', N'AuditLog')
      AND LOWER(d.definition) LIKE N'%sysdatetime()%')
    THROW 52005, 'In-scope timestamp defaults must use UTC.', 1;

-- Use RunFixtures=0 on the application database to avoid identity-counter advances.
-- Use RunFixtures=1 only on an isolated verification database; all fixture rows roll back.
IF N'$(RunFixtures)' <> N'1'
BEGIN
    SELECT DB_NAME() AS DatabaseName, N'PASS: read-only schema verification' AS Result;
    RETURN;
END;

BEGIN TRY
    BEGIN TRANSACTION;
    DECLARE @Tag varchar(36) = CONVERT(varchar(36), NEWID());
    INSERT dbo.[User] (Username, Email, PasswordHash, FullName, [Role])
        VALUES (@Tag, @Tag + '@schema.invalid', N'NOT-A-CREDENTIAL', N'Schema fixture',
            'RetailCustomer');
    DECLARE @UserId bigint = SCOPE_IDENTITY();
    IF NOT EXISTS (SELECT 1 FROM dbo.[User] WHERE UserId = @UserId
        AND ABS(DATEDIFF(second, CreatedAt, SYSUTCDATETIME())) < 15)
        THROW 52006, 'New User.CreatedAt is not UTC.', 1;

    INSERT dbo.FarmerProfile (UserId, FarmName, RepresentativeName, [Address], ProvinceCode)
        VALUES (@UserId, N'Schema farm', N'Schema farmer', N'Synthetic only', '12');
    DECLARE @FarmerId bigint = SCOPE_IDENTITY();
    INSERT dbo.Category (CategoryName) VALUES (N'Schema category ' + @Tag);
    DECLARE @CategoryId bigint = SCOPE_IDENTITY();
    INSERT dbo.Product (CategoryId, FarmerProfileId, ProductName, Unit, BaseRetailPrice)
        VALUES (@CategoryId, @FarmerId, N'Schema product', N'kg', 10000);
    DECLARE @ProductId bigint = SCOPE_IDENTITY();
    INSERT dbo.CropSeason (FarmerProfileId, SeasonName, CropVariety, CultivatedArea,
        PlantingDate, EstimatedHarvestDate, EstimatedYield)
        VALUES (@FarmerId, N'Schema season', N'Schema crop', 1, '20260101', '20261231', 23);
    DECLARE @SeasonId bigint = SCOPE_IDENTITY();
    INSERT dbo.HarvestBatch (ProductId, CropSeasonId, BatchCode, ExpirationDate,
        AvailableQuantity) VALUES (@ProductId, @SeasonId, @Tag + '-A', '20990101', 3);
    DECLARE @BatchA bigint = SCOPE_IDENTITY();
    INSERT dbo.HarvestBatch (ProductId, CropSeasonId, BatchCode, ExpirationDate,
        AvailableQuantity) VALUES (@ProductId, @SeasonId, @Tag + '-B', '20990201', 20);
    DECLARE @BatchB bigint = SCOPE_IDENTITY();

    BEGIN TRY
        UPDATE dbo.HarvestBatch SET AvailableQuantity = -1 WHERE HarvestBatchId = @BatchA;
        THROW 52007, 'Negative batch stock should be rejected.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;

    BEGIN TRY
        DELETE dbo.Category WHERE CategoryId = @CategoryId;
        THROW 52008, 'Category with an unpublished product should not be deleted.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;
    UPDATE dbo.Category SET IsActive = 0 WHERE CategoryId = @CategoryId;
    IF @@ROWCOUNT <> 1 THROW 52009, 'Referenced category can be hidden.', 1;
    INSERT dbo.Category (CategoryName) VALUES (N'Deletable ' + @Tag);
    DECLARE @UnusedCategory bigint = SCOPE_IDENTITY();
    DELETE dbo.Category WHERE CategoryId = @UnusedCategory;
    IF @@ROWCOUNT <> 1 THROW 52010, 'Unreferenced category should be deletable.', 1;

    DECLARE @QuoteId uniqueidentifier = NEWID();
    INSERT dbo.RetailCheckoutQuote (QuoteId, BuyerUserId, AcceptedSnapshotJson,
        Fingerprint, ExpiresAt)
        VALUES (@QuoteId, @UserId, N'{"fixture":true}', REPLICATE('a', 64),
            DATEADD(minute, 1, SYSUTCDATETIME()));
    BEGIN TRY
        INSERT dbo.RetailCheckoutQuote (QuoteId, BuyerUserId, AcceptedSnapshotJson,
            Fingerprint, ExpiresAt) VALUES (NEWID(), @UserId, N'invalid-json',
                REPLICATE('b', 64), DATEADD(minute, 1, SYSUTCDATETIME()));
        THROW 52011, 'Invalid quote JSON should be rejected.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;
    BEGIN TRY
        INSERT dbo.RetailCheckoutQuote (QuoteId, BuyerUserId, AcceptedSnapshotJson,
            Fingerprint, CreatedAt, ExpiresAt) VALUES (NEWID(), @UserId, N'{}',
                REPLICATE('c', 64), '20260102', '20260101');
        THROW 52012, 'Invalid quote lifetime should be rejected.', 1;
    END TRY
    BEGIN CATCH
        IF ERROR_NUMBER() <> 547 THROW;
    END CATCH;

    INSERT dbo.[Order] (BuyerUserId, OrderCode, OrderType, ShippingAddress,
        RecipientNameSnapshot, RecipientPhoneSnapshot, OrderStatus, RetailQuoteId)
        VALUES (@UserId, @Tag, 'Retail', N'Synthetic only', N'Schema buyer', '0900000000',
            'PendingPayment', @QuoteId);
    DECLARE @OrderId bigint = SCOPE_IDENTITY();
    INSERT dbo.SellerOrder (OrderId, FarmerProfileId, SellerOrderCode)
        VALUES (@OrderId, @FarmerId, @Tag + '-SELLER');
    DECLARE @SellerOrderId bigint = SCOPE_IDENTITY();
    INSERT dbo.OrderItem (SellerOrderId, ProductId, Quantity, UnitPriceSnapshot,
        UnitSnapshot, SubTotal) VALUES (@SellerOrderId, @ProductId, 5, 10000, N'kg', 50000);
    DECLARE @OrderItemId bigint = SCOPE_IDENTITY();
    INSERT dbo.OrderItemBatchAllocation (OrderItemId, HarvestBatchId, AllocationSequence,
        AllocatedQuantity) VALUES (@OrderItemId, @BatchA, 1, 3), (@OrderItemId, @BatchB, 2, 2);
    IF (SELECT COUNT(*) FROM dbo.OrderItemBatchAllocation WHERE OrderItemId = @OrderItemId) <> 2
        THROW 52013, 'A product line must support multiple batch allocations.', 1;
    UPDATE dbo.HarvestBatch SET AvailableQuantity = AvailableQuantity - 3,
        ReservedQuantity = ReservedQuantity + 3
        WHERE HarvestBatchId = @BatchA AND AvailableQuantity >= 3;
    IF @@ROWCOUNT <> 1 THROW 52014, 'Initial batch reservation failed.', 1;
    UPDATE dbo.HarvestBatch SET AvailableQuantity = AvailableQuantity - 3,
        ReservedQuantity = ReservedQuantity + 3
        WHERE HarvestBatchId = @BatchA AND AvailableQuantity >= 3;
    IF @@ROWCOUNT <> 0 THROW 52015, 'Conditional reservation allowed overselling.', 1;

    INSERT dbo.DeliveryAddress (UserId, RecipientName, RecipientPhone, AddressDetail,
        ProvinceCity, District, Ward, ProvinceCode, WardCode)
        VALUES (@UserId, N'Schema buyer', '0900000000', N'Synthetic only',
            N'Hồ Chí Minh', NULL, N'Thạnh An', '12', '268');
    INSERT dbo.AuditLog (PerformedByUserId, EntityType, EntityId, [Action], TraceId)
        VALUES (@UserId, 'Order', @OrderId, 'SchemaVerification', @Tag);

    ROLLBACK TRANSACTION;
    SELECT DB_NAME() AS DatabaseName, N'PASS' AS SchemaAndBehaviorVerification,
        N'Synthetic fixture transaction rolled back' AS FixtureStatus;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
