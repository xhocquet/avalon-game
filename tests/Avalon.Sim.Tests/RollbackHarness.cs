using System;
using System.Collections.Generic;
using xpTURN.Klotho.Core;

namespace Meesles.Avalon.Sim.Tests;

/// <summary>
/// Drives two <see cref="SimHarness"/> instances through the shape client-side prediction
/// actually produces, which <see cref="DeterminismBaselineTests"/> structurally cannot reach.
///
/// <para>The baseline proves "same inputs from tick 0 twice => same hash sequence". Both of its
/// runs build every per-system cache from scratch in the same order, so a cache living in a plain
/// system field is populated identically in both and never surfaces as a divergence. Prediction
/// does something else: it simulates ticks T..T+n, throws that branch away, restores the frame at
/// T, and resimulates the SAME ticks. Only state inside the Frame (and
/// <c>ISnapshotParticipant</c> system state) is restored — anything a system remembers across
/// ticks in an ordinary field survives the restore, so the discarded branch leaks into the replay
/// and the client lands on a state the server never computed.</para>
///
/// <para>The <see cref="Server"/> harness only ever runs authoritative commands. The
/// <see cref="Client"/> harness runs the same ones but detours through
/// <see cref="MispredictAndRollback"/> first. Any state they disagree on afterwards is state that
/// failed to roll back.</para>
///
/// <example>
/// <code>
/// var rollback = RollbackHarness.Create();
/// rollback.Advance(300, Authoritative);
/// rollback.MispredictAndRollback(15, Mispredicted);
/// rollback.AdvanceAndCompare(90, Authoritative).Should().BeEmpty();
/// </code>
/// </example>
/// </summary>
public sealed class RollbackHarness {
  private RollbackHarness(SimHarness server, SimHarness client) {
    Server = server;
    Client = client;
  }

  /// <summary>Authoritative sim. Never sees a mispredicted tick.</summary>
  public SimHarness Server { get; }

  /// <summary>Predicting sim. Rolls back and resimulates.</summary>
  public SimHarness Client { get; }

  /// <summary>Ticks simulated so far. Both sims are always on the same tick.</summary>
  public int Tick { get; private set; }

  public static RollbackHarness Create(
      int maxPlayers = SimHarness.DefaultMaxPlayers,
      int maxEntities = SimHarness.DefaultMaxEntities,
      int maxRollbackTicks = SimHarness.DefaultMaxRollbackTicks,
      int deltaTimeMs = SimHarness.DefaultDeltaTimeMs,
      bool spawnHeroesNow = true) {
    return new RollbackHarness(
      SimHarness.CreateInitialized(maxPlayers, maxEntities, maxRollbackTicks, deltaTimeMs, spawnHeroesNow),
      SimHarness.CreateInitialized(maxPlayers, maxEntities, maxRollbackTicks, deltaTimeMs, spawnHeroesNow));
  }

  /// <summary>
  /// Advances both sims in lockstep on the same commands. <paramref name="commands"/> is invoked
  /// once per sim so command objects are never shared between them. <paramref name="beforeTick"/>
  /// runs against each sim just before it ticks — use it to apply displacement or other external
  /// mutation identically to both sides.
  /// </summary>
  public void Advance(
      int ticks,
      Func<int, ICommand[]> commands = null,
      Action<SimHarness> beforeTick = null) {
    for (int i = 0; i < ticks; i++) {
      beforeTick?.Invoke(Server);
      beforeTick?.Invoke(Client);

      Server.Tick(commands?.Invoke(Tick) ?? []);
      Client.Tick(commands?.Invoke(Tick) ?? []);
      Tick++;
    }
  }

  /// <summary>
  /// Snapshots the client, simulates <paramref name="ticks"/> ticks of input the server never
  /// confirms, then rolls the client back to the snapshot. The server is untouched, so afterwards
  /// both sims are on the same tick and — for everything that rolls back correctly — the same
  /// state. Any per-system state left holding values from the discarded branch is exactly what
  /// the following <see cref="AdvanceAndCompare"/> is looking for.
  ///
  /// <paramref name="beforeTick"/> runs against the client just before each discarded tick, after
  /// the snapshot is taken - use it to drive state the commands alone cannot reach into the branch.
  /// </summary>
  public void MispredictAndRollback(int ticks, Func<int, ICommand[]> commands,
      Action<SimHarness> beforeTick = null) {
    int resumeTick = Tick;
    Client.Simulation.SaveSnapshot();

    for (int i = 0; i < ticks; i++) {
      beforeTick?.Invoke(Client);
      Client.Tick(commands?.Invoke(resumeTick + i) ?? []);
    }

    Client.Simulation.Rollback(resumeTick);
  }

  /// <summary>
  /// Advances both sims like <see cref="Advance"/> and returns the ticks at which the client's
  /// state hash left the server's. Empty is the passing result.
  /// </summary>
  public IReadOnlyList<int> AdvanceAndCompare(
      int ticks,
      Func<int, ICommand[]> commands = null,
      Action<SimHarness> beforeTick = null) {
    var divergences = new List<int>();

    for (int i = 0; i < ticks; i++) {
      int tick = Tick;
      Advance(1, commands, beforeTick);

      if (Server.StateHash != Client.StateHash)
        divergences.Add(tick);
    }

    return divergences;
  }

  /// <summary>True while both sims agree on the full state hash.</summary>
  public bool InSync => Server.StateHash == Client.StateHash;
}
