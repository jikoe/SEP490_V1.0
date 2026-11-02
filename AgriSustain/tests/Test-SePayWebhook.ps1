<#
.SYNOPSIS
    Sends a SePay "money in" webhook for an order through the public ngrok URL, exactly as SePay
    does when the linked bank account receives the transfer.

.EXAMPLE
    .\tests\Test-SePayWebhook.ps1 -OrderCode RC-0f3c... -Amount 175700
    .\tests\Test-SePayWebhook.ps1 -OrderCode RC-0f3c... -Amount 175700 -Buyer retail1 -BuyerPassword 123456
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OrderCode,
    [Parameter(Mandatory)][decimal]$Amount,
    [string]$BaseUrl,
    [string]$Buyer,
    [string]$BuyerPassword
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Get-DotEnvValue([string]$Name) {
    foreach ($file in (Join-Path $root 'AgriSustain\.env'), (Join-Path $root '.env')) {
        if (-not (Test-Path $file)) { continue }
        $line = Get-Content $file | Where-Object { $_ -match "^\s*$([regex]::Escape($Name))\s*=" } | Select-Object -First 1
        if ($line) { return ($line -split '=', 2)[1].Trim().Trim('"') }
    }
}

if (-not $BaseUrl) { $BaseUrl = Get-DotEnvValue 'NGROK_PUBLIC_URL' }
if (-not $BaseUrl) { throw 'Pass -BaseUrl or set NGROK_PUBLIC_URL in .env.' }
$BaseUrl = $BaseUrl.TrimEnd('/')
$apiKey = Get-DotEnvValue 'Payment__SePay__WebhookApiKey'
if (-not $apiKey) { throw 'Set Payment__SePay__WebhookApiKey in AgriSustain\.env.' }
$headers = @{ 'ngrok-skip-browser-warning' = 'true' }

function Get-OrderState([string]$Token) {
    $order = Invoke-RestMethod "$BaseUrl/api/orders/by-code/$([uri]::EscapeDataString($OrderCode))" `
        -Headers ($headers + @{ Authorization = "Bearer $Token" })
    "$($order.orderStatus) / $($order.paymentStatus)"
}

$buyerToken = $null
if ($Buyer) {
    $login = Invoke-RestMethod "$BaseUrl/api/auth/login" -Method Post -Headers $headers -ContentType 'application/json' `
        -Body (@{ identifier = $Buyer; password = $BuyerPassword } | ConvertTo-Json)
    $buyerToken = $login.accessToken
    Write-Host "Order before webhook : $(Get-OrderState $buyerToken)"
}

$settings = Get-Content (Join-Path $root 'AgriSustain\appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$reference = ($OrderCode -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
$reference = $reference.Substring(0, [Math]::Min(18, $reference.Length))
# Content as MB delivers it: the reference shown on the QR page followed by the bank's own trace text.
$body = @{
    id              = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    gateway         = 'MBBank'
    transactionDate = (Get-Date).ToUniversalTime().AddHours(7).ToString('yyyy-MM-dd HH:mm:ss')
    accountNumber   = $settings.Payment.VietQR.AccountNumber
    subAccount      = ''
    code            = $null
    content         = "$reference FT$(Get-Date -Format 'yyMMddHHmmss')"
    transferType    = 'in'
    description     = "BankAPINotify $reference"
    transferAmount  = $Amount
    accumulated     = 0
    referenceCode   = 'FT' + (Get-Date -Format 'yyMMddHHmmssfff')
} | ConvertTo-Json
$webhook = @{ Uri = "$BaseUrl/api/payment/sepay-webhook"; Method = 'Post'; ContentType = 'application/json; charset=utf-8'
    Headers = $headers + @{ Authorization = "Apikey $apiKey" }; Body = [Text.Encoding]::UTF8.GetBytes($body) }

$watch = [Diagnostics.Stopwatch]::StartNew()
$first = Invoke-RestMethod @webhook
Write-Host "1. sepay-webhook     : success=$($first.success) ($($watch.ElapsedMilliseconds) ms)"
$retry = Invoke-RestMethod @webhook
Write-Host "2. retry (same id)   : success=$($retry.success)"

if ($buyerToken) { Write-Host "Order after webhook  : $(Get-OrderState $buyerToken)" }
if (-not $first.success -or -not $retry.success) { exit 1 }
