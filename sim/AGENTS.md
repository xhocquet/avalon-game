# Shared Simulation Scope

- `sim/` is authoritative deterministic gameplay compiled by both `client/` and `server/`. Keep client/server behavior aligned when editing here.
- Data assets live at `client/Sim/Data/` because Godot `res://` requires them inside the Godot project. The server copies those `.bytes` files at build/runtime.
- Both sides call `SimulationSetup.RegisterSystems(...)` and `SimulationSetup.InitializeWorld(...)` through their `ISimulationCallbacks` implementations.
- Godot client callbacks poll local input and send commands; server callbacks do not poll local input because Klotho injects client commands into the authoritative server simulation.

# Working Rules

- Prefer compact intent commands with stable `Unit.UnitId` references. Do not put transient ECS entity ids in command payloads.
- Movement is planar: `TransformComponent.Position.x/z` is authoritative; `y` is not gameplay.
- Avoid dynamic physics bodies for units. Use deterministic transform integration, radii, proximity queries, grids, and stable iteration order.
- When changing gameplay rules, inspect `sim/` first instead of duplicating logic in `client/` or `server/`.
- See `plan.md` at repo root for simulation architecture, Klotho id ranges, and current work status.

# Commands

- Sim tests from repo root: `just test`
