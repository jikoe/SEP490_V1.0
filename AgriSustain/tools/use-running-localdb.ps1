param([switch]$StartIfStopped)
$ErrorActionPreference = 'Stop'
# Development-only repair: avoid LocalDB discovery/autostart failures inside a VS Debug host.
# Use the installed SQL tools to discover the CURRENT pipe, validate the SAME database using
# Windows Authentication, then write an ignored machine-local override. Never hardcode a pipe,
# recreate/drop/attach a database, grant permissions, or introduce a fallback database.
$taskInstance = 'MSSQLLocalDB'
$taskDatabase = 'AgriSustain'
$taskProject = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..'))
$taskLocalFile = Join-Path $taskProject 'src/AgriSustain.Api/appsettings.Local.json'
$taskInfo = (& sqllocaldb info $taskInstance 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect MSSQLLocalDB with the installed SQL tool.' }
$taskPipe = [regex]::Match($taskInfo, 'np:\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query').Value
if (-not $taskPipe -and $StartIfStopped) {
    & sqllocaldb start $taskInstance | Out-Null
    if ($LASTEXITCODE -ne 0) { throw 'LocalDB could not start; inspect its error log.' }
    $taskInfo = (& sqllocaldb info $taskInstance 2>&1 | Out-String)
    if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the started LocalDB instance.' }
    $taskPipe = [regex]::Match($taskInfo, 'np:\\\\\.\\pipe\\LOCALDB#[A-Za-z0-9]+\\tsql\\query').Value
}
if (-not $taskPipe) { throw 'MSSQLLocalDB is not running. Start it or use -StartIfStopped.' }
# SELECT-only validation must pass before replacing configuration. Never seed/reset here.
$taskCheck = (& sqlcmd -S $taskPipe -d $taskDatabase -E -b -l 8 -h -1 -W -Q "SET NOCOUNT ON; SELECT CASE WHEN DB_NAME()=N'AgriSustain' AND OBJECT_ID(N'dbo.Category',N'U') IS NOT NULL AND OBJECT_ID(N'dbo.RetailCheckoutQuote',N'U') IS NOT NULL THEN 'AGRI_LOCALDB_OK' ELSE 'WRONG_SCHEMA' END;" 2>&1 | Out-String)
if ($LASTEXITCODE -ne 0 -or $taskCheck.Trim() -ne 'AGRI_LOCALDB_OK') {
    throw 'The running pipe could not read the expected AgriSustain schema; configuration unchanged.'
}
$taskSettings = @{}
if (Test-Path -LiteralPath $taskLocalFile) {
    $taskExisting = [IO.File]::ReadAllText($taskLocalFile) | ConvertFrom-Json
    foreach ($taskProperty in $taskExisting.PSObject.Properties) {
        $taskSettings[$taskProperty.Name] = $taskProperty.Value
    }
}
$taskConnections = @{}
if ($taskSettings.ContainsKey('ConnectionStrings') -and $taskSettings.ConnectionStrings) {
    foreach ($taskProperty in $taskSettings.ConnectionStrings.PSObject.Properties) {
        $taskConnections[$taskProperty.Name] = $taskProperty.Value
    }
}
$taskConnections['AgriSustain'] = "Server=$taskPipe;Database=$taskDatabase;Integrated Security=True;TrustServerCertificate=True;Connect Timeout=8;"
$taskSettings['ConnectionStrings'] = $taskConnections
[IO.File]::WriteAllText($taskLocalFile, ($taskSettings | ConvertTo-Json -Depth 20) + "`n", (New-Object Text.UTF8Encoding($false)))
Write-Output 'Verified AgriSustain through its running LocalDB pipe; wrote appsettings.Local.json.'
Write-Output 'Restart an API built before local overrides were supported; subsequent changes reload automatically.'
Write-Output 'Rerun this script after LocalDB restarts; the pipe suffix is temporary.'
