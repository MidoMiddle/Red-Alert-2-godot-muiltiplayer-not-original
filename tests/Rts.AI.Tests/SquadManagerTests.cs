using Rts.AI.Modules;
using Rts.AI.Squads;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// What the squads of a bot do (docs/implementation/M5_first_bot.md, 4.6, step 8), on scenes. The bot starts at (8, 8) of the corners of
/// <see cref="Scene"/>, the far corner (56, 56) the first start where the enemy may be: the gathering point is (14, 14).
/// </summary>
public sealed class SquadManagerTests
{
    private static readonly CellPos Gather = new(14, 14);

    /// <summary>
    /// Squads of two units, so that the scenes stay small; out at once and wave after wave — the window of the first attack and the
    /// interval of the waves (M8 step 8) are tested apart, in <see cref="AttackTimingTests"/>.
    /// </summary>
    private static readonly AiSettings Small = AiSettings.Medium with { MinAttackArmy = 2, FirstAttackFrom = 0, WaveInterval = 0, AssumedEnemyPercent = 0 };

    /// <summary>
    /// One turn of the squads after <paramref name="tick"/>: the world plays on to it, <paramref name="then"/> changes the scene; the
    /// commands the turn gave, once they ran.
    /// </summary>
    private static List<Command> Turn(Scene scene, SquadManager squads, AiContext context, int tick, Action? then = null)
    {
        while (scene.Simulation.CurrentTick < tick)
        {
            scene.Simulation.Step();
        }

        then?.Invoke();
        scene.Look(tick);
        var before = scene.Simulation.ExecutedCommands.Count;
        context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = tick });
        squads.Update(context);
        context.End();
        scene.Simulation.Step();
        scene.Simulation.Step();
        return scene.Simulation.ExecutedCommands.Skip(before).Where(command => command.Player == Scene.Me).ToList();
    }

    /// <summary>Two tanks at the gathering point that set out on the first turn; the scene, the squads, the context.</summary>
    private static (Scene Scene, SquadManager Squads, AiContext Context, Unit A, Unit B) SetOut(AiSettings? settings = null)
    {
        var scene = new Scene();
        var a = scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        var b = scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        var context = scene.Context();
        var squads = new SquadManager(settings ?? Small);
        Turn(scene, squads, context, 0);
        Assert.Equal(SquadState.Moving, context.Plan.Squads.Single().State);
        return (scene, squads, context, a, b);
    }

    [Fact]
    public void Gathering_takes_the_fighters_but_not_the_scout_nor_a_harvester_and_sends_them_to_the_gathering_point()
    {
        var scene = new Scene();
        var a = scene.Spawn(Scene.Me, UnitType.Conscript, 8, 8);
        var b = scene.Spawn(Scene.Me, UnitType.Rocketeer, 9, 8);
        var scout = scene.Spawn(Scene.Me, UnitType.Conscript, 10, 8);
        scene.Spawn(Scene.Me, UnitType.Harvester, 8, 10);
        var context = scene.Context();
        context.Plan.Scout = scout.Id;

        var commands = Turn(scene, new SquadManager(AiSettings.Medium), context, 0);

        var squad = Assert.Single(context.Plan.Squads);
        Assert.Equal(SquadState.Gathering, squad.State);
        Assert.Equal([a.Id, b.Id], squad.Units.AsEnumerable());
        var order = Assert.IsType<AttackMoveCommand>(Assert.Single(commands));
        Assert.Equal(Gather, order.Target.ToCell());
        Assert.Equal([a.Id, b.Id], order.Units.AsEnumerable());
    }

    [Fact]
    public void The_same_order_is_not_given_twice()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.Conscript, 8, 8);
        var context = scene.Context();
        var squads = new SquadManager(AiSettings.Medium);

        Assert.Single(Turn(scene, squads, context, 0));
        Assert.Empty(Turn(scene, squads, context, 15));
    }

    [Fact]
    public void A_squad_sets_out_with_enough_units_and_strength_and_the_next_wave_needs_more()
    {
        var scene = new Scene();
        var settings = AiSettings.Medium with { MinAttackArmy = 4, FirstAttackFrom = 0, WaveInterval = 0, AssumedEnemyPercent = 0 };
        var squads = new SquadManager(settings);
        var context = scene.Context();
        for (var i = 0; i < 3; i++)
        {
            scene.Spawn(Scene.Me, UnitType.Conscript, 13 + i, 14);
        }

        // Three of four: it gathers.
        Turn(scene, squads, context, 0);
        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);

        // Four, but an enemy army seen stronger than they are: it still gathers.
        var tanks = new List<Unit>();
        var vision = scene.World.VisionOf(Scene.Me);
        Turn(scene, squads, context, 15, () =>
        {
            scene.Spawn(Scene.Me, UnitType.Conscript, 16, 14);
            tanks.AddRange(Enumerable.Range(0, 5).Select(i => scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 30 + i, 30)));
            vision.Reveal(new CellPos(32, 30), 3);
        });
        vision.Hide(new CellPos(32, 30), 3);
        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);

        // The army gone and, three minutes on, faded from what the bot knows (M8 step 10, EnemyEstimate.ForgetTicks): it sets out, the first
        // wave, to where the enemy was seen.
        foreach (var tank in tanks)
        {
            tank.MarkDead();
        }

        scene.World.RemoveDead();

        var commands = Turn(scene, squads, context, 15 + Rts.AI.View.EnemyEstimate.ForgetTicks);
        var first = context.Plan.Squads.Single();
        Assert.Equal(SquadState.Moving, first.State);
        Assert.Equal(1, squads.Waves);
        Assert.Equal(scene.Memory.LastEnemyCell, first.TargetCell);
        Assert.Contains(commands, command => command is AttackMoveCommand move && move.Target.ToCell() == first.TargetCell);

        // Four new ones gather; the second wave needs 4 × 1.25 = 5.
        for (var i = 0; i < 4; i++)
        {
            scene.Spawn(Scene.Me, UnitType.Conscript, 8 + i, 9);
        }

        Turn(scene, squads, context, 30 + Rts.AI.View.EnemyEstimate.ForgetTicks, () =>
        {
            foreach (var id in first.Units)
            {
                scene.World.TryGet(id, out var unit);
                Scene.Move((Unit)unit!, 40, 40);
            }
        });
        Assert.Equal(SquadState.Gathering, context.Plan.Squads[1].State);
        Assert.Equal(4, context.Plan.Squads[1].Units.Count);
    }

    [Fact]
    public void A_ready_squad_waits_while_the_plan_holds_the_attack_back()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        var context = scene.Context();
        context.Plan.AttackAllowed = false;
        var squads = new SquadManager(Small);

        Assert.Empty(Turn(scene, squads, context, 0));
        Assert.Equal(SquadState.Ready, context.Plan.Squads.Single().State);

        context.Plan.AttackAllowed = true;
        Assert.NotEmpty(Turn(scene, squads, context, 15));
        Assert.Equal(SquadState.Moving, context.Plan.Squads.Single().State);
    }

    [Fact]
    public void The_target_is_a_refinery_before_a_defense_a_factory_and_the_yard_then_where_the_enemy_was_seen()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        // Seen once, far off, then in the fog: the refinery is the farthest of them.
        var yard = scene.Build(Scene.Enemy, "construction_yard", 40, 40);
        var factory = scene.Build(Scene.Enemy, "war_factory", 44, 40);
        var refinery = scene.Build(Scene.Enemy, "refinery", 50, 50);
        var vision = scene.World.VisionOf(Scene.Me);
        vision.Reveal(new CellPos(46, 46), 10);
        scene.Look(0);
        vision.Hide(new CellPos(46, 46), 10);
        var context = scene.Context();
        var squads = new SquadManager(Small);

        Turn(scene, squads, context, 1);
        var squad = context.Plan.Squads.Single();
        Assert.Equal(refinery.Id, squad.TargetBuilding);

        // The refinery seen gone: the factory before the yard.
        Turn(scene, squads, context, 15, () =>
        {
            refinery.MarkDead();
            scene.World.RemoveDead();
            vision.Reveal(new CellPos(51, 51), 3);
        });
        Assert.Equal(factory.Id, squad.TargetBuilding);
        Assert.NotEqual(factory.Cell, yard.Cell);
    }

    [Fact]
    public void Defenses_come_before_factories_and_the_yard_last()
    {
        var rules = TestRules.Standard.Database;
        int Rank(string id) => SquadManager.Rank(rules.Buildings[rules.IndexOfBuilding(id)], AiSettings.Medium.TargetPriority.Split(','));

        Assert.True(Rank("refinery") < Rank("tesla_coil"));
        Assert.True(Rank("tesla_coil") < Rank("war_factory"));
        Assert.True(Rank("barracks") < Rank("construction_yard"));
        Assert.True(Rank("construction_yard") < Rank("reactor"));
    }

    [Fact]
    public void Without_buildings_it_goes_where_the_enemy_was_last_seen()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        var engineer = scene.Spawn(Scene.Enemy, UnitType.Engineer, 30, 20);
        var vision = scene.World.VisionOf(Scene.Me);
        vision.Reveal(new CellPos(30, 20), 2);
        scene.Look(0);
        vision.Hide(new CellPos(30, 20), 2);
        Scene.Move(engineer, 60, 60);
        var context = scene.Context();
        var squads = new SquadManager(Small);

        Turn(scene, squads, context, 1);

        Assert.Equal(new CellPos(30, 20), context.Plan.Squads.Single().TargetCell);
    }

    [Fact]
    public void A_squad_spread_out_waits_for_its_laggards()
    {
        var (scene, squads, context, a, b) = SetOut();

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 30, 30);
            Scene.Move(b, 14, 14);
        });

        var squad = context.Plan.Squads.Single();
        Assert.NotNull(squad.Regroup);
        var order = Assert.IsType<AttackMoveCommand>(Assert.Single(commands));
        Assert.Equal(squad.Regroup, order.Target.ToCell());
    }

    [Fact]
    public void In_a_fight_of_hard_it_fires_at_the_enemy_it_kills_soonest_and_the_wounded_fall_back()
    {
        // The micro of «Hard» (M8 step 5): tank shells kill the enemy tank sooner than the nearer conscript, which they barely scratch —
        // within the reach of the tank (M8 step 10). The trade is not what this scene is about: with one tank wounded the squad would turn
        // back (MicroTests).
        var (scene, squads, context, a, b) = SetOut(Small with { Micro = Rts.AI.Profiles.MicroLevel.RetreatWounded, RetreatThresholdPercent = 0 });
        Unit? tank = null;

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 24, 25);
            tank = scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 29, 24);
            scene.Spawn(Scene.Enemy, UnitType.Conscript, 27, 24);
            b.Hp = b.MaxHp / 4;
        });

        Assert.Equal(SquadState.Fighting, context.Plan.Squads.Single().State);
        var attack = Assert.Single(commands.OfType<AttackCommand>());
        Assert.Equal(tank!.Id, attack.Target);
        Assert.Equal([a.Id], attack.Units.AsEnumerable());
        // The wounded one three cells back from the middle, toward home.
        var back = Assert.Single(commands.OfType<MoveCommand>());
        Assert.Equal([b.Id], back.Units.AsEnumerable());
        Assert.True(back.Target.ToCell().X < 24 && back.Target.ToCell().Y < 24, $"{back.Target.ToCell()}");
    }

    [Fact]
    public void Below_a_third_of_its_strength_it_turns_back_to_the_gathering_point()
    {
        var (scene, squads, context, a, b) = SetOut();

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 40, 40);
            Scene.Move(b, 41, 40);
            a.Hp = a.MaxHp / 10;
            b.Hp = b.MaxHp / 10;
        });

        var squad = context.Plan.Squads.Single();
        Assert.Equal(SquadState.Retreating, squad.State);
        Assert.True(squad.Beaten);
        var move = Assert.IsType<MoveCommand>(Assert.Single(commands));
        Assert.Equal(Gather, move.Target.ToCell());

        // Back at the gathering point it joins the squad gathering there.
        Turn(scene, squads, context, 30, () =>
        {
            Scene.Move(a, 14, 14);
            Scene.Move(b, 15, 14);
        });
        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);
        Assert.NotSame(squad, context.Plan.Squads.Single());
    }

    [Fact]
    public void With_a_threat_at_the_base_the_squad_at_home_goes_for_it_and_does_not_set_out()
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        var context = scene.Context();
        context.Plan.Threat = new AiThreat(new CellPos(4, 20), new CellPos(4, 20), 100);

        var commands = Turn(scene, new SquadManager(Small), context, 0);

        Assert.Equal(SquadState.Gathering, context.Plan.Squads.Single().State);
        var order = Assert.IsType<AttackMoveCommand>(Assert.Single(commands));
        Assert.Equal(new CellPos(4, 20), order.Target.ToCell());
    }

    [Fact]
    public void A_squad_called_back_comes_by_a_plain_move_fights_at_the_base_and_goes_home_once_it_is_safe()
    {
        var (scene, squads, context, a, b) = SetOut();
        var squad = context.Plan.Squads.Single();
        var threat = new CellPos(10, 10);
        context.Plan.Threat = new AiThreat(threat, threat, 500);
        squad.Recalled = true;

        // One far off comes by a plain move, one near goes for the threat.
        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 50, 50);
            Scene.Move(b, 20, 12);
        });
        Assert.Equal(SquadState.Defending, squad.State);
        var move = Assert.Single(commands.OfType<MoveCommand>());
        Assert.Equal([a.Id], move.Units.AsEnumerable());
        Assert.Equal(threat, move.Target.ToCell());
        var attack = Assert.Single(commands.OfType<AttackMoveCommand>());
        Assert.Equal([b.Id], attack.Units.AsEnumerable());

        // Safe again: home, not beaten.
        context.Plan.Threat = null;
        Turn(scene, squads, context, 30);
        Assert.Equal(SquadState.Retreating, squad.State);
        Assert.False(squad.Recalled);
        Assert.False(squad.Beaten);
    }

    [Fact]
    public void The_fallen_leave_their_squad_and_an_empty_squad_is_dropped()
    {
        var (scene, squads, context, a, b) = SetOut();

        Turn(scene, squads, context, 15, () =>
        {
            a.MarkDead();
            scene.World.RemoveDead();
        });
        Assert.Equal([b.Id], context.Plan.Squads.Single().Units);

        Turn(scene, squads, context, 30, () =>
        {
            b.MarkDead();
            scene.World.RemoveDead();
        });
        Assert.Empty(context.Plan.Squads);
    }

    [Fact]
    public void A_squad_that_raids_goes_for_the_field_an_enemy_harvester_was_seen_at_before_the_refinery()
    {
        // M8 step 10: the raids of the «Saboteur»; with no harvester remembered it goes for the buildings as before.
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        var refinery = scene.Build(Scene.Enemy, "refinery", 50, 50);
        scene.Spawn(Scene.Enemy, UnitType.Harvester, 40, 30);
        var vision = scene.World.VisionOf(Scene.Me);
        vision.Reveal(new CellPos(45, 40), 14);
        scene.Look(0);
        vision.Hide(new CellPos(45, 40), 14);
        var context = scene.Context();

        Turn(scene, new SquadManager(Small with { Raids = true }), context, 1);
        var squad = context.Plan.Squads.Single();
        Assert.Equal((null, new CellPos(40, 30)), (squad.TargetBuilding, squad.TargetCell));

        var other = scene.Context();
        Turn(scene, new SquadManager(Small), other, 15);
        Assert.Equal(refinery.Id, other.Plan.Squads.Single().TargetBuilding);
    }

    /// <summary>
    /// The scene of the raids (M8 step 10a): an enemy refinery at (50, 50), its field at (46, 50), both seen once; the squad of the bot
    /// raids. <paramref name="more"/> adds to the scene before the look.
    /// </summary>
    private static (Scene Scene, SquadManager Squads, AiContext Context) Raiding(Action<Scene>? more = null)
    {
        var scene = new Scene();
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        scene.Build(Scene.Enemy, "refinery", 50, 50);
        scene.World.Map.SetOre(new CellPos(46, 50), new Rts.Simulation.Map.OreCell(Rts.Simulation.Map.OreKind.Ore, 5));
        more?.Invoke(scene);
        var vision = scene.World.VisionOf(Scene.Me);
        vision.Reveal(new CellPos(45, 40), 14);
        scene.Look(0);
        vision.Hide(new CellPos(45, 40), 14);
        return (scene, new SquadManager(Small with { Raids = true }), scene.Context());
    }

    [Fact]
    public void A_raid_leaves_a_guarded_harvester_and_goes_for_the_field_of_the_refinery()
    {
        // M8 step 10a: the harvester seen beside an armed enemy is no target; the field its refinery's harvesters work is.
        var (scene, squads, context) = Raiding(scene =>
        {
            scene.Spawn(Scene.Enemy, UnitType.Harvester, 40, 30);
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 41, 30);
        });

        Turn(scene, squads, context, 1);

        Assert.Equal((null, new CellPos(46, 50)), (context.Plan.Squads.Single().TargetBuilding, context.Plan.Squads.Single().TargetCell));
    }

    [Fact]
    public void A_raid_strikes_the_harvester_within_reach_first_under_fire_too()
    {
        var (scene, squads, context, a, b) = SetOut(Small with { Raids = true, RetreatThresholdPercent = 0 });
        Unit? harvester = null;

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 24, 25);
            harvester = scene.Spawn(Scene.Enemy, UnitType.Harvester, 27, 24);
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 28, 26);
        });

        var attack = Assert.Single(commands.OfType<AttackCommand>());
        Assert.Equal(harvester!.Id, attack.Target);
    }

    [Fact]
    public void A_raid_waits_at_an_empty_field_then_leaves_it()
    {
        // M8 step 10a: no harvester at the field — it waits for one 30 s, then the field is done with.
        var (scene, squads, context) = Raiding();
        Turn(scene, squads, context, 1);
        var squad = context.Plan.Squads.Single();
        Assert.Equal(new CellPos(46, 50), squad.TargetCell);
        var tanks = scene.World.Entities.OfType<Unit>().Where(unit => unit.Owner == Scene.Me).ToList();

        Turn(scene, squads, context, 15, () =>
        {
            foreach (var tank in tanks)
            {
                Scene.Move(tank, 46, 50);
            }
        });
        Assert.Contains(squad.State, new[] { SquadState.Moving, SquadState.Fighting });
        Assert.DoesNotContain(new CellPos(46, 50), squad.Swept);

        Turn(scene, squads, context, 15 + SquadManager.AmbushTicks + 15);
        Assert.Contains(new CellPos(46, 50), squad.Swept);
    }
}
