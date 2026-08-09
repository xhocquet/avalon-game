param(
  [switch] $Deep   # also drop bin/obj across the solution, not just caches and publish output
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$godotTemp = Join-Path $repoRoot "client/.godot/mono/temp"
$targets = @(
  (Join-Path $godotTemp "bin"),
  (Join-Path $godotTemp "obj"),
  (Join-Path $repoRoot ".tmp/publish"),
  (Join-Path $repoRoot ".tmp/dist")
)

if ($Deep) {
  $targets += @(
    (Join-Path $repoRoot "server/bin"),
    (Join-Path $repoRoot "server/obj"),
    (Join-Path $repoRoot "TestResults")
  )
  $targets += @(Get-ChildItem -Path (Join-Path $repoRoot "tools"), (Join-Path $repoRoot "tests") `
      -Directory -Recurse -Include "bin", "obj" -ErrorAction SilentlyContinue | Select-Object -ExpandProperty FullName)
}

foreach ($target in $targets) {
  if (-not (Test-Path -LiteralPath $target)) {
    Write-Host "[clean] skip missing $target"
    continue
  }

  Remove-Item -LiteralPath $target -Recurse -Force
  Write-Host "[clean] removed $target"
}

Write-Host "[clean] done."
