param(
  [switch] $SkipBuild,       # ship .tmp/dist/latest.txt as-is
  [switch] $SkipAssets,      # passed through to publish.ps1
  [switch] $NoRestart,       # stage the release and swap the symlink, but leave the service alone
  [int] $HealthTimeoutSec = 30
)

. (Join-Path $PSScriptRoot "_env.ps1")

$cfg = Import-DeployEnv
$repoRoot = $cfg.RepoRoot
$sudo = $cfg.AVALON_SUDO
$unit = "$($cfg.AVALON_SERVICE).service"

if (-not $SkipBuild) {
  & (Join-Path $PSScriptRoot "publish.ps1") -SkipAssets:$SkipAssets
  if ($LASTEXITCODE -ne 0) { throw "Publish failed." }
}

$latestPath = Join-Path $repoRoot ".tmp\dist\latest.txt"
if (-not (Test-Path -LiteralPath $latestPath)) { throw "No build to deploy. Run ``just publish`` first." }

$tarball = (Get-Content -LiteralPath $latestPath -Raw).Trim()
if (-not (Test-Path -LiteralPath $tarball)) { throw "Recorded build is gone: $tarball" }

$stamp = [IO.Path]::GetFileName($tarball) -replace '^avalon-server-', '' -replace '\.tar\.gz$', ''
$releaseDir = "$($cfg.ReleasesDir)/$stamp"

Write-Step "Deploying $stamp to $($cfg.Target)"

Invoke-Remote $cfg "test -d '$($cfg.ReleasesDir)'" -AllowFail | Out-Null
if ($LASTEXITCODE -ne 0) { throw "$($cfg.ReleasesDir) missing. Run ``just deploy-setup`` first." }

# Captured before the swap so a failed health check has somewhere to go back to.
$previous = (Invoke-Remote $cfg "readlink -f '$($cfg.CurrentDir)' 2>/dev/null || true" -AllowFail | Out-String).Trim()

Write-Step "Uploading $([math]::Round((Get-Item -LiteralPath $tarball).Length / 1MB, 1)) MB"
Send-ToRemote $cfg $tarball "rm -rf '$releaseDir' && mkdir -p '$releaseDir' && tar xzf - -C '$releaseDir'"
Write-Ok "extracted to $releaseDir"

Write-Step "Preparing release"
# tar from Windows carries no exec bit, and the rolling logger and MatchResultSaveSystem both
# write next to the binary — point those at the shared dirs so a prune cannot eat them.
Invoke-Remote $cfg @"
set -e
chmod +x '$releaseDir/Meesles.Avalon.Server'
rm -rf '$releaseDir/Logs' '$releaseDir/Results'
ln -s '$($cfg.AVALON_REMOTE_DIR)/logs' '$releaseDir/Logs'
ln -s '$($cfg.AVALON_REMOTE_DIR)/results' '$releaseDir/Results'
"@
Write-Ok "exec bit set, Logs/ and Results/ linked to shared storage"

Write-Step "Activating"
# ln into a temp name then mv -T: the replacement is a single rename, so `current` is never absent.
Invoke-Remote $cfg "ln -sfn '$releaseDir' '$($cfg.AVALON_REMOTE_DIR)/.current.tmp' && mv -Tf '$($cfg.AVALON_REMOTE_DIR)/.current.tmp' '$($cfg.CurrentDir)'"
Write-Ok "current -> $releaseDir"

if ($NoRestart) {
  Write-Warn "-NoRestart: the service is still running the old release until you restart it."
  exit 0
}

Write-Step "Restarting $unit"
Invoke-Remote $cfg "$sudo systemctl restart $unit"

Write-Step "Health check (${HealthTimeoutSec}s)"
$healthy = $false
$deadline = (Get-Date).AddSeconds($HealthTimeoutSec)
while ((Get-Date) -lt $deadline) {
  Start-Sleep -Seconds 2
  $active = (Invoke-Remote $cfg "systemctl is-active $unit 2>/dev/null || true" -AllowFail | Out-String).Trim()
  if ($active -ne "active") { continue }

  # is-active only proves the process survived; a bound UDP socket proves it got to the listen call.
  $bound = (Invoke-Remote $cfg "ss -lun 2>/dev/null | grep -c ':$($cfg.AVALON_GAME_PORT) ' || true" -AllowFail | Out-String).Trim()
  if ($bound -match '^[1-9]') { $healthy = $true; break }
}

if ($healthy) {
  Write-Ok "active, listening on $($cfg.AVALON_GAME_PORT)/udp"
} else {
  Write-Bad "did not come up within ${HealthTimeoutSec}s"
  Write-Host ""
  Invoke-Remote $cfg "journalctl -u $unit -n 40 --no-pager" -AllowFail | Write-Host

  if ($previous -and $previous -ne $releaseDir) {
    Write-Step "Rolling back to $previous"
    Invoke-Remote $cfg "ln -sfn '$previous' '$($cfg.AVALON_REMOTE_DIR)/.current.tmp' && mv -Tf '$($cfg.AVALON_REMOTE_DIR)/.current.tmp' '$($cfg.CurrentDir)'"
    Invoke-Remote $cfg "$sudo systemctl restart $unit" -AllowFail | Out-Null
    Write-Warn "rolled back — the bad release is still on disk at $releaseDir"
  } else {
    Write-Warn "no previous release to roll back to"
  }
  exit 1
}

Write-Step "Pruning to $($cfg.AVALON_KEEP_RELEASES) releases"
$pruned = Invoke-Remote $cfg @"
cd '$($cfg.ReleasesDir)' || exit 0
keep=`$(readlink -f '$($cfg.CurrentDir)')
ls -1dt */ 2>/dev/null | tail -n +$([int]$cfg.AVALON_KEEP_RELEASES + 1) | while read -r d; do
  [ "`$(readlink -f "`$d")" = "`$keep" ] && continue
  rm -rf "`$d" && echo "removed `$d"
done
"@ -AllowFail
if ($pruned) { $pruned | ForEach-Object { Write-Ok $_ } } else { Write-Ok "nothing to prune" }

Write-Host ""
Write-Ok "Deployed $stamp"
