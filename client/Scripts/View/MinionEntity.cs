using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public partial class MinionEntity : EntityViewNode, ISelectableTeamView {
    private const string UnitsGroup = "units";
    private const string AnimRun = "Action";
    private static readonly Quaternion FlipY = new(Vector3.Up, Mathf.Pi);

    private AnimationPlayer _anim;
    private int _ownerId = -1;
    private int _teamId = -1;
    private bool _isMoving;

    public override void OnInitialize() {
      EntityViewPhysics.DisableGodotCollision(this);

      _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
      if (_anim != null) {
        var runAnim = _anim.GetAnimation(AnimRun);
        if (runAnim != null)
          runAnim.LoopMode = Animation.LoopModeEnum.Linear;
      }
    }

    public override void OnActivate(FrameRef frame) {
      AddToGroup(UnitsGroup);
      _isMoving = false;
      _anim?.Stop();

      var live = frame.Frame;
      if (live != null && live.Has<OwnerComponent>(EntityRef))
        _ownerId = live.GetReadOnly<OwnerComponent>(EntityRef).OwnerId;
      if (live != null && live.Has<Team>(EntityRef))
        _teamId = live.GetReadOnly<Team>(EntityRef).TeamId;

      GetNodeOrNull<SelectionIndicator>("SelectionIndicator")?.SetTeamId(_teamId);
    }

    public override void OnDeactivate() {
      RemoveFromGroup(UnitsGroup);
      _ownerId = -1;
      _teamId = -1;
      _isMoving = false;
    }

    public override void OnUpdateView() {
      if (Engine == null || _anim == null) return;
      var frame = Engine.PredictedFrame.Frame;
      if (frame == null) return;

      bool moving = frame.Has<UnitMoveTarget>(EntityRef);
      if (moving == _isMoving) return;
      _isMoving = moving;

      if (_isMoving)
        _anim.Play(AnimRun);
      else
        _anim.Stop();
    }

    public override void OnLateUpdateView() {
      Quaternion *= FlipY;
    }

    public override bool OwnerMatches(int ownerId) => _ownerId == ownerId;
    public bool TeamMatches(int teamId) => _teamId == teamId;
  }
}
