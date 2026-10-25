using System;
using System.Collections.Generic;
using Rts.Simulation.Rules;

namespace Rts.Client.Terrain;

/// <summary>The primitive a part of a building is made of.</summary>
public enum LookShape
{
    Box,

    /// <summary>A cylinder; <see cref="LookPart.SizeZ"/> is its top diameter (0 for a cone).</summary>
    Cylinder,

    /// <summary>A four-sided pyramid; <see cref="LookPart.SizeX"/> across its base.</summary>
    Pyramid,

    /// <summary>The top half of a sphere, standing on its flat side.</summary>
    Dome,

    Sphere,

    /// <summary>A ring lying flat; <see cref="LookPart.SizeX"/> across, <see cref="LookPart.SizeY"/> thick. Tilted 90° it stands.</summary>
    Ring,
}

/// <summary>The colour of a part: by the faction of the building (<see cref="BuildingLooks.Palette"/>), the owner's, or one of the marks.</summary>
public enum LookTint
{
    /// <summary>The walls: light concrete for the Allies, rust red for the Soviets.</summary>
    Body,

    /// <summary>Roofs and halls: slate blue for the Allies, dark rust for the Soviets.</summary>
    Roof,

    /// <summary>Dark trim: doors, slits, caps.</summary>
    Trim,

    /// <summary>Bare metal: masts, tanks, pipes.</summary>
    Metal,

    /// <summary>Glass and glazed domes.</summary>
    Glass,

    /// <summary>Yellow: cranes, hazard rings, conveyors.</summary>
    Accent,

    /// <summary>Energy: the blue of Tesla and of the chronosphere.</summary>
    Glow,

    Concrete,
    White,
    Red,
    Green,
    Dark,

    /// <summary>The owner's colour.</summary>
    Owner,

    /// <summary>Sun-dried clay: the adobe houses of the desert.</summary>
    Clay,

    Wood,

    /// <summary>Grey stone: ruins, wells, fountains.</summary>
    Stone,

    /// <summary>Red desert rock: mesas and arches.</summary>
    Rock,

    /// <summary>Straw and dry grass: hay, thatch, dry shrubs.</summary>
    Straw,

    /// <summary>Leaves: hedges and flowerbeds.</summary>
    Leaf,

    /// <summary>A painted blue: containers, signs.</summary>
    Blue,
}

/// <summary>Whose colours a look takes: the Allies light, the Soviets rust red, the buildings and props of the map civilian.</summary>
public enum LookFaction
{
    Allied,
    Soviet,
    Civilian,
}

/// <summary>
/// One part of a building in cells, the footprint's centre at (0, 0) on the ground, x to the east, z to the south (the camera's
/// side), y up. (<see cref="X"/>, <see cref="Y"/>, <see cref="Z"/>) is the centre of the part. <see cref="Tilt"/> turns it about the
/// east axis first — its top towards the south for a positive angle — then <see cref="Yaw"/> about the vertical.
/// </summary>
public readonly record struct LookPart(
    LookShape Shape, float SizeX, float SizeY, float SizeZ, float X, float Y, float Z, LookTint Tint, float Tilt = 0f, float Yaw = 0f)
{
    /// <summary>How high its top reaches, roughly: the centre and half its height, or more when it is tilted.</summary>
    public float Top => Y + (Tilt == 0f ? Shape == LookShape.Dome ? SizeX / 2f : Shape == LookShape.Sphere ? SizeX / 2f : SizeY / 2f
        : MathF.Max(SizeX, SizeY) / 2f);
}

/// <summary>
/// The placeholder looks of the players' buildings until the models come (the owner's wish, 2026-10-24: «I do not know which building
/// is which»): every building of RA2 its own outline out of a few primitives — the power plant's dome, the barracks' hut, the war
/// factory's long hall, the Tesla coil's rings, the cooling towers of the nuclear reactor, the Chronosphere's ring. The first part
/// is the base the owner's band goes round. A building not named here gets a block that grows with its footprint. Pure, without Godot.
/// </summary>
public static class BuildingLooks
{
    /// <summary>The colours of a faction: the Allies light, the Soviets rust red, the civilians stucco and terracotta.</summary>
    public static (float R, float G, float B) Palette(LookTint tint, LookFaction faction) => (tint, faction) switch
    {
        (LookTint.Body, LookFaction.Soviet) => (0.56f, 0.33f, 0.27f),
        (LookTint.Body, LookFaction.Civilian) => (0.84f, 0.78f, 0.67f),
        (LookTint.Body, _) => (0.8f, 0.82f, 0.85f),
        (LookTint.Roof, LookFaction.Soviet) => (0.36f, 0.22f, 0.19f),
        (LookTint.Roof, LookFaction.Civilian) => (0.62f, 0.3f, 0.22f),
        (LookTint.Roof, _) => (0.4f, 0.48f, 0.6f),
        (LookTint.Trim, LookFaction.Soviet) => (0.24f, 0.19f, 0.17f),
        (LookTint.Trim, LookFaction.Civilian) => (0.3f, 0.26f, 0.22f),
        (LookTint.Trim, _) => (0.28f, 0.3f, 0.34f),
        (LookTint.Metal, LookFaction.Soviet) => (0.5f, 0.47f, 0.44f),
        (LookTint.Metal, _) => (0.62f, 0.64f, 0.68f),
        (LookTint.Glass, LookFaction.Soviet) => (0.95f, 0.7f, 0.35f),
        (LookTint.Glass, LookFaction.Civilian) => (0.55f, 0.72f, 0.82f),
        (LookTint.Glass, _) => (0.5f, 0.75f, 0.95f),
        (LookTint.Clay, _) => (0.78f, 0.6f, 0.42f),
        (LookTint.Wood, _) => (0.52f, 0.36f, 0.22f),
        (LookTint.Stone, _) => (0.68f, 0.66f, 0.6f),
        (LookTint.Rock, _) => (0.64f, 0.38f, 0.27f),
        (LookTint.Straw, _) => (0.86f, 0.74f, 0.42f),
        (LookTint.Leaf, _) => (0.22f, 0.42f, 0.18f),
        (LookTint.Blue, _) => (0.25f, 0.42f, 0.7f),
        (LookTint.Accent, _) => (0.95f, 0.78f, 0.18f),
        (LookTint.Glow, _) => (0.45f, 0.8f, 1f),
        (LookTint.Concrete, _) => (0.63f, 0.62f, 0.59f),
        (LookTint.White, _) => (0.95f, 0.95f, 0.95f),
        (LookTint.Red, _) => (0.85f, 0.15f, 0.12f),
        (LookTint.Green, _) => (0.35f, 0.95f, 0.4f),
        (LookTint.Dark, _) => (0.14f, 0.14f, 0.16f),
        _ => (1f, 1f, 1f),
    };

