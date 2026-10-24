namespace Rts.Simulation.World;

public enum UnitCategory : byte
{
    Infantry = 1,
    Vehicle = 2,

    /// <summary>A ship or a boat: built by the shipyard (stage 2a).</summary>
    Naval = 3,
}
