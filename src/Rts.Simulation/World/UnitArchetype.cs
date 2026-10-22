using Rts.Simulation.Map;

namespace Rts.Simulation.World;

/// <summary>
/// Movement and sight data of one unit type: rules, not state. Made from the unit's definition in the data
/// (<see cref="Rules.UnitDef"/>) by <see cref="Rules.GameRules"/>, with the enums the simulation reads every tick.
/// </summary>
/// <param name="Id">Data id of the type, such as <c>heavy_tank</c>.</param>
/// <param name="Speed">Movement speed, leptons per tick.</param>
/// <param name="TurnRate">Turn speed, <c>BAngle</c> units per tick; 0 turns instantly, like RA2 infantry.</param>
/// <param name="Sight">Base sight radius in cells; height adds to it (see <c>Visibility.SightRules</c>).</param>
public sealed record UnitArchetype(
    UnitType Type,
    string Id,
    UnitCategory Category,
    Locomotor Locomotor,
    UnitPlacement Placement,
    int Speed,
    int TurnRate,
    bool HasTurret,
    int Sight);
