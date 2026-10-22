using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI.Modules;

/// <summary>
/// The economy of a bot (docs/implementation/M5_first_bot.md, 4.6, step 4). On its turn it says which buildings the economy needs,
/// most important first, into <see cref="AiPlan.Wants"/>:
/// <list type="number">
/// <item>a refinery that was lost — there are fewer than there ever were — or the first one;</item>
/// <item>a war factory when harvesters are missing and there is none to make them;</item>
/// <item>a reactor — first of all when the next of these would leave the base without power, after a lost refinery when what is left
/// of the power, the buildings in production and growing counted, is below <see cref="AiSettings.PowerReservePercent"/> of the
/// output.</item>
/// </list>
/// It orders harvesters itself, one at a time, up to <see cref="AiSettings.HarvestersPerRefinery"/> a refinery, and sends a harvester
/// that works within <see cref="AiSettings.DangerCells"/> of a known enemy to the explored ore nearest its refineries that lies farther.
/// </summary>
public sealed class EconomyManager(AiSettings settings) : IAiModule
{
    private readonly List<int> _wants = new();
    private int _mostRefineries;

    public AiModuleKind Kind => AiModuleKind.Economy;

    public void Update(AiContext context)
    {
        var view = context.View;
        var rules = view.Rules.Database;
        var faction = view.Player.Faction;
        var refinery = rules.IndexOfBuilding(faction.Refinery);
        var reactor = PowerPlant(rules, faction.Id);
        var harvester = rules.IndexOfUnit(faction.Harvester);
        var factory = Factory(rules, faction.Id, ProductionCategory.Vehicle);

        var refineries = 0;
        var factories = 0;
        var (output, drain) = (0, 0);
        foreach (var building in view.OwnBuildings)
        {
            refineries += building.DefIndex == refinery ? 1 : 0;
            factories += building.DefIndex == factory ? 1 : 0;
            Add(building.Def, ref output, ref drain);
        }

        var buildingQueue = view.Player.Queue(ProductionCategory.Building);
        foreach (var item in buildingQueue)
        {
            refineries += item.DefIndex == refinery ? 1 : 0;
            factories += item.DefIndex == factory ? 1 : 0;
            Add(rules.Buildings[item.DefIndex], ref output, ref drain);
        }

        _mostRefineries = IntMath.Max(_mostRefineries, refineries);
        var harvesters = 0;
        foreach (var unit in view.OwnUnits)
        {
            harvesters += unit.Harvester is not null ? 1 : 0;
        }

        var vehicleQueue = view.Player.Queue(ProductionCategory.Vehicle);
        foreach (var item in vehicleQueue)
        {
            harvesters += item.Kind == ProductionKind.Unit && item.DefIndex == harvester ? 1 : 0;
        }

        var target = settings.HarvestersPerRefinery * IntMath.Max(refineries, 1);
        _wants.Clear();
        var restore = refineries < _mostRefineries;
        if (restore || _mostRefineries == 0)
        {
            _wants.Add(refinery);
        }

        if (harvesters < target && factories == 0 && factory >= 0 && refineries > 0)
        {
            _wants.Add(factory);
        }

        // A reactor when the reserve is low; first of all when the next building would leave the base without power.
        var next = _wants.Count > 0 ? IntMath.Max(0, -rules.Buildings[_wants[0]].Power) : 0;
        if (reactor >= 0 && output - drain - next < 0)
        {
            _wants.Insert(0, reactor);
        }
        else if (reactor >= 0 && (output - drain) * 100 < output * settings.PowerReservePercent)
        {
            _wants.Insert(restore ? 1 : 0, reactor);
        }

        context.Plan.SetEconomyWants(_wants);
        context.Plan.HarvestersWanted = harvesters < target && view.HasFactory(ProductionCategory.Vehicle) && view.Player.CanBuildUnit(harvester);

        if (harvesters < target && view.HasFactory(ProductionCategory.Vehicle) && view.Player.CanBuildUnit(harvester)
            && vehicleQueue.Count < ProductionRules.MaxUnitQueue)
        {
            context.Issue(new ProduceCommand(view.Me, context.ExecuteTick, context.NextSequence(), ProductionKind.Unit, harvester, 1));
        }

        KeepHarvestersSafe(context, refinery);
    }

