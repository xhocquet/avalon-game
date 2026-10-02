namespace Meesles.Avalon.Sim.Commands;

// Klotho command wire ids. Keep assigned ids stable and do not reuse the gaps.
public static class CommandTypeIds {
  public const int Move = 100;
  public const int Attack = 103;
  public const int SelectFaction = 104;
  public const int PurchaseItem = 106;
  public const int UpgradeSkill = 107;
  public const int CastSkill = 108;
  public const int SetCheat = 109;
  public const int Debug = 110;

  // Next free id: 111
}
