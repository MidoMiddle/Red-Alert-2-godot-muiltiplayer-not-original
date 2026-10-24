using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;

namespace Rts.MapGen.Stages;

/// <summary>
/// Stage 5, ore (docs/implementation/M7_maps.md, 4.14): a home field of radius <see cref="HomeRadius"/> five cells from the
/// first start towards the centre, and a contested field halfway between it and the centre; both copied to every image, so
/// every start has exactly as much. Not on water, rock or cliffs. Density by the resources setting.
/// </summary>
internal static class Ore
{
    public const int HomeRadius = 3;
    public const int ContestedRadius = 2;

    /// <summary>The centres of the home and the contested field of <paramref name="start"/> on a map of side <paramref name="n"/>.</summary>
    public static (CellPos Home, CellPos Contested) Centres(CellPos start, int n)
    {
        // Towards the centre, in doubled coordinates so a start on either side of it gets the mirrored direction.
        var (dx, dy) = (IntMath.Sign(n - (2 * start.X + 1)), IntMath.Sign(n - (2 * start.Y + 1)));
        return (new CellPos(start.X + dx * 5, start.Y + dy * 5), new CellPos((start.X + n / 2) / 2, (start.Y + n / 2) / 2));
    }

    public static void Place(GenMap map, out CellPos contested)
    {
        (var home, contested) = Centres(map.Starts[0].Cell, map.N);
        var value = map.Settings.Resources switch { Amount.Low => 3, Amount.Medium => 5, _ => 8 };
        Field(map, home, HomeRadius, value);
        Field(map, contested, ContestedRadius, IntMath.Min(value + 1, 9));
    }

    private static void Field(GenMap map, CellPos centre, int radius, int value)
    {
        for (var y = centre.Y - radius; y <= centre.Y + radius; y++)
        {
            for (var x = centre.X - radius; x <= centre.X + radius; x++)
            {
                var (dx, dy) = (x - centre.X, y - centre.Y);
                var cell = new CellPos(x, y);
                if (dx * dx + dy * dy > radius * radius || !map.Contains(cell))
                {
                    continue;
                }

                for (var k = 0; k < map.Symmetry.Order; k++)
                {
                    var image = map.Symmetry.Cell(k, cell);
                    if (!map.IsWater(image) && map.LandAt(image) != LandType.Rock && !map.IsCliff(image))
                    {
                        map.Ore[image.Y * map.N + image.X] = (byte)value;
                    }
                }
            }
        }
    }
}
