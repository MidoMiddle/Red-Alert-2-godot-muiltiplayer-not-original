namespace Rts.Simulation.Tests;

/// <summary>
/// Small seeded generator (SplitMix64) for property tests, so tests never depend on System.Random.
/// Not the simulation's generator — that one is DeterministicRandom.
/// </summary>
internal sealed class TestRandom(ulong seed)
{
    private ulong _state = seed;

    public ulong NextULong()
    {
        _state += 0x9E3779B97F4A7C15UL;
        var z = _state;
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }

    public long NextLong() => (long)NextULong();

    /// <summary>Uniform-enough value in [0, maxExclusive) for tests.</summary>
    public int NextInt(int maxExclusive) => (int)(NextULong() % (ulong)maxExclusive);
}
