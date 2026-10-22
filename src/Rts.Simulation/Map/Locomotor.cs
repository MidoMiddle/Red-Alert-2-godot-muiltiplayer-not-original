namespace Rts.Simulation.Map;

/// <summary>
/// How a unit moves over the ground (docs/implementation/M2_unit_control.md, 4.3; docs/implementation/M7_maps.md, 4.3). Decides
/// where it can go and how fast on each land type, see <see cref="LandSpeeds"/>. Values are part of the rules: never
/// renumber. Amphibious, naval and hover units come in M11; the types are in the tables already.
/// </summary>
public enum Locomotor : byte
{
    Foot = 1,
    Track = 2,
    Wheel = 3,
    Amphibious = 4,
    Naval = 5,
    Hover = 6,
}
