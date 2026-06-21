# Project Structure

- `client/`: Godot 4.6 Mono/.NET game client.
- `server/`: C# game server.
- `client/` and `server/` both use Klotho for deterministic networking.
- `vendor/Klotho/`: upstream Klotho submodule source. Use it to inspect runtime/framework behavior that is consumed here through prebuilt DLLs.
- `gdd/`: generated HTML game design documentation from another repo.
- `klotho-docs/`: copied Klotho source docs for local reference only; treat as read-only.

# Agent Routing

- If working in `client/`, follow [`client/AGENTS.md`](client/AGENTS.md) for Godot/editor/runtime context.
- If working in `server/`, follow [`server/AGENTS.md`](server/AGENTS.md) for backend/build/runtime context.
- Treat `sim/` (repo root) as shared deterministic simulation code compiled by both client and server. Keep client/server behavior aligned when editing it. Data assets live at `client/Sim/Data/` (Godot `res://` requires them inside the Godot project).
- When behavior depends on Klotho internals, inspect `vendor/Klotho/` in addition to this repo's game code; `client/addons/klotho/lib/*.dll` is the packaged runtime actually referenced by builds.
- **If you edit any file under `vendor/Klotho/`**, you must rebuild the client-side DLL and copy it before the client picks up your changes. Run `just sync-klotho` (or `just rebuild`). `just play` does this automatically. The Godot-flavored build project is `vendor/Klotho/com.xpturn.klotho/Godot~/xpTURN.Klotho.Runtime.csproj`; its output goes to `client/addons/klotho/lib/xpTURN.Klotho.Runtime.dll`. Server-side vendor changes compile automatically via `server/Server.csproj` and do not need this step.

# Shared Simulation

- `server/Server.csproj` links `sim/**/*.cs` into the server build; the server does not maintain a separate simulation copy.
- Client and server both call `SimulationSetup.RegisterSystems(...)` and `SimulationSetup.InitializeWorld(...)` through their `ISimulationCallbacks` implementations.
- Godot client callbacks poll local input and send commands; server callbacks do not poll local input because Klotho injects client commands into the authoritative server simulation.
