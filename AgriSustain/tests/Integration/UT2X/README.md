# UT2X Integration

Tests for UC-FAR-01, UC-FAR-02, UC-FAR-04 and UC-FAR-10 are compiled by
the existing Integration test project. No separate UT2X project is required.
Case IDs in method names correspond to the Unit/Integration reports and TestCases.json.

Run from the solution directory:

```powershell
dotnet test tests/Integration/AgriSustain.IntegrationTests.csproj --filter "FullyQualifiedName~AgriSustain.UT2X" --logger "trx;LogFileName=UT2X.trx" --results-directory tests/Integration/UT2X/TestResults
```

SQL cases require AGRISUSTAIN_TEST_SQL_CONNECTION or AGRISUSTAIN_TEST_SQL_SERVER.
The fixture always creates a unique AgriSustain_UT2X_... catalog and deletes only
that catalog, with guarded cleanup. It never tests against the application catalog.
Private-file tests use temporary directories. Email is captured, never sent externally.
Browser acceptance cases remain Pending until manually executed.
Shared clock/data/mail helpers are linked from Unit/UT2X/Infrastructure, matching
the existing shared-fixture convention used by Commerce.
