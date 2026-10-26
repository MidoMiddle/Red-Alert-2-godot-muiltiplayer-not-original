using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.AI.View;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Knowledge;

/// <summary>
/// What a bot reads from the map on its first turn (M11, the bot that knows every weapon), as the preview of the lobby shows it
/// (<see cref="AiWorldView.CanEnterOnPreview"/>): whether its tanks can drive to an enemy, and whether ships can sail there.
/// <list type="bullet">
/// <item>The enemies are at the starts of the map but the bot's own and those an ally stands near (<see cref="AllyCells"/>).</item>
/// <item><b>By land</b>: a flood fill of the cells a tank enters from the start reaches the cells round an enemy start.</item>
/// <item><b>By water</b>: the deep water is cut into seas, each the cells a ship reaches from any other. The harbour is the place of
/// the shipyard of the faction nearest the start, within <see cref="HarbourReach"/>, whose footprint is all deep water of one sea that
/// comes within <see cref="ShoreCells"/> of an enemy start; the enemy shores are the cells of that sea nearest each such start.</item>
/// </list>
/// Worked out once: the land and the water do not change in a match.
/// </summary>
public sealed class MapReading
{
    /// <summary>How far from the start the shipyard may stand, in cells: the reach of the placement of the bot.</summary>
    public const int HarbourReach = Modules.BasePlacement.Reach;

    /// <summary>A sea this near an enemy start, in cells, reaches its base: the guns of the ships and the buildings round it.</summary>
    public const int ShoreCells = 16;

    /// <summary>A start an ally stands this near, in cells, is not an enemy's.</summary>
    public const int AllyCells = 8;

