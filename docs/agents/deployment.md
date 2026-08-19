# Deployment & Client Export

- Remote deploy tooling is `scripts/deploy/`, driven by `just` recipes and configured from a gitignored `.env` at the repo root (`.env.example` documents the keys). Read [`docs/deployment.md`](../deployment.md) before changing any of it.
- Target shape: self-contained `linux-x64` publish → tarball over SSH → versioned `releases/<stamp>/` with a `current` symlink → systemd unit. Rollback is a symlink swap.
- `just deploy-check` (preflight), `just deploy`, `just deploy-status`, `just remote-{start,stop,restart,logs}`, `just rollback`.
- The publish is verified against the assets `Program.cs` loads at startup (`Data/*.bytes`, both config files); add to `$requiredAssets` in [`scripts/deploy/publish.ps1`](../../scripts/deploy/publish.ps1) when startup gains a new required file.
- Do not enable trimming on the server publish — Klotho's generated registration and reflection roots are invisible to the trimmer.
- `just client` exports a distributable game client pointed at the deployed server. The endpoint is baked as `client/server_endpoint.json` (gitignored, written and removed around the export); [`ServerEndpoint`](../../client/Scripts/ServerEndpoint.cs) resolves `--server=host:port` > that file > `127.0.0.1:7777`, which is what keeps a working copy on localhost.
- Export size is dominated by the PCK, and the PCK is dominated by raw Tripo/PBR source art. Two levers: `process/size_limit` in a texture's `.import` (ground data maps 512, ground albedo/normal and character albedo 1024) and decimating source `.glb` meshes. Originals of decimated meshes live in `backup/mesh-originals/`. `exclude_filter` in `export_presets.cfg` drops art that ships but nothing references.
- `just client` runs a headless `--editor` import pass that reformats some `client/Scripts/*.cs` files (spaces to tabs). `git checkout -- client/Scripts/` after exporting.
- Editor-only scripts in `client/Scripts/Editor/` must be wrapped in `#if TOOLS`. Exports compile without `TOOLS` defined, so an unguarded `EditorInterface` reference breaks every export while leaving `just play` working.
