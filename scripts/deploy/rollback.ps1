param(
  [string] $To,      # release stamp; defaults to the newest one that is not live
  [switch] $List
)

. (Join-Path $PSScriptRoot "_env.ps1")

$cfg = Import-DeployEnv
$sudo = $cfg.AVALON_SUDO
$unit = "$($cfg.AVALON_SERVICE).service"

$current = (Invoke-Remote $cfg "basename `"`$(readlink -f '$($cfg.CurrentDir)' 2>/dev/null)`" 2>/dev/null || echo none" -AllowFail | Out-String).Trim()
$releases = @(Invoke-Remote $cfg "ls -1dt '$($cfg.ReleasesDir)'/*/ 2>/dev/null | xargs -r -n1 basename" -AllowFail |
  ForEach-Object { "$_".Trim() } | Where-Object { $_ })

if ($List -or -not $releases) {
  Write-Step "Releases on $($cfg.Target) (newest first)"
  if (-not $releases) { Write-Warn "none"; exit 0 }
  foreach ($r in $releases) {
    if ($r -eq $current) { Write-Ok "$r  <- current" } else { Write-Host "  ..  $r" }
  }
  exit 0
}

if (-not $To) {
  $To = $releases | Where-Object { $_ -ne $current } | Select-Object -First 1
  if (-not $To) { Write-Bad "Only one release on disk ($current) — nothing to roll back to."; exit 1 }
}

if ($To -notin $releases) {
  Write-Bad "No release '$To'. Available: $($releases -join ', ')"
  exit 1
}
if ($To -eq $current) { Write-Warn "$To is already current."; exit 0 }

Write-Step "Rolling back $current -> $To"
Invoke-Remote $cfg "ln -sfn '$($cfg.ReleasesDir)/$To' '$($cfg.AVALON_REMOTE_DIR)/.current.tmp' && mv -Tf '$($cfg.AVALON_REMOTE_DIR)/.current.tmp' '$($cfg.CurrentDir)'"
Invoke-Remote $cfg "$sudo systemctl restart $unit"

Start-Sleep -Seconds 3
$state = (Invoke-Remote $cfg "systemctl is-active $unit 2>/dev/null || true" -AllowFail | Out-String).Trim()
if ($state -eq "active") {
  Write-Ok "running $To"
} else {
  Write-Bad "$unit is $state after rollback"
  Invoke-Remote $cfg "$sudo journalctl -u $unit -n 30 --no-pager -o short-iso" -AllowFail | Write-Host
  exit 1
}
