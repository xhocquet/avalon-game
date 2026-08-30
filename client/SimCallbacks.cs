using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Assets;
using Meesles.Avalon.Sim.Commands;
using Meesles.Avalon.Sim.Components;
using Meesles.Avalon.Sim.Navigation;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Logging;

namespace Meesles.Avalon.Client;

public class SimCallbacks(
  InputCapture input,
  byte[] navMeshBytes,
  IKLogger logger) : ISimulationCallbacks {
  private const int CheatSendTick = 60;
  private const int CheatRetryIntervalTicks = 30;
  private const int CheatMaxAttempts = 10;
  private int _cheatAttempts;
  private int _nextCheatSendTick = CheatSendTick;
  private bool _factionSelectionSent;
  private IKlothoEngine _engine;
  private InputCapture _input = input;

  public void RegisterSystems(EcsSimulation simulation) {
    SimulationSetup.RegisterSystems(
      simulation, NavigationRuntime.FromBytes(navMeshBytes, logger)
    );
  }

  public void OnInitializeWorld(IKlothoEngine engine) {
    _engine = engine;
    var frame = engine.PredictedFrame.Frame;

    if (frame.AssetRegistry.TryGet<MapLayoutAsset>(out var layout)) {
      var n = layout.MarkerTypes?.Length ?? 0;
      GD.Print($"[SimCallbacks] MapLayout has {n} markers (maxPlayers={engine.SessionConfig.MaxPlayers}):");

      for (var i = 0; i < n; i++) {
        var p = layout.MarkerPositions[i];
        var type = (MapMarkerType)layout.MarkerTypes[i];
        GD.Print($"[{i}] type={type} team={layout.MarkerTeams[i]} pos=({p.x}, {p.y}, {p.z})");
      }
    }
    else {
      GD.PrintErr("[SimCallbacks] No MapLayoutAsset in registry.");
    }

    SimulationSetup.InitializeWorld(engine, engine.SessionConfig.MaxPlayers);

    GD.Print("[SimCallbacks] Post-init entity positions:");
    var postFrame = engine.PredictedFrame.Frame;
    var filter = postFrame.Filter<TransformComponent, Team>();

    while (filter.Next(out var entity)) {
      ref readonly var pos = ref postFrame.GetReadOnly<TransformComponent>(entity);
      ref readonly var team = ref postFrame.GetReadOnly<Team>(entity);
      var kind = postFrame.Has<Crystal>(entity) ? "Crystal"
        : postFrame.Has<Turret>(entity) ? "Turret"
        : postFrame.Has<SpawnPoint>(entity) ? "SpawnPoint"
        : postFrame.Has<Hero>(entity) ? "Hero"
        : "Unknown";
      var p = pos.Position;
      GD.Print($"{kind} team={team.TeamId} pos=({p.x:F2}, {p.y:F2}, {p.z:F2})");
    }
  }

  // The frame is the confirmation: a rejected command leaves the flags unset, so it is asked again.
  // Capped so a cheat the sim keeps refusing (an unknown flag) doesn't starve real input forever.
  private bool ShouldSendCheats(int playerId, int tick) {
    if (CheatOptions.Flags == CheatFlags.None || _cheatAttempts >= CheatMaxAttempts)
      return false;
    if (tick < _nextCheatSendTick)
      return false;

    var frame = _engine?.PredictedFrame.Frame;
    return frame == null || !Cheats.AreAllEnabled(ref frame, playerId, CheatOptions.Flags);
  }

  public void OnPollInput(int playerId, int tick, ICommandSender sender) {
    if (!_factionSelectionSent) {
      sender.Send(new SelectFactionCommand { FactionId = FactionSelection.SelectedFactionId });
      _factionSelectionSent = true;
      LogCommandSent("SelectFactionCommand", tick, playerId, $"factionId={FactionSelection.SelectedFactionId}");
    }

    // Resent until the flags show up in the frame rather than fired once. A command is stamped for the
    // tick the poll reports, and during the bootstrap catch-up burst that tick can already be behind
    // what the server has executed - InputCollector rejects it as a past tick and godmode silently
    // never applies. Waiting for a fixed tick only moves which burst it lands in.
    if (ShouldSendCheats(playerId, tick)) {
      _cheatAttempts++;
      _nextCheatSendTick = tick + CheatRetryIntervalTicks;
      sender.Send(new SetCheatCommand { Flags = (int)CheatOptions.Flags, Enabled = 1 });
      LogCommandSent("SetCheatCommand", tick, playerId,
        $"flags={CheatOptions.Flags} attempt={_cheatAttempts}");
      return;
    }

    // Ahead of the gameplay queues: a console command is a deliberate one-off and should not wait
    // behind a stream of held-down orders.
    if (_input != null && _input.TryConsumeDebugCommand(out var debugCommand)) {
      sender.Send(debugCommand);
      LogCommandSent(debugCommand.GetType().Name, tick, playerId, Describe(debugCommand));
      return;
    }

    if (_input != null && _input.TryConsumePurchaseCommand(out var purchaseCommand)) {
      sender.Send(purchaseCommand);
      LogCommandSent("PurchaseItemCommand", tick, playerId, $"itemAssetId={purchaseCommand.ItemAssetId}");
      return;
    }

    if (_input != null && _input.TryConsumeUpgradeSkillCommand(out var upgradeSkillCommand)) {
      sender.Send(upgradeSkillCommand);
      LogCommandSent("UpgradeSkillCommand", tick, playerId, $"slot={upgradeSkillCommand.Slot}");
      return;
    }

    if (_input != null && _input.TryConsumeCastSkillCommand(out var castSkillCommand)) {
      sender.Send(castSkillCommand);
      LogCommandSent("CastSkillCommand", tick, playerId, $"slot={castSkillCommand.Slot}");
      return;
    }

    if (_input != null && _input.TryConsumeAttackCommand(out var attackCommand)) {
      sender.Send(attackCommand);
      LogCommandSent("AttackCommand", tick, playerId,
        $"targetUnitId={attackCommand.TargetUnitId} sourceCount={attackCommand.UnitIds.Count}");
      return;
    }

    if (_input != null && _input.TryConsumeMoveCommand(out var moveCommand)) {
      LogCommandSent("MoveCommand", tick, playerId,
        $"target=({moveCommand.TargetX}, {moveCommand.TargetZ}) unitCount={moveCommand.UnitIds.Count}");
      sender.Send(moveCommand);
    }
  }

  public void SetInput(InputCapture input) {
    _input = input;
  }

  private static string Describe(ICommand command) {
    return command switch {
      DebugCommand debug => $"action={(DebugAction)debug.Action} param={debug.Param}",
      SetCheatCommand cheat => $"flags={(CheatFlags)cheat.Flags} enabled={cheat.Enabled}",
      _ => ""
    };
  }

  private void LogCommandSent(string commandName, int tick, int playerId, string details) {
    logger.KInformation($"[SimCallbacks] Send {commandName} tick={tick} playerId={playerId} {details}");
  }
}
