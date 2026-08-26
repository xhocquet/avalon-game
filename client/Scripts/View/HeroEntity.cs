using Godot;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;
using xpTURN.Klotho.Godot;

namespace Meesles.Avalon;

public partial class HeroEntity : TeamEntityViewNode, IPlayerView, IAttackableView {
  private const string UnitsGroup = "units";
  private const string AnimIdle = "SK_PlayerDefault_ao|A_Player_CosmeticIdle";
  private const string AnimWalk = "SK_PlayerDefault_ao|A_Player_Walk";
  private const string AnimDeath = "SK_PlayerDefault_ao|A_Player_Death";

  [Export] public string WalkAnimationOverride { get; set; } = "";
  [Export] public string IdleAnimationOverride { get; set; } = "";
  // No default: rigs without an authored attack clip leave this empty and fall back to debug lines.
  [Export] public string AttackAnimationOverride { get; set; } = "";

  // Seconds into the attack clip where the weapon actually connects. Set it and the clip is played
  // at whatever speed puts that frame on the sim's damage tick; leave it 0 and the clip runs at 1x
  // from the start of the wind-up, which drifts as soon as the two disagree.
  [Export] public float AttackContactTime { get; set; }

  [Export] public float SelectPickRadius { get; set; } = -1.0f;
  [Export] public float SelectPickHeight { get; set; } = -1.0f;

  private AnimationPlayer _anim;
  private bool _isDead;
  private bool _isMoving;
  private bool _isAttacking;

  private string WalkAnim => string.IsNullOrEmpty(WalkAnimationOverride) ? AnimWalk : WalkAnimationOverride;
  private string IdleAnim => string.IsNullOrEmpty(IdleAnimationOverride) ? AnimIdle : IdleAnimationOverride;
  private bool HasAttackAnim => _anim != null && !string.IsNullOrEmpty(AttackAnimationOverride)
                                && _anim.HasAnimation(AttackAnimationOverride);

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

  public bool OnAttackWindupVfx(Vector3 targetPosition, float windupSeconds) {
    if (_isDead || !HasAttackAnim) return false;

    _isAttacking = true;
    _anim.SpeedScale = AttackPlaybackSpeed.For(AttackContactTime, windupSeconds);
    _anim.Play(AttackAnimationOverride);
    _anim.Seek(0.0, true); // Play() on the clip already running is a no-op, so rewind to restart it.
    return true;
  }

  public void OnAttackCanceledVfx() {
    if (!_isAttacking) return;
    _isAttacking = false;
    if (!_isDead) ReturnToLocomotion();
  }

  // The attack clip is one-shot and owns the rig until it ends; hand control back to locomotion.
  private void OnAnimationFinished(StringName animName) {
    if (!_isAttacking || (string)animName != AttackAnimationOverride) return;
    _isAttacking = false;
    if (!_isDead) ReturnToLocomotion();
  }

  private void ReturnToLocomotion() {
    _anim.SpeedScale = 1.0f;
    PlayOrStop(_isMoving ? WalkAnim : IdleAnim);
  }

  public void OnHitVfx(float damage, Vector3 attackerPosition) {
    // TODO: hit reaction / particles
  }

  public int OwnerId { get; private set; } = -1;

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

      if (HasAttackAnim) {
        // Must not loop, or AnimationFinished never fires and the hero is stuck swinging.
        _anim.GetAnimation(AttackAnimationOverride).LoopMode = Animation.LoopModeEnum.None;
        _anim.AnimationFinished += OnAnimationFinished;
      }
    }
  }

  public override void OnActivate(FrameRef frame) {
    AddToGroup(UnitsGroup);
    _isMoving = false;
    _isDead = false;
    _isAttacking = false;
    if (_anim != null) PlayOrStop(IdleAnim);

    var live = frame.Frame;
    if (live != null && live.Has<UnitIdComponent>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<UnitIdComponent>(EntityRef).UnitId);
    if (live != null && live.Has<OwnerComponent>(EntityRef))
      OwnerId = live.GetReadOnly<OwnerComponent>(EntityRef).OwnerId;
    BindTeam(frame);
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    OwnerId = -1;
    ClearTeam();
    _isDead = false;
    _isAttacking = false;
  }

  public override void OnUpdateView() {
    if (Engine == null || _anim == null) return;
    var frame = Engine.PredictedFrame.Frame;
    if (frame == null) return;

    var dead = frame.Has<PendingRespawn>(EntityRef);
    if (dead != _isDead) {
      _isDead = dead;
      _isMoving = false;
      _isAttacking = false;
      _anim.SpeedScale = 1.0f;
      PlayOrStop(_isDead ? AnimDeath : IdleAnim);
    }

    if (_isDead)
      return;

    var moving = frame.Has<UnitMoveTarget>(EntityRef);
    if (moving == _isMoving) return;
    _isMoving = moving;

    // Only walking off cuts the swing. Coming to a stop must not, because the tick a unit stops is
    // the tick it comes into range and starts winding up - overriding with idle there ate the attack
    // clip on the frame it began.
    if (!_isMoving && _isAttacking) return;

    _isAttacking = false;
    _anim.SpeedScale = 1.0f;
    PlayOrStop(_isMoving ? WalkAnim : IdleAnim);
  }

  public override bool OwnerMatches(int ownerId) {
    return OwnerId == ownerId;
  }
}
