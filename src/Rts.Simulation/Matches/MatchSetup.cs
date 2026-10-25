using System;
using System.Collections.Immutable;
using System.Linq;
using Rts.Simulation.Geometry;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.Simulation.Matches;

/// <summary>Who plays a slot of a match.</summary>
public enum PlayerKind : byte
{
    Human = 0,
    Ai = 1,
}

/// <summary>
/// The level of a bot (docs/implementation/M8_ai_difficulty.md, 4.2). «Medium» keeps the byte it had in M5, so the replays and messages
/// of M5 read the same; the levels of M8 come after it.
/// </summary>
public enum AiDifficulty : byte
{
    Medium = 1,
    Easy = 2,
    Hard = 3,
    Brutal = 4,
}

/// <summary>The options of a match (docs/implementation/M5_first_bot.md, 4.11).</summary>
/// <param name="StartingCredits">The credits of every player at the start.</param>
/// <param name="ShortGame">A player loses with the last building, not the last unit (step 10).</param>
/// <param name="GameSpeed">1–6, as the skirmish screen of M6 offers; the client plays it, the simulation keeps it.</param>
/// <param name="FogOfWar">
/// The fog of war: on, only what is in sight now is seen; off, whatever the team has explored stays in sight (<see cref="Visibility.Vision"/>).
/// On unless said otherwise — the matches of the Arena and of the tests; the skirmish screen offers it off by default.
/// </param>
/// <param name="McvRepacks">The construction yard packs up into its MCV with D (RA2 «MCV Repacks»).</param>
/// <param name="Superweapons">The superweapons can be built (RA2 «Superweapons»); off, the Bastion Field Projector, the missile silo, the
/// Phase Gate and the Storm Generator are not in the tree. The paradrops stay.</param>
/// <param name="DestroyableBridges">The bridges fall to shots (RA2 «Destroyable Bridges»); off, a shot at a bridge stops at it.</param>
/// <param name="StartingUnits">
/// The units every player gets beside its MCV (RA2 «Unit Count», M6 step 8): 0–<see cref="MaxStartingUnits"/> of its starting infantry.
/// </param>
public sealed record MatchOptions(
    int StartingCredits = SimulationSettings.DefaultStartingCredits,
    bool ShortGame = false,
    int GameSpeed = 4,
    bool FogOfWar = true,
    bool McvRepacks = true,
    bool Superweapons = true,
    bool DestroyableBridges = true,
    int StartingUnits = MatchOptions.DefaultStartingUnits)
{
    /// <summary>Three soldiers, as every match had before the option (M5, decision 1).</summary>
    public const int DefaultStartingUnits = 3;

    /// <summary>The most of the slider, as in RA2.</summary>
    public const int MaxStartingUnits = 10;
}

/// <summary>
/// One player of a match (docs/implementation/M5_first_bot.md, 4.11). <see cref="Slot"/> is its <see cref="PlayerId"/>. The start is
/// chosen before the match — a random one by whoever sets the match up — and kept as a cell: the map grid knows no starts.
/// </summary>
public sealed record MatchPlayer(
    int Slot, PlayerKind Kind, AiDifficulty Difficulty, string Faction, int Color, int Team, int StartIndex, CellPos Start)
{
    /// <summary>The personality every bot plays unless the skirmish screen chose another (M8 step 13).</summary>
    public const string DefaultPersonality = "balanced";

    /// <summary>A personality the bot draws at the start of the match from the seed (M8 step 13; <c>MatchBots.PersonalityOf</c>).</summary>
    public const string RandomPersonality = "random";

    public PlayerId Id => new(Slot);

    /// <summary>
    /// The personality of a bot (M8 step 13): the id of one of the data, or <see cref="RandomPersonality"/>; the replay keeps it as it is,
    /// a random one is drawn again the same way. A person's is not used.
    /// </summary>
    public string Personality { get; init; } = DefaultPersonality;

    /// <summary>
    /// The country the player plays (loose ends, stage 5): one of its faction in the data, or empty for the whole faction — every bot, as
    /// its build orders want.
    /// </summary>
    public string Country { get; init; } = "";
}

/// <summary>
/// The description of a match (docs/implementation/M5_first_bot.md, 4.11): the map it is played on (an id only for show — the map itself
/// is in the settings and the replay), the seed, the options, the players and every start of the map — taken or not, so that a bot
/// knows where an enemy may be without learning where it is (4.5, M5 step 3). It sets the starting state up — factions, an MCV and
/// three conscripts at every start (<see cref="MatchStart"/>) — and goes into the replay with the settings
/// (<see cref="SimulationSettings.ForMatch"/>). The slots that are not in it stay empty.
/// </summary>
public sealed record MatchSetup(string MapId, ulong Seed, MatchOptions Options, ImmutableArray<MatchPlayer> Players, ImmutableArray<CellPos> MapStarts)
{
    public const int MaxColors = 8;
    public const int MinGameSpeed = 1;
    public const int MaxGameSpeed = 6;

