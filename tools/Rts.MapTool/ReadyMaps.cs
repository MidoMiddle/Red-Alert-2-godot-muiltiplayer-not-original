using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Data.Maps;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.World;

namespace Rts.MapTool;

/// <summary>
/// The ready maps of M7 step 14 (docs/implementation/M7_maps.md, 4.15), each a script of the editor's own tools
/// (<see cref="MapEditSession"/>) on heights given by a formula, committed as game/data/maps/*.rtsmap; a test checks the files
/// against these scripts. <see cref="MapScript"/> repeats every stroke on the images of the map's symmetry, so every player
/// gets the same ground. Every map passes <see cref="MapValidator"/> without errors (the script throws otherwise).
/// </summary>
public static class ReadyMaps
{
    public static readonly string[] Files = ["frozen-crossing", "twin-rivers", "taiga-front", "four-corners"];

    public static MapDefinition Build(string file) => file switch
    {
        "frozen-crossing" => FrozenCrossing(),
        "twin-rivers" => TwinRivers(),
        "taiga-front" => TaigaFront(),
        "four-corners" => FourCorners(),
        _ => throw new ArgumentException($"No ready map '{file}'."),
    };

    /// <summary>
    /// 1 vs 1, 64 x 64, mirrored. A river down the middle, frozen in its middle third (ice: passable, a little slow), open water
    /// north and south with a bridge over each. A small hill by each base, woods in the corners, roads to the bridges and the
    /// ice, gems at the ice crossing, two houses by the ice.
    /// </summary>
    public static MapDefinition FrozenCrossing()
    {
        int Height(int x, int y) => Math.Max(Cone(x, y, 16, 50, 3), Cone(x, y, 20, 12, 2));

        var map = new MapScript("frozen-crossing", "Frozen Crossing", "A short duel across a frozen river: two bridges, and ice that anyone can cross.",
            64, 64, 2, rotate: false, Height);
        map.Water(new CellPos(31, 0), new CellPos(31, 63), 3, LandType.Deep);
        map.Land(new CellPos(31, 25), new CellPos(31, 38), 3, LandType.Ice);
        map.Bridge(new CellPos(28, 10), new CellPos(35, 10));
        map.Bridge(new CellPos(28, 53), new CellPos(35, 53));
        map.Start(new CellPos(11, 32));
        map.Road(new CellPos(13, 31), new CellPos(20, 24), new CellPos(24, 12), new CellPos(27, 10));
        map.Road(new CellPos(13, 33), new CellPos(20, 40), new CellPos(24, 51), new CellPos(27, 53));
        map.Road(new CellPos(13, 32), new CellPos(27, 32));
        map.Ore(new CellPos(15, 26), 3, 7);
        map.Ore(new CellPos(24, 36), 2, 11);
        map.Building("neutral_house_a", new CellPos(23, 27), 2);
        map.Building("neutral_shed", new CellPos(26, 36), 0);
        map.Land(new CellPos(7, 40), new CellPos(9, 44), 2, LandType.Rough);
        map.Trees(new CellPos(6, 6), 6, 55);
        map.Trees(new CellPos(6, 57), 6, 55);
        map.Trees(new CellPos(20, 18), 3, 40);
        map.Trees(new CellPos(9, 48), 3, 40);
        return map.Finish();
    }

