using System.Collections.Generic;
using Rts.AI.Profiles;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The scouting of a bot (docs/implementation/M5_first_bot.md, 4.6, step 7). One unit scouts — the cheapest fighting unit of the bot, a
/// conscript of the start at first, another when it falls — and its id goes into <see cref="AiPlan.Scout"/>, so that the squads leave it.
/// <list type="bullet">
/// <item>Until the bot has seen an enemy, the scout walks to the starts where the enemy may be, the nearest to its own start first; the
/// memory strikes a start off as it is explored.</item>
/// <item>Then once in <see cref="AiSettings.ScoutingInterval"/> it goes out to up to <see cref="PlacesAnOuting"/> places one after another,
/// each the unexplored block of <see cref="AiSettings.ScoutBlockCells"/> cells nearest the base, so that far ore fields are found; with
/// all explored, the enemy building the bot has not seen for the longest, older than two minutes. After the last it turns home.</item>
/// <item>An armed enemy within <see cref="AiSettings.ScoutDangerCells"/> of it, or within the reach of its weapon and
/// <see cref="ReachMarginCells"/> more, makes it run home and drop its errand for a while.</item>
/// <item>A scout lost is not replaced at once (M8 step 10): the next goes out after <see cref="AiSettings.ScoutingInterval"/>. Before, every
/// scout that fell took the next cheapest fighter of the army, and a bot of «Medium» fed four to seven of them to the enemy base in the
/// first quarter of an hour.</item>
/// </list>
/// By the level (M8, 4.6, step 6, <see cref="AiSettings.Scouting"/>): «Easy» has no scout at all — its cheapest fighter stays in a squad;
/// «Medium» scouts as above; «Hard» keeps its scout out — after a place the next one at once, after the unexplored blocks the field of ore
/// it has not seen for the longest, then the enemy buildings; «Brutal» looks at the fields far from its own base first — where an enemy
/// would build a second refinery.
/// The same order is never given twice: a command the bot gives always changes something.
/// </summary>
public sealed class ScoutManager(AiSettings settings) : IAiModule
{
    /// <summary>An enemy building not seen for this long is worth a look, in ticks.</summary>
    public const int StaleTicks = 1800;

    /// <summary>A scout keeps this many cells out of the reach of an armed enemy's weapon (M8 step 10).</summary>
    public const int ReachMarginCells = 2;

    /// <summary>The scout is there when it stands this near its goal, in cells.</summary>
    public const int ArrivedCells = 2;

    private readonly List<(CellPos Cell, int Distance)> _blocks = new();

    /// <summary>The places an outing looks at, one after another, before it turns home.</summary>
    public const int PlacesAnOuting = 4;

    private readonly List<(CellPos Cell, int SeenAt, int Distance)> _fields = new();
    private int[]? _seenAt;
    private CellPos? _ordered;
    private CellPos? _errand;
    private int _nextOuting;
    private int _restUntil;
    private int _places;

    public AiModuleKind Kind => AiModuleKind.Scouting;

    /// <summary>Where the scout goes now; null when it waits at home.</summary>
    internal CellPos? Errand => _errand;

    public void Update(AiContext context)
    {
        if (settings.Scouting == ScoutingLevel.None)
        {
            return;
        }

        MarkSeen(context.View);
        Scouting(context);
        context.Plan.ScoutErrand = _errand;
    }

