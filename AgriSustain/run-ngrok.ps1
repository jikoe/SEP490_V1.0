<#
.SYNOPSIS
    Runs the backend and exposes it through ngrok so SePay / PayOS callbacks reach this machine.

.EXAMPLE
    .\run-ngrok.ps1                 # start backend (if needed) + ngrok on NGROK_PUBLIC_URL from .env
    .\run-ngrok.ps1 -SkipBackend    # backend is already running (e.g. from Visual Studio)
    .\run-ngrok.ps1 -Stop           # stop the ngrok agent
#>
[CmdletBinding()]
param(
    [int]$Port = 5179,
    [string]$Url,
    [switch]$SkipBackend,
    [switch]$Stop
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
$ngrokApi = 'http://127.0.0.1:4040/api/tunnels'

function Get-DotEnvValue([string]$Name) {
    $file = Join-Path $root '.env'
    if (-not (Test-Path $file)) { return $null }
    foreach ($line in Get-Content $file) {
        if ($line -match "^\s*$([regex]::Escape($Name))\s*=\s*(.*)$") { return $Matches[1].Trim().Trim('"') }
    }
    return $null
}

function Test-Listening([int]$LocalPort) {
    [bool](Get-NetTCPConnection -State Listen -LocalPort $LocalPort -ErrorAction SilentlyContinue)
}

function Wait-Until([scriptblock]$Condition, [int]$TimeoutSeconds, [string]$What) {
    $deadline = (Get-Date).AddSeconds($TimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        $result = & $Condition
        if ($result) { return $result }
        Start-Sleep -Milliseconds 700
    }
    throw "Timed out after $TimeoutSeconds s waiting for $What."
}

function Get-Tunnel {
    try { (Invoke-RestMethod $ngrokApi -TimeoutSec 2).tunnels | Where-Object { $_.config.addr -match ":$Port$" } | Select-Object -First 1 }
    catch { $null }
}

if ($Stop) {
    Get-Process ngrok -ErrorAction SilentlyContinue | Stop-Process -Force
    Write-Host 'ngrok stopped.'
    return
}

$ngrok = Get-Command ngrok -ErrorAction SilentlyContinue
if (-not $ngrok) {
    throw 'ngrok is not installed. Run: winget install ngrok.ngrok ; ngrok config add-authtoken <token>'
}
if (-not $Url) { $Url = Get-DotEnvValue 'NGROK_PUBLIC_URL' }

if (-not $SkipBackend -and -not (Test-Listening $Port)) {
    $projectDir = Join-Path $root 'AgriSustain'
    # global.json pins SDK 8.0.x; when only a newer SDK is installed, build from outside the repo.
    Push-Location $root
    cmd /c 'dotnet --version >nul 2>&1'
    $buildDir = if ($LASTEXITCODE -eq 0) { $root } else { $env:TEMP }
    Pop-Location
    Write-Host 'Building backend ...'
    Push-Location $buildDir
    try { dotnet build (Join-Path $projectDir 'AgriSustain.csproj') -nologo -v q }
    finally { Pop-Location }
    if ($LASTEXITCODE -ne 0) { throw 'Backend build failed.' }

    Write-Host "Starting backend on http://localhost:$Port ..."
    Start-Process powershell -WorkingDirectory $projectDir -ArgumentList @('-NoExit', '-Command',
        "`$host.UI.RawUI.WindowTitle = 'AgriSustain API :$Port'; `$env:ASPNETCORE_ENVIRONMENT = 'Development'; " +
        "`$env:ASPNETCORE_URLS = 'http://localhost:$Port'; dotnet .\bin\Debug\net8.0\AgriSustain.dll")
}
if (-not $SkipBackend) {
    Wait-Until { try { (Invoke-WebRequest "http://localhost:$Port/health" -UseBasicParsing -TimeoutSec 2).StatusCode -eq 200 } catch { $false } } 180 'backend /health' | Out-Null
    Write-Host "Backend is up: http://localhost:$Port"
}

function Start-Tunnel([string]$Domain) {
    $arguments = @('http', "$Port", '--log=stdout')
    if ($Domain) { $arguments += "--url=$Domain" }
    Write-Host "Starting ngrok -> localhost:$Port $(if ($Domain) { "($Domain)" } else { '(account dev domain)' })"
    $process = Start-Process $ngrok.Source -ArgumentList $arguments -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput $ngrokLog -RedirectStandardError "$ngrokLog.err"
    $deadline = (Get-Date).AddSeconds(30)
    while ((Get-Date) -lt $deadline) {
        $found = Get-Tunnel
        if ($found) { return $found }
        if ($process.HasExited) { return $null }
        Start-Sleep -Milliseconds 700
    }
    throw "ngrok did not open a tunnel within 30 s. Log: $ngrokLog"
}

$ngrokLog = Join-Path $env:TEMP 'agrisustain-ngrok.log'
$tunnel = Get-Tunnel
if (-not $tunnel) {
    $tunnel = Start-Tunnel $Url
    if (-not $tunnel -and $Url -and (Select-String -Path $ngrokLog -Pattern 'ERR_NGROK_320' -Quiet)) {
        Write-Warning "$Url is reserved by another ngrok account (ERR_NGROK_320). Falling back to this account's own domain."
        $tunnel = Start-Tunnel $null
    }
    if (-not $tunnel) {
        $reason = Select-String -Path $ngrokLog -Pattern 'lvl=(eror|crit)' | Select-Object -Last 1
        throw "ngrok exited: $(if ($reason) { $reason.Line } else { "see $ngrokLog" })"
    }
}
$publicUrl = $tunnel.public_url.TrimEnd('/')

$ping = Invoke-RestMethod "$publicUrl/payment/config" -Headers @{ 'ngrok-skip-browser-warning' = 'true' } -TimeoutSec 15
if (-not $ping.bankName) { throw "Tunnel is up but $publicUrl/payment/config did not answer as expected." }

Write-Host ''
Write-Host "ngrok tunnel : $publicUrl -> http://localhost:$Port" -ForegroundColor Green
Write-Host "Inspector    : http://127.0.0.1:4040 (live requests from SePay)"
Write-Host ''
Write-Host 'SePay webhook (SePay > Webhooks: incoming money, auth = API Key from Payment__SePay__WebhookApiKey in .env):'
Write-Host "  $publicUrl/api/payment/sepay-webhook" -ForegroundColor Green

if ($Url -and $publicUrl -ne $Url.TrimEnd('/')) {
    Write-Warning "Public URL differs from NGROK_PUBLIC_URL ($Url); update .env, appsettings.json "
}
Write-Host ''
Write-Host "Test a payment: .\tests\Test-SePayWebhook.ps1 -OrderCode <RC-...> -Amount <final amount>"