    /// <summary>The player of <paramref name="id"/>, or null when the slot is empty.</summary>
    public MatchPlayer? PlayerOf(PlayerId id)
    {
        foreach (var player in Players)
        {
            if (player.Slot == id.Value)
            {
                return player;
            }
        }

        return null;
    }

    /// <summary>
    /// What is wrong with the match on a map of <paramref name="width"/> × <paramref name="height"/> by <paramref name="rules"/>, or null
    /// when it can be played: 2–8 players in different slots 1–8 — people, bots or both; bots alone are a match of the Arena (M8 step 4) —
    /// different starts of the map and different colours, known factions, credits not negative, a speed of 1–6, 0–10 starting units.
    /// </summary>
    public string? Problem(int width, int height, RulesDatabase rules)
    {
        if (Players.IsDefault || Players.Length < 2 || Players.Length > PlayerId.MaxPlayers)
        {
            return $"a match has 2–{PlayerId.MaxPlayers} players";
        }

        if (Options.StartingCredits < 0)
        {
            return "the starting credits are negative";
        }

        if (Options.GameSpeed < MinGameSpeed || Options.GameSpeed > MaxGameSpeed)
        {
            return $"the game speed {Options.GameSpeed} is not {MinGameSpeed}–{MaxGameSpeed}";
        }

        if (Options.StartingUnits < 0 || Options.StartingUnits > MatchOptions.MaxStartingUnits)
        {
            return $"the starting units {Options.StartingUnits} are not 0–{MatchOptions.MaxStartingUnits}";
        }

        if (MapStarts.IsDefault || MapStarts.Length < Players.Length)
        {
            return $"the map has {(MapStarts.IsDefault ? 0 : MapStarts.Length)} starts for {Players.Length} players";
        }

        foreach (var start in MapStarts)
        {
            if (start.X < 0 || start.Y < 0 || start.X >= width || start.Y >= height)
            {
                return $"the start at {start.X}, {start.Y} is off the map";
            }
        }

        for (var i = 0; i < Players.Length; i++)
        {
            var player = Players[i];
            if (player.Slot < 1 || player.Slot > PlayerId.MaxPlayers)
            {
                return $"slot {player.Slot} is not 1–{PlayerId.MaxPlayers}";
            }

            if (player.Color < 0 || player.Color >= MaxColors)
            {
                return $"{player.Id} has colour {player.Color}, not 0–{MaxColors - 1}";
            }

            if (!rules.TryGetFaction(player.Faction, out _))
            {
                return $"{player.Id} plays the faction '{player.Faction}', which is not in the data";
            }

            if (player.Country.Length > 0 && (!rules.TryGetCountry(player.Country, out var country) || country.Faction != player.Faction))
            {
                return $"{player.Id} plays the country '{player.Country}', which is not one of the faction '{player.Faction}'";
            }

            // M8 step 13: a personality of the data, or random; data without personalities plays every bot «Balanced» of the code.
            if (player.Personality != MatchPlayer.RandomPersonality && !rules.TryGetPersonality(player.Personality, out _)
                && !(rules.Personalities.IsDefaultOrEmpty && player.Personality == MatchPlayer.DefaultPersonality))
            {
                return $"{player.Id} plays the personality '{player.Personality}', which is not in the data: "
                    + string.Join(", ", rules.Personalities.Select(personality => personality.Id)) + " or random";
            }

            if (player.Start.X < 0 || player.Start.Y < 0 || player.Start.X >= width || player.Start.Y >= height)
            {
                return $"the start of {player.Id} at {player.Start.X}, {player.Start.Y} is off the map";
            }

            if (!MapStarts.Contains(player.Start))
            {
                return $"the start of {player.Id} at {player.Start.X}, {player.Start.Y} is not a start of the map";
            }

            for (var j = 0; j < i; j++)
            {
                var other = Players[j];
                if (other.Slot == player.Slot)
                {
                    return $"slot {player.Slot} is taken twice";
                }

                if (other.Start == player.Start || other.StartIndex == player.StartIndex)
                {
                    return $"{other.Id} and {player.Id} share a start";
                }

                if (other.Color == player.Color)
                {
                    return $"{other.Id} and {player.Id} share a colour";
                }
            }
        }

        return null;
    }

    public bool Equals(MatchSetup? other) =>
        other is not null
        && MapId == other.MapId
        && Seed == other.Seed
        && Options == other.Options
        && Players.AsSpan().SequenceEqual(other.Players.AsSpan())
        && MapStarts.AsSpan().SequenceEqual(other.MapStarts.AsSpan());

    // Only for dictionaries; System.HashCode is banned in the simulation.
    public override int GetHashCode() => unchecked((int)Seed ^ (int)(Seed >> 32) ^ (Players.IsDefault ? 0 : Players.Length * 7919));
}
