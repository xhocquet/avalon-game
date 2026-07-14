using System.Runtime.InteropServices;
using xpTURN.Klotho.ECS;

namespace Meesles.Avalon.Sim.Models;

// Owns every player's attributes (Strength today; more to follow). Values only change via
// ModifyStatCommand - there is no passive accrual like Inventory's gold ticking.
[KlothoComponent(119)]
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public partial struct Stats : IComponent {
  public int Strength;
}
