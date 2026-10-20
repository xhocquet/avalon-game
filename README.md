## Buff/debuff status icons

1. No unified reader.
The HUD would currently need to inspect each component individually. Existing VFX do exactly that—Molt reads StatBuffs, while Strangle reads DamageOverTime.

2. Stat entries aren’t player-facing effect entries.
A skill granting armor and magic resistance occupies two StatBuffs slots. We probably want one icon with both modifiers in its tooltip, not two icons. A skill can also produce multiple effect types, so grouping everything by skill ID alone needs care when their durations differ.

3. Buff/debuff classification isn’t exposed.
The sim knows whether a stat modifier is harmful, but that logic is private inside BuffsController. A UI reader should reuse that rule rather than duplicate it.

4. Remaining time is available; original duration generally isn’t.
Numeric countdowns are straightforward. A radial “percentage remaining” needs the applied duration/start tick, or a reliable way to reconstruct it. Most effect components don’t retain rank or application time.

5. These aren’t general stacking containers.
Stat buffs refresh by skill ID + stat, not caster. Snares/silences keep the later expiry; DOT/proc applications replace their single slot. The UI can faithfully display current state, but it can’t show separate instances the sim doesn’t retain.

My recommendation
Add a shared, read-only StatusEffects query before building the widget. It would collect these components into a consistent set of entries containing effect kind, source skill, expiry, polarity, and relevant details. The UI then consumes one list.

That’s a modest consolidation step. We’re ready to start the UI work without a full gameplay-system rewrite, but directly wiring the HUD to these six components would spread the special cases into the UI. The unified reader is the useful first piece.

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
