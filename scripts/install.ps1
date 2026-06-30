<#
  Korkut installer for Windows (PowerShell). Idempotent - safe to re-run.

    ./scripts/install.ps1            Native install (Python venv + built SPA)
    ./scripts/install.ps1 -Docker    Docker install (build + start the compose stack)
#>
param([switch]$Docker)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent $PSScriptRoot
Set-Location $RepoRoot

function Info($m) { Write-Host "==> $m" -ForegroundColor Cyan }
function Warn($m) { Write-Host "!  $m" -ForegroundColor Yellow }
function Die($m)  { Write-Host "Error: $m" -ForegroundColor Red; exit 1 }

# Generate a 64-hex session secret without assuming Python is present.
function New-Secret {
  -join ((1..32) | ForEach-Object { '{0:x2}' -f (Get-Random -Maximum 256) })
}

# Create .env from the template, fill KORKUT_SECRET, default the local DB to SQLite.
function Write-Env {
  if (Test-Path ".env") { Warn ".env already exists - leaving it untouched"; return }
  Info "Creating .env from .env.example"
  Copy-Item .env.example .env
  $secret = New-Secret
  $content = Get-Content .env
  $content = $content -replace '^KORKUT_SECRET=.*', "KORKUT_SECRET=$secret"
  $content = $content -replace '^KORKUT_DB_URL=postgresql', '# KORKUT_DB_URL=postgresql'
  Set-Content .env $content
  Info "Generated KORKUT_SECRET; using SQLite under data/ for the local database"
}

# --- Docker install ---
if ($Docker) {
  if (-not (Get-Command docker -ErrorAction SilentlyContinue)) { Die "docker not found. Install Docker Desktop." }
  docker compose version | Out-Null
  if ($LASTEXITCODE -ne 0) { Die "Docker Compose v2 required (docker compose ...)." }
  Info "Docker OK"
  Write-Env
  Info "Building and starting the stack (this can take a few minutes the first time)"
  docker compose -f deploy/docker-compose.yml up -d --build
  Write-Host @"

Setup complete. Next steps:

  1) Add a model key to .env (GROQ_API_KEY or ANTHROPIC_API_KEY), or point Korkut at a local
     OpenAI-compatible endpoint (e.g. LM Studio) from Settings -> Model in the app.

  2) Create your first user (you will be prompted for a password):
       docker compose -f deploy/docker-compose.yml exec korkut python -m korkut.api.adduser avukat --name "Av. Name"

  3) Open http://localhost:8090

  Logs:  docker compose -f deploy/docker-compose.yml logs -f korkut
  Stop:  docker compose -f deploy/docker-compose.yml down

"@
  exit 0
}

# --- Native install ---
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

if (-not (Test-Path ".venv")) {
  Info "Creating virtual environment (.venv)"
  & $python.Source -m venv .venv
}
$venvPy = Join-Path $RepoRoot ".venv\Scripts\python.exe"
Info "Installing the backend (editable, app + dev extras)"
& $venvPy -m pip install --upgrade pip | Out-Null
& $venvPy -m pip install -e "backend[app,dev]"

Info "Installing and building the frontend SPA"
Push-Location frontend
npm ci
npm run build
Pop-Location

Write-Env

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
