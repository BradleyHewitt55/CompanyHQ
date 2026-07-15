$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$projects = @(
    Get-ChildItem -Path (Join-Path $root 'src/Services') -Recurse -Filter '*.csproj' |
        Where-Object { $_.Directory.Name -eq 'Tests' }
    Get-ChildItem -Path (Join-Path $root 'tests') -Directory |
        ForEach-Object { Get-ChildItem -Path $_.FullName -Filter '*.csproj' }
) | Sort-Object FullName -Unique
if ($projects.Count -eq 0) { throw 'No test projects discovered.' }
foreach ($project in $projects) { dotnet test $project.FullName @args; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