    /// <summary>Sends every harvester that works near a known enemy to the explored ore nearest the refineries that is far enough.</summary>
    private void KeepHarvestersSafe(AiContext context, int refinery)
    {
        var view = context.View;
        CellPos? safe = null;
        var searched = false;
        foreach (var unit in view.OwnUnits)
        {
            if (unit.Harvester is not { } work || (work.Target ?? work.Field) is not { } cell || !NearEnemy(context, cell))
            {
                continue;
            }

            if (!searched)
            {
                safe = SafeField(context, refinery);
                searched = true;
            }

            if (safe is { } field && work.Field != field)
            {
                context.Issue(new HarvestCommand(view.Me, context.ExecuteTick, context.NextSequence(), [unit.Id], field));
            }
        }
    }

    /// <summary>Whether a known enemy — an army remembered or an armed building — is within the danger distance of <paramref name="cell"/>.</summary>
    private bool NearEnemy(AiContext context, CellPos cell)
    {
        foreach (var army in context.Memory.Armies)
        {
            if (Distance(cell, army.Centre.ToCell()) < settings.DangerCells)
            {
                return true;
            }
        }

        var buildings = context.View.Rules.Database.Buildings;
        foreach (var building in context.Memory.Buildings)
        {
            if (buildings[building.DefIndex].Primary.Length > 0 && Distance(cell, building.Cell) < settings.DangerCells)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>The explored ore nearest the bot's refineries that no known enemy is near; null without one.</summary>
    private CellPos? SafeField(AiContext context, int refinery)
    {
        var view = context.View;
        CellPos? best = null;
        var bestDistance = int.MaxValue;
        for (var y = 0; y < view.Height; y++)
        {
            for (var x = 0; x < view.Width; x++)
            {
                var cell = new CellPos(x, y);
                if (view.OreAt(cell).IsEmpty || NearEnemy(context, cell))
                {
                    continue;
                }

                var distance = int.MaxValue;
                foreach (var building in view.OwnBuildings)
                {
                    if (building.DefIndex == refinery)
                    {
                        distance = IntMath.Min(distance, Distance(cell, building.Cell));
                    }
                }

                if (distance < bestDistance)
                {
                    (best, bestDistance) = (cell, distance);
                }
            }
        }

        return best;
    }

    /// <summary>The faction's cheapest building that gives power; -1 when it has none.</summary>
    internal static int PowerPlant(RulesDatabase rules, string faction)
    {
        var best = -1;
        for (var i = 0; i < rules.Buildings.Length; i++)
        {
            var def = rules.Buildings[i];
            if (def.Faction == faction && def.Power > 0 && (best < 0 || def.Cost < rules.Buildings[best].Cost))
            {
                best = i;
            }
        }

        return best;
    }

    /// <summary>The faction's first building that produces <paramref name="category"/>; -1 when it has none.</summary>
    internal static int Factory(RulesDatabase rules, string faction, ProductionCategory category)
    {
        for (var i = 0; i < rules.Buildings.Length; i++)
        {
            var def = rules.Buildings[i];
            if (def.Faction == faction && def.Produces.Contains(category))
            {
                return i;
            }
        }

        return -1;
    }

    private static void Add(BuildingDef? def, ref int output, ref int drain)
    {
        if (def is not null)
        {
            output += IntMath.Max(0, def.Power);
            drain += IntMath.Max(0, -def.Power);
        }
    }

    private static int Distance(CellPos a, CellPos b) => IntMath.Max(IntMath.Abs(a.X - b.X), IntMath.Abs(a.Y - b.Y));
}
