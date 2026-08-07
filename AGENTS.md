# Project Structure

- `client/`: Godot 4.6 Mono/.NET game client.
- `server/`: C# game server.
- `client/` and `server/` both use Klotho for deterministic networking.
- `vendor/Klotho/`: upstream Klotho submodule source. Use it to inspect runtime/framework behavior that is consumed here through prebuilt DLLs.
- `klotho-docs/`: copied Klotho source docs for local reference only; treat as read-only.

# Comments
- Prefer inline comments at the end of code if it fits: `var x = 123; // var x is ...`
- Keep comments concise. Do not explain the 'why?' for code you add. Do not list consumers or other details.
- Comments should just be about non-obvious implementation details. Do not re-describe what the code is already clear about.
- De-fluff comments of LLM language and keep the dry and consistent with surrounding style

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
- **A gameplay rule is written once, in `sim/`, and the client calls that same predicate — it never re-implements or approximates it.** A rule the client copies drifts from the one the sim enforces, and the symptom is a UI that offers an action the sim then rejects with nothing but a log line. So a `*Actions` class that owns a rule exposes a read-only `Can*` predicate beside its `Try*`, both running the same evaluation: `SkillActions.CanCast`/`CanUpgrade` gate the cast hotkeys in `InputCapture` and grey the cells in `SkillBarController`; `ShopActions.IsHeroNearTeamShop` gates the buy buttons in `ActionBarController`. Keep the predicates read-only and allocation-free — the HUD polls them every sync. This is a UX and bandwidth optimization, never a security one: the sim re-checks on arrival regardless, because a command arrives from an untrusted peer.

# Network Architecture

- **Mode**: ServerDriven (authoritative dedicated server, clients predict ahead and reconcile).
- **Tuning values** (tick rate, input delay, rollback depth, interpolation delay) live in `server/simulationconfig.json` — read that file rather than trusting a copy here.
- **Client prediction**: enabled. Client runs the sim locally, executes own commands immediately, reconciles when server confirms. This hides input latency.
- **Input delay**: buffer for server to receive inputs before execution tick. Matches `SDInputLeadTicks`.
- **Interpolation delay**: view layer trails the sim to smooth jitter.
- **Error correction**: enabled. Small position discrepancies after rollback are blended rather than snapped.
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

- `just test` — xunit sim suite ([`tests/Avalon.Sim.Tests/`](tests/Avalon.Sim.Tests)): invariants, determinism baseline, rollback determinism, combat, death, nav, scoring, spatial grid. [`SimHarness`](tests/Avalon.Sim.Tests/SimHarness.cs) boots a full sim with no Godot dependency.
- **Rollback determinism**: the determinism baseline re-runs from tick 0, so it cannot catch a system cache that fails to roll back — both of its runs rebuild the cache identically. [`RollbackHarness`](tests/Avalon.Sim.Tests/RollbackHarness.cs) drives the shape prediction actually produces (server sim + client sim that mispredicts, rolls back, and resimulates) and is what to reach for when adding cross-tick state to a system. Any per-tick state a system remembers must live in frame state or an `ISnapshotParticipant`, or the discarded prediction branch leaks into the replay.
- `just smoke` — boots server + 2 headless Godot clients, asserts `=== CLIENT OK ===` at tick 120 with no sim exceptions ([`scripts/smoke.ps1`](scripts/smoke.ps1)).
- `just loadtest [ticks=1000]` — [`LoadTestHarness`](tests/Avalon.Sim.Tests/LoadTestHarness.cs) runs a headless sim, reporting per-system timings every 500 ticks.
- `just loadtest-profile [ticks=10000]` — same under `dotnet-trace`; writes a Speedscope flame graph to `TestResults/loadtest/loadtest_profile.speedscope.json`.
