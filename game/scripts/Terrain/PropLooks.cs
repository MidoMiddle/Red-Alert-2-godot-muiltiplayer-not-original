using System.Collections.Generic;

namespace Rts.Client.Terrain;

/// <summary>
/// The placeholder looks of the small map objects of the element documents (2026-10-25: «add a primitive for every object of the
/// documents»), in the civilian colours: a part list of one cell, its centre at (0, 0) on the ground, as <see cref="BuildingLooks"/>.
/// A model of the table (<c>object/&lt;id&gt;</c>) is drawn instead when there is one. Pure, without Godot.
/// </summary>
public static class PropLooks
{
    /// <summary>Whether the object has a look here.</summary>
    public static bool Known(string id) => Looks.ContainsKey(id);

    /// <summary>The parts of the object; empty for one without a look here.</summary>
    public static IReadOnlyList<LookPart> Of(string id) => Looks.TryGetValue(id, out var parts) ? parts : [];

    /// <summary>The ids with a look.</summary>
    public static IEnumerable<string> Ids => Looks.Keys;

    private static LookPart Box(float w, float h, float d, float x, float y, float z, LookTint tint, float tilt = 0f, float yaw = 0f) =>
        new(LookShape.Box, w, h, d, x, y + h / 2f, z, tint, tilt, yaw);

    private static LookPart Cyl(float diameter, float h, float x, float y, float z, LookTint tint, float top = -1f) =>
        new(LookShape.Cylinder, diameter, h, top < 0f ? diameter : top, x, y + h / 2f, z, tint);

    // A cylinder about its centre, tilted: a lying log, a wheel, a sign.
    private static LookPart Tube(float diameter, float length, float x, float y, float z, LookTint tint, float tilt, float yaw = 0f) =>
        new(LookShape.Cylinder, diameter, length, diameter, x, y, z, tint, tilt, yaw);

    private static LookPart Cone(float diameter, float h, float x, float y, float z, LookTint tint) =>
        new(LookShape.Cylinder, diameter, h, 0f, x, y + h / 2f, z, tint);

    private static LookPart Ball(float diameter, float x, float y, float z, LookTint tint) =>
        new(LookShape.Sphere, diameter, diameter, diameter, x, y, z, tint);

