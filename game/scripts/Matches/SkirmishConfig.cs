using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Rts.Data.Maps;
using Rts.Simulation;
using Rts.Simulation.Matches;
using Rts.Simulation.Random;

namespace Rts.Client.Matches;

/// <summary>Who plays a row of the skirmish screen: the person (the first row only), a bot, or nobody.</summary>
public enum SlotKind
{
    Human,
    Bot,
    Closed,
}

/// <summary>
/// One row of the skirmish screen (docs/implementation/M6_menus_skirmish.md, 4.3; M8, 4.12). <see cref="Team"/> is 0 for none — every
/// player for itself — or 1–4; <see cref="Start"/> is the index of a start of the map, or <see cref="SkirmishConfig.RandomStart"/>.
/// </summary>
public sealed record SkirmishSlot(SlotKind Kind, AiDifficulty Difficulty, string Personality, int Color, int Team, int Start);

/// <summary>
/// The skirmish screen without Godot (M6 step 7, M8 step 13): one row for every start of the map, the options, whether the match can
/// start and the <see cref="MatchSetup"/> it makes; the last one is kept as text for the next time. Colours and fixed starts never repeat:
/// the row that had the one chosen takes the old one of the row that chose it. Pure, tested in Rts.Data.Tests.
/// </summary>
public sealed record SkirmishConfig(ImmutableArray<SkirmishSlot> Slots, int Credits, bool ShortGame)
{
    /// <summary>The fog of war of the match: off by default — whatever the person has explored stays in sight (owner's decision).</summary>
    public bool FogOfWar { get; init; }

    public const int RandomStart = -1;
    public const int NoTeam = 0;
    public const int MaxTeam = 4;

    /// <summary>The starting credits the screen offers.</summary>
    public static readonly ImmutableArray<int> CreditChoices = [5_000, 10_000, 20_000];

    /// <summary>The levels as the screen lists them, easiest first.</summary>
    public static readonly ImmutableArray<AiDifficulty> Levels = [AiDifficulty.Easy, AiDifficulty.Medium, AiDifficulty.Hard, AiDifficulty.Brutal];

    // A player without a team plays in one of its own, above the teams of the screen: a team is a byte in the replay.
    private const int OwnTeamBase = MaxTeam;

    // A stream of its own for the random starts: they are drawn from the seed of the match, as the replay keeps it.
    private const ulong RandomStartStream = 0x5354_4152_5400;

    /// <summary>
    /// The rows for a map of <paramref name="starts"/> starts: the person, then a «Medium» bot of a random personality, the others closed;
    /// every row its own colour, no team, a random start. With <paramref name="last"/> its rows come back where the map has room for them.
    /// </summary>
    public static SkirmishConfig For(int starts, SkirmishConfig? last = null)
    {
        var count = Math.Clamp(starts, 0, MatchSetup.MaxColors);
        var slots = Enumerable.Range(0, count)
            .Select(row => new SkirmishSlot(
                row == 0 ? SlotKind.Human : row == 1 ? SlotKind.Bot : SlotKind.Closed,
                AiDifficulty.Medium, MatchPlayer.RandomPersonality, Color: row, NoTeam, RandomStart))
            .ToArray();
        var config = new SkirmishConfig([.. slots], SimulationSettings.DefaultStartingCredits, ShortGame: false);
        if (last is null)
        {
            return config;
        }

        config = config with
        {
            Credits = CreditChoices.Contains(last.Credits) ? last.Credits : config.Credits,
            ShortGame = last.ShortGame,
            FogOfWar = last.FogOfWar,
        };
        for (var row = 0; row < Math.Min(count, last.Slots.Length); row++)
        {
            var old = last.Slots[row];
            var kind = row == 0 ? SlotKind.Human : old.Kind == SlotKind.Human ? SlotKind.Bot : old.Kind;
            config = config.With(row, config.Slots[row] with
            {
                Kind = kind,
                Difficulty = old.Difficulty,
                Personality = old.Personality,
                Team = old.Team is >= NoTeam and <= MaxTeam ? old.Team : NoTeam,
            });
            if (old.Color is >= 0 and < MatchSetup.MaxColors)
            {
                config = config.WithColor(row, old.Color);
            }

            if (old.Start >= 0 && old.Start < count)
            {
                config = config.WithStart(row, old.Start);
            }
        }

        return config;
    }

    /// <summary>The rows that play: the person and the bots.</summary>
    public IEnumerable<SkirmishSlot> Playing => Slots.Where(slot => slot.Kind != SlotKind.Closed);

    /// <summary>Row <paramref name="row"/> replaced by <paramref name="slot"/>; its colour and start are taken as they are.</summary>
    public SkirmishConfig With(int row, SkirmishSlot slot) => this with { Slots = Slots.SetItem(row, slot) };

    /// <summary>Row <paramref name="row"/> in <paramref name="color"/>; the row that had it takes the old colour of this one.</summary>
    public SkirmishConfig WithColor(int row, int color)
    {
        var old = Slots[row].Color;
        var slots = Slots.Select((slot, i) => i == row ? slot with { Color = color } : slot.Color == color ? slot with { Color = old } : slot);
        return this with { Slots = [.. slots] };
    }

    /// <summary>Row <paramref name="row"/> on start <paramref name="start"/>; the row that had it takes the old start of this one.</summary>
    public SkirmishConfig WithStart(int row, int start)
    {
        var old = Slots[row].Start;
        var slots = Slots.Select((slot, i) =>
            i == row ? slot with { Start = start } : start != RandomStart && slot.Start == start ? slot with { Start = old } : slot);
        return this with { Slots = [.. slots] };
    }

