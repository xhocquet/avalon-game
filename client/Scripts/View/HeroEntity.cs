using Godot;
using Meesles.Avalon.Sim.Models;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class HeroEntity : EntityViewNode, ISelectableTeamView, IPlayerView, IAttackableView {
  private const string UnitsGroup = "units";
  private const string AnimIdle = "SK_PlayerDefault_ao|A_Player_CosmeticIdle";
  private const string AnimWalk = "SK_PlayerDefault_ao|A_Player_Walk";
  private const string AnimDeath = "SK_PlayerDefault_ao|A_Player_Death";

  [Export] public string WalkAnimationOverride { get; set; } = "";
  [Export] public string IdleAnimationOverride { get; set; } = "";

  [Export] public float SelectPickRadius { get; set; } = -1.0f;
  [Export] public float SelectPickHeight { get; set; } = -1.0f;

  private AnimationPlayer _anim;
  private bool _isDead;
  private bool _isMoving;
  private int _teamId = -1;

  private string WalkAnim => string.IsNullOrEmpty(WalkAnimationOverride) ? AnimWalk : WalkAnimationOverride;
  private string IdleAnim => string.IsNullOrEmpty(IdleAnimationOverride) ? AnimIdle : IdleAnimationOverride;

  // Play() is a silent no-op when the name isn't on this model's AnimationPlayer (e.g. a rig with only
  // a walk clip), which would otherwise leave whatever animation last played stuck looping forever.
  private void PlayOrStop(string animName) {
    if (!_anim.HasAnimation(animName)) {
      GD.PushWarning($"{Name}: missing animation \"{animName}\" on {_anim.GetPath()}, stopping instead.");
      _anim.Stop();
      return;
    }

    _anim.Play(animName);
  }

  public void OnAttackVfx(Vector3 targetPosition) {
    // TODO: attack animation / particles
  }

  public void OnHitVfx(int damage, Vector3 attackerPosition) {
    // TODO: hit reaction / particles
  }

  public int OwnerId { get; private set; } = -1;

  public bool TeamMatches(int teamId) {
    return _teamId == teamId;
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);

    _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
    if (_anim != null) {
      var walkAnim = _anim.GetAnimation(WalkAnim);
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
    if (_anim != null) PlayOrStop(IdleAnim);

    var live = frame.Frame;
    if (live != null && live.Has<Unit>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<Unit>(EntityRef).UnitId);
    if (live != null && live.Has<OwnerComponent>(EntityRef))
      OwnerId = live.GetReadOnly<OwnerComponent>(EntityRef).OwnerId;
    if (live != null && live.Has<Team>(EntityRef))
      _teamId = live.GetReadOnly<Team>(EntityRef).TeamId;

    GetNodeOrNull<SelectionIndicator>("SelectionIndicator")?.SetTeamId(_teamId);
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    OwnerId = -1;
    _teamId = -1;
    _isDead = false;
  }

  public override void OnUpdateView() {
    if (Engine == null || _anim == null) return;
    var frame = Engine.PredictedFrame.Frame;
    if (frame == null) return;

    var dead = frame.Has<PendingRespawn>(EntityRef);
    if (dead != _isDead) {
      _isDead = dead;
      _isMoving = false;
      PlayOrStop(_isDead ? AnimDeath : IdleAnim);
    }

    if (_isDead)
      return;

    var moving = frame.Has<UnitMoveTarget>(EntityRef);
    if (moving == _isMoving) return;
    _isMoving = moving;
    PlayOrStop(_isMoving ? WalkAnim : IdleAnim);
  }

  public override bool OwnerMatches(int ownerId) {
    return OwnerId == ownerId;
  }
}
