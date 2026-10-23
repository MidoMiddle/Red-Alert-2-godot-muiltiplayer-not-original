using Rts.Simulation.Matches;
using Rts.Simulation.Rules;

namespace Rts.Arena;

/// <summary>
/// The bot of one side (docs/implementation/M8_ai_difficulty.md, 4.10): <c>level[:personality[:faction]]</c> — <c>hard</c>,
/// <c>easy:aggressor</c>, <c>brutal:balanced:union</c>. The personality is <c>balanced</c> and the faction <c>union</c> unless written.
/// </summary>
public sealed record BotProfile(AiDifficulty Level, string Personality, string Faction)
{
    private static readonly string[] Levels = ["easy", "medium", "hard", "brutal"];

    /// <summary>Reads <paramref name="text"/> and checks the personality and the faction against <paramref name="rules"/>.</summary>
    public static BotProfile Parse(string text, RulesDatabase rules)
    {
        var parts = text.Split(':');
        if (text.Length == 0 || parts.Length > 3)
        {
            throw new ArenaException($"'{text}' is not a profile: write level[:personality[:faction]], such as hard:balanced");
        }

        var level = parts[0] switch
        {
            "easy" => AiDifficulty.Easy,
            "medium" => AiDifficulty.Medium,
            "hard" => AiDifficulty.Hard,
            "brutal" => AiDifficulty.Brutal,
            _ => throw new ArenaException($"'{parts[0]}' in '{text}' is not a level: one of {string.Join(", ", Levels)}"),
        };
        var personality = parts.Length > 1 ? parts[1] : "balanced";
        if (!rules.TryGetPersonality(personality, out _))
        {
            throw new ArenaException(
                $"'{personality}' in '{text}' is not a personality of the data: one of {string.Join(", ", rules.Personalities.Select(p => p.Id))}");
        }

        var faction = parts.Length > 2 ? parts[2] : "union";
        if (!rules.TryGetFaction(faction, out _))
        {
            throw new ArenaException($"'{faction}' in '{text}' is not a faction of the data: one of {string.Join(", ", rules.Factions.Select(f => f.Id))}");
        }

        return new BotProfile(level, personality, faction);
    }

    /// <summary>How strong the level is: «Easy» 0, «Medium» 1, «Hard» 2, «Brutal» 3 — a win of a lower one is an upset.</summary>
    public int Rank => Level switch { AiDifficulty.Easy => 0, AiDifficulty.Medium => 1, AiDifficulty.Hard => 2, _ => 3 };

    /// <summary>As the profile is written: <c>hard:balanced:union</c>.</summary>
    public override string ToString() => $"{Levels[Rank]}:{Personality}:{Faction}";
}