    private static readonly Dictionary<string, LookPart[]> Looks = new()
    {
        // The street (docs/city_elements).
        ["bench"] =
        [
            Box(0.6f, 0.05f, 0.2f, 0, 0.18f, 0, LookTint.Wood),
            Box(0.6f, 0.2f, 0.04f, 0, 0.23f, -0.1f, LookTint.Wood),
            Box(0.05f, 0.18f, 0.18f, -0.25f, 0, 0, LookTint.Dark),
            Box(0.05f, 0.18f, 0.18f, 0.25f, 0, 0, LookTint.Dark),
        ],
        ["trash_bin"] = [Cyl(0.24f, 0.36f, 0, 0, 0, LookTint.Leaf, 0.27f), Cyl(0.28f, 0.04f, 0, 0.36f, 0, LookTint.Dark)],
        ["mailbox"] = [Box(0.05f, 0.25f, 0.05f, 0, 0, 0, LookTint.Dark), Box(0.2f, 0.3f, 0.16f, 0, 0.25f, 0, LookTint.Blue)],
        ["phone_booth"] = [Box(0.42f, 0.85f, 0.42f, 0, 0, 0, LookTint.Red), Box(0.32f, 0.5f, 0.44f, 0, 0.2f, 0, LookTint.Glass)],
        ["bus_stop"] =
        [
            Box(0.9f, 0.05f, 0.4f, 0, 0.72f, 0, LookTint.Glass),
            Box(0.05f, 0.72f, 0.05f, -0.42f, 0, -0.15f, LookTint.Metal),
            Box(0.05f, 0.72f, 0.05f, 0.42f, 0, -0.15f, LookTint.Metal),
            Box(0.85f, 0.45f, 0.03f, 0, 0.12f, -0.17f, LookTint.Glass),
            Box(0.6f, 0.05f, 0.15f, 0, 0.2f, -0.05f, LookTint.Wood),
        ],
        ["billboard"] =
        [
            Box(0.06f, 0.9f, 0.06f, -0.35f, 0, 0, LookTint.Metal),
            Box(0.06f, 0.9f, 0.06f, 0.35f, 0, 0, LookTint.Metal),
            Box(0.95f, 0.5f, 0.06f, 0, 0.9f, 0, LookTint.White),
            Box(0.95f, 0.1f, 0.07f, 0, 1.2f, 0, LookTint.Accent),
        ],
        ["road_sign"] = [Cyl(0.04f, 0.6f, 0, 0, 0, LookTint.Metal), Tube(0.28f, 0.03f, 0, 0.66f, 0.03f, LookTint.Red, 90f)],
        ["traffic_light"] =
        [
            Cyl(0.06f, 0.9f, 0, 0, 0, LookTint.Dark),
            Box(0.13f, 0.36f, 0.1f, 0, 0.9f, 0, LookTint.Dark),
            Ball(0.07f, 0, 1.17f, 0.05f, LookTint.Red),
            Ball(0.07f, 0, 1.07f, 0.05f, LookTint.Accent),
            Ball(0.07f, 0, 0.97f, 0.05f, LookTint.Green),
        ],
        ["flowerbed"] =
        [
            Box(0.8f, 0.14f, 0.8f, 0, 0, 0, LookTint.Stone),
            Box(0.7f, 0.06f, 0.7f, 0, 0.14f, 0, LookTint.Leaf),
            Ball(0.12f, -0.2f, 0.24f, -0.15f, LookTint.Red),
            Ball(0.12f, 0.18f, 0.24f, 0.12f, LookTint.Accent),
            Ball(0.12f, 0.05f, 0.24f, -0.2f, LookTint.Red),
        ],
        ["street_stall"] =
        [
            Box(0.7f, 0.4f, 0.4f, 0, 0, 0, LookTint.Wood),
            Box(0.04f, 0.8f, 0.04f, -0.36f, 0, -0.2f, LookTint.Wood),
            Box(0.04f, 0.8f, 0.04f, 0.36f, 0, -0.2f, LookTint.Wood),
            Box(0.85f, 0.04f, 0.55f, 0, 0.8f, 0, LookTint.Red, 10f),
            Ball(0.12f, -0.15f, 0.46f, 0.05f, LookTint.Accent),
            Ball(0.12f, 0.12f, 0.46f, 0.05f, LookTint.Green),
        ],

        // The beach (docs/beach_elements).
        ["beach_umbrella"] = [Cyl(0.04f, 0.7f, 0, 0, 0, LookTint.White), Cone(0.75f, 0.22f, 0, 0.6f, 0, LookTint.Red)],
        ["sun_lounger"] =
        [
            Box(0.26f, 0.06f, 0.55f, 0, 0.1f, 0.08f, LookTint.White),
            Box(0.26f, 0.05f, 0.3f, 0, 0.18f, -0.28f, LookTint.White, -40f),
            Box(0.24f, 0.1f, 0.04f, 0, 0, 0.3f, LookTint.Metal),
        ],
        ["beach_shower"] =
        [
            Cyl(0.42f, 0.04f, 0, 0, 0, LookTint.Wood),
            Cyl(0.06f, 0.95f, -0.1f, 0, 0, LookTint.Metal),
            Box(0.22f, 0.05f, 0.05f, 0, 0.92f, 0, LookTint.Metal),
        ],
        ["volleyball_net"] =
        [
            Cyl(0.05f, 0.6f, -0.45f, 0, 0, LookTint.Metal),
            Cyl(0.05f, 0.6f, 0.45f, 0, 0, LookTint.Metal),
            Box(0.9f, 0.25f, 0.02f, 0, 0.32f, 0, LookTint.White),
        ],
        ["fishing_nets"] =
        [
            Cyl(0.04f, 0.5f, -0.35f, 0, 0, LookTint.Wood),
            Cyl(0.04f, 0.5f, 0.35f, 0, 0, LookTint.Wood),
            Box(0.7f, 0.35f, 0.02f, 0, 0.12f, 0, LookTint.Trim),
            Ball(0.07f, -0.15f, 0.47f, 0, LookTint.Accent),
            Ball(0.07f, 0.15f, 0.47f, 0, LookTint.Accent),
        ],
        ["driftwood"] = [Tube(0.12f, 0.8f, 0, 0.06f, 0, LookTint.Wood, 90f, 30f), Tube(0.06f, 0.3f, 0.12f, 0.05f, 0.1f, LookTint.Wood, 90f, -40f)],
        ["beached_boat"] =
        [
            Box(0.42f, 0.22f, 0.9f, 0, 0, 0, LookTint.White, 0f, 20f),
            Box(0.36f, 0.05f, 0.84f, 0, 0.22f, 0, LookTint.Wood, 0f, 20f),
            Box(0.43f, 0.06f, 0.91f, 0, 0.12f, 0, LookTint.Blue, 0f, 20f),
        ],

        // The desert and the farm (docs/desert_elements, docs/temperate_elements).
        ["dry_shrub"] = [Ball(0.42f, 0, 0.14f, 0, LookTint.Straw), Ball(0.26f, 0.18f, 0.1f, 0.12f, LookTint.Straw)],
        ["hedge"] = [Box(0.96f, 0.5f, 0.42f, 0, 0, 0, LookTint.Leaf)],
        ["hay_bale"] = [Tube(0.5f, 0.62f, 0, 0.25f, 0, LookTint.Straw, 90f, 90f)],
        ["well"] =
        [
            Cyl(0.6f, 0.35f, 0, 0, 0, LookTint.Stone),
            Cyl(0.42f, 0.02f, 0, 0.34f, 0, LookTint.Dark),
            Box(0.05f, 0.55f, 0.05f, -0.27f, 0.35f, 0, LookTint.Wood),
            Box(0.05f, 0.55f, 0.05f, 0.27f, 0.35f, 0, LookTint.Wood),
            Box(0.72f, 0.05f, 0.5f, 0, 0.9f, 0, LookTint.Roof),
        ],
        ["farm_tractor"] =
        [
            Box(0.34f, 0.24f, 0.58f, 0, 0.12f, 0.05f, LookTint.Red),
            Box(0.3f, 0.26f, 0.26f, 0, 0.36f, -0.12f, LookTint.Glass),
            Tube(0.36f, 0.08f, -0.2f, 0.18f, -0.15f, LookTint.Dark, 90f, 90f),
            Tube(0.36f, 0.08f, 0.2f, 0.18f, -0.15f, LookTint.Dark, 90f, 90f),
            Tube(0.22f, 0.07f, -0.18f, 0.11f, 0.25f, LookTint.Dark, 90f, 90f),
            Tube(0.22f, 0.07f, 0.18f, 0.11f, 0.25f, LookTint.Dark, 90f, 90f),
        ],

        // Obstacles (docs/obstacle_elements).
        ["barbed_wire"] =
        [
            Box(0.05f, 0.4f, 0.05f, -0.42f, 0, 0, LookTint.Wood),
            Box(0.05f, 0.4f, 0.05f, 0.42f, 0, 0, LookTint.Wood),
            Tube(0.28f, 0.9f, 0, 0.16f, 0, LookTint.Metal, 90f, 90f),
        ],
    };
}
