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
    Produce = 10,
    PauseProduction = 11,
    CancelProduction = 12,
    SetRallyPoint = 13,
    Harvest = 14,
    Sell = 15,
    ToggleRepair = 16,
    PlaceWalls = 17,
}
