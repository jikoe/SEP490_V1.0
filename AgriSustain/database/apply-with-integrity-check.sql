:On Error exit
/* Run from this directory in SQLCMD mode, after a backup:
   sqlcmd -S "(localdb)\MSSQLLocalDB" -d AgriSustain -E -b -f 65001
       -v TargetDatabase="AgriSustain" -i apply-with-integrity-check.sql
   Serializes writers briefly; validates original columns/rows before committing.
   SHA-256 values are computed inside SQL Server. No business rows are exported.
*/
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;
IF DB_NAME() <> N'$(TargetDatabase)'
    THROW 52300, 'Unexpected migration target.', 1;
BEGIN TRANSACTION;

CREATE TABLE #IntegrityPlan (TableName nvarchar(260) PRIMARY KEY, QueryText nvarchar(max));
CREATE TABLE #IntegrityBefore
    (TableName nvarchar(260) PRIMARY KEY, [RowCount] bigint, DataHash varbinary(32));
CREATE TABLE #IntegrityAfter
    (TableName nvarchar(260) PRIMARY KEY, [RowCount] bigint, DataHash varbinary(32));

INSERT #IntegrityPlan (TableName, QueryText)
SELECT QUOTENAME(s.name) + N'.' + QUOTENAME(t.name),
    N'SELECT @Rows=COUNT_BIG(*) FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
    + N'; SELECT @Hash=HASHBYTES(''SHA2_256'',(SELECT ' + columns_list.ColumnNames
    + N' FROM ' + QUOTENAME(s.name) + N'.' + QUOTENAME(t.name)
    + N' ORDER BY ' + keys_list.KeyNames + N' FOR JSON PATH, INCLUDE_NULL_VALUES));'
FROM sys.tables t JOIN sys.schemas s ON s.schema_id = t.schema_id
CROSS APPLY (SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), N',')
    WITHIN GROUP (ORDER BY c.column_id) AS ColumnNames
    FROM sys.columns c WHERE c.object_id = t.object_id) columns_list
CROSS APPLY (SELECT STRING_AGG(CONVERT(nvarchar(max), QUOTENAME(c.name)), N',')
    WITHIN GROUP (ORDER BY ic.key_ordinal) AS KeyNames
    FROM sys.indexes i JOIN sys.index_columns ic ON ic.object_id = i.object_id
        AND ic.index_id = i.index_id
    JOIN sys.columns c ON c.object_id = ic.object_id AND c.column_id = ic.column_id
    WHERE i.object_id = t.object_id AND i.is_primary_key = 1) keys_list
WHERE t.is_ms_shipped = 0;
IF EXISTS (SELECT 1 FROM #IntegrityPlan WHERE QueryText IS NULL)
    THROW 52301, 'All original tables need a primary key for deterministic verification.', 1;

DECLARE @Name nvarchar(260), @Query nvarchar(max), @Rows bigint, @Hash varbinary(32);
DECLARE before_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName, QueryText FROM #IntegrityPlan ORDER BY TableName;
OPEN before_cursor;
FETCH NEXT FROM before_cursor INTO @Name, @Query;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sys.sp_executesql @Query, N'@Rows bigint OUTPUT,@Hash varbinary(32) OUTPUT',
        @Rows=@Rows OUTPUT, @Hash=@Hash OUTPUT;
    INSERT #IntegrityBefore VALUES (@Name, @Rows, @Hash);
    FETCH NEXT FROM before_cursor INTO @Name, @Query;
END;
CLOSE before_cursor;
DEALLOCATE before_cursor;
GO

:r migrations/20261018_approved_three_uc.sql
GO

DECLARE @Name nvarchar(260), @Query nvarchar(max), @Rows bigint, @Hash varbinary(32);
DECLARE after_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT TableName, QueryText FROM #IntegrityPlan ORDER BY TableName;
OPEN after_cursor;
FETCH NEXT FROM after_cursor INTO @Name, @Query;
WHILE @@FETCH_STATUS = 0
BEGIN
    EXEC sys.sp_executesql @Query, N'@Rows bigint OUTPUT,@Hash varbinary(32) OUTPUT',
        @Rows=@Rows OUTPUT, @Hash=@Hash OUTPUT;
    INSERT #IntegrityAfter VALUES (@Name, @Rows, @Hash);
    FETCH NEXT FROM after_cursor INTO @Name, @Query;
END;
CLOSE after_cursor;
DEALLOCATE after_cursor;
IF EXISTS (SELECT * FROM #IntegrityBefore EXCEPT SELECT * FROM #IntegrityAfter)
    OR EXISTS (SELECT * FROM #IntegrityAfter EXCEPT SELECT * FROM #IntegrityBefore)
BEGIN
    ROLLBACK TRANSACTION;
    THROW 52302, 'Original row counts or data hashes changed; migration rolled back.', 1;
END;
IF @@TRANCOUNT <> 1
BEGIN
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW 52303, 'Unexpected transaction state; migration not committed.', 1;
END;
COMMIT TRANSACTION;
SELECT DB_NAME() AS DatabaseName, COUNT(*) AS OriginalTablesVerified,
    SUM([RowCount]) AS OriginalRowsPreserved, N'PASS: original-column SHA-256 hashes match'
        AS IntegrityResult FROM #IntegrityBefore;
