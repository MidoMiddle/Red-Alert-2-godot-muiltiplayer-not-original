using Rts.Simulation;

namespace Rts.Data.Tests;

/// <summary>
/// The number reader of data files (docs/implementation/M3_base_economy.md, 4.2). The golden values are exact raw numbers, the same
/// on every platform: nothing in the reader is a floating-point number, so the checks in CI on three systems compare equal bits.
/// </summary>
public sealed class FixedParserTests
{
    [Theory]
    [InlineData("0", 0L)]
    [InlineData("-0", 0L)]
    [InlineData("30", 1966080L)]
    [InlineData("-30", -1966080L)]
    [InlineData("1.50", 98304L)]
    [InlineData("0.1", 6554L)]
    [InlineData("1.5", 98304L)]
    [InlineData("-2.25", -147456L)]
    [InlineData("0.000001", 0L)]
    [InlineData("0.000007", 0L)]
    [InlineData("0.000008", 1L)]
    [InlineData("0.000015", 1L)]
    [InlineData("0.999999", 65536L)]
    [InlineData("1.000001", 65536L)]
    [InlineData("33.333333", 2184533L)]
    [InlineData("99.99", 6552945L)]
    [InlineData("140737488355327.9999", 9223372036854775801L)]
    [InlineData("-140737488355328", long.MinValue)]
    public void Golden_raw_values_are_exact(string text, long raw)
    {
        Assert.True(FixedParser.TryParseFixed(text, out var value));
        Assert.Equal(raw, value.Raw);
    }

    [Theory]
    [InlineData("0%", 0L)]
    [InlineData("0.001%", 1L)]
    [InlineData("0.5%", 328L)]
    [InlineData("100%", 65536L)]
    [InlineData("125%", 81920L)]
    [InlineData("125.5%", 82248L)]
    public void A_percentage_is_the_number_divided_by_100(string text, long raw)
    {
        Assert.True(FixedParser.TryParsePercent(text, out var value));
        Assert.Equal(raw, value.Raw);
    }

    [Theory]
    [InlineData("0s", 0)]
    [InlineData("0.000001s", 0)]
    [InlineData("0.1s", 2)]
    [InlineData("0.5s", 8)]
    [InlineData("1.5s", 23)]
    [InlineData("2s", 30)]
    [InlineData("3.3s", 50)]
    [InlineData("12s", 180)]
    public void Seconds_become_ticks_at_the_simulation_rate(string text, int ticks)
    {
        Assert.True(FixedParser.TryParseTicks(text, SimulationSettings.DefaultTicksPerSecond, out var result));
        Assert.Equal(ticks, result);
    }

    [Fact]
    public void The_tick_rate_is_a_parameter_and_halves_round_away_from_zero()
    {
        Assert.True(FixedParser.TryParseTicks("1.5s", 25, out var ticks));
        Assert.Equal(38, ticks);
        Assert.True(FixedParser.TryParseTicks("0.1s", 8, out ticks));
        Assert.Equal(1, ticks);
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("30", 30)]
    [InlineData("-5", -5)]
    [InlineData("2147483647", int.MaxValue)]
    public void Whole_numbers_are_read_as_they_are(string text, int expected)
    {
        Assert.True(FixedParser.TryParseInt(text, out var value));
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("1,5")]
    [InlineData("abc")]
    [InlineData("1e3")]
    [InlineData(".5")]
    [InlineData("5.")]
    [InlineData("+5")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("1.1234567")]
    [InlineData("1.2.3")]
    [InlineData("-")]
    [InlineData("")]
    [InlineData("0x10")]
    [InlineData("1_000")]
    [InlineData("1.5s")]
    [InlineData("125%")]
    [InlineData("١")]
    public void A_comma_an_exponent_or_a_missing_digit_is_an_error(string text)
    {
        Assert.False(FixedParser.TryParseFixed(text, out _));
    }

    [Theory]
    [InlineData("140737488355327.999999")]
    [InlineData("140737488355328")]
    [InlineData("999999999999999999999999999999")]
    [InlineData("1234567890123456789012345678901")]
    public void A_number_beyond_the_range_of_the_raw_value_is_an_error(string text)
    {
        Assert.False(FixedParser.TryParseFixed(text, out _));
    }

    [Theory]
    [InlineData("125")]
    [InlineData("%")]
    [InlineData("-50%")]
    [InlineData("1,5%")]
    [InlineData("125 %")]
    [InlineData("1.5s")]
    public void A_percentage_needs_its_percent_sign_and_no_minus(string text)
    {
        Assert.False(FixedParser.TryParsePercent(text, out _));
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("s")]
    [InlineData("-1s")]
    [InlineData("1,5s")]
    [InlineData("2 s")]
    [InlineData("2S")]
    [InlineData("2m")]
    public void A_duration_needs_its_unit_and_no_minus(string text)
    {
        Assert.False(FixedParser.TryParseTicks(text, SimulationSettings.DefaultTicksPerSecond, out _));
    }

    [Theory]
    [InlineData("1.5")]
    [InlineData("1.0")]
    [InlineData("1e3")]
    [InlineData("+5")]
    [InlineData("5s")]
    [InlineData("0x10")]
    [InlineData("2147483648")]
    public void A_whole_number_has_no_point_no_unit_and_fits_an_int(string text)
    {
        Assert.False(FixedParser.TryParseInt(text, out _));
    }

    [Fact]
    public void A_tick_rate_must_be_positive()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => FixedParser.TryParseTicks("1s", 0, out _));
    }
}