    private static readonly (int X, int Y)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1)];

    private MapReading(bool landRoute, CellPos? harbour, ImmutableArray<CellPos> enemyShores)
    {
        LandRoute = landRoute;
        Harbour = harbour;
        EnemyShores = enemyShores;
    }

    /// <summary>The reading of a bot that knows no start: the land leads everywhere, the water nowhere.</summary>
    public static readonly MapReading None = new(true, null, []);

    /// <summary>Whether a tank can drive from the start to an enemy start; true when no enemy start is known.</summary>
    public bool LandRoute { get; }

    /// <summary>The north-west cell of the place of the shipyard; null when no sea near the base leads to an enemy.</summary>
    public CellPos? Harbour { get; }

    /// <summary>For every enemy start the sea of the harbour reaches, the cell of that sea nearest it.</summary>
    public ImmutableArray<CellPos> EnemyShores { get; }

    /// <summary>Whether ships built at the harbour can sail to an enemy.</summary>
    public bool NavalRoute => Harbour is not null && EnemyShores.Length > 0;

    /// <summary>The enemy shore nearest <paramref name="cell"/>; null without a naval route.</summary>
    public CellPos? ShoreNear(CellPos cell)
    {
        CellPos? best = null;
        var bestDistance = int.MaxValue;
        foreach (var shore in EnemyShores)
        {
            var distance = Distance(shore, cell);
            if (distance < bestDistance)
            {
                (best, bestDistance) = (shore, distance);
            }
        }

        return best;
    }

    /// <summary>Reads the map for the bot of <paramref name="view"/>; <paramref name="shipyard"/> is the shipyard of its faction, null for none.</summary>
    public static MapReading Of(AiWorldView view, BuildingDef? shipyard)
    {
        if (view.Start is not { } start)
        {
            return None;
        }

        var enemies = new List<CellPos>();
        foreach (var other in view.MapStarts)
        {
            if (other != start && !AllyNear(view, other))
            {
                enemies.Add(other);
            }
        }

        if (enemies.Count == 0)
        {
            return None;
        }

        var land = Fill(view, start, Locomotor.Track);
        var landRoute = false;
        foreach (var enemy in enemies)
        {
            landRoute |= Near(view, land, enemy, 2);
        }

        if (shipyard is null)
        {
            return new MapReading(landRoute, null, []);
        }

        var seas = Seas(view);
        var places = new List<(int Distance, CellPos Corner, int Sea)>();
        for (var y = start.Y - HarbourReach; y <= start.Y + HarbourReach; y++)
        {
            for (var x = start.X - HarbourReach; x <= start.X + HarbourReach; x++)
            {
                if (SeaUnder(view, seas, shipyard, new CellPos(x, y)) is { } sea)
                {
                    var middle = new CellPos(x + shipyard.Footprint.Width / 2, y + shipyard.Footprint.Height / 2);
                    places.Add((Distance(middle, start), new CellPos(x, y), sea));
                }
            }
        }

        places.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
            : a.Corner.Y != b.Corner.Y ? a.Corner.Y.CompareTo(b.Corner.Y) : a.Corner.X.CompareTo(b.Corner.X));
        // The shores of a sea, once for every sea a place is on.
        var shoresOf = new Dictionary<int, ImmutableArray<CellPos>>();
        foreach (var (_, corner, sea) in places)
        {
            if (!shoresOf.TryGetValue(sea, out var shores))
            {
                var found = ImmutableArray.CreateBuilder<CellPos>();
                foreach (var enemy in enemies)
                {
                    if (NearestOfSea(view, seas, sea, enemy) is { } shore)
                    {
                        found.Add(shore);
                    }
                }

                shores = found.ToImmutable();
                shoresOf[sea] = shores;
            }

            if (shores.Length > 0)
            {
                return new MapReading(landRoute, corner, shores);
            }
        }

        return new MapReading(landRoute, null, []);
    }

    private static bool AllyNear(AiWorldView view, CellPos start)
    {
        foreach (var entity in view.Allied)
        {
            var cell = entity is Building building ? building.Cell : ((Unit)entity).Position.ToCell();
            if (Distance(cell, start) <= AllyCells)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The cells <paramref name="locomotor"/> reaches from <paramref name="from"/>, by the eight neighbours.</summary>
    private static bool[] Fill(AiWorldView view, CellPos from, Locomotor locomotor)
    {
        var reached = new bool[view.Width * view.Height];
        if (!view.Contains(from))
        {
            return reached;
        }

        var queue = new Queue<CellPos>();
        reached[from.Y * view.Width + from.X] = true;
        queue.Enqueue(from);
        while (queue.Count > 0)
        {
            var cell = queue.Dequeue();
            foreach (var (dx, dy) in Neighbours)
            {
                var next = new CellPos(cell.X + dx, cell.Y + dy);
                if (view.Contains(next) && !reached[next.Y * view.Width + next.X] && view.CanEnterOnPreview(next, locomotor))
                {
                    reached[next.Y * view.Width + next.X] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return reached;
    }

    /// <summary>Whether a reached cell lies within <paramref name="cells"/> of <paramref name="point"/>.</summary>
    private static bool Near(AiWorldView view, bool[] reached, CellPos point, int cells)
    {
        for (var y = IntMath.Max(0, point.Y - cells); y <= IntMath.Min(view.Height - 1, point.Y + cells); y++)
        {
            for (var x = IntMath.Max(0, point.X - cells); x <= IntMath.Min(view.Width - 1, point.X + cells); x++)
            {
                if (reached[y * view.Width + x])
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>The sea of every cell, from 1; 0 where no ship goes.</summary>
    private static int[] Seas(AiWorldView view)
    {
        var seas = new int[view.Width * view.Height];
        var count = 0;
        var queue = new Queue<CellPos>();
        for (var y = 0; y < view.Height; y++)
        {
            for (var x = 0; x < view.Width; x++)
            {
                if (seas[y * view.Width + x] != 0 || !view.CanEnterOnPreview(new CellPos(x, y), Locomotor.Naval))
                {
                    continue;
                }

                seas[y * view.Width + x] = ++count;
                queue.Enqueue(new CellPos(x, y));
                while (queue.Count > 0)
                {
                    var cell = queue.Dequeue();
                    foreach (var (dx, dy) in Neighbours)
                    {
                        var next = new CellPos(cell.X + dx, cell.Y + dy);
                        if (view.Contains(next) && seas[next.Y * view.Width + next.X] == 0 && view.CanEnterOnPreview(next, Locomotor.Naval))
                        {
                            seas[next.Y * view.Width + next.X] = count;
                            queue.Enqueue(next);
                        }
                    }
                }
            }
        }

        return seas;
    }

    /// <summary>The sea under the whole footprint of <paramref name="def"/> on <paramref name="corner"/>; null when any cell is not of one sea.</summary>
    private static int? SeaUnder(AiWorldView view, int[] seas, BuildingDef def, CellPos corner)
    {
        var sea = 0;
        for (var y = corner.Y; y < corner.Y + def.Footprint.Height; y++)
        {
            for (var x = corner.X; x < corner.X + def.Footprint.Width; x++)
            {
                var cell = new CellPos(x, y);
                if (!view.Contains(cell) || seas[y * view.Width + x] == 0 || (sea != 0 && seas[y * view.Width + x] != sea))
                {
                    return null;
                }

                sea = seas[y * view.Width + x];
            }
        }

        return sea;
    }

    /// <summary>The cell of <paramref name="sea"/> nearest <paramref name="point"/> within <see cref="ShoreCells"/>; null for none.</summary>
    private static CellPos? NearestOfSea(AiWorldView view, int[] seas, int sea, CellPos point)
    {
        for (var ring = 0; ring <= ShoreCells; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(point.X + dx, point.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && view.Contains(cell) && seas[cell.Y * view.Width + cell.X] == sea)
                    {
                        return cell;
                    }
                }
            }
        }

        return null;
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
