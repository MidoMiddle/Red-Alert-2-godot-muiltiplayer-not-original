using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Rts.Simulation.Matches;

namespace Rts.Client.Matches;

/// <summary>The rows of the protocol of the tests by people (docs/implementation/M8_ai_difficulty.md, section 6).</summary>
public enum PlaytestRow
{
    /// <summary>Against «Easy» bots only: the newcomer's row.</summary>
    Easy,

    /// <summary>Against «Hard» bots only, a veteran.</summary>
    Hard,

    /// <summary>Against «Brutal» bots only, a veteran.</summary>
    Brutal,

    /// <summary>The person with a bot ally — in place of two people and two bots until the network (decision of the owner, step 14).</summary>
    Ally,

    /// <summary>Any other match: kept, not judged.</summary>
    Other,
}

/// <summary>
/// The protocol of the tests by people (M8 step 14) made of the records of <c>user://playtests</c>: for every row of section 6 its
/// matches, wins, the mean difficulty, the personalities guessed, whether the expectation of the row holds, and every «stupid», «unfair»
/// and note with the map and the date. In Russian, as the documents it goes into. Pure, tested in Rts.Simulation.Tests.
/// </summary>
public static class PlaytestReport
{
    /// <summary>The matches section 6 plans for a row.</summary>
    public static int Planned(PlaytestRow row) => row switch
    {
        PlaytestRow.Easy => 3,
        PlaytestRow.Hard => 3,
        PlaytestRow.Brutal => 2,
        PlaytestRow.Ally => 2,
        _ => 0,
    };

    /// <summary>The row of <paramref name="record"/> by who played: a bot ally makes it <see cref="PlaytestRow.Ally"/>.</summary>
    public static PlaytestRow RowOf(PlaytestRecord record)
    {
        if (record.Bots.Any(bot => bot.Ally))
        {
            return PlaytestRow.Ally;
        }

        var levels = record.Enemies.Select(bot => bot.Level).Distinct().ToList();
        return levels.Count != 1 ? PlaytestRow.Other : levels[0] switch
        {
            AiDifficulty.Easy => PlaytestRow.Easy,
            AiDifficulty.Hard => PlaytestRow.Hard,
            AiDifficulty.Brutal => PlaytestRow.Brutal,
            _ => PlaytestRow.Other,
        };
    }

    /// <summary>
    /// Whether the expectation of section 6 holds for the matches of a row, in order of their dates: true, false, or null — too few
    /// matches to say. «Easy»: a win in the first or the second; «Hard»: under 70 % won of three or more; «Brutal»: hard (4 or 5 on
    /// average) and nothing unfair; the ally: helped, 3 or more on average.
    /// </summary>
    public static bool? Holds(PlaytestRow row, IReadOnlyList<PlaytestRecord> matches)
    {
        switch (row)
        {
            case PlaytestRow.Easy:
                if (matches.Take(2).Any(match => match.Outcome == MatchOutcome.Victory))
                {
                    return true;
                }

                return matches.Count >= 2 ? false : null;
            case PlaytestRow.Hard:
                return matches.Count < Planned(row) ? null : Wins(matches) < 0.7 * matches.Count;
            case PlaytestRow.Brutal:
                return matches.Count < Planned(row)
                    ? null
                    : matches.Average(match => match.Difficulty) >= 4 && matches.All(match => match.Unfair.Length == 0);
            case PlaytestRow.Ally:
                return matches.Count < Planned(row) ? null : matches.Average(match => match.AllyHelped) >= 3;
            default:
                return null;
        }
    }

    /// <summary>The protocol of <paramref name="records"/> as Markdown.</summary>
    public static string Markdown(IEnumerable<PlaytestRecord> records)
    {
        var all = records.OrderBy(record => record.Date, System.StringComparer.Ordinal).ToList();
        var text = new StringBuilder();
        text.Append("# Протокол тестов людьми (M8, раздел 6)\n\n");
        text.Append(CultureInfo.InvariantCulture, $"Собран игрой из записей `user://playtests`: партий — {all.Count}.\n\n");
        foreach (var row in new[] { PlaytestRow.Easy, PlaytestRow.Hard, PlaytestRow.Brutal, PlaytestRow.Ally, PlaytestRow.Other })
        {
            var matches = all.Where(record => RowOf(record) == row).ToList();
            if (row == PlaytestRow.Other && matches.Count == 0)
            {
                continue;
            }

            AppendRow(text, row, matches);
        }

        return text.ToString();
    }

