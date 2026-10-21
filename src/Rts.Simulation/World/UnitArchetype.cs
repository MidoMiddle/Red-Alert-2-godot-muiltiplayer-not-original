using Rts.Simulation.Map;

namespace Rts.Simulation.World;

/// <summary>
/// Movement data of one unit type: rules, not state. Speed and turn rate are in RA2 units here and converted
/// once by <see cref="UnitArchetypes"/>, so the numbers can be checked against docs/ra2_units directly.
/// </summary>
/// <param name="Id">Data id shared with the YAML files of M3 (docs/implementation/M3_base_economy.md, 4.6).</param>
/// <param name="Ra2Speed">The <c>Speed</c> of the RA2 counterpart.</param>
/// <param name="Ra2Rot">The <c>ROT</c> of the RA2 counterpart; 0 means the unit turns instantly, like RA2 infantry.</param>
public sealed record UnitArchetype(
    UnitType Type,
    string Id,
    UnitCategory Category,
    Locomotor Locomotor,
    UnitPlacement Placement,
    int Ra2Speed,
    int Ra2Rot,
    bool HasTurret)
{
    /// <summary>Movement speed, leptons per tick.</summary>
    public int Speed => UnitArchetypes.SpeedFromRa2(Ra2Speed);

    /// <summary>Turn speed, <c>BAngle</c> units per tick; 0 turns instantly.</summary>
    public int TurnRate => UnitArchetypes.TurnRateFromRa2(Ra2Rot);
}
