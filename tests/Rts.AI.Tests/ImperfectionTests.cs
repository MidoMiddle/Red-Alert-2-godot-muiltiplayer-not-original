using Rts.AI.View;
using Rts.Data.Tests;
using Rts.Simulation;
using Rts.Simulation.Commands;
using Rts.Simulation.Geometry;
using Rts.Simulation.Matches;
using Rts.Simulation.Random;
using Rts.Simulation.Tests;
using Rts.Simulation.World;

namespace Rts.AI.Tests;

/// <summary>
/// The imperfection of the bots (docs/implementation/M8_ai_difficulty.md, 4.3, step 3): the bot notices no earlier than its perception
/// lets it, gives no more commands than its level allows, errs among the three best as often as its level says, and thinks on the
/// periods of its level.
/// </summary>
public sealed class ImperfectionTests
{
    /// <summary>A module that gives the commands <see cref="Commands"/> makes on every turn.</summary>
    private sealed class Giver(AiModuleKind kind, Func<AiContext, IEnumerable<Command>> commands) : IAiModule
    {
        public AiModuleKind Kind { get; } = kind;

        public void Update(AiContext context)
        {
            foreach (var command in commands(context))
            {
                context.Issue(command);
            }
        }
    }

    private static readonly PlayerId Me = new(1);

    /// <summary>
    /// A bot of player 1 with one conscript, its modules thinking every tick, on clear land; <paramref name="ticks"/> played. The commands
    /// it gave, as they ran.
    /// </summary>
    private static (List<Command> Commands, AiPlayer Bot) Run(int actionsPerSecond, int ticks, params Func<Unit, IAiModule>[] modules)
    {
        var simulation = new GameSimulation(new SimulationSettings(1) { MapWidthCells = 64, MapHeightCells = 64 }, TestRules.Standard);
        var conscript = simulation.World.SpawnUnit(Me, UnitType.Conscript, new CellPos(10, 10).Center);
        var settings = AiSettings.Medium with
        {
            ActionsPerSecond = actionsPerSecond, TacticsPeriod = 1, ScoutingPeriod = 1, EconomyPeriod = 1, MistakePercent = 0,
        };
        var bot = new AiPlayer(Me, settings, [.. modules.Select(module => module(conscript))]);
        simulation.AttachAi(bot);
        for (var i = 0; i < ticks; i++)
        {
            simulation.Step();
        }

        return (simulation.ExecutedCommands.Where(command => command.Player == Me).ToList(), bot);
    }

    private static MoveCommand MoveTo(AiContext context, Unit unit, int x, int y) =>
        new(Me, context.ExecuteTick, context.NextSequence(), [unit.Id], new CellPos(x, y).Center);

    // --- Perception ----------------------------------------------------------------------------------------------------------------

    [Fact]
    public void An_enemy_in_sight_is_known_only_after_the_perception_delay()
    {
        var scene = new Scene(perceptionDelay: 15);
        scene.Spawn(Scene.Me, UnitType.Conscript, 10, 10);
        var tank = scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 13, 10);

        for (var tick = 0; tick < 15; tick++)
        {
            scene.Look(tick);
            Assert.Empty(scene.View.VisibleEnemies);
            Assert.False(scene.View.Sees(tank));
            Assert.True(scene.View.IsNoticing(tank));
        }

