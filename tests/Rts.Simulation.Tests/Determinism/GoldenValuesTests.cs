using Rts.Simulation.Hashing;
using Rts.Simulation.Math;
using Rts.Simulation.Random;
using Rts.Simulation.Replays;

namespace Rts.Simulation.Tests.Determinism;

/// <summary>
/// Golden values: numbers captured once on macOS (Apple Silicon) and frozen. Every platform in CI
/// must reproduce them bit for bit — that is the cross-platform determinism check.
///
/// Rule: a golden value changes only together with a deliberate rules change. Bump
/// <see cref="SimulationVersion.Current"/>, update the value in the same commit and explain why.
/// A golden value that changes "by itself" is a determinism bug, not a test to fix.
/// </summary>
public sealed class GoldenValuesTests
{
    private const int ScenarioTicks = 1000;

    [Fact]
    public void Random_generator_sequence()
    {
        var random = new DeterministicRandom(seed: 42);

        var actual = Enumerable.Range(0, 10).Select(_ => random.NextUInt()).ToArray();

        uint[] expected =
        [
            565663470, 3244226384, 2504567229, 903561869, 4026996297,
            2722332799, 3032858066, 272411090, 1181909318, 20290832,
        ];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Fixed_point_operations()
    {
        static Fixed F(long value) => Fixed.FromInt(value);
        static Fixed R(long numerator, long denominator) => Fixed.FromRatio(numerator, denominator);

        long[] actual =
        [
            (F(3) / F(7)).Raw,
            (F(-3) / F(7)).Raw,
            (F(1) / F(-9)).Raw,
            (F(10) / R(3, 4)).Raw,
            (R(1, 3) * F(3)).Raw,
            (R(-5, 7) * R(3, 11)).Raw,
            (R(22, 7) * R(22, 7)).Raw,
            (R(123_456, 1000) * R(-789, 1000)).Raw,
            (R(-1, 3) * R(-1, 3)).Raw,
            (F(1_000_000) * R(1, 1_000_000)).Raw,
            R(355, 113).Raw,
            R(-1, 7).Raw,
            R(2, 3).Raw,
            R(-99_999, 100_000).Raw,
            Fixed.Sqrt(F(2)).Raw,
            Fixed.Sqrt(F(3)).Raw,
            Fixed.Sqrt(R(1, 10)).Raw,
            Fixed.Sqrt(F(1_000_000)).Raw,
            Fixed.Sqrt(R(7, 3)).Raw,
            (F(5) * 7 / 3).Raw,
        ];

        long[] expected =
        [
            28086, -28086, -7281, 873813, 65535,
            -12767, 647333, -6383540, 7281,
            0, // 1/1,000,000 is below the 1/65536 step, so it is stored as 0
            205887, -9362, 43690, -65535,
            92681, 113511, 20723, 65536000, 100107,
            764586,
        ];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Trigonometry_tables_and_samples()
    {
        var tables = new Fnv1a64();
        foreach (var value in TrigTables.SinQuarter)
        {
            tables.Add(value);
        }

        foreach (var value in TrigTables.Atan)
        {
            tables.Add((int)value);
        }

        long[] actual =
        [
            FixedMath.Sin(BAngle.FromUnits(1)).Raw,
            FixedMath.Sin(BAngle.FromUnits(5461)).Raw,
            FixedMath.Sin(BAngle.FromUnits(12345)).Raw,
            FixedMath.Cos(BAngle.FromUnits(40000)).Raw,
            FixedMath.Sin(BAngle.FromUnits(65535)).Raw,
            FixedMath.Angle(3, -7).Value,
            FixedMath.Angle(256, 1).Value,
            FixedMath.Angle(-12_345, 6_789).Value,
            FixedMath.Angle(-1, -51_200).Value,
            FixedMath.Angle(40_000, 39_999).Value,
        ];

        long[] expected =
        [
            6, 32767, 60684, -50404, -6,
            4223, 16425, 43908,
            0, // 359.99…° rounds up to a full turn, which is north
            24576,
        ];
        Assert.Equal(140777579128644704UL, tables.Value);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Reference_scenario_checksums()
    {
        var checksums = ReferenceScenario.Run(ReferenceScenario.Commands(ScenarioTicks), ScenarioTicks);

        StateChecksum[] actual = [checksums[99], checksums[499], checksums[999]];

        StateChecksum[] expected =
        [
            new(100, 0xae487f7644eafb1bUL),
            new(500, 0xce1bc327d136e200UL),
            new(1000, 0xe221143de17e79e4UL),
        ];
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Reference_scenario_replay_bytes()
    {
        var live = new GameSimulation(ReferenceScenario.Settings());
        var recorder = new ReplayRecorder(live);
        foreach (var command in ReferenceScenario.Commands(ScenarioTicks))
        {
            live.Enqueue(command);
        }

        for (var i = 0; i < ScenarioTicks; i++)
        {
            live.Step();
            recorder.AfterStep();
        }

        var bytes = ReplayFormat.Write(recorder.Build());
        var hash = new Fnv1a64();
        hash.Add(bytes);

        Assert.Equal((22124, 5244884938992613639UL), (bytes.Length, hash.Value));
    }
}
