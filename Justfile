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

test-sim:
    dotnet test .\tests\Avalon.Sim.Tests\Avalon.Sim.Tests.csproj

# Headless smoke test: server + two headless clients, asserts the in-engine self-check.
smoke port="7777":
    & "{{justfile_directory()}}\scripts\smoke.ps1" -Port {{port}}

# Visual solo run: solo server (MinPlayers=1) + one windowed client to watch. Close window to stop.
play port="7777":
    & "{{justfile_directory()}}\scripts\play.ps1" -Port {{port}}
