namespace Rts.Simulation.Map;

/// <summary>
/// How a unit moves over the ground (docs/implementation/M2_unit_control.md, 4.4). Decides where it can go
/// and how fast on each terrain type; the passability tables come with the map in M2 step 3.
/// Values are part of the rules: never renumber.
/// </summary>
public enum Locomotor : byte
{
    Foot = 1,
    Track = 2,
    Wheel = 3,
}
