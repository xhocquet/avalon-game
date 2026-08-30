using System.Collections.Generic;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Factories;
using Meesles.Avalon.Sim.Heroes;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim;

// The rules behind DebugCommand: playground operations a player runs on its own hero and on the
// arena around it. Scoped per player like Cheats, and equally ungated - a shipped server should not
// accept these either.
//
// Everything here mutates the frame through the same helpers the real systems use, so a predicting
// client and the server reach the same state on the same tick and rollback carries it.
public static class DebugActions {
  // Cluster spawned per SpawnMinions, laid out in a ring around the target point.
  private const int MinionsPerSpawn = 5;

  // Wave id for debug-spawned minions. Negative so nothing that groups by wave confuses them with a
  // wave WaveSpawnSystem produced.
  private const int DebugWaveId = -1;

  private static readonly List<EntityRef> Scratch = [];

  public static bool Execute(ref Frame frame, int playerId, DebugAction action, int param, int factionId,
    FPVector3 target) {
    switch (action) {
      case DebugAction.SwitchFaction: return SwitchFaction(ref frame, playerId, param);
      case DebugAction.AddGold: return AddGold(ref frame, playerId, param);
      case DebugAction.AddExperience: return AddExperience(ref frame, playerId, param);
      case DebugAction.AddSkillPoints: return AddSkillPoints(ref frame, playerId, param);
      case DebugAction.MaxSkills: return MaxSkills(ref frame, playerId);
      case DebugAction.RefreshCooldowns: return RefreshCooldowns(ref frame, playerId);
      case DebugAction.HealFull: return HealFull(ref frame, playerId);
      case DebugAction.KillHero: return KillHero(ref frame, playerId);
      case DebugAction.SpawnMinions: return SpawnMinions(ref frame, playerId, param, factionId, target);
      case DebugAction.ClearMinions: return ClearMinions(ref frame, playerId, param);
      case DebugAction.TeleportHero: return TeleportHero(ref frame, playerId, target);
      default:
        Reject(ref frame, playerId, action, "unknown_action");
        return false;
    }
  }

  // Destroys the hero outright rather than re-skinning it: the skill set, stats and behavior state all
  // come off the HeroAsset at spawn. HeroSpawnSystem sees the empty slot on the next tick and rebuilds
  // from the faction written here, which is the same path a fresh match takes.
  private static bool SwitchFaction(ref Frame frame, int playerId, int factionId) {
    if (!frame.AssetRegistry.TryGet<FactionAsset>(factionId, out _)) {
      Reject(ref frame, playerId, DebugAction.SwitchFaction, $"faction_asset_missing factionId={factionId}");
      return false;
    }

    var wrote = false;
    var filter = frame.Filter<PlayerFaction>();
    while (filter.Next(out var entity)) {
      ref var slot = ref frame.Get<PlayerFaction>(entity);
      if (slot.PlayerId != playerId)
        continue;

      slot.FactionId = factionId;
      slot.Confirmed = 1;
      wrote = true;
      break;
    }

    if (!wrote) {
      Reject(ref frame, playerId, DebugAction.SwitchFaction, "no_slot_for_player");
      return false;
    }

    if (UnitLookup.TryGetPlayerHero(ref frame, playerId, out var hero))
      frame.DestroyEntity(hero);

    Log(ref frame, playerId, DebugAction.SwitchFaction, $"factionId={factionId}");
    return true;
  }

  private static bool AddGold(ref Frame frame, int playerId, int amount) {
    if (!TryGetHeroWith<Inventory>(ref frame, playerId, DebugAction.AddGold, out var hero))
      return false;

    ref var inventory = ref frame.Get<Inventory>(hero);
    inventory.Gold += amount;
    if (inventory.Gold < 0) inventory.Gold = 0;

    Log(ref frame, playerId, DebugAction.AddGold, $"amount={amount} goldNow={inventory.Gold}");
    return true;
  }

  // Deposited as raw XP so ExperienceSystem converts it into levels, stat growth and skill points on
  // its own pass - the same route a kill takes.
  private static bool AddExperience(ref Frame frame, int playerId, int amount) {
    if (!TryGetHeroWith<Experience>(ref frame, playerId, DebugAction.AddExperience, out var hero))
      return false;

    ref var experience = ref frame.Get<Experience>(hero);
    experience.Xp += amount;
    if (experience.Xp < 0) experience.Xp = 0;

    Log(ref frame, playerId, DebugAction.AddExperience, $"amount={amount} xpNow={experience.Xp}");
    return true;
  }

  private static bool AddSkillPoints(ref Frame frame, int playerId, int amount) {
    if (!TryGetHeroWith<Skills>(ref frame, playerId, DebugAction.AddSkillPoints, out var hero))
      return false;

    ref var skills = ref frame.Get<Skills>(hero);
    skills.SkillPoints += amount;
    if (skills.SkillPoints < 0) skills.SkillPoints = 0;

    Log(ref frame, playerId, DebugAction.AddSkillPoints, $"amount={amount} pointsNow={skills.SkillPoints}");
    return true;
  }

