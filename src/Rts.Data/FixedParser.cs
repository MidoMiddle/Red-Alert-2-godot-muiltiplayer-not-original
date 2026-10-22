using System;
using Rts.Simulation.Math;

namespace Rts.Data;

/// <summary>
/// Reads the numbers of data files exactly, with no <c>float</c> or <c>double</c> on the way (docs/implementation/M3_base_economy.md,
/// 4.2). Each value is a rational number rounded once, to the nearest whole number, halves away from zero. The forms are strict:
/// a comma, an exponent, a plus sign or a missing digit is an error, not a guess.
/// </summary>
public static class FixedParser
{
    /// <summary>Digits in all. Keeps every exact product below the range of <see cref="Int128"/>.</summary>
    private const int MaxDigits = 30;

    /// <summary>Fraction digits. A longer fraction would need a rounding the data does not ask for.</summary>
    private const int MaxFractionDigits = 6;

    /// <summary>
    /// A number with an optional minus sign: <c>1.25</c>, <c>-2.25</c>, <c>30</c>. The raw value is the number times 65 536 and must
    /// fit a <c>long</c>.
    /// </summary>
    public static bool TryParseFixed(string text, out Fixed value)
    {
        value = default;
        if (!TryReadDecimal(text, allowSign: true, out var negative, out var digits, out var fractionDigits))
        {
            return false;
        }

        var magnitude = RoundHalfUp(digits * Fixed.OneRaw, Power10(fractionDigits));
        var raw = negative ? -magnitude : magnitude;
        if (raw < long.MinValue || raw > long.MaxValue)
        {
            return false;
        }

        value = Fixed.FromRaw((long)raw);
        return true;
    }

    /// <summary>
    /// A percentage: <c>125%</c> is 1.25, <c>125.5%</c> is 1.255. Percentages are not negative. The number is divided by 100 once,
    /// in the same rounding as <see cref="TryParseFixed"/>.
    /// </summary>
    public static bool TryParsePercent(string text, out Fixed value)
    {
        value = default;
        if (!text.EndsWith('%') || !TryReadDecimal(text[..^1], allowSign: false, out _, out var digits, out var fractionDigits))
        {
            return false;
        }

        var raw = RoundHalfUp(digits * Fixed.OneRaw, 100 * Power10(fractionDigits));
        if (raw > long.MaxValue)
        {
            return false;
        }

        value = Fixed.FromRaw((long)raw);
        return true;
    }

    /// <summary>
    /// A duration in seconds, <c>2s</c> or <c>1.5s</c>, as whole ticks: the seconds times <paramref name="ticksPerSecond"/>, rounded.
    /// Durations are not negative.
    /// </summary>
    public static bool TryParseTicks(string text, int ticksPerSecond, out int ticks)
    {
        if (ticksPerSecond <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ticksPerSecond), ticksPerSecond, "Ticks per second must be positive.");
        }

        ticks = 0;
        if (!text.EndsWith('s') || !TryReadDecimal(text[..^1], allowSign: false, out _, out var digits, out var fractionDigits))
        {
            return false;
        }

        var result = RoundHalfUp(digits * ticksPerSecond, Power10(fractionDigits));
        if (result > int.MaxValue)
        {
            return false;
        }

        ticks = (int)result;
        return true;
    }

    /// <summary>A whole number with an optional minus sign: <c>30</c>, <c>-5</c>. A fraction is an error, even <c>1.0</c>.</summary>
    public static bool TryParseInt(string text, out int value)
    {
        value = 0;
        if (!TryReadDecimal(text, allowSign: true, out var negative, out var digits, out var fractionDigits) || fractionDigits > 0)
        {
            return false;
        }

        var signed = negative ? -digits : digits;
        if (signed < int.MinValue || signed > int.MaxValue)
        {
            return false;
        }

        value = (int)signed;
        return true;
    }

    /// <summary>
    /// Reads <c>[-]digits[.digits]</c> and nothing else: at least one digit before the point, one to six after it, at most
    /// <see cref="MaxDigits"/> digits in all. The digits come out as one exact integer; the caller scales it.
    /// </summary>
    private static bool TryReadDecimal(string text, bool allowSign, out bool negative, out Int128 digits, out int fractionDigits)
    {
        negative = allowSign && text.StartsWith('-');
        var body = negative ? text[1..] : text;
        var point = body.IndexOf('.');
        var integer = point < 0 ? body : body[..point];
        var fraction = point < 0 ? "" : body[(point + 1)..];

        digits = 0;
        fractionDigits = 0;
        if (!AreDigits(integer)
            || (point >= 0 && (!AreDigits(fraction) || fraction.Length > MaxFractionDigits))
            || integer.Length + fraction.Length > MaxDigits)
        {
            return false;
        }

        foreach (var c in integer)
        {
            digits = digits * 10 + (c - '0');
        }

        foreach (var c in fraction)
        {
            digits = digits * 10 + (c - '0');
        }

        fractionDigits = fraction.Length;
        return true;
    }

    /// <summary>One or more ASCII digits and nothing else; <see cref="char.IsDigit(char)"/> would accept other scripts.</summary>
    private static bool AreDigits(string text)
    {
        if (text.Length == 0)
        {
            return false;
        }

        foreach (var c in text)
        {
            if (c is < '0' or > '9')
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>The nearest whole number to <paramref name="numerator"/> / <paramref name="denominator"/>, halves up. For
    /// non-negative values halves up is halves away from zero.</summary>
    private static Int128 RoundHalfUp(Int128 numerator, Int128 denominator) => (2 * numerator + denominator) / (2 * denominator);

    private static Int128 Power10(int exponent)
    {
        Int128 power = 1;
        for (var i = 0; i < exponent; i++)
        {
            power *= 10;
        }

        return power;
    }
}
