using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.Tests.Matches;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Visibility;

/// <summary>
/// The cells as a team knows them (docs/implementation/M6_menus_skirmish.md, 4.3; open question A6): what the fog on screen shows. A cell
/// is visible while the player or an ally sees it, explored once one of them saw it; a player alone knows what its own grid does. Players
/// 1 and 2 are a team, 3 is the enemy; the cells are in the middle of the map, far from the starts.
/// </summary>
public sealed class TeamVisionTests
{
    private static readonly PlayerId First = new(1);
    private static readonly PlayerId Ally = new(2);
    private static readonly PlayerId Enemy = new(3);
    private static readonly CellPos Middle = new(30, 30);

    private static GameWorld Teams() =>
        new GameSimulation(
            SimulationSettings.ForMatch(
                MatchSetupTests.OneOnOne() with
                {
                    Players =
                    [
                        MatchSetupTests.Player(1, PlayerKind.Human, 10, 10) with { Team = 1 },
                        MatchSetupTests.Player(2, PlayerKind.Ai, 50, 50) with { Team = 1 },
                        MatchSetupTests.Player(3, PlayerKind.Human, 50, 10) with { Team = 2 },
                    ],
                },
                new MapGrid(64, 64)),
            TestRules.Standard).World;

    [Fact]
    public void A_cell_the_ally_sees_is_visible_to_the_team_and_not_to_the_enemy()
    {
        var world = Teams();
        world.SpawnUnit(Ally, UnitType.HeavyTank, Middle.Center);
        VisibilitySystem.Update(world);

        Assert.False(world.VisionOf(First).IsVisible(Middle));
        Assert.True(Vision.IsVisible(world, First, Middle));
        Assert.True(Vision.IsExplored(world, First, Middle));
        Assert.Equal(CellVisibility.Visible, Vision.StateAt(world, First, Middle));
        Assert.Equal(CellVisibility.Visible, Vision.StateAt(world, Ally, Middle));
        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, Enemy, Middle));
    }

    [Fact]
    public void A_cell_the_ally_saw_and_left_stays_explored_for_the_team()
    {
        var world = Teams();
        var tank = world.SpawnUnit(Ally, UnitType.HeavyTank, Middle.Center);
        VisibilitySystem.Update(world);

        tank.Position = new CellPos(50, 40).Center;
        VisibilitySystem.Update(world);

        Assert.False(Vision.IsVisible(world, First, Middle));
        Assert.Equal(CellVisibility.Fog, Vision.StateAt(world, First, Middle));
    }

    [Fact]
    public void The_enemy_sight_opens_nothing_for_the_team()
    {
        var world = Teams();
        world.SpawnUnit(Enemy, UnitType.HeavyTank, Middle.Center);
        VisibilitySystem.Update(world);

        Assert.Equal(CellVisibility.Visible, Vision.StateAt(world, Enemy, Middle));
        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, First, Middle));
        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, Ally, Middle));
    }

    [Fact]
    public void Alone_a_player_knows_every_cell_as_its_own_grid_does()
    {
        var simulation = new GameSimulation(SimulationSettings.ForMatch(MatchSetupTests.OneOnOne(), new MapGrid(64, 64)), TestRules.Standard);
        var world = simulation.World;
        var scout = world.SpawnUnit(Ally, UnitType.HeavyTank, Middle.Center);
        VisibilitySystem.Update(world);
        scout.Position = new CellPos(40, 30).Center;
        VisibilitySystem.Update(world);

        foreach (var player in new[] { First, Ally })
        {
            var grid = world.VisionOf(player);
            for (var y = 0; y < grid.Height; y++)
            {
                for (var x = 0; x < grid.Width; x++)
                {
                    var cell = new CellPos(x, y);
                    Assert.Equal(grid.StateAt(cell), Vision.StateAt(world, player, cell));
                }
            }
        }
    }

    [Fact]
    public void The_neutral_player_knows_no_cell()
    {
        var world = Teams();
        VisibilitySystem.Update(world);

        Assert.Equal(CellVisibility.Shroud, Vision.StateAt(world, PlayerId.Neutral, new CellPos(10, 10)));
    }
}
