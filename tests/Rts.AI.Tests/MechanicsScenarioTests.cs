using System.Linq;
using System.Text;
using Rts.AI.Modules;
using Rts.AI.Squads;
using Rts.Data.Tests;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Map;
using Rts.Simulation.Rules;
using Rts.Simulation.World;
using Xunit;

namespace Rts.AI.Tests;

/// <summary>
/// The mechanics a bot uses (M11 step 3, the bot that knows every weapon), on short scenes of the game's rules: the engineer that takes a
/// tech building (<see cref="EngineerManager"/>), the soldiers that hold a civilian building by the base (<see cref="GarrisonManager"/>),
/// the units that fire only when ordered (<see cref="StrikeManager"/>). The bot is of the Union, its base in the north-west corner.
/// </summary>
public sealed class MechanicsScenarioTests
{
    private static GameRules Game => GameDataFiles.Rules;

    [Fact]
    public void A_tech_building_seen_by_the_base_gets_an_engineer_from_the_barracks()
    {
        var scene = Map("building tech_oil_derrick 20 8");
        Base(scene);
        See(scene);
        scene.World.Player(Scene.Me).Credits = 2000;
        scene.Look();

        new EngineerManager(Scene.Settings).Update(scene.Context());

        var produce = Assert.Single(scene.RunCommands().OfType<ProduceCommand>());
        Assert.Equal("engineer", Game.Database.Units[produce.DefIndex].Id);
    }

    [Fact]
    public void The_engineer_is_sent_into_the_tech_building()
    {
        var scene = Map("building tech_oil_derrick 20 8");
        Base(scene);
        See(scene);
        var engineer = scene.Spawn(Scene.Me, Type("engineer"), 10, 12);
        scene.Look();

        new EngineerManager(Scene.Settings).Update(scene.Context());

        var enter = Assert.Single(scene.RunCommands().OfType<EnterCommand>());
        Assert.Equal(engineer.Id, Assert.Single(enter.Units));
        Assert.Equal(scene.World.BuildingAt(new CellPos(20, 8))!.Id, enter.Building);
    }

    [Fact]
    public void A_guarded_tech_building_is_left_alone_and_easy_takes_none()
    {
        var guarded = Map("building tech_oil_derrick 20 8");
        Base(guarded);
        See(guarded);
        guarded.World.Player(Scene.Me).Credits = 2000;
        guarded.Spawn(Scene.Enemy, Type("heavy_tank"), 23, 9);
        guarded.Look();
        new EngineerManager(Scene.Settings).Update(guarded.Context());
        Assert.Empty(guarded.RunCommands());

        var easy = Map("building tech_oil_derrick 20 8");
        Base(easy);
        See(easy);
        easy.World.Player(Scene.Me).Credits = 2000;
        easy.Look();
        new EngineerManager(Scene.Settings with { Quality = Profiles.BuildOrderQuality.Simple }).Update(easy.Context());
        Assert.Empty(easy.RunCommands());
    }

    [Fact]
    public void Soldiers_at_home_hold_a_house_by_the_base_and_the_squads_leave_them()
    {
        var scene = Map("building neutral_house_a 15 8");
        Base(scene);
        See(scene);
        var conscripts = Enumerable.Range(0, 4).Select(i => scene.Spawn(Scene.Me, Type("conscript"), 8 + i, 12)).ToList();
        scene.Look();
        var context = scene.Context();

        new GarrisonManager(Scene.Settings).Update(context);
        new SquadManager(Scene.Settings).Update(context);

        // The house holds three: the three nearest go in, the fourth stays in the squad.
        var enter = Assert.Single(scene.RunCommands().OfType<EnterCommand>());
        Assert.Equal(3, enter.Units.Length);
        Assert.Equal(scene.World.BuildingAt(new CellPos(15, 8))!.Id, enter.Building);
        var squad = Assert.Single(context.Plan.Squads);
        Assert.Equal(conscripts.Select(unit => unit.Id).Except(enter.Units), squad.Units);
    }

    [Fact]
    public void A_house_far_from_the_base_is_not_held()
    {
        var scene = Map("building neutral_house_a 40 40");
        Base(scene);
        scene.World.VisionOf(Scene.Me).Reveal(new CellPos(40, 40), 6);
        scene.Spawn(Scene.Me, Type("conscript"), 8, 12);
        scene.Look();

        new GarrisonManager(Scene.Settings).Update(scene.Context());

        Assert.Empty(scene.RunCommands());
    }

