# Simulation Plan

Target: MOBA-like / Footmen Frenzy-style deterministic simulation shared by Godot client and server.

## Naming

- Use folder context instead of redundant type suffixes where practical.
- Prefer `Models/Hero.cs` with `struct Hero : IComponent`, not `HeroComponent`.
- Prefer `Models/Team.cs`, `Models/Unit.cs`, `Models/Health.cs`, etc.
- Keep suffixes only when required for clarity or C# / Klotho naming conflicts.

## Sim Organization

- `client/Sim/Models/`: deterministic ECS state.
- `client/Sim/Data/`: deterministic authored data shared by client and server.
- `client/Sim/Commands/`: client/server simulation commands.
- `client/Sim/Systems/`: deterministic update and command systems.
- `client/Sim/Assets/`: Klotho data assets for stats, waves, teams, lanes, and rules.
- `client/Sim/Prototypes/`: reusable entity compositions if/when prototype spawning becomes useful.

The server links `client/Sim/**/*.cs`, so anything here must be engine-agnostic and deterministic.

## Core Concepts

- `Unit`: stable simulation identity for any controllable/attackable gameplay unit.
- `Team`: team ownership/allegiance, separate from player ownership.
- `Hero`: player-controlled unit state.
- `Minion`: lane/wave-spawned unit state.
- `Base`: team base / lose condition state.
- `SpawnPoint`: deterministic unit spawn source.
- `Health`: shared damage/death state.
- `Combat`: attack stats, cooldowns, target state.

`OwnerComponent` remains player ownership/control. It should not be used as the primary team marker.

## Identity

- `CommandBase.PlayerId` identifies the issuing player.
- `Team.TeamId` identifies allegiance.
- `Unit.UnitId` is a stable game-level id assigned by the simulation.
- `EntityRef` is the compact ECS reference used inside current-frame command targeting.
- Commands must be validated in sim systems before mutating state.

## Initial Build Slice

1. Add core components: `Unit`, `Team`, `Health`, `Hero`, `Minion`, `Base`, `SpawnPoint`.
2. Add a deterministic unit id allocator.
3. Update `SimulationSetup` to spawn two bases and one hero per player.
4. Register early systems: unit spawn, combat, death, victory.
5. Add targeted commands after identity is stable: attack, cast ability, maybe rally/move target.

## Current Slice

- `UnitIdState` stores the next deterministic unit id as singleton sim state.
- `UnitIdGenerator` owns allocation policy and can be replaced without changing spawn call sites.
