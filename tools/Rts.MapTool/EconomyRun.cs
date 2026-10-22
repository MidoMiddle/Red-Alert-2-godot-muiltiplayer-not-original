using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Rts.Data.Maps;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.MapTool;

/// <summary>One line of the timeline: when, and what happened.</summary>
public sealed record EconomyEvent(int Tick, string Text);

/// <summary>The economy at the end of a minute: credits, what the harvesters brought in that minute, how many there were, power.</summary>
public sealed record EconomyMinute(int Minute, int Credits, int Income, int Harvesters, int PowerOutput, int PowerDrain, string Modes);

/// <summary>What a run found: the timeline, the minutes, and the ticks of the first and the last tank.</summary>
public sealed record EconomyReport(
    IReadOnlyList<EconomyEvent> Timeline,
    IReadOnlyList<EconomyMinute> Minutes,
    IReadOnlyList<int> TankTicks,
    int Unloads,
    int Income,
    int HarvesterTicks,
    int LowestCredits,
    GameSimulation Simulation)
{
    /// <summary>Credits a harvester brought in per minute of being there, on average.</summary>
    public int IncomePerHarvesterMinute =>
        HarvesterTicks == 0 ? 0 : (int)((long)Income * 60 * Simulation.Settings.TicksPerSecond / HarvesterTicks);
}

/// <summary>
/// The end-to-end run of M3 (docs/implementation/M3_base_economy.md, step 14, section 6): a scripted player on the real data and a
/// real map, the way a person plays the first minutes of RA2 — deploy the MCV, reactor, refinery, barracks, war factory, a reactor
/// more when the power gets tight, then tanks one after another. It orders and places through the same commands as the client,
/// finds a place with the same <see cref="PlacementRules"/>, and writes a timeline to measure the pace against RA2.
/// </summary>
public static class EconomyRun
{
    public const int TicksPerMinute = 60 * 15;

    /// <summary>The order of the base, before the tanks.</summary>
    public static readonly string[] BuildOrder = ["reactor", "refinery", "barracks", "war_factory"];

    public const string Tank = "heavy_tank";

    /// <summary>Plays the run on the map's first start until <paramref name="tanks"/> tanks are out or the time is up.</summary>
    public static EconomyReport Play(MapDefinition map, GameRules rules, int tanks = 10, int maxMinutes = 20, ulong seed = 1)
    {
        if (map.Starts.IsEmpty)
        {
            throw new ArgumentException($"The map '{map.Id}' has no starts.");
        }

        var simulation = new GameSimulation(new SimulationSettings(seed, map.ToMapGrid()), rules);
        var player = new PlayerId(1);
        var bot = new Bot(simulation, player, rules, tanks);
        simulation.Enqueue(new SpawnTestUnitCommand(player, 0, bot.NextSequence(), UnitType.Mcv, map.Starts[0].Cell.Center));
        while (simulation.CurrentTick < maxMinutes * TicksPerMinute && bot.TankTicks.Count < tanks)
        {
            bot.Act();
            simulation.Step();
            bot.Observe();
        }

        // A little more time for the last tank to drive off the exit.
        for (var i = 0; i < 15 * 10; i++)
        {
            simulation.Step();
            bot.Observe();
        }

        return bot.Report();
    }

    /// <summary>The report as text: the timeline, the minutes and the result.</summary>
    public static string Format(EconomyReport report)
    {
        var text = new StringBuilder();
        foreach (var line in report.Timeline)
        {
            text.AppendLine($"{Time(line.Tick),6}  {line.Text}");
        }

        text.AppendLine();
        text.AppendLine("minute  credits  income  harvesters  power    harvesters at the end of the minute");
        foreach (var minute in report.Minutes)
        {
            text.AppendLine($"{minute.Minute,6}  {minute.Credits,7}  {minute.Income,6}  {minute.Harvesters,10}  {minute.PowerOutput + "/" + minute.PowerDrain,-7}  {minute.Modes}");
        }

        text.AppendLine();
        text.AppendLine($"tanks {report.TankTicks.Count}: first {(report.TankTicks.Count > 0 ? Time(report.TankTicks[0]) : "-")}, " +
                        $"last {(report.TankTicks.Count > 0 ? Time(report.TankTicks[^1]) : "-")}");
        text.AppendLine($"unloads {report.Unloads}, income {report.Income}, per harvester-minute {report.IncomePerHarvesterMinute}, " +
                        $"lowest credits {report.LowestCredits}");
        text.AppendLine($"checksum {report.Simulation.LastChecksum}");
        return text.ToString();
    }

    public static string Time(int tick) => $"{tick / TicksPerMinute}:{tick % TicksPerMinute / 15:00}";

