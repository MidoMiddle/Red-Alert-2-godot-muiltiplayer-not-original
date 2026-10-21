using System.Collections.Immutable;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.MapTool;

/// <summary>
/// The relief test map of M7 step 2 (docs/implementation/M7_maps.md, 4.1), 24 x 24, committed as
/// game/data/maps/relief_test.rtsmap; a test checks the file against this generator. On it: a level-4 plateau with ramps on
/// two sides and cliff walls elsewhere, a level-2 shelf with cliff edges, a stepped hill up to level 6, a pit sloping down to
/// a flat level-0 bottom inside level-2 ground (which a ramp joins to the south edge), and a corner ramp (one raised corner).
/// Cliff cells get the rock texture, everything else snow. Starts (step 4): slot 1 on the flat strip north of the hill, slot
/// 2 on the flat bottom of the pit.
/// </summary>
public static class ReliefTestMap
{
    public const int Size = 24;

    public static readonly CellPos PlateauCell = new(4, 4);
    public static readonly CellPos EastRampCell = new(9, 4);
    public static readonly CellPos SouthRampCell = new(5, 9);
    public static readonly CellPos PlateauWallCell = new(4, 1);
    public static readonly CellPos ShelfCell = new(18, 4);
    public static readonly CellPos ShelfEdgeCell = new(13, 4);
    public static readonly CellPos HillTopCell = new(17, 16);
    public static readonly CellPos PitCell = new(5, 17);
    public static readonly CellPos PitRampCell = new(5, 23);
    public static readonly CellPos CornerRampCell = new(11, 1);
    public static readonly CellPos BlueStart = new(13, 8);
    public static readonly CellPos RedStart = new(4, 16);

    public static MapDefinition Build()
    {
        var corners = new byte[(Size + 1) * (Size + 1)];
        void Set(int x, int y, int level) => corners[y * (Size + 1) + x] = (byte)level;

        for (var y = 0; y <= Size; y++)
        {
            for (var x = 0; x <= Size; x++)
            {
                var level = 0;

                // Plateau, level 4, corners 2…8.
                if (x is >= 2 and <= 8 && y is >= 2 and <= 8)
                {
                    level = 4;
                }

                // East ramp: from the plateau down one level per cell, two cells wide.
                if (y is >= 4 and <= 6 && x is >= 9 and <= 11)
                {
                    level = 4 - (x - 8);
                }

                // South ramp, three cells wide.
                if (x is >= 4 and <= 7 && y is >= 9 and <= 11)
                {
                    level = 4 - (y - 8);
                }

                // Shelf, level 2, with cliff edges all round.
                if (x is >= 14 and <= 22 && y is >= 2 and <= 7)
                {
                    level = 2;
                }

                // Stepped hill, one level per cell up to 6.
                var hill = 6 - System.Math.Max(System.Math.Abs(x - 17), System.Math.Abs(y - 16));
                level = System.Math.Max(level, hill);

                // Pit: level 2 ground sloping down to a flat level-0 bottom of 2 x 2 cells.
                if (x is >= 1 and <= 10 && y is >= 13 and <= 22)
                {
                    level = System.Math.Min(2, System.Math.Max(0, System.Math.Max(System.Math.Abs(x - 5), System.Math.Abs(y - 17)) - 1));
                }

                // A ramp from the pit ground down to the south edge, three cells wide.
                if (x is >= 4 and <= 7 && y >= 23)
                {
                    level = 2 - (y - 22);
                }

                Set(x, y, level);
            }
        }

        // One raised corner on open ground: the four cells around it become corner ramps.
        Set(11, 1, 1);

        var heights = CornerHeightGrid.FromLayer(Size, Size, corners);
        var texture = new char[Size * Size];
        for (var y = 0; y < Size; y++)
        {
            for (var x = 0; x < Size; x++)
            {
                texture[y * Size + x] = heights.ShapeAt(new CellPos(x, y)) == CellShape.Cliff ? 'k' : 's';
            }
        }

        return new MapDefinition(
            "relief-test",
            "Relief test",
            "M7 step 2",
            "Plateau with ramps, cliff shelf, stepped hill, pit and a corner ramp.",
            Size,
            Size,
            "winter",
            2,
            [new MapStart(1, BlueStart), new MapStart(2, RedStart)],
            [new('k', "rock"), new('s', "snow")],
            [.. corners],
            [.. Enumerable.Repeat(LandType.Clear, Size * Size)],
            [.. texture],
            [.. new byte[Size * Size]],
            [],
            [],
            []);
    }
}
