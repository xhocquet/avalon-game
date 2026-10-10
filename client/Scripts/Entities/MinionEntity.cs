using Godot;
using Meesles.Avalon.Client.Scripts.GameState;
using Meesles.Avalon.Client.Scripts.Interfaces;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.Deterministic.Math;
using xpTURN.Klotho.Deterministic.Navigation;

namespace Meesles.Avalon.Client.Scripts.Entities;

public partial class MinionEntity : TeamEntityViewNode, IAttackableView {
  private const string UnitsGroup = "units";
  private const string AnimRun = "Run";
  private const string AnimIdle = "Stand";

  [Export] public string WalkAnimationOverride { get; set; } = "";
  [Export] public string IdleAnimationOverride { get; set; } = "";
  // Empty rigs use the debug fallback.
  [Export] public string AttackAnimationOverride { get; set; } = "";

  // Contact time keeps the clip aligned to the sim damage tick.
  [Export] public float AttackContactTime { get; set; }

  [Export] public float SelectPickRadius { get; set; } = 0.5f;
  [Export] public float SelectPickHeight { get; set; } = 1.2f;

  private AnimationPlayer _anim;
  private bool _isMoving;
  private bool _isAttacking;

  private string RunAnim => string.IsNullOrEmpty(WalkAnimationOverride) ? AnimRun : WalkAnimationOverride;
  private string IdleAnim => string.IsNullOrEmpty(IdleAnimationOverride) ? AnimIdle : IdleAnimationOverride;
  private bool HasAttackAnim => _anim != null && !string.IsNullOrEmpty(AttackAnimationOverride)
                                && _anim.HasAnimation(AttackAnimationOverride);

  // Stop instead of leaving the previous clip playing.
  private void PlayOrStop(string animName) {
    if (!_anim.HasAnimation(animName)) {
      GD.PushWarning($"{Name}: missing animation \"{animName}\" on {_anim.GetPath()}, stopping instead.");
      _anim.Stop();
      return;
    }

    if (_anim.CurrentAnimation != animName || !_anim.IsPlaying()) {
      _anim.Play(animName);
      var clip = _anim.GetAnimation(animName);
      if (clip.LoopMode != Animation.LoopModeEnum.None && TryGetCachedUnitId(out var unitId)) {
        var phase = unchecked((uint)unitId * 2654435761u) / 4294967296.0;
        _anim.Seek(phase * clip.Length, true);
      }
    }
  }

  public bool OnAttackWindupVfx(Vector3 targetPosition, float windupSeconds) {
    if (!HasAttackAnim) return false;

    _isAttacking = true;
    _anim.SpeedScale = AttackPlaybackSpeed.For(AttackContactTime, windupSeconds);
    _anim.Play(AttackAnimationOverride);
    _anim.Seek(0.0, true); // Restart an already-running clip.
    return true;
  }

  public void OnAttackCanceledVfx() {
    if (!_isAttacking) return;
    _isAttacking = false;
    ReturnToLocomotion();
  }

  // Restore locomotion after the one-shot attack.
  private void OnAnimationFinished(StringName animName) {
    if (!_isAttacking || (string)animName != AttackAnimationOverride) return;
    _isAttacking = false;
    ReturnToLocomotion();
  }

  private void ReturnToLocomotion() {
    _anim.SpeedScale = 1.0f;
    PlayOrStop(_isMoving ? RunAnim : IdleAnim);
  }

  public void OnHitVfx(float damage, Vector3 attackerPosition) {
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);

    _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
    if (_anim != null) {
      if (_anim.HasAnimation(RunAnim)) {
        var runAnim = _anim.GetAnimation(RunAnim);
        runAnim.LoopMode = Animation.LoopModeEnum.Linear;
      }
      if (_anim.HasAnimation(IdleAnim)) {
        var idleAnim = _anim.GetAnimation(IdleAnim);
        idleAnim.LoopMode = Animation.LoopModeEnum.Linear;
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
    _isAttacking = false;
    var live = frame.Frame;
    if (live != null && live.Has<UnitIdentity>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<UnitIdentity>(EntityRef).UnitId);
    BindTeam(frame);
    if (_anim != null) ReturnToLocomotion();
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    ClearTeam();
    _isMoving = false;
    _isAttacking = false;
    _anim?.Stop();
  }

  public override void OnUpdateView() {
    if (Engine == null || _anim == null) return;
    var frame = Engine.PredictedFrame.Frame;
    if (frame == null) return;

    var moving = frame.Has<NavAgentComponent>(EntityRef)
      ? frame.GetReadOnly<NavAgentComponent>(EntityRef).Velocity.sqrMagnitude > FP64.Zero
      : frame.Has<UnitMoveTarget>(EntityRef);
    if (moving == _isMoving) return;
    _isMoving = moving;
    _isAttacking = false; // Movement interrupts the attack clip.
    ReturnToLocomotion();
  }
}