    /// <summary>Notes the tick of every block whose middle is in sight now (step 6): how old the bot's look at a field is.</summary>
    private void MarkSeen(AiWorldView view)
    {
        var size = settings.ScoutBlockCells;
        var (columns, rows) = ((view.Width + size - 1) / size, (view.Height + size - 1) / size);
        if (_seenAt is null || _seenAt.Length != columns * rows)
        {
            _seenAt = new int[columns * rows];
            System.Array.Fill(_seenAt, -1);
        }

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                if (view.IsVisible(new CellPos(column * size + size / 2, row * size + size / 2)))
                {
                    _seenAt[row * columns + column] = view.Tick;
                }
            }
        }
    }

    private void Scouting(AiContext context)
    {
        var view = context.View;
        if (context.Plan.Scout is { } last && !Alive(view, last))
        {
            // Lost: no other unit leaves the army until the next outing.
            (context.Plan.Scout, _errand, _restUntil) = (null, null, context.Tick + settings.ScoutingInterval);
            context.Log?.Add(context.Tick, context.Module, $"the scout #{last.Value} is lost; the next one at the next outing, in {settings.ScoutingInterval / 15} s");
        }

        if (context.Tick < _restUntil && context.Plan.Scout is null && context.Memory.EnemySeen)
        {
            return;
        }

        var scout = Scout(context);
        if (scout is null)
        {
            return;
        }

        var here = scout.Position.ToCell();
        var home = view.Start ?? here;
        if (InDanger(context, scout))
        {
            if (_errand is not null)
            {
                context.Log?.Add(context.Tick, context.Module, $"the scout #{scout.Id.Value} runs home from an armed enemy in reach");
            }

            _errand = null;
            _restUntil = context.Tick + settings.ScoutingInterval / 3;
            Order(context, scout, home);
            return;
        }

        if (context.Tick < _restUntil)
        {
            return;
        }

        if (!context.Memory.EnemySeen && context.Memory.PossibleEnemyStarts.Count > 0)
        {
            _errand = Nearest(context.Memory.PossibleEnemyStarts, home);
            Order(context, scout, _errand.Value);
            return;
        }

        if (_errand is { } goal && (Distance(here, goal) <= ArrivedCells || view.IsVisible(goal)))
        {
            // There: on to the next place of the outing, or home after the last, and out again in a while.
            _errand = settings.Scouting >= ScoutingLevel.Constant || ++_places < PlacesAnOuting ? NextErrand(context) : null;
            _nextOuting = context.Tick + settings.ScoutingInterval;
        }

        // A squad waits to know the enemy (M8 step 10): a look at its base at once — where its core was seen, else where it likely is.
        if (_errand is null && context.Plan.LookAtTheEnemy && (context.Memory.CoreSeenCell ?? context.Memory.LikelyEnemyCell()) is { } enemy)
        {
            (_errand, _places) = (enemy, PlacesAnOuting - 1);
            _nextOuting = context.Tick + settings.ScoutingInterval;
            context.Log?.Add(context.Tick, context.Module, $"the scout #{scout.Id.Value} looks at the enemy base at {enemy.X},{enemy.Y}: a squad waits to know the enemy");
        }

        // With the known ore gone, out at once: a far field is the only income left (step 14).
        if (_errand is null
            && (context.Tick >= _nextOuting || !context.Memory.EnemySeen || context.Plan.OreGone || settings.Scouting >= ScoutingLevel.Constant))
        {
            _errand = NextErrand(context);
            _places = 0;
            _nextOuting = context.Tick + settings.ScoutingInterval;
            if (_errand is { } outing && context.Memory.EnemySeen)
            {
                context.Log?.Add(context.Tick, context.Module, $"the scout #{scout.Id.Value} goes out to {outing.X},{outing.Y}");
            }
        }

        Order(context, scout, _errand ?? home);
    }

    /// <summary>The scout of the bot: the one it has while it lives, else the cheapest unit of the army (M11: no ship); null for none.</summary>
    private static Unit? Scout(AiContext context)
    {
        var view = context.View;
        Unit? cheapest = null;
        var cheapestCost = int.MaxValue;
        foreach (var unit in view.OwnUnits)
        {
            if (unit.Id == context.Plan.Scout)
            {
                return unit;
            }

            if (!context.Units.InArmy(unit))
            {
                continue;
            }

            var cost = view.Rules.Database.Units[view.Rules.Database.IndexOfUnit(unit.Archetype.Id)].Cost;
            if (cost < cheapestCost)
            {
                (cheapest, cheapestCost) = (unit, cost);
            }
        }

        context.Plan.Scout = cheapest?.Id;
        return cheapest;
    }

    private static bool Alive(AiWorldView view, Rts.Simulation.World.EntityId id)
    {
        foreach (var unit in view.OwnUnits)
        {
            if (unit.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether an armed enemy the bot sees stands within the danger distance of the scout: <see cref="AiSettings.ScoutDangerCells"/>, or
    /// the reach of the enemy's longest weapon and <see cref="ReachMarginCells"/> more — a tesla coil kills from farther than 6 cells.
    /// </summary>
    private bool InDanger(AiContext context, Unit scout)
    {
        var here = scout.Position.ToCell();
        var rules = context.View.Rules;
        foreach (var enemy in context.View.VisibleEnemies)
        {
            var armed = enemy switch
            {
                Unit unit => unit.Archetype.Armed,
                Building building => building.Weapons.Length > 0,
                _ => false,
            };
            if (!armed)
            {
                continue;
            }

            var reach = SquadManager.RangeOf(rules, SquadManager.WeaponsOf(rules, enemy)).ToIntFloor() + 1;
            var at = enemy is Building b ? b.Cell : ((Unit)enemy).Position.ToCell();
            if (Distance(here, at) <= IntMath.Max(settings.ScoutDangerCells, reach + ReachMarginCells))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Where to look next: the middle of the unexplored block nearest the bot's base — or, as often as the level errs, one of the three
    /// nearest (M8 step 3); with every block explored, for «Hard» and «Brutal» the field not seen for the longest (<see cref="Field"/>,
    /// step 6), then the enemy building not seen for the longest, if that is older than <see cref="StaleTicks"/>. Null for nowhere.
    /// </summary>
    private CellPos? NextErrand(AiContext context)
    {
        var view = context.View;
        var memory = context.Memory;
        var home = view.Start ?? new CellPos(view.Width / 2, view.Height / 2);
        var size = settings.ScoutBlockCells;
        _blocks.Clear();
        for (var y = size / 2; y < view.Height; y += size)
        {
            for (var x = size / 2; x < view.Width; x += size)
            {
                var middle = new CellPos(x, y);
                if (view.StateOf(middle) == CellVisibility.Shroud && !view.IsScenery(middle, size))
                {
                    _blocks.Add((middle, Distance(middle, home)));
                }
            }
        }

        if (_blocks.Count > 0)
        {
            // The nearest first; of two as near, the first in the walk above — north to south, west to east.
            _blocks.Sort((a, b) => a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
                : a.Cell.Y != b.Cell.Y ? a.Cell.Y.CompareTo(b.Cell.Y) : a.Cell.X.CompareTo(b.Cell.X));
            return context.Choose(_blocks).Cell;
        }

        if (settings.Scouting >= ScoutingLevel.Constant && Field(context, stale: true) is { } field)
        {
            return field;
        }

        RememberedBuilding? stalest = null;
        foreach (var building in memory.Buildings)
        {
            if (context.Tick - building.SeenTick >= StaleTicks && (stalest is null || building.SeenTick < stalest.SeenTick))
            {
                stalest = building;
            }
        }

        // A scout that never goes home has always somewhere to be: the field seen longest ago, however lately that was.
        return stalest?.Cell ?? (settings.Scouting >= ScoutingLevel.Constant ? Field(context, stale: false) : null);
    }

    /// <summary>
    /// «Hard» and «Brutal» (step 6): the middle of an explored block with ore, the one seen longest ago — not seen for
    /// <see cref="StaleTicks"/> when <paramref name="stale"/> — of two as old the nearer the enemy the bot knows of. «Brutal» only the
    /// fields farther than <see cref="ExpansionBlocks"/> blocks from its own buildings, where an enemy expands, while there are such. Or,
    /// as often as the level errs, one of the three first. Null for none.
    /// </summary>
    private CellPos? Field(AiContext context, bool stale)
    {
        var view = context.View;
        var size = settings.ScoutBlockCells;
        var columns = (view.Width + size - 1) / size;
        var enemy = context.Memory.LikelyEnemyCell() ?? new CellPos(view.Width / 2, view.Height / 2);
        _fields.Clear();
        for (var y = size / 2; y < view.Height; y += size)
        {
            for (var x = size / 2; x < view.Width; x += size)
            {
                var middle = new CellPos(x, y);
                var seenAt = _seenAt![y / size * columns + x / size];
                if ((stale && seenAt >= 0 && context.Tick - seenAt < StaleTicks) || !HasOre(view, middle, size))
                {
                    continue;
                }

                _fields.Add((middle, seenAt, Distance(middle, enemy)));
            }
        }

        if (settings.Scouting == ScoutingLevel.ConstantExpansions && _fields.Exists(field => FarFromBase(view, field.Cell)))
        {
            _fields.RemoveAll(field => !FarFromBase(view, field.Cell));
        }

        if (_fields.Count == 0)
        {
            return null;
        }

        _fields.Sort((a, b) => a.SeenAt != b.SeenAt ? a.SeenAt.CompareTo(b.SeenAt)
            : a.Distance != b.Distance ? a.Distance.CompareTo(b.Distance)
            : a.Cell.Y != b.Cell.Y ? a.Cell.Y.CompareTo(b.Cell.Y) : a.Cell.X.CompareTo(b.Cell.X));
        return context.Choose(_fields).Cell;
    }

    /// <summary>A field this many blocks from every building of the bot is far from its base: one an enemy may take (step 6).</summary>
    public const int ExpansionBlocks = 2;

    private bool FarFromBase(AiWorldView view, CellPos cell)
    {
        foreach (var building in view.OwnBuildings)
        {
            if (Distance(cell, building.Cell) <= ExpansionBlocks * settings.ScoutBlockCells)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Whether the block of <paramref name="size"/> cells round <paramref name="middle"/> has ore the bot knows of.</summary>
    private static bool HasOre(AiWorldView view, CellPos middle, int size)
    {
        for (var y = middle.Y - size / 2; y < middle.Y + size / 2; y++)
        {
            for (var x = middle.X - size / 2; x < middle.X + size / 2; x++)
            {
                if (!view.OreAt(new CellPos(x, y)).IsEmpty)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>Sends the scout to <paramref name="cell"/>, unless that is where it was sent last and it still goes or stands there.</summary>
    private void Order(AiContext context, Unit scout, CellPos cell)
    {
        var here = scout.Position.ToCell();
        if ((_ordered == cell && (scout.MoveTarget is not null || Distance(here, cell) <= ArrivedCells)) || here == cell)
        {
            return;
        }

        _ordered = cell;
        context.Issue(new MoveCommand(context.View.Me, context.ExecuteTick, context.NextSequence(), [scout.Id], cell.Center));
    }

    private static CellPos Nearest(System.Collections.Generic.IReadOnlyList<CellPos> cells, CellPos to)
    {
        var best = cells[0];
        foreach (var cell in cells)
        {
            if (Distance(cell, to) < Distance(best, to))
            {
                best = cell;
            }
        }

        return best;
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
