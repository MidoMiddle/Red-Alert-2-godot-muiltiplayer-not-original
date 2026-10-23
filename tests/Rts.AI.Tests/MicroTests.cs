using Rts.AI.Modules;
using Rts.AI.Profiles;
using Rts.AI.Squads;
using Rts.Data.Tests;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.World;
using Xunit.Abstractions;

namespace Rts.AI.Tests;

/// <summary>
/// The micro of the squads by level (docs/implementation/M8_ai_difficulty.md, 4.4, step 5), on the scenes of <see cref="SquadManagerTests"/>:
/// «Easy» attack-moves, «Medium» focuses its fire, «Hard» also pulls the wounded back, «Brutal» also keeps its range and falls back to a
/// defense; from «Medium» on a squad that loses the trade turns back. And the battle of the step: the same squad of «Hard» loses fewer
/// units than that of «Easy».
/// </summary>
public sealed class MicroTests(ITestOutputHelper output)
{
    private static AiSettings Level(MicroLevel micro, int threshold = 70) =>
        AiSettings.Medium with { MinAttackArmy = 2, Micro = micro, RetreatThresholdPercent = threshold, FirstAttackFrom = 0, WaveInterval = 0, AssumedEnemyPercent = 0 };

    private static AiSettings Data(string level) =>
        AiSettings.From(GameDataFiles.Rules.Database.TryGetDifficulty(level, out var def) ? def : throw new ArgumentException(level));

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

    /// <summary>Two tanks of the bot at the gathering point (14, 14), set out on the first turn toward the far corner.</summary>
    private static (Scene Scene, SquadManager Squads, AiContext Context, Unit A, Unit B) SetOut(AiSettings settings, Action<Scene>? more = null)
    {
        var scene = new Scene();
        var a = scene.Spawn(Scene.Me, UnitType.HeavyTank, 14, 14);
        var b = scene.Spawn(Scene.Me, UnitType.HeavyTank, 15, 14);
        more?.Invoke(scene);
        var context = scene.Context();
        var squads = new SquadManager(settings);
        Turn(scene, squads, context, 0);
        Assert.Equal(SquadState.Moving, context.Plan.Squads.Single().State);
        return (scene, squads, context, a, b);
    }

