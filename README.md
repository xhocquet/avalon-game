## TODO

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
