# Visual solo run: boots the server in solo mode (MinPlayers=1) and opens ONE windowed
# client so you can actually watch the simulation (minions spawning, team colors, etc).
#
# MaxPlayers stays 2 so the server still spawns both team bases + spawn points, meaning
# both colors of minions spawn even though only one client connects. The windowed DEBUG
# client auto-joins and stays open (the tick-120 auto-quit is headless-only).
#
# The solo config is generated in a temp dir and points the server at it via --config-dir,
# copying the real simulationconfig.json so MaxEntities etc. never drift from the committed
# one. Nothing in the repo is modified. Close the game window to tear the server down.
param(
  [int]    $Port = 7777,
  [string] $Godot = $(if ($env:GODOT) { $env:GODOT } else { "C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe" })
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$server = $null
function Stop-Server {
  if ($script:server) { Stop-Process -Id $script:server.Id -Force -ErrorAction SilentlyContinue }
  Get-Process "Meesles.Avalon.Server" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

try {
  if (-not (Test-Path $Godot)) { throw "Godot binary not found: $Godot (set `$env:GODOT to override)" }

  # Generate a solo config dir (MinPlayers=1) without touching committed config.
  $soloDir = Join-Path ([System.IO.Path]::GetTempPath()) "avalon-solo-config"
  New-Item -ItemType Directory -Path $soloDir -Force | Out-Null
  $session = @{
    MaxPlayers = 2
    MinPlayers = 1
    AllowLateJoin = $false
    MaxSpectators = 0
    CountdownDurationMs = 3000
  }
  ($session | ConvertTo-Json) | Set-Content -Path (Join-Path $soloDir "sessionconfig.json") -Encoding ASCII
  Copy-Item (Join-Path $repoRoot "server/simulationconfig.json") (Join-Path $soloDir "simulationconfig.json") -Force

  Write-Host "[play] building server + client..."
  & dotnet build (Join-Path $repoRoot "server/Server.csproj") -c Debug | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "server build failed" }
  & dotnet build (Join-Path $repoRoot "client/Client.csproj") -c Debug | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "client build failed" }

  Write-Host "[play] starting solo server on port $Port (MinPlayers=1)..."
  $server = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", (Join-Path $repoRoot "server/Server.csproj"), "--", "$Port", "Information", "--config-dir", $soloDir) `
    -WorkingDirectory $repoRoot -PassThru -WindowStyle Hidden
  Start-Sleep -Seconds 6

  Write-Host "[play] launching windowed client - close the window to stop."
  $client = Start-Process -FilePath $Godot -ArgumentList @("--path", (Join-Path $repoRoot "client")) -PassThru
  $client | Wait-Process
}
finally {
  Stop-Server
}
