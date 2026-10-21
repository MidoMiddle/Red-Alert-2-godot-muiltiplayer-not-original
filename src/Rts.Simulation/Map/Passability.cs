using Rts.Simulation.Geometry;

namespace Rts.Simulation.Map;

/// <summary>
/// Speed of every locomotor on every cell, in percent, 0 where it cannot go (docs/implementation/M7_maps.md, 4.4): the cell's
/// shape (a cliff closes it), then its land, then a slope, then an obstacle on it that stops this mover (a fence lets tracks in). Built with the map and kept up to date
/// cell by cell when the map changes, so movement and path searches read one byte instead of working it out. Derived from the
/// map layers: not part of the state hash.
/// </summary>
internal sealed class Passability
{
    private readonly byte[] _percent;
    private readonly int _cells;

    public Passability(MapGrid map)
    {
        _cells = map.Width * map.Height;
        _percent = new byte[LocomotorRules.Count * _cells];
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                Update(map, new CellPos(x, y));
            }
        }
    }

    public int Percent(int cellIndex, Locomotor locomotor) => _percent[((int)locomotor - 1) * _cells + cellIndex];

    /// <summary>Works the cell out again for every locomotor.</summary>
    public void Update(MapGrid map, CellPos cell)
    {
        var index = cell.Y * map.Width + cell.X;
        var shape = map.Heights.ShapeAt(cell);
        var obstacles = map.ObstacleMask(cell);
        var land = map.LandAt(cell);
        for (var locomotor = Locomotor.Foot; locomotor <= Locomotor.Hover; locomotor++)
        {
            var blocked = (obstacles & MapObjectTypes.Bit(locomotor)) != 0;
            _percent[((int)locomotor - 1) * _cells + index] = blocked ? (byte)0 : (byte)LocomotorRules.CellPercent(locomotor, land, shape);
        }
    }
}
