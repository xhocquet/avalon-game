# Headless smoke test: boots the dedicated server + two headless Godot clients and
# asserts the in-engine self-check (`=== CLIENT OK ===`, emitted by MultiplayerGameNode's
# #if DEBUG AutoTestStep at tick 120) passes, with no server-side simulation exceptions.
#
# Two clients are required because the server's sessionconfig sets MinPlayers=2, so a
# match won't start with one. Client startup is staggered to avoid a Godot user-log
# file-lock collision when two instances launch in the same second.
param(
  [int]    $Port = 7777,
  [int]    $TimeoutSeconds = 90,
  [string] $Godot = $(if ($env:GODOT) { $env:GODOT } else { "C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe" })
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$logDir = Join-Path ([System.IO.Path]::GetTempPath()) ("avalon-smoke-" + (Get-Date -Format "yyyyMMdd-HHmmss"))
New-Item -ItemType Directory -Path $logDir -Force | Out-Null

$srvOut = Join-Path $logDir "server.out.log"
$srvErr = Join-Path $logDir "server.err.log"
$c1Out  = Join-Path $logDir "client1.out.log"
$c1Err  = Join-Path $logDir "client1.err.log"
$c2Out  = Join-Path $logDir "client2.out.log"
$c2Err  = Join-Path $logDir "client2.err.log"

$server = $null
$c1 = $null
$c2 = $null
$exit = 1

function Stop-Tree {
  if ($script:c1) { Stop-Process -Id $script:c1.Id -Force -ErrorAction SilentlyContinue }
  if ($script:c2) { Stop-Process -Id $script:c2.Id -Force -ErrorAction SilentlyContinue }
  Get-Process "Godot_v4.6.3-stable_mono_win64" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
  if ($script:server) { Stop-Process -Id $script:server.Id -Force -ErrorAction SilentlyContinue }
  Get-Process "Meesles.Avalon.Server" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

try {
  if (-not (Test-Path $Godot)) { throw "Godot binary not found: $Godot (set `$env:GODOT to override)" }

  Write-Host "[smoke] logs: $logDir"

  # Clear any stale Godot clients that would hold the 2 player slots (RoomFull).
  Get-Process "Godot_v4.6.3-stable_mono_win64" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue

  Write-Host "[smoke] building server + client..."
  & dotnet build (Join-Path $repoRoot "server/Server.csproj") -c Debug | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "server build failed" }
  & dotnet build (Join-Path $repoRoot "client/Client.csproj") -c Debug | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "client build failed" }

  Write-Host "[smoke] starting server on port $Port..."
  $server = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", (Join-Path $repoRoot "server/Server.csproj"), "--", "$Port") `
    -WorkingDirectory $repoRoot -RedirectStandardOutput $srvOut -RedirectStandardError $srvErr `
    -PassThru -WindowStyle Hidden
  Start-Sleep -Seconds 6

  Write-Host "[smoke] launching client 1..."
  $c1 = Start-Process -FilePath $Godot -ArgumentList @("--headless", "--path", (Join-Path $repoRoot "client")) `
    -RedirectStandardOutput $c1Out -RedirectStandardError $c1Err -PassThru
  Start-Sleep -Seconds 3
  Write-Host "[smoke] launching client 2..."
  $c2 = Start-Process -FilePath $Godot -ArgumentList @("--headless", "--path", (Join-Path $repoRoot "client")) `
    -RedirectStandardOutput $c2Out -RedirectStandardError $c2Err -PassThru

  Write-Host "[smoke] waiting up to ${TimeoutSeconds}s for clients to finish (auto-quit at tick 120)..."
  $c1 | Wait-Process -Timeout $TimeoutSeconds -ErrorAction SilentlyContinue
  $c2 | Wait-Process -Timeout $TimeoutSeconds -ErrorAction SilentlyContinue

  $c1Text  = if (Test-Path $c1Out) { Get-Content $c1Out -Raw } else { "" }
  $srvText = $(if (Test-Path $srvOut) { Get-Content $srvOut -Raw }) + $(if (Test-Path $srvErr) { Get-Content $srvErr -Raw })

  $clientOk    = $c1Text -match "=== CLIENT OK ==="
  $clientFail  = $c1Text -match "=== CLIENT FAILED ===" -or $c1Text -match "join failed"
  $srvCrashed  = $srvText -match "Update exception" -or $srvText -match "Index was outside the bounds"

  $viewNodes = if ($c1Text -match "viewNodes=(\d+)") { $matches[1] } else { "?" }

  Write-Host ""
  Write-Host "[smoke] client1 self-check : $(if ($clientOk) { 'OK' } else { 'FAIL' }) (viewNodes=$viewNodes)"
  Write-Host "[smoke] server exceptions  : $(if ($srvCrashed) { 'FOUND (regression!)' } else { 'none' })"

  if ($clientOk -and -not $clientFail -and -not $srvCrashed) {
    Write-Host "[smoke] PASS" -ForegroundColor Green
    $exit = 0
  } else {
    Write-Host "[smoke] FAIL - see logs in $logDir" -ForegroundColor Red
    $exit = 1
  }
}
finally {
  Stop-Tree
}

exit $exit
