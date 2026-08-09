param(
  [switch] $SkipAssets,       # skip AssetGen; use the .bytes already in client/Sim/Data
  [string] $Configuration = "Release"
)

. (Join-Path $PSScriptRoot "_env.ps1")

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot "..\..")).Path
$publishDir = Join-Path $repoRoot ".tmp\publish"
$distDir = Join-Path $repoRoot ".tmp\dist"

# Loaded by Program.cs at startup from Data/ next to the executable; a publish missing any of
# these builds clean and then dies on the remote.
$requiredAssets = @("Assets.bytes", "MapLayout.bytes", "NavigationRegion3D.NavMeshData.bytes")

if (-not $SkipAssets) {
  Write-Step "Regenerating data assets"
  & dotnet run --project (Join-Path $repoRoot "tools\AssetGen") --nologo -v q
  if ($LASTEXITCODE -ne 0) { throw "AssetGen failed." }
}

Write-Step "Publishing server (linux-x64, self-contained, $Configuration)"
if (Test-Path -LiteralPath $publishDir) { Remove-Item -LiteralPath $publishDir -Recurse -Force }

# No PublishTrimmed: Klotho resolves commands/messages through generated registration and
# reflection, and the trimmer cannot see those roots.
& dotnet publish (Join-Path $repoRoot "server\Server.csproj") `
  -c $Configuration `
  -r linux-x64 `
  --self-contained true `
  -p:PublishTrimmed=false `
  -p:PublishSingleFile=false `
  -p:DebugType=none `
  -o $publishDir `
  --nologo
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed." }

Write-Step "Verifying payload"
$exe = Join-Path $publishDir "Meesles.Avalon.Server"
if (-not (Test-Path -LiteralPath $exe)) { throw "Publish produced no Meesles.Avalon.Server apphost." }

foreach ($asset in $requiredAssets) {
  $path = Join-Path $publishDir "Data\$asset"
  if (-not (Test-Path -LiteralPath $path)) {
    throw "Missing Data\$asset in the publish output. Run ``just export-scene-data`` and retry."
  }
}
foreach ($cfg in @("simulationconfig.json", "sessionconfig.json")) {
  if (-not (Test-Path -LiteralPath (Join-Path $publishDir $cfg))) { throw "Missing $cfg in the publish output." }
}
Write-Ok "$($requiredAssets.Count) data assets + 2 config files present"

# Stamp ties an artifact back to a commit; -dirty marks a build made over uncommitted edits.
$sha = (& git -C $repoRoot rev-parse --short HEAD 2>$null)
if ($LASTEXITCODE -ne 0 -or -not $sha) { $sha = "nogit" }
if (& git -C $repoRoot status --porcelain 2>$null) { $sha = "$sha-dirty" }
$stamp = "{0}-{1}" -f (Get-Date -Format "yyyyMMdd-HHmmss"), $sha

Write-Step "Packing avalon-server-$stamp.tar.gz"
New-Item -ItemType Directory -Force -Path $distDir | Out-Null
$tarball = Join-Path $distDir "avalon-server-$stamp.tar.gz"
& tar.exe -czf $tarball -C $publishDir .
if ($LASTEXITCODE -ne 0) { throw "tar failed." }

# Recorded so deploy.ps1 can ship the newest build without re-deriving the stamp.
Set-Content -LiteralPath (Join-Path $distDir "latest.txt") -Value $tarball -NoNewline

$sizeMb = [math]::Round((Get-Item -LiteralPath $tarball).Length / 1MB, 1)
Write-Ok "$tarball ($sizeMb MB)"
