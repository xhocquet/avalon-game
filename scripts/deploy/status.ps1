param(
  [int] $Lines = 20
)

. (Join-Path $PSScriptRoot "_env.ps1")

$cfg = Import-DeployEnv
$sudo = $cfg.AVALON_SUDO
$unit = "$($cfg.AVALON_SERVICE).service"

Write-Step "$unit on $($cfg.Target)"

# One round trip: each extra Invoke-Remote is a fresh SSH handshake.
$summary = Invoke-Remote $cfg @"
echo "STATE=`$(systemctl is-active $unit 2>/dev/null || echo unknown)"
echo "ENABLED=`$(systemctl is-enabled $unit 2>/dev/null || echo unknown)"
echo "SINCE=`$(systemctl show $unit -p ActiveEnterTimestamp --value 2>/dev/null)"
echo "PID=`$(systemctl show $unit -p MainPID --value 2>/dev/null)"
echo "MEM=`$(systemctl show $unit -p MemoryCurrent --value 2>/dev/null)"
echo "RESTARTS=`$(systemctl show $unit -p NRestarts --value 2>/dev/null)"
echo "RELEASE=`$(basename "`$(readlink -f '$($cfg.CurrentDir)' 2>/dev/null)" 2>/dev/null || echo none)"
echo "RELEASES=`$(ls -1d '$($cfg.ReleasesDir)'/*/ 2>/dev/null | wc -l)"
echo "BOUND=`$(ss -lun 2>/dev/null | grep -c ':$($cfg.AVALON_GAME_PORT) ' || echo 0)"
echo "MATCHES=`$(ls -1 '$($cfg.AVALON_REMOTE_DIR)/results'/*.json 2>/dev/null | wc -l)"
echo "DISK=`$(df -h '$($cfg.AVALON_REMOTE_DIR)' 2>/dev/null | tail -1 | awk '{print `$4" free of "`$2}')"
echo "UPTIME=`$(uptime -p 2>/dev/null)"
echo "LOAD=`$(cut -d' ' -f1-3 /proc/loadavg 2>/dev/null)"
"@ -AllowFail

$v = @{}
foreach ($line in $summary) {
  $split = "$line".IndexOf("=")
  if ($split -gt 0) { $v["$line".Substring(0, $split)] = "$line".Substring($split + 1).Trim() }
}

$mem = "n/a"
if ($v.MEM -match '^\d+$' -and [long]$v.MEM -gt 0) { $mem = "$([math]::Round([long]$v.MEM / 1MB, 1)) MB" }

if ($v.STATE -eq "active") { Write-Ok "state    active (pid $($v.PID), $mem)" }
else { Write-Bad "state    $($v.STATE)" }

Write-Host "  ..  enabled  $($v.ENABLED)"
Write-Host "  ..  since    $($v.SINCE)"
Write-Host "  ..  restarts $($v.RESTARTS)"
Write-Host "  ..  release  $($v.RELEASE)  ($($v.RELEASES) on disk)"

if ($v.BOUND -match '^[1-9]') { Write-Ok "port     $($cfg.AVALON_GAME_PORT)/udp bound" }
else { Write-Bad "port     $($cfg.AVALON_GAME_PORT)/udp not bound" }

Write-Host "  ..  matches  $($v.MATCHES) result file(s)"
Write-Host "  ..  disk     $($v.DISK)"
Write-Host "  ..  host     $($v.UPTIME), load $($v.LOAD)"

Write-Step "Last $Lines log lines"
Invoke-Remote $cfg "$sudo journalctl -u $unit -n $Lines --no-pager -o short-iso" -AllowFail | Write-Host
