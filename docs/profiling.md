# Client profiling

For rendering CPU/GPU timings with manual input:

```powershell
just multiplayer-profile-render
just multiplayer-profile-render 180
```

These use the existing server + two-client capture with `-RenderTimings` enabled.
GPU timestamp measurement and the additional viewport queries are opt-in; the
existing capture commands retain their previous overhead. The flag also works
with automated movement or `-SkipTrace` through `scripts/multiplayer-profile.ps1`.

Each client writes `clientN.render.csv` alongside its frame/input CSVs. It contains
per-viewport rendering CPU/GPU milliseconds, global CPU frame setup, and visible
draw-call/primitive/object counts. The main viewport and minimap are measured
separately; later SubViewports added beneath the match scene are registered too.
The CSV uses the input probe's clock and records observation frame numbers. GPU
measurements are the latest available and can lag the observation; they are not
guaranteed to describe the same frame as a wall-clock sample. Zero/unavailable
CPU or GPU measurements are omitted from report percentiles.

`responsiveness.html` includes a rendering timeline and viewport selector.
`responsiveness.json` includes per-viewport timing statistics and global CPU setup
statistics. CPU and GPU times overlap; do not add them. Global CPU setup is counted
once per observation, not once per viewport. A wall-clock draw interval remains
distinct from these rendering measurements. Headless clients cannot measure GPU work.

To open an editor and connect the first client to its debugger:

```powershell
just multiplayer-profile-editor
just multiplayer-profile-editor 180
```

This opens a new editor using debug-server port 6017, plus the same two rendered
clients. Client 1 connects with `--remote-debug` and prints rendering-pass profiles to `client1.out.log` with
`--gpu-profile`. Client 2 stays standalone. The editor remains open after the timed
clients close so its debugger results can be inspected. The extra editor/debugger
work affects timings; use a standalone render capture as the comparison.

Godot 4.6 has no CLI switch to select/open the Visual Profiler tab. In the editor,
open **Debugger > Visual Profiler > Start**. Its **Autostart** checkbox can start
subsequent connected sessions. The launcher does not pass `--profiling`: the current
build rejects its request for the unavailable `scripts` profiler. Use the Visual
Profiler rather than the standard script profiler for these rendering captures.
For a different port, invoke the script with `-EditorDebugger -EditorDebugPort <port>`
and the other desired capture flags. An occupied debugger port fails explicitly;
existing editor processes are not reused or stopped.

To measure one rendered game instance:

```powershell
just multiplayer-profile-render-single
just multiplayer-profile-editor-single
```

Both accept a duration, e.g. `just multiplayer-profile-render-single 180`. Client 1
is rendered and measured; client 2 is a hidden headless peer capped at 30 FPS. The
peer meets the server's two-player minimum without a second game renderer. It
still runs simulation/networking and consumes CPU. Its frame CSV and logs are saved,
but it produces no render/latency CSV. `--profile-peer` keeps it alive beyond the
normal headless self-check until the timed capture finishes. Existing integration
clients still quit after their self-check. The editor variant also runs the editor,
which can use GPU resources; use the render-only variant as the cleaner comparison.

For manual selection-drag latency measurements:

```powershell
just multiplayer-profile-manual
just multiplayer-profile-manual 180
```

Drag selections yourself in either client; press F8 to bookmark a moment of lag.
This mode uses visible 1600x900 windows,
injects no movement, and closes the clients after the requested match duration.
Each client also writes `clientN.latency.csv`, buffered in memory until normal
scene exit. Forced termination loses these samples. Capture stops at one million
samples. The CSV records mouse dispatch, rectangle updates, frame starts,
session-update boundaries and rendering-server draw boundaries on one monotonic
clock, with a UTC anchor for alignment to traces. Session timing starts after the
existing input-capture hook and excludes the driver's earlier transport pump.
`session_end.x` is the starting predicted tick; `tick` is the ending tick.
These are application timings: input waiting before dispatch and actual monitor
presentation are not measured. Draw completion does not imply screen presentation.

Summarize a capture with `python scripts/analyze-input-latency.py <capture-folder>`.
The launcher generates the report automatically when Python is available.
Add `-SkipTrace` to collect the same frame/input timings without EventPipe sampling
or allocation events; use this as a control for profiler overhead.

`responsiveness.html` is a standalone interactive report; `responsiveness.json`
contains the same budget statistics and rolling 10-second windows for comparisons.
Choose a client and time range, click a frame for attribution, or drag across the
timeline to zoom. F8 bookmarks select the surrounding ten seconds.

Defaults are a 60 FPS target (16.67 ms frame interval), a 33.33 ms input-feedback
warning budget, and five seconds of warmup excluded. The local feedback goal is
one frame after input dispatch; the warning threshold is two target frames at
60 FPS. These are analysis settings, not simulation scheduling limits:

```powershell
python scripts/analyze-input-latency.py <capture-folder> --target-fps 120 --feedback-budget-ms 16.67 --warmup-seconds 5
```

The report shows p95/p99, over-budget sample counts, consecutive frame overruns,
and frame attribution into session wall time, draw scope, and unattributed time.
Percentiles count frames or input samples, not time-weighted exposure. A small
frame-budget overrun can be frame pacing jitter; inspect its magnitude and the
tail rather than treating each overrun as a dropped display frame. Session
scope includes reconciliation and view callbacks fired inside the session; draw
scope is not a GPU duration. Unattributed time includes other scene work and waits.
Net predicted tick advance does not count rollback/resimulation ticks. Feedback
statistics exist only while dragging, and multiple updates may share one draw.
Use matching scenes, inputs, factions, resolution and foreground/background state
when comparing runs. Clock times are seconds since probe attachment in the match
scene. A full ten-second window is required before rolling statistics appear.

