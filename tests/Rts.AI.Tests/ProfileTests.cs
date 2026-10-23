using Rts.AI.Profiles;
using Rts.Data.Tests;
using Rts.Simulation.Geometry;
using Rts.Simulation.Math;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.Tests;

namespace Rts.AI.Tests;

/// <summary>
/// The levels and the personalities of the bots as the bot reads them (docs/implementation/M8_ai_difficulty.md, 4.2, 4.7, step 1): the
/// table of the levels from the data, the words of the data in the enums of the code, and the build order and army a bot gets.
/// </summary>
public sealed class ProfileTests
{
    private static DifficultyDef Level(string id) =>
        GameDataFiles.Rules.Database.TryGetDifficulty(id, out var level) ? level : throw new ArgumentException($"No level '{id}'.");

    private static PersonalityDef Personality(string id) =>
        GameDataFiles.Rules.Database.TryGetPersonality(id, out var personality) ? personality : throw new ArgumentException($"No personality '{id}'.");

    /// <summary>The words of an enum as the data writes them: <c>RetreatWounded</c> — <c>retreat_wounded</c>.</summary>
    private static string[] Words<T>()
        where T : struct, Enum =>
        Enum.GetNames<T>().Select(name => string.Concat(name.Select((c, i) => char.IsUpper(c) && i > 0 ? "_" + char.ToLowerInvariant(c) : char.ToLowerInvariant(c).ToString()))).ToArray();

    [Fact]
    public void The_words_of_every_choice_are_those_of_the_enums_in_the_same_order()
    {
        // The data keeps a choice as the place of its word: the enums must list the words of DifficultyDef.Choices in its order.
        Assert.Equal(DifficultyDef.Choices["micro"], Words<MicroLevel>());
        Assert.Equal(DifficultyDef.Choices["scouting"], Words<ScoutingLevel>());
        Assert.Equal(DifficultyDef.Choices["expansion"], Words<ExpansionLevel>());
        Assert.Equal(DifficultyDef.Choices["build_order_quality"], Words<BuildOrderQuality>());
        Assert.Equal(DifficultyDef.Choices["rebuild_destroyed"], Words<RebuildLevel>());
        Assert.Equal(new[] { "no", "yes" }, DifficultyDef.Choices["defend_harvesters"]);
        Assert.Equal(PersonalityDef.Targets, Words<AttackTarget>());
    }

    [Theory]
    // id, strategy, tactics, perception (ticks), actions/s, mistakes %, micro, scouting, counters %, threshold %, first army, first attack
    // (ticks), squads, harvesters, expansion, quality, rebuilding — the table of 4.2; «Medium» keeps its first wave of M5, 6.
    [InlineData("ai_easy", 90, 30, 45, 1, 25, MicroLevel.None, ScoutingLevel.None, 0, 60, 4, 9000, 1, false, ExpansionLevel.None, BuildOrderQuality.Simple, RebuildLevel.Partial)]
    [InlineData("ai_medium", 45, 15, 15, 3, 10, MicroLevel.Focus, ScoutingLevel.Periodic, 30, 100, 6, 5400, 2, true, ExpansionLevel.Late, BuildOrderQuality.Standard, RebuildLevel.Yes)]
    [InlineData("ai_hard", 15, 5, 6, 8, 2, MicroLevel.RetreatWounded, ScoutingLevel.Constant, 70, 130, 12, 0, 4, true, ExpansionLevel.Early, BuildOrderQuality.Optimized, RebuildLevel.Priority)]
    [InlineData("ai_brutal", 15, 3, 3, 12, 0, MicroLevel.KeepRange, ScoutingLevel.ConstantExpansions, 90, 120, 12, 0, 4, true, ExpansionLevel.Early, BuildOrderQuality.Optimized, RebuildLevel.Priority)]
    public void A_level_of_the_data_reads_as_the_table(
        string id, int strategy, int tactics, int perception, int actions, int mistakes, MicroLevel micro, ScoutingLevel scouting, int counters,
        int threshold, int firstArmy, int firstAttack, int squads, bool harvesters, ExpansionLevel expansion, BuildOrderQuality quality, RebuildLevel rebuild)
    {
        var settings = AiSettings.From(Level(id));

        Assert.Equal(
            (strategy, tactics, perception, actions, mistakes, micro, scouting, counters, threshold, firstArmy, firstAttack, squads, harvesters, expansion, quality, rebuild),
            (settings.StrategyPeriod, settings.TacticsPeriod, settings.PerceptionDelay, settings.ActionsPerSecond, settings.MistakePercent, settings.Micro,
                settings.Scouting, settings.CounterPickPercent, settings.AttackThresholdPercent, settings.MinAttackArmy, settings.FirstAttackDelay,
                settings.ParallelSquads, settings.DefendHarvesters, settings.Expansion, settings.Quality, settings.Rebuild));
    }

