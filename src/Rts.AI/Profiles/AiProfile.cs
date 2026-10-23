using System;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;

namespace Rts.AI.Profiles;

/// <summary>
/// The profile of one bot (docs/implementation/M8_ai_difficulty.md, 4.2, 4.7, step 1): its level, its personality and its faction together.
/// <see cref="Settings"/> are those of the level with the build order and the army of the personality for the faction — the build order of
/// the quality the level asks for — its threshold of retreat (step 5), and from step 8 its weights and its attacks: refineries by the
/// weight of the economy, defenses by that of defense, the second war factory sooner the more the army weighs; the first attack in the
/// window of the personality when the level leaves it to it, the waves apart, the targets in its order.
/// </summary>
public sealed record AiProfile(AiSettings Settings, PersonalityProfile Personality)
{
    public static AiProfile Of(DifficultyDef level, PersonalityDef personality, string faction)
    {
        var settings = AiSettings.From(level);
        var weights = personality.Weights;
        var byPersonality = settings.FirstAttackDelay == 0;
        return new AiProfile(
            settings with
            {
                BuildOrder = BuildOrderOf(personality, faction, settings.Quality),
                Army = ArmyOf(personality, faction),
                RetreatThresholdPercent = Percent(personality.RetreatThreshold),
                // Step 8: the weights, 1 the level as it is.
                MaxRefineries = IntMath.Max(1, Scaled(settings.MaxRefineries, weights.Economy)),
                MaxDefenses = Scaled(settings.MaxDefenses, weights.Defense),
                SecondFactoryCredits = weights.Army > Fixed.Zero ? (int)(((Fixed.FromInt(settings.SecondFactoryCredits) / weights.Army).Raw + (1L << (Fixed.FractionalBits - 1))) >> Fixed.FractionalBits) : int.MaxValue,
                FirstAttackFrom = byPersonality ? personality.FirstAttackMin : settings.FirstAttackDelay,
                FirstAttackBy = byPersonality ? personality.FirstAttackMax : 0,
                WaveInterval = personality.WaveInterval,
                TargetPriority = string.Join(",", personality.TargetPriority),
                // Step 10: harassment above 1 sends the squads for the harvesters in the fields first; a bold personality — army or
                // harassment above 1, the «Aggressor» and the «Saboteur» — counts only the enemy it has seen: its rush and its raids
                // take the risk the cautious ones do not.
                Raids = weights.Harassment > Fixed.One,
                AssumedEnemyPercent = weights.Army > Fixed.One || weights.Harassment > Fixed.One ? 0 : settings.AssumedEnemyPercent,
            },
            PersonalityProfile.From(personality));
    }

    /// <summary><paramref name="value"/> × <paramref name="weight"/>, rounded half up: 3 × 1.5 = 5.</summary>
    private static int Scaled(int value, Fixed weight) => (int)((value * weight.Raw + (1L << (Fixed.FractionalBits - 1))) >> Fixed.FractionalBits);

    /// <summary>0.7 as a whole percent, 70.</summary>
    private static int Percent(Fixed fraction) => (int)((fraction.Raw * 100 + (1L << (Fixed.FractionalBits - 1))) >> Fixed.FractionalBits);

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
