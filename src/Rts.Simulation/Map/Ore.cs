namespace Rts.Simulation.Map;

/// <summary>What lies on a cell to be harvested (docs/implementation/M3_base_economy.md, 4.12). Values go into replays: never renumber.</summary>
public enum OreKind : byte
{
    None = 0,
    Ore = 1,
    Gems = 2,
}

/// <summary>
/// The ore of one cell: its kind and its stage, 0…<see cref="MaxStage"/> (12 stages, as RA2). A stage is worth the <c>ore_value</c> or
/// <c>gem_value</c> of the data, so a cell holds <c>(stage + 1) × value</c> credits.
/// </summary>
public readonly record struct OreCell(OreKind Kind, int Stage)
{
    public const int MaxStage = 11;

    public static readonly OreCell Empty = new(OreKind.None, 0);

    public bool IsEmpty => Kind == OreKind.None;

    /// <summary>The replay layout: kind × 16 + stage, 0 for no ore.</summary>
    public byte Packed => IsEmpty ? (byte)0 : (byte)(((int)Kind << 4) | Stage);

    public static OreCell FromPacked(byte packed) => packed == 0 ? Empty : new((OreKind)(packed >> 4), packed & 0xF);

    /// <summary>True for a byte <see cref="FromPacked"/> takes: 0, or ore or gems at a stage of 0…11.</summary>
    public static bool IsValidPacked(byte packed) =>
        packed == 0 || ((packed >> 4) is (int)OreKind.Ore or (int)OreKind.Gems && (packed & 0xF) <= MaxStage);
}
