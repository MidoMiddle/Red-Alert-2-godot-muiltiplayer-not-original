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
/// Deploying and the superweapons of a bot (M11 step 3, the bot that knows every weapon), on short scenes of the game's rules
/// (<see cref="DeployManager"/>, <see cref="SuperweaponManager"/>). The bot's start is the north-west corner.
/// </summary>
public sealed class DeployAndSuperweaponScenarioTests
{
    private static GameRules Game => GameDataFiles.Rules;

    [Fact]
    public void A_gi_digs_in_with_an_armed_enemy_in_reach_and_not_without()
    {
        var scene = new Scene(rules: Game);
        var gi = scene.Spawn(Scene.Me, Type("alliance_rifleman"), 20, 20);
        scene.Spawn(Scene.Enemy, Type("conscript"), 23, 20);
        See(scene, new CellPos(20, 20));
        scene.Look();
        new DeployManager().Update(scene.Context());
        Assert.Equal(gi.Id, Assert.Single(scene.RunCommands().OfType<DeployCommand>()).Unit);

        var alone = new Scene(rules: Game);
        alone.Spawn(Scene.Me, Type("alliance_rifleman"), 20, 20);
        alone.Spawn(Scene.Enemy, Type("conscript"), 40, 40);
        See(alone, new CellPos(20, 20));
        alone.Look();
        new DeployManager().Update(alone.Context());
        Assert.Empty(alone.RunCommands());
    }

    [Fact]
    public void A_deployed_unit_is_not_deployed_again()
    {
        var scene = new Scene(rules: Game);
        var gi = scene.Spawn(Scene.Me, Type("alliance_rifleman"), 20, 20);
        gi.Deployed = true;
        scene.Spawn(Scene.Enemy, Type("conscript"), 23, 20);
        See(scene, new CellPos(20, 20));
        scene.Look();

        new DeployManager().Update(scene.Context());

        Assert.Empty(scene.RunCommands());
    }

    [Fact]
    public void Yuri_lets_his_wave_off_among_enemy_soldiers_but_not_by_his_own()
    {
        var scene = new Scene(rules: Game);
        var yuri = scene.Spawn(Scene.Me, Type("yuri"), 20, 20);
        scene.Spawn(Scene.Me, Type("yuri"), 21, 21);
        scene.Spawn(Scene.Enemy, Type("alliance_rifleman"), 22, 20);
        scene.Spawn(Scene.Enemy, Type("alliance_rifleman"), 20, 22);
        See(scene, new CellPos(20, 20));
        scene.Look();
        new DeployManager().Update(scene.Context());

        // His own kind the wave spares: the other Yuri does not hold him back.
        Assert.Contains(scene.RunCommands().OfType<DeployCommand>(), deploy => deploy.Unit == yuri.Id);

        var friends = new Scene(rules: Game);
        friends.Spawn(Scene.Me, Type("yuri"), 20, 20);
        friends.Spawn(Scene.Me, Type("conscript"), 21, 20);
        friends.Spawn(Scene.Enemy, Type("alliance_rifleman"), 22, 20);
        friends.Spawn(Scene.Enemy, Type("alliance_rifleman"), 20, 22);
        See(friends, new CellPos(20, 20));
        friends.Look();
        new DeployManager().Update(friends.Context());
        Assert.DoesNotContain(friends.RunCommands(), command => command is DeployCommand);
    }

    [Fact]
    public void The_desolator_poisons_two_enemy_soldiers_and_not_one()
    {
        var two = new Scene(rules: Game);
        var desolator = two.Spawn(Scene.Me, Type("desolator"), 20, 20);
        two.Spawn(Scene.Enemy, Type("alliance_rifleman"), 21, 20);
        two.Spawn(Scene.Enemy, Type("alliance_rifleman"), 20, 22);
        See(two, new CellPos(20, 20));
        two.Look();
        new DeployManager().Update(two.Context());
        Assert.Equal(desolator.Id, Assert.Single(two.RunCommands().OfType<DeployCommand>()).Unit);

        var one = new Scene(rules: Game);
        one.Spawn(Scene.Me, Type("desolator"), 20, 20);
        one.Spawn(Scene.Enemy, Type("alliance_rifleman"), 21, 20);
        See(one, new CellPos(20, 20));
        one.Look();
        new DeployManager().Update(one.Context());
        Assert.Empty(one.RunCommands());
    }

    [Fact]
    public void The_missile_falls_on_the_enemy_base_but_not_where_the_bot_stands()
    {
        var scene = new Scene(rules: Game);
        var silo = Silo(scene, "nuclear_missile_silo");
        Own(scene, Scene.Enemy, "war_factory", 40, 40);
        Own(scene, Scene.Enemy, "refinery", 46, 40);
        Own(scene, Scene.Enemy, "barracks", 40, 45);
        Own(scene, Scene.Enemy, "reactor", 58, 58);
        See(scene, new CellPos(46, 46), 14);
        scene.Look();
        new SuperweaponManager().Update(scene.Context());
        var fire = Assert.Single(scene.RunCommands().OfType<SuperweaponCommand>());
        Assert.Equal(silo.Id, fire.Building);
        Assert.InRange(fire.Target.X, 38, 50);
        Assert.InRange(fire.Target.Y, 38, 50);

        var near = new Scene(rules: Game);
        Silo(near, "nuclear_missile_silo");
        Own(near, Scene.Enemy, "war_factory", 40, 40);
        Own(near, Scene.Enemy, "refinery", 46, 40);
        Own(near, Scene.Enemy, "barracks", 40, 45);
        near.Spawn(Scene.Me, Type("heavy_tank"), 44, 44);
        See(near, new CellPos(46, 46), 14);
        near.Look();
        new SuperweaponManager().Update(near.Context());
        Assert.Empty(near.RunCommands());
    }

