using Rts.Data.Maps;
using Rts.MapGen;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

/// <summary>
/// The outposts of RA2 on a map the generator made (the map tool's <c>add-tech</c>): one as near the middle as it fits, copied to every
/// image of the map's symmetry, so every start is as near to one as the others — contested ground, as RA2 puts its tech buildings. A footprint fits on flat, clear or rough land a tank can enter, with no ore,
/// bridge, building or other tech building on it or next to it, and away from the flat ground of the starts. The generator itself does
/// not place them: its maps and their reference hashes stay as they are.
/// </summary>
internal static class TechSites
{
    public const string Outpost = "tech_outpost";

    // The footprint of the outpost (BuildingTypes): it does not turn, so every image has it as it is; and the free ring round it.
    private const int Width = 4;
    private const int Height = 3;
    private const int Margin = 1;

    // The flat ground of a start of the generator (Rts.MapGen Stages.Starts.FlatRadius, internal there).
    private const int StartFlat = 12;

    /// <summary><paramref name="map"/> with the outposts placed, or an error when they find no room.</summary>
    public static MapDefinition AddTo(MapDefinition map, Symmetry symmetry)
    {
        if (map.Structures.Any(structure => structure.Type is Outpost))
        {
            throw new ArgumentException($"The map '{map.Id}' has its tech buildings already.");
        }

        var ground = MapChecks.BaseGrid(map);
        var taken = new HashSet<CellPos>();
        foreach (var bridge in map.Bridges)
        {
            taken.UnionWith(MapChecks.BridgeCells(bridge));
        }

        foreach (var structure in map.Structures)
        {
            taken.UnionWith(MapChecks.Footprint(structure));
        }

        var corners = Place(map, ground, symmetry, taken)
            ?? throw new ArgumentException($"The map '{map.Id}' has no room for an outpost with its images.");
        var placed = corners.Select(corner => new StructurePlacement(Outpost, corner, 0, 0)).ToList();

        return new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
            map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, map.Objects, map.Bridges, [.. map.Structures, .. placed],
            map.Lighting);
    }

    // The north-west corners of the footprint nearest the middle that fits with all its images, those images; null without one.
    private static List<CellPos>? Place(MapDefinition map, MapGrid ground, Symmetry symmetry, HashSet<CellPos> taken)
    {
        var (middleX, middleY) = ((map.Width - Width) / 2, (map.Height - Height) / 2);
        var order = Enumerable.Range(0, map.Height - Height + 1)
            .SelectMany(y => Enumerable.Range(0, map.Width - Width + 1).Select(x => new CellPos(x, y)))
            .OrderBy(c => (c.X - middleX) * (c.X - middleX) + (c.Y - middleY) * (c.Y - middleY)).ThenBy(c => c.Y).ThenBy(c => c.X);
        foreach (var corner in order)
        {
            // An image of the footprint is the footprint of the images of its cells: its corner is their north-west (turned a quarter, a
            // 4 x 3 footprint covers 3 x 4; the outpost there keeps its 4 x 3 from that corner).
            var images = Enumerable.Range(0, symmetry.Order)
                .Select(k => Cells(corner, 0).Select(cell => symmetry.Cell(k, cell)).ToList())
                .Select(cells => new CellPos(cells.Min(c => c.X), cells.Min(c => c.Y)))
                .Distinct()
                .ToList();
            var claimed = new HashSet<CellPos>();
            if (images.All(image => Fits(map, ground, image, taken) && Cells(image, Margin).All(claimed.Add)))
            {
                return images;
            }
        }

        return null;
    }

    private static bool Fits(MapDefinition map, MapGrid ground, CellPos corner, HashSet<CellPos> taken) =>
        Cells(corner, 0).All(cell => map.Contains(cell) && map.LandAt(cell) is LandType.Clear or LandType.Rough
                                     && ground.CanEnter(cell, Locomotor.Track) && !taken.Contains(cell))
        && Cells(corner, Margin).All(cell => !map.Contains(cell) || (map.Ore[cell.Y * map.Width + cell.X] == 0 && !taken.Contains(cell)))
        && Flat(map, corner)
        && map.Starts.All(start => Distance2(Centre(corner), start.Cell) > (StartFlat + Width) * (StartFlat + Width));

    // Every corner of the footprint at one height: a building stands on flat ground (MapValidator).
    private static bool Flat(MapDefinition map, CellPos corner)
    {
        var level = map.CornerHeight(corner.X, corner.Y);
        for (var y = corner.Y; y <= corner.Y + Height; y++)
        {
            for (var x = corner.X; x <= corner.X + Width; x++)
            {
                if (map.CornerHeight(x, y) != level)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static IEnumerable<CellPos> Cells(CellPos corner, int margin) =>
        Enumerable.Range(-margin, Height + 2 * margin)
            .SelectMany(dy => Enumerable.Range(-margin, Width + 2 * margin).Select(dx => new CellPos(corner.X + dx, corner.Y + dy)));

    private static CellPos Centre(CellPos corner) => new(corner.X + Width / 2, corner.Y + Height / 2);

    private static int Distance2(CellPos a, CellPos b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
