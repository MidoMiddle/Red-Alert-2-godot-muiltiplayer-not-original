using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The options of the skirmish screen of RA2 that change the rules: MCV Repacks, Superweapons, Destroyable Bridges. By the game data;
/// two people play, no bot.
/// </summary>
public sealed class MatchOptionsTests
{
    private static readonly PlayerId Union = new(1);

    private static GameSimulation Match(MatchOptions options, MapGrid? map = null) =>
        new(
            SimulationSettings.ForMatch(
                new MatchSetup(
                    "options",
                    71,
                    options,
                    [
                        new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)),
                        new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)),
                    ],
                    [new CellPos(4, 4), new CellPos(60, 60)]),
                map ?? new MapGrid(64, 64)),
            GameDataFiles.Rules);

    private static Building Build(GameWorld world, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(Union, def, new CellPos(x, y), true, null);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_yard_packs_up_only_with_mcv_repacks(bool repacks)
    {
        var simulation = Match(new MatchOptions(StartingCredits: 10_000, McvRepacks: repacks));
        var yard = Build(simulation.World, "construction_yard", 20, 20);
        simulation.Enqueue(new DeployCommand(Union, simulation.CurrentTick, 0, yard.Id));
        simulation.Step();
        Assert.Equal(!repacks, yard.IsAlive);
    }

    [Fact]
    public void Without_superweapons_their_buildings_are_not_in_the_tree()
    {
        foreach (var allowed in new[] { true, false })
        {
            var simulation = Match(new MatchOptions(StartingCredits: 30_000, Superweapons: allowed));
            var world = simulation.World;
            Build(world, "construction_yard", 6, 6);
            Build(world, "radar", 12, 6);
            Build(world, "tech_center", 18, 6);
            Build(world, "nuclear_reactor", 6, 14);
            TechTree.Update(world);
            Assert.Equal(allowed, world.Player(Union).CanBuildBuilding(world.Rules.Database.IndexOfBuilding("iron_curtain")));
        }
    }

    [Fact]
    public void Without_destroyable_bridges_a_shot_stops_at_the_bridge()
    {
        const int Size = 64;
        var land = new byte[Size * Size];
        for (var i = 0; i < land.Length; i++)
        {
            land[i] = (byte)(i % Size is >= 30 and <= 33 ? LandType.Deep : LandType.Clear);
        }

        var map = MapGrid.FromLayers(Size, Size, land, new byte[land.Length], default, null,
            [BridgeState.New(BridgeKind.WoodLow, new CellPos(30, 8), new CellPos(33, 8))]);
        var world = Match(new MatchOptions(StartingCredits: 10_000, DestroyableBridges: false), map).World;
        var segment = world.Map.Bridges[0].Segments[1];
        var hp = segment.Hp;
        Assert.True(world.DamageAtCell(segment.Cell, 5000, null));
        Assert.Equal(hp, segment.Hp);
    }
}