    /// <summary>The parts of the building, the first its base.</summary>
    public static IReadOnlyList<LookPart> Of(BuildingDef def) => Of(def.Id, def.Footprint.Width, def.Footprint.Height, def.Wall);

    /// <summary>Whether the building has a look of its own, not the block of <see cref="Block"/>.</summary>
    public static bool Known(string id) => Looks.ContainsKey(id);

    /// <summary>How high the building reaches, in cells: for the bars and the marks over it.</summary>
    public static float Top(BuildingDef def)
    {
        var top = 0f;
        foreach (var part in Of(def))
        {
            top = MathF.Max(top, part.Top);
        }

        return top;
    }

    /// <summary>How high the parts reach, in cells.</summary>
    public static float Top(IReadOnlyList<LookPart> parts)
    {
        var top = 0f;
        foreach (var part in parts)
        {
            top = MathF.Max(top, part.Top);
        }

        return top;
    }

    public static IReadOnlyList<LookPart> Of(string id, int width, int height, bool wall) =>
        Looks.TryGetValue(id, out var parts) ? parts : Block(width, height, wall);

    /// <summary>A block over the footprint, taller with its size: a building of the data without a look of its own.</summary>
    public static IReadOnlyList<LookPart> Block(int width, int height, bool wall)
    {
        var tall = wall ? 0.5f : 0.35f + 0.2f * Math.Max(width, height);
        return [Box(width - 0.16f, tall, height - 0.16f, 0, 0, 0, wall ? LookTint.Concrete : LookTint.Body)];
    }

    // A box of the size, its bottom at y.
    private static LookPart Box(float w, float h, float d, float x, float y, float z, LookTint tint, float tilt = 0f, float yaw = 0f) =>
        new(LookShape.Box, w, h, d, x, y + h / 2f, z, tint, tilt, yaw);

    // An upright cylinder of the diameter (the top one, if given), its bottom at y.
    private static LookPart Cyl(float diameter, float h, float x, float y, float z, LookTint tint, float top = -1f) =>
        new(LookShape.Cylinder, diameter, h, top < 0f ? diameter : top, x, y + h / 2f, z, tint);

    // A cylinder about its centre, tilted: a barrel, a lying tank, a dish.
    private static LookPart Tube(float diameter, float length, float x, float y, float z, LookTint tint, float tilt, float yaw = 0f, float top = -1f) =>
        new(LookShape.Cylinder, diameter, length, top < 0f ? diameter : top, x, y, z, tint, tilt, yaw);

    private static LookPart Cone(float diameter, float h, float x, float y, float z, LookTint tint) =>
        new(LookShape.Cylinder, diameter, h, 0f, x, y + h / 2f, z, tint);

    private static LookPart Pyramid(float across, float h, float x, float y, float z, LookTint tint) =>
        new(LookShape.Pyramid, across, h, 0f, x, y + h / 2f, z, tint);

    // A dome standing on y.
    private static LookPart Dome(float diameter, float x, float y, float z, LookTint tint) =>
        new(LookShape.Dome, diameter, diameter / 2f, diameter, x, y, z, tint);

    // A ball about its centre.
    private static LookPart Ball(float diameter, float x, float y, float z, LookTint tint) =>
        new(LookShape.Sphere, diameter, diameter, diameter, x, y, z, tint);

    // A flat ring about its centre; tilted 90° it stands facing the camera.
    private static LookPart Ring(float diameter, float thick, float x, float y, float z, LookTint tint, float tilt = 0f) =>
        new(LookShape.Ring, diameter, thick, diameter, x, y, z, tint, tilt);

    // A radar dish: a wide bowl on a short stem, turned towards the camera.
    private static LookPart Dish(float diameter, float x, float y, float z, float tilt = 30f) =>
        new(LookShape.Cylinder, diameter * 0.3f, 0.14f, diameter, x, y, z, LookTint.White, tilt);

    // A flag on a pole at the corner: the pole and the cloth.
    private static LookPart[] Flag(float x, float y, float z, LookTint cloth) =>
        [Cyl(0.05f, 1.2f, x, y, z, LookTint.Metal), Box(0.4f, 0.24f, 0.03f, x + 0.2f, y + 0.94f, z, cloth)];

