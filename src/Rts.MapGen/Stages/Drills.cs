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
    public static (List<MapObjectPlacement> Placements, List<string> Missing) For(MapDefinition map, MapTypes types, Symmetry symmetry, DrillKind kind)
    {
        var placements = new List<MapObjectPlacement>();
        var missing = new List<string>();
        if (kind == DrillKind.None || map.Starts.IsEmpty)
        {
            return (placements, missing);
        }

        var (home, contested) = Stages.Ore.Centres(map.Starts[0].Cell, symmetry.Size);
        var site = new Site(map, types);
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
    public static MapDefinition AddTo(MapDefinition map, MapTypes types, MapSymmetry symmetry, DrillKind kind)
    {
        if (HasDrills(map))
        {
            throw new ArgumentException($"The map '{map.Id}' has drills already.");
        }

        if (map.Width != map.Height)
        {
            throw new ArgumentException($"The map '{map.Id}' is {map.Width} × {map.Height}: the generator makes square maps only.");
        }

        var (placements, missing) = For(map, types, new Symmetry(symmetry, map.Width), kind);
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
            map.Structures, map.Lighting, map.Playable, map.Origin);
    }

    /// <summary>How far in a straight line from every start the shared gem drill of a map from RA2 stands, in cells: no base has it at home.</summary>
    public const int BaseRadius = 35;

    /// <summary>
    /// <paramref name="map"/> — a map from RA2 — with gem drills in the middle ground instead of one by every start. Its gem drills
    /// go; a new one stands where the tank ways from all the starts differ least, if they differ by at most a
    /// <see cref="FairShare"/> of the longest — else one for every pair of neighbouring starts (the pairing whose longest way within
    /// a pair is shortest), where the ways from the two differ least. A drill stands farther than <see cref="BaseRadius"/> from
    /// every start, with room for what it throws; ties go to the nearer, then by y and x. Everything else stays as it is.
    /// </summary>
    /// <exception cref="ArgumentException">No cell of the map fits.</exception>
    public static MapDefinition ShareGems(MapDefinition map, MapTypes types)
    {
        var objects = map.Objects.Where(o => o.Type != GemSource).ToList();
        var bare = new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
            map.TextureLegend, map.Heights, map.Land, map.Texture, map.Ore, [.. objects], map.Bridges, map.Structures, map.Lighting,
            map.Playable, map.Origin);
        var site = new Site(bare, types);
        var paths = map.Starts.Select(start => PathCosts(bare.ToMapGrid(types), start.Cell)).ToList();
        var occupied = objects.Select(o => o.Cell).ToHashSet();
        var all = Enumerable.Range(0, map.Starts.Length).ToList();
        var shared = Fairest(map, site, paths, occupied, all);
        List<(int Spread, int Farthest, int Y, int X)> drills;
        if (shared is { } one && one.Spread * 100 <= one.Farthest / 10 * FairShare)
        {
            drills = [one];
        }
        else if (all.Count % 2 == 0 && all.Count > 2)
        {
            var pairs = Pairings(all).MinBy(pairing => pairing.Max(pair => paths[pair.A][Index(map, map.Starts[pair.B].Cell)]))!;
            drills = [.. pairs.Select(pair => Fairest(map, site, paths, occupied, [pair.A, pair.B])
                                              ?? throw new ArgumentException($"The map '{map.Id}' has no cell for a gem drill of starts {pair.A + 1} and {pair.B + 1}."))];
        }
        else
        {
            drills = shared is { } fallback ? [fallback] : throw new ArgumentException($"The map '{map.Id}' has no cell for a shared gem drill.");
        }

        var ore = map.Ore.ToBuilder();
        foreach (var drill in drills)
        {
            var at = new CellPos(drill.X, drill.Y);
            ore[Index(map, at)] = 0;
            objects.Add(new MapObjectPlacement(GemSource, at, (int)(((long)at.X * 73856093 ^ (long)at.Y * 19349663) % 4)));
        }

        return new MapDefinition(
            map.Id, map.Name, map.Author, map.Description, map.Width, map.Height, map.Climate, map.Players, map.Starts,
            map.TextureLegend, map.Heights, map.Land, map.Texture, ore.MoveToImmutable(), [.. objects], map.Bridges, map.Structures,
            map.Lighting, map.Playable, map.Origin);
    }

    /// <summary>How much the tank ways from the starts to one shared gem drill may differ, in percent of the longest.</summary>
    public const int FairShare = 30;

    // The cell for a drill of the starts in the group: the ways from them differ least (in whole cells), then the nearest.
    private static (int Spread, int Farthest, int Y, int X)? Fairest(
        MapDefinition map, Site site, List<int[]> paths, HashSet<CellPos> occupied, List<int> group)
    {
        (int Spread, int Farthest, int Y, int X)? best = null;
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = new CellPos(x, y);
                var i = Index(map, cell);
                if (group.Any(k => paths[k][i] == int.MaxValue) || occupied.Contains(cell)
                    || map.Starts.Any(start => Site.Distance2(cell, start.Cell) <= BaseRadius * BaseRadius) || !site.Fits(cell, GemSource))
                {
                    continue;
                }

                // Path costs are tenths of a cell.
                var (near, far) = (group.Min(k => paths[k][i]), group.Max(k => paths[k][i]));
                var key = ((far - near) / 10, far, y, x);
                if (best is null || key.CompareTo(best.Value) < 0)
                {
                    best = key;
                }
            }
        }

        return best;
    }

    // Every way to split the starts (an even count) into pairs, the lower index first in each.
    private static IEnumerable<List<(int A, int B)>> Pairings(List<int> starts)
    {
        if (starts.Count == 0)
        {
            yield return [];
            yield break;
        }

        for (var j = 1; j < starts.Count; j++)
        {
            var rest = starts.Where((_, k) => k != 0 && k != j).ToList();
            foreach (var pairing in Pairings(rest))
            {
                yield return [(starts[0], starts[j]), .. pairing];
            }
        }
    }

    private static int Index(MapDefinition map, CellPos cell) => cell.Y * map.Width + cell.X;

    // The cost of a tank's way from the start to every cell, in tenths of a cell (10 straight, 14 diagonal, no cutting corners);
    // int.MaxValue where it never gets. The deck of a high bridge counts as ground: close enough to weigh the ways.
    public static int[] PathCosts(MapGrid ground, CellPos start)
    {
        var cost = new int[ground.Width * ground.Height];
        Array.Fill(cost, int.MaxValue);
        var queue = new PriorityQueue<CellPos, int>();
        cost[start.Y * ground.Width + start.X] = 0;
        queue.Enqueue(start, 0);
        while (queue.TryDequeue(out var cell, out var reached))
        {
            if (reached > cost[cell.Y * ground.Width + cell.X])
            {
                continue;
            }

            for (var dy = -1; dy <= 1; dy++)
            {
                for (var dx = -1; dx <= 1; dx++)
                {
                    var next = new CellPos(cell.X + dx, cell.Y + dy);
                    if ((dx, dy) == (0, 0) || !Passes(ground, next)
                        || (dx != 0 && dy != 0 && !(Passes(ground, new CellPos(cell.X + dx, cell.Y)) && Passes(ground, new CellPos(cell.X, cell.Y + dy)))))
                    {
                        continue;
                    }

                    var step = reached + (dx != 0 && dy != 0 ? 14 : 10);
                    if (step < cost[next.Y * ground.Width + next.X])
                    {
                        cost[next.Y * ground.Width + next.X] = step;
                        queue.Enqueue(next, step);
                    }
                }
            }
        }

        return cost;
    }

    private static bool Passes(MapGrid ground, CellPos cell) => ground.CanEnter(cell, Locomotor.Track) || ground.IsDeck(cell);

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

        public Site(MapDefinition map, MapTypes types)
        {
            _map = map;
            _ground = MapChecks.BaseGrid(map, types);
            foreach (var bridge in map.Bridges)
            {
                _taken.UnionWith(MapChecks.BridgeCells(bridge));
            }

            foreach (var structure in map.Structures)
            {
                _taken.UnionWith(MapChecks.Footprint(structure, types));
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

        public static int Distance2(CellPos a, CellPos b) => (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);
    }
}
