using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Events;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Replays;
using Rts.Simulation.Systems;
using Rts.Simulation.World;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The countries of RA2 (loose ends, stage 5): what a country alone has, what it may not have, the USA's paradrop from its air command,
/// a player of no country with the whole faction, a country of another faction refused, and the country kept in the replay. By the
/// game data, without the fog of war; two people play, no bot.
/// </summary>
public sealed class CountryTests
{
    private static readonly PlayerId Union = new(1);
    private static readonly PlayerId Allied = new(2);

    private static MatchSetup Setup(string union, string allied) =>
        new(
            "countries",
            61,
            new MatchOptions(StartingCredits: 30_000, FogOfWar: false),
            [
                new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", Color: 0, Team: 1, StartIndex: 1, new CellPos(4, 4)) { Country = union },
                new MatchPlayer(2, PlayerKind.Human, AiDifficulty.Medium, "alliance", Color: 1, Team: 2, StartIndex: 2, new CellPos(60, 60)) { Country = allied },
            ],
            [new CellPos(4, 4), new CellPos(60, 60)]);

    private static GameSimulation Match(string union, string allied) =>
        new(SimulationSettings.ForMatch(Setup(union, allied), new MapGrid(64, 64)), GameDataFiles.Rules);

    private static Building Build(GameWorld world, PlayerId player, string id, int x, int y)
    {
        world.Rules.Database.TryGetBuilding(id, out var def);
        return world.CreateBuilding(player, def, new CellPos(x, y), true, null);
    }

    private static bool CanBuildUnit(GameWorld world, PlayerId player, string id) =>
        world.Player(player).CanBuildUnit(world.Rules.Database.IndexOfUnit(id));

    private static bool CanBuildBuilding(GameWorld world, PlayerId player, string id) =>
        world.Player(player).CanBuildBuilding(world.Rules.Database.IndexOfBuilding(id));

    /// <summary>Every building the late units and buildings of both sides need.</summary>
    private static GameWorld Base(GameSimulation simulation)
    {
        var world = simulation.World;
        foreach (var (id, x, y) in new[]
        {
            ("construction_yard", 6, 6), ("barracks", 12, 6), ("war_factory", 18, 6), ("radar", 26, 6), ("tech_center", 32, 6),
            ("nuclear_reactor", 6, 14), ("nuclear_reactor", 12, 14),
        })
        {
            Build(world, Union, id, x, y);
        }

        foreach (var (id, x, y) in new[]
        {
            ("alliance_construction_yard", 40, 40), ("alliance_barracks", 46, 40), ("alliance_war_factory", 52, 40),
            ("alliance_air_force_command", 40, 48), ("alliance_battle_lab", 46, 48), ("alliance_power_plant", 52, 48),
            ("alliance_power_plant", 56, 48), ("alliance_power_plant", 56, 54),
        })
        {
            Build(world, Allied, id, x, y);
        }

        TechTree.Update(world);
        return world;
    }

    [Fact]
    public void Russia_builds_the_tesla_tank_and_libya_its_demolition_truck_but_not_the_other_s()
    {
        var russia = Base(Match("russia", "usa"));
        Assert.True(CanBuildUnit(russia, Union, "tesla_tank"));
        Assert.False(CanBuildUnit(russia, Union, "demolition_truck"));
        Assert.True(CanBuildUnit(russia, Union, "heavy_tank"));

        var libya = Base(Match("libya", "usa"));
        Assert.False(CanBuildUnit(libya, Union, "tesla_tank"));
        Assert.True(CanBuildUnit(libya, Union, "demolition_truck"));
    }

    [Fact]
    public void Korea_flies_the_black_eagle_and_no_harrier_and_france_has_the_grand_cannon()
    {
        var korea = Base(Match("russia", "korea"));
        Assert.True(CanBuildUnit(korea, Allied, "alliance_black_eagle"));
        Assert.False(CanBuildUnit(korea, Allied, "alliance_harrier"));
        Assert.False(CanBuildBuilding(korea, Allied, "alliance_grand_cannon"));

        var france = Base(Match("russia", "france"));
        Assert.False(CanBuildUnit(france, Allied, "alliance_black_eagle"));
        Assert.True(CanBuildUnit(france, Allied, "alliance_harrier"));
        Assert.True(CanBuildBuilding(france, Allied, "alliance_grand_cannon"));
    }

    [Fact]
    public void A_player_of_no_country_has_the_whole_faction()
    {
        var world = Base(Match("", ""));
        Assert.True(CanBuildUnit(world, Union, "tesla_tank"));
        Assert.True(CanBuildUnit(world, Union, "demolition_truck"));
        Assert.True(CanBuildUnit(world, Allied, "alliance_harrier"));
        Assert.True(CanBuildUnit(world, Allied, "alliance_black_eagle"));
    }

    [Fact]
    public void The_usa_drops_eight_riflemen_from_its_air_command_and_nobody_else_does()
    {
        var simulation = Match("russia", "usa");
        var world = Base(simulation);
        var command = world.Entities.OfType<Building>().Single(b => b.Def?.Id == "alliance_air_force_command");
        Assert.Equal(Rts.Simulation.Rules.SuperweaponKind.Paradrop, SuperweaponSystem.KindOf(world, command));
        command.ChargeLeft = 0;
        var target = new CellPos(30, 30);
        simulation.Enqueue(new SuperweaponCommand(Allied, simulation.CurrentTick, 0, command.Id, target, target));
        for (var tick = 0; tick < 1500 && !world.Entities.OfType<Unit>().Any(u => u.Type == UnitType.ParadropPlane && !u.IsAlive); tick++)
        {
            simulation.Step();
        }

        var dropped = world.Entities.OfType<Unit>().Count(u => u.IsAlive && u.Type == UnitType.AllianceRifleman
            && Math.Abs(u.Position.ToCell().X - target.X) <= 2 && Math.Abs(u.Position.ToCell().Y - target.Y) <= 2);
        Assert.Equal(8, dropped);

        var korea = Base(Match("russia", "korea"));
        var other = korea.Entities.OfType<Building>().Single(b => b.Def?.Id == "alliance_air_force_command");
        Assert.Equal(Rts.Simulation.Rules.SuperweaponKind.None, SuperweaponSystem.KindOf(korea, other));
    }

    [Fact]
    public void A_country_of_another_faction_is_refused_and_the_replay_keeps_the_country()
    {
        Assert.Contains("country 'usa'", Setup("usa", "").Problem(64, 64, GameDataFiles.Rules.Database));
        Assert.Null(Setup("cuba", "britain").Problem(64, 64, GameDataFiles.Rules.Database));

        var settings = SimulationSettings.ForMatch(Setup("cuba", "britain"), new MapGrid(64, 64));
        var replay = ReplayFormat.Read(ReplayFormat.Write(new Replay(SimulationVersion.Current, GameDataFiles.Current.Hash, settings, 0, [], [])));
        Assert.Equal(["cuba", "britain"], replay.Settings.Match!.Players.Select(p => p.Country));
    }
}
