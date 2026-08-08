using Meesles.Avalon.Sim;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon;

public interface IViewHud {
  void SetLocalPlayerId(int? playerId);
  void HideResult();
  void SyncFromFrame(Frame frame);
  void ShowResult(MatchResult result);
}