    private sealed class Bot(GameSimulation simulation, PlayerId player, GameRules rules, int tanks)
    {
        private readonly List<EconomyEvent> _timeline = [];
        private readonly List<EconomyMinute> _minutes = [];
        private readonly Dictionary<EntityId, int> _loads = new();
        private readonly HashSet<string> _seen = [];
        private uint _sequence;
        private int _next;
        private int _tanksOrdered;
        private int _income;
        private int _minuteIncome;
        private int _unloads;
        private int _harvesterTicks;
        private int _lowest = int.MaxValue;
        private bool _deployOrdered;

        public List<int> TankTicks { get; } = [];

        private GameWorld World => simulation.World;

        private RulesDatabase Data => rules.Database;

        private PlayerState State => World.Player(player);

        public uint NextSequence() => _sequence++;

        /// <summary>The player's orders for this tick.</summary>
        public void Act()
        {
            var tick = simulation.CurrentTick;
            if (!_deployOrdered && World.Entities.OfType<Unit>().FirstOrDefault(unit => unit.IsAlive && unit.Owner == player && unit.Type == UnitType.Mcv) is { } mcv)
            {
                simulation.Enqueue(new DeployCommand(player, tick, NextSequence(), mcv.Id));
                _deployOrdered = true;
                return;
            }

            var queue = State.Queue(ProductionCategory.Building);
            if (queue is [{ Ready: true } ready, ..])
            {
                var def = Data.Buildings[ready.DefIndex];
                if (Spot(def) is { } spot)
                {
                    simulation.Enqueue(new PlaceBuildingCommand(player, tick, NextSequence(), ready.DefIndex, spot));
                }
            }
            else if (queue.Count == 0 && NextBuilding() is { } id)
            {
                Order(ProductionKind.Building, Data.IndexOfBuilding(id), 1);
            }

            var tank = Data.IndexOfUnit(Tank);
            if (_tanksOrdered < tanks && State.CanBuildUnit(tank) && ProductionRules.HasFactory(World, player, ProductionCategory.Vehicle)
                && State.Queue(ProductionCategory.Vehicle).Count < 2)
            {
                Order(ProductionKind.Unit, tank, 1);
                _tanksOrdered++;
            }
        }

        /// <summary>What happened in the tick just played.</summary>
        public void Observe()
        {
            var tick = simulation.CurrentTick - 1;
            foreach (var simEvent in simulation.LastTickEvents)
            {
                switch (simEvent)
                {
                    case Deployed:
                        Note(tick, "MCV deployed");
                        break;
                    case ProductionReady ready when ready.Player == player:
                        Note(tick, $"{Data.Buildings[ready.DefIndex].Id} ready to place");
                        break;
                    case BuildingPlaced placed when placed.Player == player && Data.Buildings[placed.DefIndex].Id != Data.Factions[0].ConstructionYard:
                        Note(tick, $"{Data.Buildings[placed.DefIndex].Id} placed at {placed.Cell.X},{placed.Cell.Y}");
                        break;
                    case BuildUpComplete complete when World.TryGet(complete.Building, out var entity) && entity is Building { Def: { } def } && entity.Owner == player:
                        Note(tick, $"{def.Id} up");
                        break;
                    case UnitProduced produced when produced.Player == player && World.TryGet(produced.Unit, out var unit) && unit is Unit made:
                        if (made.Archetype.Id == Tank)
                        {
                            TankTicks.Add(tick);
                            Note(tick, $"tank {TankTicks.Count} out");
                        }
                        else
                        {
                            Note(tick, $"{made.Archetype.Id} out");
                        }

                        break;
                    case HarvesterUnloading:
                        _unloads++;
                        if (_unloads == 1)
                        {
                            Note(tick, "first unload");
                        }

                        break;
                    case InsufficientFunds funds when funds.Player == player && _seen.Add($"funds {funds.Category}"):
                        Note(tick, $"insufficient funds ({funds.Category}), first time");
                        break;
                    case LowPower low when low.Player == player:
                        Note(tick, "LOW POWER");
                        break;
                    case PowerRestored restored when restored.Player == player:
                        Note(tick, "power restored");
                        break;
                }
            }

            foreach (var entity in World.Entities)
            {
                if (entity is Unit { IsAlive: true, Harvester: { } harvester } unit && unit.Owner == player)
                {
                    _harvesterTicks++;
                    if (_loads.TryGetValue(unit.Id, out var before) && harvester.Load < before && harvester.Mode == HarvesterMode.Unloading)
                    {
                        _income += before - harvester.Load;
                        _minuteIncome += before - harvester.Load;
                    }

                    _loads[unit.Id] = harvester.Load;
                }
            }

            _lowest = Math.Min(_lowest, State.Credits);
            if (simulation.CurrentTick % TicksPerMinute == 0)
            {
                var harvesters = World.Entities.OfType<Unit>().Where(unit => unit.IsAlive && unit.Harvester is not null && unit.Owner == player).ToList();
                var modes = string.Join(" ", harvesters.Select(unit =>
                    $"{unit.Harvester!.Mode} {unit.Harvester.Load} at {unit.Position.ToCell().X},{unit.Position.ToCell().Y}"));
                _minutes.Add(new EconomyMinute(simulation.CurrentTick / TicksPerMinute, State.Credits, _minuteIncome, harvesters.Count, State.PowerOutput, State.PowerDrain, modes));
                _minuteIncome = 0;
            }
        }

