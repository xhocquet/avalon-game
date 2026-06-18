# Client Scope

- This directory is the Godot 4.6 Mono client. Default to a Godot/gameplay mindset, not a generic backend mindset.
- Primary entrypoints: `project.godot`, `Scenes/Singleplayer.tscn`, `Scenes/Multiplayer.tscn`, `Scripts/GameNode.cs`, `Shared/Hud.cs`, `Shared/Menu.tscn`.
- Put cross-mode client code and reusable scene assets in `Shared/`.
- Repeated scene nodes that are identical across modes should become `Shared/*.tscn` subscenes.
- Shared deterministic gameplay code lives in `Sim/`. Changes there also affect the server build.
- Godot/Klotho integration lives under `addons/klotho/`. This is the packaged Godot addon: adapter code is source, while `lib/*.dll` is the prebuilt runtime referenced by the client.
- Klotho runtime/framework source lives in `../vendor/Klotho/`. Inspect it when enhancing or debugging behavior that crosses into the prebuilt runtime; avoid editing vendored code unless the task explicitly targets Klotho itself.
- Shared deterministic sim data lives in `Sim/Data/`.

# Working Rules

- Prefer fixes that preserve Godot scene/script wiring. When changing a script used by a scene, check the related `.tscn` and node/script assumptions.
- Preserve `.uid` files and Godot path conventions.
- Do not treat `.godot/` as source of truth for manual edits unless the task explicitly requires it.
- When changing simulation behavior, inspect `Sim/` first and keep parity with server expectations.
- When changing rendering/input/UI behavior, focus on client scripts/scenes rather than server code.

# Commands

- Open editor: `just client`
- Build C# client code: `dotnet build .\Client.csproj`
