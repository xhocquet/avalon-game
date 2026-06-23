using xpTURN.Klotho.ECS;

namespace Meesles.Avalon {
  public interface IViewHud {
    void SetLocalPlayerId(int? playerId);
    void HideResult();
    void SyncFromFrame(Frame frame);
    void ShowResult(string text);
  }
}
