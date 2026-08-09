# Shared config + SSH plumbing for the deploy scripts. Dot-source, don't run.

$ErrorActionPreference = "Stop"

# systemd and journalctl emit UTF-8; without this the console renders "→" as mojibake.
[Console]::OutputEncoding = [Text.Encoding]::UTF8

$script:RepoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path

$script:EnvDefaults = @{
  AVALON_SUDO           = "sudo"
  AVALON_REMOTE_DIR     = "/opt/avalon"
  AVALON_SERVICE        = "avalon-server"
  AVALON_RUN_USER       = "avalon"
  AVALON_GAME_PORT      = "7777"
  AVALON_LOG_LEVEL      = "Information"
  AVALON_KEEP_RELEASES  = "5"
}

# Only the host. User, port and key are whatever ~/.ssh/config resolves for it.
$script:EnvRequired = @("AVALON_SSH_HOST")

function Import-DeployEnv {
  param([string] $Path)

  if (-not $Path) { $Path = Join-Path $script:RepoRoot ".env" }

  if (-not (Test-Path -LiteralPath $Path)) {
    throw "No .env at $Path. Copy .env.example to .env and fill it in."
  }

  $cfg = @{}
  foreach ($key in $script:EnvDefaults.Keys) { $cfg[$key] = $script:EnvDefaults[$key] }

  foreach ($line in Get-Content -LiteralPath $Path) {
    $trimmed = $line.Trim()
    if (-not $trimmed -or $trimmed.StartsWith("#")) { continue }

    $split = $trimmed.IndexOf("=")
    if ($split -lt 1) { continue }

    $key = $trimmed.Substring(0, $split).Trim()
    $value = $trimmed.Substring($split + 1).Trim()
    if ($value.Length -ge 2 -and (($value[0] -eq '"' -and $value[-1] -eq '"') -or
        ($value[0] -eq "'" -and $value[-1] -eq "'"))) {
      $value = $value.Substring(1, $value.Length - 2)
    }
    $cfg[$key] = $value
  }

  $missing = $script:EnvRequired | Where-Object { -not $cfg[$_] }
  if ($missing) { throw "$Path is missing: $($missing -join ', ')" }

  $cfg.RepoRoot = $script:RepoRoot
  $cfg.Target = $cfg.AVALON_SSH_HOST
  $cfg.ReleasesDir = "$($cfg.AVALON_REMOTE_DIR)/releases"
  $cfg.CurrentDir = "$($cfg.AVALON_REMOTE_DIR)/current"
  return $cfg
}

# Godot reads these before the preset's own keystore fields (EditorExportPreset::get_or_env), so
# signing config stays in .env and never reaches client/.godot/export_credentials.cfg.
function Set-AndroidKeystoreEnv {
  param([hashtable] $Cfg)

  $map = @{
    AVALON_ANDROID_KEYSTORE_DEBUG_PATH       = "GODOT_ANDROID_KEYSTORE_DEBUG_PATH"
    AVALON_ANDROID_KEYSTORE_DEBUG_USER       = "GODOT_ANDROID_KEYSTORE_DEBUG_USER"
    AVALON_ANDROID_KEYSTORE_DEBUG_PASSWORD   = "GODOT_ANDROID_KEYSTORE_DEBUG_PASSWORD"
    AVALON_ANDROID_KEYSTORE_RELEASE_PATH     = "GODOT_ANDROID_KEYSTORE_RELEASE_PATH"
    AVALON_ANDROID_KEYSTORE_RELEASE_USER     = "GODOT_ANDROID_KEYSTORE_RELEASE_USER"
    AVALON_ANDROID_KEYSTORE_RELEASE_PASSWORD = "GODOT_ANDROID_KEYSTORE_RELEASE_PASSWORD"
  }

  $set = 0
  foreach ($key in $map.Keys) {
    $value = $Cfg[$key]
    if (-not $value) { continue }
    [Environment]::SetEnvironmentVariable($map[$key], $value, "Process")
    $set++
  }
  return $set
}

