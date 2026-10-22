using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.MapTool;

/// <summary>
/// How a fight ended: the side that has fighters left (1 or 2; 0 when both or neither have), its length in ticks, and for each side the
/// fighters left — units and armed buildings — and the value of its units left and at the start (cost × the share of hit points left).
/// </summary>
public sealed record SkirmishResult(int Winner, int Ticks, int[] UnitsLeft, int[] ValueLeft, int[] ValueStart)
{
    /// <summary>The value left of a side in percent of its start.</summary>
    public int PercentLeft(int side) => ValueStart[side - 1] == 0 ? 0 : ValueLeft[side - 1] * 100 / ValueStart[side - 1];
}

/// <summary>
/// The fights of balance v0 (docs/implementation/M4_combat.md, step 14): two groups on an open field 12 cells apart attack-move into
/// each other, and the fight goes on until a side has no fighters — units or armed buildings — or the time is up. The groups stand in
/// columns of six, a cell apart. A side with no units — a defense the setup places — does not move. Only the public commands of the game are used, as the client
/// would; the setup can place what they cannot (buildings), for the tests.
/// </summary>
public static class Skirmish
{
    public const int Width = 48;
    public const int Height = 32;
    public const int FirstColumn = 16;
    public const int SecondColumn = 28;
    public const int MaxTicks = 4500;

    /// <summary>The kinds of units the matrix pits against each other, those that fight on the ground.</summary>
    public static readonly UnitType[] Fighters = [UnitType.Conscript, UnitType.Rocketeer, UnitType.SupportVehicle, UnitType.HeavyTank, UnitType.TeslaTank];

    public static SkirmishResult Fight(
        GameRules rules, IReadOnlyList<UnitType> first, IReadOnlyList<UnitType> second, int maxTicks = MaxTicks, ulong seed = 1,
        Action<GameSimulation>? setup = null)
    {
        var simulation = new GameSimulation(new SimulationSettings(seed, new MapGrid(Width, Height, rules.Land)), rules);
        setup?.Invoke(simulation);
        var players = new[] { new PlayerId(1), new PlayerId(2) };
        uint sequence = 0;
        Spawn(simulation, players[0], first, FirstColumn, -1, ref sequence);
        Spawn(simulation, players[1], second, SecondColumn, +1, ref sequence);
        simulation.Step();

        // Into each other: each side to a point beyond the other's start.
        Charge(simulation, players[0], SecondColumn + 6, ref sequence);
        Charge(simulation, players[1], FirstColumn - 6, ref sequence);
        var start = new[] { ValueOf(simulation, rules, players[0]), ValueOf(simulation, rules, players[1]) };
        while (simulation.CurrentTick < maxTicks && Count(simulation, players[0]) > 0 && Count(simulation, players[1]) > 0)
        {
            simulation.Step();
        }

        var left = new[] { Count(simulation, players[0]), Count(simulation, players[1]) };
        var winner = left[0] > 0 && left[1] == 0 ? 1 : left[1] > 0 && left[0] == 0 ? 2 : 0;
        return new SkirmishResult(winner, simulation.CurrentTick, left, [ValueOf(simulation, rules, players[0]), ValueOf(simulation, rules, players[1])], start);
    }

    /// <summary>How many units of <paramref name="type"/> come to about <paramref name="credits"/>: at least one.</summary>
    public static int CountFor(GameRules rules, UnitType type, int credits) => Math.Max(1, credits / CostOf(rules, type));

    /// <summary>
    /// Every fighter against every other on <paramref name="credits"/> a side, as a table: the row's side attacks the column's; each
    /// cell names the winner and what it has left in percent, or "draw".
    /// </summary>
    public static string Matrix(GameRules rules, int credits)
    {
        var text = new StringBuilder();
        text.AppendLine(CultureInfo.InvariantCulture, $"each side ≈ {credits} credits; a cell: the winner and its value left, ticks");
        text.Append("".PadRight(19));
        foreach (var column in Fighters)
        {
            text.Append(Name(rules, column).PadRight(27));
        }

        text.AppendLine();
        foreach (var row in Fighters)
        {
            text.Append($"{CountFor(rules, row, credits)} {Name(rules, row)}".PadRight(19));
            foreach (var column in Fighters)
            {
                if (row == column)
                {
                    text.Append("-".PadRight(27));
                    continue;
                }

                var result = Fight(rules, Repeat(row, CountFor(rules, row, credits)), Repeat(column, CountFor(rules, column, credits)));
                var cell = result.Winner switch
                {
                    1 => $"{Name(rules, row)} {result.PercentLeft(1)}%",
                    2 => $"{Name(rules, column)} {result.PercentLeft(2)}%",
                    _ => "draw",
                };
                text.Append($"{cell} {result.Ticks}t".PadRight(27));
            }

            text.AppendLine();
        }

        return text.ToString();
    }

    public static IReadOnlyList<UnitType> Repeat(UnitType type, int count) => Enumerable.Repeat(type, count).ToArray();

    private static string Name(GameRules rules, UnitType type) => rules.Unit(type).Id;

    private static int CostOf(GameRules rules, UnitType type)
    {
        rules.Database.TryGetUnit(rules.Unit(type).Id, out var unit);
        return unit.Cost;
    }

    /// <summary>A side in columns of six, one cell apart, growing away from the other side.</summary>
    private static void Spawn(GameSimulation simulation, PlayerId player, IReadOnlyList<UnitType> units, int column, int away, ref uint sequence)
    {
        for (var i = 0; i < units.Count; i++)
        {
            var cell = new CellPos(column + away * (i / 6), Height / 2 - 3 + i % 6);
            simulation.Enqueue(new SpawnTestUnitCommand(player, simulation.CurrentTick, sequence++, units[i], cell.Center));
        }
    }

    private static void Charge(GameSimulation simulation, PlayerId player, int column, ref uint sequence)
    {
        var units = simulation.World.Entities.OfType<Unit>().Where(u => u.IsAlive && u.Owner == player).Select(u => u.Id).ToImmutableArray();
        if (units.Length > 0)
        {
            simulation.Enqueue(new AttackMoveCommand(player, simulation.CurrentTick, sequence++, units, new CellPos(column, Height / 2).Center));
        }
    }

    /// <summary>The fighters of a side: its units, and its buildings with a weapon.</summary>
    private static int Count(GameSimulation simulation, PlayerId player) =>
        simulation.World.Entities.Count(e => e.IsAlive && e.Owner == player && e is Unit or Building { Weapons.Length: > 0 });

    private static int ValueOf(GameSimulation simulation, GameRules rules, PlayerId player)
    {
        long value = 0;
        foreach (var entity in simulation.World.Entities)
        {
            if (entity is Unit { IsAlive: true } unit && unit.Owner == player)
            {
                value += (long)CostOf(rules, unit.Type) * unit.Hp / unit.MaxHp;
            }
        }

        return (int)value;
    }
}
