using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Data.Maps;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;

namespace Rts.MapGen;

/// <summary>Which drills a random map gets (docs/implementation/M8_ai_difficulty.md, step 10b; M3 step 16 made them).</summary>
public enum DrillKind
{
    /// <summary>None: the ore of the map runs out, as before M8 step 10b.</summary>
    None,

    /// <summary>An ore drill in the home field of every player.</summary>
    Ore,

    /// <summary>An ore drill at home and a gem drill in the contested field of every player.</summary>
    OreAndGems,
}

/// <summary>
/// Where the drills of a map go (M8 step 10b), one rule for the generator and for the ready maps made by it: an ore drill in the
/// home field of the first start and a gem drill in its contested field (<see cref="Stages.Ore"/>), each copied to every image of the
/// symmetry, so every start has the same. The cell is the centre of the field, or the field cell nearest to it that fits in every
/// image: clear or rough land a tank can enter, no bridge or building, outside the flat ground of every start, and room around
/// it for what the drill throws. The ore under a drill is taken away — a drill grows nothing on its own cell.
/// </summary>
public static class Drills
{
    public const string OreSource = "ore_source";
    public const string GemSource = "gem_source";

    /// <summary>Drills already on the map, by kind: a map with them gets no more.</summary>
    public static bool HasDrills(MapDefinition map) => map.Objects.Any(o => o.Type is OreSource or GemSource);

    /// <summary>
    /// The drills for the fields of the first start of <paramref name="map"/> under <paramref name="symmetry"/>, with the fields that
    /// had no cell fit for one (none of them on a sane map).
    /// </summary>
    public static (List<MapObjectPlacement> Placements, List<string> Missing) For(MapDefinition map, Symmetry symmetry, DrillKind kind)
    {
        var placements = new List<MapObjectPlacement>();
        var missing = new List<string>();
        if (kind == DrillKind.None || map.Starts.IsEmpty)
        {
            return (placements, missing);
        }

        var (home, contested) = Stages.Ore.Centres(map.Starts[0].Cell, symmetry.Size);
        var site = new Site(map);
        if (!Add(site, symmetry, home, Stages.Ore.HomeRadius, OreSource, placements))
        {
            missing.Add($"the home field at {home.X}, {home.Y}");
        }

        if (kind == DrillKind.OreAndGems && !Add(site, symmetry, contested, Stages.Ore.ContestedRadius, GemSource, placements))
        {
            missing.Add($"the contested field at {contested.X}, {contested.Y}");
        }

        return (placements, missing);
    }

    /// <summary>
    /// <paramref name="map"/> — a ready map made by the generator with <paramref name="symmetry"/> — with the drills of
    /// <paramref name="kind"/> and the ore under them taken away; the description says which. Everything else stays as it is.
    /// </summary>
    /// <exception cref="ArgumentException">The map has drills already, or a field has no cell fit for one.</exception>
    public static MapDefinition AddTo(MapDefinition map, MapSymmetry symmetry, DrillKind kind)
    {
        if (HasDrills(map))
        {
            throw new ArgumentException($"The map '{map.Id}' has drills already.");
        }

        if (map.Width != map.Height)
        {
            throw new ArgumentException($"The map '{map.Id}' is {map.Width} × {map.Height}: the generator makes square maps only.");
        }

        var (placements, missing) = For(map, new Symmetry(symmetry, map.Width), kind);
        if (missing.Count > 0)
        {
            throw new ArgumentException($"The map '{map.Id}' has no cell for a drill in {string.Join(" and ", missing)}.");
        }

        var ore = map.Ore.ToBuilder();
        foreach (var placement in placements)
        {
            ore[placement.Cell.Y * map.Width + placement.Cell.X] = 0;
        }

        return new MapDefinition(
            map.Id, map.Name, map.Author, WithDrills(map.Description, kind), map.Width, map.Height, map.Climate, map.Players, map.Starts,
            map.TextureLegend, map.Heights, map.Land, map.Texture, ore.MoveToImmutable(), [.. map.Objects, .. placements], map.Bridges,
            map.Structures, map.Lighting);
    }