    [Fact]
    public void The_levels_share_what_the_steps_of_m8_have_not_yet_made_differ()
    {
        // The economy, the base, scouting, the squads and the defense of M5: alike on every level until the Arena tunes them (step 10).
        var medium = AiSettings.From(Level("ai_medium"));
        foreach (var id in new[] { "ai_easy", "ai_hard", "ai_brutal" })
        {
            var level = AiSettings.From(Level(id));
            Assert.Equal(
                medium with { StrategyPeriod = 0, TacticsPeriod = 0, PerceptionDelay = 0, MinAttackArmy = 0, AttackThresholdPercent = 0 },
                level with
                {
                    StrategyPeriod = 0, TacticsPeriod = 0, PerceptionDelay = 0, MinAttackArmy = 0, AttackThresholdPercent = 0,
                    ActionsPerSecond = medium.ActionsPerSecond, MistakePercent = medium.MistakePercent, Micro = medium.Micro, Scouting = medium.Scouting,
                    CounterPickPercent = medium.CounterPickPercent, FirstAttackDelay = medium.FirstAttackDelay, ParallelSquads = medium.ParallelSquads,
                    DefendHarvesters = medium.DefendHarvesters, Expansion = medium.Expansion, Quality = medium.Quality, Rebuild = medium.Rebuild,
                });
        }
    }

    [Fact]
    public void Medium_with_balanced_on_the_union_builds_the_build_order_and_the_army_of_m5()
    {
        var profile = AiProfile.Of(Level("ai_medium"), Personality("balanced"), "union");

        Assert.Equal(("standard", "standard_army"), (profile.Settings.BuildOrder, profile.Settings.Army));
        Assert.Equal(AiSettings.Medium, profile.Settings);
        Assert.Equal("balanced", profile.Personality.Id);
    }

    [Fact]
    public void A_level_takes_the_build_order_of_its_quality_from_the_personality()
    {
        var personality = Personality("balanced") with { BuildOrders = [new FactionBuildOrders("union", "rush_simple", "rush", "rush_fast")] };

        Assert.Equal("rush_simple", AiProfile.Of(Level("ai_easy"), personality, "union").Settings.BuildOrder);
        Assert.Equal("rush", AiProfile.Of(Level("ai_medium"), personality, "union").Settings.BuildOrder);
        Assert.Equal("rush_fast", AiProfile.Of(Level("ai_hard"), personality, "union").Settings.BuildOrder);
        Assert.Throws<ArgumentException>(() => AiProfile.Of(Level("ai_hard"), personality, "soviet"));
    }

    [Theory]
    [InlineData("balanced", 70)]
    [InlineData("aggressor", 60)]
    [InlineData("builder", 80)]
    public void A_squad_turns_back_by_the_threshold_of_its_personality(string personality, int percent)
    {
        // M8 step 5: the trade a squad gives up on is the personality's, not the level's.
        Assert.Equal(percent, AiProfile.Of(Level("ai_hard"), Personality(personality), "union").Settings.RetreatThresholdPercent);
    }

    [Fact]
    public void A_personality_reads_as_the_bot_needs_it()
    {
        var aggressor = PersonalityProfile.From(Personality("aggressor"));

        // The army far above the defense (4.7): 1.5 against 0.4, the economy 0.8 between.
        var weights = aggressor.Weights;
        Assert.Equal(Fixed.FromRatio(15, 10), weights.Army);
        Assert.True(weights.Army > weights.Economy && weights.Economy > weights.Harassment && weights.Harassment > weights.Tech && weights.Tech > weights.Defense);
        Assert.Equal((2700, 4500, 1800), (aggressor.FirstAttackMin, aggressor.FirstAttackMax, aggressor.WaveInterval));
        // It stays in a fight longer than «Balanced»: 60% against 70%.
        Assert.True(aggressor.RetreatThreshold < Personality("balanced").RetreatThreshold);
        Assert.Equal(
            new[] { AttackTarget.Production, AttackTarget.Army, AttackTarget.Power, AttackTarget.Harvesters, AttackTarget.Defense, AttackTarget.Core },
            aggressor.TargetPriority);
    }

    [Theory]
    [InlineData(AiDifficulty.Easy, 1)]
    [InlineData(AiDifficulty.Medium, 3)]
    [InlineData(AiDifficulty.Hard, 8)]
    [InlineData(AiDifficulty.Brutal, 12)]
    public void A_bot_of_every_level_is_made_from_the_data(AiDifficulty difficulty, int actions)
    {
        var bot = new MatchPlayer(2, PlayerKind.Ai, difficulty, "union", 1, 2, 1, new CellPos(50, 50));
        var setup = new MatchSetup("clear", 1, new MatchOptions(), [new MatchPlayer(1, PlayerKind.Human, AiDifficulty.Medium, "union", 0, 1, 0, new CellPos(10, 10)), bot],
            [new CellPos(10, 10), new CellPos(50, 50)]);

        var player = (AiPlayer)MatchBots.Create(setup, bot, GameDataFiles.Rules);

        Assert.Equal(actions, player.Settings.ActionsPerSecond);
        Assert.Equal(("standard", "standard_army"), (player.Settings.BuildOrder, player.Settings.Army));
    }

    [Fact]
    public void A_level_the_data_does_not_have_is_refused_and_medium_falls_back_to_the_code()
    {
        // The rules of the simulation tests have «Medium» only.
        Assert.Throws<ArgumentException>(() => MatchBots.SettingsOf(AiDifficulty.Hard, "union", TestRules.Standard));
        Assert.Equal(AiSettings.Medium, MatchBots.SettingsOf(AiDifficulty.Medium, "union", TestRules.Standard));
    }
}
