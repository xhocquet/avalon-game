# Server Scope

- This directory is the backend server executable. Default to a backend/networking/runtime mindset, not a Godot editor mindset.
- Primary entrypoints: `Program.cs`, `AvalonServerCallbacks.cs`, `Server.csproj`, `simulationconfig.json`, `sessionconfig.json`.
- The server compiles shared deterministic gameplay from `../client/Sim/`. Treat that folder as shared code and keep client/server behavior aligned when editing it.
- The server also copies `.bytes` data assets from `../client/Sim/Data/` at build/runtime. Be aware of that dependency when changing data loading.
- The server imports Klotho from `../client/addons/klotho/Klotho.Server.props`, which references packaged prebuilt runtime DLLs. Inspect `../vendor/Klotho/` for upstream Klotho source when runtime behavior matters.

# Working Rules

- Prefer changes that keep build/run behavior explicit in `Server.csproj` and `Program.cs`.
- Focus on backend concerns here: startup flow, session/simulation config, logging, networking, and deterministic sim integration.
- Treat `../client/addons/klotho/` and `../vendor/Klotho/` as framework context by default; edit them only when the task is specifically about the Klotho layer.
- Do not edit `bin/`, `obj/`, or `Logs/` unless the task is specifically about generated output or diagnostics.
- When a task touches gameplay rules, inspect whether the real source is shared code in `../client/Sim/` instead of duplicating logic in `server/`.

# Commands

- Build: `dotnet build .\Server.csproj`
- Run from repo root: `just server`
- Run directly here: `dotnet run --project .\Server.csproj -- 7777`
