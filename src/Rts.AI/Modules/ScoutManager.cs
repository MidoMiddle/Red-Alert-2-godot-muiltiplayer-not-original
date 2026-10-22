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
/// <item>An armed enemy within <see cref="AiSettings.ScoutDangerCells"/> of it makes it run home and drop its errand for a while.</item>
/// </list>
/// The same order is never given twice: a command the bot gives always changes something.
/// </summary>
public sealed class ScoutManager(AiSettings settings) : IAiModule
{
    /// <summary>An enemy building not seen for this long is worth a look, in ticks.</summary>
    public const int StaleTicks = 1800;

    /// <summary>The scout is there when it stands this near its goal, in cells.</summary>
    public const int ArrivedCells = 2;

    /// <summary>The places an outing looks at, one after another, before it turns home.</summary>
    public const int PlacesAnOuting = 4;

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
        var view = context.View;
        var scout = Scout(context);
        if (scout is null)
        {
            return;
        }

        var here = scout.Position.ToCell();
        var home = view.Start ?? here;
        if (InDanger(context, scout))
        {
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
            _errand = ++_places < PlacesAnOuting ? NextErrand(context) : null;
            _nextOuting = context.Tick + settings.ScoutingInterval;
        }

        if (_errand is null && (context.Tick >= _nextOuting || !context.Memory.EnemySeen))
        {
            _errand = NextErrand(context);
            _places = 0;
            _nextOuting = context.Tick + settings.ScoutingInterval;
        }

        Order(context, scout, _errand ?? home);
    }

    /// <summary>The scout of the bot: the one it has while it lives, else the cheapest fighting unit that is no harvester; null for none.</summary>
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

            if (!unit.Archetype.Armed || unit.Harvester is not null)
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

    /// <summary>Whether an armed enemy the bot sees stands within the danger distance of the scout.</summary>
    private bool InDanger(AiContext context, Unit scout)
    {
        var here = scout.Position.ToCell();
        foreach (var enemy in context.View.VisibleEnemies)
        {
            var armed = enemy switch
            {
                Unit unit => unit.Archetype.Armed,
                Building building => building.Weapons.Length > 0,
                _ => false,
            };
            var at = enemy is Building b ? b.Cell : ((Unit)enemy).Position.ToCell();
            if (armed && Distance(here, at) <= settings.ScoutDangerCells)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Where to look next: the middle of the unexplored block nearest the bot's base; with every block explored, the enemy building not
    /// seen for the longest, if that is older than <see cref="StaleTicks"/>. Null for nowhere.
    /// </summary>
    private CellPos? NextErrand(AiContext context)
    {
        var view = context.View;
        var memory = context.Memory;
        var home = view.Start ?? new CellPos(view.Width / 2, view.Height / 2);
        var size = settings.ScoutBlockCells;
        CellPos? best = null;
        var bestDistance = int.MaxValue;
        for (var y = size / 2; y < view.Height; y += size)
        {
            for (var x = size / 2; x < view.Width; x += size)
            {
                var middle = new CellPos(x, y);
                if (view.StateOf(middle) == CellVisibility.Shroud && Distance(middle, home) < bestDistance)
                {
                    (best, bestDistance) = (middle, Distance(middle, home));
                }
            }
        }

        if (best is not null)
        {
            return best;
        }

        RememberedBuilding? stalest = null;
        foreach (var building in memory.Buildings)
        {
            if (context.Tick - building.SeenTick >= StaleTicks && (stalest is null || building.SeenTick < stalest.SeenTick))
            {
                stalest = building;
            }
        }

        return stalest?.Cell;
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
