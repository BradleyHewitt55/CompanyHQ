# Builds and starts every service in docker-compose.yml. Production reads .env and keeps its own
# stack, apart from the development one.
#
#   ./Scripts/StartProd.ps1
param()
$ErrorActionPreference = 'Stop'
if ($args.Count) { throw 'StartProd takes no arguments; all configured services run in Docker.' }
$root = Split-Path $PSScriptRoot -Parent
$settings = Get-Content (Join-Path $root 'project.json') -Raw | ConvertFrom-Json
$envFile = Join-Path $root '.env'
if (-not (Test-Path $envFile)) { throw '.env not found. It holds the production values; see README.md.' }

# project.json owns the project name and port; Compose reads .env for the API's settings.
$env:COMPOSE_PROJECT_NAME = $settings.slug
$env:ENV_FILE = '.env'
$env:API_PORT = $settings.ports.api

$compose = @('compose', '--project-directory', $root, '--env-file', $envFile, '-f', (Join-Path $root 'docker-compose.yml'))
# The development stack publishes the same port; stopping it leaves its data untouched.
docker compose -p "$($settings.slug)-dev" stop
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed ($LASTEXITCODE)." }
docker @compose up -d --build --wait --wait-timeout 180
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed ($LASTEXITCODE)." }
Write-Host "$($settings.name): all services run in Docker. API http://localhost:$($settings.ports.api)/health"
