# Starts the tools and the API in Docker, and optionally one web project on the host for debugging.
# See Scripts/AGENTS.md.
#
#   ./Scripts/StartDev.ps1          defaults to --Web
#   ./Scripts/StartDev.ps1 --web    the same, then Apps/Web on the host (next dev)
param(
    [Parameter(ValueFromRemainingArguments)][string[]]$Rest
)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

function Read-EnvFile([string]$path) {
    $values = @{}
    foreach ($line in Get-Content $path) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $values[$Matches[1]] = $Matches[2] }
    }
    $values
}

function Get-OrDefault($values, [string]$key, [string]$default) {
    if ($values[$key]) { $values[$key] } else { $default }
}

$envFile = Join-Path $root '.env.dev'
if (-not (Test-Path $envFile)) { throw ".env.dev not found. Copy .env.example to .env.dev and fill in the values." }

# Resolve --{project} to Apps/{project} before starting anything.
$project = $null
if (-not $Rest) { $Rest = @('--Web') }
foreach ($flag in $Rest) {
    if ($flag -notmatch '^--(\w+)$') { throw "Unknown argument '$flag'. Expected --{project}, e.g. --web." }
    if ($project) { throw "Only one --{project} can run on the host at a time." }
    $project = Join-Path $root "Apps/$($Matches[1])"
    if (-not (Test-Path (Join-Path $project 'package.json'))) { throw "'$flag' is not a web project under Apps/." }
}

docker compose --env-file $envFile -f (Join-Path $root 'docker-compose.yml') -f (Join-Path $root 'docker-compose.dev.yml') up -d --build --wait
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
if (-not $project) { exit 0 }

# The host process reaches the API and Redis on their dev-published ports.
$values = Read-EnvFile $envFile
$redisPassword = [uri]::EscapeDataString($values['REDIS_PASSWORD'])
$env:PORT = Get-OrDefault $values 'WEB_PORT' '3004'
$env:API_INTERNAL_URL = "http://localhost:$(Get-OrDefault $values 'API_DEV_PORT' '4002')"
$env:WEB_REDIS_URL = "redis://:$redisPassword@localhost:$(Get-OrDefault $values 'REDIS_DEV_PORT' '6379')"
$env:MARKETING_URL = Get-OrDefault $values 'MARKETING_URL' 'http://localhost:3003'

Push-Location $project
try {
    if (-not (Test-Path 'node_modules')) {
        npm ci
        if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' }
    }
    npm run dev
    if ($LASTEXITCODE -ne 0) { throw 'Development server failed.' }
}
finally {
    Pop-Location
}
