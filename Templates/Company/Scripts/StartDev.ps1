param([Parameter(ValueFromRemainingArguments)][string[]]$Rest)
$developmentMode = $true
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path $PSScriptRoot -Parent
$settings = Get-Content (Join-Path $projectRoot 'project.json') -Raw | ConvertFrom-Json
$localPath = Join-Path $projectRoot '.local'
$envFile = Join-Path $localPath 'compose.env'
function Initialize-Local {
    # Process environment wins over .env.dev (development only), then .env.
    $seedValues = @{}
    $files = @('.env')
    if ($developmentMode) { $files += '.env.dev' }
    foreach ($file in $files) {
        $filePath = Join-Path $projectRoot $file
        if (Test-Path $filePath) {
            foreach ($line in Get-Content $filePath) {
                if ($line -match '^\s*(Seed__Admin__(Email|Password|FirstName|LastName))\s*=\s*(.*?)\s*$') {
                    $value = $Matches[3]
                    if ($value.Length -ge 2 -and (($value.StartsWith('"') -and $value.EndsWith('"')) -or ($value.StartsWith("'") -and $value.EndsWith("'")))) { $value = $value.Substring(1, $value.Length - 2) }
                    $seedValues[$Matches[2]] = $value
                }
            }
        }
    }
    foreach ($key in @('Email', 'Password', 'FirstName', 'LastName')) {
        $value = [Environment]::GetEnvironmentVariable("Seed__Admin__$key")
        if ($null -ne $value) { $seedValues[$key] = $value }
    }
    New-Item -ItemType Directory -Force $localPath, (Join-Path $localPath 'storage') | Out-Null
    $configPath = Join-Path $localPath 'runtime.json'
    if (-not (Test-Path $configPath)) {
        function New-Secret {
            $bytes = New-Object byte[] 32
            $rng = [Security.Cryptography.RandomNumberGenerator]::Create()
            try { $rng.GetBytes($bytes); [Convert]::ToBase64String($bytes) } finally { $rng.Dispose() }
        }
        @{
            Postgres = @{ Password = (New-Secret) }
            Jwt = @{ SigningKey = (New-Secret) }
            Auth = @{ WebBaseAddress = "http://localhost:$($settings.ports.web)" }
            Marketing = @{ Origins = @("http://localhost:$($settings.ports.web)") }
            Mcp = @{ OAuth = @{
                PublicWebBaseAddress = "http://localhost:$($settings.ports.web)"
                AuthorizationServerBaseAddress = "http://localhost:$($settings.ports.api)"
                ResourceAddress = "http://localhost:$($settings.ports.api)/mcp"
            } }
            Media = @{ ContentBaseUrl = "http://localhost:$($settings.ports.api)" }
            Demo = @{ StorageSigningKey = (New-Secret); CompanyName = $settings.name }
        } | ConvertTo-Json -Depth 8 | Set-Content $configPath
    }
    $config = Get-Content $configPath -Raw | ConvertFrom-Json
    # Backfill new defaults without replacing existing company overrides or credentials.
    if (-not $config.Marketing) {
        $config | Add-Member -MemberType NoteProperty -Name Marketing -Value @{ Origins = @("http://localhost:$($settings.ports.web)") } -Force
    }
    if (-not $config.Mcp) {
        $config | Add-Member -MemberType NoteProperty -Name Mcp -Value @{ OAuth = @{
            PublicWebBaseAddress = "http://localhost:$($settings.ports.web)"
            AuthorizationServerBaseAddress = "http://localhost:$($settings.ports.api)"
            ResourceAddress = "http://localhost:$($settings.ports.api)/mcp"
        } } -Force
    }
    if ($seedValues.Count) {
        if ([string]::IsNullOrWhiteSpace($seedValues.Email) -or [string]::IsNullOrWhiteSpace($seedValues.Password)) { throw 'Configure both Seed__Admin__Email and Seed__Admin__Password.' }
        $admin = @{ Email = $seedValues.Email; Password = $seedValues.Password; FirstName = 'Company'; LastName = 'Admin' }
        foreach ($key in @('FirstName', 'LastName')) { if ($seedValues.ContainsKey($key)) { $admin[$key] = $seedValues[$key] } }
        $config | Add-Member -MemberType NoteProperty -Name Seed -Value @{ Admin = $admin; FromEnvironment = $true } -Force
    }
    # Environment-supplied seed values apply only to this launch mode; do not reuse dev values in prod.
    if (-not $seedValues.Count -and $config.Seed.FromEnvironment) { $config.PSObject.Properties.Remove('Seed') }
    $config | ConvertTo-Json -Depth 8 | Set-Content $configPath
    @(
        "DEMO_POSTGRES_PASSWORD=$($config.Postgres.Password)"
        "COMPOSE_PROJECT_NAME=$($settings.slug)"
        "API_PORT=$($settings.ports.api)"
        "WEB_PORT=$($settings.ports.web)"
        "DATABASE_PORT=$($settings.ports.database)"
        "REDIS_PORT=$($settings.ports.redis)"
        "SMTP_PORT=$($settings.ports.smtp)"
        "MAIL_PORT=$($settings.ports.mail)"
    ) | Set-Content $envFile
}
function Invoke-Stack {
    param([switch]$Dev, [string[]]$Arguments)
    $composeArgs = @('compose', '--project-directory', $projectRoot, '--env-file', $envFile, '-f', (Join-Path $projectRoot 'docker-compose.yml'))
    if ($Dev) { $composeArgs += @('-f', (Join-Path $projectRoot 'docker-compose.dev.yml')) }
    & docker @composeArgs @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Docker Compose failed ($LASTEXITCODE)." }
}
function Wait-Url([string]$Url, [switch]$Health, [int]$TimeoutSeconds = 120) {
    $deadline = [DateTime]::UtcNow.AddSeconds($TimeoutSeconds)
    do {
        try {
            $response = Invoke-WebRequest $Url -UseBasicParsing -TimeoutSec 3
            if ($response.StatusCode -eq 200 -and (-not $Health -or $response.Content -eq 'Healthy')) { return }
        } catch {}
        Start-Sleep -Milliseconds 500
    } while ([DateTime]::UtcNow -lt $deadline)
    throw "Timed out waiting for $Url. Inspect docker compose logs and .local logs."
}
function Initialize-Company {
    $api = "http://localhost:$($settings.ports.api)"
    Wait-Url "$api/health" -Health
    $existing = $null
    try { $existing = Invoke-RestMethod "$api/api/company" } catch { if ([int]$_.Exception.Response.StatusCode -ne 404) { throw } }
    if ($existing) { return }
    $config = Get-Content (Join-Path $localPath 'runtime.json') -Raw | ConvertFrom-Json
    if (-not $config.Seed.Admin.Email -or -not $config.Seed.Admin.Password) { return }
    $login = Invoke-RestMethod "$api/api/auth/login" -Method Post -ContentType 'application/json' -Body (@{ email = $config.Seed.Admin.Email; password = $config.Seed.Admin.Password } | ConvertTo-Json)
    try {
        $profile = @{ name = $settings.name; website = "http://localhost:$($settings.ports.web)"; supportEmail = 'support@northstar.test'; infoEmail = 'hello@northstar.test'; description = 'A small studio building thoughtful websites and digital products.'; publicEmails = @(@{ title = 'Support'; address = 'support@northstar.test' }, @{ title = 'Hello'; address = 'hello@northstar.test' }) }
        Invoke-RestMethod "$api/api/company" -Method Put -Headers @{ Authorization = "Bearer $($login.accessToken)" } -ContentType 'application/json' -Body ($profile | ConvertTo-Json -Depth 5) | Out-Null
    } finally {
        Invoke-RestMethod "$api/api/auth/logout" -Method Post -ContentType 'application/json' -Body (@{ refreshToken = $login.refreshToken } | ConvertTo-Json) | Out-Null
    }
}
function Stop-HostWeb {
    $recordPath = Join-Path $localPath 'web-process.json'
    if (-not (Test-Path $recordPath)) { return }
    $record = Get-Content $recordPath -Raw | ConvertFrom-Json
    $process = Get-Process -Id $record.Id -ErrorAction SilentlyContinue
    if ($process -and $process.StartTime.ToUniversalTime().ToString('o') -eq $record.Started) {
        $command = (Get-CimInstance Win32_Process -Filter "ProcessId = $($record.Id)").CommandLine
        if ($command -notlike '*next*' -or $command -notlike "*--port $($record.Port)*") { throw 'Recorded process is not the project web server.' }
        taskkill /PID $record.Id /T /F | Out-Null
        if ($LASTEXITCODE -ne 0) { throw 'Could not stop the project web server.' }
    }
    Remove-Item -LiteralPath $recordPath
}

