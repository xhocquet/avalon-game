$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
Set-Location $repoRoot

$staged = @(git diff --cached --name-only --diff-filter=ACMR -- "*.cs" | ForEach-Object {
  $_.Replace("\", "/")
} | Where-Object {
  $_ -and
  -not $_.StartsWith("vendor/") -and
  -not $_.StartsWith("klotho-docs/") -and
  -not $_.StartsWith("client/addons/klotho/")
})

if ($staged.Count -eq 0) {
  exit 0
}

$unstaged = @(git diff --name-only --diff-filter=ACMR -- "*.cs" | ForEach-Object {
  $_.Replace("\", "/")
})

$mixed = @($staged | Where-Object { $unstaged -contains $_ })
if ($mixed.Count -gt 0) {
  Write-Error ("Refusing to format C# files with both staged and unstaged changes:`n  " + ($mixed -join "`n  "))
}

function Invoke-DotnetFormat {
  param(
    [string] $Project,
    [string[]] $Includes
  )

  if ($Includes.Count -eq 0) {
    return
  }

  dotnet format $Project whitespace --no-restore --include $Includes --verbosity quiet
}

$clientFiles = @($staged | Where-Object { $_.StartsWith("client/") })
$serverFiles = @($staged | Where-Object { $_.StartsWith("server/") })
$testsFiles  = @($staged | Where-Object { $_.StartsWith("tests/") })
$toolsFiles  = @($staged | Where-Object { $_.StartsWith("tools/") })

Invoke-DotnetFormat "client/Meesles.Avalon.Client.csproj" $clientFiles
Invoke-DotnetFormat "server/Server.csproj"                 $serverFiles
Invoke-DotnetFormat "tests/Avalon.Sim.Tests/Avalon.Sim.Tests.csproj" $testsFiles
Invoke-DotnetFormat "tools/AssetGen/AssetGen.csproj"       $toolsFiles

git add -- $staged
