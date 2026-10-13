# Starts the API in Docker. Development reads .env.dev and stays apart from the production stack.
#
#   ./Scripts/StartDev.ps1          API in Docker, then returns
#   ./Scripts/StartDev.ps1 --Web    also runs Apps/Web on the host (next dev); Ctrl+C stops it
#
# A web app is optional and company-owned: --{project} needs Apps/{project}/package.json with a dev
# script, and its port under "apps" in project.json. The API address reaches it as API_INTERNAL_URL.
param([Parameter(ValueFromRemainingArguments)][string[]]$Rest)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$settings = Get-Content (Join-Path $root 'project.json') -Raw | ConvertFrom-Json
$envFile = Join-Path $root '.env.dev'
if (-not (Test-Path $envFile)) { throw '.env.dev not found. It holds the development values; see README.md.' }

if ($Rest.Count -gt 1 -or ($Rest -and $Rest[0] -notmatch '^--([A-Za-z][A-Za-z0-9_-]*)$')) { throw 'Expected at most one --{project} argument.' }
if ($Rest) {
    $project = $Matches[1]
    $webPath = Join-Path $root "Apps/$project"
    if (-not (Test-Path (Join-Path $webPath 'package.json'))) { throw "Apps/$project is not a web project." }
    $port = $settings.apps.$project
    if (-not $port) { throw "Set the $project app port under apps in project.json first." }
}

# project.json owns the project name and port; Compose reads .env.dev for the API's settings.
$env:COMPOSE_PROJECT_NAME = "$($settings.slug)-dev"
$env:ENV_FILE = '.env.dev'
$env:API_PORT = $settings.ports.api

$compose = @('compose', '--project-directory', $root, '--env-file', $envFile, '-f', (Join-Path $root 'docker-compose.yml'), '-f', (Join-Path $root 'docker-compose.dev.yml'))
# The production stack publishes the same port; stopping it leaves its data untouched.
docker compose -p $settings.slug stop
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed ($LASTEXITCODE)." }
docker @compose up -d --build --wait --wait-timeout 180
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed ($LASTEXITCODE)." }
Write-Host "$($settings.name) API: http://localhost:$($settings.ports.api)/health"
if (-not $Rest) { return }

# The host process reaches the API on its published port.
$env:PORT = $port
$env:API_INTERNAL_URL = "http://localhost:$($settings.ports.api)"
Push-Location $webPath
try {
    if (-not (Test-Path 'node_modules')) { npm ci; if ($LASTEXITCODE -ne 0) { throw 'Dependency installation failed.' } }
    npm run dev
    if ($LASTEXITCODE -ne 0) { throw 'Development server failed.' }
} finally { Pop-Location }
