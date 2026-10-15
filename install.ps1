# Install or update Korkut on this machine (Windows): creates the secrets the stack needs in .env
# (once), then builds and starts it with Docker Compose. Safe to run again: a value already in .env
# is never changed, because the model gateway's key seals every provider key stored in it.
#
#   .\install.ps1                          install, or update to this checkout
#   .\install.ps1 -ResetAdminPassword      give korkut-admin a new password (if it was lost)
param([switch] $ResetAdminPassword)
$ErrorActionPreference = "Stop"
Set-Location $PSScriptRoot

docker compose version *> $null
if ($LASTEXITCODE -ne 0) {
    Write-Error "install: Docker Desktop with Compose v2 is required (https://docs.docker.com/get-docker/)."
}

# Written for Windows PowerShell 5.1 as well as PowerShell 7: no APIs newer than .NET Framework.
function New-RandomBytes([int] $count) {
    $bytes = New-Object byte[] $count
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $rng.GetBytes($bytes) } finally { $rng.Dispose() }
    return ,$bytes
}

# Letters and digits only, so it can be read out and typed.
function New-Password {
    $alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789"
    return -join ((New-RandomBytes 20) | ForEach-Object { $alphabet[$_ % $alphabet.Length] })
}

if (-not (Test-Path .env)) { New-Item -ItemType File .env | Out-Null }
$lines = @(Get-Content .env)

function Add-Setting([string] $name, [string] $value) {
    if ($script:lines | Where-Object { $_ -match "^$name=.+" }) { return }
    # Start on a new line even when the existing file does not end with one.
    $raw = Get-Content .env -Raw
    if ($raw -and -not $raw.EndsWith("`n")) { Add-Content -Path .env -Value "" }
    Add-Content -Path .env -Value "$name=$value"
    $script:lines += "$name=$value"
    Write-Host "install: generated $name"
}

# The model gateway (llmr) seals the provider keys you enter in Korkut's settings with this key.
# Keep a copy of .env: without this value those keys cannot be read back.
Add-Setting "LLMR_MASTER_KEY" ([Convert]::ToBase64String((New-RandomBytes 32)))
# Korkut presents this token to the gateway; nothing else on the machine can call it.
Add-Setting "LLMR_TOKEN" (-join ((New-RandomBytes 32) | ForEach-Object { $_.ToString("x2") }))

# The installation admin (user name korkut-admin) is created with this password on first start.
# Change it in the app afterwards (Ayarlar > Hesap); this value is then only the first one.
$newAdminPassword = $false
if (-not ($lines | Where-Object { $_ -match "^KORKUT_ADMIN_PASSWORD=.+" })) {
    Add-Setting "KORKUT_ADMIN_PASSWORD" (New-Password)
    $newAdminPassword = $true
} elseif ($ResetAdminPassword) {
    $lines = @($lines | Where-Object { $_ -notmatch "^KORKUT_ADMIN_PASSWORD=" })
    Set-Content -Path .env -Value $lines
    Add-Setting "KORKUT_ADMIN_PASSWORD" (New-Password)
    $newAdminPassword = $true
}

if ($ResetAdminPassword) {
    # One start with the reset flag sets the admin's password back to the one in .env.
    $env:KORKUT_ADMIN_PASSWORD_RESET = "1"
}
try {
    docker compose up -d --build
} finally {
    Remove-Item Env:KORKUT_ADMIN_PASSWORD_RESET -ErrorAction SilentlyContinue
}
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$port = ($lines | Where-Object { $_ -match "^KORKUT_HTTP_PORT=" } | ForEach-Object { $_.Split("=", 2)[1] })
if (-not $port) { $port = "8090" }
Write-Host ""
Write-Host "Korkut is starting at http://localhost:$port"
if ($newAdminPassword) {
    $password = ($lines | Where-Object { $_ -match "^KORKUT_ADMIN_PASSWORD=" } | ForEach-Object { $_.Split("=", 2)[1] })
    Write-Host "Sign in with:  korkut-admin / $password"
    Write-Host "Change this password after signing in (Ayarlar > Hesap), then invite your colleagues."
} else {
    Write-Host "Sign in as korkut-admin. Lost the password? .\install.ps1 -ResetAdminPassword"
}
Write-Host "Keep a copy of .env somewhere safe: it holds the key to the AI provider keys you enter."
