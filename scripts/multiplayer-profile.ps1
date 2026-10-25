param(
    [ValidateRange(30, 600)][int]$Seconds = 60,
    [string]$Faction1 = '202',
    [string]$Faction2 = '200',
    [string]$Godot = 'C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe',
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot ('TestResults\multiplayer\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$processes = @()
$collectors = @()
$providers = 'Microsoft-DotNETCore-SampleProfiler:0x0:5,Microsoft-Windows-DotNETRuntime:0x2080019:5'
$traceDuration = [TimeSpan]::FromSeconds($Seconds - 5).ToString('dd\:hh\:mm\:ss')

function Start-Logged($name, $exe, $arguments) {
    Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $repoRoot -WindowStyle Hidden `
        -RedirectStandardOutput (Join-Path $output "$name.out.log") `
        -RedirectStandardError (Join-Path $output "$name.err.log") -PassThru
}

try {
    if (-not (Get-Command dotnet-trace -ErrorAction SilentlyContinue)) { throw 'dotnet-trace is required.' }
    if (-not $SkipBuild) {
        foreach ($project in @('server/Server.csproj', 'client/Meesles.Avalon.Client.csproj')) {
            & dotnet build (Join-Path $repoRoot $project)
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
        }
    }
    if (Get-NetUDPEndpoint -LocalPort 7777 -ErrorAction SilentlyContinue) { throw 'Port 7777 is already in use.' }
    Write-Host "[profile] Output: $output"
    $server = Start-Logged 'server' 'dotnet' @((Join-Path $repoRoot 'server/bin/Debug/net8.0/Meesles.Avalon.Server.dll'), '7777')
    $processes += $server
    Start-Sleep -Seconds 3
    for ($i = 1; $i -le 2; $i++) {
        $faction = if ($i -eq 1) { $Faction1 } else { $Faction2 }
        $client = Start-Logged "client$i" $Godot @('--path', (Join-Path $repoRoot 'client'),
            '--resolution', '1000x650', '--max-fps', '60', '--script', (Join-Path $PSScriptRoot 'profile-movement.gd'),
            '--', '--quickplay', "--faction=$faction", "--profile-seconds=$Seconds",
            "--profile-output=$(Join-Path $output "client$i.frames.csv")")
        $processes += $client
        Start-Sleep -Seconds 2
    }
    foreach ($i in 0..2) {
        $name = @('server', 'client1', 'client2')[$i]
        $collector = Start-Logged "$name.collector" 'dotnet-trace' @('collect', '--process-id', $processes[$i].Id,
            '--providers', $providers, '--duration', $traceDuration,
            '--output', (Join-Path $output "$name.nettrace"), '--format', 'Speedscope')
        $collectors += $collector
    }
    $deadline = [DateTime]::UtcNow.AddSeconds($Seconds + 60)
    while (@($collectors | Where-Object { -not $_.HasExited }).Count -gt 0) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Profiling timed out.' }
        Start-Sleep -Seconds 2
    }
    foreach ($collector in $collectors) {
        if ($collector.ExitCode -ne 0) { throw "Collector $($collector.Id) failed. See $output" }
    }
    while (@($processes | Select-Object -Skip 1 | Where-Object { -not $_.HasExited }).Count -gt 0) {
        if ([DateTime]::UtcNow -gt $deadline) { throw 'Clients did not finish their movement run.' }
        Start-Sleep -Seconds 2
    }
    foreach ($i in 1..2) {
        if (-not (Test-Path (Join-Path $output "client$i.frames.csv"))) { throw "Client $i did not finish its movement run." }
        $log = Get-Content (Join-Path $output "client$i.out.log") -Raw
        if ($log -notmatch 'Send MoveCommand' -or $log -notmatch '=== CLIENT OK ===') { throw "Client $i did not verify and send movement." }
    }
    Write-Host "[profile] Complete: $output"
}
finally {
    foreach ($process in @($collectors) + @($processes)) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    }
}
