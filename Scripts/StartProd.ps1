# Starts the tools, the API and every web project in Docker. See Scripts/AGENTS.md.
#
#   ./Scripts/StartProd.ps1
param()
$ErrorActionPreference = 'Stop'
if ($args.Count) { throw 'StartProd takes no arguments; the full stack runs in Docker.' }
$root = Split-Path $PSScriptRoot -Parent

$envFile = Join-Path $root '.env'
if (-not (Test-Path $envFile)) { throw ".env not found. Copy .env.example to .env and fill in the values." }

docker compose --env-file $envFile -f (Join-Path $root 'docker-compose.yml') up -d --build --wait
exit $LASTEXITCODE
