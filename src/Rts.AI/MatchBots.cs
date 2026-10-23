using System;
using System.Collections.Generic;
using Rts.AI.Modules;
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
    /// The bot of <paramref name="player"/> in <paramref name="setup"/>, by its level in the data of <paramref name="rules"/> (step 14) — the
    /// numbers of the code when the data has none: it knows its start and every start of the map.
    /// </summary>
    public static IAiController Create(MatchSetup setup, MatchPlayer player, GameRules rules) => player.Kind == PlayerKind.Ai
        ? Medium(player.Id, SettingsOf(player.Difficulty, rules), player.Start, setup)
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
    public const string MediumId = "ai_medium";

    /// <summary>The settings of a level: from the data of <paramref name="rules"/>, else those of the code.</summary>
    public static AiSettings SettingsOf(AiDifficulty difficulty, GameRules rules) => difficulty switch
    {
        AiDifficulty.Medium => rules.Database.TryGetDifficulty(MediumId, out var level) ? AiSettings.From(level) : AiSettings.Medium,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
    };
}
