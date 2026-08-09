# Deploying the server

Self-contained `linux-x64` publish, shipped over SSH into a versioned release directory, run
under systemd. No .NET runtime on the host, and rollback is a symlink swap.

## Setup

```powershell
Copy-Item .env.example .env   # then set AVALON_SSH_HOST
just deploy-check             # preflight: SSH, sudo, systemd, disk, port
just deploy-setup             # one-time: service user, directories, systemd unit
just deploy                   # build, upload, activate, restart, verify
```

`AVALON_SSH_HOST` is a `Host` alias from `~/.ssh/config` — user, port and key come from there,
so nothing about authentication is duplicated in `.env`:

```
Host avalon-prod
  HostName 203.0.113.10
  User deploy
  IdentityFile ~/.ssh/id_ed25519
```

`just deploy-check` prints what the alias resolved to, so a wrong entry shows up there rather
than as a confusing auth failure mid-deploy. Connections run with `BatchMode=yes`: a key with a
passphrase must be loaded into `ssh-agent` first, or it reads as a connection failure.

`just deploy-setup -OpenFirewall` also opens the game port on ufw/firewalld. Without it, open
`AVALON_GAME_PORT/udp` yourself — on a cloud VM that usually means the provider's security
group, not the host firewall.

## Commands

| Command | Does |
| --- | --- |
| `just publish` | Build a deployable tarball into `.tmp/dist`. No remote contact. |
| `just deploy` | Publish, upload, swap `current`, restart, health check. |
| `just redeploy` | Same, reusing the tarball already in `.tmp/dist`. |
| `just deploy-check` | Read-only preflight against the remote. |
| `just deploy-setup` | One-time provisioning. Re-runnable; rewrites the unit file. |
| `just deploy-status` | Service state, live release, port, match count, recent logs. |
| `just remote-start` / `remote-stop` / `remote-restart` | `systemctl` on the unit. |
| `just remote-logs` | Last 100 journal lines. `just remote-logs -Follow` to tail. |
| `just rollback` | Swap to the previous release. `-List` to see them, `-To <stamp>` to pick. |
| `just client` | Export a distributable client into `.tmp/client`, baked to point at the deployed server. |
| `just clean` | Godot mono cache, `.tmp/publish`, `.tmp/dist`, `dotnet clean`. |
| `just clean-deep` | Also every `bin/obj` in the solution. |

Flags pass through: `just deploy -SkipAssets`, `just publish -Configuration Debug`,
`just deploy-status -Lines 50`.

These recipes run under `pwsh` rather than the `powershell` 5.1 the rest of the justfile uses:
the SSH helpers need `ProcessStartInfo.ArgumentList`, which .NET Framework does not have. Remote
commands carry `&&`, `>`, quotes and newlines, and only argv-level passing gets them across
intact — a shell-interpolated command line silently mangles them.

## Remote layout

```
/opt/avalon/
  releases/20260808-205849-7371ad3/   unpacked build
  current -> releases/20260808-.../   what systemd runs
  logs/                               rolling logger output
  results/                            match result JSON
```

`logs/` and `results/` are symlinked into each release as `Logs/` and `Results/`, because the
rolling logger and `MatchResultSaveSystem` both write relative to `AppContext.BaseDirectory`.
Keeping them outside `releases/` means pruning never deletes match history.

## How a deploy goes

1. `AssetGen` regenerates `Assets.bytes`, then `dotnet publish -r linux-x64 --self-contained`.
2. The publish is checked for the three `.bytes` assets and two config files `Program.cs`
   requires at startup — a build missing them succeeds locally and dies on the remote.
3. `tar` streams over SSH stdin into `releases/<stamp>/`. The stamp is timestamp + short SHA,
   suffixed `-dirty` when the working tree had uncommitted edits.
4. `ln -sfn` to a temp name, then `mv -T` — a single rename, so `current` is never missing.
5. `systemctl restart`, then poll until the unit is active *and* the UDP port is bound.
6. On failure: dump the last 40 journal lines, repoint `current` at the previous release,
   restart. The bad release stays on disk for inspection.
