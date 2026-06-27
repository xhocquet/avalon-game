using global::Godot;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;
using Meesles.Avalon.Sim.Models;

namespace Meesles.Avalon {
  public partial class PlayerEntity : EntityViewNode, ISelectableTeamView, IPlayerView {
    private const string UnitsGroup = "units";
    private const string AnimIdle = "SK_PlayerDefault_ao|A_Player_CosmeticIdle";
    private const string AnimWalk = "SK_PlayerDefault_ao|A_Player_Walk";
    private const string AnimDeath = "SK_PlayerDefault_ao|A_Player_Death";

    private AnimationPlayer _anim;
    private int _ownerId = -1;
    private int _teamId = -1;
    private bool _isMoving;
    private bool _isDead;

    public override void OnInitialize() {
      _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
      if (_anim != null) {
        var walkAnim = _anim.GetAnimation(AnimWalk);
        if (walkAnim != null)
          walkAnim.LoopMode = Animation.LoopModeEnum.Linear;

        var deathAnim = _anim.GetAnimation(AnimDeath);
        if (deathAnim != null)
          deathAnim.LoopMode = Animation.LoopModeEnum.None;
      }
    }

    public override void OnActivate(FrameRef frame) {
      AddToGroup(UnitsGroup);
      _isMoving = false;
      _isDead = false;
      _anim?.Play(AnimIdle);

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
      _isDead = false;
    }

    public override void OnUpdateView() {
      if (Engine == null || _anim == null) return;
      var frame = Engine.PredictedFrame.Frame;
      if (frame == null) return;

      bool dead = frame.Has<PendingRespawn>(EntityRef);
      if (dead != _isDead) {
        _isDead = dead;
        _isMoving = false;
        _anim.Play(_isDead ? AnimDeath : AnimIdle);
      }

      if (_isDead)
        return;

      bool moving = frame.Has<UnitMoveTarget>(EntityRef);
      if (moving == _isMoving) return;
      _isMoving = moving;
      _anim.Play(_isMoving ? AnimWalk : AnimIdle);
    }

    public int OwnerId => _ownerId;
    public override bool OwnerMatches(int ownerId) => _ownerId == ownerId;
    public bool TeamMatches(int teamId) => _teamId == teamId;
  }
}