        public EconomyReport Report() =>
            new(_timeline, _minutes, TankTicks, _unloads, _income, _harvesterTicks, _lowest == int.MaxValue ? State.Credits : _lowest, simulation);

        private void Note(int tick, string text) => _timeline.Add(new EconomyEvent(tick, text));

        private void Order(ProductionKind kind, int index, int count)
        {
            simulation.Enqueue(new ProduceCommand(player, simulation.CurrentTick, NextSequence(), kind, index, count));
            var id = ProductionRules.IdOf(Data, kind, index);
            if (kind == ProductionKind.Building || _seen.Add($"ordered {id}"))
            {
                Note(simulation.CurrentTick, $"order {id}");
            }
        }

        /// <summary>
        /// The next building of the order, or a reactor first when the next one would drain more than there is; after the order, a
        /// reactor whenever the power gets within 50 of the output.
        /// </summary>
        private string? NextBuilding()
        {
            if (!ProductionRules.HasFactory(World, player, ProductionCategory.Building))
            {
                return null;
            }

            var reactor = Data.Buildings[Data.IndexOfBuilding("reactor")];
            var (output, drain) = PlannedPower();
            if (_next < BuildOrder.Length)
            {
                var index = Data.IndexOfBuilding(BuildOrder[_next]);
                var next = Data.Buildings[index];
                if (next.Power < 0 && drain - next.Power > output && _next > 0)
                {
                    return reactor.Id;
                }

                // Its prerequisites may still be growing: the player waits for them.
                if (!State.CanBuildBuilding(index))
                {
                    return null;
                }

                _next++;
                return next.Id;
            }

            return output - drain < 50 ? reactor.Id : null;
        }

        /// <summary>The power of every building of the player, the growing ones too: a player counts what is already placed.</summary>
        private (int Output, int Drain) PlannedPower()
        {
            var (output, drain) = (0, 0);
            foreach (var entity in World.Entities)
            {
                if (entity is Building { IsAlive: true, Def: { } def } && entity.Owner == player)
                {
                    output += Math.Max(0, def.Power);
                    drain += Math.Max(0, -def.Power);
                }
            }

            return (output, drain);
        }

        /// <summary>
        /// A place for the building near the construction yard where the rules allow it and a cell of ground stays free all round, so
        /// that exits and docks are not walled in; the refinery goes as near the ore as it can.
        /// </summary>
        private CellPos? Spot(BuildingDef def)
        {
            var yard = World.Entities.OfType<Building>().FirstOrDefault(b => b.IsAlive && b.Owner == player && b.Def?.Id == Data.Factions[0].ConstructionYard);
            if (yard is null)
            {
                return null;
            }

            var map = World.Map;
            var refinery = def.DockCell is not null;
            var ore = refinery ? OreCells(map) : [];
            var centre = new CellPos(yard.Cell.X + yard.Width / 2, yard.Cell.Y + yard.Height / 2);
            CellPos? best = null;
            var bestScore = long.MaxValue;
            for (var y = centre.Y - 14; y <= centre.Y + 14; y++)
            {
                for (var x = centre.X - 14; x <= centre.X + 14; x++)
                {
                    var corner = new CellPos(x, y);
                    if (PlacementRules.Check(World, player, def, corner) != PlacementProblem.None || !Clear(map, corner, def))
                    {
                        continue;
                    }

                    var middleX = x + def.Footprint.Width / 2;
                    var middleY = y + def.Footprint.Height / 2;
                    long score = refinery && ore.Count > 0
                        ? ore.Min(cell => Square(cell.X - middleX) + Square(cell.Y - middleY))
                        : Square(middleX - centre.X) + Square(middleY - centre.Y);
                    score = score * 10_000 + y * 100 + x;
                    if (score < bestScore)
                    {
                        (best, bestScore) = (corner, score);
                    }
                }
            }

            return best;
        }

        private static long Square(int value) => (long)value * value;

        /// <summary>No building in the ring of cells round the footprint.</summary>
        private static bool Clear(MapGrid map, CellPos corner, BuildingDef def)
        {
            for (var y = corner.Y - 1; y <= corner.Y + def.Footprint.Height; y++)
            {
                for (var x = corner.X - 1; x <= corner.X + def.Footprint.Width; x++)
                {
                    var cell = new CellPos(x, y);
                    if (map.Contains(cell) && map.HasBuilding(cell))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static List<CellPos> OreCells(MapGrid map)
        {
            var cells = new List<CellPos>();
            for (var y = 0; y < map.Height; y++)
            {
                for (var x = 0; x < map.Width; x++)
                {
                    if (!map.OreAt(new CellPos(x, y)).IsEmpty)
                    {
                        cells.Add(new CellPos(x, y));
                    }
                }
            }

            return cells;
        }
    }
}
