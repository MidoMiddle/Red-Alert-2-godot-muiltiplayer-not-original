namespace Rts.Simulation.Commands;

/// <summary>
/// Command type tag in the binary format. Values are part of the wire and replay format: never renumber.
/// </summary>
public enum CommandType : byte
{
    SpawnTestUnit = 1,
    MoveUnits = 2,
}
