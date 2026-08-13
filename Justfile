set shell := ["powershell", "-NoLogo", "-Command"]

godot_exe := 'C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64.exe'
godot_console := 'C:\Users\meesles\Coding\Godot-4.6-mono\Godot_v4.6.3-stable_mono_win64_console.exe'
resharper_cleanup := 'C:\Users\meesles\Downloads\JetBrains.ReSharper.CommandLineTools.2026.1.4\cleanupcode.exe'
klotho_src := justfile_directory() + '\vendor\Klotho\com.xpturn.klotho\Godot~'
klotho_dll := "xpTURN.Klotho.Runtime.dll"

godmode := "false"

default:
    @just --list

# Multiplayer: Server + 2 clients
[group('play')]
play:
    & .\scripts\play.ps1 -Godmode:${{ godmode }}

# `just play` + autostart
[group('play')]
quickplay ticks="0" faction1="200" faction2="201":
    & .\scripts\quickplay.ps1 -Ticks {{ ticks }} -Faction1 {{ faction1 }} \
      -Faction2 {{ faction2 }} -Godmode:${{ godmode }}

[group('play')]
server:
    dotnet run --project .\tools\AssetGen
    dotnet build .\server\Server.csproj
    dotnet run --project .\server\Server.csproj -- 7777

[group('dev')]
godot:
    & "{{ godot_exe }}" -e ".\client\project.godot"

# Headless smoke test: server + two headless clients, self-check
[group('test')]
smoke:
    & .\scripts\smoke.ps1 -Godmode:${{ godmode }}

[group('test')]
test:
    dotnet test .\tests\Avalon.Sim.Tests\Avalon.Sim.Tests.csproj

# Load test: run N ticks (default 1000) and report per-system timings
[group('test')]
loadtest ticks="1000":
    dotnet test .\tests\Avalon.Sim.Tests\Avalon.Sim.Tests.csproj \
      --filter "DisplayName~RunLoadTest(totalTicks: {{ ticks }})" \
      -l "console;verbosity=detailed"

# Load test with dotnet-trace flame graph
[group('test')]
loadtest-profile ticks="10000":
    dotnet build .\tools\LoadTestRunner\LoadTestRunner.csproj \
      -c Release --nologo -v q
    & .\scripts\loadtest-profile.ps1 -Ticks {{ ticks }}

[group('build')]
rebuild: clean
    just sync-klotho
    just export-scene-data
    dotnet build .\server\Server.csproj

# Various godot tools to export map and other data
[group('build')]
export-scene-data:
    dotnet run --project .\tools\AssetGen
    dotnet build .\client\Meesles.Avalon.Client.csproj
    & "{{ godot_console }}" --headless --editor --path ".\client" \
      --script "res://Scripts/Editor/run_build_exports.gd"

# Sync klotho addon from vendor code, run after custom Klotho changes
[group('build')]
sync-klotho:
    dotnet build "{{ klotho_src }}\xpTURN.Klotho.Runtime.csproj" -c Debug
    Copy-Item -Force "{{ klotho_src }}\bin\Debug\net8.0\{{ klotho_dll }}" \
      ".\client\addons\klotho\lib\{{ klotho_dll }}"
    Write-Host "Klotho runtime DLL synced."

[group('build')]
clean:
    @& .\scripts\clean.ps1
    dotnet clean .\server\Server.csproj

# `just clean` + every bin/obj in the solution
[group('build')]
clean-deep:
    @& .\scripts\clean.ps1 -Deep
    dotnet clean .\server\Server.csproj

# Report .cs comment blocks longer than N lines, grouped by file
[group('lint')]
lint-comments min="3" top="0":
    & .\scripts\lint-comments.ps1 -MinLines {{ min }} -Top {{ top }}

### Deployment ##################################################################
# Config comes from .env at the repo root (copy .env.example). See docs/deployment.md.
#
# These run under pwsh 7, not the 5.1 `set shell` the rest of the file uses: the SSH helpers
# need ProcessStartInfo.ArgumentList, which .NET Framework does not have.

# Preflight the remote: SSH, sudo, systemd, layout, disk, port
[group('server admin')]
deploy-check:
    pwsh -NoProfile -File .\scripts\deploy\doctor.ps1

# One-time remote provisioning: service user, directories, systemd unit
[group('server admin')]
deploy-setup *args:
    pwsh -NoProfile -File .\scripts\deploy\provision.ps1 {{ args }}

# Build a deployable linux-x64 tarball into .tmp/dist (no remote contact)
[group('server admin')]
publish *args:
    pwsh -NoProfile -File .\scripts\deploy\publish.ps1 {{ args }}

# Export a distributable game client into .tmp/client, pointed at the .env server.
# `just client -Preset Linux`, `-Debug`, `-NoZip`, or `-ServerHost x -ServerPort n` to override.
[group('build')]
client *args:
    pwsh -NoProfile -File .\scripts\deploy\export-client.ps1 {{ args }}

# Build, upload, swap the release symlink, restart, health check
[group('server admin')]
deploy *args:
    pwsh -NoProfile -File .\scripts\deploy\deploy.ps1 {{ args }}

# Deploy the tarball already in .tmp/dist
[group('server admin')]
redeploy:
    pwsh -NoProfile -File .\scripts\deploy\deploy.ps1 -SkipBuild

[group('server admin')]
deploy-status *args:
    pwsh -NoProfile -File .\scripts\deploy\status.ps1 {{ args }}

# Swap back to a previous release. `just rollback -List` to see them.
[group('server admin')]
rollback *args:
    pwsh -NoProfile -File .\scripts\deploy\rollback.ps1 {{ args }}

[group('server admin')]
remote-start:
    pwsh -NoProfile -File .\scripts\deploy\service.ps1 -Action start

[group('server admin')]
remote-stop:
    pwsh -NoProfile -File .\scripts\deploy\service.ps1 -Action stop

[group('server admin')]
remote-restart:
    pwsh -NoProfile -File .\scripts\deploy\service.ps1 -Action restart

[group('server admin')]
remote-logs *args:
    pwsh -NoProfile -File .\scripts\deploy\service.ps1 -Action logs {{ args }}

#################################################################################

# Reformat only. The default "Full Cleanup" profile also reorders type members, which
# alphabetized NavigationAgentSystem's fields and split comment blocks off what they
# document. sim/ is formatted by the pre-commit hook too, so keep this to whitespace/layout
# the way dotnet format is — otherwise the two tools fight over every sim file.
[group('lint')]
format:
    & "{{ resharper_cleanup }}" .\client\Meesles.Avalon.Client.sln \
      --profile="Built-in: Reformat Code" --exclude="**\addons\klotho\**"
