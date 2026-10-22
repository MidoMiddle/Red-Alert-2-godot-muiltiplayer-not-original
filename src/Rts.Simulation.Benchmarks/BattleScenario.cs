using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Benchmarks;

/// <summary>
/// The general battle of docs/implementation/M4_combat.md, 4.21 and step 15, on the land of the load map (game/maps/test/load128.txt;
/// its own units are left out): two armies of 150 on equal credits — 60 conscripts, 20 rocket troopers, 20 flak tracks, 40 heavy
/// tanks and 10 tesla tanks each — blue in the west, red 12 cells east of it, each with a small base to the north
/// (<see cref="BattleBases"/>), so that buildings, power, the fog and the notices of attacks all work. On tick 1 both armies
/// attack-move through each other. Shared by the benchmarks, the determinism test of Rts.Simulation.Tests and the client's
/// <c>--perf-battle</c>; only the public commands are used here, so the client can link this file.
/// </summary>
public static class BattleScenario
{
    public const ulong Seed = 0x424154544C45;
    public const int Ticks = 3000;
    public const int Columns = 10;
    public const int Rows = 15;
    public const int Top = 47;
    public const int BlueLeft = 4;
    public const int RedLeft = BlueLeft + 2 * Columns + 10;

    public static readonly PlayerId Blue = new(1);
    public static readonly PlayerId Red = new(2);

    /// <summary>One army, in the order the units take their places, front column first.</summary>
    public static readonly ImmutableArray<(UnitType Type, int Count)> Army =
    [
        (UnitType.HeavyTank, 40), (UnitType.TeslaTank, 10), (UnitType.SupportVehicle, 20), (UnitType.Rocketeer, 20), (UnitType.Conscript, 60),
    ];

    public static int ArmySize => Army.Sum(part => part.Count);

    /// <summary>
    /// A new match on <paramref name="map"/> by <paramref name="rules"/> with both armies spawned (tick 0 done), and both bases when
    /// <paramref name="bases"/> builds them (<see cref="BattleBases.Build"/>). <paramref name="hashInterval"/> is how often the state
    /// is hashed.
    /// </summary>
    public static GameSimulation Start(MapGrid map, GameRules rules, int hashInterval = 1, Action<GameWorld>? bases = null)
    {
        var simulation = Create(map, rules, hashInterval, bases);
        simulation.Step();
        return simulation;
    }

    /// <summary>
    /// The match before tick 0: the bases built by <paramref name="bases"/> (none in a replay, which knows only the map and the
    /// commands, nor in the client, which cannot place a ready building) and the spawns of both armies queued for tick 0, so a
    /// replay recorder can follow it from the start.
    /// </summary>
    public static GameSimulation Create(MapGrid map, GameRules rules, int hashInterval = 1, Action<GameWorld>? bases = null)
    {
        var simulation = new GameSimulation(new SimulationSettings(Seed, map) { HashInterval = hashInterval }, rules);
        bases?.Invoke(simulation.World);
        uint sequence = 0;
        Spawn(simulation, Blue, BlueLeft, facingEast: true, ref sequence);
        Spawn(simulation, Red, RedLeft, facingEast: false, ref sequence);
        return simulation;
    }

    /// <summary>Both armies attack-move through each other on the next tick.</summary>
    public static void Charge(GameSimulation simulation)
    {
        uint sequence = 10_000;
        foreach (var (player, goal) in new[] { (Blue, RedLeft + 2 * Columns + 4), (Red, BlueLeft - 2) })
        {
            var units = simulation.World.Entities.OfType<Unit>().Where(u => u.IsAlive && u.Owner == player).Select(u => u.Id).ToImmutableArray();
            for (var start = 0; start < units.Length; start += UnitsCommand.MaxUnits)
            {
                var group = units.Skip(start).Take(UnitsCommand.MaxUnits).ToImmutableArray();
                simulation.Enqueue(new AttackMoveCommand(player, simulation.CurrentTick, sequence++, group, new CellPos(goal, Top + Rows).Center));
            }
        }
    }

    /// <summary>The army in a block of <see cref="Columns"/> by <see cref="Rows"/>, every second cell, skipping what it cannot enter.</summary>
    private static void Spawn(GameSimulation simulation, PlayerId player, int left, bool facingEast, ref uint sequence)
    {
        var map = simulation.World.Map;
        var places = new List<CellPos>();
        for (var column = 0; column < Columns * 2 && places.Count < ArmySize; column++)
        {
            // The front column — nearest the other army — fills first.
            var x = facingEast ? left + 2 * Columns - 1 - column : left + column;
            for (var y = Top; y < Top + Rows * 2 && places.Count < ArmySize; y += 2)
            {
                var cell = new CellPos(x, y + (column % 2));
                if (map.CanEnter(cell, Locomotor.Track) && map.CanEnter(cell, Locomotor.Wheel) && map.CanEnter(cell, Locomotor.Foot))
                {
                    places.Add(cell);
                }
            }
        }

        var next = 0;
        foreach (var (type, count) in Army)
        {
            for (var i = 0; i < count && next < places.Count; i++)
            {
                simulation.Enqueue(new SpawnTestUnitCommand(player, 0, sequence++, type, places[next++].Center));
            }
        }
    }
}
