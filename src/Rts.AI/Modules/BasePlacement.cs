using System.Collections.Generic;
using Rts.AI.View;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// Where a bot places a building (docs/implementation/M5_first_bot.md, 4.6, step 5). The north-west cells within <see cref="Reach"/> of the
/// middle of the construction yard are the candidates, ring by ring. A candidate is left out when:
/// <list type="bullet">
/// <item>the rules do not let the building stand there, as far as the bot has seen (<see cref="AiWorldView.CanPlace"/>);</item>
/// <item>it comes nearer than <see cref="EdgeMargin"/> cells to the edge of the map;</item>
/// <item>an own building stands in the ring of cells round it — the passages of one cell stay open;</item>
/// <item>the cell where the building releases units or takes ore cannot be driven on.</item>
/// </list>
/// The rest are weighed, the lowest first: a building by its distance to the yard, a refinery by its distance to the explored ore, a
/// defense by its distance to a point <see cref="DefenseAhead"/> cells from the yard toward the likely enemy; equal ones go to the smaller
/// (Y, X). The best that keeps every exit and dock of the base open to the outside — checked by a flood fill over the base, at most
/// <see cref="PassageChecks"/> times — is the place. At most <see cref="CandidatesPerCall"/> candidates are looked at in one call: an
/// unfinished search goes on in the next one, for the same building.
/// </summary>
public sealed class BasePlacement
{
    public const int Reach = 14;
    public const int CandidatesPerCall = 400;
    public const int PassageChecks = 8;
    public const int EdgeMargin = 2;
    public const int DefenseAhead = 6;

    /// <summary>The flood fill of the passages looks this far round the base.</summary>
    public const int PassageMargin = 4;

    private static readonly List<CellPos> Offsets = Rings(Reach);

    private static readonly (int X, int Y)[] Neighbours = [(1, 0), (-1, 0), (0, 1), (0, -1)];

    private readonly List<(long Score, CellPos Corner)> _found = new();

    // The explored ore of the search of a refinery, gathered once as it starts.
    private readonly List<CellPos> _ore = new();
    private int _defIndex = -1;
    private int _next;

    /// <summary>
    /// Looks on for a place for building <paramref name="defIndex"/> near <paramref name="yard"/>. True when the search is over: then
    /// <paramref name="corner"/> is the north-west cell of the place, or null when there is none. False while it goes on.
    /// </summary>
    /// <summary>Where a defense faces, when it is not the likely enemy: the side the base was attacked from (step 9).</summary>
    public CellPos? Toward { get; set; }

    /// <summary>The place of the shipyard the map reading found (M11): a shipyard goes as near it as it can.</summary>
    public CellPos? Harbour { get; set; }

    public bool TryFind(AiWorldView view, AiMemory memory, Building yard, int defIndex, out CellPos? corner)
    {
        if (defIndex != _defIndex)
        {
            _defIndex = defIndex;
            _next = 0;
            _found.Clear();
            _ore.Clear();
            if (view.Rules.Database.Buildings[defIndex].DockCell is not null)
            {
                GatherOre(view);
            }
        }

        var def = view.Rules.Database.Buildings[defIndex];
        var centre = Middle(yard);
        var target = Target(memory, def, centre, Toward);
        var end = IntMath.Min(_next + CandidatesPerCall, Offsets.Count);
        for (; _next < end; _next++)
        {
            var candidate = new CellPos(centre.X + Offsets[_next].X, centre.Y + Offsets[_next].Y);
            if (Fits(view, def, candidate))
            {
                _found.Add((Score(def, candidate, centre, target), candidate));
            }
        }

        corner = null;
        if (_next < Offsets.Count)
        {
            return false;
        }

        _found.Sort((a, b) => a.Score != b.Score ? a.Score.CompareTo(b.Score)
            : a.Corner.Y != b.Corner.Y ? a.Corner.Y.CompareTo(b.Corner.Y) : a.Corner.X.CompareTo(b.Corner.X));
        var checks = 0;
        foreach (var (_, candidate) in _found)
        {
            // The base may have changed while the search went on: the place is looked at again.
            if (!Fits(view, def, candidate))
            {
                continue;
            }

            if (KeepsPassages(view, def, candidate))
            {
                corner = candidate;
                break;
            }

            if (++checks >= PassageChecks)
            {
                break;
            }
        }

        _defIndex = -1;
        return true;
    }

