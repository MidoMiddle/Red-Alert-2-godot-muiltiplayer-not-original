using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Matches;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Visibility;

/// <summary>
/// The fog of war, an option of the match (the skirmish screen, off by default there): off, whatever the team has explored stays in
/// sight — enemies on it are seen and can be attacked — and only the shroud over the unexplored is left; on, as before.
/// </summary>
public sealed class FogOfWarTests
{
    private static readonly PlayerId First = new(1);
    private static readonly PlayerId Enemy = new(2);
    private static readonly CellPos Explored = new(30, 30);
    private static readonly CellPos Unexplored = new(30, 55);

    /// <summary>A match with the fog as asked; a tank of the first player has looked at <see cref="Explored"/> and gone.</summary>
    private static GameSimulation Scouted(bool fog)
    {
        var setup = MatchSetupTests.OneOnOne() with { Options = new MatchOptions(FogOfWar: fog) };
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), TestRules.Standard);
        var scout = simulation.World.SpawnUnit(First, UnitType.HeavyTank, Explored.Center);
        VisibilitySystem.Update(simulation.World);
        scout.Position = new CellPos(12, 12).Center;
        VisibilitySystem.Update(simulation.World);
        return simulation;
    }

    [Fact]
    public void Off_an_enemy_on_explored_land_is_seen_and_one_in_the_shroud_is_not()
    {
        var world = Scouted(fog: false).World;
        var near = world.SpawnUnit(Enemy, UnitType.HeavyTank, Explored.Center);
        var far = world.SpawnUnit(Enemy, UnitType.HeavyTank, Unexplored.Center);
        VisibilitySystem.Update(world);

        Assert.False(world.VisionOf(First).IsVisible(Explored));
        Assert.True(Vision.Sees(world, First, near));
        Assert.False(Vision.Sees(world, First, far));
        Assert.Equal(CellVisibility.Visible, Vision.StateAt(world, First, Explored));
        Assert.True(Vision.IsVisible(world, First, Explored));
        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, First, Unexplored));
    }

    [Fact]
    public void On_explored_land_is_in_the_fog_as_before()
    {
        var world = Scouted(fog: true).World;
        var near = world.SpawnUnit(Enemy, UnitType.HeavyTank, Explored.Center);
        VisibilitySystem.Update(world);

        Assert.False(Vision.Sees(world, First, near));
        Assert.Equal(CellVisibility.Fog, Vision.StateAt(world, First, Explored));
        Assert.False(Vision.NoFog(world));
    }

    [Fact]
    public void Off_an_order_to_attack_an_enemy_on_explored_land_is_taken_on_it_is_dropped()
    {
        foreach (var fog in new[] { false, true })
        {
            var simulation = Scouted(fog);
            var tank = simulation.World.SpawnUnit(First, UnitType.HeavyTank, new CellPos(12, 12).Center);
            var enemy = simulation.World.SpawnUnit(Enemy, UnitType.HeavyTank, Explored.Center);
            var attack = new AttackCommand(First, simulation.CurrentTick, 0, [tank.Id], enemy.Id);
            simulation.Enqueue(attack);
            simulation.Step();

            Assert.Equal(fog, simulation.RefusedCommands.Contains(attack));
        }
    }

    [Fact]
    public void The_replay_keeps_the_fog_of_war()
    {
        var setup = MatchSetupTests.OneOnOne() with { Options = new MatchOptions(FogOfWar: false) };
        var replay = new Replay(
            SimulationVersion.Current, Rts.Simulation.Rules.DataHasher.NoData, SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), 0, [], []);

        var read = ReplayFormat.Read(ReplayFormat.Write(replay)).Settings.Match!;

        Assert.False(read.Options.FogOfWar);
        Assert.True(new MatchOptions().FogOfWar);
    }
}
