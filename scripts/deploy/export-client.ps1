param(
  [ValidateSet("Windows Desktop", "Linux")]
  [string] $Preset = "Windows Desktop",
  [switch] $Debug,          # export_debug: keeps the console wrapper and debug asserts
  [switch] $NoZip,
  # Not -Host/-Port: $Host is a PowerShell automatic variable and cannot be a parameter name.
  [string] $ServerHost,
  [int] $ServerPort
)

. (Join-Path $PSScriptRoot "_env.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$clientDir = Join-Path $repoRoot "client"
$godotConsole = "C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64_console.exe"
$endpointFile = Join-Path $clientDir "server_endpoint.json"

# The endpoint is the deploy target unless overridden, so a client build and the server it talks
# to cannot drift apart.
$cfg = Import-DeployEnv
$resolved = Get-SshResolved $cfg
$targetHost = if ($ServerHost) { $ServerHost } elseif ($resolved.hostname) { $resolved.hostname } else { $cfg.AVALON_SSH_HOST }
$targetPort = if ($ServerPort) { $ServerPort } else { [int] $cfg.AVALON_GAME_PORT }

if (-not (Test-Path -LiteralPath $godotConsole)) { throw "Godot not found at $godotConsole" }

# No-op until .env carries AVALON_ANDROID_KEYSTORE_*. Count only — the values are secrets.
$keystoreVars = Set-AndroidKeystoreEnv $cfg
if ($keystoreVars) { Write-Ok "Android keystore: $keystoreVars var(s) from .env" }

Write-Step "Baking endpoint ${targetHost}:${targetPort}"
$endpoint = [ordered]@{
  host      = $targetHost
  port      = $targetPort
  bakedAtUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss'Z'")
}
Set-Content -LiteralPath $endpointFile -Value ($endpoint | ConvertTo-Json) -Encoding utf8
Write-Ok "client/server_endpoint.json"

try {
  Write-Step "Regenerating data assets"
  & dotnet run --project (Join-Path $repoRoot "tools\AssetGen") --nologo -v q
  if ($LASTEXITCODE -ne 0) { throw "AssetGen failed." }

  Write-Step "Building client assembly"
  & dotnet build (Join-Path $clientDir "Meesles.Avalon.Client.csproj") -c Release --nologo -v q
  if ($LASTEXITCODE -ne 0) { throw "Client build failed." }

  # A new file in the project root is invisible to an export until the editor imports it.
  Write-Step "Importing resources"
  & $godotConsole --headless --editor --quit --path $clientDir 2>&1 | Out-Null

  $outputName = if ($Preset -eq "Linux") { "Avalon.x86_64" } else { "Avalon.exe" }
  $outputDir = Join-Path $repoRoot ".tmp\client\$(if ($Preset -eq 'Linux') { 'linux' } else { 'windows' })"
  if (Test-Path -LiteralPath $outputDir) { Remove-Item -LiteralPath $outputDir -Recurse -Force }
  New-Item -ItemType Directory -Force -Path $outputDir | Out-Null

  $exportFlag = if ($Debug) { "--export-debug" } else { "--export-release" }
  Write-Step "Exporting '$Preset' ($(if ($Debug) { 'debug' } else { 'release' }))"
  $log = & $godotConsole --headless --path $clientDir $exportFlag $Preset (Join-Path $outputDir $outputName) 2>&1
  # Anchored on the colon: a bare "ERROR" also matches asset paths like ErrorVisualState.cs.
  $log | Where-Object { $_ -match 'ERROR:|error:|No export template|Cannot find' } | ForEach-Object { Write-Warn $_ }

  $exportedExe = Join-Path $outputDir $outputName
  if (-not (Test-Path -LiteralPath $exportedExe)) {
    Write-Bad "Export produced no binary."
    # Overwhelmingly the cause: templates absent, or a version/flavour mismatch with the editor.
    # The .NET editor needs .NET templates — the directory name carries a .mono suffix.
    $templateDir = Join-Path $env:APPDATA "Godot\export_templates"
    $version = ((& $godotConsole --version) | Select-Object -First 1)
    $expected = ($version -split '\.official')[0]

    Write-Host ""
    Write-Host "Editor version : $version"
    Write-Host "Needs templates: $templateDir\$expected"
    Write-Host "Installed      :"
    if (Test-Path -LiteralPath $templateDir) {
      Get-ChildItem -LiteralPath $templateDir -Directory | ForEach-Object { Write-Host "  $($_.Name)" }
    } else {
      Write-Host "  (none)"
    }
    $tpz = "Godot_v$($expected -replace '\.stable\.mono', '-stable_mono')_export_templates.tpz"
    Write-Host ""
    Write-Host "Install via Editor > Manage Export Templates > Download and Install, or unpack"
    Write-Host "$tpz into that directory."
    throw "Export failed — see above."
  }

  $sizeMb = [math]::Round(((Get-ChildItem -LiteralPath $outputDir -Recurse | Measure-Object Length -Sum).Sum / 1MB), 1)
  Write-Ok "$outputDir ($sizeMb MB)"

  if (-not $NoZip) {
    Write-Step "Zipping"
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $zip = Join-Path $repoRoot ".tmp\client\Avalon-$(if ($Preset -eq 'Linux') { 'linux' } else { 'win64' })-$stamp.zip"
    Compress-Archive -Path (Join-Path $outputDir "*") -DestinationPath $zip -Force
    Write-Ok "$zip ($([math]::Round((Get-Item -LiteralPath $zip).Length / 1MB, 1)) MB)"
  }

  Write-Host ""
  Write-Ok "Client points at ${targetHost}:${targetPort}"
}
finally {
  # Left behind, a working copy would silently connect to the deployed server instead of localhost.
  Remove-Item -LiteralPath $endpointFile -Force -ErrorAction SilentlyContinue
  Remove-Item -LiteralPath "$endpointFile.import" -Force -ErrorAction SilentlyContinue
}
