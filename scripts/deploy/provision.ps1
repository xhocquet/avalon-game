# One-time remote setup: service user, directory layout, systemd unit. Safe to re-run —
# it rewrites the unit from the template and leaves releases alone.
param(
  [switch] $OpenFirewall   # also allow the game port through ufw/firewalld
)

. (Join-Path $PSScriptRoot "_env.ps1")

$cfg = Import-DeployEnv
$sudo = $cfg.AVALON_SUDO
$dir = $cfg.AVALON_REMOTE_DIR
$runUser = $cfg.AVALON_RUN_USER
$unit = "$($cfg.AVALON_SERVICE).service"

Write-Step "Provisioning $($cfg.Target)"

Write-Step "Service user '$runUser'"
Invoke-Remote $cfg "id -u '$runUser' >/dev/null 2>&1 || $sudo useradd --system --no-create-home --shell /usr/sbin/nologin '$runUser'"
Write-Ok "present"

Write-Step "Directory layout under $dir"
# logs/ and results/ live outside releases/ so a deploy or prune never takes match history with it.
Invoke-Remote $cfg "$sudo mkdir -p '$dir/releases' '$dir/logs' '$dir/results' && $sudo chown -R '${runUser}:${runUser}' '$dir'"
# The deploy user unpacks into releases/ directly, so it needs write access there. Resolved
# remotely rather than from .env, since ~/.ssh/config owns who we log in as.
Invoke-Remote $cfg "$sudo chown `"`$(id -un):`" '$dir' '$dir/releases'"
Write-Ok "releases/ logs/ results/"

Write-Step "Installing $unit"
$template = Get-Content -LiteralPath (Join-Path $PSScriptRoot "avalon-server.service.template") -Raw
$rendered = $template `
  -replace '@RUN_USER@', $runUser `
  -replace '@REMOTE_DIR@', $dir `
  -replace '@GAME_PORT@', $cfg.AVALON_GAME_PORT `
  -replace '@LOG_LEVEL@', $cfg.AVALON_LOG_LEVEL `
  -replace '@SERVICE@', $cfg.AVALON_SERVICE
$rendered = $rendered -replace "`r`n", "`n"

$tempUnit = Join-Path ([IO.Path]::GetTempPath()) $unit
Set-Content -LiteralPath $tempUnit -Value $rendered -NoNewline -Encoding utf8
try {
  Send-ToRemote $cfg $tempUnit "$sudo tee /etc/systemd/system/$unit >/dev/null"
} finally {
  Remove-Item -LiteralPath $tempUnit -Force -ErrorAction SilentlyContinue
}

Invoke-Remote $cfg "$sudo systemctl daemon-reload && $sudo systemctl enable $unit"
Write-Ok "installed and enabled (not started — no release yet)"

if ($OpenFirewall) {
  Write-Step "Firewall: $($cfg.AVALON_GAME_PORT)/udp"
  $result = Invoke-Remote $cfg @"
if command -v ufw >/dev/null && ufw status | grep -q active; then
  $sudo ufw allow $($cfg.AVALON_GAME_PORT)/udp && echo 'ufw rule added'
elif command -v firewall-cmd >/dev/null; then
  $sudo firewall-cmd --permanent --add-port=$($cfg.AVALON_GAME_PORT)/udp && $sudo firewall-cmd --reload && echo 'firewalld rule added'
else
  echo 'no active ufw/firewalld — check your cloud provider security group'
fi
"@ -AllowFail
  Write-Ok $result
} else {
  Write-Warn "Firewall untouched. $($cfg.AVALON_GAME_PORT)/udp must be open — re-run with -OpenFirewall, or edit your cloud security group."
}

Write-Host ""
Write-Ok "Provisioned. Next: just deploy"