if (-not $Rest) { $Rest = @('--Web') }
if ($Rest.Count -ne 1 -or $Rest[0] -notmatch '^--([A-Za-z][A-Za-z0-9_-]*)$') { throw 'Expected one --{project} argument; defaults to --Web.' }
$project = $Matches[1]
$webPath = Join-Path $projectRoot "Apps/$project"
if (-not (Test-Path (Join-Path $webPath 'package.json'))) { throw "Apps/$project is not a web project." }
$port = $settings.apps.$project
if (-not $port) { throw "Set the $project app port in project.json first." }
Initialize-Local
Stop-HostWeb
# Remove any production Web container before the host owns its port; preserve all data.
foreach ($appName in $settings.apps.PSObject.Properties.Name) {
    Invoke-Stack -Arguments @('stop', $appName.ToLowerInvariant())
}
Invoke-Stack -Dev -Arguments @('up', '-d', '--build', '--wait', '--wait-timeout', '180')
Initialize-Company
$env:API_INTERNAL_URL = "http://localhost:$($settings.ports.api)"
$env:WEB_REDIS_URL = "redis://localhost:$($settings.ports.redis)"
$env:MARKETING_URL = "http://localhost:$port"
Push-Location $webPath
try {
    if (-not (Test-Path node_modules)) { npm ci; if ($LASTEXITCODE -ne 0) { throw 'npm ci failed.' } }
    $node = (Get-Command node).Source
    $process = Start-Process $node -ArgumentList @('node_modules/next/dist/bin/next', 'dev', '--hostname', 'localhost', '--port', $port) -WorkingDirectory $webPath -WindowStyle Hidden -RedirectStandardOutput (Join-Path $localPath 'web.log') -RedirectStandardError (Join-Path $localPath 'web-error.log') -PassThru
    @{ Id = $process.Id; Started = $process.StartTime.ToUniversalTime().ToString('o'); Port = $port } | ConvertTo-Json | Set-Content (Join-Path $localPath 'web-process.json')
} finally { Pop-Location }
Wait-Url "http://localhost:$port" -TimeoutSeconds 300
Write-Host "$($settings.name): development Web http://localhost:$port; API runs in Docker."