    /// <summary>
    /// Whether the match can start: two players or more, and an enemy of the person among them — a player of another team, or any other
    /// one when the person has no team.
    /// </summary>
    public bool CanStart
    {
        get
        {
            var playing = Slots.Select((slot, row) => (slot, row)).Where(entry => entry.slot.Kind != SlotKind.Closed).ToList();
            var person = playing.FirstOrDefault(entry => entry.slot.Kind == SlotKind.Human);
            return playing.Count >= 2 && person.slot is not null
                && playing.Any(entry => entry.row != person.row && TeamOf(entry.slot, entry.row) != TeamOf(person.slot, person.row));
        }
    }

    /// <summary>
    /// The match of these rows on <paramref name="map"/>: the players in the order of the rows, slots 1, 2, …; every one plays
    /// <paramref name="faction"/>; the random starts are drawn from <paramref name="seed"/> among those no row has taken.
    /// </summary>
    public MatchSetup ToSetup(MapDefinition map, string faction, ulong seed)
    {
        if (!CanStart)
        {
            throw new InvalidOperationException("The skirmish has no enemy of the person to play against.");
        }

        if (Slots.Length > map.Starts.Length)
        {
            throw new ArgumentException($"The map '{map.Id}' has {map.Starts.Length} starts for {Slots.Length} rows.", nameof(map));
        }

        var playing = Slots.Select((slot, row) => (slot, row)).Where(entry => entry.slot.Kind != SlotKind.Closed).ToList();
        var free = Enumerable.Range(0, map.Starts.Length)
            .Where(start => !playing.Any(entry => entry.slot.Start == start))
            .ToList();
        var random = new DeterministicRandom(seed, RandomStartStream);
        var players = ImmutableArray.CreateBuilder<MatchPlayer>(playing.Count);
        foreach (var (slot, row) in playing)
        {
            var index = slot.Start;
            if (index == RandomStart)
            {
                var pick = random.NextInt(free.Count);
                index = free[pick];
                free.RemoveAt(pick);
            }

            var start = map.Starts[index];
            var id = players.Count + 1;
            players.Add(new MatchPlayer(
                id,
                slot.Kind == SlotKind.Human ? PlayerKind.Human : PlayerKind.Ai,
                slot.Difficulty,
                faction,
                slot.Color,
                TeamOf(slot, row),
                start.Slot,
                start.Cell)
            {
                Personality = slot.Kind == SlotKind.Bot ? slot.Personality : MatchPlayer.DefaultPersonality,
            });
        }

        return new MatchSetup(
            map.Id,
            seed,
            new MatchOptions(Credits, ShortGame, FogOfWar: FogOfWar),
            players.MoveToImmutable(),
            [.. map.Starts.Select(start => start.Cell)]);
    }

    /// <summary>The config as lines of text, for the next time the screen opens.</summary>
    public string Save()
    {
        var text = new StringBuilder();
        text.Append(CultureInfo.InvariantCulture, $"credits={Credits}\n");
        text.Append(CultureInfo.InvariantCulture, $"short={(ShortGame ? 1 : 0)}\n");
        text.Append(CultureInfo.InvariantCulture, $"fog={(FogOfWar ? 1 : 0)}\n");
        foreach (var slot in Slots)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"slot={slot.Kind.ToString().ToLowerInvariant()},{(int)slot.Difficulty},{slot.Personality},{slot.Color},{slot.Team},{slot.Start}\n");
        }

        return text.ToString();
    }

    /// <summary>The config of <see cref="Save"/>, or null when <paramref name="text"/> is not one; a broken line is left out.</summary>
    public static SkirmishConfig? Load(string text)
    {
        var credits = SimulationSettings.DefaultStartingCredits;
        var shortGame = false;
        var fog = false;
        var slots = new List<SkirmishSlot>();
        foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var equals = line.IndexOf('=', StringComparison.Ordinal);
            if (equals < 0)
            {
                continue;
            }

            var value = line[(equals + 1)..];
            switch (line[..equals])
            {
                case "credits" when int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number):
                    credits = number;
                    break;
                case "short":
                    shortGame = value == "1";
                    break;
                case "fog":
                    fog = value == "1";
                    break;
                case "slot" when ParseSlot(value) is { } slot:
                    slots.Add(slot);
                    break;
            }
        }

        return slots.Count == 0 ? null : new SkirmishConfig([.. slots], credits, shortGame) { FogOfWar = fog };
    }

    private static SkirmishSlot? ParseSlot(string value)
    {
        var parts = value.Split(',');
        if (parts.Length != 6
            || !Enum.TryParse<SlotKind>(parts[0], ignoreCase: true, out var kind)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var level)
            || !Levels.Contains((AiDifficulty)level)
            || parts[2].Length == 0
            || !int.TryParse(parts[3], NumberStyles.None, CultureInfo.InvariantCulture, out var color)
            || !int.TryParse(parts[4], NumberStyles.None, CultureInfo.InvariantCulture, out var team)
            || !int.TryParse(parts[5], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var start))
        {
            return null;
        }

        return new SkirmishSlot(kind, (AiDifficulty)level, parts[2], color, team, start);
    }

    private static int TeamOf(SkirmishSlot slot, int row) => slot.Team == NoTeam ? OwnTeamBase + 1 + row : slot.Team;
}
