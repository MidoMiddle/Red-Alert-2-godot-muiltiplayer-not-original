using System.Collections.Generic;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The base of a bot (docs/implementation/M5_first_bot.md, 4.6–4.7, steps 4–5). First the MCV deploys — where it stands, or on the nearest
/// cell within <see cref="DeployReach"/> where the yard fits as far as the bot has seen; with none seen it drives <see cref="ScoutStep"/>
/// cells toward the middle of the map to see more. Then, on every turn, a ready building is placed where <see cref="BasePlacement"/>
/// finds room, and with a queue empty the next building is ordered, the most important first:
/// <list type="number">
/// <item>what the economy needs at once — a reactor, a refinery (<see cref="AiPlan.Wants"/>);</item>
/// <item>the next step of the build order of the data (<see cref="AiSettings.BuildOrder"/>) not done yet: a step is done when the base
/// has as many such buildings, standing, growing or in production, as the order has up to it — a lost one is built again; a step that
/// cannot be built yet (its prerequisite still grows) is waited for, nothing after it goes first;</item>
/// <item>the rest of the economy's needs — a war factory for the harvesters;</item>
/// <item>after the order — refineries up to <see cref="AiSettings.MaxRefineries"/>, a second war factory once the credits reach
/// <see cref="AiSettings.SecondFactoryCredits"/>.</item>
/// </list>
/// A building is ordered only when the credits, less what the queues still have to pay, cover it; one order a queue a turn. The defense
/// on the side of the attacks comes with the defense of step 9. The bot gives no command it knows to be refused.
/// </summary>
public sealed class BuildPlanner(AiSettings settings) : IAiModule
{
    /// <summary>How far, in cells, the MCV may go to find flat ground for its yard.</summary>
    public const int DeployReach = 6;

    /// <summary>How far the MCV drives to see more ground when it found none for its yard.</summary>
    public const int ScoutStep = 4;

    private static readonly ProductionCategory[] Categories =
        [ProductionCategory.Infantry, ProductionCategory.Vehicle, ProductionCategory.Building, ProductionCategory.Defense];

    private readonly BasePlacement _placement = new();
    private readonly List<int> _choices = new();
    private int[] _have = [];

    public AiModuleKind Kind => AiModuleKind.BuildPlanner;

    public void Update(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var yardIndex = rules.IndexOfBuilding(view.Player.Faction.ConstructionYard);
        Building? yard = null;
        foreach (var building in view.OwnBuildings)
        {
            if (building.DefIndex == yardIndex)
            {
                yard = building;
                break;
            }
        }

        if (yard is null)
        {
            Deploy(context, rules.Buildings[yardIndex]);
            return;
        }

        PlaceReady(context, yard);
        Order(context);
    }

    /// <summary>The first ready building of the building queue, else of the defense queue, goes where the placement finds room.</summary>
    private void PlaceReady(AiContext context, Building yard)
    {
        var view = context.View;
        foreach (var category in new[] { ProductionCategory.Building, ProductionCategory.Defense })
        {
            if (view.Player.Queue(category) is not [{ Ready: true } ready, ..])
            {
                continue;
            }

            if (_placement.TryFind(view, context.Memory, yard, ready.DefIndex, out var corner) && corner is { } place)
            {
                context.Issue(new PlaceBuildingCommand(view.Me, context.ExecuteTick, context.NextSequence(), ready.DefIndex, place));
            }

            return;
        }
    }