    /// <summary>Whether <paramref name="def"/> may stand with its north-west cell on <paramref name="corner"/>, the passages aside.</summary>
    private static bool Fits(AiWorldView view, BuildingDef def, CellPos corner)
    {
        var far = new CellPos(corner.X + def.Footprint.Width - 1, corner.Y + def.Footprint.Height - 1);
        if (corner.X < EdgeMargin || corner.Y < EdgeMargin || far.X >= view.Width - EdgeMargin || far.Y >= view.Height - EdgeMargin)
        {
            return false;
        }

        foreach (var building in view.OwnBuildings)
        {
            if (building.Cell.X <= far.X + 1 && building.FarCell.X >= corner.X - 1 && building.Cell.Y <= far.Y + 1 && building.FarCell.Y >= corner.Y - 1)
            {
                return false;
            }
        }

        // A shipyard releases its ships onto the water (M11).
        foreach (var opening in Openings(def, corner))
        {
            if (!view.CanEnter(opening, def.Water ? Locomotor.Naval : Locomotor.Track))
            {
                return false;
            }
        }

        return view.CanPlace(def, corner);
    }

    private long Score(BuildingDef def, CellPos corner, CellPos centre, CellPos? target)
    {
        var middle = new CellPos(corner.X + def.Footprint.Width / 2, corner.Y + def.Footprint.Height / 2);
        if (def.DockCell is not null && _ore.Count > 0)
        {
            var best = long.MaxValue;
            foreach (var ore in _ore)
            {
                var distance = Square(ore.X - middle.X) + Square(ore.Y - middle.Y);
                best = distance < best ? distance : best;
            }

            return best;
        }

        // A shipyard goes nearest the harbour of the map reading (M11), which is on the sea that leads to an enemy.
        var to = def.Water && Harbour is { } harbour ? new CellPos(harbour.X + def.Footprint.Width / 2, harbour.Y + def.Footprint.Height / 2)
            : def.Category == ProductionCategory.Defense && target is { } point ? point : centre;
        return Square(middle.X - to.X) + Square(middle.Y - to.Y);
    }