  // Ranks up through SkillActions.TryUpgrade rather than writing Ranks directly, so each rank still
  // runs the hero's OnRankGained and the passives a ranked slot grants actually land.
  private static bool MaxSkills(ref Frame frame, int playerId) {
    if (!TryGetHeroWith<Skills>(ref frame, playerId, DebugAction.MaxSkills, out var hero))
      return false;

    var ranksGained = 0;
    for (var slot = 0; slot < Skills.MaxSlots; slot++) {
      var skillAssetId = frame.GetReadOnly<Skills>(hero).GetSkillAssetId(slot);
      if (!frame.AssetRegistry.TryGet<SkillAsset>(skillAssetId, out var skill))
        continue;

      while (frame.GetReadOnly<Skills>(hero).GetRank(slot) < skill.MaxRank) {
        frame.Get<Skills>(hero).SkillPoints++;
        if (!SkillActions.TryUpgrade(ref frame, playerId, slot))
          break;

        ranksGained++;
      }
    }

    Log(ref frame, playerId, DebugAction.MaxSkills, $"ranksGained={ranksGained}");
    return true;
  }

  private static bool RefreshCooldowns(ref Frame frame, int playerId) {
    if (!TryGetHeroWith<Skills>(ref frame, playerId, DebugAction.RefreshCooldowns, out var hero))
      return false;

    ref var skills = ref frame.Get<Skills>(hero);
    for (var slot = 0; slot < Skills.MaxSlots; slot++)
      skills.StartCooldown(slot, 0);

    Log(ref frame, playerId, DebugAction.RefreshCooldowns, "");
    return true;
  }

  private static bool HealFull(ref Frame frame, int playerId) {
    if (!TryGetHeroWith<Health>(ref frame, playerId, DebugAction.HealFull, out var hero))
      return false;

    HealthApplication.RestoreToFull(ref frame, hero);
    ManaApplication.RestoreToFull(ref frame, hero);
    Log(ref frame, playerId, DebugAction.HealFull, "");
    return true;
  }

  // Zeroed rather than destroyed: a hero carries Respawns, so RespawnSystem picks it up next tick and
  // the whole death -> timer -> respawn path runs.
  private static bool KillHero(ref Frame frame, int playerId) {
    if (!TryGetHeroWith<Health>(ref frame, playerId, DebugAction.KillHero, out var hero))
      return false;

    frame.Get<Health>(hero).Current = FP64.Zero;
    Log(ref frame, playerId, DebugAction.KillHero, "");
    return true;
  }

  // teamId 0 means "the other side": the first team id that isn't the caller's, so the common case of
  // wanting something to hit is one command with no argument.
  //
  // The faction is stamped onto each minion rather than left to the view's team lookup. That lookup
  // reads the team's PlayerFaction slot, and a playground has a slot only for the team the one player
  // is on - minions spawned on any other team would resolve to faction 0 and the view would throw for
  // every one of them, every tick.
  private static bool SpawnMinions(ref Frame frame, int playerId, int teamId, int factionId, FPVector3 target) {
    if (!UnitLookup.TryGetPlayerTeamId(ref frame, playerId, out var playerTeamId)) {
      Reject(ref frame, playerId, DebugAction.SpawnMinions, "no_team_for_player");
      return false;
    }

    if (teamId <= 0)
      teamId = ResolveOpposingTeam(ref frame, playerTeamId);

    factionId = ResolveSpawnFaction(ref frame, playerId, teamId, factionId);
    var stats = frame.AssetRegistry.Get<MinionStatsAsset>();
    var spacing = frame.AssetRegistry.Get<WaveRulesAsset>().MinionSpacing;
    for (var i = 0; i < MinionsPerSpawn; i++) {
      var offset = RingOffset(i, spacing);
      var minion = MinionFactory.Spawn(ref frame, stats, target + offset, FP64.Zero, teamId, DebugWaveId);
      frame.Add(minion, new Faction(factionId));
    }

    Log(ref frame, playerId, DebugAction.SpawnMinions,
      $"teamId={teamId} factionId={factionId} count={MinionsPerSpawn} at=({target.x}, {target.z})");
    return true;
  }

  // Explicit pick first, then whatever the target team actually plays, then the caller's own faction so
  // the models at least exist. Only reaches the default on a frame with no factions decided at all.
  private static int ResolveSpawnFaction(ref Frame frame, int playerId, int teamId, int factionId) {
    if (factionId > 0)
      return factionId;

    var slots = frame.Filter<PlayerFaction>();
    while (slots.Next(out var slot)) {
      ref readonly var pf = ref frame.GetReadOnly<PlayerFaction>(slot);
      if (pf.TeamId == teamId)
        return pf.FactionId;
    }

    if (UnitLookup.TryGetPlayerHero(ref frame, playerId, out var hero) && frame.Has<Faction>(hero))
      return frame.GetReadOnly<Faction>(hero).FactionId;

    return SimulationSetup.DefaultFactionId;
  }