    private static readonly Dictionary<string, LookPart[]> Looks = new()
    {
        // The Allies.
        ["alliance_construction_yard"] =
        [
            Box(2.8f, 0.35f, 2.8f, 0, 0, 0, LookTint.Body),
            Box(1.4f, 0.7f, 1.2f, -0.5f, 0.35f, -0.4f, LookTint.Roof),
            Box(1.5f, 0.08f, 1.3f, -0.5f, 1.05f, -0.4f, LookTint.Trim),
            Cyl(0.16f, 1.6f, 0.9f, 0.35f, -0.9f, LookTint.Accent),
            Box(1.9f, 0.12f, 0.14f, 0.15f, 1.9f, -0.9f, LookTint.Accent),
            Box(0.05f, 0.5f, 0.05f, -0.6f, 1.4f, -0.9f, LookTint.Trim),
            Box(0.6f, 0.3f, 0.6f, 0.7f, 0.35f, 0.7f, LookTint.Metal),
        ],
        ["alliance_power_plant"] =
        [
            Box(1.8f, 0.4f, 1.8f, 0, 0, 0, LookTint.Body),
            Dome(1.3f, -0.1f, 0.4f, 0.1f, LookTint.Glass),
            Cyl(0.25f, 0.9f, 0.6f, 0.4f, -0.6f, LookTint.Metal),
            Box(0.5f, 0.25f, 0.3f, -0.55f, 0.4f, -0.65f, LookTint.Trim),
        ],
        ["alliance_barracks"] =
        [
            Box(1.8f, 0.25f, 1.8f, 0, 0, 0, LookTint.Body),
            Tube(1.2f, 1.5f, 0.1f, 0.25f, 0, LookTint.Roof, 90f),
            Box(0.45f, 0.4f, 0.1f, 0.1f, 0.25f, 0.8f, LookTint.Trim),
            .. Flag(-0.75f, 0.25f, -0.75f, LookTint.Owner),
        ],
        ["alliance_ore_refinery"] =
        [
            Box(2.8f, 0.35f, 2.8f, 0, 0, 0, LookTint.Body),
            Box(1.6f, 0.8f, 1.2f, -0.5f, 0.35f, -0.6f, LookTint.Roof),
            Cyl(0.7f, 1.2f, 0.9f, 0.35f, -0.8f, LookTint.Metal),
            Dome(0.7f, 0.9f, 1.55f, -0.8f, LookTint.Metal),
            Cyl(0.5f, 0.9f, 0.9f, 0.35f, 0.1f, LookTint.Metal),
            Box(0.35f, 0.12f, 1.0f, 0, 0.5f, 0.75f, LookTint.Accent),
        ],
        ["alliance_war_factory"] =
        [
            Box(2.8f, 0.3f, 3.8f, 0, 0, 0, LookTint.Body),
            Tube(2.3f, 3.0f, 0, 0.3f, -0.25f, LookTint.Roof, 90f),
            Box(1.2f, 0.75f, 0.1f, 0, 0.3f, 1.3f, LookTint.Trim),
            Cyl(0.22f, 0.8f, 1.05f, 0.3f, -1.55f, LookTint.Metal),
            Box(0.3f, 0.3f, 0.3f, -1.1f, 0.3f, 1.55f, LookTint.Accent),
        ],
        ["alliance_air_force_command"] =
        [
            Box(2.8f, 0.35f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(1.3f, 0.45f, 1.3f, -0.65f, 0.35f, 0, LookTint.Roof),
            Cyl(0.45f, 1.1f, 0.85f, 0.35f, -0.4f, LookTint.Body),
            Cyl(0.75f, 0.3f, 0.85f, 1.45f, -0.4f, LookTint.Glass),
            Cyl(0.8f, 0.04f, 0.65f, 0.35f, 0.45f, LookTint.Accent),
            Dish(0.75f, -0.65f, 1.0f, 0),
        ],
        ["alliance_naval_shipyard"] =
        [
            Box(3.8f, 0.25f, 0.5f, 0, 0, -1.65f, LookTint.Concrete),
            Box(0.5f, 0.25f, 3.3f, -1.65f, 0, 0.25f, LookTint.Concrete),
            Box(0.5f, 0.25f, 3.3f, 1.65f, 0, 0.25f, LookTint.Concrete),
            Box(1.2f, 0.6f, 0.45f, -1.0f, 0.25f, -1.65f, LookTint.Roof),
            Box(0.18f, 1.4f, 0.18f, -1.65f, 0.25f, 1.2f, LookTint.Accent),
            Box(0.18f, 1.4f, 0.18f, 1.65f, 0.25f, 1.2f, LookTint.Accent),
            Box(3.5f, 0.2f, 0.3f, 0, 1.65f, 1.2f, LookTint.Accent),
        ],
        ["alliance_service_depot"] =
        [
            Box(2.7f, 0.15f, 2.7f, 0, 0, 0, LookTint.Body),
            Cyl(2.0f, 0.04f, 0, 0.15f, 0, LookTint.Accent),
            Cyl(1.7f, 0.05f, 0, 0.15f, 0, LookTint.Concrete),
            Box(0.18f, 1.0f, 0.18f, -1.15f, 0.15f, -1.15f, LookTint.Metal),
            Box(0.18f, 1.0f, 0.18f, 1.15f, 0.15f, -1.15f, LookTint.Metal),
            Box(2.5f, 0.16f, 0.2f, 0, 1.15f, -1.15f, LookTint.Metal),
            Box(0.45f, 0.14f, 0.12f, 0, 1.35f, -1.15f, LookTint.Green),
            Box(0.14f, 0.45f, 0.12f, 0, 1.2f, -1.15f, LookTint.Green),
        ],
        ["alliance_battle_lab"] =
        [
            Box(2.8f, 0.45f, 2.8f, 0, 0, 0, LookTint.Body),
            Dome(2.0f, 0, 0.45f, -0.1f, LookTint.Glass),
            Cyl(0.08f, 0.9f, 1.05f, 0.45f, -1.05f, LookTint.Metal),
            Ball(0.18f, 1.05f, 1.4f, -1.05f, LookTint.Red),
            Box(0.8f, 0.35f, 0.45f, -0.9f, 0.45f, 1.05f, LookTint.Roof),
        ],
        ["alliance_ore_purifier"] =
        [
            Box(2.8f, 0.35f, 2.8f, 0, 0, 0, LookTint.Body),
            Cyl(1.2f, 1.1f, 0, 0.35f, 0, LookTint.Metal),
            Cone(1.25f, 0.5f, 0, 1.45f, 0, LookTint.Roof),
            Cyl(0.45f, 0.7f, -1.0f, 0.35f, -1.0f, LookTint.Glass),
            Cyl(0.45f, 0.7f, 1.0f, 0.35f, -1.0f, LookTint.Glass),
            Box(0.18f, 0.18f, 1.3f, 0.95f, 0.55f, 0.45f, LookTint.Accent),
        ],
        ["alliance_spy_satellite_uplink"] =
        [
            Box(1.8f, 0.3f, 1.8f, 0, 0, 0, LookTint.Body),
            Cyl(0.25f, 0.8f, 0, 0.3f, -0.1f, LookTint.Metal),
            Dish(1.2f, 0, 1.2f, 0, 50f),
            Box(0.4f, 0.3f, 0.4f, 0.6f, 0.3f, 0.6f, LookTint.Roof),
        ],
        ["alliance_chronosphere"] =
        [
            Box(3.8f, 0.3f, 2.8f, 0, 0, 0, LookTint.Body),
            Cyl(2.3f, 0.15f, 0, 0.3f, 0, LookTint.Metal),
            Box(0.25f, 1.1f, 0.3f, -1.15f, 0.45f, 0, LookTint.Metal),
            Box(0.25f, 1.1f, 0.3f, 1.15f, 0.45f, 0, LookTint.Metal),
            Ring(2.2f, 0.18f, 0, 1.45f, 0, LookTint.Roof, 90f),
            Ball(0.6f, 0, 1.45f, 0, LookTint.Glow),
        ],
        ["alliance_weather_control_device"] =
        [
            Box(2.8f, 0.35f, 2.8f, 0, 0, 0, LookTint.Body),
            Cyl(1.0f, 0.5f, 0, 0.35f, 0, LookTint.Roof),
            Cyl(0.3f, 1.6f, 0, 0.85f, 0, LookTint.Metal),
            Cyl(1.7f, 0.12f, 0, 2.45f, 0, LookTint.Body),
            Ball(0.3f, -0.75f, 2.7f, 0, LookTint.Glow),
            Ball(0.3f, 0.75f, 2.7f, 0, LookTint.Glow),
            Ball(0.3f, 0, 2.7f, 0.75f, LookTint.Glow),
        ],
        ["alliance_gap_generator"] =
        [
            Box(0.8f, 0.25f, 0.8f, 0, 0, 0, LookTint.Body),
            Cyl(0.26f, 1.2f, 0, 0.25f, 0, LookTint.Metal, 0.12f),
            Ring(0.7f, 0.06f, 0, 1.0f, 0, LookTint.Glow),
            Ball(0.4f, 0, 1.55f, 0, LookTint.Trim),
        ],
        ["alliance_pillbox"] =
        [
            Box(0.86f, 0.2f, 0.86f, 0, 0, 0, LookTint.Concrete),
            Dome(0.8f, 0, 0.2f, 0, LookTint.Concrete),
            Box(0.5f, 0.06f, 0.06f, 0, 0.32f, 0.38f, LookTint.Dark),
        ],
        ["alliance_patriot_missile_system"] =
        [
            Box(0.86f, 0.25f, 0.86f, 0, 0, 0, LookTint.Body),
            Box(0.6f, 0.3f, 0.5f, 0, 0.25f, 0, LookTint.Roof),
            Tube(0.13f, 0.55f, -0.14f, 0.75f, -0.05f, LookTint.White, -35f),
            Tube(0.13f, 0.55f, 0.14f, 0.75f, -0.05f, LookTint.White, -35f),
            Tube(0.13f, 0.55f, -0.14f, 0.9f, 0.1f, LookTint.White, -35f),
            Tube(0.13f, 0.55f, 0.14f, 0.9f, 0.1f, LookTint.White, -35f),
        ],
        ["alliance_prism_tower"] =
        [
            Box(0.86f, 0.2f, 0.86f, 0, 0, 0, LookTint.Body),
            Cyl(0.55f, 1.4f, 0, 0.2f, 0, LookTint.Body, 0.3f),
            Pyramid(0.5f, 0.45f, 0, 1.6f, 0, LookTint.Glow),
            Pyramid(0.5f, 0.3f, 0, 1.6f, 0, LookTint.Glow) with { Tilt = 180f, Y = 1.45f },
        ],
        ["alliance_grand_cannon"] =
        [
            Cyl(1.8f, 0.5f, 0, 0, 0, LookTint.Concrete),
            Cyl(1.2f, 0.4f, 0, 0.5f, 0, LookTint.Roof),
            Tube(0.24f, 1.6f, 0, 0.95f, 0.75f, LookTint.Metal, 75f),
            Box(0.5f, 0.25f, 0.4f, 0, 0.9f, -0.3f, LookTint.Trim),
        ],
        ["alliance_wall"] = [Box(0.96f, 0.5f, 0.96f, 0, 0, 0, LookTint.Concrete), Box(0.96f, 0.06f, 0.96f, 0, 0.5f, 0, LookTint.Body)],

        // The buildings of the map (the element documents, 2026-10-25), in the civilian colours.
        ["neutral_school"] =
        [
            Box(2.8f, 0.9f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(2.9f, 0.12f, 1.9f, 0, 0.9f, 0, LookTint.Roof),
            Box(2.4f, 0.18f, 0.03f, 0, 0.45f, 0.91f, LookTint.Glass),
            Box(0.6f, 0.5f, 0.12f, 0, 0, 0.88f, LookTint.Trim),
            Box(0.45f, 0.45f, 0.45f, 0, 1.02f, -0.3f, LookTint.Body),
            Pyramid(0.5f, 0.3f, 0, 1.47f, -0.3f, LookTint.Roof),
        ],
        ["neutral_office"] =
        [
            Box(1.8f, 1.6f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(1.82f, 0.18f, 1.82f, 0, 0.45f, 0, LookTint.Glass),
            Box(1.82f, 0.18f, 1.82f, 0, 0.95f, 0, LookTint.Glass),
            Box(1.82f, 0.18f, 1.82f, 0, 1.3f, 0, LookTint.Glass),
            Box(1.9f, 0.1f, 1.9f, 0, 1.6f, 0, LookTint.Trim),
            Box(0.4f, 0.25f, 0.4f, 0.5f, 1.7f, -0.5f, LookTint.Metal),
        ],
        ["neutral_church"] =
        [
            Box(1.5f, 0.9f, 1.9f, 0, 0, 0.45f, LookTint.Body),
            // The ridge runs north–south: the slopes along x, turned a quarter; the outer edge of each the lower.
            Box(2.0f, 0.1f, 1.0f, -0.32f, 1.05f, 0.45f, LookTint.Roof, -35f, 90f),
            Box(2.0f, 0.1f, 1.0f, 0.32f, 1.05f, 0.45f, LookTint.Roof, 35f, 90f),
            Box(0.7f, 1.9f, 0.7f, 0, 0, -1.05f, LookTint.Body),
            Pyramid(0.8f, 0.7f, 0, 1.9f, -1.05f, LookTint.Roof),
            Box(0.05f, 0.35f, 0.05f, 0, 2.6f, -1.05f, LookTint.Accent),
            Box(0.22f, 0.05f, 0.05f, 0, 2.82f, -1.05f, LookTint.Accent),
            Box(0.35f, 0.5f, 0.05f, 0, 0, 1.42f, LookTint.Trim),
        ],
        ["neutral_shop"] =
        [
            Box(1.8f, 0.8f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(1.9f, 0.1f, 1.9f, 0, 0.8f, 0, LookTint.Trim),
            Box(1.4f, 0.35f, 0.03f, 0, 0.1f, 0.91f, LookTint.Glass),
            Box(1.8f, 0.05f, 0.35f, 0, 0.62f, 0.8f, LookTint.Red, 20f),
            Box(1.0f, 0.2f, 0.05f, 0, 0.82f, 0.9f, LookTint.Accent),
        ],
        ["neutral_kiosk"] =
        [
            Box(0.75f, 0.6f, 0.75f, 0, 0, 0, LookTint.Body),
            Box(0.9f, 0.06f, 0.9f, 0, 0.6f, 0, LookTint.Red),
            Box(0.5f, 0.25f, 0.03f, 0, 0.25f, 0.39f, LookTint.Glass),
        ],
        ["neutral_skyscraper"] =
        [
            Box(1.6f, 3.6f, 1.6f, 0, 0, 0, LookTint.Glass),
            Box(1.7f, 0.15f, 1.7f, 0, 1.2f, 0, LookTint.Body),
            Box(1.7f, 0.15f, 1.7f, 0, 2.4f, 0, LookTint.Body),
            Box(1.7f, 0.2f, 1.7f, 0, 3.6f, 0, LookTint.Body),
            Cyl(0.06f, 0.8f, 0.4f, 3.8f, -0.4f, LookTint.Metal),
        ],
        ["neutral_cafe"] =
        [
            Box(1.8f, 0.6f, 0.75f, 0, 0, -0.08f, LookTint.Body),
            Box(1.9f, 0.08f, 0.85f, 0, 0.6f, -0.08f, LookTint.Roof),
            Box(1.8f, 0.05f, 0.3f, 0, 0.5f, 0.35f, LookTint.Green, 15f),
            Box(1.2f, 0.25f, 0.03f, 0, 0.12f, 0.3f, LookTint.Glass),
        ],
        ["neutral_bank"] =
        [
            Box(2.8f, 0.9f, 1.6f, 0, 0, -0.1f, LookTint.Stone),
            Box(2.9f, 0.15f, 1.9f, 0, 0.9f, 0, LookTint.White),
            Cyl(0.16f, 0.75f, -1.05f, 0.15f, 0.8f, LookTint.White),
            Cyl(0.16f, 0.75f, -0.35f, 0.15f, 0.8f, LookTint.White),
            Cyl(0.16f, 0.75f, 0.35f, 0.15f, 0.8f, LookTint.White),
            Cyl(0.16f, 0.75f, 1.05f, 0.15f, 0.8f, LookTint.White),
            Box(2.6f, 0.15f, 0.3f, 0, 0, 0.8f, LookTint.Stone),
            Box(0.6f, 0.08f, 0.04f, 0, 0.98f, 0.95f, LookTint.Accent),
        ],
        ["neutral_hotel"] =
        [
            Box(2.8f, 1.8f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(2.82f, 0.12f, 1.82f, 0, 0.55f, 0, LookTint.Glass),
            Box(2.82f, 0.12f, 1.82f, 0, 1.0f, 0, LookTint.Glass),
            Box(2.82f, 0.12f, 1.82f, 0, 1.45f, 0, LookTint.Glass),
            Box(2.9f, 0.1f, 1.9f, 0, 1.8f, 0, LookTint.Trim),
            Box(1.0f, 0.3f, 0.05f, 0, 1.9f, 0.6f, LookTint.Red),
            Box(0.8f, 0.05f, 0.4f, 0, 0.45f, 0.8f, LookTint.Red),
        ],
        ["neutral_fire_station"] =
        [
            Box(2.8f, 0.9f, 1.8f, 0, 0, 0, LookTint.Red),
            Box(2.9f, 0.1f, 1.9f, 0, 0.9f, 0, LookTint.Trim),
            Box(0.7f, 0.6f, 0.05f, -0.75f, 0, 0.91f, LookTint.White),
            Box(0.7f, 0.6f, 0.05f, 0.15f, 0, 0.91f, LookTint.White),
            Box(0.6f, 1.8f, 0.6f, 1.05f, 0, -0.5f, LookTint.Red),
            Box(0.7f, 0.1f, 0.7f, 1.05f, 1.8f, -0.5f, LookTint.Trim),
        ],
        ["neutral_auto_repair"] =
        [
            Box(1.8f, 0.7f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(1.9f, 0.08f, 1.9f, 0, 0.7f, 0, LookTint.Trim),
            Box(1.0f, 0.55f, 0.05f, -0.2f, 0, 0.91f, LookTint.Dark),
            Box(0.8f, 0.2f, 0.05f, 0, 0.78f, 0.9f, LookTint.Blue),
            Ring(0.4f, 0.1f, 0.65f, 0.2f, 0.75f, LookTint.Dark, 90f),
        ],
        ["neutral_fountain"] =
        [
            Cyl(1.7f, 0.25f, 0, 0, 0, LookTint.Stone),
            Cyl(1.45f, 0.05f, 0, 0.22f, 0, LookTint.Glass),
            Cyl(0.3f, 0.6f, 0, 0.25f, 0, LookTint.Stone),
            Cyl(0.6f, 0.12f, 0, 0.75f, 0, LookTint.Stone, 0.85f),
            Ball(0.22f, 0, 1.0f, 0, LookTint.Glass),
        ],
        ["neutral_beach_cabin"] =
        [
            Box(0.7f, 0.85f, 0.7f, 0, 0, 0, LookTint.White),
            Box(0.72f, 0.15f, 0.72f, 0, 0.35f, 0, LookTint.Blue),
            Pyramid(0.85f, 0.3f, 0, 0.85f, 0, LookTint.Red),
            Box(0.3f, 0.55f, 0.03f, 0, 0, 0.36f, LookTint.Wood),
        ],
        ["neutral_beach_bar"] =
        [
            Box(2.0f, 0.45f, 0.5f, 0, 0, 0.3f, LookTint.Wood),
            Box(0.1f, 1.0f, 0.1f, -1.2f, 0, -0.7f, LookTint.Wood),
            Box(0.1f, 1.0f, 0.1f, 1.2f, 0, -0.7f, LookTint.Wood),
            Box(0.1f, 1.0f, 0.1f, -1.2f, 0, 0.7f, LookTint.Wood),
            Box(0.1f, 1.0f, 0.1f, 1.2f, 0, 0.7f, LookTint.Wood),
            Pyramid(2.8f, 0.55f, 0, 1.0f, 0, LookTint.Straw),
            Cyl(0.18f, 0.3f, -0.6f, 0, 0.75f, LookTint.Red),
            Cyl(0.18f, 0.3f, 0.6f, 0, 0.75f, LookTint.Red),
        ],
        ["neutral_lifeguard_tower"] =
        [
            Box(0.08f, 1.0f, 0.08f, -0.4f, 0, -0.4f, LookTint.White),
            Box(0.08f, 1.0f, 0.08f, 0.4f, 0, -0.4f, LookTint.White),
            Box(0.08f, 1.0f, 0.08f, -0.4f, 0, 0.4f, LookTint.White),
            Box(0.08f, 1.0f, 0.08f, 0.4f, 0, 0.4f, LookTint.White),
            Box(0.95f, 0.5f, 0.95f, 0, 1.0f, 0, LookTint.White),
            Box(0.97f, 0.12f, 0.97f, 0, 1.2f, 0, LookTint.Red),
            Pyramid(1.1f, 0.3f, 0, 1.5f, 0, LookTint.Red),
            Box(0.3f, 1.0f, 0.05f, 0, 0.5f, 0.7f, LookTint.Wood, -20f),
        ],
        ["neutral_lighthouse"] =
        [
            Cyl(1.5f, 0.3f, 0, 0, 0, LookTint.Stone),
            Cyl(0.9f, 2.8f, 0, 0.3f, 0, LookTint.White, 0.6f),
            Cyl(0.8f, 0.35f, 0, 1.3f, 0, LookTint.Red, 0.74f),
            Cyl(0.66f, 0.3f, 0, 2.3f, 0, LookTint.Red, 0.62f),
            Cyl(0.6f, 0.35f, 0, 3.1f, 0, LookTint.Glow),
            Cone(0.72f, 0.3f, 0, 3.45f, 0, LookTint.Red),
        ],
        ["neutral_boat_shed"] =
        [
            Box(1.8f, 0.75f, 1.6f, 0, 0, 0, LookTint.Wood),
            Box(1.9f, 0.08f, 0.95f, 0, 0.85f, -0.4f, LookTint.Roof, -28f),
            Box(1.9f, 0.08f, 0.95f, 0, 0.85f, 0.4f, LookTint.Roof, 28f),
            Box(1.0f, 0.6f, 0.05f, 0, 0, 0.81f, LookTint.Dark),
        ],
        ["neutral_adobe_house"] =
        [
            Box(1.7f, 0.7f, 1.7f, 0, 0, 0, LookTint.Clay),
            Box(0.9f, 0.45f, 0.9f, -0.35f, 0.7f, -0.35f, LookTint.Clay),
            Box(0.05f, 0.05f, 0.3f, -0.6f, 0.95f, 0.15f, LookTint.Wood),
            Box(0.05f, 0.05f, 0.3f, -0.1f, 0.95f, 0.15f, LookTint.Wood),
            Box(0.35f, 0.45f, 0.05f, 0.3f, 0, 0.86f, LookTint.Dark),
            Box(0.25f, 0.2f, 0.05f, -0.4f, 0.35f, 0.86f, LookTint.Dark),
        ],
        ["neutral_ancient_ruins"] =
        [
            Box(1.9f, 0.12f, 1.9f, 0, 0, 0, LookTint.Stone),
            Cyl(0.26f, 1.1f, -0.6f, 0.12f, -0.5f, LookTint.Stone),
            Cyl(0.26f, 1.1f, 0.6f, 0.12f, -0.5f, LookTint.Stone),
            Box(1.5f, 0.2f, 0.32f, 0, 1.22f, -0.5f, LookTint.Stone),
            Cyl(0.26f, 0.45f, -0.6f, 0.12f, 0.5f, LookTint.Stone),
            Box(0.5f, 0.2f, 0.3f, 0.4f, 0.12f, 0.5f, LookTint.Stone, 0f, 30f),
        ],
        ["neutral_rock_mesa"] =
        [
            Box(1.9f, 1.1f, 1.9f, 0, 0, 0, LookTint.Rock),
            Box(1.5f, 0.35f, 1.4f, 0.1f, 1.1f, -0.1f, LookTint.Rock),
            Ball(0.5f, 0.75f, 0.15f, 0.75f, LookTint.Rock),
            Box(1.92f, 0.08f, 1.92f, 0, 0.55f, 0, LookTint.Clay),
        ],
        ["neutral_rock_arch"] =
        [
            Box(0.5f, 1.2f, 0.8f, -0.7f, 0, 0, LookTint.Rock),
            Box(0.5f, 1.2f, 0.8f, 0.7f, 0, 0, LookTint.Rock),
            Box(1.9f, 0.4f, 0.8f, 0, 1.2f, 0, LookTint.Rock),
        ],
        ["neutral_port_crane"] =
        [
            Box(0.9f, 0.3f, 0.9f, 0, 0, 0, LookTint.Accent),
            Box(0.22f, 2.2f, 0.22f, 0, 0.3f, 0, LookTint.Accent),
            Box(0.4f, 0.35f, 0.4f, 0, 2.4f, 0, LookTint.Dark),
            Box(0.18f, 0.14f, 2.0f, 0, 2.6f, 0.45f, LookTint.Accent, -8f),
        ],
        ["neutral_container_stack"] =
        [
            Box(1.9f, 0.45f, 0.85f, 0, 0, 0, LookTint.Red),
            Box(1.9f, 0.45f, 0.85f, 0, 0.45f, 0, LookTint.Blue),
            Box(0.95f, 0.45f, 0.85f, -0.47f, 0.9f, 0, LookTint.Accent),
        ],
        ["neutral_port_warehouse"] =
        [
            Box(2.8f, 1.2f, 2.8f, 0, 0, 0, LookTint.Metal),
            Tube(2.6f, 2.8f, 0, 1.2f, 0, LookTint.Roof, 90f, 90f),
            Box(1.2f, 0.9f, 0.05f, 0, 0, 1.41f, LookTint.Dark),
        ],
        ["neutral_water_tower"] =
        [
            Box(0.08f, 1.4f, 0.08f, -0.3f, 0, -0.3f, LookTint.Metal),
            Box(0.08f, 1.4f, 0.08f, 0.3f, 0, -0.3f, LookTint.Metal),
            Box(0.08f, 1.4f, 0.08f, -0.3f, 0, 0.3f, LookTint.Metal),
            Box(0.08f, 1.4f, 0.08f, 0.3f, 0, 0.3f, LookTint.Metal),
            Cyl(0.85f, 0.6f, 0, 1.4f, 0, LookTint.Metal),
            Cone(0.9f, 0.25f, 0, 2.0f, 0, LookTint.Roof),
        ],
        ["neutral_farmhouse"] =
        [
            Box(1.6f, 0.7f, 1.4f, 0, 0, -0.1f, LookTint.Body),
            Box(1.7f, 0.1f, 0.85f, 0, 0.88f, -0.42f, LookTint.Roof, -30f),
            Box(1.7f, 0.1f, 0.85f, 0, 0.88f, 0.22f, LookTint.Roof, 30f),
            Box(0.18f, 0.5f, 0.18f, 0.55f, 0.8f, -0.3f, LookTint.Trim),
            Box(0.9f, 0.05f, 0.3f, 0, 0.1f, 0.78f, LookTint.Wood),
            Box(0.3f, 0.45f, 0.05f, 0, 0, 0.61f, LookTint.Wood),
        ],
        ["neutral_barn"] =
        [
            Box(2.6f, 1.0f, 1.7f, 0, 0, 0, LookTint.Red),
            Box(2.7f, 0.1f, 1.0f, 0, 1.2f, -0.42f, LookTint.Trim, -32f),
            Box(2.7f, 0.1f, 1.0f, 0, 1.2f, 0.42f, LookTint.Trim, 32f),
            Box(0.8f, 0.75f, 0.05f, 0, 0, 0.86f, LookTint.White),
            Box(0.4f, 0.3f, 0.05f, 0, 0.82f, 0.86f, LookTint.White),
        ],
        ["neutral_silo"] =
        [
            Cyl(0.8f, 2.0f, 0, 0, 0, LookTint.Concrete),
            Dome(0.8f, 0, 2.0f, 0, LookTint.Metal),
            Box(0.06f, 1.8f, 0.06f, 0.42f, 0.1f, 0.05f, LookTint.Dark),
        ],
        ["neutral_windmill"] =
        [
            Cyl(1.3f, 1.6f, 0, 0, 0, LookTint.Body, 0.75f),
            Pyramid(0.8f, 0.5f, 0, 1.6f, 0, LookTint.Roof),
            Box(0.14f, 1.7f, 0.04f, 0, 0.9f, 0.48f, LookTint.Wood),
            Box(1.7f, 0.14f, 0.04f, 0, 1.68f, 0.48f, LookTint.Wood),
            Box(0.3f, 0.45f, 0.05f, 0, 0, 0.6f, LookTint.Dark),
        ],
        // The outpost of RA2: a concrete pad, a block with the missile turret on it, the repair circle and its crane.
        ["tech_outpost"] =
        [
            Box(3.9f, 0.15f, 2.9f, 0, 0, 0, LookTint.Concrete),
            Box(1.6f, 0.6f, 1.2f, -1.1f, 0.15f, -0.8f, LookTint.Body),
            Cyl(0.6f, 0.3f, -1.1f, 0.75f, -0.8f, LookTint.Metal),
            Box(0.5f, 0.2f, 0.32f, -1.1f, 1.05f, -0.8f, LookTint.Dark),
            Cyl(1.6f, 0.04f, 0.9f, 0.15f, 0.35f, LookTint.Accent),
            Box(0.15f, 1.1f, 0.15f, 1.75f, 0.15f, -1.3f, LookTint.Metal),
            Box(1.6f, 0.12f, 0.15f, 1.0f, 1.25f, -1.3f, LookTint.Metal),
        ],

        // The Soviets.
        ["construction_yard"] =
        [
            Box(2.8f, 0.35f, 2.8f, 0, 0, 0, LookTint.Body),
            Box(1.6f, 0.8f, 1.4f, -0.4f, 0.35f, -0.45f, LookTint.Roof),
            Box(1.7f, 0.1f, 1.5f, -0.4f, 1.15f, -0.45f, LookTint.Trim),
            Box(0.4f, 0.4f, 0.04f, -0.4f, 0.55f, 0.27f, LookTint.Red),
            Box(0.18f, 1.7f, 0.18f, 0.9f, 0.35f, -0.9f, LookTint.Accent),
            Box(1.9f, 0.12f, 0.16f, 0.15f, 2.0f, -0.9f, LookTint.Accent),
            Cyl(0.4f, 0.55f, 0.85f, 0.35f, 0.8f, LookTint.Metal),
        ],
        ["reactor"] =
        [
            Box(1.8f, 0.35f, 1.8f, 0, 0, 0, LookTint.Body),
            Cyl(1.0f, 0.7f, 0, 0.35f, 0, LookTint.Metal),
            Ring(1.15f, 0.08f, 0, 0.6f, 0, LookTint.Glow),
            Ring(1.15f, 0.08f, 0, 0.9f, 0, LookTint.Glow),
            Cyl(0.35f, 0.5f, 0, 1.05f, 0, LookTint.Trim),
            Ball(0.45f, 0, 1.7f, 0, LookTint.Glow),
        ],
        ["refinery"] =
        [
            Box(2.8f, 0.35f, 2.8f, 0, 0, 0, LookTint.Body),
            Box(1.5f, 0.7f, 1.3f, -0.6f, 0.35f, -0.6f, LookTint.Roof),
            Cyl(0.8f, 1.1f, 0.8f, 0.35f, -0.8f, LookTint.Metal),
            Cone(0.8f, 0.3f, 0.8f, 1.45f, -0.8f, LookTint.Metal),
            Cyl(0.25f, 1.5f, -1.1f, 0.35f, 0.7f, LookTint.Trim),
            Box(0.35f, 0.12f, 1.0f, 0, 0.5f, 0.75f, LookTint.Accent),
        ],
        ["barracks"] =
        [
            Box(1.8f, 0.3f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(1.4f, 0.55f, 1.2f, 0.1f, 0.3f, -0.1f, LookTint.Roof),
            Box(1.5f, 0.08f, 1.3f, 0.1f, 0.85f, -0.1f, LookTint.Trim),
            Box(0.45f, 0.4f, 0.08f, 0.1f, 0.3f, 0.54f, LookTint.Trim),
            .. Flag(-0.75f, 0.3f, -0.75f, LookTint.Red),
        ],
        ["war_factory"] =
        [
            Box(2.8f, 0.3f, 3.8f, 0, 0, 0, LookTint.Body),
            Box(2.4f, 0.9f, 3.0f, 0, 0.3f, -0.3f, LookTint.Roof),
            Box(2.4f, 0.22f, 0.55f, 0, 1.15f, -1.3f, LookTint.Trim, -30f),
            Box(2.4f, 0.22f, 0.55f, 0, 1.15f, -0.3f, LookTint.Trim, -30f),
            Box(2.4f, 0.22f, 0.55f, 0, 1.15f, 0.7f, LookTint.Trim, -30f),
            Box(1.2f, 0.7f, 0.08f, 0, 0.3f, 1.24f, LookTint.Trim),
            Cyl(0.25f, 1.0f, 1.0f, 1.2f, -1.6f, LookTint.Metal),
        ],
        ["radar"] =
        [
            Box(1.8f, 0.3f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(0.8f, 0.5f, 0.8f, -0.4f, 0.3f, 0.4f, LookTint.Roof),
            Box(0.2f, 1.3f, 0.2f, 0.35f, 0.3f, -0.3f, LookTint.Metal),
            Dish(0.85f, 0.35f, 1.7f, -0.3f, 50f),
        ],
        ["tech_center"] =
        [
            Box(2.8f, 0.4f, 2.8f, 0, 0, 0, LookTint.Body),
            Box(2.0f, 0.7f, 1.6f, 0, 0.4f, -0.3f, LookTint.Roof),
            Dome(1.3f, 0, 1.1f, -0.3f, LookTint.Metal),
            Cyl(0.06f, 1.2f, 1.0f, 0.4f, 0.9f, LookTint.Metal),
            Ball(0.18f, 1.0f, 1.65f, 0.9f, LookTint.Red),
            Dish(0.6f, -1.0f, 0.6f, 0.9f),
        ],
        ["wall"] = [Box(0.96f, 0.5f, 0.96f, 0, 0, 0, LookTint.Concrete), Box(0.96f, 0.06f, 0.96f, 0, 0.5f, 0, LookTint.Body)],
        ["sentry_bunker"] =
        [
            Box(0.86f, 0.35f, 0.86f, 0, 0, 0, LookTint.Concrete),
            Box(0.6f, 0.15f, 0.6f, 0, 0.35f, 0, LookTint.Body),
            Tube(0.09f, 0.5f, 0, 0.42f, 0.4f, LookTint.Dark, 90f),
        ],
        // RA2 psychic sensor: a low block with a dish antenna and a glowing emitter.
        ["psychic_sensor"] =
        [
            Box(1.8f, 0.4f, 1.8f, 0, 0, 0, LookTint.Body),
            Cyl(0.18f, 0.9f, 0, 0.4f, 0, LookTint.Metal),
            Cone(1.1f, 0.3f, 0, 1.3f, 0, LookTint.Metal),
            Ball(0.3f, 0, 1.75f, 0, LookTint.Glow),
            Box(0.5f, 0.3f, 0.5f, -0.55f, 0.4f, 0.55f, LookTint.Roof),
        ],
        ["tesla_coil"] =
        [
            Box(1.6f, 0.25f, 1.6f, 0, 0, 0, LookTint.Body),
            Cyl(0.7f, 1.5f, 0, 0.25f, 0, LookTint.Metal, 0.35f),
            Ring(0.95f, 0.08f, 0, 0.6f, 0, LookTint.Glow),
            Ring(0.8f, 0.08f, 0, 1.0f, 0, LookTint.Glow),
            Ring(0.65f, 0.08f, 0, 1.4f, 0, LookTint.Glow),
            Ball(0.45f, 0, 1.95f, 0, LookTint.Glow),
        ],
        ["flak_cannon"] =
        [
            Cyl(1.6f, 0.3f, 0, 0, 0, LookTint.Concrete),
            Cyl(0.9f, 0.35f, 0, 0.3f, 0, LookTint.Body),
            Tube(0.1f, 0.8f, -0.12f, 0.95f, 0.15f, LookTint.Dark, 25f),
            Tube(0.1f, 0.8f, 0.12f, 0.95f, 0.15f, LookTint.Dark, 25f),
        ],
        ["naval_shipyard"] =
        [
            Box(3.8f, 0.25f, 0.5f, 0, 0, -1.65f, LookTint.Concrete),
            Box(0.5f, 0.25f, 3.3f, -1.65f, 0, 0.25f, LookTint.Concrete),
            Box(0.5f, 0.25f, 3.3f, 1.65f, 0, 0.25f, LookTint.Concrete),
            Box(1.2f, 0.7f, 0.45f, -1.0f, 0.25f, -1.65f, LookTint.Roof),
            Box(0.2f, 1.4f, 0.2f, -1.65f, 0.25f, 1.2f, LookTint.Body),
            Box(0.2f, 1.4f, 0.2f, 1.65f, 0.25f, 1.2f, LookTint.Body),
            Box(3.5f, 0.22f, 0.3f, 0, 1.65f, 1.2f, LookTint.Body),
        ],
        ["service_depot"] =
        [
            Box(2.7f, 0.15f, 2.7f, 0, 0, 0, LookTint.Body),
            Cyl(2.0f, 0.04f, 0, 0.15f, 0, LookTint.Accent),
            Cyl(1.7f, 0.05f, 0, 0.15f, 0, LookTint.Concrete),
            Box(0.18f, 1.0f, 0.18f, -1.15f, 0.15f, -1.15f, LookTint.Metal),
            Box(0.18f, 1.0f, 0.18f, 1.15f, 0.15f, -1.15f, LookTint.Metal),
            Box(2.5f, 0.16f, 0.2f, 0, 1.15f, -1.15f, LookTint.Metal),
            Box(0.45f, 0.14f, 0.12f, 0, 1.35f, -1.15f, LookTint.Green),
            Box(0.14f, 0.45f, 0.12f, 0, 1.2f, -1.15f, LookTint.Green),
        ],
        ["nuclear_reactor"] =
        [
            Box(3.8f, 0.35f, 3.8f, 0, 0, 0, LookTint.Body),
            Cyl(1.3f, 1.7f, -0.9f, 0.35f, -0.8f, LookTint.Concrete, 0.95f),
            Cyl(1.3f, 1.7f, 0.9f, 0.35f, -0.8f, LookTint.Concrete, 0.95f),
            Box(1.8f, 0.6f, 1.0f, 0, 0.35f, 1.0f, LookTint.Roof),
            Box(0.9f, 0.2f, 0.05f, 0, 0.55f, 1.52f, LookTint.Green),
            Dome(0.8f, 0, 0.95f, 1.0f, LookTint.Metal),
        ],
        // RA2 cloning vats: four glass vats with a glow inside on a low block.
        ["cloning_vats"] =
        [
            Box(1.9f, 0.3f, 1.9f, 0, 0, 0, LookTint.Body),
            Cyl(0.55f, 0.8f, -0.45f, 0.3f, -0.45f, LookTint.Glass),
            Cyl(0.55f, 0.8f, 0.45f, 0.3f, -0.45f, LookTint.Glass),
            Cyl(0.55f, 0.8f, -0.45f, 0.3f, 0.45f, LookTint.Glass),
            Cyl(0.55f, 0.8f, 0.45f, 0.3f, 0.45f, LookTint.Glass),
            Ball(0.3f, 0, 0.7f, 0, LookTint.Glow),
        ],
        ["iron_curtain"] =
        [
            Box(2.8f, 0.35f, 2.8f, 0, 0, 0, LookTint.Body),
            Box(2.2f, 0.3f, 1.4f, 0, 0.35f, 0, LookTint.Roof),
            Box(0.35f, 1.6f, 0.5f, -0.95f, 0.65f, 0, LookTint.Trim),
            Box(0.35f, 1.6f, 0.5f, 0.95f, 0.65f, 0, LookTint.Trim),
            Box(2.3f, 0.35f, 0.6f, 0, 2.25f, 0, LookTint.Trim),
            Box(1.5f, 1.55f, 0.08f, 0, 0.68f, 0, LookTint.Red),
        ],
        ["nuclear_missile_silo"] =
        [
            Box(2.8f, 0.3f, 2.8f, 0, 0, 0, LookTint.Body),
            Cyl(1.9f, 0.2f, 0, 0.3f, 0.1f, LookTint.Concrete),
            Ring(1.9f, 0.1f, 0, 0.5f, 0.1f, LookTint.Accent),
            Box(0.8f, 0.08f, 1.5f, -0.42f, 0.5f, 0.1f, LookTint.Trim),
            Box(0.8f, 0.08f, 1.5f, 0.42f, 0.5f, 0.1f, LookTint.Trim),
            Box(0.7f, 0.5f, 0.6f, -1.0f, 0.3f, -1.0f, LookTint.Roof),
        ],
    };
}
