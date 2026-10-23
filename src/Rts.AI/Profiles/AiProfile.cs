using System;
using Rts.Simulation.Rules;

namespace Rts.AI.Profiles;

/// <summary>
/// The profile of one bot (docs/implementation/M8_ai_difficulty.md, 4.2, 4.7, step 1): its level, its personality and its faction together.
/// <see cref="Settings"/> are those of the level with the build order and the army of the personality for the faction — the build order of
/// the quality the level asks for.
/// </summary>
public sealed record AiProfile(AiSettings Settings, PersonalityProfile Personality)
{
    public static AiProfile Of(DifficultyDef level, PersonalityDef personality, string faction)
    {
        var settings = AiSettings.From(level);
        return new AiProfile(
            settings with { BuildOrder = BuildOrderOf(personality, faction, settings.Quality), Army = ArmyOf(personality, faction) },
            PersonalityProfile.From(personality));
    }

    /// <summary>The build order of <paramref name="personality"/> for <paramref name="faction"/> of that quality.</summary>
    public static string BuildOrderOf(PersonalityDef personality, string faction, BuildOrderQuality quality)
    {
        foreach (var orders in personality.BuildOrders)
        {
            if (orders.Faction == faction)
            {
                return quality switch
                {
                    BuildOrderQuality.Simple => orders.Simple,
                    BuildOrderQuality.Optimized => orders.Optimized,
                    _ => orders.Standard,
                };
            }
        }

        throw new ArgumentException($"'{personality.Id}' has no build orders for '{faction}'.", nameof(faction));
    }

    /// <summary>The army of <paramref name="personality"/> for <paramref name="faction"/>.</summary>
    public static string ArmyOf(PersonalityDef personality, string faction)
    {
        foreach (var army in personality.Armies)
        {
            if (army.Faction == faction)
            {
                return army.Army;
            }
        }

        throw new ArgumentException($"'{personality.Id}' has no army for '{faction}'.", nameof(faction));
    }
}
