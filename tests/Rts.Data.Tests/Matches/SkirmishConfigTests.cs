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

        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);
        Assert.Null(setup.Problem(grid.Width, grid.Height, GameDataFiles.Rules.Database));
        // Crates on by default, as in RA2 (M11 a6).
        Assert.Equal(new MatchOptions(20_000, ShortGame: true, FogOfWar: false, Crates: true), setup.Options);
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
    public void Every_row_plays_its_faction_the_default_one_when_unset_and_a_random_one_from_the_seed()
    {
        var map = FourCorners();
        var config = SkirmishConfig.For(4);
        config = config
            .With(0, config.Slots[0] with { Faction = "alliance" })
            .With(2, config.Slots[2] with { Kind = SlotKind.Bot, Faction = SkirmishConfig.RandomFaction });

        var setup = config.ToSetup(map, "union", seed: 9, ["union", "alliance"]);
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);

        Assert.Null(setup.Problem(grid.Width, grid.Height, GameDataFiles.Rules.Database));
        Assert.Equal(["alliance", "union"], setup.Players.Take(2).Select(player => player.Faction));
        Assert.Equal(setup.Players[2].Faction, config.ToSetup(map, "union", seed: 9, ["union", "alliance"]).Players[2].Faction);
        var drawn = Enumerable.Range(1, 30).Select(seed => config.ToSetup(map, "union", (ulong)seed, ["union", "alliance"]).Players[2].Faction).Distinct().Order();
        Assert.Equal(["alliance", "union"], drawn);

        // Kept with the config; a config of before, without the faction, plays the default one.
        Assert.Equal("alliance", SkirmishConfig.Load(config.Save())!.Slots[0].Faction);
        Assert.Equal("", SkirmishConfig.Load("slot=human,1,random,0,0,-1\nslot=bot,1,random,1,0,-1\n")!.Slots[0].Faction);
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

        // The fog of war: off by default and in a config of before, kept when on, carried into the match.
        Assert.False(config.FogOfWar);
        Assert.False(SkirmishConfig.Load("credits=10000\nslot=human,1,random,0,0,-1\nslot=bot,1,random,1,0,-1\n")!.FogOfWar);
        var fog = config with { FogOfWar = true };
        Assert.True(SkirmishConfig.Load(fog.Save())!.FogOfWar);
        Assert.True(SkirmishConfig.For(2, fog).FogOfWar);
        Assert.True(fog.ToSetup(FourCorners(), "union", 5).Options.FogOfWar);
        Assert.False(config.ToSetup(FourCorners(), "union", 5).Options.FogOfWar);
    }

    [Fact]
    public void Every_player_plays_its_country_or_one_drawn_from_its_faction()
    {
        var map = FourCorners();
        var countries = GameDataFiles.Rules.Database.Countries;
        var config = SkirmishConfig.For(4);
        config = config.With(0, config.Slots[0] with { Faction = "alliance", Country = "france" });
        var setup = config.ToSetup(map, "union", seed: 3, ["union", "alliance"], countries);
        var grid = map.ToMapGrid(GameDataFiles.Rules.MapTypes);

        Assert.Null(setup.Problem(grid.Width, grid.Height, GameDataFiles.Rules.Database));
        Assert.Equal("france", setup.Players[0].Country);
        // A bot too (RA2): its country, or one of its faction drawn.
        Assert.All(setup.Players, player => Assert.Contains(countries, country => country.Id == player.Country && country.Faction == player.Faction));
        var iraq = config.With(1, config.Slots[1] with { Faction = "union", Country = "iraq" });
        Assert.Equal("iraq", iraq.ToSetup(map, "union", seed: 3, null, countries).Players[1].Country);

        // The quick skirmish: a country of the faction for everyone; without the countries, as the bots' tests pin it, none.
        Assert.All(MatchSetups.TwoOnTwo(map, "union", 3, countries).Players, player => Assert.Contains(player.Country, new[] { "cuba", "iraq", "libya", "russia" }));
        Assert.All(MatchSetups.OneOnOne(map, "union", 3).Players, player => Assert.Equal("", player.Country));

        // Random, or a country of another faction: one of the faction played, from the seed.
        var random = config.With(0, config.Slots[0] with { Faction = "union", Country = "france" });
        var drawn = Enumerable.Range(1, 40).Select(seed => random.ToSetup(map, "union", (ulong)seed, null, countries).Players[0].Country).Distinct().Order();
        Assert.Equal(["cuba", "iraq", "libya", "russia"], drawn);

        // Kept with the config; a config of before plays a random one.
        Assert.Equal("france", SkirmishConfig.Load(config.Save())!.Slots[0].Country);
        Assert.Equal(SkirmishConfig.RandomCountry, SkirmishConfig.Load("slot=human,1,random,0,0,-1,alliance\nslot=bot,1,random,1,0,-1\n")!.Slots[0].Country);
    }

    [Fact]
    public void A_side_of_ra2_is_a_country_a_random_country_of_a_faction_or_random()
    {
        var countries = GameDataFiles.Rules.Database.Countries;
        var slot = SkirmishConfig.For(2).Slots[0];

        var america = SkirmishConfig.WithSide(slot, "usa", countries);
        Assert.Equal(("alliance", "usa"), (america.Faction, america.Country));
        Assert.Equal("usa", SkirmishConfig.SideOf(america, "union"));

        var soviet = SkirmishConfig.WithSide(slot, SkirmishConfig.RandomSidePrefix + "union", countries);
        Assert.Equal(("union", SkirmishConfig.RandomCountry), (soviet.Faction, soviet.Country));
        Assert.Equal("random:union", SkirmishConfig.SideOf(soviet, "union"));

        var random = SkirmishConfig.WithSide(slot, SkirmishConfig.RandomSide, countries);
        Assert.Equal(SkirmishConfig.RandomFaction, random.Faction);
        Assert.Equal(SkirmishConfig.RandomSide, SkirmishConfig.SideOf(random, "union"));

        // A row of before, with its faction and a random country, reads as the random country of the faction.
        Assert.Equal("random:union", SkirmishConfig.SideOf(slot, "union"));
    }

    [Fact]
    public void The_options_of_ra2_go_into_the_match_and_come_back_from_the_text()
    {
        var config = SkirmishConfig.For(4) with
        {
            McvRepacks = false,
            Superweapons = false,
            DestroyableBridges = false,
            GameSpeed = 6,
            StartingUnits = 10,
            Crates = false,
            MapId = "four-corners",
        };

        var options = config.ToSetup(FourCorners(), "union", 5).Options;
        Assert.Equal((false, false, false, 6, 10), (options.McvRepacks, options.Superweapons, options.DestroyableBridges, options.GameSpeed, options.StartingUnits));
        var loaded = SkirmishConfig.Load(config.Save())!;
        Assert.Equal((false, false, false, 6, 10, "four-corners"),
            (loaded.McvRepacks, loaded.Superweapons, loaded.DestroyableBridges, loaded.GameSpeed, loaded.StartingUnits, loaded.MapId));
        Assert.False(options.Crates);
        Assert.False(loaded.Crates);

        // A config of before has them on, at speed 4, with three soldiers.
        var old = SkirmishConfig.Load("slot=human,1,random,0,0,-1\nslot=bot,1,random,1,0,-1\n")!;
        Assert.Equal((true, true, true, 4, 3), (old.McvRepacks, old.Superweapons, old.DestroyableBridges, old.GameSpeed, old.StartingUnits));
        Assert.True(old.Crates);
    }
}
