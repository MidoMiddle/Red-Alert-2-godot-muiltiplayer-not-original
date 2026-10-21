using Godot;
using Rts.Client.Terrain;
using Rts.Simulation.Geometry;

namespace Rts.Client;

/// <summary>
/// Conversion between simulation leptons and Godot space: 1 cell = 1 Godot unit,
/// world X → Godot X, world Y → Godot Z, Godot Y is up.
/// Float is fine here: commands carry the rounded integer leptons, so every peer sees the same numbers.
/// </summary>
public static class WorldCoords
{
    public const float UnitsPerLepton = 1f / WorldPos.LeptonsPerCell;

    /// <summary>The visual terrain of the current scene, or null on flat ground. Set by <see cref="TerrainBuilder"/>.</summary>
    public static TerrainHeights? Terrain { get; set; }

    public static Vector3 ToGodot(WorldPos position, float height = 0f) =>
        new(position.X * UnitsPerLepton, height, position.Y * UnitsPerLepton);

    /// <summary>The point on the terrain surface above <paramref name="position"/>, raised by <paramref name="lift"/>.</summary>
    public static Vector3 ToGodotOnGround(WorldPos position, float lift = 0f)
    {
        var x = position.X * UnitsPerLepton;
        var z = position.Y * UnitsPerLepton;
        return new Vector3(x, (Terrain?.HeightAt(x, z) ?? 0f) + lift, z);
    }

    public static WorldPos FromGodot(Vector3 point) =>
        new(Mathf.RoundToInt(point.X * WorldPos.LeptonsPerCell), Mathf.RoundToInt(point.Z * WorldPos.LeptonsPerCell));
}
