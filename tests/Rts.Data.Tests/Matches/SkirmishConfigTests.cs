using Rts.Client.Matches;
using Rts.Data.Maps;
using Rts.Data.Tests.Maps;
using Rts.Simulation.Matches;

namespace Rts.Data.Tests.Matches;

/// <summary>
/// The skirmish screen without Godot (docs/implementation/M6_menus_skirmish.md, step 7; M8 step 13): rows by the starts of the map, colours
/// and starts that never repeat, Start only against an enemy, the match setup of the rows and the config kept for the next time.
/// </summary>
public sealed class SkirmishConfigTests
{
    private static MapDefinition FourCorners() =>
        RtsMapReader.Read(File.ReadAllText(Path.Combine(SampleMaps.RepoRoot, "game", "data", "maps", "four-corners.rtsmap")));

    [Fact]
    public void A_map_of_four_starts_has_four_rows_the_person_a_bot_and_two_closed_each_its_own_colour()
    {
        var config = SkirmishConfig.For(4);

        Assert.Equal([SlotKind.Human, SlotKind.Bot, SlotKind.Closed, SlotKind.Closed], config.Slots.Select(slot => slot.Kind));
        Assert.Equal([0, 1, 2, 3], config.Slots.Select(slot => slot.Color));
        Assert.All(config.Slots, slot => Assert.Equal((AiDifficulty.Medium, "random", 0, -1), (slot.Difficulty, slot.Personality, slot.Team, slot.Start)));
        Assert.Equal(10_000, config.Credits);
        Assert.True(config.CanStart);
        Assert.Equal(8, SkirmishConfig.For(12).Slots.Length);
    }

    [Fact]
    public void A_colour_or_a_start_taken_by_another_row_swaps_with_it()
    {
        var config = SkirmishConfig.For(4).WithColor(2, 0);
        Assert.Equal([2, 1, 0, 3], config.Slots.Select(slot => slot.Color));

        config = config.WithStart(0, 3).WithStart(1, 3);
        Assert.Equal([-1, 3, -1, -1], config.Slots.Select(slot => slot.Start));
        config = config.WithStart(0, 1).WithStart(1, 1);
        Assert.Equal([3, 1, -1, -1], config.Slots.Select(slot => slot.Start));
    }

    [Fact]
    public void Start_needs_an_enemy_of_the_person()
    {
        var alone = SkirmishConfig.For(4).With(1, SkirmishConfig.For(4).Slots[1] with { Kind = SlotKind.Closed });
        Assert.False(alone.CanStart);

        var allies = SkirmishConfig.For(4);
        allies = allies.With(0, allies.Slots[0] with { Team = 1 }).With(1, allies.Slots[1] with { Team = 1 });
        Assert.False(allies.CanStart);

        var enemy = allies.With(2, allies.Slots[2] with { Kind = SlotKind.Bot, Team = 2 });
        Assert.True(enemy.CanStart);
        Assert.True(allies.With(2, allies.Slots[2] with { Kind = SlotKind.Bot }).CanStart);
    }

    [Fact]
    public void One_against_three_bots_makes_a_setup_the_simulation_accepts_with_levels_personalities_teams_and_options()
    {
        var map = FourCorners();
        var config = SkirmishConfig.For(4) with { Credits = 20_000, ShortGame = true };
        config = config
            .With(1, config.Slots[1] with { Difficulty = AiDifficulty.Easy, Personality = "balanced", Team = 2 })
            .With(2, config.Slots[2] with { Kind = SlotKind.Bot, Difficulty = AiDifficulty.Hard, Personality = "aggressor", Team = 2 })
            .With(3, config.Slots[3] with { Kind = SlotKind.Bot, Difficulty = AiDifficulty.Brutal })
            .WithStart(0, 2);

        var setup = config.ToSetup(map, "union", seed: 77);

        var grid = map.ToMapGrid();
        Assert.Null(setup.Problem(grid.Width, grid.Height, GameDataFiles.Rules.Database));
        Assert.Equal(new MatchOptions(20_000, ShortGame: true), setup.Options);
        Assert.Equal([1, 2, 3, 4], setup.Players.Select(player => player.Slot));
        Assert.Equal([PlayerKind.Human, PlayerKind.Ai, PlayerKind.Ai, PlayerKind.Ai], setup.Players.Select(player => player.Kind));
        Assert.Equal(
            [AiDifficulty.Easy, AiDifficulty.Hard, AiDifficulty.Brutal],
            setup.Players.Skip(1).Select(player => player.Difficulty));
        Assert.Equal(["balanced", "aggressor", "random"], setup.Players.Skip(1).Select(player => player.Personality));

        // Teams 2 for the two allied bots, one of its own for the person and the last bot.
        Assert.Equal(setup.Players[1].Team, setup.Players[2].Team);
        Assert.Equal(3, setup.Players.Select(player => player.Team).Distinct().Count());
        Assert.Equal(map.Starts[2].Cell, setup.Players[0].Start);
        Assert.Equal(4, setup.Players.Select(player => player.Start).Distinct().Count());
    }

    [Fact]
    public void Random_starts_come_from_the_seed_and_closed_rows_are_left_out()
    {
        var map = FourCorners();
        var config = SkirmishConfig.For(4);

        var once = config.ToSetup(map, "union", seed: 5);
        Assert.Equal(once, config.ToSetup(map, "union", seed: 5));
        Assert.Equal(2, once.Players.Length);
        var starts = Enumerable.Range(1, 40).Select(seed => config.ToSetup(map, "union", (ulong)seed).Players[0].StartIndex).Distinct().Count();
        Assert.Equal(4, starts);
    }

    [Fact]
    public void The_config_comes_back_from_its_text_and_fits_a_smaller_map()
    {
        var config = SkirmishConfig.For(4) with { Credits = 5_000, ShortGame = true };
        config = config
            .With(2, config.Slots[2] with { Kind = SlotKind.Bot, Difficulty = AiDifficulty.Brutal, Personality = "saboteur", Team = 3 })
            .WithColor(2, 6)
            .WithStart(1, 3);

        var loaded = SkirmishConfig.Load(config.Save());
        Assert.NotNull(loaded);
        Assert.Equal(config.Slots.ToArray(), loaded.Slots.ToArray());
        Assert.Equal((5_000, true), (loaded.Credits, loaded.ShortGame));

        // On a map of two starts the third row is gone and start 4 does not exist: the bot's start is random again.
        var two = SkirmishConfig.For(2, loaded);
        Assert.Equal(2, two.Slots.Length);
        Assert.Equal(-1, two.Slots[1].Start);
        Assert.Equal((5_000, true), (two.Credits, two.ShortGame));

        Assert.Null(SkirmishConfig.Load("nothing here"));
    }
}
