[CmdletBinding()]
param([string]$SqlServer)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$testRoot = $PSScriptRoot
$results = Join-Path $testRoot 'TestResults'
$previousServer = [Environment]::GetEnvironmentVariable('AGRISUSTAIN_TEST_SQL_SERVER')

function Invoke-TestProject {
    param([string]$Project, [string]$Filter, [string]$ResultName)
    $arguments = @('test', $Project, '--filter', $Filter, '--logger', 'console;verbosity=minimal', '--logger', "trx;LogFileName=$ResultName", '--results-directory', $results, '-p:UseSharedCompilation=false', '-p:UseAppHost=false')
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Tests failed: $Project ($Filter). See $results." }
}
try {
    if ($SqlServer) { $env:AGRISUSTAIN_TEST_SQL_SERVER = $SqlServer }
    Invoke-TestProject (Join-Path $testRoot 'Unit/AgriSustain.UnitTests.csproj') 'FullyQualifiedName~.Commerce.' 'commerce-unit.trx'
    Invoke-TestProject (Join-Path $testRoot 'Integration/AgriSustain.IntegrationTests.csproj') 'Layer=ApiIntegration' 'commerce-api.trx'
    if ($env:AGRISUSTAIN_TEST_SQL_SERVER -or $env:AGRISUSTAIN_TEST_SQL_CONNECTION) {
        Invoke-TestProject (Join-Path $testRoot 'Integration/AgriSustain.IntegrationTests.csproj') 'Layer=SqlIntegration' 'commerce-sql.trx'
    }
    else {
        Write-Warning 'SQL integration NOT RUN. Supply -SqlServer or AGRISUSTAIN_TEST_SQL_CONNECTION. API test doubles do not verify database transactions.'
    }
}
finally {
    [Environment]::SetEnvironmentVariable('AGRISUSTAIN_TEST_SQL_SERVER', $previousServer)
}
