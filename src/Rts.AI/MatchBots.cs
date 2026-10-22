using System;
using Rts.Simulation;
using Rts.Simulation.Ai;
using Rts.Simulation.Matches;

namespace Rts.AI;

/// <summary>The bots of a match setup (docs/implementation/M5_first_bot.md, 4.11): one <see cref="AiPlayer"/> for every bot slot.</summary>
public static class MatchBots
{
    /// <summary>The bot of <paramref name="player"/>, by its level.</summary>
    public static IAiController Create(MatchPlayer player) => player.Kind == PlayerKind.Ai
        ? new AiPlayer(player.Id, SettingsOf(player.Difficulty))
        : throw new ArgumentException($"{player.Id} is played by a person.", nameof(player));

    /// <summary>Attaches the bots of the match of <paramref name="simulation"/>; none without a match setup. Only for a live match.</summary>
    public static void AttachAll(GameSimulation simulation)
    {
        foreach (var player in simulation.Settings.Match?.Players ?? [])
        {
            if (player.Kind == PlayerKind.Ai)
            {
                simulation.AttachAi(Create(player));
            }
        }
    }

    private static AiSettings SettingsOf(AiDifficulty difficulty) => difficulty switch
    {
        AiDifficulty.Medium => AiSettings.Medium,
        _ => throw new ArgumentOutOfRangeException(nameof(difficulty), difficulty, null),
    };
}
