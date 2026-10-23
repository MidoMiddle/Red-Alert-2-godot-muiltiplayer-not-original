using Rts.Simulation.Math;
using Rts.Simulation.Random;

namespace Rts.AI;

/// <summary>
/// The mistakes of a bot (docs/implementation/M8_ai_difficulty.md, 4.3, step 3): with the chance of its level a module takes one of the
/// three best of its choices at random instead of the best — a plausible mistake, not a foolish one.
/// </summary>
public static class AiChoice
{
    /// <summary>The choices a mistake picks among.</summary>
    public const int Top = 3;

    /// <summary>
    /// The place of the choice among <paramref name="count"/>, the best first: 0, or with <paramref name="mistakePercent"/> chance a random
    /// one of the <see cref="Top"/>. Draws from <paramref name="random"/> only when there are two choices or more.
    /// </summary>
    public static int Pick(int count, int mistakePercent, DeterministicRandom random)
    {
        if (count < 2)
        {
            return 0;
        }

        return random.NextInt(100) < mistakePercent ? random.NextInt(IntMath.Min(Top, count)) : 0;
    }
}
