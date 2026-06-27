# Client Scope

- This directory is the Godot 4.6 Mono client. Default to a Godot/gameplay mindset, not a generic backend mindset.
- Primary entrypoints: `project.godot`, `Scenes/Singleplayer.tscn`, `Scenes/Multiplayer.tscn`
- Shared deterministic sim data lives in `Sim/Data/`.
- Shared deterministic gameplay code lives in `Sim/`. Changes there also affect the server build.
- Godot/Klotho integration lives under `addons/klotho/`. This is vendor code, avoid changing it whenever possible.
- Klotho runtime/framework source lives in `../vendor/Klotho/`. Inspect it when enhancing or debugging behavior that crosses into the prebuilt runtime; avoid editing vendored code unless the task explicitly targets Klotho itself.

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
