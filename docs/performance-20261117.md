# Overnight profiling, 17 November 2026

Prepared on `perf/overnight-20261117`. Four optimizations, one commit each. Existing uncommitted work and the dirty Klotho submodule were preserved. Initial diff: `TestResults/overnight/initial-working-tree.patch`.

## Changes and evidence

| Commit | Optimization | Evidence |
|---|---|---|
| 623c4a8 | Skip friendly/already-revealed vision queries; defer source walkability until needed | Vision system 992 → 186 µs/tick in matched 10,000-tick benchmarks |
| 633cdbf | Recycle spatial-grid lists locally instead of using a global locked pool | Full simulation 9.14 → 8.96 seconds; modest gain, reduces shared-pool contention |
| 3c775dd | Skip fog worker, texture upload and filtering when captured inputs are unchanged | Stationary raster check 0.038 ms versus 47.17 ms rendering; moving 44.90 versus 47.17 ms; pixel checksums identical |
| 4fe14ee | Skip fog presentation/resources on headless clients | Both clients pass runtime self-check; headless peer remains connected throughout captures |

Full sim baseline: **17.06 → 8.96 seconds for 10,000 ticks** (1.706 → 0.896 ms/tick, about 47% lower). All 20 checkpoint hashes and entity counts match. These are deterministic harness measurements, not a claim of 47% higher rendered FPS. Fog skipping only helps when every captured source input remains unchanged; moving minion waves still require fog work.

## Serial rendered captures

Each capture uses one rendered 1600×900 client, one headless peer, automated movement, factions 200/201, and GPU timing enabled. The runner records PID/start-time ownership and closes its clients/server/collectors before starting another capture. It leaves unrelated editor processes alone. Exact manifests: `TestResults/overnight/runs.jsonl` and each capture's `owned-processes.jsonl`.

| Capture | Duration | Notes |
|---|---|---|
| ../multiplayer/20261116-234658 | 30 s | Before overnight changes |
| ../multiplayer/20261117-000208 | 60 s | All four optimizations; no CPU sampling |
| ../multiplayer/20261117-000332 | 60 s | Repeat; no CPU sampling |
| ../multiplayer/20261117-000504 | 60 s | Final CPU sampling + GPU capture |

Capture paths above resolve under `TestResults/multiplayer/` by timestamp. Each capture contains CSV timing data and a `responsiveness.html` report; the final capture additionally contains Speedscope flamegraphs/nettrace files.

## GPU findings

The two 60-second unsampled captures agree closely:

| Viewport | Average GPU ms | p95 GPU ms | Average primitives/frame |
|---|---:|---:|---:|
| Main | 6.62–6.64 | 9.77–9.81 | ~878,000 |
| Minimap | 4.03–4.04 | 5.68 | ~5,058,000 |

GPU timing is latest-available data and can lag the observation. Zero/unavailable measurements are excluded. Viewport and pass timings overlap; do not add them or independently measured percentiles.

Godot's printed GPU pass medians: opaque **3.50–3.54 ms**, shadows **2.59–2.60 ms**, depth prepass **2.06 ms**, SSIL **0.79–0.80 ms**, transparent **0.73 ms**, post-transparent compositor **0.45 ms**. Fog-filter viewport GPU work is tiny when active; its CPU rasterization is much more significant.

The minimap renders the world every frame at 244×244, with `WorldAndMinimapLayers` and an orthographic camera. It is the clearest next GPU target: inspect why so much world geometry reaches that camera, then use a dedicated simplified minimap representation or suitable mesh LOD. No quality/shadow/SSIL settings were changed overnight.

## Frame pacing and warnings

After excluding the first five seconds, the two unsampled 60-second runs have wall-frame p95 **19.82 / 21.45 ms** and p99 **25.03 / 23.81 ms**, despite a 60 FPS cap and ~16.67 ms average. Spikes remain. The shorter baseline is not a controlled frame-pacing comparison; these results do not establish an improvement in selection responsiveness. Automated movement does not generate selection-drag latency samples.

Warnings remain: cold-start prediction ticks, an early client lead hard-limit warning, missing ErrorCorrectionTargetComponent warning, render-clock clamps, and shutdown texture/RID warnings. These were present before the overnight work. They are not solved by these commits. Example repeat run ends with clamp worst hold ~280 ms and 3.6% engaged frames; this is view/simulation-clock holding, not a measurement of GPU time.

## Remaining CPU costs

The final deterministic harness averages are navigation **424 µs/tick**, vision **183 µs**, target acquisition **133 µs**, and unit spatial indexing **55 µs**. Navigation is now roughly half the measured simulation system time. Remaining work includes collision/navmesh clamping and ORCA; reducing those further requires behavior-aware evaluation. Moving fog still costs roughly **45 ms per 128×128 raster** on a worker in the diagnostic fixture, dominated by sight raycasts. Static sight-geometry acceleration is a good next CPU investigation.

Raw benchmark logs, frozen before/after binaries, raster fixture and scripts remain in `TestResults/overnight/` for local review. Capture artifacts are ignored by Git. Four supported optimizations were preferred over adding a speculative fifth change.

The final rendered-client trace, excluding its first five seconds, attributes about **30.3% of sampled main-thread time to snapshot saves**, **16.9% to navigation** (including **11.1% collision resolution**), **7.2% to sim vision**, **5.0% to target acquisition**, and **2.5% to spatial indexing**. Snapshot leaf attribution is almost entirely `System.Buffer.MemmoveInternal`; the live log reports a 3,288,484-byte ECS heap, and `Frame.CopyFrom` copies the whole heap for each snapshot. Scope percentages include native-call attribution and overlap; they are not machine CPU utilization or exact method stopwatch durations. Snapshot copy bandwidth/resimulation frequency is now a leading investigation, alongside navigation. No snapshot or rollback policy was changed.

The rendered client's fog worker attributes ~18.9 seconds of sampled time to rasterization, of which ~18.0 seconds falls under line-of-sight (~95%). The headless client's trace contains **no VisionRaster.Render stack**, confirming the fourth change eliminates that background work. It still runs authoritative-compatible simulation and snapshots.

The final trace's extra sampling overhead gives wall-frame p95 ~22.3 ms and p99 ~25.8 ms. Use the two unsampled captures for frame-pacing observations. These tests automate unit movement, not a selection drag.

## Validation

- Client and server builds pass in all final captures.
- Focused vision/spatial/rollback/determinism tests: **58 passed**.
- Full sim suite: **677 passed, 5 failed**. A source-substituted pre-optimization baseline reproduces the **same five failures** (672 passed; the five added overnight test cases are absent). Logs: `final-sim-tests.log` and `baseline-tests/results.log`.
- Existing failures: `TargetAcquisitionTests.PriorityStillOutranksDistance`, `TargetAcquisitionTests.DoesNotRetargetAnAttackerThatAlreadyHasATarget`, and `AttackCommandExecutionTests.TurretAttack_DamagesEnemyInRangeWithoutMoveTarget`, `.DamageSystem_AppliesDamageAndStartsCooldownWhenTargetIsInRange`, `.TurretAttack_DoesNotPathWhenTargetLeavesRange`.
- All four captures completed with both runtime `CLIENT OK` checks. Owned process cleanup and free server port verified between runs.
- HTML flamegraphs accompany all three final Speedscope files; these graphs show managed CPU samples after warmup and exclude native/wait-labelled samples. `final-cpu.txt` retains broader sampled-scope attribution.
