using System.Collections.Generic;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>One crystal of a cell of ore: where it stands inside the cell (0 to 1 from the north-west corner) and how large it is.</summary>
public readonly record struct Crystal(float X, float Z, float Size);

/// <summary>
/// How the ore of a cell is laid out (docs/implementation/M3_base_economy.md, 4.17): one to four crystals, more and larger the higher
/// the stage (0 to <see cref="OreCell.MaxStage"/>), gems larger than ore. Where they stand inside the cell follows from the cell
/// alone, so the field looks the same every time it is drawn again.
/// </summary>
public static class OreCrystals
{
    public const int MaxCrystals = 4;

    // The four places of crystals in a cell; a cell turns them by its own number so neighbours differ.
    private static readonly (float X, float Z)[] Spots = [(0.5f, 0.5f), (0.25f, 0.3f), (0.72f, 0.28f), (0.3f, 0.74f)];

    /// <summary>How many crystals a stage shows: one at the start, four from the top third on.</summary>
    public static int CountOf(int stage) => 1 + stage * MaxCrystals / (OreCell.MaxStage + 1);

    /// <summary>
    /// The size of the cluster model of a cell (one cluster a cell): from <see cref="SmallestCluster"/> at stage 0 to full size at
    /// <see cref="OreCell.MaxStage"/>, so a field visibly grows and thins as it is harvested.
    /// </summary>
    public static float ClusterScale(OreCell cell) =>
        cell.IsEmpty ? 0f : SmallestCluster + (1f - SmallestCluster) * cell.Stage / OreCell.MaxStage;

    /// <summary>How far the cluster of a cell is turned, in quarter turns and eighths of one (0 to 2π), from the cell alone.</summary>
    public static float ClusterTurn(int x, int y)
    {
        var hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        return (hash % 32) * (System.MathF.PI / 16f);
    }

    /// <summary>The cluster of stage 0 against the full one.</summary>
    public const float SmallestCluster = 0.45f;

    public static IReadOnlyList<Crystal> Of(int x, int y, OreCell cell)
    {
        if (cell.IsEmpty)
        {
            return [];
        }

        var gem = cell.Kind == OreKind.Gems;
        var count = CountOf(cell.Stage);
        var size = (gem ? 0.16f : 0.11f) + cell.Stage * (gem ? 0.012f : 0.01f);
        var hash = (uint)(x * 73856093) ^ (uint)(y * 19349663);
        var crystals = new List<Crystal>(count);
        for (var i = 0; i < count; i++)
        {
            var (spotX, spotZ) = Spots[i];
            // A small shift of each crystal, from the cell and the crystal's number: in 0 to 0.06 of a cell.
            var shift = ((hash >> (i * 4)) & 0xF) / 15f * 0.06f;
            var flip = (hash & 1) == 0;
            crystals.Add(new Crystal(flip ? spotX + shift : 1f - spotX - shift, spotZ + shift, i == 0 ? size : size * 0.8f));
        }

        return crystals;
    }
}
