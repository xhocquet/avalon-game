namespace Meesles.Avalon.Sim.Components;

// Uniform access to the counter field so IdCounter<T> can drive every id sequence.
public interface IIdCounter {
  int NextId { get; set; }
}
