using System.Collections.Immutable;
using Rts.Client.Matches;
using Rts.Simulation.Commands;
using Rts.Simulation.Map;
using Rts.Simulation.Matches;
using Rts.Simulation.Tests.Matches;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>
/// The records of the tests by people and their protocol (docs/implementation/M8_ai_difficulty.md, section 6, step 14): the facts of
/// a match with the answers of the person, JSON there and back, the row of section 6 by who played, and what the protocol says.
/// </summary>
public sealed class PlaytestTests
{
    private static readonly PlayerId Person = new(1);

    private static PlaytestAnswers Answers(int difficulty = 2, string stupid = "", string unfair = "", int ally = 4) =>
        new(PlaytestRecord.Newcomer, difficulty, stupid, unfair, ally, "", new Dictionary<int, string> { [2] = "aggressor" });

    /// <summary>The person against two bots — slot 2 random, slot 3 its ally when <paramref name="ally"/> — who surrenders on tick 3.</summary>
    private static (MatchSummary Summary, MatchSetup Match) Played(bool ally)
    {
        var setup = MatchSetupTests.OneOnOne();
        setup = setup with
        {
            Players =
            [
                setup.Players[0],
                setup.Players[1] with { Difficulty = AiDifficulty.Hard, Personality = "random" },
                MatchSetupTests.Player(3, PlayerKind.Ai, 50, 10) with { Team = ally ? setup.Players[0].Team : 3 },
            ],
        };
        var simulation = new GameSimulation(SimulationSettings.ForMatch(setup, new MapGrid(64, 64)), TestRules.Standard);
        simulation.Enqueue(new SurrenderCommand(Person, 3, 0));
        for (var tick = 0; tick < 20; tick++)
        {
            simulation.Step();
        }

        return (MatchSummary.Of(simulation.World, Person, 15)!, setup);
    }

    private static PlaytestRecord Record(bool ally = false, PlaytestAnswers? answers = null)
    {
        var (summary, match) = Played(ally);
        return PlaytestRecord.Of(
            summary, match, Person, player => player.Personality == "random" ? "aggressor" : player.Personality, answers ?? Answers(),
            new DateTime(2026, 10, 24, 14, 5, 0), "user://playtests/a.rtsreplay", "user://logs/ai");
    }

    private static PlaytestRecord Match(AiDifficulty level, MatchOutcome outcome, string date, int difficulty = 3, string unfair = "") =>
        Record() with
        {
            Date = date,
            Outcome = outcome,
            Difficulty = difficulty,
            Unfair = unfair,
            Bots = [new PlaytestBot(2, level, "builder", Random: true, Ally: false, Guess: "builder")],
        };

    [Fact]
    public void A_record_keeps_the_match_with_the_random_personality_as_drawn_and_the_answers()
    {
        var record = Record();

        Assert.Equal(("clear", 7UL, MatchOutcome.Defeat, "2026-10-24 14:05"), (record.Map, record.Seed, record.Outcome, record.Date));
        Assert.Equal(
            [new PlaytestBot(2, AiDifficulty.Hard, "aggressor", Random: true, Ally: false, "aggressor"), new PlaytestBot(3, AiDifficulty.Medium, "balanced", false, false, PlaytestRecord.CannotTell)],
            record.Bots.ToArray());
        Assert.Equal(MatchSummary.Columns.Length, record.Numbers.Length);
        Assert.Equal(0, record.AllyHelped);
        Assert.Equal("user://playtests/a.rtsreplay", record.Replay);

        var allied = Record(ally: true);
        Assert.True(allied.Bots[1].Ally);
        Assert.Equal("", allied.Bots[1].Guess);
        Assert.Equal(4, allied.AllyHelped);
    }

    [Fact]
    public void A_record_comes_back_from_its_json()
    {
        var record = Record(answers: Answers(stupid: "drove the tanks into the river, 6:10", unfair: "«saw» my base at 2:00"));

        var read = PlaytestRecord.FromJson(record.ToJson());

        Assert.NotNull(read);
        Assert.Equal(record.ToJson(), read.ToJson());
        Assert.Contains("\"Outcome\": \"Defeat\"", record.ToJson());
        Assert.Null(PlaytestRecord.FromJson("not json"));
        Assert.Null(PlaytestRecord.FromJson("{}"));
    }

