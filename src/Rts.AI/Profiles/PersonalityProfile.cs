using System;
using System.Collections.Immutable;
using Rts.Simulation.Math;
using Rts.Simulation.Rules;

namespace Rts.AI.Profiles;

/// <summary>What a squad may go for, in the order of <see cref="PersonalityDef.Targets"/>.</summary>
public enum AttackTarget
{
    Production,
    Army,
    Power,
    Harvesters,
    Defense,
    Core,
}

/// <summary>
/// A personality of the bots as the bot reads it (docs/implementation/M8_ai_difficulty.md, 4.7, step 1): a <c>personality</c> of the data,
/// typed. The strategist weighs by <see cref="Weights"/> and the squads go for <see cref="TargetPriority"/> from step 8; until then the bot
/// of M5 plays every personality alike.
/// </summary>
public sealed record PersonalityProfile
{
    public required string Id { get; init; }

    public required PersonalityWeights Weights { get; init; }

    /// <summary>The window of the first attack, in ticks, for the levels that leave it to the personality.</summary>
    public required int FirstAttackMin { get; init; }

    public required int FirstAttackMax { get; init; }

    /// <summary>The ticks between two waves.</summary>
    public required int WaveInterval { get; init; }

    /// <summary>A fighting squad turns back when its strength falls below this share of the enemy's near it.</summary>
    public required Fixed RetreatThreshold { get; init; }

    /// <summary>Every target once, the first the one a squad goes for first.</summary>
    public required ImmutableArray<AttackTarget> TargetPriority { get; init; }

    public static PersonalityProfile From(PersonalityDef personality)
    {
        var targets = ImmutableArray.CreateBuilder<AttackTarget>(personality.TargetPriority.Length);
        foreach (var target in personality.TargetPriority)
        {
            var index = PersonalityDef.Targets.IndexOf(target);
            targets.Add(index >= 0 ? (AttackTarget)index : throw new ArgumentException($"'{personality.Id}' has no target '{target}'.", nameof(personality)));
        }

        return new PersonalityProfile
        {
            Id = personality.Id,
            Weights = personality.Weights,
            FirstAttackMin = personality.FirstAttackMin,
            FirstAttackMax = personality.FirstAttackMax,
            WaveInterval = personality.WaveInterval,
            RetreatThreshold = personality.RetreatThreshold,
            TargetPriority = targets.MoveToImmutable(),
        };
    }
}