    /// <summary>Orders the most important building there is room and money for, one a queue.</summary>
    private void Order(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        CountBuildings(view);
        var money = view.Player.Credits - Promised(view);
        var buildingQueueFree = view.Player.Queue(ProductionCategory.Building).Count == 0;
        var defenseQueueFree = view.Player.Queue(ProductionCategory.Defense).Count == 0;
        Choose(context);
        foreach (var index in _choices)
        {
            if (index < 0)
            {
                // A step of the order that cannot be built yet: nothing after it goes first.
                return;
            }

            var def = rules.Buildings[index];
            var free = def.Category == ProductionCategory.Defense ? defenseQueueFree : buildingQueueFree;
            if (!free || !view.Player.CanBuildBuilding(index))
            {
                continue;
            }

            if (def.Cost > money)
            {
                // The money is not there yet: wait for it rather than build something cheaper first.
                return;
            }

            context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Building, index, 1));
            money -= def.Cost;
            if (def.Category == ProductionCategory.Defense)
            {
                defenseQueueFree = false;
            }
            else
            {
                buildingQueueFree = false;
            }
        }
    }

    /// <summary>The buildings to order, the most important first; -1 marks a step of the order that has to be waited for.</summary>
    private void Choose(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var faction = view.Player.Faction;
        var refinery = rules.IndexOfBuilding(faction.Refinery);
        var reactor = EconomyManager.PowerPlant(rules, faction.Id);
        var factory = EconomyManager.Factory(rules, faction.Id, ProductionCategory.Vehicle);
        _choices.Clear();
        foreach (var wanted in context.Plan.Wants)
        {
            if (wanted == reactor || wanted == refinery)
            {
                _choices.Add(wanted);
            }
        }

        var orderDone = true;
        if (rules.TryGetBuildOrder(settings.BuildOrder, out var order) && order.Faction == faction.Id)
        {
            var counts = new int[rules.Buildings.Length];
            foreach (var step in order.Steps)
            {
                var index = rules.IndexOfBuilding(step);
                counts[index]++;
                if (_have[index] < counts[index])
                {
                    _choices.Add(view.Player.CanBuildBuilding(index) ? index : -1);
                    orderDone = false;
                    break;
                }
            }
        }

        foreach (var wanted in context.Plan.Wants)
        {
            if (wanted != reactor && wanted != refinery)
            {
                _choices.Add(wanted);
            }
        }

        if (orderDone)
        {
            if (_have[refinery] < settings.MaxRefineries)
            {
                _choices.Add(refinery);
            }

            if (factory >= 0 && _have[factory] < 2 && view.Player.Credits - Promised(view) >= settings.SecondFactoryCredits)
            {
                _choices.Add(factory);
            }
        }
    }

    /// <summary>How many of each building the base has: standing, growing, or in a queue.</summary>
    private void CountBuildings(AiWorldView view)
    {
        var buildings = view.Rules.Database.Buildings.Length;
        if (_have.Length != buildings)
        {
            _have = new int[buildings];
        }

        System.Array.Clear(_have);
        foreach (var building in view.OwnBuildings)
        {
            if (building.Def is not null)
            {
                _have[building.DefIndex]++;
            }
        }

        foreach (var category in new[] { ProductionCategory.Building, ProductionCategory.Defense })
        {
            foreach (var item in view.Player.Queue(category))
            {
                _have[item.DefIndex]++;
            }
        }
    }

    /// <summary>What the queues still have to pay.</summary>
    private static int Promised(AiWorldView view)
    {
        var promised = 0;
        foreach (var category in Categories)
        {
            foreach (var item in view.Player.Queue(category))
            {
                promised += ProductionRules.CostOf(view.Rules.Database, item.Kind, item.DefIndex) - item.Paid;
            }
        }

        return promised;
    }

    /// <summary>The MCV deploys where its yard fits, or drives to the nearest cell where it does; it waits while it drives.</summary>
    private static void Deploy(AiContext context, BuildingDef yard)
    {
        var view = context.View;
        Unit? mcv = null;
        foreach (var unit in view.OwnUnits)
        {
            if (unit.Archetype.Id == view.Player.Faction.Mcv)
            {
                mcv = unit;
                break;
            }
        }

        if (mcv is null || mcv.MoveTarget is not null)
        {
            return;
        }

        var here = mcv.Position.ToCell();
        if (view.CanPlace(yard, Corner(here, yard), needsBase: false, ignoreUnit: mcv.Id))
        {
            context.Issue(new DeployCommand(view.Me, context.ExecuteTick, context.NextSequence(), mcv.Id));
            return;
        }

        for (var ring = 1; ring <= DeployReach; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(here.X + dx, here.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && view.CanEnter(cell, mcv.Archetype.Locomotor)
                        && view.CanPlace(yard, Corner(cell, yard), needsBase: false, ignoreUnit: mcv.Id))
                    {
                        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [mcv.Id], cell.Center));
                        return;
                    }
                }
            }
        }

        // Nothing seen will do: a few cells toward the middle of the map, to the nearest cell it can enter there, to see more ground.
        var middle = new CellPos(view.Width / 2, view.Height / 2);
        var ahead = new CellPos(
            here.X + IntMath.Max(-ScoutStep, IntMath.Min(ScoutStep, middle.X - here.X)),
            here.Y + IntMath.Max(-ScoutStep, IntMath.Min(ScoutStep, middle.Y - here.Y)));
        for (var ring = 0; ring <= 2; ring++)
        {
            for (var dy = -ring; dy <= ring; dy++)
            {
                for (var dx = -ring; dx <= ring; dx++)
                {
                    var cell = new CellPos(ahead.X + dx, ahead.Y + dy);
                    if (IntMath.Max(IntMath.Abs(dx), IntMath.Abs(dy)) == ring && cell != here && view.CanEnter(cell, mcv.Archetype.Locomotor))
                    {
                        context.Issue(new MoveCommand(view.Me, context.ExecuteTick, context.NextSequence(), [mcv.Id], cell.Center));
                        return;
                    }
                }
            }
        }
    }

    /// <summary>The north-west cell of <paramref name="def"/> centred on <paramref name="centre"/>, as a deploy places the yard.</summary>
    private static CellPos Corner(CellPos centre, BuildingDef def) =>
        new(centre.X - (def.Footprint.Width - 1) / 2, centre.Y - (def.Footprint.Height - 1) / 2);
}
