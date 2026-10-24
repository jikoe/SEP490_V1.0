-- Incremental baseline upgrade for the supplied 46-table schema. Select AgriSustain.
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