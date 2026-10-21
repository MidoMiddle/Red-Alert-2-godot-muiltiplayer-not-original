using System;

namespace Rts.Simulation.Hashing;

/// <summary>
/// FNV-1a 64-bit hash accumulator. Multi-byte numbers are fed little-endian by explicit shifts,
/// so the result never depends on the CPU byte order.
/// </summary>
public sealed class Fnv1a64
{
    private const ulong OffsetBasis = 14695981039346656037UL;
    private const ulong Prime = 1099511628211UL;

    public ulong Value { get; private set; } = OffsetBasis;

    public void Add(byte value) => Value = unchecked((Value ^ value) * Prime);

    public void Add(ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            Add(value);
        }
    }

    public void Add(bool value) => Add(value ? (byte)1 : (byte)0);

    public void Add(int value) => Add(unchecked((uint)value));

    public void Add(uint value)
    {
        Add((byte)value);
        Add((byte)(value >> 8));
        Add((byte)(value >> 16));
        Add((byte)(value >> 24));
    }

    public void Add(long value) => Add(unchecked((ulong)value));

    public void Add(ulong value)
    {
        Add((uint)value);
        Add((uint)(value >> 32));
    }
}
