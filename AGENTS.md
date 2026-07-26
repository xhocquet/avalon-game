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
- If working in `sim/`, follow [`sim/AGENTS.md`](sim/AGENTS.md) for shared deterministic simulation context.
- Treat `sim/` (repo root) as shared deterministic simulation code compiled by both client and server. Keep client/server behavior aligned when editing it. Data assets live at `client/Sim/Data/` (Godot `res://` requires them inside the Godot project).
- When behavior depends on Klotho internals, inspect `vendor/Klotho/` in addition to this repo's game code; `client/addons/klotho/lib/*.dll` is the packaged runtime actually referenced by builds.
- The client project file is `client/Meesles.Avalon.Client.csproj`; older `client/Client.csproj` references are stale.
- **If you edit any file under `vendor/Klotho/`**, you must rebuild the client-side DLL and copy it before the client picks up your changes. Run `just sync-klotho` (or `just rebuild`). `just play` does this automatically. The Godot-flavored build project is `vendor/Klotho/com.xpturn.klotho/Godot~/xpTURN.Klotho.Runtime.csproj`; its output goes to `client/addons/klotho/lib/xpTURN.Klotho.Runtime.dll`. Server-side vendor changes compile automatically via `server/Server.csproj` and do not need this step.

# Shared Simulation

- `server/Server.csproj` links `sim/**/*.cs` into the server build; the server does not maintain a separate simulation copy.
- Client and server both call `SimulationSetup.RegisterSystems(...)` and `SimulationSetup.InitializeWorld(...)` through their `ISimulationCallbacks` implementations.
- Godot client callbacks poll local input and send commands; server callbacks do not poll local input because Klotho injects client commands into the authoritative server simulation.

# Network Architecture

- **Mode**: ServerDriven (authoritative dedicated server, clients predict ahead and reconcile).
- **Tick rate**: 66ms (15Hz) — set in `server/simulationconfig.json`.
- **Client prediction**: enabled. Client runs the sim locally, executes own commands immediately, reconciles when server confirms. This hides input latency.
- **Input delay**: 2 ticks (132ms) — buffer for server to receive inputs before execution tick. Matches `SDInputLeadTicks`.
- **Interpolation delay**: 2 ticks (132ms) — view layer trails the sim to smooth jitter.
- **Error correction**: enabled. Small position discrepancies after rollback are blended rather than snapped.
- **Max rollback**: 8 ticks (528ms) — maximum prediction lead / reconciliation depth.
- **Singleplayer**: uses P2P mode locally with reduced delays (InputDelay=1, InterpolationDelay=1) for near-instant response.

Config authority chain:
1. Server loads `server/simulationconfig.json` at startup.
2. Server sends `SimulationConfigMessage` to client after handshake.
3. Client uses received config to initialize its engine (overrides client-side defaults).
4. Singleplayer bypasses this — uses hardcoded `SimulationConfig` in `SingleplayerGameNode.cs`.

# Common Commands

- Client build: `dotnet build .\client\Meesles.Avalon.Client.csproj`
- Server build: `dotnet build .\server\Server.csproj`
- Server build if normal output is locked by a running server: `dotnet build .\server\Server.csproj -o C:\tmp\avalon-server-build`

# Testing

- `just test` — xunit sim suite ([`tests/Avalon.Sim.Tests/`](tests/Avalon.Sim.Tests)): invariants, determinism baseline, combat, death, nav, scoring, spatial grid. [`SimHarness`](tests/Avalon.Sim.Tests/SimHarness.cs) boots a full sim with no Godot dependency.
- `just smoke` — boots server + 2 headless Godot clients, asserts `=== CLIENT OK ===` at tick 120 with no sim exceptions ([`scripts/smoke.ps1`](scripts/smoke.ps1)).
- `just loadtest [ticks=1000]` — [`LoadTestHarness`](tests/Avalon.Sim.Tests/LoadTestHarness.cs) runs a headless sim, reporting per-system timings every 500 ticks.
- `just loadtest-profile [ticks=10000]` — same under `dotnet-trace`; writes a Speedscope flame graph to `TestResults/loadtest/loadtest_profile.speedscope.json`.
