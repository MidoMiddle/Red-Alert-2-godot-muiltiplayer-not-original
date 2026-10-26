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

    /// <summary>The map <see cref="Terrain"/> shows, for the decks of its high bridges; null with no terrain. Set by <see cref="TerrainBuilder"/>.</summary>
    public static Rts.Simulation.Map.MapGrid? Map { get; set; }

    public static Vector3 ToGodot(WorldPos position, float height = 0f) =>
        new(position.X * UnitsPerLepton, height, position.Y * UnitsPerLepton);

    /// <summary>The point on the terrain surface above <paramref name="position"/>, raised by <paramref name="lift"/>.</summary>
    public static Vector3 ToGodotOnGround(WorldPos position, float lift = 0f)
    {
        var x = position.X * UnitsPerLepton;
        var z = position.Y * UnitsPerLepton;
        return new Vector3(x, (Terrain?.HeightAt(x, z) ?? 0f) + lift, z);
    }

    /// <summary>
    /// The point a unit is drawn at above <paramref name="position"/>, raised by <paramref name="lift"/>: on the deck of a high bridge
    /// for a unit up there (<see cref="Unit.OnDeck"/>), else on the terrain.
    /// </summary>
    public static Vector3 ToGodotOnUnit(Rts.Simulation.World.Unit unit, WorldPos position, float lift = 0f) =>
        DeckHeight(position, unit.OnDeck) is { } deck ? ToGodot(position, deck + lift) : ToGodotOnGround(position, lift);

    /// <summary>
    /// The height of the deck over <paramref name="position"/> for one on it, or null for the terrain. An end cell of a deck always
    /// counts as the deck: from the ground nobody stands on it (a cliff), so the pose of a unit that just drove off stays level.
    /// </summary>
    public static float? DeckHeight(WorldPos position, bool onDeck)
    {
        if (Map is not { DeckCount: > 0 } map || map.DeckSlot(position.ToCell()) is not (var slot and >= 0) || (!onDeck && map.DeckExit(slot) < 0))
        {
            return null;
        }

        return map.DeckLevel(slot) * TerrainHeights.LevelHeight;
    }

    public static WorldPos FromGodot(Vector3 point) =>
        new(Mathf.RoundToInt(point.X * WorldPos.LeptonsPerCell), Mathf.RoundToInt(point.Z * WorldPos.LeptonsPerCell));
}
