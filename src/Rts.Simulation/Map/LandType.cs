namespace Rts.Simulation.Map;

/// <summary>
/// Logical land type of a cell: what a unit drives on, not how it looks (docs/implementation/M7_maps.md, 4.3;
/// docs/map_elements/02_land_types). Values are part of the state hash and of map files: never renumber.
/// </summary>
public enum LandType : byte
{
    Clear = 1,
    Rough = 2,
    Road = 3,
    Beach = 4,
    Shallow = 5,
    Deep = 6,
    Rock = 7,
    Ice = 8,
    Track = 9,
    Path = 10,
}
