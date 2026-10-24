namespace Meesles.Avalon.Client.Scripts.GameState;

public static class PlayerProfile {
  private static readonly string[] Names = [
    "MossGoblin", "SoupWizard", "SirBonks", "PocketDragon",
    "WobblyKnight", "BogEnjoyer", "TinyMenace", "BreadPaladin",
    "SleepyMimic", "FrogBaron", "LootGremlin", "MildlyCursed",
    "SneakyTurnip", "GravyMage", "OopsAllGoblins", "CrumbCollector",
    "BonkAppetit", "SnailWarlord", "MushroomMayor", "RustySpoon"
  ];

  public static readonly string DefaultName = Names[System.Random.Shared.Next(Names.Length)];
  public static string PlayerName = DefaultName;
}
