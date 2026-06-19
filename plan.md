# Avalon Simulation Plan

Target shape: Warcraft/Dota-like top-down combat with a handful of human players and large deterministic armies. The server is authoritative. Clients send commands, predict locally, then reconcile against server-verified state.

## Current Model

- `client/Sim/**` is shared deterministic gameplay code compiled by both client and server.
- Godot nodes are view/input only. They do not own authoritative gameplay state.
- Klotho ServerDriven mode is not classic wait-for-all lockstep. The server advances fixed ticks, substitutes empty input when needed, broadcasts verified state, and clients rollback/reconcile.
- Keep network and replay data command-centric. Commands are the durable record of player intent.
- Keep sim state light. Do not network or store per-frame unit transforms as the gameplay protocol.
- Movement is planar. `TransformComponent.Position.x/z` is authoritative; `y` is not gameplay.
- Avoid dynamic physics bodies for units. Use deterministic transform integration, radii, proximity queries, grids, and stable iteration order.

## Identity And Command Rules

- Use stable `Unit.UnitId` for command references, events, selection, targeting, replay, and UI bookkeeping.
- Do not put transient ECS entity ids in command payloads.
- Validate every command in SIM:
  - `PlayerId` must own or be allowed to command the referenced unit.
  - referenced `UnitId`s must still exist at execution time.
  - stale commands should no-op deterministically, not throw.
- Prefer compact intent commands:
  - `SelectCommand { selection input shape }`
  - `MoveToCommand { target position }`
  - `AttackCommand { target UnitId }`
  - `Spawn/Buy/TrainCommand { unit type, source structure UnitId }`
  - `AbilityCommand { caster UnitId, ability id, target UnitId/position }`
- Selection is gameplay state. The command stream should send deterministic mouse/input facts, and SIM derives selected units from player ownership plus current unit positions.
- Group orders act on the player's current SIM selection. Do not send hundreds of unit ids as normal order payload.
- Commands should carry enough intent to reproduce behavior, not sampled movement state.

## Klotho Ids

- `KlothoComponent`: 100-109 used, next free 110.
- `KlothoSerializable`: 100 `MoveCommand`, 101 `GameOverEvent`, next free 102.
- `KlothoDataAsset`: 100 `PlayerStats`, 101 `WaveRules`, next free 102.

## Done

- ServerDriven client/server flow is wired.
- Shared sim bootstrap creates bases, spawn points, heroes, teams, health, and stable unit ids.
- Minion waves exist through `WaveRulesAsset` and `WaveSpawnSystem`.
- Minion view exists through `PlayerViewFactory`.
- Minions move deterministically toward center with transform-only movement.
- Player movement no longer uses `PhysicsBodyComponent`; it directly integrates `TransformComponent.Position.x/z`.
- Klotho physics is no longer registered for core gameplay movement.
- `UnitIdGenerator` provides stable sim-level unit identity.

## Next Slice: Command And Unit Identity Foundation

Goal: make commands ready for real unit orders before adding deeper combat.

1. Add shared helpers for resolving `UnitId -> entity`.
2. Add ownership/team validation helpers for command systems.
3. Add SIM-owned selection state per player.
4. Add `SelectCommand` carrying deterministic selection input:
   - click point / drag rectangle in planar world space.
   - selection mode, such as replace/add/remove if needed.
5. Add `MoveToCommand` as the first non-WASD order command. It applies to the player's current SIM selection.
6. Keep existing WASD `MoveCommand` only as a temporary debug path until click-to-move works.

Acceptance:

- A command can reference a unit by `UnitId`.
- SIM can deterministically resolve, validate, select, and no-op stale unit references.
- No command depends on Godot node paths or transient ECS entity ids.
- Given the same input command stream and unit positions, all peers derive the same selected units.

## Milestone A: Combat And Death

Goal: make minions meet, fight, die, and reduce live entity counts deterministically.

1. `UnitDiedEvent` (`KlothoSerializable(102)`): `{ UnitId, UnitTypeId, Position }`.
2. `DeathSystem`: remove entities with `Health.Current <= 0`; raise `UnitDiedEvent`.
3. `CombatSystem`: deterministic nearest-enemy acquisition using `Team`, `Unit`, `TransformComponent`, `Health`, and `Combat`.
4. Stable targeting priority:
   - hero/champion
   - minion
   - structure
5. View reacts to synced attack/death events for VFX only.

Acceptance:

- Opposing minions damage each other and die in sync.
- Death removes entities from the deterministic sim.
- High-count waves do not rely on physics bodies or Godot overlap triggers.

## Milestone B: Structures And Win Condition

Goal: first playable deterministic Footmen-Frenzy slice.

1. Bases already spawn with `Health`; make base death end the match.
2. Add or extend victory logic to emit `GameOverEvent`.
3. Add turret units as stationary combat entities.
4. Add simple structure views and team tinting.

Acceptance:

- Minions can eventually destroy a base.
- Server and clients agree on winner through synced deterministic state.

## Milestone C: Selection And Click Orders

Goal: replace direct WASD with command-based MOBA control.

1. Client raycasts mouse input into deterministic planar world coordinates.
2. Client sends `SelectCommand`; SIM derives selected units from that input plus current unit positions.
3. Client sends `MoveToCommand` / `AttackCommand` without listing every selected unit.
4. SIM validates ownership, reads the player's current selection, and applies orders.
5. View renders selection from SIM state.
6. Remove WASD `MoveCommand` from normal gameplay once this path works.

Acceptance:

- Right-click ground moves an owned unit by command.
- Right-click enemy targets by `UnitId`.
- Selection is deterministic SIM state; orders are deterministic commands.

## Milestone D: Lightweight Movement And Avoidance

Goal: scale toward hundreds or thousands of units without physics.

1. Add unit radius data where needed.
2. Add simple deterministic separation or lane spacing.
3. Add a spatial grid if proximity scans become expensive.
4. Keep all iteration order stable.
5. Use fixed-point math only.

Acceptance:

- Large minion counts remain stable and cheap.
- No dynamic physics bodies are required for normal unit movement.

## Milestone E: Navigation

Goal: upgrade straight-line movement only when the simpler model is proven.

1. Decide whether full Klotho navmesh is actually needed for the map shape.
2. If yes, export deterministic nav data and load it in shared sim setup.
3. Apply nav to hero/minion orders through SIM systems.
4. Keep command payloads as intent: target position or target `UnitId`, not path samples.

Acceptance:

- Units path around real blockers deterministically.
- Commands remain compact and replayable.

## Milestone F: HUD, Camera, And Polish

- Health bars, minimap, scoreboard, and match clock are view-only.
- Camera pan/zoom/follow stays client-only.
- VFX and audio are event-driven from synced events, not gameplay authority.

## Open Decisions

1. Exact `SelectCommand` shape: click point, drag rectangle, add/remove modes, double-click type selection.
2. Aggro model: always push lane until blocked, or divert to nearby enemies.
3. Whether formations matter for footmen-scale groups.
4. Whether navmesh is needed early, or simple planar steering carries the first playable slice.
5. When to delete WASD debug movement entirely.
