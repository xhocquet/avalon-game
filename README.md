## TODO

- **Assist gold.** `GoldRulesAsset.GoldPerAssist` (50) is authored but nothing reads it. Assists need a
damage-participation window per victim before a payout has anything to key off — `Health.LastDamagerUnitId` only remembers the fatal hit, so the killer is the only actor a death can currently credit.

## Design gaps

Fixed-buffer accessors are publicly unchecked. Skills.GetRank/GetSkillAssetId/GetCooldownRemainingTicks and Inventory.GetItemAssetId index fixed int buffers with no bounds check. That's documented and gated for the skill path (CommandValidation.AcceptSkillSlot), but Inventory.GetItemAssetId has no equivalent gate described anywhere, and both are reachable from the client's UI code.

## Stat buffs overloaded

- StatBuffs.MaxEntries = 6 — Desperation applies 5; Desperation + Sprint overlapping = 7, and the 7th
silently fails to apply. The struct is at the 128-byte ceiling so it can't just be bumped. Saved a memory note.
