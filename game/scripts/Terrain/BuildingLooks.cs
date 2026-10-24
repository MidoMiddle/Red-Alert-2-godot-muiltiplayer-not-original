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
    /// <summary>The colours of a faction: the Allies light, the Soviets rust red.</summary>
    public static (float R, float G, float B) Palette(LookTint tint, bool soviet) => tint switch
    {
        LookTint.Body => soviet ? (0.56f, 0.33f, 0.27f) : (0.8f, 0.82f, 0.85f),
        LookTint.Roof => soviet ? (0.36f, 0.22f, 0.19f) : (0.4f, 0.48f, 0.6f),
        LookTint.Trim => soviet ? (0.24f, 0.19f, 0.17f) : (0.28f, 0.3f, 0.34f),
        LookTint.Metal => soviet ? (0.5f, 0.47f, 0.44f) : (0.62f, 0.64f, 0.68f),
        LookTint.Glass => soviet ? (0.95f, 0.7f, 0.35f) : (0.5f, 0.75f, 0.95f),
        LookTint.Accent => (0.95f, 0.78f, 0.18f),
        LookTint.Glow => (0.45f, 0.8f, 1f),
        LookTint.Concrete => (0.63f, 0.62f, 0.59f),
        LookTint.White => (0.95f, 0.95f, 0.95f),
        LookTint.Red => (0.85f, 0.15f, 0.12f),
        LookTint.Green => (0.35f, 0.95f, 0.4f),
        LookTint.Dark => (0.14f, 0.14f, 0.16f),
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
        ["alliance_robot_control_center"] =
        [
            Box(1.8f, 0.35f, 1.8f, 0, 0, 0, LookTint.Body),
            Box(1.2f, 0.45f, 1.2f, 0, 0.35f, 0, LookTint.Roof),
            Pyramid(1.1f, 0.55f, 0, 0.8f, 0, LookTint.Glass),
            Cyl(0.05f, 0.75f, -0.72f, 0.35f, -0.72f, LookTint.Metal),
            Cyl(0.05f, 0.75f, 0.72f, 0.35f, -0.72f, LookTint.Metal),
            Ball(0.13f, -0.72f, 1.12f, -0.72f, LookTint.Red),
            Ball(0.13f, 0.72f, 1.12f, -0.72f, LookTint.Red),
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
        ["industrial_plant"] =
        [
            Box(2.8f, 0.35f, 2.8f, 0, 0, 0, LookTint.Body),
            Box(2.0f, 0.8f, 1.6f, -0.2f, 0.35f, -0.4f, LookTint.Roof),
            Cyl(0.3f, 1.1f, -0.85f, 1.15f, -0.9f, LookTint.Metal),
            Cyl(0.3f, 1.1f, -0.25f, 1.15f, -0.9f, LookTint.Metal),
            Cyl(0.3f, 1.1f, 0.35f, 1.15f, -0.9f, LookTint.Metal),
            Tube(0.9f, 0.18f, 0.95f, 0.85f, 0.6f, LookTint.Accent, 90f),
        ],
        ["battle_bunker"] =
        [
            Box(1.8f, 0.45f, 1.8f, 0, 0, 0, LookTint.Concrete),
            Box(1.4f, 0.15f, 1.4f, 0, 0.45f, 0, LookTint.Body),
            Box(1.2f, 0.08f, 0.05f, 0, 0.26f, 0.91f, LookTint.Dark),
            Box(0.05f, 0.08f, 1.2f, 0.91f, 0.26f, 0, LookTint.Dark),
        ],
    };
}
