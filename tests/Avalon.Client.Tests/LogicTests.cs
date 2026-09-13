using GdUnit4;
using Meesles.Avalon.Client.Scripts.View;
using Meesles.Avalon.Sim;
using static GdUnit4.Assertions;

namespace Meesles.Avalon.Client.Tests;

[TestSuite]
public class LogicTests {
  [TestCase]
  public void MatchResultText_UsesTheLocalPlayersTeamForTheHeadline() {
    var result = new MatchResult {
      WinnerTeamId = 2,
      Reason = MatchEndReason.Crystal,
      DurationMs = 125_000,
      Players = [
        new PlayerResult { PlayerId = 1, TeamId = 1 },
        new PlayerResult { PlayerId = 2, TeamId = 2 }
      ]
    };

    AssertThat(MatchResultText.Headline(result, localPlayerId: 2)).IsEqual("Victory");
    AssertThat(MatchResultText.Headline(result, localPlayerId: 1)).IsEqual("Defeat");
    AssertThat(MatchResultText.Summary(result, localPlayerId: 2))
      .IsEqual("Victory\nCrystal destroyed  ·  02:05");
  }

  [TestCase]
  public void AttackPlaybackSpeed_UsesFallbackAndClampsExtremeValues() {
    AssertThat(AttackPlaybackSpeed.For(contactTime: 0.0f, windupSeconds: 0.5f)).IsEqual(1.0f);
    AssertThat(AttackPlaybackSpeed.For(contactTime: 0.05f, windupSeconds: 1.0f)).IsEqual(0.25f);
    AssertThat(AttackPlaybackSpeed.For(contactTime: 1.0f, windupSeconds: 0.5f)).IsEqual(2.0f);
    AssertThat(AttackPlaybackSpeed.For(contactTime: 5.0f, windupSeconds: 0.5f)).IsEqual(4.0f);
  }
}