        scene.Look(15);
        Assert.Equal(tank, Assert.Single(scene.View.VisibleEnemies));
        Assert.True(scene.View.Sees(tank));
    }

    [Fact]
    public void An_enemy_back_soon_is_known_at_once_and_one_gone_long_is_news_again()
    {
        var scene = new Scene(perceptionDelay: 15);
        scene.Spawn(Scene.Me, UnitType.Conscript, 10, 10);
        var tank = scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 13, 10);
        for (var tick = 0; tick <= 15; tick++)
        {
            scene.Look(tick);
        }

        // Out of sight for 5 s: known again as it comes back.
        Scene.Move(tank, 50, 50);
        scene.Look(16);
        Scene.Move(tank, 13, 10);
        scene.Look(16 + 75);
        Assert.Single(scene.View.VisibleEnemies);

        // Out of sight for more than 10 s: news again.
        Scene.Move(tank, 50, 50);
        scene.Look(100);
        Scene.Move(tank, 13, 10);
        scene.Look(100 + PerceptionBuffer.ForgetTicks + 1);
        Assert.Empty(scene.View.VisibleEnemies);
    }

    [Fact]
    public void An_attack_on_the_base_is_known_after_the_perception_delay_even_while_it_goes_on()
    {
        var buffer = new PerceptionBuffer(15);

        // The base is hit every tick from tick 10 on: the bot learns of the hit of tick 10 on tick 25, and of later ones as late.
        for (var tick = 0; tick <= 40; tick++)
        {
            buffer.EndTick(tick, tick >= 10 ? tick : null);
            Assert.Equal(tick >= 25 ? tick - 15 : null, buffer.KnownAttack);
        }
    }

    [Theory]
    [InlineData(AiDifficulty.Easy, 45)]
    [InlineData(AiDifficulty.Hard, 6)]
    public void A_tank_at_the_base_is_a_threat_to_the_bot_no_earlier_than_its_perception_lets_it(AiDifficulty difficulty, int delay)
    {
        var match = new BotMatch(difficulty: difficulty);
        while (match.Simulation.CurrentTick < 3 * 900)
        {
            match.Step();
        }

        var bot = match.Setup.PlayerOf(BotMatch.Bot)!;
        match.GivePerson(UnitType.HeavyTank, 1, bot.Start);
        var given = match.Simulation.CurrentTick;
        var plan = match.Brain!.Context!.Plan;
        while (plan.Threat is null && match.Simulation.CurrentTick < given + 300)
        {
            match.Step();
        }

        // The tank comes on tick given + 1 and is in sight from then; the defense thinks every 5 ticks.
        var noticed = match.Simulation.CurrentTick - given;
        Assert.NotNull(plan.Threat);
        Assert.InRange(noticed, delay + 1, delay + 1 + 5);
        Assert.Empty(match.RefusedBotCommands);
    }

    // --- The limit of commands -----------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(8)]
    [InlineData(12)]
    public void No_more_commands_go_out_in_any_second_than_the_level_allows(int actionsPerSecond)
    {
        // Ten new orders on every tick, all different.
        var (commands, _) = Run(actionsPerSecond, 150, unit => new Giver(
            AiModuleKind.Squads, context => Enumerable.Range(0, 10).Select(i => MoveTo(context, unit, 1 + context.Tick % 60, 1 + i))));

        var ticks = commands.Select(command => command.ExecuteTick).ToList();
        for (var start = 0; start < 150; start++)
        {
            Assert.InRange(ticks.Count(tick => tick >= start && tick < start + ActionBudget.WindowTicks), 0, actionsPerSecond);
        }

        // The limit is used, not just kept.
        Assert.InRange(commands.Count, 10 * actionsPerSecond - actionsPerSecond, 10 * actionsPerSecond);
    }

    [Fact]
    public void The_squads_go_first_and_the_scouting_waits()
    {
        var (commands, _) = Run(1, 60,
            unit => new Giver(AiModuleKind.Scouting, context => [MoveTo(context, unit, 2, 1 + context.Tick % 60)]),
            unit => new Giver(AiModuleKind.Squads, context => [MoveTo(context, unit, 1, 1 + context.Tick % 60)]));

        // One action a second, and the squads ask every tick: the scouting never gets its turn and its orders grow stale.
        Assert.Equal(4, commands.Count);
        Assert.All(commands.Cast<MoveCommand>(), move => Assert.Equal(1, move.Target.ToCell().X));
    }

    [Fact]
    public void The_same_order_is_not_left_twice_and_a_stale_one_is_dropped()
    {
        // The same order on every tick: one waits at most.
        var (same, bot) = Run(1, 30, unit => new Giver(AiModuleKind.Squads, context => [MoveTo(context, unit, 5, 5)]));
        Assert.InRange(bot.Context!.Budget.Waiting, 0, 1);
        Assert.Equal(2, same.Count);

        // Five orders once: one goes out at once, one a second later; the rest have waited too long by the next.
        var (once, _) = Run(1, 60, unit => new Giver(
            AiModuleKind.Squads, context => context.Tick == 0 ? Enumerable.Range(0, 5).Select(i => MoveTo(context, unit, 5, 5 + i)) : []));
        Assert.Equal([1, 16], once.Select(command => command.ExecuteTick));
    }

    [Fact]
    public void A_waiting_order_goes_out_with_the_units_still_alive_or_not_at_all()
    {
        var scene = new Scene();
        var first = scene.Spawn(Scene.Me, UnitType.Conscript, 10, 10);
        var second = scene.Spawn(Scene.Me, UnitType.Conscript, 12, 10);
        var tank = scene.Spawn(Scene.Enemy, UnitType.HeavyTank, 14, 10);
        scene.Look();
        var context = scene.Context();
        context.Issue(new MoveCommand(Me, context.ExecuteTick, context.NextSequence(), [first.Id, second.Id], new CellPos(20, 20).Center));
        context.Issue(new AttackCommand(Me, context.ExecuteTick, context.NextSequence(), [first.Id], tank.Id));

        first.MarkDead();
        scene.World.RemoveDead();
        scene.Look();
        var commands = scene.RunCommands();

        Assert.Equal(new[] { second.Id }, Assert.IsType<MoveCommand>(Assert.Single(commands)).Units);
    }

    // --- Mistakes ------------------------------------------------------------------------------------------------------------------

    [Fact]
    public void Without_mistakes_the_best_is_taken_and_a_single_choice_draws_nothing()
    {
        var random = new DeterministicRandom(7, 1);
        var state = random.State;

        Assert.Equal(0, AiChoice.Pick(1, 100, random));
        Assert.Equal(state, random.State);
        Assert.All(Enumerable.Range(0, 100), _ => Assert.Equal(0, AiChoice.Pick(10, 0, random)));
    }

    [Fact]
    public void With_mistakes_one_of_the_three_best_is_taken_the_same_for_the_same_seed()
    {
        List<int> Picks(ulong seed, int percent)
        {
            var random = new DeterministicRandom(seed, AiContext.RandomStream + 2);
            return [.. Enumerable.Range(0, 300).Select(_ => AiChoice.Pick(10, percent, random))];
        }

        var always = Picks(3, 100);
        Assert.All(always, pick => Assert.InRange(pick, 0, AiChoice.Top - 1));
        Assert.Equal(new[] { 0, 1, 2 }, always.Distinct().Order());
        Assert.Equal(always, Picks(3, 100));
        Assert.NotEqual(always, Picks(4, 100));

        // «Easy», 25 %: about one choice in four is a random one of the three, two in three of those not the best.
        var easy = Picks(3, 25).Count(pick => pick != 0);
        Assert.InRange(easy, 25, 75);
    }

    [Fact]
    public void A_bot_that_always_errs_does_not_always_take_the_first_choice()
    {
        var scene = new Scene();
        scene.Look();
        var context = new AiContext(scene.View, scene.Memory, AiSettings.Medium with { MistakePercent = 100 }, seed: 5);
        string[] ranked = ["first", "second", "third", "fourth"];

        var choices = Enumerable.Range(0, 60).Select(_ => context.Choose(ranked)).ToList();

        Assert.DoesNotContain("fourth", choices);
        Assert.Contains("second", choices);
        Assert.Contains("third", choices);
    }

    // --- Periods ------------------------------------------------------------------------------------------------------------------

    [Theory]
    [InlineData("ai_easy", AiModuleKind.Strategist, 90)]
    [InlineData("ai_easy", AiModuleKind.Squads, 30)]
    [InlineData("ai_hard", AiModuleKind.Squads, 5)]
    [InlineData("ai_brutal", AiModuleKind.Squads, 3)]
    public void The_modules_think_on_the_periods_of_their_level_and_the_next_player_sooner(string level, AiModuleKind kind, int period)
    {
        var settings = AiSettings.From(GameDataFiles.Rules.Database.TryGetDifficulty(level, out var def) ? def : throw new ArgumentException(level));
        List<int> Ticks(int player) => [.. Enumerable.Range(0, 900).Where(tick => AiSchedule.IsDue(settings, kind, new PlayerId(player), tick))];

        var first = Ticks(1);
        Assert.Equal(900 / period, first.Count);
        Assert.All(first.Skip(1).Zip(first), pair => Assert.Equal(period, pair.First - pair.Second));
        Assert.Equal((first[0] - 3 + period) % period, Ticks(2)[0] % period);
    }
}