    /// <summary>The description of a generated map with the drills said: «…, resources Medium, drills OreAndGems.»; none — as it was.</summary>
    public static string WithDrills(string description, DrillKind kind) =>
        kind == DrillKind.None ? description : $"{description.TrimEnd('.')}, drills {kind}.";

    // The first cell of the field — by distance from its centre, then y, then x — that fits in every image, its images added.
    private static bool Add(Site site, Symmetry symmetry, CellPos centre, int radius, string type, List<MapObjectPlacement> placements)
    {
        var candidates = new List<CellPos>();
        for (var y = centre.Y - radius; y <= centre.Y + radius; y++)
        {
            for (var x = centre.X - radius; x <= centre.X + radius; x++)
            {
                var (dx, dy) = (x - centre.X, y - centre.Y);
                if (dx * dx + dy * dy <= radius * radius)
                {
                    candidates.Add(new CellPos(x, y));
                }
            }
        }

        foreach (var cell in candidates.OrderBy(c => (c.X - centre.X) * (c.X - centre.X) + (c.Y - centre.Y) * (c.Y - centre.Y)).ThenBy(c => c.Y).ThenBy(c => c.X))
        {
            var images = Enumerable.Range(0, symmetry.Order).Select(k => symmetry.Cell(k, cell)).Distinct().ToList();
            if (images.All(image => site.Fits(image, type) && !placements.Any(p => p.Cell == image)))
            {
                placements.AddRange(images.Select(image => new MapObjectPlacement(type, image, 0)));
                return true;
            }
        }

        return false;
    }

    /// <summary>What a cell of the map allows a drill.</summary>
    private sealed class Site
    {
        // Room around a drill: cells within the radius it throws to (3, game/data/shared/economy.yaml) it can grow on.
        private const int ThrowRadius = 3;
        private const int MinRoom = 4;

        private readonly MapDefinition _map;
        private readonly MapGrid _ground;
        private readonly HashSet<CellPos> _taken = [];

        public Site(MapDefinition map)
        {
            _map = map;
            _ground = MapChecks.BaseGrid(map);
            foreach (var bridge in map.Bridges)
            {
                _taken.UnionWith(MapChecks.BridgeCells(bridge));
            }

            foreach (var structure in map.Structures)
            {
                _taken.UnionWith(MapChecks.Footprint(structure));
            }
        }

        public bool Fits(CellPos cell, string type)
        {
            if (!Open(cell) || _map.Starts.Any(start => Distance2(cell, start.Cell) <= Stages.Starts.FlatRadius * Stages.Starts.FlatRadius))
            {
                return false;
            }

            var room = 0;
            for (var dy = -ThrowRadius; dy <= ThrowRadius; dy++)
            {
                for (var dx = -ThrowRadius; dx <= ThrowRadius; dx++)
                {
                    var near = new CellPos(cell.X + dx, cell.Y + dy);
                    if ((dx, dy) != (0, 0) && dx * dx + dy * dy <= ThrowRadius * ThrowRadius && Open(near) && Grows(near, type))
                    {
                        room++;
                    }
                }
            }

            return room >= MinRoom;
        }

        // A cell a drill can stand on or throw to: on the map, clear or rough, a tank can enter it, no bridge or building.
        private bool Open(CellPos cell) =>
            _map.Contains(cell) && _map.LandAt(cell) is LandType.Clear or LandType.Rough && !_taken.Contains(cell)
            && _ground.CanEnter(cell, Locomotor.Track);

        // Empty, or ore of the drill's own kind (gems are 10–12).
        private bool Grows(CellPos cell, string type)
        {
            var ore = _map.Ore[cell.Y * _map.Width + cell.X];
            return ore == 0 || (type == GemSource ? ore >= 10 : ore < 10);
        }

        private static int Distance2(CellPos a, CellPos b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
    }
}
