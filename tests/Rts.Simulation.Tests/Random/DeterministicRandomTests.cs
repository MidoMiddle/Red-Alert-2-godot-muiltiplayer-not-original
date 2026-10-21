using Rts.Simulation.Math;
using Rts.Simulation.Random;

namespace Rts.Simulation.Tests.Random;

public sealed class DeterministicRandomTests
{
    [Fact]
    public void Matches_PCG32_reference_vector()
    {
        // Output of pcg32-demo from the reference implementation: pcg32_srandom_r(&rng, 42u, 54u).
        uint[] expected = [0xa15c02b7, 0x7b47f409, 0xba1d3330, 0x83d2f293, 0xbfa4784b, 0xcbed606e];
        var random = new DeterministicRandom(seed: 42, stream: 54);

        var actual = expected.Select(_ => random.NextUInt()).ToArray();

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Same_seed_and_stream_give_the_same_sequence()
    {
        var a = new DeterministicRandom(seed: 2026, stream: 7);
        var b = new DeterministicRandom(seed: 2026, stream: 7);

        for (var i = 0; i < 10_000; i++)
        {
            Assert.Equal(a.NextUInt(), b.NextUInt());
        }
    }

    [Fact]
    public void Different_seed_or_stream_give_different_sequences()
    {
        var baseline = Take(new DeterministicRandom(seed: 1, stream: 1), 16);

        Assert.NotEqual(baseline, Take(new DeterministicRandom(seed: 2, stream: 1), 16));
        Assert.NotEqual(baseline, Take(new DeterministicRandom(seed: 1, stream: 2), 16));
    }

    [Fact]
    public void NextInt_is_in_range_and_evenly_distributed()
    {
        const int faces = 6;
        const int draws = 60_000;
        var random = new DeterministicRandom(seed: 99);
        var counts = new int[faces];

        for (var i = 0; i < draws; i++)
        {
            var value = random.NextInt(faces);
            Assert.InRange(value, 0, faces - 1);
            counts[value]++;
        }

        const int expected = draws / faces;
        Assert.All(counts, count => Assert.InRange(count, expected * 95 / 100, expected * 105 / 100));
    }

    [Theory]
    [InlineData(-10, 10)]
    [InlineData(5, 6)]
    [InlineData(int.MinValue, int.MaxValue)]
    [InlineData(int.MinValue, int.MinValue + 3)]
    [InlineData(int.MaxValue - 3, int.MaxValue)]
    public void NextInt_with_min_stays_in_range(int min, int maxExclusive)
    {
        var random = new DeterministicRandom(seed: 5);

        for (var i = 0; i < 10_000; i++)
        {
            var value = random.NextInt(min, maxExclusive);
            Assert.True(value >= min && value < maxExclusive, $"{value} is outside [{min}, {maxExclusive})");
        }
    }

    [Fact]
    public void NextInt_rejects_empty_ranges()
    {
        var random = new DeterministicRandom(seed: 1);

        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(-5));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(5, 5));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.NextInt(5, 4));
    }

    [Fact]
    public void NextFixed_is_in_unit_interval()
    {
        var random = new DeterministicRandom(seed: 3);

        for (var i = 0; i < 10_000; i++)
        {
            var value = random.NextFixed();
            Assert.True(value >= Fixed.Zero && value < Fixed.One, $"{value} is outside [0, 1)");
        }
    }

    [Fact]
    public void NextBool_returns_both_values()
    {
        var random = new DeterministicRandom(seed: 4);
        var trues = Enumerable.Range(0, 1000).Count(_ => random.NextBool());

        Assert.InRange(trues, 400, 600);
    }

    [Fact]
    public void Restored_state_continues_the_same_sequence()
    {
        var original = new DeterministicRandom(seed: 77, stream: 3);
        Take(original, 1000);

        var restored = DeterministicRandom.FromState(original.State);

        Assert.Equal(original.State, restored.State);
        Assert.Equal(Take(original, 1000), Take(restored, 1000));
    }

    [Fact]
    public void FromState_rejects_even_increment()
    {
        Assert.Throws<ArgumentException>(() => DeterministicRandom.FromState(new RandomState(1, 2)));
    }

    private static uint[] Take(DeterministicRandom random, int count) =>
        Enumerable.Range(0, count).Select(_ => random.NextUInt()).ToArray();
}
