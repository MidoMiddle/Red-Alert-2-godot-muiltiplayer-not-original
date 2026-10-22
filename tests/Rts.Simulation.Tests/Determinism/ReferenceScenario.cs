using System.Collections.Immutable;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Hashing;
using Rts.Simulation.Random;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Determinism;

/// <summary>
/// The reference match used by determinism tests and golden values. Commands are produced by the
/// simulation's own generator (PCG32), so the scenario depends only on simulation code.
/// Changing anything here changes the golden values.
/// </summary>
internal static class ReferenceScenario
{
    public const ulong MatchSeed = 42;
    public const int Players = 2;
    private const ulong ScenarioSeed = 2026;
    private const ulong ScenarioStream = 7;
    private const int MapLeptons = SimulationSettings.DefaultMapSizeCells * WorldPos.LeptonsPerCell;

    public static SimulationSettings Settings(ulong seed = MatchSeed) => new(seed);

    /// <summary>
    /// Commands for <paramref name="ticks"/> ticks: each tick, each player issues a command with probability 1/4 —
    /// a spawn of a random unit type (5/16), or for 1–12 unit ids a move (5/16, one of them queued), an attack-move,
    /// a route of 2–3 points (sometimes a patrol), a stop or a scatter (1/16 each), at random points on the map.
    /// </summary>
    public static IReadOnlyList<Command> Commands(int ticks)
    {
        var random = new DeterministicRandom(ScenarioSeed, ScenarioStream);
        var sequences = new uint[Players + 1];
        var commands = new List<Command>();

        for (var tick = 0; tick < ticks; tick++)
        {
            for (var player = 1; player <= Players; player++)
            {
                if (random.NextInt(4) != 0)
                {
                    continue;
                }

                var owner = new PlayerId(player);
                var point = new WorldPos(random.NextInt(MapLeptons), random.NextInt(MapLeptons));
                var kind = random.NextInt(19);
                if (kind < 5)
                {
                    var type = (UnitType)(1 + random.NextInt(TestRules.Standard.Units.Count));
                    commands.Add(new SpawnTestUnitCommand(owner, tick, sequences[player]++, type, point));
                    continue;
                }

                var units = Enumerable.Range(0, 1 + random.NextInt(12))
                    .Select(_ => new EntityId(random.NextInt(1, 200)))
                    .ToImmutableArray();
                var sequence = sequences[player]++;
                commands.Add(kind switch
                {
                    11 => new AttackMoveCommand(owner, tick, sequence, units, point, Queue: random.NextInt(2) == 0),
                    12 => new WaypointsCommand(owner, tick, sequence, units,
                        Enumerable.Range(0, 2 + random.NextInt(2))
                            .Select(_ => new WorldPos(random.NextInt(MapLeptons), random.NextInt(MapLeptons)))
                            .ToImmutableArray(),
                        loop: random.NextInt(2) == 0),
                    13 => new StopCommand(owner, tick, sequence, units),
                    14 => new ScatterCommand(owner, tick, sequence, units),
                    16 => new AttackCommand(owner, tick, sequence, units, new EntityId(random.NextInt(1, 200))),
                    17 => new ForceAttackCommand(owner, tick, sequence, units,
                        random.NextInt(2) == 0 ? new EntityId(random.NextInt(1, 200)) : null, point),
                    18 => new SetStanceCommand(owner, tick, sequence, units, (Stance)random.NextInt(3)),
                    _ => new MoveCommand(owner, tick, sequence, units, point, Queue: kind == 10),
                });
            }
        }

        return commands;
    }

    /// <summary>Runs the commands for <paramref name="ticks"/> ticks; returns the checksum after every step.</summary>
    public static IReadOnlyList<StateChecksum> Run(IEnumerable<Command> commands, int ticks, ulong seed = MatchSeed)
    {
        var simulation = new GameSimulation(Settings(seed), TestRules.Standard);
        foreach (var command in commands)
        {
            simulation.Enqueue(command);
        }

        var checksums = new List<StateChecksum>(ticks);
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
            checksums.Add(simulation.LastChecksum!.Value);
        }

        return checksums;
    }
}
