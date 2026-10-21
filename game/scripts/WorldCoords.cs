using Godot;
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

    public static Vector3 ToGodot(WorldPos position, float height = 0f) =>
        new(position.X * UnitsPerLepton, height, position.Y * UnitsPerLepton);

    public static WorldPos FromGodot(Vector3 point) =>
        new(Mathf.RoundToInt(point.X * WorldPos.LeptonsPerCell), Mathf.RoundToInt(point.Z * WorldPos.LeptonsPerCell));
}
