# Avalon Test Harness Plan

TL;DR: build a first-class `dotnet` sim test harness for Avalon, starting with deterministic state-hash tests modeled on Klotho's vendor tools, but using modern, easy-to-inspect JSON/CSV artifacts.

## Tooling

- Test project: `tests/Avalon.Sim.Tests/`
- Target framework: `net8.0`
- Test framework: `xUnit`
- Assertions: `FluentAssertions`
  - Use this for comparing complex state snapshots, collections of entities/components, hash dumps, and fixture payloads.
- JSON: `System.Text.Json`
- CSV: `CsvHelper`
- Output artifacts: `TestResults/avalon-sim/...`
- Commands:
  - `just test`
  - `just test-sim`

## Phase 1: Harness Skeleton

1. Add `tests/Avalon.Sim.Tests/Avalon.Sim.Tests.csproj`.
2. Import `../../client/addons/klotho/Klotho.Server.props`.
3. Link `../../client/Sim/**/*.cs`, matching the server and `tools/AssetGen` pattern.
4. Copy or load `client/Sim/Data/Assets.bytes`.
5. Add a small `SimHarness` helper that:
   - calls `WarmupRegistry.RunAll()`
   - builds the `DataAssetRegistry`
   - creates `EcsSimulation`
   - calls `SimulationSetup.RegisterSystems(...)`
   - initializes the world with the same shape as client/server.

## Phase 2: Vendor-Style Determinism Baseline

Start with Avalon equivalents of Klotho's determinism verification tooling:

1. `SameInputSequence_ProducesSameHashSequence`
   - Create sim A and sim B.
   - Initialize both with the same max players, config, and assets.
   - Feed identical deterministic `MoveCommand` streams.
   - Compare `GetStateHash()` every tick with FluentAssertions.

2. `HashDump_CanBeWrittenAsCsv`
   - Run 300-1000 ticks.
   - Write `tick,hash` CSV output.
   - Keep output useful for manual diffing and regression inspection.

3. `HashDump_CanBeWrittenAsJson`
   - Write run metadata plus per-tick hashes.
   - Include tick count, max players, seed, command generator name, and final hash.

## Phase 3: Avalon-Specific Sim Invariants

Add direct tests around current gameplay rules:

1. `InitializeWorld_CreatesExpectedPlayerHeroesAndBases`
   - Assert 2 heroes, 2 bases, 2 spawn points, and unique `UnitId`s.

2. `InitialWorld_HashIsStable`
   - Reinitialize repeatedly and assert identical initial hashes.

3. `MoveCommands_AffectOnlyOwningPlayer`
   - Send player 1 movement.
   - Assert player 2 does not inherit player 1 input/state.

4. `WaveSpawn_IsDeterministic`
   - Advance past the wave interval.
   - Assert minion count, team ownership, and stable hash.

5. `Respawn_IsDeterministic`
   - Force a fall/death condition if practical.
   - Advance the sim.
   - Assert spawn point outcome and hash stability.

## Phase 4: Regression Fixtures

Once the harness is useful:

1. Save blessed CSV/JSON fixtures under `tests/Avalon.Sim.Tests/Fixtures/`.
2. Add opt-in baseline updates with `AVALON_UPDATE_BASELINES=1`.
3. Normal tests compare against fixtures.
4. Update mode rewrites fixtures intentionally.
5. Keep fixtures small and human-readable at first.

## Phase 5: Cross-Host Later

After the `dotnet` harness is stable:

1. Add a Godot-hosted determinism runner modeled after `vendor/Klotho/Samples/GodotDeterminismCheck`.
2. Run the same command fixtures in Godot and `dotnet`.
3. Compare CSV outputs.
4. Use this to catch server CoreCLR vs Godot runtime divergence.

## First Slice

Implement these first:

1. `tests/Avalon.Sim.Tests` xUnit project.
2. `SimHarness` helper.
3. Same-input hash equality test.
4. Initial world entity/invariant test.
5. CSV/JSON artifact writer smoke test.

