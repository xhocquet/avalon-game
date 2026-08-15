using Godot;
using Meesles.Avalon.Sim.Components;
using xpTURN.Klotho.Core;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Client.Scripts.View;

public partial class MinionEntity : TeamEntityViewNode, IAttackableView {
  private const string UnitsGroup = "units";
  private const string AnimRun = "Run";
  private const string AnimIdle = "Stand";
  // private static readonly Quaternion FlipY = new(Vector3.Up, Mathf.Pi);

  [Export] public string WalkAnimationOverride { get; set; } = "";
  [Export] public string IdleAnimationOverride { get; set; } = "";
  // No default: rigs without an authored attack clip leave this empty and fall back to debug lines.
  [Export] public string AttackAnimationOverride { get; set; } = "";

  [Export] public float SelectPickRadius { get; set; } = 0.5f;
  [Export] public float SelectPickHeight { get; set; } = 1.2f;

  private AnimationPlayer _anim;
  private bool _isMoving;
  private bool _isAttacking;

  private string RunAnim => string.IsNullOrEmpty(WalkAnimationOverride) ? AnimRun : WalkAnimationOverride;
  private string IdleAnim => string.IsNullOrEmpty(IdleAnimationOverride) ? AnimIdle : IdleAnimationOverride;
  private bool HasAttackAnim => _anim != null && !string.IsNullOrEmpty(AttackAnimationOverride)
                                && _anim.HasAnimation(AttackAnimationOverride);

  // Play() is a silent no-op when the name isn't on this model's AnimationPlayer (e.g. a rig with only
  // a single custom-named clip), which would otherwise leave whatever animation last played stuck looping.
  private void PlayOrStop(string animName) {
    if (!_anim.HasAnimation(animName)) {
      GD.PushWarning($"{Name}: missing animation \"{animName}\" on {_anim.GetPath()}, stopping instead.");
      _anim.Stop();
      return;
    }

    if (_anim.CurrentAnimation != animName) {
      _anim.Play(animName);
    }
  }

  public bool OnAttackVfx(Vector3 targetPosition) {
    if (!HasAttackAnim) return false;

    _isAttacking = true;
    _anim.Play(AttackAnimationOverride);
    _anim.Seek(0.0, true); // Play() on the clip already running is a no-op, so rewind to restart it.
    return true;
  }

  // The attack clip is one-shot and owns the rig until it ends; hand control back to locomotion.
  private void OnAnimationFinished(StringName animName) {
    if (!_isAttacking || (string)animName != AttackAnimationOverride) return;
    _isAttacking = false;
    PlayOrStop(_isMoving ? RunAnim : IdleAnim);
  }

  public void OnHitVfx(float damage, Vector3 attackerPosition) {
    // TODO: hit reaction / particles
  }

  public override void OnInitialize() {
    EntityViewPhysics.DisableGodotCollision(this);
    EntityViewPhysics.AddSelectionCollider(this, SelectPickRadius, SelectPickHeight);

    _anim = GetNodeOrNull<AnimationPlayer>("AnimationPlayer");
    if (_anim != null) {
      var runAnim = _anim.GetAnimation(RunAnim);
      if (runAnim != null)
        runAnim.LoopMode = Animation.LoopModeEnum.Linear;
      var idleAnim = _anim.GetAnimation(IdleAnim);
      if (idleAnim != null)
        idleAnim.LoopMode = Animation.LoopModeEnum.Linear;

      if (HasAttackAnim) {
        // Must not loop, or AnimationFinished never fires and the minion is stuck swinging.
        _anim.GetAnimation(AttackAnimationOverride).LoopMode = Animation.LoopModeEnum.None;
        _anim.AnimationFinished += OnAnimationFinished;
      }
    }
  }

  public override void OnActivate(FrameRef frame) {
    AddToGroup(UnitsGroup);
    _isMoving = false;
    _isAttacking = false;
    if (_anim != null) PlayOrStop(IdleAnim);

    var live = frame.Frame;
    if (live != null && live.Has<UnitIdComponent>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<UnitIdComponent>(EntityRef).UnitId);
    BindTeam(frame);
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    ClearTeam();
    _isMoving = false;
    _isAttacking = false;
  }

  public override void OnUpdateView() {
    if (Engine == null || _anim == null) return;
    var frame = Engine.PredictedFrame.Frame;
    if (frame == null) return;

    var moving = frame.Has<UnitMoveTarget>(EntityRef);
    if (moving == _isMoving) return;
    _isMoving = moving;
    _isAttacking = false; // Locomotion changes cut the attack clip short.
    PlayOrStop(_isMoving ? RunAnim : IdleAnim);
  }

  // public override void OnLateUpdateView() {
  //   Quaternion *= FlipY;
  // }
}
