# Chooses the CompanyHQ release this company builds against. Rebuild afterward. Only the pin changes:
# a newer release may need changes in the packages you added or how Program.cs registers them.
#
#   ./Scripts/Update.ps1            move to the newest published release
#   ./Scripts/Update.ps1 0.1.0      pin a specific release, older or newer
#   ./Scripts/Update.ps1 --list     show the published releases
param([string]$Version)
$ErrorActionPreference = 'Stop'
if ($args.Count) { throw 'Update takes at most one argument: a version or --list.' }
$root = Split-Path $PSScriptRoot -Parent

# The same token the API build uses: process environment, then .env.dev or .env, then the GitHub CLI login.
$token = $env:COMPANYHQ_PACKAGES_TOKEN
foreach ($envFile in @('.env.dev', '.env') | ForEach-Object { Join-Path $root $_ }) {
    if (-not $token -and (Test-Path $envFile)) {
        foreach ($line in Get-Content $envFile) { if ($line -match '^\s*COMPANYHQ_PACKAGES_TOKEN\s*=\s*(\S+)\s*$') { $token = $Matches[1] } }
    }
}
if (-not $token -and (Get-Command gh -ErrorAction SilentlyContinue)) { $token = gh auth token }
if (-not $token) { throw 'Set COMPANYHQ_PACKAGES_TOKEN in .env.dev or .env to a GitHub token with read:packages, or sign in with the GitHub CLI.' }

$feed = (([xml](Get-Content (Join-Path $root 'nuget.config') -Raw)).configuration.packageSources.add | Where-Object key -eq 'companyhq').value
if ($feed -notmatch '^(https://.+)/index\.json$') { throw 'nuget.config has no companyhq package source.' }
$headers = @{ Authorization = 'Basic ' + [Convert]::ToBase64String([Text.Encoding]::ASCII.GetBytes("token:$token")) }
# Every CompanyHQ package shares one version, and the generic Rss package is published after the other
# generic packages, so its feed marks the releases that are complete. CompanyHQ.Api is not consulted.
$published = @((Invoke-RestMethod "$($Matches[1])/download/companyhq.services.rss/index.json" -Headers $headers).versions |
    Where-Object { $_ -match '^\d+(\.\d+){1,3}$' } | Sort-Object { [version]$_ })
if (-not $published) { throw 'No released CompanyHQ version is published.' }

$propsFile = Join-Path $root 'Directory.Build.props'
$props = Get-Content $propsFile -Raw
if ($props -notmatch '<CompanyHQVersion>([^<]+)</CompanyHQVersion>') { throw 'Directory.Build.props has no CompanyHQVersion.' }
$current = $Matches[1]

if ($Version -eq '--list') {
    foreach ($release in $published) { if ($release -eq $current) { "$release  (this company)" } else { $release } }
    return
}
$target = if ($Version) { $Version } else { $published[-1] }
if ($target -notin $published) { throw "CompanyHQ $target is not published. Available: $($published -join ', ')." }
if ($current -eq $target) { Write-Host "Already on CompanyHQ $target."; return }
[IO.File]::WriteAllText($propsFile, $props.Replace("<CompanyHQVersion>$current</CompanyHQVersion>", "<CompanyHQVersion>$target</CompanyHQVersion>"), (New-Object Text.UTF8Encoding($false)))
Write-Host "CompanyHQ $current -> $target. Rebuild with a startup script."
