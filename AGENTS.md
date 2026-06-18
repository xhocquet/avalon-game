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
- Treat `client/Sim/` as shared deterministic simulation code compiled by both client and server. Keep client/server behavior aligned when editing it.
- When behavior depends on Klotho internals, inspect `vendor/Klotho/` in addition to this repo's game code; `client/addons/klotho/lib/*.dll` is the packaged runtime actually referenced by builds.

# Shared Simulation

- `server/Server.csproj` links `client/Sim/**/*.cs` into the server build; the server does not maintain a separate simulation copy.
- Client and server both call `SimulationSetup.RegisterSystems(...)` and `SimulationSetup.InitializeWorld(...)` through their `ISimulationCallbacks` implementations.
- Godot client callbacks poll local input and send commands; server callbacks do not poll local input because Klotho injects client commands into the authoritative server simulation.
