using xpTURN.Klotho.Core;
using xpTURN.Klotho.Serialization;

namespace Meesles.Avalon.Sim.Commands {
  // Sent once per player at match start to carry their lobby faction pick into the
  // deterministic sim. CommandSystem records it as a PlayerFaction entity; HeroSpawnSystem
  // then spawns the player's hero with the matching faction. FactionId == FactionAsset AssetId.
  [KlothoSerializable(104)]
  public partial class SelectFactionCommand : CommandBase {
    public override bool IsContinuousInput => false;

    public int FactionId;

    // 12 header + 4 faction id
    public override int GetSerializedSize() => 16;

    protected override void SerializeData(ref SpanWriter writer) {
      writer.WriteInt32(FactionId);
    }

    protected override void DeserializeData(ref SpanReader reader) {
      FactionId = reader.ReadInt32();
    }
  }
}
