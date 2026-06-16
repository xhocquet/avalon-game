set shell := ["powershell", "-NoLogo", "-Command"]

default:
    @just --list

server:
    dotnet build .\server\Server.csproj
    dotnet run --project .\server\Server.csproj -- 7777

client:
    & "C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe" -e ".\client\project.godot"