    [Fact]
    public void Rocket_artillery_strikes_the_enemy_in_reach_worth_the_most()
    {
        var scene = Map();
        var v3 = scene.Spawn(Scene.Me, Type("rocket_artillery"), 20, 20);
        scene.Spawn(Scene.Enemy, Type("conscript"), 27, 20);
        var factory = Enemy(scene, "war_factory", 32, 19);
        Enemy(scene, "reactor", 50, 50);
        See(scene, new CellPos(30, 20), 14);
        scene.Look();

        new StrikeManager().Update(scene.Context());

        // The war factory is worth more than the conscript; the reactor is out of reach.
        var attack = Assert.Single(scene.RunCommands().OfType<AttackCommand>());
        Assert.Equal(v3.Id, Assert.Single(attack.Units));
        Assert.Equal(factory.Id, attack.Target);
    }

    [Fact]
    public void A_jet_strikes_with_its_ammo_full_only()
    {
        var full = Map();
        full.Spawn(Scene.Me, Type("alliance_harrier"), 10, 10);
        var target = Enemy(full, "war_factory", 40, 10);
        See(full, new CellPos(42, 11), 6);
        full.Look();
        new StrikeManager().Update(full.Context());
        Assert.Equal(target.Id, Assert.Single(full.RunCommands().OfType<AttackCommand>()).Target);

        var empty = Map();
        var jet = empty.Spawn(Scene.Me, Type("alliance_harrier"), 10, 10);
        jet.Ammo = 0;
        Enemy(empty, "war_factory", 40, 10);
        See(empty, new CellPos(42, 11), 6);
        empty.Look();
        new StrikeManager().Update(empty.Context());
        Assert.DoesNotContain(empty.RunCommands(), command => command is AttackCommand);
    }

    [Fact]
    public void A_striker_with_nothing_in_reach_follows_the_squad_that_is_out()
    {
        var scene = Map();
        var v3 = scene.Spawn(Scene.Me, Type("rocket_artillery"), 10, 10);
        var tank = scene.Spawn(Scene.Me, Type("heavy_tank"), 40, 40);
        scene.Look();
        var context = scene.Context();
        var squad = new Squad(1) { State = SquadState.Moving, Centre = new CellPos(40, 40) };
        squad.Add(tank.Id);
        context.Plan.Squads = [squad];

        new StrikeManager().Update(context);

        var move = Assert.Single(scene.RunCommands().OfType<MoveCommand>());
        Assert.Equal(v3.Id, Assert.Single(move.Units));
        Assert.Equal(new CellPos(40, 40), move.Target.ToCell());
        Assert.False(context.Units.InArmy(v3));
    }

    /// <summary>A scene of the game's rules on a 64 × 64 map of clear land with the lines of <see cref="TestMapParser"/>.</summary>
    private static Scene Map(params string[] lines)
    {
        var text = new StringBuilder();
        for (var y = 0; y < 64; y++)
        {
            text.AppendLine(string.Join(' ', Enumerable.Repeat(".", 64)));
        }

        foreach (var line in lines)
        {
            text.AppendLine(line);
        }

        return new Scene(rules: Game, map: TestMapParser.Parse(text.ToString(), Game).Grid);
    }

    /// <summary>The base of the bot: the yard, a reactor, a refinery, barracks.</summary>
    private static void Base(Scene scene)
    {
        Own(scene, Scene.Me, "construction_yard", 6, 6);
        Own(scene, Scene.Me, "reactor", 2, 2);
        Own(scene, Scene.Me, "refinery", 10, 2);
        Own(scene, Scene.Me, "barracks", 2, 9);
    }

    /// <summary>The bot's player sees round its base, or round <paramref name="centre"/>.</summary>
    private static void See(Scene scene, CellPos? centre = null, int radius = 20) =>
        scene.World.VisionOf(Scene.Me).Reveal(centre ?? new CellPos(12, 10), radius);

    private static Building Enemy(Scene scene, string id, int x, int y) => Own(scene, Scene.Enemy, id, x, y);

    private static Building Own(Scene scene, Rts.Simulation.World.PlayerId owner, string id, int x, int y) =>
        scene.World.CreateBuilding(owner, Game.Database.Buildings[Game.Database.IndexOfBuilding(id)], new CellPos(x, y), ready: true, null);

    private static UnitType Type(string id) => UnitTypes.TryGetById(id, out var type) ? type : throw new System.ArgumentException(id);
}
