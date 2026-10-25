using Rts.Data.Maps;
using Rts.MapGen;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

/// <summary>
/// The tech buildings of M11 a5 on a map the generator made (the map tool's <c>add-tech</c>): a power plant and a machine shop as
/// near the middle as they fit, each copied to every image of the map's symmetry, so every start is as near to one of each as the
/// others — contested ground, as RA2 puts its tech buildings. A footprint fits on flat, clear or rough land a tank can enter, with no ore,
/// bridge, building or other tech building on it or next to it, and away from the flat ground of the starts. The generator itself does
/// not place them: its maps and their reference hashes stay as they are.
/// </summary>
internal static class TechSites
{
    public const string PowerPlant = "tech_power_plant";
    public const string MachineShop = "tech_machine_shop";

    // The footprint of both (BuildingTypes) and the free ring round it.
    private const int Size = 3;
    private const int Margin = 1;

    // The flat ground of a start of the generator (Rts.MapGen Stages.Starts.FlatRadius, internal there).
    private const int StartFlat = 12;

    /// <summary><paramref name="map"/> with the two kinds placed, or an error when one finds no room.</summary>
    public static MapDefinition AddTo(MapDefinition map, Symmetry symmetry)
    {
        if (map.Structures.Any(structure => structure.Type is PowerPlant or MachineShop))
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

        var placed = new List<StructurePlacement>();
        foreach (var type in new[] { PowerPlant, MachineShop })
        {
            var corners = Place(map, ground, symmetry, taken)
                ?? throw new ArgumentException($"The map '{map.Id}' has no room for a {type} with its images.");
            foreach (var corner in corners)
            {
                placed.Add(new StructurePlacement(type, corner, 0, 0));
                taken.UnionWith(Cells(corner, Margin));
            }
        }

        return new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
            map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, map.Objects, map.Bridges, [.. map.Structures, .. placed],
            map.Lighting);
    }

    // The north-west corners of the footprint nearest the middle that fits with all its images, those images; null without one.
    private static List<CellPos>? Place(MapDefinition map, MapGrid ground, Symmetry symmetry, HashSet<CellPos> taken)
    {
        var middle = (map.Width - Size) / 2;
        var order = Enumerable.Range(0, map.Height - Size + 1)
            .SelectMany(y => Enumerable.Range(0, map.Width - Size + 1).Select(x => new CellPos(x, y)))
            .OrderBy(c => (c.X - middle) * (c.X - middle) + (c.Y - middle) * (c.Y - middle)).ThenBy(c => c.Y).ThenBy(c => c.X);
        foreach (var corner in order)
        {
            // An image of the footprint is the footprint of the images of its cells: its corner is their north-west.
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
        && map.Starts.All(start => Distance2(Centre(corner), start.Cell) > (StartFlat + Size) * (StartFlat + Size));

    // Every corner of the footprint at one height: a building stands on flat ground (MapValidator).
    private static bool Flat(MapDefinition map, CellPos corner)
    {
        var level = map.CornerHeight(corner.X, corner.Y);
        for (var y = corner.Y; y <= corner.Y + Size; y++)
        {
            for (var x = corner.X; x <= corner.X + Size; x++)
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
        Enumerable.Range(-margin, Size + 2 * margin)
            .SelectMany(dy => Enumerable.Range(-margin, Size + 2 * margin).Select(dx => new CellPos(corner.X + dx, corner.Y + dy)));

    private static CellPos Centre(CellPos corner) => new(corner.X + Size / 2, corner.Y + Size / 2);

    private static int Distance2(CellPos a, CellPos b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
}