    private static void AppendRow(StringBuilder text, PlaytestRow row, List<PlaytestRecord> matches)
    {
        var planned = Planned(row);
        text.Append(CultureInfo.InvariantCulture, $"## {Title(row)} — партий {matches.Count}{(planned > 0 ? $" из {planned}" : "")}\n\n");
        if (Expectation(row) is { } expectation)
        {
            var verdict = Holds(row, matches) switch
            {
                true => "**выполнено**",
                false => "**не выполнено**",
                _ => "мало партий",
            };
            text.Append(CultureInfo.InvariantCulture, $"Ожидание: {expectation} — {verdict}.\n\n");
        }

        if (matches.Count == 0)
        {
            return;
        }

        text.Append("| Дата | Карта | Роль | Исход | Длина | Сложность | Боты: уровень · характер — догадка |\n");
        text.Append("|---|---|---|---|---|---|---|\n");
        foreach (var match in matches)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"| {match.Date} | {match.Map} | {Role(match.Role)} | {Outcome(match.Outcome)} | {match.Duration} | {match.Difficulty} | {Bots(match)} |\n");
        }

        var enemies = matches.SelectMany(match => match.Enemies).ToList();
        var guessed = enemies.Count(bot => bot.Guess == bot.Personality);
        text.Append('\n');
        text.Append(CultureInfo.InvariantCulture,
            $"Побед {Wins(matches)} из {matches.Count} · средняя сложность {Mean(matches.Select(match => match.Difficulty))} · характер угадан {guessed} из {enemies.Count}");
        if (row == PlaytestRow.Ally)
        {
            text.Append(CultureInfo.InvariantCulture, $" · польза союзника {Mean(matches.Select(match => match.AllyHelped))}");
        }

        text.Append('\n');
        AppendNotes(text, "Бот вёл себя глупо", matches, match => match.Stupid);
        AppendNotes(text, "Бот вёл себя нечестно", matches, match => match.Unfair);
        AppendNotes(text, "Заметки", matches, match => match.Notes);
        text.Append('\n');
    }

    private static void AppendNotes(StringBuilder text, string title, List<PlaytestRecord> matches, System.Func<PlaytestRecord, string> note)
    {
        var said = matches.Where(match => note(match).Length > 0).ToList();
        if (said.Count == 0)
        {
            return;
        }

        text.Append(CultureInfo.InvariantCulture, $"\n**{title}:**\n");
        foreach (var match in said)
        {
            text.Append(CultureInfo.InvariantCulture, $"- {match.Date}, {match.Map}, {match.Duration}: {note(match)}\n");
        }
    }

    private static int Wins(IEnumerable<PlaytestRecord> matches) => matches.Count(match => match.Outcome == MatchOutcome.Victory);

    private static string Mean(IEnumerable<int> values)
    {
        var list = values.ToList();
        return list.Count == 0 ? "—" : list.Average().ToString("0.0", CultureInfo.GetCultureInfo("ru-RU"));
    }

    private static string Bots(PlaytestRecord match) => string.Join("; ", match.Bots.Select(bot =>
    {
        var played = $"{Level(bot.Level)} · {bot.Personality}{(bot.Random ? " (Random)" : "")}";
        if (bot.Ally)
        {
            return $"союзник {played}";
        }

        var guess = bot.Guess == PlaytestRecord.CannotTell ? "не понял" : bot.Guess;
        return $"{played} — {guess} {(bot.Guess == bot.Personality ? "✓" : "✗")}";
    }));

    private static string Title(PlaytestRow row) => row switch
    {
        PlaytestRow.Easy => "Новичок против «Лёгкого»",
        PlaytestRow.Hard => "Ветеран против «Сложного»",
        PlaytestRow.Brutal => "Ветеран против «Жестокого»",
        PlaytestRow.Ally => "Человек и бот-союзник против двух «Средних»",
        _ => "Прочие партии",
    };

    private static string? Expectation(PlaytestRow row) => row switch
    {
        PlaytestRow.Easy => "выигрывает с первой или второй попытки",
        PlaytestRow.Hard => "побеждает не всегда — меньше 70 %",
        PlaytestRow.Brutal => "сложно (4–5), но ничего нечестного",
        PlaytestRow.Ally => "союзник полезен — 3 и выше",
        _ => null,
    };

    private static string Role(string role) => role == PlaytestRecord.Newcomer ? "новичок" : "ветеран";

    private static string Outcome(MatchOutcome outcome) => outcome switch
    {
        MatchOutcome.Victory => "победа",
        MatchOutcome.Defeat => "поражение",
        _ => "ничья",
    };

    private static string Level(AiDifficulty level) => level switch
    {
        AiDifficulty.Easy => "Easy",
        AiDifficulty.Hard => "Hard",
        AiDifficulty.Brutal => "Brutal",
        _ => "Medium",
    };
}
