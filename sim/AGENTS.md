# Shared Simulation Scope

- `sim/` is authoritative deterministic gameplay compiled by both `client/` and `server/`. Keep client/server behavior aligned when editing here.
- Data assets live at `client/Sim/Data/` because Godot `res://` requires them inside the Godot project. The server copies those `.bytes` files at build/runtime.
- Both sides call `SimulationSetup.RegisterSystems(...)` and `SimulationSetup.InitializeWorld(...)` through their `ISimulationCallbacks` implementations.
- Godot client callbacks poll local input and send commands; server callbacks do not poll local input because Klotho injects client commands into the authoritative server simulation.

# Navigation & Temporal Spreading

- `NavigationAgentSystem` handles all unit movement: hero A* pathfinding, minion flow-field steering, ORCA avoidance, and movement integration.
- **Temporal spreading** distributes expensive phases across frames via `NavigationTuningAsset` (row `AssetId: 112` in `client/Sim/Data/Assets.json`):
  - `HeroSteeringSpread` — A* steering update interval (default 1 = every tick)
  - `MinionSteeringSpread` — flow field steering interval (default 1)
  - `AvoidanceSpread` — ORCA collision avoidance interval (default 1)
  - Set to N to update 1/N of agents per tick. Phases are offset (0, 1, 2) so they don't spike the same frame.
- The same asset holds the steering/settle tuning (arrival radii, brake distance, blocked/stuck settle thresholds, ORCA neighbour radius). Distances are authored linearly; the system squares them per tick.
- Movement integration and transform sync run every tick regardless of spread, keeping positions smooth.
- spread=N multiplies each agent's steering refresh interval by N ticks (see `server/simulationconfig.json` for the tick rate). Small values stay within the genre's acceptable latency for AI-controlled units.

# Working Rules

- Prefer compact intent commands with stable `Unit.UnitId` references. Do not put transient ECS entity ids in command payloads.
- Movement is planar: `TransformComponent.Position.x/z`.
- NO dynamic physics. Use deterministic transform integration, radii, proximity queries, grids, and stable iteration order.
- When changing gameplay rules, inspect `sim/` first instead of duplicating logic in `client/` or `server/`.
- Klotho asset id ranges (both the AssetId and wire TypeId planes) are tracked in `sim/Assets/AssetIds.cs`; allocate from the "next free" markers there.
- Systems hold no tuning constants. Gameplay numbers live in `client/Sim/Data/Assets.json` and are read through `frame.AssetRegistry.Get<T>()`; after editing the JSON run `just` asset generation (`dotnet run --project tools/AssetGen`) to rebuild `Assets.bytes`.

# Commands

- Sim tests from repo root: `just test`
