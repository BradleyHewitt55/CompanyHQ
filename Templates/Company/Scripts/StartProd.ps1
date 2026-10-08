# Builds and starts the whole company stack in Docker.
# Production reads .env and keeps its own database, apart from the development stack.
#
#   ./Scripts/StartProd.ps1
param()
$ErrorActionPreference = 'Stop'
if ($args.Count) { throw 'StartProd takes no arguments; all configured services run in Docker.' }
$root = Split-Path $PSScriptRoot -Parent
$settings = Get-Content (Join-Path $root 'project.json') -Raw | ConvertFrom-Json
$envFile = Join-Path $root '.env'
if (-not (Test-Path $envFile)) { throw '.env not found. It holds the production values; see README.md.' }

# project.json owns the project name and ports; Compose reads .env itself for credentials.
$env:COMPOSE_PROJECT_NAME = $settings.slug
$env:ENV_FILE = '.env'
$env:API_PORT = $settings.ports.api
$env:WEB_PORT = $settings.ports.web
$env:DATABASE_PORT = $settings.ports.database
$env:REDIS_PORT = $settings.ports.redis
$env:SMTP_PORT = $settings.ports.smtp
$env:MAIL_PORT = $settings.ports.mail

# Downloading the CompanyHQ packages needs a GitHub token with read:packages: the one in .env, or
# failing that the GitHub CLI login, so a broad personal token never has to be stored in a file.
if (-not $env:COMPANYHQ_PACKAGES_TOKEN) {
    foreach ($line in Get-Content $envFile) { if ($line -match '^\s*COMPANYHQ_PACKAGES_TOKEN\s*=\s*(\S+)\s*$') { $env:COMPANYHQ_PACKAGES_TOKEN = $Matches[1] } }
}
if (-not $env:COMPANYHQ_PACKAGES_TOKEN -and (Get-Command gh -ErrorAction SilentlyContinue)) {
    $env:COMPANYHQ_PACKAGES_TOKEN = gh auth token
}

# This company owns the migrations for its whole schema. When one of its models changed, or a
# CompanyHQ update changed a generic one, the migration is written here; the API applies it on start.
function Update-Migrations {
    if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'The .NET SDK is required to write database migrations.' }
    Push-Location (Join-Path $root 'Apps/Api')
    try {
        dotnet tool restore | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not restore the migration tool.' }
        $built = dotnet build | Out-String
        if ($LASTEXITCODE -ne 0) { throw "The API does not build.`n$built" }
        # Reading the model boots the host without a database, so it gets throwaway settings rather
        # than this mode's credentials; they are removed again before Compose reads the environment.
        $key = New-Object byte[] 32
        [Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($key)
        $design = @{ Postgres__Password = 'migrations-only'; Postgres__AutoMigrate = 'false'; Postgres__AutoSeed = 'false'; Jwt__SigningKey = [Convert]::ToBase64String($key) }
        foreach ($entry in $design.GetEnumerator()) { [Environment]::SetEnvironmentVariable($entry.Key, $entry.Value) }
        $pending = dotnet ef migrations has-pending-model-changes --no-build | Out-String
        if ($LASTEXITCODE -eq 0) { return }
        if ($pending -notmatch 'Changes have been made to the model') { throw "Could not compare the model with the migrations.`n$pending" }
        $name = if (Test-Path 'Migrations') { "Update$([DateTime]::UtcNow.ToString('yyyyMMddHHmmss'))" } else { 'Initial' }
        $added = dotnet ef migrations add $name --no-build | Out-String
        if ($LASTEXITCODE -ne 0) { throw "Could not write migration $name.`n$added" }
        Write-Host "Wrote migration $name in Apps/Api/Migrations; commit it with the change that needed it."
        # Nothing has touched the database yet, so a migration that drops data waits for a person.
        if ($added -match 'loss of data') { throw "Migration $name may delete data. Review it in Apps/Api/Migrations, then run this script again to apply it." }
    } finally {
        foreach ($name in @('Postgres__Password', 'Postgres__AutoMigrate', 'Postgres__AutoSeed', 'Jwt__SigningKey')) { [Environment]::SetEnvironmentVariable($name, $null) }
        Pop-Location
    }
}

# Creates the company profile once, signed in as the administrator seeded from .env.
function Initialize-Company {
    $api = "http://localhost:$($settings.ports.api)"
    try { Invoke-RestMethod "$api/api/company" | Out-Null; return } catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw } }
    $values = @{}
    foreach ($line in Get-Content $envFile) {
        if ($line -match '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*=\s*(.*?)\s*$') { $values[$Matches[1]] = $Matches[2] }
    }
    $login = Invoke-RestMethod "$api/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{ email = $values['Seed__Admin__Email']; password = $values['Seed__Admin__Password'] } | ConvertTo-Json)
    try {
        $profile = @{ name = $settings.name; website = "http://localhost:$($settings.ports.web)"; supportEmail = 'support@northstar.test'; infoEmail = 'hello@northstar.test'; description = 'A small studio building thoughtful websites and digital products.'; publicEmails = @(@{ title = 'Support'; address = 'support@northstar.test' }, @{ title = 'Hello'; address = 'hello@northstar.test' }) }
        Invoke-RestMethod "$api/api/company" -Method Put -Headers @{ Authorization = "Bearer $($login.accessToken)" } -ContentType 'application/json' -Body ($profile | ConvertTo-Json -Depth 5) | Out-Null
    } finally {
        Invoke-RestMethod "$api/api/auth/logout" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $login.refreshToken } | ConvertTo-Json) | Out-Null
    }
}

# The API build wrote one OpenAPI contract per company service; each goes next to its C# contracts.
function Save-Contracts {
    $temp = Join-Path ([IO.Path]::GetTempPath()) "company-openapi-$PID"
    docker @compose cp api:/app/openapi $temp
    if ($LASTEXITCODE -ne 0) { throw 'Could not read the OpenAPI contracts from the API container.' }
    try {
        foreach ($file in Get-ChildItem $temp -Filter *.json) {
            $name = $file.BaseName -replace '^.*_', ''
            # "api" describes every controller, for the website's types; it belongs to no one service.
            if ($name -eq 'api') { continue }
            $service = Get-ChildItem (Join-Path $root 'Services') -Directory | Where-Object Name -eq $name
            if (-not $service) { throw "No Services folder for OpenAPI contract '$name'." }
            $target = New-Item -ItemType Directory -Force (Join-Path $service.FullName 'Contracts/OpenApi')
            Copy-Item $file.FullName (Join-Path $target.FullName "$name.json") -Force
        }
    } finally { Remove-Item $temp -Recurse -Force }
}

$compose = @('compose', '--project-directory', $root, '--env-file', $envFile, '-f', (Join-Path $root 'docker-compose.yml'))
# The development stack publishes the same ports; stopping it leaves its data untouched.
docker compose -p "$($settings.slug)-dev" stop
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed ($LASTEXITCODE)." }
Update-Migrations
docker @compose up -d --build --wait --wait-timeout 180
if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed ($LASTEXITCODE)." }
Save-Contracts
Initialize-Company
Write-Host "$($settings.name): all services run in Docker. Website http://localhost:$($settings.ports.web)"
