param(
    [int]$Ticks = 10000
)

$traceDir = Join-Path (Resolve-Path .) "tests\Avalon.Sim.Tests\TestResults\loadtest"
New-Item -ItemType Directory -Force -Path $traceDir | Out-Null
$traceFile = Join-Path $traceDir "loadtest_profile.nettrace"

if (Test-Path $traceFile) { Remove-Item $traceFile -Force }

$runner = Start-Process -PassThru -NoNewWindow `
    dotnet -ArgumentList "run", "--project", ".\tools\LoadTestRunner\LoadTestRunner.csproj", "-c", "Release", "--no-build", "--", "$Ticks"

$targetPid = $null
for ($i = 0; $i -lt 40; $i++) {
    $candidate = Get-Process -Name "Meesles.Avalon.LoadTestRunner" -ErrorAction SilentlyContinue |
        Select-Object -First 1
    if ($candidate) {
        $targetPid = $candidate.Id
        break
    }
    Start-Sleep -Milliseconds 250
}

if (-not $targetPid) {
    Write-Host "Could not find LoadTestRunner process. Falling back to dotnet PID $($runner.Id)"
    $targetPid = $runner.Id
}

Write-Host "Tracing PID $targetPid..."

$traceJob = Start-Job -ScriptBlock {
    param($tracePid, $outFile)
    dotnet-trace collect --process-id $tracePid --output $outFile --format Speedscope 2>&1
} -ArgumentList $targetPid, $traceFile

$runner.WaitForExit()
Write-Host "Runner finished (exit $($runner.ExitCode)). Waiting for trace to flush..."

Start-Sleep -Seconds 3

if ($traceJob -and $traceJob.State -eq 'Running') {
    Stop-Job $traceJob
}
if ($traceJob) {
    Remove-Job $traceJob -Force
}

$speedscope = $traceFile -replace '\.nettrace$', '.speedscope.json'
if (Test-Path $speedscope) {
    Write-Host "`nFlame graph: $speedscope"
    Write-Host "Open at https://www.speedscope.app"
} elseif (Test-Path $traceFile) {
    dotnet-trace convert $traceFile --format Speedscope
    Write-Host "`nFlame graph: $speedscope"
    Write-Host "Open at https://www.speedscope.app"
} else {
    Write-Host "No trace file produced."
}
