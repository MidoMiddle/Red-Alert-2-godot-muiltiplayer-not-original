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
    /// Commands for <paramref name="ticks"/> ticks: each tick, each player issues a command with
    /// probability 1/4 — a spawn (1/3) or a move of 1–12 unit ids (2/3) to a random point on the map.
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
                if (random.NextInt(3) == 0)
                {
                    commands.Add(new SpawnTestUnitCommand(owner, tick, sequences[player]++, point));
                }
                else
                {
                    var units = Enumerable.Range(0, 1 + random.NextInt(12))
                        .Select(_ => new EntityId(random.NextInt(1, 200)))
                        .ToImmutableArray();
                    commands.Add(new MoveUnitsCommand(owner, tick, sequences[player]++, units, point));
                }
            }
        }

        return commands;
    }

    /// <summary>Runs the commands for <paramref name="ticks"/> ticks; returns the checksum after every step.</summary>
    public static IReadOnlyList<StateChecksum> Run(IEnumerable<Command> commands, int ticks, ulong seed = MatchSeed)
    {
        var simulation = new GameSimulation(Settings(seed));
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
