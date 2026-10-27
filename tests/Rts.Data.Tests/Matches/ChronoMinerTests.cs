using Rts.Simulation;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// RA2's Chrono Miner (docs/ra2_units/allies/chrono_miner): full, it jumps to its refinery instead of driving there, phases in for the
/// distance and unloads; the War Miner drives. By the game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class ChronoMinerTests
{
    private const int Size = 64;
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static GameSimulation Match()
    {
        var land = Enumerable.Repeat((byte)LandType.Clear, Size * Size).ToArray();
        var map = MapGrid.FromLayers(Size, Size, land, new byte[Size * Size], default, null, null, [], types: GameDataFiles.Rules.MapTypes);
        var simulation = new GameSimulation(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "chrono-miner",
                    91,
                    new MatchOptions(StartingCredits: 0, FogOfWar: false, StartingUnits: 0),
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(4, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(4, 60)]),
                map),
            GameDataFiles.Rules);
        simulation.World.VisionOf(Union).ExploreAll();
        simulation.World.VisionOf(Allied).ExploreAll();
        return simulation;
    }

    /// <summary>A full harvester of <paramref name="player"/> 40 cells from its refinery: the ticks until the credits come, and its jumps.</summary>
    private static (int Ticks, int Jumps) HaulHome(PlayerId player, string refineryId, UnitType type)
    {
        var simulation = Match();
        var world = simulation.World;
        world.Rules.Database.TryGetBuilding(player == Union ? "reactor" : "alliance_power_plant", out var power);
        world.CreateBuilding(player, power, new CellPos(20, 4), true, null);
        world.Rules.Database.TryGetBuilding(refineryId, out var refineryDef);
        world.CreateBuilding(player, refineryDef, new CellPos(6, 6), true, null);
        var harvester = world.SpawnUnit(player, type, new CellPos(50, 50).Center);
        harvester.Harvester!.Load = 1000;
        harvester.Harvester.Mode = HarvesterMode.SeekRefinery;
        var credits = world.Player(player).Credits;

        var jumps = 0;
        var ticks = 0;
        for (; ticks < 3000 && world.Player(player).Credits == credits; ticks++)
        {
            simulation.Step();
            jumps += simulation.LastTickEvents.OfType<UnitTeleported>().Count(jump => jump.Unit == harvester.Id);
        }

        return (ticks, jumps);
    }

    [Fact]
    public void Full_the_chrono_miner_jumps_home_and_the_war_miner_drives()
    {
        var chrono = HaulHome(Allied, "alliance_ore_refinery", UnitType.AllianceHarvester);
        var war = HaulHome(Union, "refinery", UnitType.Harvester);

        Assert.Equal(1, chrono.Jumps);
        Assert.Equal(0, war.Jumps);
        // About 40 cells: driving takes the War Miner some hundreds of ticks; the jump phases in for 10240 / 48 = 213 at most.
        Assert.True(chrono.Ticks < war.Ticks, $"{chrono.Ticks} against {war.Ticks}");
        Assert.InRange(chrono.Ticks, MovementSystemWarp.Min, 400);
    }
}

file static class MovementSystemWarp
{
    public const int Min = Rts.Simulation.Systems.MovementSystem.MinWarp;
}
