using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Rts.AI.Modules;
using Rts.AI.Profiles;
using Rts.AI.Team;
using Rts.Simulation;
using Rts.Simulation.Ai;
using Rts.Simulation.Geometry;
using Rts.Simulation.Matches;
using Rts.Simulation.Rules;
using Rts.Simulation.World;

namespace Rts.AI;

/// <summary>The bots of a match setup (docs/implementation/M5_first_bot.md, 4.11): one <see cref="AiPlayer"/> for every bot slot.</summary>
public static class MatchBots
{
    /// <summary>
    /// The bot of <paramref name="player"/> in <paramref name="setup"/>, by its level and the personality «Balanced» in the data of
    /// <paramref name="rules"/> (M5 step 14, M8 step 1) — the numbers of the code when the data has none: it knows its start and every
    /// start of the map.
    /// </summary>
    public static IAiController Create(MatchSetup setup, MatchPlayer player, GameRules rules) => Create(setup, player, rules, BalancedId);

    /// <summary>
    /// The bot of <paramref name="player"/> with the personality <paramref name="personality"/> of the data — the Arena chooses it (M8 step
    /// 4); the match setup does not keep it yet (step 13).
    /// </summary>
    public static IAiController Create(MatchSetup setup, MatchPlayer player, GameRules rules, string personality) =>
        Create(setup, player, rules, personality, null);

    /// <summary>The «Medium» bot of <paramref name="player"/> with <paramref name="settings"/> of its own — the tuning of step 14, a person's seat played by a bot in the arena.</summary>
    public static AiPlayer Create(MatchSetup setup, MatchPlayer player, AiSettings settings, TeamBlackboard? board = null) =>
        Medium(player, settings, setup, board);

    /// <summary>
    /// Creates the bots of <paramref name="setup"/> (M8 step 11): every team of two bots or more shares one <see cref="TeamBlackboard"/>,
    /// so a match and its replay checked with the bots run again (<see cref="ReplayPlayer.VerifyWithBots"/>) get the same bots. The
    /// personality of a bot is <paramref name="personalityOf"/>, «Balanced» without it.
    /// </summary>
    public static Func<MatchPlayer, IAiController> For(MatchSetup setup, GameRules rules, Func<MatchPlayer, string>? personalityOf = null)
    {
        var boards = Boards(setup);
        return player => Create(setup, player, rules, personalityOf?.Invoke(player) ?? BalancedId, boards.GetValueOrDefault(player.Team));
    }

    /// <summary>The boards of the teams of <paramref name="setup"/> with two bots or more, by team.</summary>
    public static IReadOnlyDictionary<int, TeamBlackboard> Boards(MatchSetup setup)
    {
        var boards = new SortedDictionary<int, TeamBlackboard>();
        foreach (var team in setup.Players.Where(player => player.Kind == PlayerKind.Ai).GroupBy(player => player.Team))
        {
            var bots = team.Select(player => player.Id).OrderBy(id => id.Value).ToImmutableArray();
            if (bots.Length >= 2)
            {
                boards[team.Key] = new TeamBlackboard(bots);
            }
        }

        return boards;
    }

    private static IAiController Create(MatchSetup setup, MatchPlayer player, GameRules rules, string personality, TeamBlackboard? board) =>
        player.Kind == PlayerKind.Ai
            ? Medium(player, SettingsOf(player.Difficulty, player.Faction, rules, personality), setup, board, $"{LevelId(player.Difficulty)}:{personality}")
            : throw new ArgumentException($"{player.Id} is played by a person.", nameof(player));

    /// <summary>
    /// Attaches the bots of the match of <paramref name="simulation"/> and returns them, for the debug overlay (step 13); none without a
    /// match setup. Only for a live match.
    /// </summary>
    public static IReadOnlyList<AiPlayer> AttachAll(GameSimulation simulation)
    {
        var bots = new List<AiPlayer>();
        if (simulation.Settings.Match is not { } setup)
        {
            return bots;
        }

        var create = For(setup, simulation.World.Rules);
        foreach (var player in setup.Players)
        {
            if (player.Kind == PlayerKind.Ai)
            {
                var bot = (AiPlayer)create(player);
                simulation.AttachAi(bot);
                bots.Add(bot);
            }
        }

        return bots;
    }

    /// <summary>
    /// The bot of M5 so far: its economy (step 4), its base by the build order and where there is room (step 5), its army (step 6), its
    /// scouting (step 7), its squads (step 8), its strategist and the defense of its base (step 9) — and its team (M8 step 11): the board
    /// of its allied bots, if any, and the people of its team. The starts of its allies are not where the enemy may be: the bot does not
    /// get them.
    /// </summary>
    private static AiPlayer Medium(MatchPlayer player, AiSettings settings, MatchSetup setup, TeamBlackboard? board, string? profile = null)
    {
        var humans = setup.Players
            .Where(other => other.Kind == PlayerKind.Human && other.Team == player.Team && other.Id != player.Id)
            .Select(other => new HumanAlly(other.Id, other.Start))
            .ToImmutableArray();
        var allied = setup.Players.Where(other => other.Team == player.Team && other.Id != player.Id).Select(other => other.Start).ToHashSet();
        return new(
            player.Id,
            settings,
            [
                new Strategist(), new EconomyManager(settings), new BuildPlanner(settings), new ProductionManager(settings), new ScoutManager(settings),
                new SquadManager(settings, board), new DefenseManager(settings, board), new TeamCoordinator(board, humans, settings),
            ],
            player.Start,
            setup.MapStarts.IsDefault ? setup.MapStarts : [.. setup.MapStarts.Where(start => !allied.Contains(start))],
            setup.Seed)
        {
            Profile = profile,
        };
    }

    /// <summary>The id of «Medium» in the data: ids are shared by all the data, and <c>medium</c> is an armour.</summary>
    public static readonly string MediumId = DifficultyDef.IdOf(AiDifficulty.Medium);

    /// <summary>The personality every bot plays until the skirmish screen offers the choice (M8 steps 8, 13).</summary>
    public const string BalancedId = "balanced";

    /// <summary>The id of a level in the data (M8 step 1): <c>ai_easy</c>, <c>ai_medium</c>, <c>ai_hard</c>, <c>ai_brutal</c>.</summary>
    public static string LevelId(AiDifficulty difficulty) => DifficultyDef.IdOf(difficulty);

    /// <summary>
    /// The settings of a bot of <paramref name="faction"/> at a level: the level of the data of <paramref name="rules"/> with the build order
    /// and the army of <paramref name="personality"/> (<see cref="AiProfile"/>). Without «Balanced» in the data — the build order and the army
    /// of the code; without the level — the «Medium» of the code, and no other level. Another personality the data does not have is a mistake.
    /// </summary>
    public static AiSettings SettingsOf(AiDifficulty difficulty, string faction, GameRules rules, string personality = BalancedId)
    {
        var database = rules.Database;
        if (!database.TryGetDifficulty(LevelId(difficulty), out var level))
        {
            return difficulty == AiDifficulty.Medium
                ? AiSettings.Medium
                : throw new ArgumentException($"The data has no level '{LevelId(difficulty)}'.", nameof(difficulty));
        }

        if (database.TryGetPersonality(personality, out var chosen))
        {
            return AiProfile.Of(level, chosen, faction).Settings;
        }

        return personality == BalancedId
            ? AiSettings.From(level)
            : throw new ArgumentException($"The data has no personality '{personality}'.", nameof(personality));
    }
}
