# TestIT2 Unit

Tests for UC-FAR-01, UC-FAR-02, UC-FAR-04 and UC-FAR-10 are compiled by
the existing Unit test project. No separate TestIT2 project is required.
Case IDs in method names correspond to the Unit/Integration reports and TestCases.json.

Run from the solution directory:

```powershell
dotnet test tests/Unit/AgriSustain.UnitTests.csproj --filter "FullyQualifiedName~AgriSustain.TestIT2" --logger "trx;LogFileName=TestIT2.trx" --results-directory tests/Unit/TestIT2/TestResults
```

Unit cases use a fixed clock and repository/transaction/mail doubles; no SQL or real SMTP.