  // Destroyed outright rather than damaged to death, so nothing pays out XP or gold for a cleanup.
  private static bool ClearMinions(ref Frame frame, int playerId, int teamId) {
    Scratch.Clear();
    var filter = frame.Filter<Minion, Team>();
    while (filter.Next(out var entity)) {
      if (teamId > 0 && frame.GetReadOnly<Team>(entity).TeamId != teamId)
        continue;

      Scratch.Add(entity);
    }

    for (var i = 0; i < Scratch.Count; i++)
      frame.DestroyEntity(Scratch[i]);

    Log(ref frame, playerId, DebugAction.ClearMinions, $"teamId={teamId} count={Scratch.Count}");
    Scratch.Clear();
    return true;
  }

  private static bool TeleportHero(ref Frame frame, int playerId, FPVector3 target) {
    if (!TryGetHeroWith<TransformComponent>(ref frame, playerId, DebugAction.TeleportHero, out var hero))
      return false;

    frame.Get<TransformComponent>(hero).Position = target;
    UnitIntent.ClearMoveTarget(ref frame, hero);
    UnitIntent.ClearAttackIntent(ref frame, hero);
    ResetNavAgent(ref frame, hero, target);

    Log(ref frame, playerId, DebugAction.TeleportHero, $"at=({target.x}, {target.z})");
    return true;
  }

  // NavAgentComponent.Init resets the tuned Radius/Speed/Acceleration to component defaults, so they
  // are carried across by hand - same dance RespawnSystem does when it moves a hero.
  private static void ResetNavAgent(ref Frame frame, EntityRef entity, FPVector3 position) {
    if (!frame.Has<NavAgentComponent>(entity))
      return;

    ref var nav = ref frame.Get<NavAgentComponent>(entity);
    var radius = nav.Radius;
    var speed = nav.Speed;
    var acceleration = nav.Acceleration;
    NavAgentComponent.Stop(ref nav);
    NavAgentComponent.Init(ref nav, position);
    nav.Radius = radius;
    nav.Speed = speed;
    nav.Acceleration = acceleration;
  }

  // Lowest team id with a crystal that isn't the caller's. On a playground TeamPruneSystem has usually
  // already deleted every base but the player's, so the fallback is just "not mine" - minions need a
  // team id, not a base to belong to.
  private static int ResolveOpposingTeam(ref Frame frame, int playerTeamId) {
    var best = 0;
    var filter = frame.Filter<Crystal, Team>();
    while (filter.Next(out var entity)) {
      var teamId = frame.GetReadOnly<Team>(entity).TeamId;
      if (teamId == playerTeamId || teamId <= 0)
        continue;

      if (best == 0 || teamId < best)
        best = teamId;
    }

    return best != 0 ? best : playerTeamId == 1 ? 2 : 1;
  }

  private static readonly FP64 InvSqrt2 = FP64.One / FP64.Sqrt(FP64.FromInt(2));

  // Index 0 sits on the point, the rest ring it at one minion spacing per step out. Enough to keep
  // a cluster from spawning inside itself without pulling in WaveSpawnSystem's occupancy search.
  private static FPVector3 RingOffset(int index, FP64 spacing) {
    if (index <= 0)
      return FPVector3.Zero;

    // Diagonal offsets, so the per-axis step is the ring radius over sqrt(2). Halving it instead
    // put ring 1 at 0.71x the advertised spacing - close enough to spawn two minions inside each other.
    var step = spacing * FP64.FromInt(index) * InvSqrt2;
    return (index % 4) switch {
      0 => new FPVector3(step, FP64.Zero, step),
      1 => new FPVector3(step, FP64.Zero, -step),
      2 => new FPVector3(-step, FP64.Zero, step),
      _ => new FPVector3(-step, FP64.Zero, -step)
    };
  }

  private static bool TryGetHeroWith<T>(ref Frame frame, int playerId, DebugAction action, out EntityRef hero)
    where T : unmanaged, IComponent {
    if (!UnitLookup.TryGetPlayerHero(ref frame, playerId, out hero)) {
      Reject(ref frame, playerId, action, "no_hero_for_player");
      return false;
    }

    if (frame.Has<T>(hero))
      return true;

    Reject(ref frame, playerId, action, $"hero_missing_{typeof(T).Name}");
    return false;
  }

  private static void Log(ref Frame frame, int playerId, DebugAction action, string details) {
    SimLog.Info(ref frame, $"[Debug] {action} tick={frame.Tick} playerId={playerId} {details}");
  }

  private static void Reject(ref Frame frame, int playerId, DebugAction action, string reason) {
    SimLog.Info(ref frame,
      $"[Debug] REJECT tick={frame.Tick} playerId={playerId} action={action} reason={reason}");
  }
}
