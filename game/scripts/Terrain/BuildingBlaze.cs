using System;
using System.Collections.Generic;
using Godot;
using Rts.Simulation.Combat;

namespace Rts.Client.Terrain;

/// <summary>
/// The smoke and the fire of a hurt building (docs/04_gameplay.md: under half of its hit points it smokes, under a quarter it burns).
/// The smoke rises off the roof. The fire stands in the nodes <c>fire_*</c> of its model, where Blender sets the flames (the windows,
/// the broken edges); a building without such nodes, or a placeholder, burns off the roof. The particles run once they are in the
/// tree, so a view makes them when it is built, and again when the building changes its state.
/// </summary>
public static class BuildingBlaze
{
    private const string FireNode = "fire_";

    private static readonly Color SmokeColor = new(0.25f, 0.25f, 0.25f);
    private static readonly Color FireColor = new(1f, 0.45f, 0.1f);

    /// <summary>
    /// What a building in <paramref name="state"/> shows: nothing while it is whole, the smoke over <paramref name="top"/> once it is
    /// damaged, the fire too when it is critical. <paramref name="model"/> is its model, or null for a placeholder; its footprint is
    /// <paramref name="width"/> by <paramref name="height"/> cells.
    /// </summary>
    public static void Add(Node3D parent, Node3D? model, Vector3 top, int width, int height, DamageState state)
    {
        if (state is not (DamageState.Damaged or DamageState.Critical))
        {
            return;
        }

        Start(parent, Plume(width, height, SmokeColor, 0.12f, 1.6, 0.6f), top);
        if (state != DamageState.Critical)
        {
            return;
        }

        var flames = new List<Node3D>();
        if (model is not null)
        {
            Anchors(model, flames);
        }

        if (flames.Count == 0)
        {
            Start(parent, Plume(width, height, FireColor, 0.09f, 0.6, 0.9f), top);
        }

        foreach (var flame in flames)
        {
            Start(flame, Plume(0, 0, FireColor, 0.09f, 0.6, 0.9f), Vector3.Zero);
        }
    }

    /// <summary>Particles that rise off a footprint, or off one point when it is 0 by 0: smoke, or the flames of a fire.</summary>
    internal static CpuParticles3D Plume(int width, int height, Color color, float size, double lifetime, float speed) =>
        new()
        {
            Emitting = false,
            Amount = 16,
            Lifetime = lifetime,
            LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Box,
            EmissionBoxExtents = new Vector3(width / 4f, 0.05f, height / 4f),
            Direction = Vector3.Up,
            Spread = 15f,
            InitialVelocityMin = speed * 0.7f,
            InitialVelocityMax = speed,
            Gravity = Vector3.Zero,
            ScaleAmountMin = 0.8f,
            ScaleAmountMax = 1.6f,
            Mesh = new SphereMesh { Radius = size, Height = size * 2, Material = new StandardMaterial3D { AlbedoColor = color } },
        };

    // Puts the particles on their place under the parent and starts them: they need their place in the world first.
    private static void Start(Node parent, CpuParticles3D particles, Vector3 at)
    {
        particles.Position = at;
        parent.AddChild(particles);
        particles.Emitting = true;
    }

    // The nodes fire_* below a model, in the order of the tree.
    private static void Anchors(Node node, List<Node3D> found)
    {
        if (node is Node3D spatial && node.Name.ToString().StartsWith(FireNode, StringComparison.Ordinal))
        {
            found.Add(spatial);
        }

        foreach (var child in node.GetChildren())
        {
            Anchors(child, found);
        }
    }
}
