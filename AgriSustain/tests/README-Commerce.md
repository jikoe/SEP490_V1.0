# Commerce tests: UC-FAR-03, UC-FAR-06, UC-RC-01

Run from the AgriSustain repository folder with the .NET 8 SDK selected by global.json.

## Quick start

    .\tests\Run-CommerceTests.ps1

This runs the 93 new unit cases and 51 HTTP integration cases. Results are saved as TRX files under tests/TestResults. Without SQL configuration, the script explicitly reports that SQL integration was NOT RUN.

To include SQL integration:

    .\tests\Run-CommerceTests.ps1 -SqlServer 'localhost'

Or use your dedicated test instance:

    .\tests\Run-CommerceTests.ps1 -SqlServer '(localdb)\MSSQLLocalDB'

SQL Server must be running and reachable. The current Windows identity needs CREATE/DROP DATABASE permission. For SQL Authentication, set AGRISUSTAIN_TEST_SQL_CONNECTION to your test connection string in the current PowerShell session, then run the script. Keep credentials out of source control. The connection string takes precedence over AGRISUSTAIN_TEST_SQL_SERVER.

The fixture ALWAYS replaces Initial Catalog with AgriSustain_CommerceTests_<GUID>, seeds that database, and deletes only that guarded name afterward. It never loads the application's connection string or updates the application's AgriSustain database. Image/evidence files use an isolated, guarded temporary folder.

## Coverage

| Source | Cases and purpose |
|---|---|
| Unit/Commerce/ProductListingTests.cs | 38 cases: UT-FAR-03-R2-001 through 038; validation, submission/resubmission, images, unit locking, content/price edits, visibility and save failures |
| Unit/Commerce/OrderFulfillmentTests.cs | 26 cases: UT-FAR-06-R2-001 through 026; payment, allocation validity, confirm/ready/dispatch, ownership, duplicate actions and failures |
| Unit/Commerce/ShoppingCartTests.cs | 29 cases: UT-RC-01-R2-001 through 029; merge/batch identity, wholesale prices, quantity steps, stock, selection and checkout preparation |
| Integration/Commerce/CommerceApiTests.cs and EvidenceAndErrorApiTests.cs | 51 HTTP cases: routing, JSON/model binding, role authorization, CurrentActor, controllers, real services/validators/pricing, transitions, evidence deadlines and exception middleware |
| Integration/Commerce/CommerceSqlTests.cs and AdditionalCommerceSqlTests.cs | 29 Fact/Theory methods with multiple input vectors: HTTP, real account middleware, repositories, UnitOfWork, SQL Server and file storage |

Unit tests use typed test doubles from CommerceFixture.cs. HTTP component integration links the same fixture into the integration assembly and uses real ASP.NET controllers/services with fake persistence ports. It does NOT prove SQL query behavior, durability or transaction rollback.

SQL tests exercise persisted product/image/audit records, stale versions, checkout price snapshots, order transitions, reservation release, evidence ownership/deadlines, support notifications, separate batches, current prices/stock, rollback and concurrent dispatch. Upload cases cover extension/signature checks and the inclusive 5 MiB boundary. A test-only EF interceptor throws AFTER SaveChanges writes succeed and BEFORE UnitOfWork commits; assertions use a new DbContext and retry checks to verify actual transaction rollback.

SQL fixtures use EnsureCreated against the EF model. Deployment-script-only triggers and check constraints are not installed automatically. These tests do not verify deployment SQL scripts or production trigger behavior. The authentication scheme exists only in the test assembly; it is not registered in production.

Browser-only behavior such as cancelling a dialog, navigating home, layout and opening the print dialog is outside these backend suites. The print-data SQL test checks checkout snapshots, shipping, discounts, voucher and total, not HTML/PDF rendering. Do not treat these results as all 106 Report-5.2 cases or as frontend E2E coverage.

## Individual commands

    dotnet test .\tests\Unit\AgriSustain.UnitTests.csproj --filter 'UseCase=UC-FAR-03'
    dotnet test .\tests\Unit\AgriSustain.UnitTests.csproj --filter 'CaseId=UT-FAR-06-R2-019'
    dotnet test .\tests\Integration\AgriSustain.IntegrationTests.csproj --filter 'Layer=ApiIntegration'
    dotnet test .\tests\Integration\AgriSustain.IntegrationTests.csproj --filter 'Layer=SqlIntegration'

CommerceSqlFact/CommerceSqlTheory explicitly skip when neither SQL environment variable is configured. A configured but unreachable server MUST fail, rather than being reported as a pass or skip. When a whole Theory is skipped, xUnit reports one skipped method before expanding its InlineData vectors.

## Verified results: 2026-10-27

- Entire unit project: 146/146 passed, including the 93 new commerce cases.
- HTTP integration: 51/51 passed.
- Integration project builds successfully.
- Tried SQL Submit_PersistsPendingImagesAndAudit_WithoutStock against tcp:127.0.0.1,1433. It failed while opening the connection: the target machine actively refused it. The localhost connection was also inaccessible.
- With SQL configuration absent, 29 SQL test methods were correctly reported as skipped.
- SQL assertions, rollback and concurrency have NOT been verified as passing. Rerun against a reachable SQL instance before marking corresponding report cases as passed.
