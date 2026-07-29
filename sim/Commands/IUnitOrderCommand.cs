using xpTURN.Klotho.Core;

namespace Meesles.Avalon.Sim.Commands;

// An order issued against a player's current selection. CommandSystem resolves UnitIds against the
// issuing player's team once for every implementation, so a new order type only has to describe
// what it does with the units it gets back.
public interface IUnitOrderCommand : ICommand {
  UnitIdList UnitIds { get; }
}
