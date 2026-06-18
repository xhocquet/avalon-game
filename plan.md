# Moboids → Avalon Integration Plan

Source prototype: `C:\Users\meesles\Documents\moboids` (Godot 4.5, GDScript, GD-Sync RPC networking, node-based gameplay).
Target: this repo (Godot 4.6 Mono/.NET client + C# server, **Klotho deterministic ECS** shared by client and server).

## Locked decisions

- **Movement model:** moboids 100% — full MOBA **deterministic click-to-move** (navmesh + commands). WASD direct-drive is transitional and gets removed once nav is in.
- **Lanes:** single lane for now; minions/units target the **center of the map**.
- **Team size:** keep everything **generic** so larger teams are possible later, but ship 1v1-sized milestones now. Bigger teams are a later problem.
- **Priority:** **minions first.** The immediate goal is to watch many networked minion entities behave under the deterministic Klotho model before building polish around them.

## The one rule that shapes everything

Moboids gameplay is **non-deterministic node logic** (`move_and_slide`, `NavigationAgent3D`, `Area3D` overlap triggers, per-node signals, GD-Sync RPCs). None of it ports directly.

Per `AGENTS.md` + `klotho-docs/`, authoritative gameplay lives in `client/Sim/**` (linked into `server/Server.csproj`) and must be **deterministic**: fixed-point (`FP64`), Klotho `PhysicsSystem` / `FPNavMesh`, ECS components + systems, commands injected via `OnPollInput`. The Godot scene tree is a **view** driven by `EntityViewNode` / `EntityViewFactory`.

Each feature splits into:

- **SIM** (`client/Sim/`): deterministic components, systems, commands, data assets. Runs identically on client + server.
- **VIEW** (`client/Shared/`, scenes): rendering, VFX, HUD, input, selection. Client-only, non-deterministic.

*If it changes who wins, it's SIM; if it only changes what you see, it's VIEW.*

### ID bookkeeping (next free Klotho IDs)

- `KlothoComponent`: 100–109 used → **next free 110**.
- `KlothoSerializable` (commands/events): 100 (`MoveCommand`), 101 (`GameOverEvent`) → **next free 102**.
- `KlothoDataAsset`: 100 (`PlayerStats`) → **next free 101**.

---

## What we discard outright

- **GD-Sync** (`addons/GD-Sync`, all RPC/sync-node usage) — fully replaced by Klotho.
- **`GameState` autoload as authoritative state** — match lifecycle/scores/timer/win belong in SIM (`ScoreSystem`, `GameOverEvent`, future `VictorySystem`) + Klotho session phase. Salvage only view-only `focus`/`selection` and HUD state labels.
- **`PlayerSettings.gd`** — empty stub.
- **Node-physics movement + `NavigationAgent3D`** — replaced by Klotho `PhysicsSystem` + `FPNavMesh`/`NavAgentComponent`.
- **`Area3D` overlap targeting** — replaced by deterministic proximity queries in sim systems.
- **Godmode free-fly camera** — defer indefinitely (debug toy).
- **`Main.gd` bootstrap reflection / runtime group rules** — avalon already bootstraps via `GameNode`/`MultiplayerGameNode`.
- **WASD direct-drive** (`MoveCommand` H/V path) — removed once click-to-move lands (Milestone D).

---

## Milestones (bite-size, minions-first)

Each chunk ≈ one focused checkpoint. ✅ = already exists in avalon and just needs extending.

### Milestone M — Minions on the wire (THE PRIORITY)

Goal: spawn lots of minions and watch them sync deterministically. Deliberately avoids the navmesh dependency at first so we get entities on screen fast.

- **M1 — Wave spawning (SIM).**
  Add `WaveSpawnSystem : ISystem` driven by `frame.Tick`. Spawn interval + `MinionsPerWave` from a new `WaveRulesAsset` (`KlothoDataAsset(101)`) — easy to crank up for stress testing. For each `SpawnPoint`, every N ticks create minion entities: `Unit{UnitTypeId=Minion}`, `Team`, `Health`, `Minion{LaneId=0,WaveId}`, `TransformComponent`, `PhysicsBodyComponent`. No movement yet.
  *Discard:* `Spawner.gd` `Timer`, stubbed `spawn_*` methods.

- **M2 — Minion VIEW + count readout.**
  Extend `PlayerViewFactory` to render `UnitTypeId=Minion` (start with a cheap box mesh; real mesh comes in H). Add a live entity/minion count to the HUD so we can quantify what the networking model handles. **This is the actual stress-test deliverable.**

- **M3 — Simple deterministic movement toward center (SIM).**
  Add `MinionMoveSystem`: steer each minion straight toward map center (`FPVector3.Zero`) by integrating `TransformComponent.Position` directly (FP64, no navmesh, **no physics body** — see capacity note below). Lets us watch hundreds of minions move + converge under rollback before investing in nav.
  *Later replaced by nav in Milestone D.*

> **Capacity constraint (discovered in M2):** the prebuilt Klotho `PhysicsSystem`/`FPPhysicsWorld` have unclamped fixed buffers — body array sized by the ctor arg (now `MaxEntities`), but contact-snapshot buffers hard-fixed at 256 (every grounded body = 1 static contact). Hundreds of physics-bodied units crash with `Index was outside the bounds of the array` in `PhysicsSystem.Update`. **Therefore minions are transform-only entities (no `PhysicsBodyComponent`)** — movement integrates the transform, combat uses deterministic proximity queries; only heroes use physics. `MaxEntities` is 1024 (`server/simulationconfig.json`, propagated to clients) and `WaveRulesAsset.MaxConcurrentMinions` (800) keeps minions under it.

**Acceptance:** waves spawn on a tunable cadence; N minions per team render and march to center; headless client+server stay in sync at high counts; HUD shows the count.

### Milestone A — Combat & death (SIM)

Makes the existing unused `Combat`/`Health` components real; gives minions something to do when they meet.

- **A1 — DeathSystem + `UnitDiedEvent`.** Scan `Health`; on `Current<=0` raise a `KlothoSerializable(102)` Synced `UnitDiedEvent{UnitId,Position,UnitTypeId}` and destroy the entity (players fall-respawn instead). VIEW plays death VFX off the event.
- **A2 — CombatSystem (auto-attack).** On `Unit,Team,Transform,Combat`: reacquire nearest enemy-team `Unit` within `AttackRange` (deterministic FP64 filter, no Area3D), tick `CooldownRemainingTicks`, on ready+in-range subtract `AttackDamage` from target `Health`, raise `AttackEvent` for VIEW FX. One system serves minions, heroes, and turrets — they differ only by stats/priority.
- **A3 — Targeting + team helpers.** Static `Targeting` (priority: champion > minion > structure) + `TeamColors` (port `Spawner._static_team_color`) shared by SIM-adjacent logic and HUD.

**Acceptance:** opposing minions meeting at center trade damage and die deterministically; counts drop correctly on every peer.

### Milestone B — Structures & win condition (SIM + VIEW)

- **B1 — Bases lose.** Bases already spawn with `Health`. Add `VictorySystem` (or extend `ScoreSystem`): `Base.Health<=0` → `GameOverEvent` for the surviving team (`Reason="nexus"`). (moboids `Crystal.core_destroyed`, deterministic.)
- **B2 — Turrets.** Spawn stationary `Combat` units per team near the lane; A2 drives them. Tiering deferred.
- **B3 — Structure VIEW.** Map `UnitTypeId`→turret/crystal scenes; team-tint via `BaseView`-style `OnActivate`.

### Milestone D — Navigation & click-to-move (SIM + VIEW + tooling)

Implements the "moboids 100%" movement decision; upgrades M3's straight-line steering.

- **D1 — Bake navmesh.** Per `klotho-docs/Navigation.md` + `NavMeshVisualizer.Godot.md`: `NavigationRegion3D` in the map scene → Godot Klotho exporter → `client/Sim/Data/Nav.bytes`; load in `RegisterSystems` and register `FPNavAgentSystem`.
- **D2 — Minions + heroes on NavAgent.** Give minions `NavAgentComponent` (destination = center, later enemy base); replace M3 steering. Add `MoveToCommand{FPVector3 Target}` + `AttackCommand{int TargetUnitId}`; new `HeroOrderSystem` sets `NavAgentComponent.Destination` / `Combat.Target`. **Remove WASD `MoveCommand`.** VIEW raycasts mouse→ground/unit and sends the command via `OnPollInput`.

### Milestone E — Selection & focus (VIEW only)

- **E1 — Selection + ring overlay.** Port `FocusRingOverlay.gd` + selection half of `GameUI.gd` (click-select, box-select via screen→ground quad) to C#. Selection = client list of `EntityViewNode`→`UnitId`.
- **E2 — Pointer→command.** Selected owned units + right-click → `MoveToCommand`/`AttackCommand`; ownership validated in SIM via `OwnerComponent`.

### Milestone F — Camera polish (VIEW only)

Extend existing `CameraController`: **scroll-wheel zoom** (`_zoom_t` FOV/height lerp) + **WASD/edge pan with follow-resume**, ported from `GameCamera.gd`. Drop godmode.

### Milestone G — HUD & overlays (VIEW only)

- **G1** world-space health bars (`HealthBars.gd` → C#, `TeamColors`).
- **G2** minimap blips (`Minimap.gd` → C#).
- **G3** scoreboard/Tab panel + match clock (extend `Hud.cs` `SyncFromFrame`).
- **G4** VFX: port `ExplosionFX.tscn` + shot-line; spawn from VIEW on `UnitDiedEvent`/`AttackEvent`.

### Milestone H — Assets & polish (VIEW only)

Import moboids assets (regenerate `.import` on first 4.6 open).

- **H1** unit meshes: `Player/Minion/Turret.glb`, `CrystalC.tscn` → view scenes (replace placeholder boxes).
- **H2** environment: `FirTree*`, `Rock*`, `Platform`, materials. Decorative = VIEW-only; if they should block, bake into navmesh + add Klotho static colliders.
- **H3** UI textures: `bars/border/topbar/player_avatar.png` → HUD theming.

---

## Execution order

1. **M1 → M2 → M3** — minions on the wire + stress test (priority).
2. **A1 → A2 → A3** — combat/death so minions fight at center.
3. **B1 → B2 → B3** — structures + win condition → first real match.
4. **D1 → D2** — navmesh + click-to-move (replaces M3 + WASD).
5. **E**, then **F / G / H** polish in parallel once gameplay is stable.

Milestone M alone answers the networking question. M+A+B is a playable deterministic Footmen-Frenzy slice; D–H make it feel like the moboids prototype.

## Still-open (resolve before D/E)

1. Aggro behavior: do minions divert to nearby enemies, or always push to center until blocked? (Affects A2/D2.)
2. Turret count/placement on the single center lane (B2).
3. When to formally drop WASD (D2) vs. keep it as a debug fallback.
