param(
  [ValidateSet("all", "sim", "server", "client")]
  [string] $Section = "all"
)

$ErrorActionPreference = "Stop"
$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")

$projects = @{
  sim = "tests/Avalon.Sim.Tests/Avalon.Sim.Tests.csproj"
  server = "tests/Avalon.Server.Tests/Avalon.Server.Tests.csproj"
  client = "tests/Avalon.Client.Tests/Avalon.Client.Tests.csproj"
}

$sections = if ($Section -eq "all") { @("sim", "server", "client") } else { @($Section) }
foreach ($name in $sections) {
  & dotnet test (Join-Path $repoRoot $projects[$name])
  if ($LASTEXITCODE -ne 0) {
    exit $LASTEXITCODE
  }
}