    /// <summary>
    /// 1 vs 1, 96 x 96, mirrored. A river on each half with two bridges each; between the rivers a level-4 plateau with ramps
    /// to the west, north and south and cliffs over the crossings — whoever holds the height sees farther. Contested ore and an
    /// oil derrick on the plateau.
    /// </summary>
    public static MapDefinition TwinRivers()
    {
        // Written for the west half (the script reads every corner at its west image).
        static int Height(int x, int y)
        {
            if (x >= 36 && y >= 28 && y <= 68)
            {
                return 4;
            }

            if (y is >= 45 and <= 51 && x is >= 32 and <= 36)
            {
                return x - 32;
            }

            if (x >= 45 && y is >= 24 and <= 28)
            {
                return y - 24;
            }

            return x >= 45 && y is >= 68 and <= 72 ? 72 - y : 0;
        }

        var map = new MapScript("twin-rivers", "Twin Rivers", "Two rivers and four bridges; the plateau between them watches every crossing.",
            96, 96, 2, rotate: false, Height);
        map.Water(new CellPos(25, 0), new CellPos(25, 95), 2, LandType.Deep);
        map.Bridge(new CellPos(23, 20), new CellPos(27, 20));
        map.Bridge(new CellPos(23, 76), new CellPos(27, 76));
        map.Start(new CellPos(11, 48));
        map.Road(new CellPos(12, 46), new CellPos(18, 30), new CellPos(22, 20));
        map.Road(new CellPos(12, 50), new CellPos(18, 66), new CellPos(22, 76));
        map.Road(new CellPos(28, 20), new CellPos(44, 20), new CellPos(48, 20));
        map.Road(new CellPos(28, 76), new CellPos(44, 76), new CellPos(48, 76));
        map.Ore(new CellPos(16, 40), 3, 7);
        map.Ore(new CellPos(42, 40), 3, 8);
        map.Building("tech_oil_derrick", new CellPos(40, 54), 0);
        map.Trees(new CellPos(7, 8), 6, 55);
        map.Trees(new CellPos(7, 88), 6, 55);
        map.Trees(new CellPos(31, 9), 3, 50);
        map.Trees(new CellPos(31, 87), 3, 50);
        map.Trees(new CellPos(14, 62), 4, 35);
        return map.Finish();
    }

    /// <summary>
    /// 2 vs 2, 128 x 96, mirrored: two bases on each side (slots 1 and 3 west against 2 and 4 east). Dense taiga everywhere but
    /// the clearings of the bases, the roads and the village at the central crossroads (houses, an apartment block, sheds).
    /// Gentle hills.
    /// </summary>
    public static MapDefinition TaigaFront()
    {
        int Height(int x, int y) => Math.Max(Cone(x, y, 34, 48, 3), Math.Max(Cone(x, y, 48, 14, 2), Cone(x, y, 48, 82, 2)));

        var map = new MapScript("taiga-front", "Taiga Front", "Two against two through dense taiga; teams: slots 1 and 3 in the west, 2 and 4 in the east.",
            128, 96, 4, rotate: false, Height);
        map.Start(new CellPos(16, 20));
        map.Start(new CellPos(16, 75));
        map.Road(new CellPos(18, 48), new CellPos(63, 48));
        map.Road(new CellPos(63, 8), new CellPos(63, 87));
        map.Road(new CellPos(16, 23), new CellPos(16, 46));
        map.Road(new CellPos(16, 72), new CellPos(16, 50));
        map.Road(new CellPos(19, 20), new CellPos(40, 20), new CellPos(62, 10));
        map.Road(new CellPos(19, 75), new CellPos(40, 75), new CellPos(62, 85));
        map.Ore(new CellPos(25, 20), 3, 6);
        map.Ore(new CellPos(25, 75), 3, 6);
        map.Ore(new CellPos(46, 36), 2, 8);
        map.Ore(new CellPos(46, 60), 2, 8);
        map.Building("neutral_house_a", new CellPos(58, 43), 1);
        map.Building("neutral_house_a", new CellPos(57, 51), 5);
        map.Building("neutral_apartment", new CellPos(51, 51), 0);
        map.Building("neutral_shed", new CellPos(61, 44), 0);
        map.Building("neutral_shed", new CellPos(55, 44), 3);
        map.Forest(new Rect(0, 0, 64, 96), 50, keepOut: new Rect(48, 39, 16, 19));
        return map.Finish();
    }

