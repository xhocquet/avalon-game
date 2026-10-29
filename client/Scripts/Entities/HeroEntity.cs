using Godot;
using Meesles.Avalon.Client.Scripts.GameState;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class HeroEntity : TeamEntityViewNode, IPlayerView, IAttackableView {
  private const string UnitsGroup = "units";
  private const string AnimIdle = "SK_PlayerDefault_ao|A_Player_CosmeticIdle";
  private const string AnimWalk = "SK_PlayerDefault_ao|A_Player_Walk";
  private const string AnimDeath = "SK_PlayerDefault_ao|A_Player_Death";

  [Export] public string WalkAnimationOverride { get; set; } = "";
  [Export] public string IdleAnimationOverride { get; set; } = "";
  [Export] public string DeathAnimationOverride { get; set; } = "";
  // Empty rigs use the debug fallback.
  [Export] public string AttackAnimationOverride { get; set; } = "";
  [Export] public Godot.Collections.Dictionary<int, string> SkillAnimationOverrides { get; set; } = new();

  // Contact time keeps the clip aligned to the sim damage tick.
  [Export] public float AttackContactTime { get; set; }

  [Export] public float SelectPickRadius { get; set; } = -1.0f;
  [Export] public float SelectPickHeight { get; set; } = -1.0f;

  private AnimationPlayer _anim;
  private bool _isDead;
  private bool _isMoving;
  private bool _isAttacking;
  private string _skillAnimation = "";

  private string WalkAnim => string.IsNullOrEmpty(WalkAnimationOverride) ? AnimWalk : WalkAnimationOverride;
  private string IdleAnim => string.IsNullOrEmpty(IdleAnimationOverride) ? AnimIdle : IdleAnimationOverride;
  private string DeathAnim => string.IsNullOrEmpty(DeathAnimationOverride) ? AnimDeath : DeathAnimationOverride;
  private bool HasAttackAnim => _anim != null && !string.IsNullOrEmpty(AttackAnimationOverride)
                                && _anim.HasAnimation(AttackAnimationOverride);

  // Stop instead of leaving the previous clip playing.
  private void PlayOrStop(string animName) {
    if (!_anim.HasAnimation(animName)) {
      GD.PushWarning($"{Name}: missing animation \"{animName}\" on {_anim.GetPath()}, stopping instead.");
      _anim.Stop();
      return;
    }

    _anim.Play(animName);
  }

  public bool OnAttackWindupVfx(Vector3 targetPosition, float windupSeconds) {
    if (_isDead || !string.IsNullOrEmpty(_skillAnimation) || !HasAttackAnim) return false;

    _isAttacking = true;
    _anim.SpeedScale = AttackPlaybackSpeed.For(AttackContactTime, windupSeconds);
    _anim.Play(AttackAnimationOverride);
    _anim.Seek(0.0, true); // Restart an already-running clip.
    return true;
  }

  public void OnAttackCanceledVfx() {
    if (!_isAttacking) return;
    _isAttacking = false;
    if (!_isDead) ReturnToLocomotion();
  }

  // Restore locomotion after the one-shot attack.
  private void OnAnimationFinished(StringName animName) {
    if (!_isAttacking || (string)animName != AttackAnimationOverride) return;
    _isAttacking = false;
    if (!_isDead) ReturnToLocomotion();
  }

  private void ReturnToLocomotion() {
    _anim.SpeedScale = 1.0f;
    PlayOrStop(!string.IsNullOrEmpty(_skillAnimation) ? _skillAnimation : _isMoving ? WalkAnim : IdleAnim);
  }

  public void OnHitVfx(float damage, Vector3 attackerPosition) {
  }

  public int OwnerId { get; private set; } = -1;

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);

    _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
    if (_anim != null) {
      if (_anim.HasAnimation(WalkAnim)) {
        var walkAnim = _anim.GetAnimation(WalkAnim);
        walkAnim.LoopMode = Animation.LoopModeEnum.Linear;
      }

      if (_anim.HasAnimation(DeathAnim)) {
        var deathAnim = _anim.GetAnimation(DeathAnim);
        deathAnim.LoopMode = Animation.LoopModeEnum.None;
      }

      if (HasAttackAnim) {
        // AnimationFinished requires a one-shot clip.
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
    _skillAnimation = "";
    if (_anim != null) _anim.SpeedScale = 1.0f;
    if (_anim != null) PlayOrStop(IdleAnim);

    var live = frame.Frame;
    if (live != null && live.Has<UnitIdentity>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<UnitIdentity>(EntityRef).UnitId);
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
    _skillAnimation = "";
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
      _skillAnimation = "";
      _anim.SpeedScale = 1.0f;
      PlayOrStop(_isDead ? DeathAnim : IdleAnim);
    }

    if (_isDead)
      return;

    var moving = frame.Has<UnitMoveTarget>(EntityRef) && !SnareController.IsSnared(ref frame, EntityRef);
    var skillAnimation = "";
    if (frame.Has<SkillDash>(EntityRef)) {
      ref readonly var dash = ref frame.GetReadOnly<SkillDash>(EntityRef);
      if (dash.IsActive && SkillAnimationOverrides.TryGetValue(dash.SourceId, out var clip) &&
          _anim.HasAnimation(clip))
        skillAnimation = clip;
    }
    UpdateLocomotion(moving, skillAnimation);
  }

  private void UpdateLocomotion(bool moving, string skillAnimation) {
    var skillChanged = skillAnimation != _skillAnimation;
    var movementChanged = moving != _isMoving;
    _isMoving = moving;
    _skillAnimation = skillAnimation;

    if (!string.IsNullOrEmpty(_skillAnimation)) {
      _isAttacking = false;
      if (skillChanged) ReturnToLocomotion();
      return;
    }

    if (!skillChanged && !movementChanged) return;

    // Stopping at attack range must not interrupt windup.
    if (!_isMoving && _isAttacking) return;

    _isAttacking = false;
    ReturnToLocomotion();
  }

  public override bool OwnerMatches(int ownerId) {
    return OwnerId == ownerId;
  }
}