7. Prune to `AVALON_KEEP_RELEASES`, never touching whatever `current` points at.

## Client builds

`just client` exports a distributable client that already points at the deployed server, so a
player never types an address.

```powershell
just client                              # Windows x86_64, release, zipped
just client -Preset Linux                # Linux x86_64
just client -Debug                       # console wrapper + debug asserts
just client -ServerHost 1.2.3.4 -ServerPort 7788   # override the .env target
```

Output lands in `.tmp/client/<platform>/` plus a timestamped zip beside it.

The endpoint comes from `.env`: the host is whatever `~/.ssh/config` resolves `AVALON_SSH_HOST`
to (the real IP, not the alias — an alias means nothing on a player's machine) and the port is
`AVALON_GAME_PORT`. The build and the server it talks to therefore cannot drift apart.

It is passed as `client/server_endpoint.json`, written just before the export and deleted
afterwards, even when the export fails. Resolution order at runtime:

1. `--server=host:port` on the command line
2. `res://server_endpoint.json`, present only in exported builds
3. `127.0.0.1:7777`

Step 3 is why a working copy still runs against localhost — the file is gitignored and never
exists outside an export. `ServerEndpoint` is read by `LobbyGameNode` (prefills the lobby's
host/port fields, still editable) and by `MultiplayerGameNode`'s direct-join fallback.

**Export templates.** The .NET editor needs the matching **.NET** templates — the directory name
carries a `.mono` suffix (`%APPDATA%\Godot\export_templates\4.6.3.stable.mono\`). A plain
`4.6.3.stable` set is the wrong flavour and exports will fail. When the export produces no
binary, `just client` prints the editor version, the exact directory it wanted, and what is
actually installed.

Editor-only scripts under `client/Scripts/Editor/` must be wrapped in `#if TOOLS`. Exports build
the assembly in a configuration without `TOOLS` defined, so anything touching `EditorInterface`
outside a guard breaks every export.

### Signing credentials

No Android preset exists yet; this is the wiring for when one does. Godot splits export options
across two files: anything flagged `PROPERTY_USAGE_SECRET` — all six `keystore/*` fields — plus
the script encryption key go to `client/.godot/export_credentials.cfg`, never to
`client/export_presets.cfg`. The presets file is tracked; `.godot/` is not.

Better still, keep the credentials out of the project entirely. `EditorExportPreset::get_or_env`
checks the environment *before* the preset field, so setting these in `.env` means Godot never
writes a credentials file at all:

```
AVALON_ANDROID_KEYSTORE_RELEASE_PATH=C:\keys\avalon-release.keystore
AVALON_ANDROID_KEYSTORE_RELEASE_USER=avalon
AVALON_ANDROID_KEYSTORE_RELEASE_PASSWORD=...
```

`just client` maps them to `GODOT_ANDROID_KEYSTORE_*` (`Set-AndroidKeystoreEnv` in `_env.ps1`)
and reports a count, never the values. `_DEBUG_` variants exist for the debug keystore. Store the
keystore itself outside the repo; `*.keystore`/`*.jks` are gitignored as a backstop.

Generate one with (`keytool` ships with the JDK):

```powershell
keytool -genkeypair -v -keystore C:\keys\avalon-release.keystore -alias avalon `
  -keyalg RSA -keysize 2048 -validity 10000
```

## Config

Everything lives in `.env` at the repo root (gitignored). `.env.example` documents each key.
`AVALON_SSH_HOST` is the only required one. `AVALON_SUDO` may be set empty when the deploy user
is root or already owns the unit.

Runtime tuning is separate: `server/simulationconfig.json` and `server/sessionconfig.json` ship
inside the release and are read at startup, so changing them means a redeploy and a restart.

## Notes

- **Not trimmed.** Klotho resolves commands and messages through generated registration and
  reflection; the trimmer cannot see those roots.
- **`KillSignal=SIGINT`** so the tick loop exits cleanly rather than being cut off mid-write.
- **One match at a time.** `Program.cs` pins `maxRooms = 1`. Running concurrent matches means a
  process per match with the port passed in, not a bigger box.
