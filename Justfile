set shell := ["powershell", "-NoLogo", "-Command"]

godot_exe := 'C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe'
godot_console := 'C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64_console.exe'
resharper_cleanup := 'C:\Users\meesles\Downloads\JetBrains.ReSharper.CommandLineTools.2026.1.4\cleanupcode.exe'
klotho_src := justfile_directory() + '\vendor\Klotho\com.xpturn.klotho\Godot~'
klotho_dll := "xpTURN.Klotho.Runtime.dll"

default:
    @just --list

# Multiplayer: Server + 2 clients
play:
    & .\scripts\play.ps1

# `just play` + autostart
quickplay ticks="0" faction1="200" faction2="201":
    & .\scripts\quickplay.ps1 -Ticks {{ ticks }} -Faction1 {{ faction1 }} \
      -Faction2 {{ faction2 }}

server:
    dotnet run --project .\tools\AssetGen
    dotnet build .\server\Server.csproj
    dotnet run --project .\server\Server.csproj -- 7777

godot:
    & "{{ godot_exe }}" -e ".\client\project.godot"

# Headless smoke test: server + two headless clients, self-check
smoke:
    & .\scripts\smoke.ps1

# Unit tests
test:
    dotnet test .\tests\Avalon.Sim.Tests\Avalon.Sim.Tests.csproj

# Load test: run N ticks (default 1000) and report per-system timings
loadtest ticks="1000":
    dotnet test .\tests\Avalon.Sim.Tests\Avalon.Sim.Tests.csproj \
      --filter "DisplayName~RunLoadTest(totalTicks: {{ ticks }})" \
      -l "console;verbosity=detailed"

# Load test with dotnet-trace flame graph
loadtest-profile ticks="10000":
    dotnet build .\tools\LoadTestRunner\LoadTestRunner.csproj \
      -c Release --nologo -v q
    & .\scripts\loadtest-profile.ps1 -Ticks {{ ticks }}

rebuild: clean
    just sync-klotho
    just export-scene-data
    dotnet build .\server\Server.csproj

# Various godot tools to export map and other data
export-scene-data:
    dotnet run --project .\tools\AssetGen
    dotnet build .\client\Meesles.Avalon.Client.csproj
    & "{{ godot_console }}" --headless --editor --path ".\client" \
      --script "res://Scripts/Editor/run_build_exports.gd"

# Sync klotho addon from vendor code, run after custom Klotho changes
sync-klotho:
    dotnet build "{{ klotho_src }}\xpTURN.Klotho.Runtime.csproj" -c Debug
    Copy-Item -Force "{{ klotho_src }}\bin\Debug\net8.0\{{ klotho_dll }}" \
      ".\client\addons\klotho\lib\{{ klotho_dll }}"
    Write-Host "Klotho runtime DLL synced."

clean:
    @& .\scripts\clean.ps1
    dotnet clean .\server\Server.csproj

# Reformat only. The default "Full Cleanup" profile also reorders type members, which
# alphabetized NavigationAgentSystem's fields and split comment blocks off what they
# document. sim/ is formatted by the pre-commit hook too, so keep this to whitespace/layout
# the way dotnet format is — otherwise the two tools fight over every sim file.
format:
    & "{{ resharper_cleanup }}" .\client\Meesles.Avalon.Client.sln \
      --profile="Built-in: Reformat Code" --exclude="**\addons\klotho\**"
