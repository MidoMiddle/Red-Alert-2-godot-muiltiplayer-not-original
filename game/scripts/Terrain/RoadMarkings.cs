using Rts.Client.Climates;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// The markings of the asphalt for the ground shader (docs/map_elements/04_roads, the owner, 2026-10-30: an asphalt road has its
/// markings, a wide one a narrow green strip down its middle, as in RA2), Godot-free: four bytes per cell. Only a cell of road or
/// highway in asphalt or highway (not a street, cobbles or pavers) is marked.
/// <list type="bullet">
/// <item><see cref="Line"/>: a road one cell wide (any cell of road, not highway, that is no band) — a dashed line from the centre of the cell towards each road cell it runs on to
/// (byte 1, a bit a neighbour, <see cref="Directions"/>), straight, round a bend or across a corner. A diagonal link the two
/// cells also share through a cell beside both is left out, so a staircase of cells gives one line, not triangles. A cell with
/// more than two links (a crossing, a square) has none.</item>
/// <item><see cref="Band"/>: a band 2 to 6 cells across and at least 4 long — along X (byte 1 = 0) or along Y (1), the cell's
/// place across it from its north or west edge (byte 2) and the band's width (byte 3). The shader draws solid lines along both
/// edges, and on a band of 3 or more a green strip down the middle with a dashed line in each half; on a band of 2 a dashed line
/// between the lanes.</item>
/// </list>
/// Every other cell of that asphalt is <see cref="Plain"/>. Only a picture: the rules never see it.
/// </summary>
public static class RoadMarkings
{
    public const int BytesPerCell = 4;
    public const byte Line = 1;
    public const byte Band = 2;

    /// <summary>Asphalt without markings: a crossing, a square, a ragged end of a highway — lit smooth like the rest of the asphalt.</summary>
    public const byte Plain = 3;

    /// <summary>The neighbours by bit, from bit 0: east, south-east, south, south-west, west, north-west, north, north-east (y south).</summary>
    public static readonly (int Dx, int Dy)[] Directions = [(1, 0), (1, 1), (0, 1), (-1, 1), (-1, 0), (-1, -1), (0, -1), (1, -1)];

    private const int MinBand = 2;
    private const int MaxBand = 6;
    private const int MinLength = 4;

    public static byte[] Build(MapLook look, MapGrid map)
    {
        if (look.Width != map.Width || look.Height != map.Height)
        {
            look = MapLook.FromLand(map);
        }

        var (width, height) = (map.Width, map.Height);
        var marked = new bool[width * height];
        for (var i = 0; i < marked.Length; i++)
        {
            var land = map.LandAt(new CellPos(i % width, i / width));
            marked[i] = land is LandType.Road or LandType.Highway && look.MaterialAt(i) is null or "asphalt" or "highway";
        }

        bool On(int x, int y) => x >= 0 && y >= 0 && x < width && y < height && marked[y * width + x];

        int Run(int x, int y, int dx, int dy)
        {
            var count = 0;
            while (On(x + dx * (count + 1), y + dy * (count + 1)))
            {
                count++;
            }

            return count;
        }

        var cells = new byte[width * height * BytesPerCell];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                if (!On(x, y))
                {
                    continue;
                }

                var at = (y * width + x) * BytesPerCell;
                cells[at] = Plain;
                var (west, east) = (Run(x, y, -1, 0), Run(x, y, 1, 0));
                var (north, south) = (Run(x, y, 0, -1), Run(x, y, 0, 1));
                var (alongX, alongY) = (1 + west + east, 1 + north + south);
                var across = System.Math.Min(alongX, alongY);
                var byX = alongX >= alongY;

                // As wide as the band beside it along the axis too: the corner of a bend of a narrow road is no band.
                int Across(int cx, int cy) => byX ? 1 + Run(cx, cy, 0, -1) + Run(cx, cy, 0, 1) : 1 + Run(cx, cy, -1, 0) + Run(cx, cy, 1, 0);
                var (nextX, nextY) = byX ? (west > 0 ? x - 1 : x + 1, y) : (x, north > 0 ? y - 1 : y + 1);
                if (across is >= MinBand and <= MaxBand && System.Math.Max(alongX, alongY) >= MinLength && Across(nextX, nextY) >= MinBand)
                {
                    cells[at] = Band;
                    cells[at + 1] = (byte)(byX ? 0 : 1);
                    cells[at + 2] = (byte)(byX ? north : west);
                    cells[at + 3] = (byte)across;
                    continue;
                }

                // A cell of highway that is no band is a ragged end of one: no line of its own.
                if (map.LandAt(new CellPos(x, y)) == LandType.Highway)
                {
                    continue;
                }

                var links = 0;
                var count = 0;
                for (var bit = 0; bit < Directions.Length; bit++)
                {
                    var (dx, dy) = Directions[bit];
                    if (On(x + dx, y + dy) && (dx == 0 || dy == 0 || (!On(x + dx, y) && !On(x, y + dy))))
                    {
                        links |= 1 << bit;
                        count++;
                    }
                }

                if (count is >= 1 and <= 2)
                {
                    cells[at] = Line;
                    cells[at + 1] = (byte)links;
                }
            }
        }

        return cells;
    }
}
