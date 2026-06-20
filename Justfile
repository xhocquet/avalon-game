set shell := ["powershell", "-NoLogo", "-Command"]

default:
    @just --list

server:
    dotnet build .\server\Server.csproj
    dotnet run --project .\server\Server.csproj -- 7777

client:
    & "C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe" -e ".\client\project.godot"

test:
    dotnet test .\tests\Avalon.Sim.Tests\Avalon.Sim.Tests.csproj

# Headless smoke test: server + two headless clients, asserts the in-engine self-check.
smoke port="7777":
    & "{{justfile_directory()}}\scripts\smoke.ps1" -Port {{port}}

# Visual solo run: solo server (MinPlayers=1) + one windowed client to watch. Close window to stop.
play port="7777":
    & "{{justfile_directory()}}\scripts\play.ps1" -Port {{port}}

# Wipe Godot's compiled DLL cache so the next build/play starts clean.
clean:
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "{{justfile_directory()}}\client\.godot\mono\temp\bin"
    Remove-Item -Recurse -Force -ErrorAction SilentlyContinue "{{justfile_directory()}}\client\.godot\mono\temp\obj"
    Write-Host "Godot mono cache cleared."

# Build Klotho runtime DLL from vendor source (Godot flavor) and sync it into the client addon.
sync-klotho:
    dotnet build "{{justfile_directory()}}\vendor\Klotho\com.xpturn.klotho\Godot~\xpTURN.Klotho.Runtime.csproj" -c Debug
    Copy-Item -Force "{{justfile_directory()}}\vendor\Klotho\com.xpturn.klotho\Godot~\bin\Debug\net8.0\xpTURN.Klotho.Runtime.dll" "{{justfile_directory()}}\client\addons\klotho\lib\xpTURN.Klotho.Runtime.dll"
    Write-Host "Klotho runtime DLL synced."

# Clean then rebuild the client assembly (with fresh Klotho DLL).
rebuild:
    just clean
    just sync-klotho
    dotnet build "{{justfile_directory()}}\client\Meesles.Avalon.Client.csproj"
