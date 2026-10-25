using System.Collections.Generic;
using System.Collections.Immutable;
using Rts.AI.View;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
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
    /// <summary>The ore within this of a refinery is what its harvesters are bought for, in cells (M8 step 10).</summary>
    public const int OreReachCells = 24;

    /// <summary>The ore within reach that pays for one more harvester, in its costs (M8 step 10).</summary>
    public const int OrePerHarvester = 4;

    private readonly List<int> _wants = new();
    private readonly List<(CellPos Cell, int Distance)> _fields = new();
    private int _mostRefineries;
    private int? _soldAt;
    private int? _idleSince;
    private bool _sawOre;

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

        // A refinery still growing or in the queue brings its own harvester: not to be ordered as well (step 14, the factory first).
        foreach (var building in view.OwnBuildings)
        {
            harvesters += building.DefIndex == refinery && !building.IsReady ? 1 : 0;
        }

        foreach (var item in buildingQueue)
        {
            harvesters += item.DefIndex == refinery ? 1 : 0;
        }

        // Gone only once seen: at the start the field may lie outside the sight of the base.
        var (ore, near) = OreNear(context, refinery);
        var anyOre = ore > 0;
        _sawOre |= anyOre;
        var oreGone = _sawOre && !anyOre;
        if (oreGone && !context.Plan.OreGone)
        {
            context.Log?.Add(context.Tick, context.Module, "the ore on the bot's side of the map is gone: no harvesters bought, spare buildings sold for the army");
        }

        context.Plan.OreGone = oreGone;
        var target = oreGone ? 0 : settings.HarvestersPerRefinery * IntMath.Max(refineries, 1);
        if (settings.HarvestersByOre && !oreGone && harvester >= 0)
        {
            // By the ore (M8 step 10): a harvester for every OrePerHarvester times its cost within OreReachCells of the refineries, one
            // at least. The ore of a map is soon mined out; a harvester more brings it in sooner, and its 1 400 are missing in the army
            // that decides the match.
            var cost = (long)rules.Units[harvester].Cost * OrePerHarvester;
            target = IntMath.Min(target, IntMath.Max(1, (int)(near / cost)));
        }
        _wants.Clear();
        var restore = refineries < _mostRefineries && !oreGone;
        if (restore || (_mostRefineries == 0 && !oreGone))
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
        if (oreGone)
        {
            SellForTheArmy(context);
        }
        else
        {
            _idleSince = null;
        }
    }

    /// <summary>
    /// Whether the bot knows of any ore — an explored cell with ore or gems. Without, the base has no more income: it is done
    /// (<see cref="AiPlan.OreGone"/>).
    /// </summary>
    /// <summary>
    /// The credits of the explored ore on the bot's side of the map (M8 step 10): nearer one of its refineries than any enemy building it
    /// knows of and any start where the enemy may be — all of it before the first refinery. The ore by the enemy base, found by a scout, is
    /// no income the bot can count on: its harvesters die on the way, and while it saw some the bot never took the ore for gone. And of it
    /// the ore within <see cref="OreReachCells"/> of a refinery, what the harvesters are bought for.
    /// </summary>
    private static (long Side, long Near) OreNear(AiContext context, int refinery)
    {
        var view = context.View;
        var (side, near) = (0L, 0L);
        for (var y = 0; y < view.Height; y++)
        {
            for (var x = 0; x < view.Width; x++)
            {
                var cell = new CellPos(x, y);
                var ore = view.OreAt(cell);
                if (ore.Kind == OreKind.None)
                {
                    continue;
                }

                var ours = OurDistance(view, cell, refinery);
                if (OnOurSide(context, cell, ours))
                {
                    side += view.Rules.CreditsOf(ore);
                    near += ours <= OreReachCells ? view.Rules.CreditsOf(ore) : 0;
                }
            }
        }

        return (side, near);
    }

    /// <summary>The distance of <paramref name="cell"/> from the nearest refinery of the bot; <see cref="int.MaxValue"/> with none.</summary>
    private static int OurDistance(AiWorldView view, CellPos cell, int refinery)
    {
        var ours = int.MaxValue;
        foreach (var building in view.OwnBuildings)
        {
            if (building.DefIndex == refinery)
            {
                ours = IntMath.Min(ours, Distance(cell, building.Cell));
            }
        }

        return ours;
    }

    private static bool OnOurSide(AiContext context, CellPos cell, int ours)
    {
        if (ours == int.MaxValue)
        {
            return true;
        }

        foreach (var building in context.Memory.Buildings)
        {
            if (Distance(cell, building.Cell) < ours)
            {
                return false;
            }
        }

        foreach (var start in context.Memory.PossibleEnemyStarts)
        {
            if (Distance(cell, start) < ours)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// With the ore gone and the factories idle for <see cref="AiSettings.SellTicks"/> — the army no longer paid for — sells a building
    /// the base can do without: a refinery, then what only opens technologies (a radar, a tech centre); one in
    /// <see cref="AiSettings.SellTicks"/>, so that the money goes to the army. The yard, the factories, the power plants and the
    /// defenses are kept.
    /// </summary>
    private void SellForTheArmy(AiContext context)
    {
        var view = context.View;
        var idle = view.Player.Queue(ProductionCategory.Infantry).Count == 0 && view.Player.Queue(ProductionCategory.Vehicle).Count == 0;
        _idleSince = idle ? _idleSince ?? context.Tick : null;
        if (_idleSince is not { } since || context.Tick - since < settings.SellTicks || context.Tick - _soldAt < settings.SellTicks)
        {
            return;
        }

        Building? sell = null;
        foreach (var building in view.OwnBuildings)
        {
            if (!building.IsReady || building.Def is not { } def || !Spare(def))
            {
                continue;
            }

            // Refineries first: without ore they are no use at all.
            if (sell is null || (def.DockCell is not null && sell.Def!.DockCell is null))
            {
                sell = building;
            }
        }

        if (sell is not null)
        {
            _soldAt = context.Tick;
            context.Issue(new SellCommand(view.Me, context.ExecuteTick, context.NextSequence(), sell.Id));
            context.Log?.Add(context.Tick, context.Module, $"sells {sell.Def!.Id}: the ore is gone and the factories stood idle");
        }
    }

    /// <summary>A building the base can do without once the ore is gone: neither a factory nor the yard, power or a defense.</summary>
    internal static bool Spare(BuildingDef def) =>
        def.Produces.Length == 0 && def.Power <= 0 && def.Primary.Length == 0 && !def.Wall;

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
            if (building.DefIndex >= 0 && buildings[building.DefIndex].Primary.Length > 0 && Distance(cell, building.Cell) < settings.DangerCells)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// The explored ore nearest the bot's refineries that no known enemy is near — or, as often as the level errs, one of the three nearest
    /// (M8 step 3); null without one.
    /// </summary>
    private CellPos? SafeField(AiContext context, int refinery)
    {
        var view = context.View;
        _fields.Clear();
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

                // The three nearest, in order; of two as near, the first in the walk — north to south, west to east.
                var at = _fields.Count;
                while (at > 0 && distance < _fields[at - 1].Distance)
                {
                    at--;
                }

                if (at < AiChoice.Top && distance != int.MaxValue)
                {
                    _fields.Insert(at, (cell, distance));
                    if (_fields.Count > AiChoice.Top)
                    {
                        _fields.RemoveAt(AiChoice.Top);
                    }
                }
            }
        }

        return _fields.Count == 0 ? null : context.Choose(_fields).Cell;
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
