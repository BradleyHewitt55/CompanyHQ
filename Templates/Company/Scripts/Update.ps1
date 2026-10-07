# Updates the selected HQ dependency, preserving company-owned files. Rebuild afterward.
param()
$ErrorActionPreference = 'Stop'
if ($args.Count) { throw 'Update takes no arguments.' }
$projectRoot = Split-Path $PSScriptRoot -Parent
$lockFile = Join-Path $projectRoot '.companyhq/lock.json'
$lock = Get-Content $lockFile -Raw | ConvertFrom-Json
function Invoke-Git([string]$Directory, [string[]]$Arguments) {
    $output = & git -C $Directory @Arguments
    if ($LASTEXITCODE -ne 0) { throw "Git $($Arguments[0]) failed; dependency lock was not advanced." }
    return $output
}
function Save-Lock {
    $temporary = "$lockFile.tmp"
    [IO.File]::WriteAllText($temporary, ($lock | ConvertTo-Json -Depth 5), (New-Object Text.UTF8Encoding($false)))
    Move-Item -LiteralPath $temporary -Destination $lockFile -Force
}
if ($lock.version -ne 1 -or $lock.revision -notmatch '^[a-f0-9]{40,64}$' -or -not $lock.source -or -not $lock.branch) { throw 'Invalid CompanyHQ lock.' }
Invoke-Git $projectRoot @('check-ref-format', "refs/heads/$($lock.branch)") | Out-Null
$linked = $lock.mode -eq 'linked'
if ($lock.mode -and $lock.mode -notin @('managed','linked')) { throw 'Unknown dependency mode.' }
if ($linked) {
    if (-not $lock.path) { throw 'Linked dependency path is missing.' }
    $dependency = [IO.Path]::GetFullPath((Join-Path $projectRoot $lock.path))
} else { $dependency = Join-Path $projectRoot '.companyhq/source' }
if (-not (Test-Path $dependency)) {
    if ($linked) { throw 'Linked HQ folder is missing.' }
    $temporary = Join-Path $projectRoot ('.companyhq/restoring-' + [Guid]::NewGuid())
    Invoke-Git $projectRoot @('clone','--no-checkout','--', $lock.source, $temporary) | Out-Null
    Invoke-Git $temporary @('checkout','--detach', $lock.revision) | Out-Null
    Move-Item -LiteralPath $temporary -Destination $dependency
    Write-Host 'Restored the locked CompanyHQ version. Run Update again to advance to latest.'
    return
}
if (-not (Test-Path (Join-Path $dependency '.git'))) { throw 'Dependency must be its own Git checkout.' }
if ((Invoke-Git $dependency @('remote','get-url','origin')) -ne $lock.source) { throw 'Dependency origin differs from the lock.' }
if (Invoke-Git $dependency @('status','--porcelain','--untracked-files=all')) { throw 'HQ has local edits. Commit/publish them before pulling.' }
$previous = Invoke-Git $dependency @('rev-parse','HEAD')
if ($linked) {
    if ((Invoke-Git $dependency @('branch','--show-current')) -ne $lock.branch) { throw 'Linked HQ is on a different branch.' }
} elseif ($previous -ne $lock.revision) { throw 'Dependency differs from its locked revision.' }
Invoke-Git $dependency @('fetch','origin', "refs/heads/$($lock.branch)") | Out-Null
$next = Invoke-Git $dependency @('rev-parse','FETCH_HEAD^{commit}')
if ($next -eq $previous) {
    $lock.revision = $previous; Save-Lock
    Write-Host 'CompanyHQ is already current.'; return
}
Invoke-Git $dependency @('merge-base','--is-ancestor',$previous,$next) | Out-Null
if ($linked) { Invoke-Git $dependency @('merge','--ff-only',$next) | Out-Null }
else { Invoke-Git $dependency @('checkout','--detach',$next) | Out-Null }
$lock.revision = $next
try { Save-Lock } catch {
    if (-not $linked) { Invoke-Git $dependency @('checkout','--detach',$previous) | Out-Null }
    throw
}
Write-Host "Updated CompanyHQ to $next. Rebuild using a startup script."
