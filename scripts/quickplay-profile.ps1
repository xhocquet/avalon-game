param(
    [ValidateSet('dotnet-sampled-thread-time', 'gc-verbose')]
    [string]$Profile = 'dotnet-sampled-thread-time',
    [string]$Godot = 'C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe',
    [string]$GameType = 'combat-playground',
    [switch]$Headless,
    [ValidateRange(0, 2147483647)]
    [int]$QuitAfter = 0,
    [Parameter(ValueFromRemainingArguments = $true)]
    [string[]]$GameArgs = @()
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent

if (-not (Test-Path -LiteralPath $Godot -PathType Leaf)) {
    throw "Godot binary not found: $Godot"
}
if (-not (Get-Command dotnet-trace -ErrorAction SilentlyContinue)) {
    throw 'dotnet-trace is required. Install it with: dotnet tool install --global dotnet-trace'
}

Push-Location $repoRoot
try {
    & dotnet build .\client\Meesles.Avalon.Client.csproj
    if ($LASTEXITCODE -ne 0) { throw 'Client build failed.' }

    $traceDir = Join-Path $repoRoot ('TestResults\quickplay\' + (Get-Date -Format 'yyyyMMdd-HHmmss-fff') + '-' + $Profile)
    New-Item -ItemType Directory -Path $traceDir -Force | Out-Null
    $traceFile = Join-Path $traceDir 'client.nettrace'
    $runtimeKeywords = if ($Profile -eq 'gc-verbose') { '0x2080019' } else { '0x18' } # Loader/JIT; GC mode adds GC, types, and sampled allocations.
    $providers = "Microsoft-DotNETCore-SampleProfiler:0x0:5,Microsoft-Windows-DotNETRuntime:${runtimeKeywords}:5"
    $engineArgs = @('--path', (Join-Path $repoRoot 'client'))
    if ($Headless) { $engineArgs += '--headless' }
    if ($QuitAfter -gt 0) { $engineArgs += @('--quit-after', "$QuitAfter") }
    Write-Host "[profile] Output: $traceDir"
    Write-Host '[profile] Close the game normally, then wait for tracing and conversion to finish.'

    & dotnet-trace collect --providers $providers --output $traceFile --format Speedscope --show-child-io -- `
        $Godot @engineArgs -- --quickplay "--gametype=$GameType" @GameArgs
    $traceExitCode = $LASTEXITCODE

    if (-not (Test-Path -LiteralPath $traceFile) -or (Get-Item -LiteralPath $traceFile).Length -eq 0) {
        throw "No trace saved (exit $traceExitCode). Output directory: $traceDir"
    }
    Write-Host "[profile] Trace saved: $traceFile"
    $speedscope = Join-Path $traceDir 'client.speedscope.json'
    if (-not (Test-Path -LiteralPath $speedscope)) {
        & dotnet-trace convert $traceFile --format Speedscope
        if ($LASTEXITCODE -ne 0) { throw "Flame graph conversion failed. Raw trace retained: $traceFile" }
    }
    if (-not (Test-Path -LiteralPath $speedscope)) {
        throw "Flame graph missing. Raw trace retained: $traceFile"
    }
    Write-Host "[profile] Flame graph saved: $speedscope"
    Write-Host '[profile] Open the flame graph at https://www.speedscope.app; use PerfView for GC/allocation events in the .nettrace.'
    if ($traceExitCode -ne 0) { throw "Game or collector exited with code $traceExitCode. Trace retained: $traceFile" }
}
finally {
    Pop-Location
}
