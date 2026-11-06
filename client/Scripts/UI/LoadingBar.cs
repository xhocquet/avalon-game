using Godot;

namespace Meesles.Avalon;

public partial class LoadingBar : ProgressBar {
  private Label _caption;

  public override void _Ready() {
    _caption = GetNode<Label>("Caption");
  }

  public void SetProgress(double fraction, string caption) {
    Value = Mathf.Clamp(fraction, 0.0, 1.0);
    _caption.Text = caption;
  }
}
