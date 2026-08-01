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

  [Export] public float SelectPickRadius { get; set; } = 0.5f;
  [Export] public float SelectPickHeight { get; set; } = 1.2f;

  private AnimationPlayer _anim;
  private bool _isMoving;
  private int _ownerId = -1;

  private string RunAnim => string.IsNullOrEmpty(WalkAnimationOverride) ? AnimRun : WalkAnimationOverride;
  private string IdleAnim => string.IsNullOrEmpty(IdleAnimationOverride) ? AnimIdle : IdleAnimationOverride;

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

  public void OnAttackVfx(Vector3 targetPosition) {
    // TODO: attack animation / particles
  }

  public void OnHitVfx(int damage, Vector3 attackerPosition) {
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
    }
  }

  public override void OnActivate(FrameRef frame) {
    AddToGroup(UnitsGroup);
    _isMoving = false;
    if (_anim != null) PlayOrStop(IdleAnim);

    var live = frame.Frame;
    if (live != null && live.Has<Unit>(EntityRef))
      SetCachedUnitId(live.GetReadOnly<Unit>(EntityRef).UnitId);
    if (live != null && live.Has<OwnerComponent>(EntityRef))
      _ownerId = live.GetReadOnly<OwnerComponent>(EntityRef).OwnerId;
    BindTeam(frame);
  }

  public override void OnDeactivate() {
    RemoveFromGroup(UnitsGroup);
    _ownerId = -1;
    ClearTeam();
    _isMoving = false;
  }

  public override void OnUpdateView() {
    if (Engine == null || _anim == null) return;
    var frame = Engine.PredictedFrame.Frame;
    if (frame == null) return;

    var moving = frame.Has<UnitMoveTarget>(EntityRef);
    if (moving == _isMoving) return;
    _isMoving = moving;
    PlayOrStop(_isMoving ? RunAnim : IdleAnim);
  }

  // public override void OnLateUpdateView() {
  //   Quaternion *= FlipY;
  // }

  public override bool OwnerMatches(int ownerId) {
    return _ownerId == ownerId;
  }
}