    [Fact]
    public void The_curtain_covers_the_tanks_of_a_squad_that_fights_and_kills_no_soldier_of_it()
    {
        var scene = new Scene(rules: Game);
        Silo(scene, "iron_curtain");
        var tanks = Enumerable.Range(0, 3).Select(i => scene.Spawn(Scene.Me, Type("heavy_tank"), 30 + i, 30)).ToList();
        scene.Look();
        var context = scene.Context();
        context.Plan.Squads = [Squad(SquadState.Fighting, tanks)];
        new SuperweaponManager().Update(context);
        var fire = Assert.Single(scene.RunCommands().OfType<SuperweaponCommand>());
        Assert.Equal(new CellPos(31, 30), fire.Target);

        var mixed = new Scene(rules: Game);
        Silo(mixed, "iron_curtain");
        var units = Enumerable.Range(0, 3).Select(i => mixed.Spawn(Scene.Me, Type("heavy_tank"), 30 + i, 30)).ToList();
        units.Add(mixed.Spawn(Scene.Me, Type("conscript"), 31, 31));
        mixed.Look();
        context = mixed.Context();
        context.Plan.Squads = [Squad(SquadState.Fighting, units)];
        new SuperweaponManager().Update(context);
        Assert.Empty(mixed.RunCommands());
    }

    [Fact]
    public void The_gate_moves_the_tanks_of_a_squad_far_out_short_of_its_target()
    {
        var scene = new Scene(rules: Game);
        Silo(scene, "alliance_chronosphere");
        var tanks = Enumerable.Range(0, 3).Select(i => scene.Spawn(Scene.Me, Type("alliance_light_tank"), 10 + i, 10)).ToList();
        scene.Look();
        var context = scene.Context();
        var squad = Squad(SquadState.Moving, tanks);
        squad.TargetCell = new CellPos(50, 50);
        context.Plan.Squads = [squad];

        new SuperweaponManager().Update(context);

        var fire = Assert.Single(scene.RunCommands().OfType<SuperweaponCommand>());
        Assert.Equal(new CellPos(11, 10), fire.Target);
        Assert.Equal(new CellPos(50 - 6, 50 - 6), fire.Second);
    }

    [Fact]
    public void The_paradrop_falls_on_the_enemy_refinery()
    {
        var text = new StringBuilder();
        for (var y = 0; y < 64; y++)
        {
            text.AppendLine(string.Join(' ', Enumerable.Repeat(".", 64)));
        }

        text.AppendLine("building tech_airfield 12 12 1");
        var scene = new Scene(rules: Game, map: TestMapParser.Parse(text.ToString(), Game).Grid);
        var airfield = scene.World.BuildingAt(new CellPos(12, 12))!;
        airfield.ChargeLeft = 0;
        Own(scene, Scene.Enemy, "barracks", 30, 30);
        var refinery = Own(scene, Scene.Enemy, "refinery", 50, 50);
        See(scene, new CellPos(40, 40), 20);
        scene.Look();

        new SuperweaponManager().Update(scene.Context());

        var fire = Assert.Single(scene.RunCommands().OfType<SuperweaponCommand>());
        Assert.Equal((airfield.Id, refinery.Cell), (fire.Building, fire.Target));
    }

    /// <summary>The superweapon <paramref name="id"/> of the bot by its start, charged, with reactors to power it.</summary>
    private static Building Silo(Scene scene, string id)
    {
        Own(scene, Scene.Me, "reactor", 2, 2);
        Own(scene, Scene.Me, "reactor", 2, 5);
        Own(scene, Scene.Me, "reactor", 6, 2);
        var silo = Own(scene, Scene.Me, id, 6, 6);
        silo.ChargeLeft = 0;
        return silo;
    }

    private static Squad Squad(SquadState state, System.Collections.Generic.IEnumerable<Unit> units)
    {
        var squad = new Squad(1) { State = state };
        foreach (var unit in units)
        {
            squad.Add(unit.Id);
        }

        return squad;
    }

    private static void See(Scene scene, CellPos centre, int radius = 10) => scene.World.VisionOf(Scene.Me).Reveal(centre, radius);

    private static Building Own(Scene scene, PlayerId owner, string id, int x, int y) =>
        scene.World.CreateBuilding(owner, Game.Database.Buildings[Game.Database.IndexOfBuilding(id)], new CellPos(x, y), ready: true, null);

    private static UnitType Type(string id) => UnitTypes.TryGetById(id, out var type) ? type : throw new System.ArgumentException(id);
}
