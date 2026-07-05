set shell := ["powershell", "-NoLogo", "-Command"]

default:
    @just --list

server:
    dotnet run --project .\tools\AssetGen
    dotnet build .\server\Server.csproj
    dotnet run --project .\server\Server.csproj -- 7777

godot:
    & "C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe" -e ".\client\project.godot"

export-scene-data:
    dotnet run --project .\tools\AssetGen
    dotnet build .\client\Meesles.Avalon.Client.csproj
    & "C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64_console.exe" --headless --editor --path ".\client" --script "res://Scripts/Editor/run_build_exports.gd"

# Unit tests
test:
    dotnet test .\tests\Avalon.Sim.Tests\Avalon.Sim.Tests.csproj

# Load test: run N ticks (default 1000) and report per-system timings
loadtest ticks="1000":
    dotnet test .\tests\Avalon.Sim.Tests\Avalon.Sim.Tests.csproj --filter "DisplayName~RunLoadTest(totalTicks: {{ticks}})" -l "console;verbosity=detailed"

# Load test with dotnet-trace flame graph (default 10000 ticks for meaningful profile)
loadtest-profile ticks="10000":
    dotnet build .\tools\LoadTestRunner\LoadTestRunner.csproj -c Release --nologo -v q
    & .\scripts\loadtest-profile.ps1 -Ticks {{ticks}}

# Headless smoke test: server + two headless clients, asserts the in-engine self-check.
smoke:
    & .\scripts\smoke.ps1

# Multiplayer: Server + 2 clients
play:
    & .\scripts\play.ps1

# `just play` + autostart (ticks: fast-forward N ticks at max speed)
quickplay ticks="0":
    & .\scripts\quickplay.ps1 -Ticks {{ticks}}

# Build Klotho runtime DLL from vendor source (Godot flavor) and sync it into the client addon.
sync-klotho:
    dotnet build "{{justfile_directory()}}\vendor\Klotho\com.xpturn.klotho\Godot~\xpTURN.Klotho.Runtime.csproj" -c Debug
    Copy-Item -Force "{{justfile_directory()}}\vendor\Klotho\com.xpturn.klotho\Godot~\bin\Debug\net8.0\xpTURN.Klotho.Runtime.dll" "{{justfile_directory()}}\client\addons\klotho\lib\xpTURN.Klotho.Runtime.dll"
    Write-Host "Klotho runtime DLL synced."

rebuild: clean
    just sync-klotho
    just export-scene-data
    dotnet build .\server\Server.csproj

clean:
    @& .\scripts\clean.ps1
    dotnet clean .\server\Server.csproj
