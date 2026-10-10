# Install or update Korkut on this machine (Windows): creates the secrets the stack needs in .env
# (once), then builds and starts it with Docker Compose. Safe to run again: a value already in .env
# is never changed, because the model gateway's key seals every provider key stored in it.
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

docker compose up -d --build
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

$port = ($lines | Where-Object { $_ -match "^KORKUT_HTTP_PORT=" } | ForEach-Object { $_.Split("=", 2)[1] })
if (-not $port) { $port = "8090" }
Write-Host ""
Write-Host "Korkut is starting at http://localhost:$port"
Write-Host "The first visit asks for your office's name and creates the first account."
Write-Host "Keep a copy of .env somewhere safe: it holds the key to the AI provider keys you enter."