    /// <summary>
    /// Four players, 128 x 128, turned by 90°: bases in the corners, a level-4 plateau in the centre with gems and an oil
    /// derrick, ramps on all four sides; a lake and woods in every quarter, home ore, a hospital between each pair of
    /// neighbours.
    /// </summary>
    public static MapDefinition FourCorners()
    {
        static int Height(int x, int y)
        {
            var (ax, ay) = (Math.Abs(x - 64), Math.Abs(y - 64));
            if (Math.Max(ax, ay) <= 14)
            {
                return 4;
            }

            return (ax <= 3, ay <= 3) switch
            {
                (true, _) when y is >= 46 and <= 50 => y - 46,
                (true, _) when y is >= 78 and <= 82 => 82 - y,
                (_, true) when x is >= 46 and <= 50 => x - 46,
                (_, true) when x is >= 78 and <= 82 => 82 - x,
                _ => 0,
            };
        }

        var map = new MapScript("four-corners", "Four Corners", "Four bases in the corners; the plateau in the middle holds the gems.",
            128, 128, 4, rotate: true, Height);
        map.Start(new CellPos(20, 20));
        map.Water(new CellPos(38, 36), new CellPos(42, 40), 4, LandType.Deep);
        map.Road(new CellPos(22, 22), new CellPos(34, 24), new CellPos(50, 30), new CellPos(62, 44));
        map.Ore(new CellPos(27, 13), 3, 7);
        map.Ore(new CellPos(58, 58), 3, 11);
        map.Building("tech_oil_derrick", new CellPos(63, 63), 0);
        map.Building("tech_hospital", new CellPos(62, 8), 0);
        map.Trees(new CellPos(10, 44), 6, 55);
        map.Trees(new CellPos(44, 10), 5, 45);
        map.Trees(new CellPos(30, 52), 4, 40);
        return map.Finish();
    }

    /// <summary>A cone of slopes: <paramref name="peak"/> at corner (<paramref name="cx"/>, <paramref name="cy"/>), one level down every two corners out (no cliffs).</summary>
    private static int Cone(int x, int y, int cx, int cy, int peak) => Math.Max(0, peak - Math.Max(Math.Abs(x - cx), Math.Abs(y - cy)) / 2);
}

/// <summary>A rectangle of cells from (<paramref name="X"/>, <paramref name="Y"/>), <paramref name="Width"/> × <paramref name="Height"/>.</summary>
public readonly record struct Rect(int X, int Y, int Width, int Height)
{
    public bool Contains(CellPos cell) => cell.X >= X && cell.Y >= Y && cell.X < X + Width && cell.Y < Y + Height;
}

/// <summary>
/// Builds a map with the tools of the editor, every stroke repeated on the images of the symmetry: west/east mirror, or quarter
/// turns of a square map. Heights come from a formula read at the canonical corner of every orbit. Lines of strokes are laid
/// out cell by cell before they are mirrored, so the images match exactly.
/// </summary>
public sealed class MapScript
{
    private readonly MapEditSession _session;
    private readonly int _width;
    private readonly int _height;
    private readonly bool _rotate;

    public MapScript(string id, string name, string description, int width, int height, int players, bool rotate, Func<int, int, int> heightAt)
    {
        (_width, _height, _rotate) = (width, height, rotate);
        var levels = new byte[(width + 1) * (height + 1)];
        for (var y = 0; y <= height; y++)
        {
            for (var x = 0; x <= width; x++)
            {
                var (cx, cy) = CanonicalCorner(x, y);
                levels[y * (width + 1) + x] = (byte)Math.Clamp(heightAt(cx, cy), 0, CornerHeightGrid.MaxLevel);
            }
        }

        var cells = width * height;
        var definition = new MapDefinition(
            id, name, "Ready maps (M7)", description, width, height, "winter", players, [], TestMapConverter.Legend,
            ImmutableArray.Create(levels), [.. Enumerable.Repeat(LandType.Clear, cells)], [.. Enumerable.Repeat('s', cells)],
            ImmutableArray.Create(new byte[cells]), [], [], []);
        _session = new MapEditSession(EditableMap.FromDefinition(definition));
    }

    private int Order => _rotate ? 4 : 2;

    public void Water(CellPos from, CellPos to, int size, LandType depth) =>
        Strokes(EditTool.Water, new Brush(BrushShape.Square, size), new EditOptions { Water = depth }, Line(from, to));

    public void Land(CellPos from, CellPos to, int size, LandType land) =>
        Strokes(EditTool.Land, new Brush(BrushShape.Square, size), new EditOptions { Land = land }, Line(from, to));

