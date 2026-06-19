# Visual multiplayer run: boots the normal two-player server and opens two windowed
# clients. Close both game windows to tear the server down.
param(
  [int]    $Port = 7777,
  [string] $Godot = $(if ($env:GODOT) { $env:GODOT } else { "C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe" })
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$server = $null
$client1 = $null
$client2 = $null
function Stop-Server {
  if ($script:client1) { Stop-Process -Id $script:client1.Id -Force -ErrorAction SilentlyContinue }
  if ($script:client2) { Stop-Process -Id $script:client2.Id -Force -ErrorAction SilentlyContinue }
  if ($script:server) { Stop-Process -Id $script:server.Id -Force -ErrorAction SilentlyContinue }
  Get-Process "Meesles.Avalon.Server" -ErrorAction SilentlyContinue | Stop-Process -Force -ErrorAction SilentlyContinue
}

try {
  if (-not (Test-Path $Godot)) { throw "Godot binary not found: $Godot (set `$env:GODOT to override)" }

  Write-Host "[play] building server + client..."
  & dotnet build (Join-Path $repoRoot "server/Server.csproj") -c Debug | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "server build failed" }
  & dotnet build (Join-Path $repoRoot "client/Client.csproj") -c Debug | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "client build failed" }

  Write-Host "[play] starting server on port $Port..."
  $server = Start-Process -FilePath "dotnet" `
    -ArgumentList @("run", "--project", (Join-Path $repoRoot "server/Server.csproj"), "--", "$Port") `
    -WorkingDirectory $repoRoot -PassThru -WindowStyle Hidden
  Start-Sleep -Seconds 6

  Write-Host "[play] launching client 1..."
  $client1 = Start-Process -FilePath $Godot -ArgumentList @("--path", (Join-Path $repoRoot "client")) -PassThru
  Start-Sleep -Seconds 3
  Write-Host "[play] launching client 2 - close both windows to stop."
  $client2 = Start-Process -FilePath $Godot -ArgumentList @("--path", (Join-Path $repoRoot "client")) -PassThru

  $client1 | Wait-Process
  $client2 | Wait-Process
}
finally {
  Stop-Server
}
