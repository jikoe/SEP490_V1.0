<#
.SYNOPSIS
    Replays the VietQR.vn callback flow (token_generate -> transaction-sync) against the public
    ngrok URL, exactly as VietQR does after a successful transfer.

    With -ViaVietQrSandbox the callback is not replayed locally: VietQR's own sandbox
    (dev.vietqr.org "Test Callback") is asked to push it to the URL declared in the VietQR portal.
    Needs VIETQR_SANDBOX_USERNAME / VIETQR_SANDBOX_PASSWORD (issued by VietQR) in .env.

.EXAMPLE
    .\tests\Test-VietQrCallback.ps1 -OrderCode RC-0f3c... -Amount 214500
    .\tests\Test-VietQrCallback.ps1 -OrderCode RC-0f3c... -Amount 214500 -Buyer retail1 -BuyerPassword 123456
    .\tests\Test-VietQrCallback.ps1 -OrderCode RC-0f3c... -Amount 214500 -Buyer retail1 -BuyerPassword 123456 -ViaVietQrSandbox
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OrderCode,
    [Parameter(Mandatory)][decimal]$Amount,
    [string]$BaseUrl,
    [string]$BasePath = '/vqr',
    [string]$Buyer,
    [string]$BuyerPassword,
    [switch]$ViaVietQrSandbox
)
Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Get-DotEnvValue([string]$Name) {
    $line = Get-Content (Join-Path $root '.env') | Where-Object { $_ -match "^\s*$([regex]::Escape($Name))\s*=" } | Select-Object -First 1
    if ($line) { ($line -split '=', 2)[1].Trim().Trim('"') }
}

if (-not $BaseUrl) {
    $BaseUrl = Get-DotEnvValue 'NGROK_PUBLIC_URL'
    if (-not $BaseUrl) { throw 'Pass -BaseUrl or set NGROK_PUBLIC_URL in .env.' }
}
$BaseUrl = $BaseUrl.TrimEnd('/')
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
    Write-Host "Order before callback : $(Get-OrderState $buyerToken)"
}

$reference = ($OrderCode -replace '[^A-Za-z0-9]', '').ToUpperInvariant()
$reference = $reference.Substring(0, [Math]::Min(18, $reference.Length))
$appSettings = Get-Content (Join-Path $root 'AgriSustain\appsettings.json') -Raw -Encoding UTF8 | ConvertFrom-Json
$bankAccount = $appSettings.Payment.VietQR.AccountNumber

if ($ViaVietQrSandbox) {
    $user = Get-DotEnvValue 'VIETQR_SANDBOX_USERNAME'
    $pass = Get-DotEnvValue 'VIETQR_SANDBOX_PASSWORD'
    if (-not $user -or -not $pass) { throw 'Set VIETQR_SANDBOX_USERNAME and VIETQR_SANDBOX_PASSWORD (issued by VietQR) in .env.' }
    $vietQrBasic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("${user}:$pass"))
    $vietQrToken = Invoke-RestMethod 'https://dev.vietqr.org/vqr/api/token_generate' -Method Post `
        -Headers @{ Authorization = "Basic $vietQrBasic" } -ContentType 'application/json'
    Write-Host "1. VietQR sandbox token: OK"
    $result = Invoke-RestMethod 'https://dev.vietqr.org/vqr/bank/api/test/transaction-callback' -Method Post `
        -Headers @{ Authorization = "Bearer $($vietQrToken.access_token)" } -ContentType 'application/json' `
        -Body (@{ bankAccount = $bankAccount; content = $reference; amount = $Amount; transType = 'C'; bankCode = $appSettings.Payment.VietQR.BankShortName } | ConvertTo-Json)
    Write-Host "2. VietQR test-callback: $($result | ConvertTo-Json -Compress)"
    if (-not $buyerToken) { return }
    $deadline = (Get-Date).AddSeconds(30)
    do { Start-Sleep 2; $state = Get-OrderState $buyerToken } while ($state -notmatch '^Confirmed' -and (Get-Date) -lt $deadline)
    Write-Host "Order after callback  : $state"
    if ($state -notmatch '^Confirmed') { Write-Warning 'VietQR did not call back. Check the partner URL/credentials in the VietQR portal and http://127.0.0.1:4040.'; exit 1 }
    return
}

$callback = $appSettings.Payment.VietQR.Callback
$basic = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes("$($callback.Username):$($callback.Password)"))
$watch = [Diagnostics.Stopwatch]::StartNew()
$token = Invoke-RestMethod "$BaseUrl$BasePath/api/token_generate" -Method Post `
    -Headers ($headers + @{ Authorization = "Basic $basic" }) -ContentType 'application/json'
Write-Host "1. token_generate     : $($token.token_type), expires_in=$($token.expires_in)s ($($watch.ElapsedMilliseconds) ms)"

# Content as a bank delivers it: VietQR/bank prefixes around the 18-char reference shown on the QR page.
$body = @{
    bankaccount     = $bankAccount
    amount          = $Amount
    transType       = 'C'
    content         = "QR VQR712a7d45ea $reference Ma giao dich Trace756759"
    transactionid   = [guid]::NewGuid().ToString()
    transactiontime = [DateTimeOffset]::UtcNow.ToUnixTimeMilliseconds()
    referencenumber = 'FT' + (Get-Date -Format 'yyMMddHHmmssfff')
    orderId         = ''
    terminalCode    = ''
    subTerminalCode = ''
    serviceCode     = ''
    urlLink         = ''
    sign            = ''
} | ConvertTo-Json
$syncHeaders = $headers + @{ Authorization = "Bearer $($token.access_token)" }
$syncUrl = "$BaseUrl$BasePath/bank/api/transaction-sync"

$watch.Restart()
$first = Invoke-RestMethod $syncUrl -Method Post -Headers $syncHeaders -ContentType 'application/json; charset=utf-8' `
    -Body ([Text.Encoding]::UTF8.GetBytes($body))
Write-Host "2. transaction-sync   : error=$($first.error) ref=$($first.object.reftransactionid) ($($watch.ElapsedMilliseconds) ms)"
Write-Host "                        $($first.toastMessage)"

$retry = Invoke-RestMethod $syncUrl -Method Post -Headers $syncHeaders -ContentType 'application/json; charset=utf-8' `
    -Body ([Text.Encoding]::UTF8.GetBytes($body))
Write-Host "3. retry (same txn)   : error=$($retry.error) ref=$($retry.object.reftransactionid) - $($retry.toastMessage)"

if ($buyerToken) { Write-Host "Order after callback  : $(Get-OrderState $buyerToken)" }
if ($first.error -or $retry.object.reftransactionid -ne $first.object.reftransactionid) { exit 1 }