    public void Road(params CellPos[] points)
    {
        var cells = new List<CellPos>();
        for (var i = 1; i < points.Length; i++)
        {
            cells.AddRange(Line(points[i - 1], points[i]));
        }

        Strokes(EditTool.Road, new Brush(BrushShape.Square, 1), null, cells);
    }

    public void Ore(CellPos centre, int size, int value) =>
        Strokes(EditTool.Ore, new Brush(BrushShape.Circle, size), new EditOptions { Ore = value }, [centre]);

    /// <summary>Press on one shore, let go on the other.</summary>
    public void Bridge(CellPos from, CellPos to) => Strokes(EditTool.Bridge, new Brush(BrushShape.Circle, 1), null, [from, to]);

    /// <summary>A start and its images: slots in the order of the images.</summary>
    public void Start(CellPos cell) => Strokes(EditTool.Start, new Brush(BrushShape.Circle, 1), null, [cell]);

    /// <summary>A building with its north-west cell here, and its images (turned footprints start at their own north-west cell).</summary>
    public void Building(string type, CellPos corner, int facing)
    {
        BuildingTypes.TryGetById(type, out var kind);
        var placed = new List<CellPos>();
        for (var k = 0; k < Order; k++)
        {
            var (minX, minY) = (int.MaxValue, int.MaxValue);
            for (var y = 0; y < kind.Height; y++)
            {
                for (var x = 0; x < kind.Width; x++)
                {
                    var image = Cell(k, new CellPos(corner.X + x, corner.Y + y));
                    (minX, minY) = (Math.Min(minX, image.X), Math.Min(minY, image.Y));
                }
            }

            var at = new CellPos(minX, minY);
            if (placed.Contains(at))
            {
                continue;
            }

            placed.Add(at);
            Stroke(EditTool.Building, new Brush(BrushShape.Circle, 1), new EditOptions { BuildingType = type, Facing = facing }, [at]);
        }
    }

    /// <summary>Pines on <paramref name="density"/> % of the clear cells within <paramref name="radius"/>, and on their images.</summary>
    public void Trees(CellPos centre, int radius, int density)
    {
        for (var y = centre.Y - radius; y <= centre.Y + radius; y++)
        {
            for (var x = centre.X - radius; x <= centre.X + radius; x++)
            {
                var (dx, dy) = (x - centre.X, y - centre.Y);
                if (dx * dx + dy * dy <= radius * radius)
                {
                    Tree(new CellPos(x, y), density);
                }
            }
        }
    }

    /// <summary>Pines on <paramref name="density"/> % of the clear cells of the area (and its images), away from starts, ore and roads.</summary>
    public void Forest(Rect area, int density, Rect keepOut)
    {
        for (var y = area.Y; y < area.Y + area.Height; y++)
        {
            for (var x = area.X; x < area.X + area.Width; x++)
            {
                var cell = new CellPos(x, y);
                if (!keepOut.Contains(cell) && !NearStart(cell, 13) && !NearOre(cell, 4))
                {
                    Tree(cell, density);
                }
            }
        }
    }

    /// <summary>The finished map; throws when <see cref="MapValidator"/> finds errors.</summary>
    public MapDefinition Finish()
    {
        var map = _session.Map.ToDefinition();
        var errors = MapValidator.Check(map).Where(p => p.Level == ProblemLevel.Error).ToList();
        if (errors.Count > 0)
        {
            throw new InvalidOperationException($"{map.Id}: " + string.Join("; ", errors.Select(e => $"{e.Message} [{e.Cell}]")));
        }

        return map;
    }

    private void Tree(CellPos cell, int density)
    {
        if (!_session.Map.ContainsCell(cell) || Hash(cell) % 100 >= density || _session.Map.LandAt(cell) is not (LandType.Clear or LandType.Rough) || NextToRoad(cell))
        {
            return;
        }

        for (var k = 0; k < Order; k++)
        {
            _session.BeginStroke(EditTool.Objects, new Brush(BrushShape.Circle, 1), new EditOptions { Density = 100 });
            var image = Cell(k, cell);
            _session.Paint(image.X, image.Y, image);
            _session.EndStroke();
        }
    }

