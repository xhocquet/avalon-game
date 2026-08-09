using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Random;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public class OasisSpawnSystem : ISystem {
  private const ulong RandomFeatureKey = 1;

  public void Update(ref Frame frame) {
    var rules = frame.AssetRegistry.Get<PickupRulesAsset>();

    AdvanceCooldowns(ref frame, rules);
    AdvancePending(ref frame, rules);
    AdvanceLanding(ref frame);
  }

  // Oases stay clear of new triggers while a spawn is already winding up or in flight — cheap
  // insurance in case the prepare + flight durations are ever tuned close to the spawn interval.
  private static void AdvanceCooldowns(ref Frame frame, PickupRulesAsset rules) {
    var seed = GetWorldSeed(ref frame);
    var filter = frame.Filter<Oasis, TransformComponent>();
    while (filter.Next(out var entity)) {
      if (frame.Has<OasisEjectPending>(entity) || frame.Has<OasisResourceLanding>(entity))
        continue;

      ref var oasis = ref frame.Get<Oasis>(entity);
      oasis.SpawnCooldownRemainingMs -= frame.DeltaTimeMs;
      if (oasis.SpawnCooldownRemainingMs > 0)
        continue;

      oasis.SpawnCooldownRemainingMs += rules.OasisSpawnIntervalMs;

      if (frame.Filter<Pickup>().Count >= rules.MaxGroundPickups)
        continue;

      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      var target = GetRandomTargetPosition(seed, oasis.OasisId, frame.Tick, transform.Position,
        rules.OasisEjectRadius);
      var pickupId = IdCounter<PickupIdCounter>.Next(ref frame);
      var typeAssetId = oasis.PickupTypeAssetId;

      frame.Add(entity, new OasisEjectPending {
        PickupId = pickupId,
        Amount = GetAmount(ref frame, typeAssetId),
        TypeAssetId = typeAssetId,
        TargetPosition = target,
        RemainingMs = rules.OasisPrepareDurationMs
      });

      RaisePreparing(ref frame, oasis.OasisId, pickupId, typeAssetId, transform.Position, target,
        rules.OasisPrepareDurationMs);
    }
  }

  private static void AdvancePending(ref Frame frame, PickupRulesAsset rules) {
    var filter = frame.Filter<Oasis, OasisEjectPending, TransformComponent>();
    while (filter.Next(out var entity)) {
      ref var pending = ref frame.Get<OasisEjectPending>(entity);
      pending.RemainingMs -= frame.DeltaTimeMs;
      if (pending.RemainingMs > 0)
        continue;

      ref readonly var oasis = ref frame.GetReadOnly<Oasis>(entity);
      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);

      frame.Add(entity, new OasisResourceLanding {
        PickupId = pending.PickupId,
        Amount = pending.Amount,
        TypeAssetId = pending.TypeAssetId,
        TargetPosition = pending.TargetPosition,
        RemainingMs = rules.OasisFlightDurationMs
      });

      RaiseEjected(ref frame, oasis.OasisId, pending.PickupId, pending.TypeAssetId, transform.Position,
        pending.TargetPosition, rules.OasisFlightDurationMs);
      frame.Remove<OasisEjectPending>(entity);
    }
  }

  private static void AdvanceLanding(ref Frame frame) {
    var filter = frame.Filter<Oasis, OasisResourceLanding>();
    while (filter.Next(out var entity)) {
      ref var landing = ref frame.Get<OasisResourceLanding>(entity);
      landing.RemainingMs -= frame.DeltaTimeMs;
      if (landing.RemainingMs > 0)
        continue;

      SpawnPickup(ref frame, landing.PickupId, landing.Amount, landing.TypeAssetId, landing.TargetPosition);
      RaiseLanded(ref frame, landing.PickupId, landing.TypeAssetId, landing.TargetPosition, landing.Amount);
      frame.Remove<OasisResourceLanding>(entity);
    }
  }

  private static void SpawnPickup(ref Frame frame, int pickupId, int amount, int typeAssetId,
    FPVector3 position) {
    var entity = frame.CreateEntity();
    frame.Add(entity, TransformFactory.At(position));
    frame.Add(entity, new Pickup { PickupId = pickupId, Amount = amount, TypeAssetId = typeAssetId });
  }

  private static int GetAmount(ref Frame frame, int typeAssetId) {
    return frame.AssetRegistry.TryGet<PickupTypeAsset>(typeAssetId, out var type) ? type.Amount : 0;
  }

  // A point on a ring of radius `radius` around origin, at a uniformly random angle. Derived purely
  // from (world seed, oasis id, tick) so it's identical on every rollback replay.
  private static FPVector3 GetRandomTargetPosition(ulong seed, int oasisId, int tick, FPVector3 origin,
    FP64 radius) {
    var index = (ulong)(uint)oasisId << 32 | (uint)tick;
    var rng = DeterministicRandom.FromSeed(seed, RandomFeatureKey, index);
    var direction = rng.NextDirection2D();
    return origin + new FPVector3(direction.x * radius, FP64.Zero, direction.y * radius);
  }

  // KlothoEngine injects RandomSeedComponent before world init; headless test harnesses that
  // build an EcsSimulation directly (skipping KlothoEngine) don't, so fall back to a fixed seed.
  private static ulong GetWorldSeed(ref Frame frame) {
    return frame.TryGetSingleton<RandomSeedComponent>(out var entity)
      ? frame.GetReadOnly<RandomSeedComponent>(entity).Seed
      : 0UL;
  }

  private static void RaisePreparing(ref Frame frame, int oasisId, int pickupId, int typeAssetId,
    FPVector3 oasisPosition, FPVector3 targetPosition, int prepareDurationMs) {
    if (frame.EventRaiser == null) return;

    var evt = EventPool.Get<OasisResourcePreparingEvent>();
    evt.OasisId = oasisId;
    evt.PickupId = pickupId;
    evt.TypeAssetId = typeAssetId;
    evt.OasisPosition = oasisPosition;
    evt.TargetPosition = targetPosition;
    evt.PrepareDurationMs = prepareDurationMs;
    frame.EventRaiser.RaiseEvent(evt);
  }

  private static void RaiseEjected(ref Frame frame, int oasisId, int pickupId, int typeAssetId,
    FPVector3 oasisPosition, FPVector3 targetPosition, int flightDurationMs) {
    if (frame.EventRaiser == null) return;

    var evt = EventPool.Get<OasisResourceEjectedEvent>();
    evt.OasisId = oasisId;
    evt.PickupId = pickupId;
    evt.TypeAssetId = typeAssetId;
    evt.OasisPosition = oasisPosition;
    evt.TargetPosition = targetPosition;
    evt.FlightDurationMs = flightDurationMs;
    frame.EventRaiser.RaiseEvent(evt);
  }

  private static void RaiseLanded(ref Frame frame, int pickupId, int typeAssetId, FPVector3 position,
    int amount) {
    if (frame.EventRaiser == null) return;

    var evt = EventPool.Get<OasisResourceLandedEvent>();
    evt.PickupId = pickupId;
    evt.TypeAssetId = typeAssetId;
    evt.Position = position;
    evt.Amount = amount;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
