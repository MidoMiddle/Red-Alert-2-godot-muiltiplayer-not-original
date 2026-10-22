using System;
using System.Collections.Immutable;
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

/// <summary>The level of a bot; only «Medium» in M5, the others come with M8.</summary>
public enum AiDifficulty : byte
{
    Medium = 1,
}

/// <summary>The options of a match (docs/implementation/M5_first_bot.md, 4.11).</summary>
/// <param name="StartingCredits">The credits of every player at the start.</param>
/// <param name="ShortGame">A player loses with the last building, not the last unit (step 10).</param>
/// <param name="GameSpeed">1–6, as the skirmish screen of M6 offers; the client plays it, the simulation keeps it.</param>
public sealed record MatchOptions(int StartingCredits = SimulationSettings.DefaultStartingCredits, bool ShortGame = false, int GameSpeed = 4);

/// <summary>
/// One player of a match (docs/implementation/M5_first_bot.md, 4.11). <see cref="Slot"/> is its <see cref="PlayerId"/>. The start is
/// chosen before the match — a random one by whoever sets the match up — and kept as a cell: the map grid knows no starts.
/// </summary>
public sealed record MatchPlayer(
    int Slot, PlayerKind Kind, AiDifficulty Difficulty, string Faction, int Color, int Team, int StartIndex, CellPos Start)
{
    public PlayerId Id => new(Slot);
}

/// <summary>
/// The description of a match (docs/implementation/M5_first_bot.md, 4.11): the map it is played on (an id only for show — the map itself
/// is in the settings and the replay), the seed, the options and the players. It sets the starting state up — factions, an MCV and
/// three conscripts at every start (<see cref="MatchStart"/>) — and goes into the replay with the settings
/// (<see cref="SimulationSettings.ForMatch"/>). The slots that are not in it stay empty.
/// </summary>
public sealed record MatchSetup(string MapId, ulong Seed, MatchOptions Options, ImmutableArray<MatchPlayer> Players)
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
    /// when it can be played: 2–8 players in different slots 1–8, a person among them, different starts on the map and different colours,
    /// known factions, credits not negative, a speed of 1–6.
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

        var humans = 0;
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

            if (player.Start.X < 0 || player.Start.Y < 0 || player.Start.X >= width || player.Start.Y >= height)
            {
                return $"the start of {player.Id} at {player.Start.X}, {player.Start.Y} is off the map";
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

            humans += player.Kind == PlayerKind.Human ? 1 : 0;
        }

        return humans == 0 ? "nobody plays: a match needs a person" : null;
    }

    public bool Equals(MatchSetup? other) =>
        other is not null
        && MapId == other.MapId
        && Seed == other.Seed
        && Options == other.Options
        && Players.AsSpan().SequenceEqual(other.Players.AsSpan());

    // Only for dictionaries; System.HashCode is banned in the simulation.
    public override int GetHashCode() => unchecked((int)Seed ^ (int)(Seed >> 32) ^ (Players.IsDefault ? 0 : Players.Length * 7919));
}