    private bool NextToRoad(CellPos cell)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (_session.Map.ContainsCell(next) && _session.Map.LandAt(next) == LandType.Road)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool NearStart(CellPos cell, int distance) =>
        _session.Map.Starts.Any(s => (s.Cell.X - cell.X) * (s.Cell.X - cell.X) + (s.Cell.Y - cell.Y) * (s.Cell.Y - cell.Y) < distance * distance);

    private bool NearOre(CellPos cell, int distance)
    {
        for (var y = cell.Y - distance; y <= cell.Y + distance; y++)
        {
            for (var x = cell.X - distance; x <= cell.X + distance; x++)
            {
                if (_session.Map.ContainsCell(new CellPos(x, y)) && _session.Map.OreAt(new CellPos(x, y)) > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The stroke and its images; an image that covers the same cells as one already made is left out.</summary>
    private void Strokes(EditTool tool, Brush brush, EditOptions? options, IReadOnlyList<CellPos> points)
    {
        var made = new List<HashSet<CellPos>>();
        for (var k = 0; k < Order; k++)
        {
            var mapped = points.Select(p => Cell(k, p)).ToList();
            var set = mapped.ToHashSet();
            if (made.Any(earlier => earlier.SetEquals(set)))
            {
                continue;
            }

            made.Add(set);
            Stroke(tool, brush, options, mapped);
        }
    }

    private void Stroke(EditTool tool, Brush brush, EditOptions? options, IReadOnlyList<CellPos> points)
    {
        _session.BeginStroke(tool, brush, options);
        foreach (var point in points)
        {
            _session.Paint(point.X, point.Y, point);
        }

        _session.EndStroke();
        if (_session.Message is { } refused)
        {
            throw new InvalidOperationException($"{tool} at {points[0]}: {refused}");
        }
    }

    private CellPos Cell(int k, CellPos cell)
    {
        var (x, y) = Image(k, cell.X, cell.Y, _width - 1, _height - 1);
        return new CellPos(x, y);
    }

    private (int X, int Y) CanonicalCorner(int x, int y)
    {
        var best = (x, y);
        for (var k = 1; k < Order; k++)
        {
            var image = Image(k, x, y, _width, _height);
            if (image.Item2 < best.Item2 || (image.Item2 == best.Item2 && image.Item1 < best.Item1))
            {
                best = image;
            }
        }

        return best;
    }

    // Mirror: west to east. Quarter turns (square maps): (x, y) → (max − y, x) and on.
    private (int X, int Y) Image(int k, int x, int y, int maxX, int maxY) => (_rotate, k) switch
    {
        (_, 0) => (x, y),
        (false, _) => (maxX - x, y),
        (true, 1) => (maxX - y, x),
        (true, 2) => (maxX - x, maxY - y),
        _ => (y, maxX - x),
    };

    // Bresenham, both ends included: the cells of the stroke are laid out before they are mirrored.
    private static List<CellPos> Line(CellPos from, CellPos to)
    {
        var cells = new List<CellPos>();
        var (x0, y0, x1, y1) = (from.X, from.Y, to.X, to.Y);
        var (dx, dy) = (Math.Abs(x1 - x0), -Math.Abs(y1 - y0));
        var (sx, sy) = (x0 < x1 ? 1 : -1, y0 < y1 ? 1 : -1);
        var error = dx + dy;
        while (true)
        {
            cells.Add(new CellPos(x0, y0));
            if (x0 == x1 && y0 == y1)
            {
                return cells;
            }

            var doubled = 2 * error;
            if (doubled >= dy)
            {
                error += dy;
                x0 += sx;
            }

            if (doubled <= dx)
            {
                error += dx;
                y0 += sy;
            }
        }
    }

    // FNV-1a of the cell: which cells of a wood get a tree, the same on every run.
    private static uint Hash(CellPos cell)
    {
        var hash = 2166136261u;
        foreach (var value in new[] { cell.X, cell.Y, 0x7EE5 })
        {
            for (var shift = 0; shift < 32; shift += 8)
            {
                hash = (hash ^ (uint)((value >> shift) & 0xFF)) * 16777619u;
            }
        }

        return hash;
    }
}
