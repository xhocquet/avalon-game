using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Random;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

// Oases periodically eject a resource pickup through three frame-state-driven phases (no wall
// clock, so rollback replays identically): after SpawnIntervalMs the oasis telegraphs the eject
// (OasisResourcePreparingEvent) and picks a random landing point on a ring around itself; after
// PrepareDurationMs it actually ejects (OasisResourceEjectedEvent); after FlightDurationMs the
// Pickup entity is created and OasisResourceLandedEvent fires. The events exist purely so every
// client's view layer can animate the same coordinates/timings the sim already computed.
public class OasisSpawnSystem : ISystem {
  public const int SpawnIntervalMs = 5000;
  private const int PrepareDurationMs = 800;
  private const int FlightDurationMs = 700;
  private const int DefaultAmount = 10; // TODO: source from a data asset once amounts need tuning
  private static readonly FP64 SpawnRadius = FP64.FromInt(4);
  private const ulong RandomFeatureKey = 1;

  public void Update(ref Frame frame) {
    AdvanceCooldowns(ref frame);
    AdvancePending(ref frame);
    AdvanceLanding(ref frame);
  }

  // Oases stay clear of new triggers while a spawn is already winding up or in flight — cheap
  // insurance in case PrepareDurationMs + FlightDurationMs is ever tuned close to SpawnIntervalMs.
  private static void AdvanceCooldowns(ref Frame frame) {
    var seed = GetWorldSeed(ref frame);
    var filter = frame.Filter<Oasis, TransformComponent>();
    while (filter.Next(out var entity)) {
      if (frame.Has<OasisEjectPending>(entity) || frame.Has<OasisResourceLanding>(entity))
        continue;

      ref var oasis = ref frame.Get<Oasis>(entity);
      oasis.SpawnCooldownRemainingMs -= frame.DeltaTimeMs;
      if (oasis.SpawnCooldownRemainingMs > 0)
        continue;

      oasis.SpawnCooldownRemainingMs += SpawnIntervalMs;

      ref readonly var transform = ref frame.GetReadOnly<TransformComponent>(entity);
      var target = GetRandomTargetPosition(seed, oasis.OasisId, frame.Tick, transform.Position);
      var pickupId = PickupIdGenerator.Next(ref frame);

      frame.Add(entity, new OasisEjectPending {
        PickupId = pickupId,
        Amount = DefaultAmount,
        TargetPosition = target,
        RemainingMs = PrepareDurationMs
      });

      RaisePreparing(ref frame, oasis.OasisId, pickupId, transform.Position, target);
    }
  }

  private static void AdvancePending(ref Frame frame) {
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
        TargetPosition = pending.TargetPosition,
        RemainingMs = FlightDurationMs
      });

      RaiseEjected(ref frame, oasis.OasisId, pending.PickupId, transform.Position, pending.TargetPosition);
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

      SpawnPickup(ref frame, landing.PickupId, landing.Amount, landing.TargetPosition);
      RaiseLanded(ref frame, landing.PickupId, landing.TargetPosition, landing.Amount);
      frame.Remove<OasisResourceLanding>(entity);
    }
  }

  private static void SpawnPickup(ref Frame frame, int pickupId, int amount, FPVector3 position) {
    var entity = frame.CreateEntity();
    frame.Add(entity, new TransformComponent {
      Position = position,
      Rotation = FP64.Zero,
      Scale = FPVector3.One
    });
    frame.Add(entity, new Pickup { PickupId = pickupId, Amount = amount });
  }

  // A point on a ring of radius SpawnRadius around origin, at a uniformly random angle. Derived
  // purely from (world seed, oasis id, tick) so it's identical on every rollback replay.
  private static FPVector3 GetRandomTargetPosition(ulong seed, int oasisId, int tick, FPVector3 origin) {
    var index = (ulong)(uint)oasisId << 32 | (uint)tick;
    var rng = DeterministicRandom.FromSeed(seed, RandomFeatureKey, index);
    var direction = rng.NextDirection2D();
    return origin + new FPVector3(direction.x * SpawnRadius, FP64.Zero, direction.y * SpawnRadius);
  }

  // KlothoEngine injects RandomSeedComponent before world init; headless test harnesses that
  // build an EcsSimulation directly (skipping KlothoEngine) don't, so fall back to a fixed seed.
  private static ulong GetWorldSeed(ref Frame frame) {
    return frame.TryGetSingleton<RandomSeedComponent>(out var entity)
      ? frame.GetReadOnly<RandomSeedComponent>(entity).Seed
      : 0UL;
  }

  private static void RaisePreparing(ref Frame frame, int oasisId, int pickupId, FPVector3 oasisPosition,
    FPVector3 targetPosition) {
    if (frame.EventRaiser == null) return;

    var evt = EventPool.Get<OasisResourcePreparingEvent>();
    evt.OasisId = oasisId;
    evt.PickupId = pickupId;
    evt.OasisPosition = oasisPosition;
    evt.TargetPosition = targetPosition;
    evt.PrepareDurationMs = PrepareDurationMs;
    frame.EventRaiser.RaiseEvent(evt);
  }

  private static void RaiseEjected(ref Frame frame, int oasisId, int pickupId, FPVector3 oasisPosition,
    FPVector3 targetPosition) {
    if (frame.EventRaiser == null) return;

    var evt = EventPool.Get<OasisResourceEjectedEvent>();
    evt.OasisId = oasisId;
    evt.PickupId = pickupId;
    evt.OasisPosition = oasisPosition;
    evt.TargetPosition = targetPosition;
    evt.FlightDurationMs = FlightDurationMs;
    frame.EventRaiser.RaiseEvent(evt);
  }

  private static void RaiseLanded(ref Frame frame, int pickupId, FPVector3 position, int amount) {
    if (frame.EventRaiser == null) return;

    var evt = EventPool.Get<OasisResourceLandedEvent>();
    evt.PickupId = pickupId;
    evt.Position = position;
    evt.Amount = amount;
    frame.EventRaiser.RaiseEvent(evt);
  }
}