Capture a dedicated server and two rendered clients with regular movement:

```powershell
just multiplayer-profile
just multiplayer-profile 90
```

Port 7777 must be free. Each client idles for the first 12 seconds after entering
the match scene, then sends a right-click every three seconds through normal
input handling. Clients use 1000x650 windows capped at 60 FPS and quit after the
requested match-scene duration. Default factions are 202 and 200; override them
with `scripts/multiplayer-profile.ps1 -Faction1 ... -Faction2 ...`.

`TestResults/multiplayer/<timestamp>/` contains separate server/client `.nettrace`
and `.speedscope.json` dumps, stdout/stderr logs, and per-client frame CSVs with
idle/movement phases, wall-clock frame intervals, and Godot process, physics,
and draw-call monitors. Traces include managed CPU samples, GC events, and
sampled allocations. Startup is included;
use movement log timestamps to distinguish it from steady play. The script stops
only its own processes. Collection ends before client shutdown so EventPipe can
flush and collect rundown while the managed runtime is still alive.

Launch Combat Playground with managed stack sampling:

```powershell
just quickplay-profile
just quickplay-profile combat-playground --faction=201 --allcheats
```

Include GC events and sampled allocations:

```powershell
just quickplay-profile-gc combat-playground --faction=201 --allcheats
```

Both commands build the client, accept the same game type and game arguments as
`just quickplay`, and require the global `dotnet-trace` tool. Each run writes to a
separate timestamped directory under `TestResults/quickplay/`.

Close the game normally and leave the terminal open until the command finishes.
The collector waits for game exit, flushes the trace, and completes conversion;
there is no timed collector shutdown. Files:

- `client.nettrace`: managed runtime trace; use PerfView for GC/allocation analysis.
- `client.speedscope.json`: sampled stacks; open at https://www.speedscope.app.

Godot's console executable launches another process. These recipes trace the main
executable directly. Verbose loader/JIT events retain method names from startup.
These captures cover managed code; use Godot's profiler for scene/rendering work.

The converter can warn about a potentially broken trace when Godot exits. The
verified smoke captures still contain named game/framework stacks; some shutdown
stacks may remain unresolved.

For a short automated capture:

```powershell
just quickplay-profile combat-playground -Headless -QuitAfter 240 --faction=201 --allcheats
```

`QuitAfter` counts rendered frames, not simulation ticks. Omit it for interactive testing.

For a standalone simulation benchmark with per-system time and allocation totals:

```powershell
just benchmark
just benchmark 20000
```

The system monitor excludes the first 120 executions of each system from steady
totals. Omit `--system-perf` for wall-clock timing without per-system instrumentation.
This runner uses 16 ms simulation ticks and the authored navigation tuning; it
does not measure rendering, prediction or multiplayer reconciliation.

Inspect component memory reservations and live counts:

```powershell
just memory
just memory 5000
```

This uses the standalone two-player fixture and writes
`TestResults/latest/component-memory.html`, plus console tables grouped by
responsibility. Groups have reserved-byte subtotals and heap shares; groups and
their components are ordered by reserved size. HTML groups can be collapsed.
Reporting categories are defined once in `ComponentMemoryReport.GroupFor`; they
do not change component namespaces, network IDs or heap layout.
`live` is the count at the final tick; `slots` is the maximum
concurrent component count. Empty slots are headroom, not evidence that a
component can be removed. Sparse indices remain sized to `MaxEntities` even for
capped components.

`SimulationCapacity.PlayerSlots` defines the process-wide player maximum shared
by local and networked modes. Hero-only components use it through
`KlothoComponent.MaxCount`; startup rejects a larger roster. Shared components
such as health, stats and navigation retain the full entity capacity. Raising
the supported player maximum requires changing this bound and deploying matching
client/server builds. Klotho freezes its storage layout process-wide, so capacities
cannot safely follow the current connected-player count or change between modes.

Shared reservation limits live in [`SimulationCapacity`](../sim/SimulationCapacity.cs):

| Bound | Components | Invariant |
|---|---|---|
| `PlayerSlots` | `Player`, `Hero`, `PlayerFaction`, `Inventory`, `Experience`, `Respawns`, `Skills`, `Resources` | At most one of each per player; heroes survive death and respawn in place. |
| `OasisSlots` | `Oasis`, `OasisEjectPending`, `OasisResourceLanding` | At most one live fountain per match; pending/flight state lives on the fountain. |
| `TurretSlots = TurretsPerPlayer * PlayerSlots` | `Turret` | At most five turrets per player/team. |
| `CrystalSlots = CrystalsPerPlayer * PlayerSlots` | `Crystal` | At most one base per player/team. |
| `SpawnPointSlots = SpawnPointsPerPlayer * PlayerSlots` | `SpawnPoint` | At most one spawn point per player/team. |
| `PickupSlots` | `Pickup` | At most 128 ground pickups; oasis spawning respects this bound and the authored `MaxGroundPickups`. |

`Oasis` stores match-instance state, not a fountain type definition. `Pickup` is
separate: each resource landed by the oasis becomes its own pickup entity.
Maps may omit the oasis; initialization rejects more oasis markers than
`OasisSlots` before creating world entities. A storage cap of one does not make
these components Klotho singletons or change how the oasis filter finds them.
Map validation also checks structure counts per team and against the process-wide
capacity. Networked initialization can temporarily create every authored team's
structures before pruning inactive teams, so storage follows the supported player
maximum rather than the connected roster size.

After changing generator source, `just sync-klotho-generator` rebuilds and copies
the packaged analyzer used by client, server and test builds.
