-- Deploy the code without the two legacy properties before running this migration.
-- Preserve product, quantity and existing complaint text in Description before dropping columns.
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
BEGIN TRY
    BEGIN TRANSACTION;

    IF OBJECT_ID(N'dbo.DisputeTicket', N'U') IS NULL
        THROW 51400, 'The DisputeTicket table does not exist.', 1;

    IF COL_LENGTH(N'dbo.DisputeTicket', N'OrderItemId') IS NOT NULL
        AND COL_LENGTH(N'dbo.DisputeTicket', N'AffectedQuantity') IS NOT NULL
    BEGIN
        EXEC(N'
            UPDATE ticket
            SET Description = CONCAT(
                N''Sản phẩm khiếu nại (dòng hàng #'', ticket.OrderItemId, N''): '',
                COALESCE(item.ProductNameSnapshot, product.ProductName, N''Không rõ tên sản phẩm''),
                NCHAR(10), N''Số lượng khiếu nại: '',
                CONVERT(nvarchar(32), ticket.AffectedQuantity), N'' '', item.UnitSnapshot,
                NCHAR(10), NCHAR(10), COALESCE(ticket.Description, N''''))
            FROM dbo.DisputeTicket AS ticket
            LEFT JOIN dbo.OrderItem AS item ON item.OrderItemId = ticket.OrderItemId
            LEFT JOIN dbo.Product AS product ON product.ProductId = item.ProductId
            WHERE ticket.OrderItemId IS NOT NULL;');
    END
    ELSE IF COL_LENGTH(N'dbo.DisputeTicket', N'OrderItemId') IS NOT NULL
        OR COL_LENGTH(N'dbo.DisputeTicket', N'AffectedQuantity') IS NOT NULL
        THROW 51401, 'The legacy columns are incomplete. Review their data before removal.', 1;

    IF OBJECT_ID(N'dbo.FK_DisputeTicket_OrderItem', N'F') IS NOT NULL
        ALTER TABLE dbo.DisputeTicket DROP CONSTRAINT FK_DisputeTicket_OrderItem;
    IF OBJECT_ID(N'dbo.CK_DisputeTicket_AffectedQuantity', N'C') IS NOT NULL
        ALTER TABLE dbo.DisputeTicket DROP CONSTRAINT CK_DisputeTicket_AffectedQuantity;
    IF EXISTS (SELECT 1 FROM sys.indexes
        WHERE object_id = OBJECT_ID(N'dbo.DisputeTicket') AND name = N'IX_DisputeTicket_OrderItem')
        DROP INDEX IX_DisputeTicket_OrderItem ON dbo.DisputeTicket;

    IF COL_LENGTH(N'dbo.DisputeTicket', N'OrderItemId') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.DisputeTicket DROP COLUMN OrderItemId;');
    IF COL_LENGTH(N'dbo.DisputeTicket', N'AffectedQuantity') IS NOT NULL
        EXEC(N'ALTER TABLE dbo.DisputeTicket DROP COLUMN AffectedQuantity;');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;
