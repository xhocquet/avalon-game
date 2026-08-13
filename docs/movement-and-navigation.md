# Movement Commands and Navigation

## 1. Client - Before sending to server

```mermaid
flowchart TB
    Click["Right-click"]
    What{"enemy or<br/>ground?"}
    Sel{"any of your own<br/>units selected?"}
    Snap["Resolve to walkable ground<br/>(NavTargets)"]
    Marker["Show the click marker"]
    Same{"same spot as<br/>the last order?"}
    Attack["Attack order"]
    Move["Move order"]
    Drop["Ignored"]

    Click --> What
    What -->|enemy| Sel
    What -->|ground| Snap
    Sel -->|no| Drop
    Sel -->|yes| Attack
    Snap --> Marker
    Marker --> Same
    Same -->|yes| Drop
    Same -->|no| Move

    style Drop fill:#9a9a2e,color:#fff
    style Attack fill:#1a9a4e,color:#fff
    style Move fill:#1a9a4e,color:#fff
```

- Clicking unrechable areas will move you as close as possible
- `NavTargets.ResolveMoveTarget` is the sim's own resolver, so the point sent, the point
  `CommandSystem` re-resolves, and the point the click marker draws at are the same one. It backs the
  destination `MovementRulesAsset.MoveTargetEdgeClearance` off the nearest unwalkable edge — a
  destination sitting on the boundary is one the agent grinds along instead of arriving at
- Visual feedback is shown on all clicks regardless of deduplication
- `_pendingMoveCommand` / `_pendingAttackCommand` - single slots, newest click wins

---

## 2. Client: poll, stamp, send

Klotho takes one command per player per tick, so `OnPollInput` drains a priority order and returns
after each send.

```mermaid
sequenceDiagram
    autonumber
    participant E as KlothoEngine
    participant SC as SimCallbacks
    participant Q as Pending slots
    participant B as Input buffer
    participant S as Server

    E->>SC: OnPollInput at CurrentTick
    SC->>Q: drain in priority order

    alt nothing pending
        Note over SC: no command this tick
    else first match wins
        Q-->>SC: one command, the rest wait a tick
        SC->>E: ICommandSender.Send
        Note over E: cmd.Tick = CurrentTick<br/>+ InputDelayTicks<br/>+ RecommendedExtraDelay
        E->>B: AddCommandChecked

        alt Stored
            E->>S: SendClientInput
            Note over E: local predicted sim runs it<br/>at cmd.Tick, ~2 ticks later
        else DroppedDuplicate
            Note over E: send suppressed,<br/>returned to pool
        end
    end
```

Drain order - first match returns, the rest wait a tick:

1. `SelectFaction`, once per session
2. `Purchase`
3. `Upgrade`
4. `Cast`
5. `Attack`
6. `Move`

- `cmd.Tick` - `CurrentTick + InputDelayTicks + RecommendedExtraDelay`. The tick both client and server run it on
- `InputDelayTicks` - # of ticks between the click and the command running (MP: 2, SP: 1)
- duplicate `(tick, playerId)` - dropped, not queued. Both sides keep the first, so the send is suppressed rather than allowed to diverge

The stamp applies locally too - your own predicted sim doesn't run the command on the tick you
clicked. Prediction hides the server round-trip, not your own input delay. ServerDriven refuses
`InputDelayTicks < 1`, so ~1 tick is the floor.

> **Bug:** the faction branch has no `return`. On the first poll it sends `SelectFactionCommand` and
> falls through, so anything else pending that tick collides and is lost, not retried.

---

## 3. Server round-trip and reconciliation

```mermaid
sequenceDiagram
    autonumber
    participant C as Client sim<br/>(predicted)
    participant S as Server sim<br/>(authoritative)

    Note over C: right-click at tick T<br/>stamped for T+2, buffered locally
    C->>S: SendClientInput(T+2, MoveCommand)
    Note over C: predicts forward and<br/>executes the move locally at T+2

    S->>S: executes T+2 with all players' inputs
    S-->>C: VerifiedState(tick=T+3, inputs, stateHash)

    Note over C: ProcessVerifiedBatch
    C->>C: rollback to T+2
    C->>C: resim with verified inputs
    C->>C: compare state hash

    alt hashes match
        C->>C: promote to verified,<br/>resim prediction forward
    else hashes differ
        C->>S: SendFullStateRequest
        S-->>C: FullState snapshot
        Note over C: restore, discard queued batch
    end
```

- `SDInputLeadTicks` - max # of ticks a client can run ahead of server (default: 2)
- `MaxRollbackTicks` - hard ceiling on that lead. Client slows approaching it, stops at it (default: 16)
- `TickIntervalMs` - ms per tick (MP: 33, SP: 25)
- hash mismatch - client requests a `FullState` snapshot and discards the queued batch

---

## 4. Sim: command to intent

```mermaid
sequenceDiagram
    autonumber
    participant C as CommandSystem
    participant V as CommandValidation
    participant L as UnitLookup
    participant F as GroupFormation
    participant I as UnitIntent

    C->>V: Accept(command)

    alt malformed selection, or target out of bounds
        V-->>C: REJECT - logged
    else pass
        alt UnitIds empty
            C->>L: TryGetPlayerHero
            L-->>C: the player's own hero
        else UnitIds present
            C->>L: CollectOrderedUnits

            alt none controllable
                Note over C: order dropped - no log
            else 1 unit, or no MovementRulesAsset
                L-->>C: straight to the clicked point
            else 2+ units
                C->>F: Solve
                F-->>C: per-unit destinations
            end
        end

        C->>I: ClearAttackIntent
        C->>I: SetMoveTarget
        C->>I: AllowImmediateRepath
    end
```

- `CommandValidation` - runs inside the sim, so client prediction and the server reach the same verdict on the same frame
- `CollectOrderedUnits` - an empty result is the only rejection with no log line. An order on units you don't control vanishes silently
- `GroupFormation.Solve` - only for 2+ units. A single unit goes straight to the clicked point
- `AllowImmediateRepath` - see section 6

---

## 5. NavigationAgentSystem: the six phases

One system, every tick. Two steering strategies sharing one avoidance and one integration step.

```mermaid
flowchart TB
    Collect["Snap all agents to navmesh"]
    HeroRoute["A* path/unit"]
    MinionRoute["Flow field per click,<br/>no per-unit path"]
    Avoid["ORCA - separate<br/>heroes and minions"]
    Move["Move along navmesh surface"]
    Write["Write position and facing"]
    Done{"arrived?"}
    Clear["drop the move order"]
    Again["carry on next tick"]

    Collect -->|hero| HeroRoute
    Collect -->|minion| MinionRoute
    HeroRoute --> Avoid
    MinionRoute --> Avoid
    Avoid --> Move
    Move --> Write
    Write -->|arrived| Clear
    Write -->|not arrived| Again
```

Spatial grids - `SpatialHashGrid`, cleared and refilled every tick, queried by radius:
- `AvoidanceGridCellSize` - 5.0. Phase 4 builds **two** at this size, heroes and minions indexed separately so the two populations never avoid each other
- `TargetGridCellSize` - 10.0, the coarser one. Used by `TargetAcquisitionSystem` and `ProjectileSystem`, **not** by the nav pipeline
- `WaveSpawnSystem` builds a third at minion spacing, for spawn occupancy

Other:
- `*Spread` - stripes work across ticks. All three are `1` in `Assets/rules.json`, so every agent runs every tick
- `OffCorridorTicks` - # of ticks drifted off the corridor before forcing a repath
