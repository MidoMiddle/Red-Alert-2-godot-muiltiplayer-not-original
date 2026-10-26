using System.Numerics;
using Rts.Client.Terrain;
using Rts.Simulation.Geometry;
using Rts.Simulation.Tests.Map;
using Rts.Simulation.World;

namespace Rts.Simulation.Tests.Client;

/// <summary>A click on a high bridge lands on its deck (docs/implementation/M7_maps.md, 4.6), not on the valley behind it.</summary>
public sealed class DeckPickingTests
{
    private static readonly Vector3 Down = Vector3.Normalize(new Vector3(-0.6f, -1f, -0.6f));

    private static readonly float Deck = 4 * TerrainHeights.LevelHeight;

    // The camera's point for a ray that comes to (x, y, z).
    private static Vector3 Above(float x, float z, float y = 0f) => new Vector3(x, y, z) - Down * 20f;

    [Fact]
    public void A_ray_meets_the_deck_at_its_level_before_the_ground_below()
    {
        var map = HighBridgeTests.Gorge();
        var origin = Above(8.4f, 5.5f, Deck);

        Assert.True(DeckPicking.TryHit(map, origin, Down, 100f, out var hit));
        Assert.Equal(Deck, hit.Y, 3);
        Assert.Equal(new CellPos(8, 5), new CellPos((int)hit.X, (int)hit.Z));

        // Beside the bridge the ray goes down to the valley; the ground in front of the deck wins.
        Assert.False(DeckPicking.TryHit(map, Above(8.4f, 1.5f), Down, 100f, out _));
        Assert.False(DeckPicking.TryHit(map, origin, Down, 1f, out _));
        Assert.False(DeckPicking.TryHit(HighBridgeTests.Gorge(bridge: false), origin, Down, 100f, out _));
    }

    [Fact]
    public void A_fallen_segment_is_no_deck_to_click()
    {
        var world = new GameWorld(seed: 1, HighBridgeTests.Gorge(), TestRules.Standard);
        world.DestroyBridgeSegment(world.Map.BridgeAt(new CellPos(8, 5))!, null);

        Assert.False(DeckPicking.TryHit(world.Map, Above(8.4f, 5.5f, Deck), Down, 100f, out _));
        Assert.True(DeckPicking.TryHit(world.Map, Above(10.4f, 5.5f, Deck), Down, 100f, out _));
    }
}
