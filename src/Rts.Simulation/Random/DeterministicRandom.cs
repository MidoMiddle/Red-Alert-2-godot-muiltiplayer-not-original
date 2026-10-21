using System;
using System.Numerics;
using Rts.Simulation.Math;

namespace Rts.Simulation.Random;

/// <summary>
/// PCG32 (XSH-RR) random number generator, bit-for-bit identical to the reference
/// implementation (<c>pcg32_random_r</c> / <c>pcg32_srandom_r</c> from pcg-random.org).
/// The sequence is defined by the algorithm, not by .NET, so every platform and runtime version agrees.
/// One instance per simulation; visual effects in the client use their own generator.
/// Not thread-safe: the simulation is single-threaded.
/// </summary>
public sealed class DeterministicRandom
{
    private const ulong Multiplier = 6364136223846793005UL;

    private ulong _state;
    private readonly ulong _increment;

    /// <param name="seed">Starting point of the sequence.</param>
    /// <param name="stream">
    /// Selects one of 2^63 independent sequences for the same seed
    /// (for example, one stream per bot player).
    /// </param>
    public DeterministicRandom(ulong seed, ulong stream = 0)
    {
        _state = 0;
        _increment = (stream << 1) | 1;
        NextUInt();
        _state = unchecked(_state + seed);
        NextUInt();
    }

    private DeterministicRandom(RandomState state)
    {
        if ((state.Increment & 1) == 0)
        {
            throw new ArgumentException("PCG increment must be odd.", nameof(state));
        }

        _state = state.State;
        _increment = state.Increment;
    }

    /// <summary>Current state, for hashing and for resuming via <see cref="FromState"/>.</summary>
    public RandomState State => new(_state, _increment);

    /// <summary>Generator that continues exactly where the captured one was.</summary>
    public static DeterministicRandom FromState(RandomState state) => new(state);

    public uint NextUInt()
    {
        var oldState = _state;
        _state = unchecked(oldState * Multiplier + _increment);
        var xorShifted = (uint)(((oldState >> 18) ^ oldState) >> 27);
        var rotation = (int)(oldState >> 59);
        return BitOperations.RotateRight(xorShifted, rotation);
    }

    /// <summary>Uniform value in [0, <paramref name="maxExclusive"/>), without modulo bias.</summary>
    public int NextInt(int maxExclusive)
    {
        if (maxExclusive <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, "Must be positive.");
        }

        return (int)NextBounded((uint)maxExclusive);
    }

    /// <summary>Uniform value in [<paramref name="min"/>, <paramref name="maxExclusive"/>), without modulo bias.</summary>
    public int NextInt(int min, int maxExclusive)
    {
        if (maxExclusive <= min)
        {
            throw new ArgumentOutOfRangeException(nameof(maxExclusive), maxExclusive, $"Must be greater than min ({min}).");
        }

        var range = (uint)((long)maxExclusive - min);
        return (int)(min + (long)NextBounded(range));
    }

    public bool NextBool() => (NextUInt() >> 31) != 0;

    /// <summary>Uniform value in [0, 1) with the full 16-bit fractional precision of <see cref="Fixed"/>.</summary>
    public Fixed NextFixed() => Fixed.FromRaw(NextUInt() >> (32 - Fixed.FractionalBits));

    // pcg32_boundedrand_r: reject the lowest (2^32 mod bound) values so every result is equally likely.
    private uint NextBounded(uint bound)
    {
        var threshold = unchecked(0u - bound) % bound;
        while (true)
        {
            var value = NextUInt();
            if (value >= threshold)
            {
                return value % bound;
            }
        }
    }
}
