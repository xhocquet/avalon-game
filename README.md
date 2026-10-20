## Buff/debuff status icons

1. No unified reader.
The HUD would currently need to inspect each component individually. Existing VFX do exactly that—Molt reads StatBuffs, while Strangle reads DamageOverTime.
Add a shared, read-only StatusEffects query before building the widget. It would collect these components into a consistent set of entries containing effect kind, source skill, expiry, polarity, and relevant details. The UI then consumes one list.

Issues to fix first

1. Separate identity, stacking, and timing policy
These are different decisions:

Decision Examples
What counts as the same effect? Same skill globally; same skill per caster; shared Poison status
What happens on reapplication? Refresh existing; add an independent stack; ignore
What happens to duration? Restart full duration; keep later expiry; extend
What happens at the stack limit? Refresh oldest; replace weakest; reject new stack
These belong on the effect definition, with defaults. A skill can eventually apply several effects with different policies—Strangle’s slow and DOT are already an example.

Your default for ordinary buffs is straightforward: same skill refreshes one buff unless explicitly authored otherwise. We should also record caster unit ID separately from the origin skill ID, so effects that opt into per-caster behavior can use it.

1. Define how overlapping stat percentages combine
Different skills currently calculate percentage changes against the current modified stat. That makes their amounts depend on application/refresh order.

For example:

Start with 100 armor.
Apply +20% → 120.
Apply +50% → 180.
Refresh the first buff: remove its stored 20, then grant 20% of 160 → 192.
The first skill didn’t stack another copy, but refreshing it increased the combined bonus. We should choose a consistent modifier calculation—such as percentages against base stats—before supporting more stacking.

That step should cover:

Find an existing effect by its application identity.
Refresh all its changes together.
Restart its stored lifetime.
Reject the entire application if capacity is insufficient.
Define and test overlapping percentage modifiers.
Then tackle DOT instances and capped stacking, including preserving accrued damage on refresh. This keeps the work incremental while addressing the application problems in the current roster first.

- **Assist gold.** `GoldRulesAsset.GoldPerAssist` (50) is authored but nothing reads it. Assists need a
damage-participation window per victim before a payout has anything to key off — `Health.LastDamagerUnitId` only remembers the fatal hit, so the killer is the only actor a death can currently credit.

## Design gaps

Fixed-buffer accessors are publicly unchecked. Skills.GetRank/GetSkillAssetId/GetCooldownRemainingTicks and Inventory.GetItemAssetId index fixed int buffers with no bounds check. That's documented and gated for the skill path (CommandValidation.AcceptSkillSlot), but Inventory.GetItemAssetId has no equivalent gate described anywhere, and both are reachable from the client's UI code.

## Stat buffs overloaded

- StatBuffs.MaxEntries = 6 — Desperation applies 5; Desperation + Sprint overlapping = 7, and the 7th
silently fails to apply. The struct is at the 128-byte ceiling so it can't just be bumped. Saved a memory note.

## Klotho follow-ups

- Desync diagnosis: Klotho now keeps a 60-tick hash history by default and probes peers automatically on a desync, narrowing it to the first divergent tick/component/system. Avalon already inherits this default; no code needed. It will matter the first time a real multiplayer desync occurs.

- Component-memory report: enable `ComponentMemoryPeakSampling` temporarily on the server when sizing the 1,024-entity component stores. Klotho reports peak live component counts at shutdown, which gives evidence for safe `ComponentMaxCountOverrides`. Useful before raising player/minion limits.

- System performance report: similarly, set `SystemPerfMonitoring` for a dedicated profiling run. Klotho dumps per-system execution time and allocations on shutdown. Our load-test harness already uses the same monitor, so no new integration is needed.

Things to defer:

- Dynamic navmesh rebaking, building footprints, area masks: powerful, but Avalon’s map/nav is static and the new machinery would add more moving parts than value today.
- Late join/spectators: Klotho supports them now; Avalon explicitly disables both.
- Entitlement guards and per-room match configs: wait until factions/maps are server-authorized from a matchmaker.
- `IMatchResultProvider`: could standardize a serialized result payload on the match-end event, but it overlaps with Avalon’s existing verified-frame result reader and server save path. No urgency.
