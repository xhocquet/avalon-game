param(
    [ValidateRange(30, 600)][int]$Seconds = 60,
    [string]$Faction1 = '202',
    [string]$Faction2 = '200',
    [string]$Godot = 'C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe',
    [switch]$SkipBuild,
    [switch]$ManualInput,
    [switch]$SkipTrace,
    [switch]$RenderTimings,
    [switch]$EditorDebugger,
    [switch]$SingleRenderedClient,
    [string]$Resolution,
    [ValidateRange(1024, 65535)][int]$EditorDebugPort = 6017
)

$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path $PSScriptRoot -Parent
$output = Join-Path $repoRoot ('TestResults\multiplayer\' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
New-Item -ItemType Directory -Path $output -Force | Out-Null
$processes = @()
$collectors = @()
$providers = 'Microsoft-DotNETCore-SampleProfiler:0x0:5,Microsoft-Windows-DotNETRuntime:0x2080019:5'
$traceDuration = [TimeSpan]::FromSeconds($Seconds - 5).ToString('dd\:hh\:mm\:ss')

function Start-Logged($name, $exe, $arguments, $windowStyle = 'Hidden') {
    Start-Process -FilePath $exe -ArgumentList $arguments -WorkingDirectory $repoRoot -WindowStyle $windowStyle `
        -RedirectStandardOutput (Join-Path $output "$name.out.log") `
        -RedirectStandardError (Join-Path $output "$name.err.log") -PassThru
}

try {
    if (-not $SkipTrace -and -not (Get-Command dotnet-trace -ErrorAction SilentlyContinue)) { throw 'dotnet-trace is required.' }
    if (-not $SkipBuild) {
        foreach ($project in @('server/Server.csproj', 'client/Meesles.Avalon.Client.csproj')) {
            & dotnet build (Join-Path $repoRoot $project)
            if ($LASTEXITCODE -ne 0) { throw "Build failed: $project" }
        }
    }
    if (Get-NetUDPEndpoint -LocalPort 7777 -ErrorAction SilentlyContinue) { throw 'Port 7777 is already in use.' }
    if ($EditorDebugger) {
        if (Get-NetTCPConnection -LocalPort $EditorDebugPort -State Listen -ErrorAction SilentlyContinue) {
            throw "Editor debugger port $EditorDebugPort is already in use. Choose -EditorDebugPort."
        }
        $debugUri = "tcp://127.0.0.1:$EditorDebugPort"
        $editor = Start-Logged 'editor' $Godot @('--editor', '--path', (Join-Path $repoRoot 'client'), '--debug-server', $debugUri) 'Normal'
        Write-Host "[profile] Editor PID $($editor.Id), debugger $debugUri. Editor stays open after capture."
        $editorDeadline = [DateTime]::UtcNow.AddSeconds(90)
        while (-not (Get-NetTCPConnection -LocalPort $EditorDebugPort -State Listen -ErrorAction SilentlyContinue)) {
            if ($editor.HasExited -or [DateTime]::UtcNow -gt $editorDeadline) { throw 'Editor debug server did not start. See editor logs.' }
            Start-Sleep -Milliseconds 250
        }
        Write-Host '[profile] Client 1 connects to the editor. Open Debugger > Visual Profiler > Start for its graphs.'
    }
    Write-Host "[profile] Output: $output"
    $server = Start-Logged 'server' 'dotnet' @((Join-Path $repoRoot 'server/bin/Debug/net8.0/Meesles.Avalon.Server.dll'), '7777')
    $processes += $server
    Start-Sleep -Seconds 3
    for ($i = 1; $i -le 2; $i++) {
        $headlessPeer = $SingleRenderedClient -and $i -eq 2
        $faction = if ($i -eq 1) { $Faction1 } else { $Faction2 }
        $clientResolution = if ($Resolution) { $Resolution } elseif ($ManualInput) { '1600x900' } else { '1000x650' }
        $clientFps = if ($headlessPeer) { '30' } else { '60' }
        $clientArguments = @('--path', (Join-Path $repoRoot 'client'),
            '--resolution', $clientResolution, '--max-fps', $clientFps, '--script', (Join-Path $PSScriptRoot 'profile-movement.gd'))
        if ($headlessPeer) { $clientArguments += '--headless' }
        if ($EditorDebugger -and $i -eq 1) {
            $clientArguments += @('--remote-debug', $debugUri, '--gpu-profile')
        }
        $clientArguments += @('--', '--quickplay', "--faction=$faction", "--profile-seconds=$Seconds",
            "--profile-output=$(Join-Path $output "client$i.frames.csv")")
        $style = 'Hidden'
        if ($ManualInput -or $headlessPeer) {
            if (-not $headlessPeer) { $style = 'Normal' }
            $clientArguments += '--profile-manual-input'
        }
        if ($headlessPeer) { $clientArguments += '--profile-peer' }
        if (($ManualInput -or $RenderTimings) -and -not $headlessPeer) {
            $clientArguments += "--input-latency-output=$(Join-Path $output "client$i.latency.csv")"
        }
        if ($RenderTimings -and -not $headlessPeer) { $clientArguments += '--render-timings' }
        $client = Start-Logged "client$i" $Godot $clientArguments $style
        $processes += $client
        Start-Sleep -Seconds 2
    }
    if (-not $SkipTrace) {
        foreach ($i in 0..2) {
            $name = @('server', 'client1', 'client2')[$i]
            $collector = Start-Logged "$name.collector" 'dotnet-trace' @('collect', '--process-id', $processes[$i].Id,
                '--providers', $providers, '--duration', $traceDuration,
                '--output', (Join-Path $output "$name.nettrace"), '--format', 'Speedscope')
            $collectors += $collector
        }
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
        $headlessPeer = $SingleRenderedClient -and $i -eq 2
        if (-not (Test-Path (Join-Path $output "client$i.frames.csv"))) { throw "Client $i did not finish its movement run." }
        $log = Get-Content (Join-Path $output "client$i.out.log") -Raw
        if ($log -notmatch '=== CLIENT OK ===') { throw "Client $i did not verify." }
        if (($ManualInput -or $RenderTimings) -and -not $headlessPeer) {
            if ($log -notmatch '\[InputLatency\] Saved' -or -not (Test-Path (Join-Path $output "client$i.latency.csv"))) {
                throw "Client $i did not finish its input latency capture."
            }
        }
        if (-not $ManualInput -and -not $headlessPeer -and $log -notmatch 'Send MoveCommand') { throw "Client $i did not send movement." }
        if ($log -match "no profiler: 'scripts'") { throw "Client $i tried to enable the unavailable scripts profiler." }
        $errors = Get-Content (Join-Path $output "client$i.err.log") -Raw
        if ($errors -match "no profiler: 'scripts'") { throw "Client $i tried to enable the unavailable scripts profiler." }
        if ($RenderTimings -and -not $headlessPeer -and ($log -notmatch '\[RenderTiming\] Saved' -or -not (Test-Path (Join-Path $output "client$i.render.csv")))) {
            throw "Client $i did not finish its render timing capture."
        }
    }
    if (($ManualInput -or $RenderTimings) -and (Get-Command python -ErrorAction SilentlyContinue)) {
        & python (Join-Path $PSScriptRoot 'analyze-input-latency.py') $output
        if ($LASTEXITCODE -ne 0) { throw 'Input latency analysis failed.' }
    }
    Write-Host "[profile] Complete: $output"
}
finally {
    foreach ($process in @($collectors) + @($processes)) {
        if (-not $process.HasExited) { Stop-Process -Id $process.Id -ErrorAction SilentlyContinue }
    }
}
