# Status Effect Query

[`StatusEffects.Collect`](../Utils/StatusEffects.cs) reads one unit from the supplied frame into a caller-owned `List<StatusEffect>`. Each call clears the list, fills it, and sorts by kind, source skill, caster, then instance id. Reuse the list's capacity; collection and sorting allocate nothing once capacity is sufficient. The list grows rather than truncating results.

```csharp
var entries = new List<StatusEffect>(32); // Retain between syncs
StatusEffects.Collect(ref frame, target, entries);
```

The query reads frame state without resolving skill assets, firing events, expiring effects, or advancing timers. Supply the predicted or verified frame appropriate to the view. Entries are value snapshots and remain independent of later frame changes and output-buffer reuse.

## Entries

- **Buff:** one entry per active `Buffs` slot. `Changes` contains only that instance's remaining changes, with authored magnitudes and individual polarities. Aggregate polarity is `Mixed` when beneficial and harmful changes coexist. Neutral changes do not mask other polarities. Classification uses the current permanent base through the shared stat rule.
- **DamageOverTime:** one entry per active DOT entity, including independent same-skill and per-caster stacks. `Dot` includes accrual per tick, pending damage, payout interval, next payout tick, tick interval in milliseconds, and damage type. `DamagePerSecond` derives the effective rate from stored accrual; it is not a fresh read of skill tuning.
- **Snare / Silence:** one entry for each active single-slot status.
- **AttackProc:** includes the armed attack's damage multiplier.
- **AttackBurst:** includes the remaining extra-attack counter and delay ticks.

Every entry provides a source skill id, target unit id, polarity, timing, and `Key`. Multi-change buffs stay grouped; different kinds and independent stacks stay separate, even if they share a source skill.

## Timing and Identity

`Timing` exposes stored start/expiry ticks, duration, remaining ticks, and remaining fraction for all supported effects. Accepted snare/silence extensions and re-armed procs/bursts record a new start tick; rejected shorter snares/silences preserve their existing timing. Caster id is `0` and rank is null where the simulation does not retain them. The reader does not infer metadata from the current skill rank or asset defaults.

Keys identify storage instances on a target: buff slot, DOT entity id including generation, or single-slot kind. Buff keys survive refresh and may be reused after removal. Keys are presentation identities, not globally unique application ids.

Inactive, cleared, spent, and expired effects are omitted before cleanup runs. Retired DOTs waiting for their final payout are also omitted. Invalid, destroyed, stale-generation, unidentified, and dead targets return an empty list. Other units' effects are never included.

Tests: [`StatusEffectsQueryTests`](../../tests/Avalon.Sim.Tests/StatusEffectsQueryTests.cs), [`StatusEffectsQueryPolicyTests`](../../tests/Avalon.Sim.Tests/StatusEffectsQueryPolicyTests.cs).
