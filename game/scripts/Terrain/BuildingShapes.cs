using System;
using Rts.Simulation.Geometry;
using Rts.Simulation.Rules;

namespace Rts.Client.Terrain;

/// <summary>
/// The symbolic shape of a building of a player (docs/implementation/conventions.md, section 12; M3_base_economy.md, 4.17): a block
/// over the footprint and only the marks a rule stands behind — a stack on a building that gives power, a dish on a radar (the
/// minimap), a pad on the dock cell of a refinery, a strip on the exit cell of a factory. Walls are low and thick, defenses a little
/// taller than a wall; other buildings grow with their footprint.
/// </summary>
/// <param name="Height">Height of the block above the ground, in cells.</param>
/// <param name="Inset">How far the block stands in from the edge of the footprint.</param>
/// <param name="Stack">A chimney: the building produces power.</param>
/// <param name="Dish">A dish on the roof: the building is a radar.</param>
/// <param name="Dock">The dock cell, relative to the north-west cell, where harvesters unload.</param>
/// <param name="Exit">The exit cell, relative to the north-west cell, where units come out.</param>
public readonly record struct BuildingShape(float Height, float Inset, bool Stack, bool Dish, CellPos? Dock, CellPos? Exit)
{
    public const float WallHeight = 0.5f;
    public const float DefenseHeight = 0.7f;
    public const float Margin = 0.08f;

    public static BuildingShape Of(BuildingDef def)
    {
        var wall = def.Wall;
        var height = wall ? WallHeight
            : def.Category == ProductionCategory.Defense ? DefenseHeight
            : 0.35f + 0.2f * Math.Max(def.Footprint.Width, def.Footprint.Height);
        return new BuildingShape(
            height,
            wall ? 0.02f : Margin,
            def.Power > 0,
            def.Radar,
            def.DockCell,
            def.Produces.Length > 0 ? def.ExitCell : null);
    }

    /// <summary>
    /// How much of the block shows while it grows, 0 to 1, smoothly between ticks: <paramref name="alpha"/> is the share of the next
    /// tick already gone.
    /// </summary>
    public static float Growth(int buildUp, int buildUpLeft, float alpha) =>
        buildUp <= 0 || buildUpLeft <= 0 ? 1f : Math.Clamp((buildUp - buildUpLeft + alpha) / buildUp, 0f, 1f);
}
