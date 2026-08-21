using Godot;

namespace Meesles.Avalon.Client.Scripts.View;

// A focused text field owns the keyboard. Global hotkeys live in `_Input` handlers, which run ahead of
// the viewport's GUI focus pass, so a hotkey bound to an ordinary character is consumed before the
// field can type it — Space is `focus_player`, Tab toggles the scoreboard. Any `_Input` that claims a
// key should ask this first.
public static class UiFocus {
  public static bool IsTypingInTextField(Viewport viewport) {
    return viewport?.GuiGetFocusOwner() is LineEdit or TextEdit;
  }
}
