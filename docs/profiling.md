# Client profiling

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
