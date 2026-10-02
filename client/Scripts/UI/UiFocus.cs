using Godot;

namespace Meesles.Avalon;

// `_Input` runs before GUI focus; check this before handling character hotkeys
public static class UiFocus {
  public static bool IsTypingInTextField(Viewport viewport) {
    return viewport?.GuiGetFocusOwner() is LineEdit or TextEdit;
  }
}
