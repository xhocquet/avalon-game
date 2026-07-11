# Quick multiplayer run: same build + launch as `play`, but clients auto-join and
# auto-ready so you land in-game without touching the lobby UI.
param(
  [int]    $Port = 7777,
  [int]    $Ticks = 0,
  [int]    $Faction1 = 200,
  [int]    $Faction2 = 201,
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

  Write-Host "[quickplay] building server + client..."
  & dotnet build (Join-Path $repoRoot "server/Server.csproj") -c Debug | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "server build failed" }

  $runtimeSrc = Join-Path $repoRoot "vendor/Klotho/com.xpturn.klotho/Godot~/bin/Debug/net8.0/xpTURN.Klotho.Runtime.dll"
  $runtimeDst = Join-Path $repoRoot "client/addons/klotho/lib/xpTURN.Klotho.Runtime.dll"
  $klothoSrcDir = Join-Path $repoRoot "vendor/Klotho/com.xpturn.klotho/Godot~"
  $newestSrc = Get-ChildItem -Recurse $klothoSrcDir -Include "*.cs","*.csproj" |
    Where-Object { $_.FullName -notmatch '\\(bin|obj)\\' } |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1
  $klothoUpToDate = (Test-Path $runtimeDst) -and $newestSrc -and
    ((Get-Item $runtimeDst).LastWriteTime -gt $newestSrc.LastWriteTime)
  if ($klothoUpToDate) {
    Write-Host "[quickplay] Klotho runtime up-to-date, skipping build."
  } else {
    Write-Host "[quickplay] building Klotho runtime from source..."
    & dotnet build (Join-Path $repoRoot "vendor/Klotho/com.xpturn.klotho/Godot~/xpTURN.Klotho.Runtime.csproj") -c Debug | Out-Null
    if ($LASTEXITCODE -ne 0) { throw "Klotho runtime build failed" }
    Copy-Item -Force $runtimeSrc $runtimeDst
  }

  & dotnet build (Join-Path $repoRoot "client/Meesles.Avalon.Client.csproj") -c Debug | Out-Null
  if ($LASTEXITCODE -ne 0) { throw "client build failed" }

  $serverArgs = @("run", "--project", (Join-Path $repoRoot "server/Server.csproj"), "--", "$Port")
  if ($Ticks -gt 0) {
    $serverArgs += @("Information", "$Ticks")
    Write-Host "[quickplay] starting server on port $Port (fast-forward $Ticks ticks)..."
  } else {
    Write-Host "[quickplay] starting server on port $Port..."
  }
  $server = Start-Process -FilePath "dotnet" `
    -ArgumentList $serverArgs `
    -WorkingDirectory $repoRoot -PassThru -WindowStyle Normal
  Start-Sleep -Seconds 6

  Write-Host "[quickplay] launching client 1 (faction $Faction1)..."
  $client1 = Start-Process -FilePath $Godot `
    -ArgumentList @("--path", (Join-Path $repoRoot "client"), "--", "--quickplay", "--faction=$Faction1") -RedirectStandardError "NUL" -PassThru
  Start-Sleep -Seconds 2
  Write-Host "[quickplay] launching client 2 (faction $Faction2) - close both windows to stop."
  $client2 = Start-Process -FilePath $Godot `
    -ArgumentList @("--path", (Join-Path $repoRoot "client"), "--", "--quickplay", "--faction=$Faction2") -RedirectStandardError "NUL" -PassThru

  $client1 | Wait-Process
  $client2 | Wait-Process
}
finally {
  Stop-Server
}
