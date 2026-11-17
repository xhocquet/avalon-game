# Client profiling

For manual selection-drag latency measurements:

```powershell
pwsh -NoProfile -File scripts/multiplayer-profile.ps1 -ManualInput -Seconds 180
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
