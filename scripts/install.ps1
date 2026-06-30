<#
  Korkut native install for Windows (PowerShell). Idempotent - safe to re-run.
  Creates a Python venv + installs the backend, builds the frontend SPA, and writes .env.

  Usage:  ./scripts/install.ps1
#>
$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

function Info($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Warn($m) { Write-Host "!  $m" -ForegroundColor Yellow }
function Die($m)  { Write-Host "Error: $m" -ForegroundColor Red; exit 1 }

# --- preflight ---
$python = Get-Command python -ErrorAction SilentlyContinue
if (-not $python) { $python = Get-Command python3 -ErrorAction SilentlyContinue }
if (-not $python) { Die "Python not found. Install Python 3.12 or newer." }
$pyv = & $python.Source -c "import sys; print('%d.%d' % sys.version_info[:2])"
& $python.Source -c "import sys; raise SystemExit(0 if sys.version_info[:2] >= (3,12) else 1)"
if ($LASTEXITCODE -ne 0) { Die "Python 3.12+ required (found $pyv)." }
if (-not (Get-Command node -ErrorAction SilentlyContinue)) { Die "node not found. Install Node 20 or newer." }
$nodeMajor = [int](node -p "process.versions.node.split('.')[0]")
if ($nodeMajor -lt 20) { Die "Node 20+ required (found $(node -v))." }
Info "Python $pyv, Node $(node -v) - OK"

# --- backend venv ---
if (-not (Test-Path ".venv")) {
  Info "Creating virtual environment (.venv)"
  & $python.Source -m venv .venv
}
$venvPy = Join-Path $RepoRoot ".venv\Scripts\python.exe"
Info "Installing the backend (editable, app + dev extras)"
& $venvPy -m pip install --upgrade pip | Out-Null
& $venvPy -m pip install -e "backend[app,dev]"

# --- frontend build ---
Info "Installing and building the frontend SPA"
Push-Location frontend
npm ci
npm run build
Pop-Location

# --- environment file ---
if (-not (Test-Path ".env")) {
  Info "Creating .env from .env.example"
  Copy-Item .env.example .env
  $secret = & $venvPy -c "import secrets; print(secrets.token_hex(32))"
  $content = Get-Content .env
  $content = $content -replace '^KORKUT_SECRET=.*', "KORKUT_SECRET=$secret"
  $content = $content -replace '^KORKUT_DB_URL=postgresql', '# KORKUT_DB_URL=postgresql'
  Set-Content .env $content
  Info "Generated KORKUT_SECRET; using SQLite under data/ for the local database"
} else {
  Warn ".env already exists - leaving it untouched"
}

Write-Host @"

Setup complete. Next steps:

  1) Add a model key to .env (GROQ_API_KEY or ANTHROPIC_API_KEY), or point Korkut at a local
     OpenAI-compatible endpoint (e.g. LM Studio) from Settings -> Model in the app.

  2) Create your first user (you will be prompted for a password):
       cd backend; ..\.venv\Scripts\python -m korkut.api.adduser avukat --name "Av. Name"

  3) Start the server:
       cd backend; ..\.venv\Scripts\python -m korkut.api
     Then open http://localhost:8080

"@