    /// <summary>
    /// For a defense, the point <see cref="DefenseAhead"/> cells from the yard toward <paramref name="toward"/> — the side the base was
    /// attacked from — else toward the likely enemy: an enemy building or army the bot remembers, else where it last saw an enemy, else
    /// the first start where an enemy may be. Null for other buildings, or with nothing known.
    /// </summary>
    private static CellPos? Target(AiMemory memory, BuildingDef def, CellPos centre, CellPos? toward)
    {
        if (def.Category != ProductionCategory.Defense)
        {
            return null;
        }

        if ((toward ?? memory.LikelyEnemyCell()) is not { } at)
        {
            return null;
        }

        var (dx, dy) = (at.X - centre.X, at.Y - centre.Y);
        var length = IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy));
        return length == 0 ? centre : new CellPos(centre.X + dx * DefenseAhead / length, centre.Y + dy * DefenseAhead / length);
    }

    /// <summary>
    /// Whether, with <paramref name="def"/> on <paramref name="corner"/>, every exit and dock of the base — its own too — still joins the
    /// edge of the area round the base: a flood fill from that edge over the cells a tank can drive, the buildings closed.
    /// </summary>
    internal static bool KeepsPassages(AiWorldView view, BuildingDef def, CellPos corner)
    {
        // A shipyard stands on the water, its exit on it too: no tank drives there (M11).
        if (def.Water)
        {
            return true;
        }

        var far = new CellPos(corner.X + def.Footprint.Width - 1, corner.Y + def.Footprint.Height - 1);
        var (left, top, right, bottom) = (corner.X, corner.Y, far.X, far.Y);
        foreach (var building in view.OwnBuildings)
        {
            (left, top) = (IntMath.Min(left, building.Cell.X), IntMath.Min(top, building.Cell.Y));
            (right, bottom) = (IntMath.Max(right, building.FarCell.X), IntMath.Max(bottom, building.FarCell.Y));
        }

        left = IntMath.Max(0, left - PassageMargin);
        top = IntMath.Max(0, top - PassageMargin);
        right = IntMath.Min(view.Width - 1, right + PassageMargin);
        bottom = IntMath.Min(view.Height - 1, bottom + PassageMargin);
        var width = right - left + 1;
        var height = bottom - top + 1;
        var closed = new bool[width * height];
        void Close(CellPos from, CellPos to)
        {
            for (var y = IntMath.Max(from.Y, top); y <= IntMath.Min(to.Y, bottom); y++)
            {
                for (var x = IntMath.Max(from.X, left); x <= IntMath.Min(to.X, right); x++)
                {
                    closed[(y - top) * width + x - left] = true;
                }
            }
        }

        var openings = new List<CellPos>(Openings(def, corner));
        Close(corner, far);
        foreach (var building in view.OwnBuildings)
        {
            Close(building.Cell, building.FarCell);
            if (building.Def is { Water: false } own)
            {
                openings.AddRange(Openings(own, building.Cell));
            }
        }

        for (var y = top; y <= bottom; y++)
        {
            for (var x = left; x <= right; x++)
            {
                if (!view.CanEnter(new CellPos(x, y), Locomotor.Track))
                {
                    closed[(y - top) * width + x - left] = true;
                }
            }
        }

        var reached = new bool[width * height];
        var queue = new Queue<int>();
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * width + x;
                if ((x == 0 || y == 0 || x == width - 1 || y == height - 1) && !closed[index])
                {
                    reached[index] = true;
                    queue.Enqueue(index);
                }
            }
        }

        while (queue.Count > 0)
        {
            var index = queue.Dequeue();
            var (x, y) = (index % width, index / width);
            foreach (var (stepX, stepY) in Neighbours)
            {
                var (nx, ny) = (x + stepX, y + stepY);
                if (nx >= 0 && ny >= 0 && nx < width && ny < height && !closed[ny * width + nx] && !reached[ny * width + nx])
                {
                    reached[ny * width + nx] = true;
                    queue.Enqueue(ny * width + nx);
                }
            }
        }

        foreach (var opening in openings)
        {
            if (opening.X < left || opening.X > right || opening.Y < top || opening.Y > bottom
                || !reached[(opening.Y - top) * width + opening.X - left])
            {
                return false;
            }
        }

        return true;
    }

    private void GatherOre(AiWorldView view)
    {
        for (var y = 0; y < view.Height; y++)
        {
            for (var x = 0; x < view.Width; x++)
            {
                if (!view.OreAt(new CellPos(x, y)).IsEmpty)
                {
                    _ore.Add(new CellPos(x, y));
                }
            }
        }
    }

    /// <summary>The cells where a building on <paramref name="corner"/> releases its units or takes ore in.</summary>
    internal static IEnumerable<CellPos> Openings(BuildingDef def, CellPos corner)
    {
        if (def.ExitCell is { } exit)
        {
            yield return new CellPos(corner.X + exit.X, corner.Y + exit.Y);
        }

        if (def.DockCell is { } dock)
        {
            // A dock in the footprint (RA2: the refinery's pad) is driven onto from the cell beside it outside.
            var inside = dock.X >= 0 && dock.Y >= 0 && dock.X < def.Footprint.Width && dock.Y < def.Footprint.Height;
            var opening = !inside ? dock
                : dock.X == def.Footprint.Width - 1 ? dock with { X = dock.X + 1 }
                : dock.X == 0 ? dock with { X = -1 }
                : dock.Y == 0 ? dock with { Y = -1 }
                : dock with { Y = dock.Y + 1 };
            yield return new CellPos(corner.X + opening.X, corner.Y + opening.Y);
        }
    }

    internal static CellPos Middle(Building building) => new(building.Cell.X + building.Width / 2, building.Cell.Y + building.Height / 2);

    /// <summary>The offsets of the cells within <paramref name="reach"/>, ring by ring, each ring row by row.</summary>
    private static List<CellPos> Rings(int reach)
    {
        var offsets = new List<CellPos>();
        for (var ring = 0; ring <= reach; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring)
                    {
                        offsets.Add(new CellPos(dx, dy));
                    }
                }
            }
        }

        return offsets;
    }

    private static long Square(int value) => (long)value * value;
}
