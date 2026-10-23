using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text.Json;
using Rts.Simulation.Matches;
using Rts.Simulation.World;

namespace Rts.Client.Matches;

/// <summary>
/// A bot of a match of the tests by people (docs/implementation/M8_ai_difficulty.md, section 6, step 14): its slot, level, the personality
/// it played — a random one as drawn — whether it was random, whether it played with the person, and what the person guessed it was.
/// </summary>
public sealed record PlaytestBot(int Slot, AiDifficulty Level, string Personality, bool Random, bool Ally, string Guess);

/// <summary>
/// What the person answers on the results screen (M8 step 14): newcomer or veteran, how hard it was (1–5), where the bot was stupid and
/// where unfair, how much a bot ally helped (1–5, 0 without an ally), notes, and the personality guessed for every enemy bot by slot
/// (<see cref="PlaytestRecord.CannotTell"/> when the person could not tell).
/// </summary>
public sealed record PlaytestAnswers(
    string Role, int Difficulty, string Stupid, string Unfair, int AllyHelped, string Notes, IReadOnlyDictionary<int, string> Guesses);

/// <summary>
/// One match of the tests by people (M8 step 14): the facts of the match and the answers of the person, kept as JSON in
/// <c>user://playtests</c> with the replay; <see cref="PlaytestReport"/> makes the protocol of them. Pure, tested in Rts.Simulation.Tests.
/// </summary>
public sealed record PlaytestRecord(
    string Date,
    string Map,
    ulong Seed,
    MatchOutcome Outcome,
    string Duration,
    int DurationTicks,
    string Role,
    int Difficulty,
    string Stupid,
    string Unfair,
    int AllyHelped,
    string Notes,
    ImmutableArray<PlaytestBot> Bots,
    ImmutableArray<int> Numbers,
    string Replay,
    string Journals)
{
    public const string Newcomer = "newcomer";
    public const string Veteran = "veteran";
    public const string CannotTell = "cannot_tell";

    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    /// <summary>
    /// The record of the match of <paramref name="summary"/> and <paramref name="match"/> for the person <paramref name="local"/>:
    /// <paramref name="personalityOf"/> gives the personality a bot played (<c>MatchBots.PersonalityOf</c>), so a random one is shown
    /// as drawn. The numbers are the person's row of the results.
    /// </summary>
    public static PlaytestRecord Of(
        MatchSummary summary, MatchSetup match, PlayerId local, Func<MatchPlayer, string> personalityOf, PlaytestAnswers answers,
        DateTime when, string replay, string journals)
    {
        var team = match.PlayerOf(local)?.Team;
        var bots = match.Players
            .Where(player => player.Kind == PlayerKind.Ai)
            .OrderBy(player => player.Slot)
            .Select(player => new PlaytestBot(
                player.Slot,
                player.Difficulty,
                personalityOf(player),
                player.Personality == MatchPlayer.RandomPersonality,
                player.Team == team,
                player.Team == team ? "" : answers.Guesses.GetValueOrDefault(player.Slot, CannotTell)))
            .ToImmutableArray();
        var numbers = summary.Rows.FirstOrDefault(row => row.IsYou)?.Numbers ?? [];
        return new PlaytestRecord(
            when.ToString("yyyy-MM-dd HH:mm", System.Globalization.CultureInfo.InvariantCulture),
            match.MapId,
            match.Seed,
            summary.Outcome,
            summary.Duration,
            summary.DurationTicks,
            answers.Role,
            answers.Difficulty,
            answers.Stupid.Trim(),
            answers.Unfair.Trim(),
            bots.Any(bot => bot.Ally) ? answers.AllyHelped : 0,
            answers.Notes.Trim(),
            bots,
            numbers,
            replay,
            journals);
    }

    /// <summary>The bots that played against the person.</summary>
    [System.Text.Json.Serialization.JsonIgnore]
    public IEnumerable<PlaytestBot> Enemies => Bots.Where(bot => !bot.Ally);

    public string ToJson() => JsonSerializer.Serialize(this, Json);

    /// <summary>The record of <see cref="ToJson"/>, or null when <paramref name="text"/> is not one.</summary>
    public static PlaytestRecord? FromJson(string text)
    {
        try
        {
            var record = JsonSerializer.Deserialize<PlaytestRecord>(text, Json);
            return record is null || record.Bots.IsDefault || record.Numbers.IsDefault ? null : record;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
