# Client Scope

- This directory is the Godot 4.6 Mono client. Default to a Godot/gameplay mindset, not a generic backend mindset.
- Primary entrypoints: `project.godot`, `Scenes/Singleplayer.tscn`, `Scenes/Multiplayer.tscn`
- Shared deterministic sim data lives in `Sim/Data/` (data assets only — Godot `res://` requires them inside the client project). Gameplay assets are authored as `*.json` arrays under `Sim/Data/Assets/` (one file per topic, one per hero under `heroes/`) and merged into `Sim/Data/Assets.bytes` by `dotnet run --project tools/AssetGen`. Only the `.bytes` is loaded at runtime or packed into an export.
- Shared deterministic gameplay code lives in `sim/` at the repo root, not here. Changes there also affect the server build.
- Maps/modes are listed in `Scripts/View/GameTypeCatalog.cs`: each entry names the game scene the lobby hands off to, the world scene that scene instances as "World", and the navmesh + MapLayout exports the sim loads. Networked types join the dedicated server (Avalon only — the server loads its own map data at startup); local types host the session in-process, which is what lets a playground run its own map. Adding a map means adding a `.tscn` (with at least a `SpawnPoint` marker per team), a catalog entry, and its scene path to `Scripts/Editor/run_build_exports.gd`.
- Godot/Klotho integration lives under `addons/klotho/`. This is vendor code, avoid changing it whenever possible.
- Klotho runtime/framework source lives in `../vendor/Klotho/`. Inspect it when enhancing or debugging behavior that crosses into the prebuilt runtime; avoid editing vendored code unless the task explicitly targets Klotho itself.

# Network & Prediction (Client Side)

- **Quickplay/Multiplayer** (`LobbyGameNode`): connects to .NET server in ServerDriven mode. Server sends `SimulationConfigMessage` which overrides the client's initial config. Client runs local prediction (executes own commands immediately, reconciles on server confirmation). Error correction smooths rollback deltas.
- **Singleplayer** (`SingleplayerGameNode`): P2P mode, local host. Uses reduced delays (InputDelay=1, InterpolationDelay=1, tick=25ms) for near-instant feedback. Prediction is on by default.
- **Config override chain**: `GodotSimulationConfig` (editor `.tres`) → hardcoded in GameNode → overridden by server message at runtime.
- **View interpolation**: `EntityViewNode` uses `InterpolationDelayTicks` to trail behind sim state. `ErrorVisualState` blends position/rotation corrections when rollback causes pops.
- **Input flow**: `InputCapture` → `OnPollInput` callback → `MoveCommand`/`AttackCommand` stamped at `CurrentTick` → executed locally in predicted sim → sent to server.

# Working Rules

- Prefer fixes that preserve Godot scene/script wiring. When changing a script used by a scene, check the related `.tscn` and node/script assumptions.
- Preserve `.uid` files and Godot path conventions.
- Do not treat `.godot/` as source of truth for manual edits unless the task explicitly requires it.
- When changing simulation behavior, inspect `Sim/` first and keep parity with server expectations.
- When changing rendering/input/UI behavior, focus on client scripts/scenes rather than server code.

# Commands

- Build C# client code from `client/`: `dotnet build .\Meesles.Avalon.Client.csproj`
- Build C# client code from repo root: `dotnet build .\client\Meesles.Avalon.Client.csproj`
- Export scene data from repo root: `just export-scene-data`