function Get-SshArgs {
  param([hashtable] $Cfg)

  # BatchMode turns a would-be interactive prompt into a fast failure rather than a hung script.
  return @("-o", "BatchMode=yes", "-o", "ConnectTimeout=10")
}

# What ~/.ssh/config actually resolves for the host, so the scripts can report it.
function Get-SshResolved {
  param([hashtable] $Cfg)

  $resolved = @{}
  $lines = & ssh -G $Cfg.Target 2>$null
  if ($LASTEXITCODE -ne 0) { return $resolved }

  foreach ($line in $lines) {
    $split = "$line".IndexOf(" ")
    if ($split -lt 1) { continue }
    $key = "$line".Substring(0, $split)
    if ($key -in @("user", "hostname", "port", "identityfile") -and -not $resolved.ContainsKey($key)) {
      $resolved[$key] = "$line".Substring($split + 1).Trim()
    }
  }
  return $resolved
}

# Remote commands carry &&, >, quotes and newlines. Going through ProcessStartInfo.ArgumentList
# hands ssh.exe one argv entry verbatim; a shell-interpolated command line would not survive.
function Start-Ssh {
  param(
    [hashtable] $Cfg,
    [string] $Command,
    [string] $StdinFile
  )

  $psi = [Diagnostics.ProcessStartInfo]::new()
  $psi.FileName = "ssh"
  foreach ($arg in ((Get-SshArgs $Cfg) + @($Cfg.Target, $Command))) { $psi.ArgumentList.Add($arg) }
  $psi.UseShellExecute = $false
  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.RedirectStandardInput = [bool] $StdinFile

  $proc = [Diagnostics.Process]::Start($psi)
  # Both pipes are drained concurrently: a remote command that fills either buffer would
  # otherwise block forever waiting for a reader.
  $stdout = $proc.StandardOutput.ReadToEndAsync()
  $stderr = $proc.StandardError.ReadToEndAsync()

  if ($StdinFile) {
    $source = [IO.File]::OpenRead($StdinFile)
    try { $source.CopyTo($proc.StandardInput.BaseStream) } finally { $source.Dispose() }
    $proc.StandardInput.Close()
  }

  $proc.WaitForExit()
  return @{
    ExitCode = $proc.ExitCode
    Output   = ("$($stdout.Result)`n$($stderr.Result)").Trim()
  }
}

# Runs a command on the remote. Returns its stdout+stderr lines; $LASTEXITCODE holds the status.
function Invoke-Remote {
  param(
    [hashtable] $Cfg,
    [string] $Command,
    [switch] $AllowFail
  )

  $result = Start-Ssh -Cfg $Cfg -Command $Command
  $global:LASTEXITCODE = $result.ExitCode
  if ($result.ExitCode -ne 0 -and -not $AllowFail) {
    throw "Remote command failed (exit $($result.ExitCode)): $Command`n$($result.Output)"
  }
  if (-not $result.Output) { return @() }
  return $result.Output -split "`r?`n"
}

# Pipes a local file into a remote command's stdin — no scp, and no writable staging dir needed.
function Send-ToRemote {
  param(
    [hashtable] $Cfg,
    [string] $LocalPath,
    [string] $RemoteCommand
  )

  $result = Start-Ssh -Cfg $Cfg -Command $RemoteCommand -StdinFile $LocalPath
  $global:LASTEXITCODE = $result.ExitCode
  if ($result.ExitCode -ne 0) {
    throw "Upload failed (exit $($result.ExitCode)): $RemoteCommand`n$($result.Output)"
  }
  return $result.Output
}

function Write-Step { param([string] $Message) Write-Host "==> $Message" -ForegroundColor Cyan }
function Write-Ok   { param([string] $Message) Write-Host "  OK  $Message" -ForegroundColor Green }
function Write-Warn { param([string] $Message) Write-Host "  !!  $Message" -ForegroundColor Yellow }
function Write-Bad  { param([string] $Message) Write-Host "  XX  $Message" -ForegroundColor Red }
