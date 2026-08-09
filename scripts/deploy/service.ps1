param(
  [Parameter(Mandatory = $true)]
  [ValidateSet("start", "stop", "restart", "logs")]
  [string] $Action,
  [switch] $Follow,     # logs: tail -f
  [int] $Lines = 100    # logs: history depth
)

. (Join-Path $PSScriptRoot "_env.ps1")

$cfg = Import-DeployEnv
$sudo = $cfg.AVALON_SUDO
$unit = "$($cfg.AVALON_SERVICE).service"

if ($Action -eq "logs") {
  $journal = "$sudo journalctl -u $unit -n $Lines -o short-iso"
  if ($Follow) {
    Write-Step "Following $unit (ctrl-c to stop)"
    # Streams instead of capturing, so -f prints as it arrives.
    $sshArgs = (Get-SshArgs $cfg) + @("-t", $cfg.Target, "$journal -f")
    & ssh @sshArgs
  } else {
    Write-Step "Last $Lines lines of $unit"
    Invoke-Remote $cfg "$journal --no-pager" -AllowFail | Write-Host
  }
  exit 0
}

Write-Step "systemctl $Action $unit"
Invoke-Remote $cfg "$sudo systemctl $Action $unit"

Start-Sleep -Seconds 2
$state = (Invoke-Remote $cfg "systemctl is-active $unit 2>/dev/null || true" -AllowFail | Out-String).Trim()
$expected = if ($Action -eq "stop") { "inactive" } else { "active" }

if ($state -eq $expected) {
  Write-Ok "$unit is $state"
} else {
  Write-Bad "$unit is $state (expected $expected)"
  Invoke-Remote $cfg "$sudo journalctl -u $unit -n 20 --no-pager -o short-iso" -AllowFail | Write-Host
  exit 1
}
