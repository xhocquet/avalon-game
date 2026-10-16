# Client profiling

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
