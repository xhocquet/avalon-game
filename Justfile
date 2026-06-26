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

# Headless smoke test: server + two headless clients, asserts the in-engine self-check.
smoke port="7777":
    & .\scripts\smoke.ps1 -Port {{port}}

# Multiplayer: Server + 2 clients
play port="7777":
    & .\scripts\play.ps1 -Port {{port}}

# `just play` + autostart
quickplay port="7777":
    & .\scripts\quickplay.ps1 -Port {{port}}

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