    [Fact]
    public void The_row_of_section_6_follows_who_played()
    {
        Assert.Equal(PlaytestRow.Easy, PlaytestReport.RowOf(Match(AiDifficulty.Easy, MatchOutcome.Victory, "1")));
        Assert.Equal(PlaytestRow.Hard, PlaytestReport.RowOf(Match(AiDifficulty.Hard, MatchOutcome.Victory, "1")));
        Assert.Equal(PlaytestRow.Brutal, PlaytestReport.RowOf(Match(AiDifficulty.Brutal, MatchOutcome.Victory, "1")));
        Assert.Equal(PlaytestRow.Ally, PlaytestReport.RowOf(Record(ally: true)));

        // Bots of two levels, or «Medium» alone, are not a row of the protocol.
        Assert.Equal(PlaytestRow.Other, PlaytestReport.RowOf(Record()));
        Assert.Equal(PlaytestRow.Other, PlaytestReport.RowOf(Match(AiDifficulty.Medium, MatchOutcome.Victory, "1")));
    }

    [Fact]
    public void The_expectations_of_section_6_hold_or_not_or_wait_for_more_matches()
    {
        var lostOnce = new[] { Match(AiDifficulty.Easy, MatchOutcome.Defeat, "1") };
        Assert.Null(PlaytestReport.Holds(PlaytestRow.Easy, lostOnce));
        Assert.True(PlaytestReport.Holds(PlaytestRow.Easy, [.. lostOnce, Match(AiDifficulty.Easy, MatchOutcome.Victory, "2")]));
        Assert.False(PlaytestReport.Holds(PlaytestRow.Easy, [.. lostOnce, lostOnce[0], Match(AiDifficulty.Easy, MatchOutcome.Victory, "3")]));

        var won = Match(AiDifficulty.Hard, MatchOutcome.Victory, "1");
        var lost = Match(AiDifficulty.Hard, MatchOutcome.Defeat, "2");
        Assert.Null(PlaytestReport.Holds(PlaytestRow.Hard, [won, won]));
        Assert.True(PlaytestReport.Holds(PlaytestRow.Hard, [won, won, lost]));
        Assert.False(PlaytestReport.Holds(PlaytestRow.Hard, [won, won, won]));

        var hard = Match(AiDifficulty.Brutal, MatchOutcome.Defeat, "1", difficulty: 5);
        Assert.True(PlaytestReport.Holds(PlaytestRow.Brutal, [hard, hard with { Difficulty = 4 }]));
        Assert.False(PlaytestReport.Holds(PlaytestRow.Brutal, [hard, hard with { Unfair = "knew where my army was" }]));
        Assert.False(PlaytestReport.Holds(PlaytestRow.Brutal, [hard, hard with { Difficulty = 2 }]));

        var helped = Record(ally: true);
        Assert.True(PlaytestReport.Holds(PlaytestRow.Ally, [helped, helped]));
        Assert.False(PlaytestReport.Holds(PlaytestRow.Ally, [helped, helped with { AllyHelped = 1 }]));
    }

    [Fact]
    public void The_protocol_shows_every_row_its_matches_guesses_and_what_was_said()
    {
        var easy = Match(AiDifficulty.Easy, MatchOutcome.Victory, "2026-10-24 14:05", difficulty: 2) with { Stupid = "left its harvester alone" };
        var guessedWrong = easy with
        {
            Date = "2026-10-24 15:00",
            Outcome = MatchOutcome.Defeat,
            Bots = [new PlaytestBot(2, AiDifficulty.Easy, "saboteur", Random: true, Ally: false, Guess: PlaytestRecord.CannotTell)],
        };

        var text = PlaytestReport.Markdown([guessedWrong, easy]);

        Assert.Contains("## Новичок против «Лёгкого» — партий 2 из 3", text);
        Assert.Contains("выигрывает с первой или второй попытки — **выполнено**", text);
        Assert.Contains("Побед 1 из 2 · средняя сложность 2,0 · характер угадан 1 из 2", text);
        Assert.Contains("Easy · builder (Random) — builder ✓", text);
        Assert.Contains("Easy · saboteur (Random) — не понял ✗", text);
        Assert.Contains("- 2026-10-24 14:05, clear, 0:01: left its harvester alone", text);
        Assert.Contains("## Ветеран против «Жестокого» — партий 0 из 2", text);
        Assert.DoesNotContain("Прочие партии", text);

        // The earlier match is first, whatever order the files come in.
        Assert.True(text.IndexOf("| 2026-10-24 14:05", StringComparison.Ordinal) < text.IndexOf("| 2026-10-24 15:00", StringComparison.Ordinal));
        Assert.Contains("партий — 0", PlaytestReport.Markdown([]));
        Assert.DoesNotContain("\n\n\n", text);
        Assert.DoesNotContain("Enemies", easy.ToJson());
    }
}