    [Fact]
    public void Easy_attack_moves_at_the_enemy_and_leaves_its_wounded_in_the_fight()
    {
        var (scene, squads, context, a, b) = SetOut(Level(MicroLevel.None));

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 24, 25);
            scene.Spawn(Scene.Enemy, UnitType.Conscript, 28, 24);
            b.Hp = b.MaxHp / 4;
        });

        Assert.Equal(SquadState.Fighting, context.Plan.Squads.Single().State);
        Assert.Empty(commands.OfType<AttackCommand>());
        var move = Assert.Single(commands.OfType<AttackMoveCommand>());
        Assert.Equal(new[] { a.Id, b.Id }, move.Units);
        Assert.Equal(new CellPos(28, 24), move.Target.ToCell());
    }

    [Fact]
    public void Medium_fires_at_the_enemy_it_kills_soonest_and_keeps_its_wounded_in_the_fight()
    {
        var (scene, squads, context, a, b) = SetOut(Level(MicroLevel.Focus, threshold: 0));
        Unit? hurt = null;

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 24, 25);
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 28, 24);
            hurt = scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 30, 25);
            hurt.Hp = hurt.MaxHp / 5;
            b.Hp = b.MaxHp / 4;
        });

        // The wounded enemy tank, though the other is nearer; both tanks of the bot fire, the wounded one too.
        var attack = Assert.Single(commands.OfType<AttackCommand>());
        Assert.Equal(hurt!.Id, attack.Target);
        Assert.Equal(new[] { a.Id, b.Id }, attack.Units);
        Assert.Empty(commands.OfType<MoveCommand>());
    }

    [Fact]
    public void A_unit_out_of_the_reach_of_the_focus_attack_moves_to_it_and_one_in_reach_fires()
    {
        // M8 step 10: an order to attack walks a unit to its target past the enemies on the way without firing; out of reach — the range of
        // its weapon and a cell — it attack-moves there instead. A cannon reaches 5.75 cells.
        var (scene, squads, context, a, b) = SetOut(Level(MicroLevel.Focus, threshold: 0));
        Unit? tank = null;

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 19, 24);
            tank = scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 29, 24);
        });

        var attack = Assert.Single(commands.OfType<AttackCommand>());
        Assert.Equal(tank!.Id, attack.Target);
        Assert.Equal(new[] { a.Id }, attack.Units);
        var move = Assert.Single(commands.OfType<AttackMoveCommand>());
        Assert.Equal(new CellPos(29, 24), move.Target.ToCell());
        Assert.Equal(new[] { b.Id }, move.Units);
    }

    [Fact]
    public void Hard_pulls_its_wounded_back_and_the_rest_focus()
    {
        var (scene, squads, context, a, b) = SetOut(Level(MicroLevel.RetreatWounded, threshold: 0));

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 24, 25);
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 28, 24);
            b.Hp = b.MaxHp / 4;
        });

        Assert.Equal(new[] { a.Id }, Assert.Single(commands.OfType<AttackCommand>()).Units);
        Assert.Equal(new[] { b.Id }, Assert.Single(commands.OfType<MoveCommand>()).Units);
    }

    [Theory]
    [InlineData(MicroLevel.KeepRange, UnitType.Conscript, true)]
    [InlineData(MicroLevel.RetreatWounded, UnitType.Conscript, false)]
    [InlineData(MicroLevel.KeepRange, UnitType.HeavyTank, false)]
    public void Brutal_keeps_a_tank_out_of_the_reach_of_a_conscript(MicroLevel micro, UnitType enemy, bool stepsBack)
    {
        // A tank shoots 5.75 cells, a conscript 4, a tank as far as a tank.
        var (scene, squads, context, a, b) = SetOut(Level(micro, threshold: 0));

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 20, 20);
            scene.Spawn(Scene.Enemy, enemy, 27, 24);
        });

        var back = commands.OfType<MoveCommand>().SingleOrDefault(move => move.Units.Contains(a.Id));
        Assert.Equal(stepsBack, back is not null);
        if (back is not null)
        {
            Assert.True(back.Target.ToCell().X < 24, $"{back.Target.ToCell()}");
            Assert.DoesNotContain(commands.OfType<AttackCommand>(), attack => attack.Units.Contains(a.Id));
        }
    }

    [Theory]
    [InlineData(MicroLevel.None, 70, false)]
    [InlineData(MicroLevel.Focus, 70, true)]
    [InlineData(MicroLevel.Focus, 60, false)]
    public void A_squad_that_loses_the_trade_turns_back_from_medium_on_by_the_threshold_of_its_personality(MicroLevel micro, int threshold, bool back)
    {
        // Two tanks against two, one of the bot's at 30 % of its hit points: 1.3 tanks' worth against 2 — 65 %.
        var (scene, squads, context, a, b) = SetOut(Level(micro, threshold) with { RetreatAtStrengthPercent = 0 });

        Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 24, 25);
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 28, 24);
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 28, 26);
            b.Hp = b.MaxHp * 3 / 10;
        });

        var squad = context.Plan.Squads.Single();
        Assert.Equal(back ? SquadState.Retreating : SquadState.Fighting, squad.State);
        Assert.Equal(back, squad.Beaten);
    }

    [Fact]
    public void Brutal_falls_back_to_a_defense_of_its_own()
    {
        var (scene, squads, context, a, b) = SetOut(Level(MicroLevel.KeepRange), more => more.Build(Scene.Me, "tesla_coil", 20, 10));

        var commands = Turn(scene, squads, context, 15, () =>
        {
            Scene.Move(a, 24, 24);
            Scene.Move(b, 24, 25);
            for (var i = 0; i < 4; i++)
            {
                scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 28, 23 + i);
            }
        });

        var squad = context.Plan.Squads.Single();
        Assert.Equal(SquadState.Retreating, squad.State);
        Assert.Equal(new CellPos(20, 10), squad.RetreatPoint);
        Assert.Equal(new CellPos(20, 10), Assert.Single(commands.OfType<MoveCommand>()).Target.ToCell());
    }

    /// <summary>
    /// The battle of the step, by the rules of the game — their costs and weapons: six tanks of the bot from its gathering point at three
    /// tanks and six conscripts standing in the middle of the map, the squads thinking on the period of the level, two minutes. The tanks
    /// of the bot lost and the enemies left.
    /// </summary>
    private (int Lost, int Left) Battle(string level, int tanks = 3, int conscripts = 6, int mineCount = 6)
    {
        var settings = Data(level) with { MinAttackArmy = mineCount, AttackThresholdPercent = 0, FirstAttackFrom = 0, WaveInterval = 0 };
        var scene = new Scene(rules: GameDataFiles.Rules);
        var mine = Enumerable.Range(0, mineCount).Select(i => scene.Spawn(Scene.Me, UnitType.HeavyTank, 13 + i % 3, 13 + i / 3)).ToList();
        for (var i = 0; i < tanks; i++)
        {
            scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 34, 31 + 2 * i);
        }

        for (var i = 0; i < conscripts; i++)
        {
            scene.Spawn(Scene.Enemy, UnitType.Conscript, 32, 31 + i);
        }

        var context = scene.Context();
        var squads = new SquadManager(settings);
        for (var tick = 0; tick < 2 * 900; tick++)
        {
            if (AiSchedule.IsDue(settings, AiModuleKind.Squads, Scene.Me, tick))
            {
                scene.Look(tick);
                context.Begin(new Rts.Simulation.Ai.AiTurn(scene.Simulation, Scene.Me) { Tick = tick });
                squads.Update(context);
                context.End();
            }

            scene.Simulation.Step();
            scene.World.RemoveDead();
        }

        var lost = mine.Count(unit => !unit.IsAlive);
        var enemies = scene.World.Entities.OfType<Unit>().Count(unit => unit.IsAlive && unit.Owner == Scene.Enemy);
        output.WriteLine($"{level}: lost {lost} of {mineCount} tanks, {enemies} enemies left");
        return (lost, enemies);
    }

    [Trait("Category", "Slow")]
    [Fact]
    public void In_the_same_battle_the_squad_of_hard_loses_fewer_units_than_that_of_easy()
    {
        // Step 5: «Easy» 2 lost, «Medium» 1, «Hard» and «Brutal» none; every one of them wins it.
        var easy = Battle("ai_easy");
        var hard = Battle("ai_hard");

        Assert.True(hard.Lost < easy.Lost, $"«Hard» lost {hard.Lost}, «Easy» {easy.Lost}");
        Assert.Equal((0, 0), (hard.Left, easy.Left));
    }
}
