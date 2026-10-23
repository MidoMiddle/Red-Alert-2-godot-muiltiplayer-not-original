using System;
using System.Collections.Generic;
using Rts.AI.Modules;
using Rts.AI.Profiles;
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
    public static IAiController Create(MatchSetup setup, MatchPlayer player, GameRules rules) => player.Kind == PlayerKind.Ai
        ? Medium(player.Id, SettingsOf(player.Difficulty, player.Faction, rules), player.Start, setup)
        : throw new ArgumentException($"{player.Id} is played by a person.", nameof(player));

    /// <summary>The «Medium» bot of <paramref name="player"/> with <paramref name="settings"/> of its own — the tuning of step 14, a person's seat played by a bot in the arena.</summary>
    public static AiPlayer Create(MatchSetup setup, MatchPlayer player, AiSettings settings) => Medium(player.Id, settings, player.Start, setup);

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

        foreach (var player in setup.Players)
        {
            if (player.Kind == PlayerKind.Ai)
            {
                var bot = (AiPlayer)Create(setup, player, simulation.World.Rules);
                simulation.AttachAi(bot);
                bots.Add(bot);
            }
        }

        return bots;
    }

    /// <summary>The bot of M5 so far: its economy (step 4), its base by the build order and where there is room (step 5), its army (step 6), its scouting (step 7), its squads (step 8), its strategist and the defense of its base (step 9).</summary>
    private static AiPlayer Medium(PlayerId player, AiSettings settings, CellPos start, MatchSetup setup) =>
        new(
            player,
            settings,
            [
                new Strategist(), new EconomyManager(settings), new BuildPlanner(settings), new ProductionManager(settings), new ScoutManager(settings),
                new SquadManager(settings), new DefenseManager(settings),
            ],
            start,
            setup.MapStarts);

    /// <summary>The id of «Medium» in the data: ids are shared by all the data, and <c>medium</c> is an armour.</summary>
    public static readonly string MediumId = DifficultyDef.IdOf(AiDifficulty.Medium);

    /// <summary>The personality every bot plays until the skirmish screen offers the choice (M8 steps 8, 13).</summary>
    public const string BalancedId = "balanced";

    /// <summary>The id of a level in the data (M8 step 1): <c>ai_easy</c>, <c>ai_medium</c>, <c>ai_hard</c>, <c>ai_brutal</c>.</summary>
    public static string LevelId(AiDifficulty difficulty) => DifficultyDef.IdOf(difficulty);

    /// <summary>
    /// The settings of a bot of <paramref name="faction"/> at a level: the level of the data of <paramref name="rules"/> with the build order
    /// and the army of «Balanced» (<see cref="AiProfile"/>). Without the personality in the data — the build order and the army of the code;
    /// without the level — the «Medium» of the code, and no other level.
    /// </summary>
    public static AiSettings SettingsOf(AiDifficulty difficulty, string faction, GameRules rules)
    {
        var database = rules.Database;
        if (!database.TryGetDifficulty(LevelId(difficulty), out var level))
        {
            return difficulty == AiDifficulty.Medium
                ? AiSettings.Medium
                : throw new ArgumentException($"The data has no level '{LevelId(difficulty)}'.", nameof(difficulty));
        }

        return database.TryGetPersonality(BalancedId, out var personality) ? AiProfile.Of(level, personality, faction).Settings : AiSettings.From(level);
    }
}
