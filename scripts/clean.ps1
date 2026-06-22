param()

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$godotTemp = Join-Path $repoRoot "client/.godot/mono/temp"
$targets = @(
  (Join-Path $godotTemp "bin"),
  (Join-Path $godotTemp "obj")
)

foreach ($target in $targets) {
  if (-not (Test-Path -LiteralPath $target)) {
    Write-Host "[clean] skip missing $target"
    continue
  }

  Remove-Item -LiteralPath $target -Recurse -Force
  Write-Host "[clean] removed $target"
}

Write-Host "[clean] Godot mono cache cleared."
