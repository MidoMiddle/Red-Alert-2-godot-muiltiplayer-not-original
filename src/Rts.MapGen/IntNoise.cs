namespace Rts.MapGen;

/// <summary>
/// Integer value noise (docs/implementation/M7_maps.md, 4.14): values on a lattice from a hash of the lattice point, the seed and
/// the octave; between them a smoothstep blend in integers. Three octaves of cell sizes 16, 8 and 4, weighted 4 : 2 : 1. Values
/// 0…<see cref="Max"/>. No floating point: the same on every system.
/// </summary>
public static class IntNoise
{
    public const int Max = 65535;

    private static readonly (int Cell, int Weight)[] Octaves = [(16, 4), (8, 2), (4, 1)];

    /// <summary>The noise at point (<paramref name="x"/>, <paramref name="y"/>) for <paramref name="seed"/> and a channel (one per use).</summary>
    public static int At(int x, int y, uint seed, int channel)
    {
        long sum = 0;
        long weights = 0;
        for (var octave = 0; octave < Octaves.Length; octave++)
        {
            var (cell, weight) = Octaves[octave];
            sum += Octave(x, y, cell, seed, channel * 8 + octave) * weight;
            weights += weight;
        }

        return (int)(sum / weights);
    }

    /// <summary>A hash of the integers, spread over all 32 bits: the same for the same inputs everywhere.</summary>
    public static uint Hash(int x, int y, uint seed, int channel)
    {
        var h = seed * 0x9E3779B1u;
        h ^= (uint)x * 0x85EBCA6Bu;
        h = Rotate(h, 13) * 5u + 0xE6546B64u;
        h ^= (uint)y * 0xC2B2AE35u;
        h = Rotate(h, 13) * 5u + 0xE6546B64u;
        h ^= (uint)channel * 0x27D4EB2Fu;
        h ^= h >> 16;
        h *= 0x85EBCA6Bu;
        h ^= h >> 13;
        h *= 0xC2B2AE35u;
        h ^= h >> 16;
        return h;
    }

    private static int Octave(int x, int y, int cell, uint seed, int channel)
    {
        var (ix, iy) = (FloorDiv(x, cell), FloorDiv(y, cell));
        var tx = Smooth((x - ix * cell) * 65536L / cell);
        var ty = Smooth((y - iy * cell) * 65536L / cell);
        var top = Lerp(Node(ix, iy, seed, channel), Node(ix + 1, iy, seed, channel), tx);
        var bottom = Lerp(Node(ix, iy + 1, seed, channel), Node(ix + 1, iy + 1, seed, channel), tx);
        return (int)Lerp(top, bottom, ty);
    }

    private static long Node(int x, int y, uint seed, int channel) => Hash(x, y, seed, channel) & Max;

    // t in 0…65536 → 3t² − 2t³, also in 0…65536.
    private static long Smooth(long t) => t * t / 65536 * (3 * 65536 - 2 * t) / 65536;

    private static long Lerp(long a, long b, long t) => a + (b - a) * t / 65536;

    private static int FloorDiv(int value, int divisor) => value >= 0 ? value / divisor : -((-value + divisor - 1) / divisor);

    private static uint Rotate(uint value, int bits) => (value << bits) | (value >> (32 - bits));
}
