namespace Rts.Simulation.Commands;

/// <summary>
/// Command type tag in the binary format. Values are part of the wire and replay format: never renumber.
/// </summary>
public enum CommandType : byte
{
    SpawnTestUnit = 1,
    Move = 2,
    Stop = 3,
    AttackMove = 4,
    Waypoints = 5,
    Scatter = 6,
    DamageMapObject = 7,
    PlaceBuilding = 8,
    Deploy = 9,
}
