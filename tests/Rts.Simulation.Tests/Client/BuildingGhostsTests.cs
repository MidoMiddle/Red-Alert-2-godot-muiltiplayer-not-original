using Rts.Client.Terrain;
using Rts.Simulation.Geometry;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>The buildings as a player knows them under the fog (docs/implementation/M4_combat.md, 4.15, step 12).</summary>
public sealed class BuildingGhostsTests
{
    private static readonly PlayerId Me = new(1);
    private static readonly PlayerId Enemy = new(2);

    private static GameWorld World() => new(seed: 1, widthCells: 48, heightCells: 48, TestRules.Standard);

    private static Building Build(GameWorld world, PlayerId owner, string id, int x, int y, bool ready = true) =>
        world.CreateBuilding(owner, TestRules.BuildingDef(id), new CellPos(x, y), ready, null);

    [Fact]
    public void A_building_seen_and_lost_from_sight_stays_as_last_seen()
    {
        var world = World();
        var reactor = Build(world, Enemy, "reactor", 20, 20, ready: false);
        var scout = world.SpawnUnit(Me, UnitType.HeavyTank, new CellPos(20, 24).Center);
        VisibilitySystem.Update(world);
        var ghosts = new BuildingGhosts();
        var seenGrowth = reactor.BuildUpLeft;

        Assert.Equal(new BuildingGhosts.Shown(reactor, seenGrowth, Ghost: false), Assert.Single(ghosts.Update(world, Me)));

        scout.Position = new CellPos(45, 45).Center;
        VisibilitySystem.Update(world);
        world.CompleteBuildUp(reactor);

        Assert.Equal(new BuildingGhosts.Shown(reactor, seenGrowth, Ghost: true), Assert.Single(ghosts.Update(world, Me)));
    }

    [Fact]
    public void A_building_that_fell_in_the_fog_stays_a_ghost_until_its_cells_are_seen()
    {
        var world = World();
        var reactor = Build(world, Enemy, "reactor", 20, 20);
        var scout = world.SpawnUnit(Me, UnitType.HeavyTank, new CellPos(20, 24).Center);
        VisibilitySystem.Update(world);
        var ghosts = new BuildingGhosts();
        ghosts.Update(world, Me);

        scout.Position = new CellPos(45, 45).Center;
        VisibilitySystem.Update(world);
        world.DestroyBuilding(reactor, null);
        DeathSystem.Update(world, null);
        world.RemoveDead();

        Assert.True(Assert.Single(ghosts.Update(world, Me)).Ghost);

        scout.Position = new CellPos(20, 24).Center;
        VisibilitySystem.Update(world);

        Assert.Empty(ghosts.Update(world, Me));
    }

    [Fact]
    public void A_building_never_seen_is_not_shown_and_an_own_one_always_is()
    {
        var world = World();
        var enemy = Build(world, Enemy, "reactor", 30, 30);
        var own = Build(world, Me, "reactor", 2, 2);
        VisibilitySystem.Update(world);
        var ghosts = new BuildingGhosts();

        Assert.Equal(own, Assert.Single(ghosts.Update(world, Me)).Building);

        var all = ghosts.Update(world, Me, revealed: true);
        Assert.Equal(2, all.Count);
        Assert.All(all, shown => Assert.False(shown.Ghost));
        Assert.Contains(all, shown => shown.Building == enemy);
    }
}
