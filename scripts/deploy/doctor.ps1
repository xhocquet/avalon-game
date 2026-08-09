# Read-only preflight: proves the remote can take a deploy before one is attempted.
param()

. (Join-Path $PSScriptRoot "_env.ps1")

$cfg = Import-DeployEnv
$failures = 0
function Fail { param([string] $Message) Write-Bad $Message; $script:failures++ }

Write-Step "Target '$($cfg.Target)'"

# Everything about the connection comes from ~/.ssh/config — show what it resolved to, so a
# wrong Host alias is obvious here rather than after a confusing auth failure.
$resolved = Get-SshResolved $cfg
if ($resolved.Count -eq 0) {
  Fail "ssh could not resolve '$($cfg.Target)' — check the Host alias in ~/.ssh/config"
  exit 1
}
Write-Ok "ssh_config -> $($resolved.user)@$($resolved.hostname):$($resolved.port)"
if ($resolved.identityfile) { Write-Ok "identity     $($resolved.identityfile)" }

Write-Step "SSH reachability"
$whoami = Invoke-Remote $cfg "id -un" -AllowFail
if ($LASTEXITCODE -ne 0) {
  Fail "cannot connect: $($whoami -join ' ')"
  Write-Host "`nBatchMode is on, so a passphrase or password prompt reads as a failure."
  Write-Host "Load the key with ssh-add first, or check that '$($cfg.Target)' is right in ~/.ssh/config."
  exit 1
}
Write-Ok "connected as $whoami"

Write-Step "Host"
$os = Invoke-Remote $cfg ". /etc/os-release 2>/dev/null && echo `$PRETTY_NAME || uname -sr" -AllowFail
Write-Ok "$os ($(Invoke-Remote $cfg 'uname -m' -AllowFail))"

Invoke-Remote $cfg "command -v systemctl >/dev/null" -AllowFail | Out-Null
if ($LASTEXITCODE -ne 0) { Fail "systemctl not found — this tooling assumes systemd" }
else { Write-Ok "systemd present" }

Write-Step "Privileges"
$sudo = $cfg.AVALON_SUDO
if ($sudo) {
  Invoke-Remote $cfg "$sudo -n true" -AllowFail | Out-Null
  if ($LASTEXITCODE -ne 0) { Fail "'$sudo -n true' failed — needs passwordless sudo, or set AVALON_SUDO= if already root" }
  else { Write-Ok "passwordless $sudo" }
} else {
  Write-Ok "AVALON_SUDO empty — assuming the deploy user is privileged"
}

Write-Step "Remote layout"
$dir = $cfg.AVALON_REMOTE_DIR
$exists = Invoke-Remote $cfg "test -d '$dir' && echo yes || echo no" -AllowFail
if ($exists -eq "yes") {
  Write-Ok "$dir exists"
  $current = Invoke-Remote $cfg "readlink -f '$($cfg.CurrentDir)' 2>/dev/null || echo '(none)'" -AllowFail
  Write-Ok "current -> $current"
  $count = Invoke-Remote $cfg "ls -1 '$($cfg.ReleasesDir)' 2>/dev/null | wc -l" -AllowFail
  Write-Ok "$count release(s) on disk"
} else {
  Write-Warn "$dir missing — run ``just deploy-setup`` to provision"
}

Write-Step "Service"
$unit = "$($cfg.AVALON_SERVICE).service"
$state = Invoke-Remote $cfg "systemctl is-active $unit 2>/dev/null || true" -AllowFail
$enabled = Invoke-Remote $cfg "systemctl is-enabled $unit 2>/dev/null || echo 'not-installed'" -AllowFail
Write-Ok "$unit is-active=$state is-enabled=$enabled"

Write-Step "Tooling"
foreach ($tool in @("tar", "gzip")) {
  Invoke-Remote $cfg "command -v $tool >/dev/null" -AllowFail | Out-Null
  if ($LASTEXITCODE -ne 0) { Fail "$tool missing on the remote" } else { Write-Ok "$tool present" }
}

Write-Step "Disk"
# Parenthesised so the fallback runs when the dir is missing — `tail` alone always exits 0.
Write-Ok (Invoke-Remote $cfg "(df -h '$($cfg.AVALON_REMOTE_DIR)' 2>/dev/null || df -h /) | tail -1" -AllowFail)

# The game transport is UDP, a separate port namespace from TCP — a web server on :443/tcp is
# not a conflict. Only another UDP listener on the same port is.
Write-Step "Game port $($cfg.AVALON_GAME_PORT)/udp"
$holder = (Invoke-Remote $cfg "$sudo ss -lunp 2>/dev/null | awk '`$5 ~ /:$($cfg.AVALON_GAME_PORT)`$/ {print `$NF}' | head -1" -AllowFail | Out-String).Trim()
if (-not $holder) {
  Write-Warn "free (server down, or not deployed yet)"
} elseif ($holder -match [regex]::Escape($cfg.AVALON_SERVICE) -or $holder -match "Meesles") {
  Write-Ok "bound by our service"
} else {
  Fail "held by another process: $holder — pick a different AVALON_GAME_PORT"
}

Write-Step "Neighbours"
$tcp = (Invoke-Remote $cfg "$sudo ss -lnt 2>/dev/null | grep -c LISTEN" -AllowFail | Out-String).Trim()
$udp = (Invoke-Remote $cfg "$sudo ss -lnu 2>/dev/null | tail -n +2 | grep -c . " -AllowFail | Out-String).Trim()
Write-Ok "$tcp TCP listener(s), $udp UDP listener(s) already on the host"

# Local publish toolchain — a linux-x64 self-contained build needs the runtime pack restored.
Write-Step "Local toolchain"
$sdk = & dotnet --version 2>$null
if ($LASTEXITCODE -ne 0) { Fail "dotnet SDK not on PATH" } else { Write-Ok "dotnet SDK $sdk" }
if (-not (Get-Command tar.exe -ErrorAction SilentlyContinue)) { Fail "tar.exe not on PATH" } else { Write-Ok "tar.exe present" }

Write-Host ""
if ($failures -gt 0) { Write-Bad "$failures check(s) failed."; exit 1 }
Write-Ok "All checks passed."
