using System.Collections.Generic;
using Rts.AI.Squads;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The strikes of a bot (M11 step 3, the bot that knows every weapon): its units that fire only when ordered
/// (<see cref="Knowledge.UnitUse.Striker"/>) — the jets, the rocket artillery, the dreadnought and the carrier, the bombers on foot and on
/// wheels, Yuri Prime — are ordered at their targets; by themselves they would never fire.
/// <list type="bullet">
/// <item>The target is the enemy in sight the unit can hurt (<see cref="TargetingSystem.CanHurt"/>) that is worth the most — its cost by
/// the share of the damage the unit does to it — within its reach: the reach of its weapon and <see cref="ReachMargin"/> cells more, a
/// jet's <see cref="JetReachCells"/>; of as many the nearest. An attack order needs the enemy in sight, so the strikes go where the bot
/// sees: at the base under attack, at the enemy a squad has found.</item>
/// <item>A jet goes only with its ammo full: out of it, it flies home to load and comes again.</item>
/// <item>A unit on the ground with nothing to strike follows the squad that is out nearest to it, to within <see cref="FollowCells"/>; the
/// ships go with the fleet (<see cref="FleetManager"/>).</item>
/// </list>
/// A unit busy with an attack on an enemy still in sight is left to it; no order is given twice.
/// </summary>
public sealed class StrikeManager : IAiModule
{
    /// <summary>A target this much farther than the weapon reaches, in cells, is still struck: the unit closes in.</summary>
    public const int ReachMargin = 3;

    /// <summary>How far a jet flies to strike, in cells.</summary>
    public const int JetReachCells = 48;

    /// <summary>A unit this near the middle of the squad it follows, in cells, is with it.</summary>
    public const int FollowCells = 6;

    private readonly SortedDictionary<int, CellPos> _follows = new();
    private readonly HashSet<int> _visible = new();

    public AiModuleKind Kind => AiModuleKind.Strikes;

    public void Update(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules;
        _visible.Clear();
        foreach (var enemy in view.VisibleEnemies)
        {
            _visible.Add(enemy.Id.Value);
        }

        foreach (var unit in view.OwnUnits)
        {
            if (!context.Units.InStrikes(unit)
                || (unit.Target is { } busy && unit.TargetOrder == TargetOrder.Attack && _visible.Contains(busy.Value)))
            {
                continue;
            }

            var def = rules.Database.Units[rules.Database.IndexOfUnit(unit.Archetype.Id)];
            var jet = unit.Archetype.Category == UnitCategory.Aircraft && def.Ammo > 0;
            if (jet && (unit.Ammo < def.Ammo || unit.MoveTarget is not null))
            {
                continue;
            }

            var range = SquadManager.RangeOf(rules, rules.WeaponsOf(unit.Type)).ToIntFloor();
            if (Best(context, unit, jet ? JetReachCells : range + ReachMargin) is { } target)
            {
                _follows.Remove(unit.Id.Value);
                context.Issue(new AttackCommand(view.Me, context.ExecuteTick, context.NextSequence(), [unit.Id], target.Id));
                context.Log?.Add(context.Tick, context.Module, $"sends {unit.Archetype.Id} at {Name(rules, target)}");
                continue;
            }

            if (!jet && unit.Archetype.Category != UnitCategory.Naval)
            {
                Follow(context, unit);
            }
        }
    }

    /// <summary>The enemy in sight worth the most to strike within <paramref name="reach"/> of <paramref name="unit"/>; null for none.</summary>
    private static Entity? Best(AiContext context, Unit unit, int reach)
    {
        var rules = context.View.Rules;
        var here = unit.Position.ToCell();
        Entity? best = null;
        var (bestWorth, bestDistance) = (0L, int.MaxValue);
        foreach (var enemy in context.View.VisibleEnemies)
        {
            if (enemy is Building { Def.Wall: true })
            {
                continue;
            }

            var distance = Distance(here, enemy is Building building ? Nearest(building, here) : ((Unit)enemy).Position.ToCell());
            var share = distance <= reach ? TargetingSystem.ShareAgainst(rules, unit.Type, enemy) : 0;
            if (share <= 0)
            {
                continue;
            }

            var worth = (long)CostOf(context, enemy) * share;
            if (worth > bestWorth || (worth == bestWorth && distance < bestDistance))
            {
                (best, bestWorth, bestDistance) = (enemy, worth, distance);
            }
        }

        return best;
    }

    /// <summary>A unit on the ground follows the nearest squad that is out, once it is farther than <see cref="FollowCells"/>.</summary>
    private void Follow(AiContext context, Unit unit)
    {
        var here = unit.Position.ToCell();
        Squad? nearest = null;
        foreach (var squad in context.Plan.Squads)
        {
            if (squad.State is SquadState.Moving or SquadState.Fighting && squad.Units.Count > 0
                && (nearest is null || Distance(here, squad.Centre) < Distance(here, nearest.Centre)))
            {
                nearest = squad;
            }
        }

        if (nearest is null || Distance(here, nearest.Centre) <= FollowCells
            || (unit.MoveTarget is not null && _follows.TryGetValue(unit.Id.Value, out var going) && Distance(going, nearest.Centre) <= FollowCells))
        {
            return;
        }

        _follows[unit.Id.Value] = nearest.Centre;
        context.Issue(new MoveCommand(context.View.Me, context.ExecuteTick, context.NextSequence(), [unit.Id], nearest.Centre.Center));
    }

    private static int CostOf(AiContext context, Entity enemy)
    {
        var database = context.View.Rules.Database;
        return enemy switch
        {
            Building { Def: { } def } => def.Cost,
            Building => 1000,
            Unit unit => database.Units[database.IndexOfUnit(unit.Archetype.Id)].Cost,
            _ => 0,
        };
    }

    private static string Name(Rts.Simulation.Rules.GameRules rules, Entity enemy) => enemy switch
    {
        Building { Def: { } def } => def.Id,
        Building building => building.Kind.ToString(),
        Unit unit => unit.Archetype.Id,
        _ => "?",
    };

    /// <summary>The cell of the footprint nearest <paramref name="cell"/>.</summary>
    private static CellPos Nearest(Building building, CellPos cell) => new(
        IntMath.Clamp(cell.X, building.Cell.X, building.FarCell.X), IntMath.Clamp(cell.Y, building.Cell.Y, building.FarCell.Y));

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
