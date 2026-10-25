# Optimization findings

Investigation: 2026-10-24. Dedicated server with two rendered clients, factions
202/200, normal waves, 12 seconds idle followed by right-click movement every
three seconds. Captures include startup and use 60 match-scene seconds.

## Completed: sparse-input warning flood

Avalon sends one-shot commands and no input on quiet ticks. ServerDriven
prediction and server input collection both supply `EmptyCommand` for those
ticks. Replay and verification were logging this expected path at Warning.

The baseline contained 3,685 `CmdMissing` warnings across both clients, all for
`EmptyCommand`, with no command mismatches or determinism failures. Godot's
warning sink generated native output and C# backtraces for each warning.

In `vendor/Klotho/com.xpturn.klotho/Runtime/Core/Engine/KlothoEngine.ServerDrivenClient.cs`,
expected replay fallback and missing local entries confirmed as `EmptyCommand`
now log at Debug. Missing real commands and mismatches retain Warning severity.
Input generation, prediction, and reconciliation behavior are unchanged.

| Measurement | Before: clients 1 / 2 | After: clients 1 / 2 |
| --- | --- | --- |
| Full-run warnings, including startup/shutdown | 15,714 / 15,721 | 56 / 52 |
| Mean movement wall-clock frame interval | 41.23 / 41.17 ms | 17.05 / 16.95 ms |
| p95 movement wall-clock frame interval | 50.88 / 50.72 ms | 25.16 / 22.57 ms |
| Last cumulative clamp engagement | 32.2% / 30.8% | 5.4% / 3.9% |

Frame statistics cover match-scene seconds 20–50. Clamp counters cover different
frame counts: 1,200 before and 3,000 after. Both clients completed, passed
`CLIENT OK`, and sent 16 movement commands each. All three traces converted.

## Completed: ServerDriven extra-delay feedback

The server calculated `max(rttBased, reportedEffective)`. The client's effective
report includes the server baseline, so a pushed baseline became its own
permanent floor. Local decay could only reduce the client's reactive component.

`ServerNetworkService` now derives its baseline from smoothed RTT independently
of client reports. Reports remain diagnostic. Client-local correction and decay
remain active. P2P behavior is unchanged.

Regression coverage in [DynamicDelayTests.cs](tests/Avalon.Server.Tests/DynamicDelayTests.cs):

- Reproduced the old failure: after 628 ms RTT and an echoed report of 8 ticks,
  five 2 ms samples left the baseline at 8 instead of 3.
- Verified recovery to 3, resistance to delayed reports of 8, and escalation to
  8 when high RTT returns.
- Verified a server decrease preserves local correction until it decays.

All five server tests pass. Client/server builds and `just sync-klotho` succeed.

Live logs showed startup RTT of 661/664 ms pushing both clients to 8 extra ticks.
Within about three seconds, recommendations fell to 4 and 3 ticks at smoothed
RTT of 66 and 33 ms. At 33 ms per tick, this removes 132/165 ms of extra command
scheduling delay; perceived response time was not measured. Later RTT rises of
98–167 ms raised recommendations again alongside tick stalls and hard-limit
warnings.

Validation limits: that run ended before frame CSVs were saved because the
launcher reported a collector failure, although all three conversions completed.
A repeat failed during startup with Vulkan buffer-allocation errors. These logs
validate delay recovery, not a new frame-time improvement.

## Next actions, in priority order

1. **Measure RTT and delay recovery without CPU tracing.** Separate network RTT
   from client/server pumping stalls. Examine the four-tick downward threshold:
   it can retain smaller excess delays even after the feedback fix. Capture a
   complete repeat once Vulkan allocation failures are resolved.
2. **Reduce navigation collision candidates.** Navigation consumed roughly half
   of client simulation CPU in the post-warning-fix trace. Vendor
   `FPNavAgentSystem.ResolveCollisions` tests agent pairs in nested loops. Use a
   nearby-agent broadphase, preserve deterministic pair order, and rebuild
   candidates each collision iteration. Verify rollback determinism for any cache.
3. **Cache unchanged fog contributions.** Background `VisionRenderer.RenderRaster`
   consumed roughly 30–36% of one CPU core. Investigate per-source visibility
   masks with correct invalidation for position, range, team, lifetime, and map
   changes. Keep authoritative visibility rules in shared simulation.
4. **Wire rollback smoothing.** Error correction is enabled, but moving entities
   lack `ErrorCorrectionTargetComponent`. Verify factory/view wiring and test
   correction under induced latency.
5. **Reduce health-bar drawing overhead.** `HealthBars._Draw` consumed roughly
   8% of one CPU core. Investigate cached unit references and fewer native draw
   calls after the larger costs above.

CPU estimates use CPU sample markers in trace seconds 20–50, not inclusive wall
time. Percentages of one core are not percentages of total process CPU.
Snapshot copying was about 4–5% of one core and is a lower priority.

## Capture and evidence

Run `just multiplayer-profile` or `just multiplayer-profile 90` with port 7777
free. See [docs/profiling.md](docs/profiling.md) for artifacts and options.

Local evidence under ignored `TestResults/multiplayer/`:

- `20261024-193839`: complete baseline before warning changes.
- `20261024-201926`: complete post-warning-fix capture, analysis, and findings.
- `20261024-205425`: delay-recovery logs and findings; incomplete capture.
- `20261024-211546`: failed repeat with Vulkan allocation errors.

Vendor edits require `just sync-klotho` before the client uses them.
