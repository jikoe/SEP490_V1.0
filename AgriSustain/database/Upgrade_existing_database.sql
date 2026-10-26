:ON ERROR EXIT
-- SQLCMD mode required. Run with this database directory as the working directory.
USE [AgriSustain];
GO
:r .\20261022_seed_schema_compatibility.sql
GO
:r .\20261018_approved_three_uc.sql
GO
:r .\20261022_checkout_input_schema.sql
GO
:r .\20261022_checkout_schema_alignment.sql
GO
:r .\20261022_three_use_cases.sql
GO
PRINT N'AgriSustain migrations completed. Restart the API.';
