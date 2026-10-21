using System;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// Symbolic colour of each land type (docs/implementation/conventions.md, section 12; M2_unit_control.md, 4.3):
/// flat colours that tell the rules at a glance, water blue, rock grey, rough brown. sRGB components 0…1.
/// Pure data without Godot types, tested in Rts.Simulation.Tests.
/// </summary>
public static class LandColors
{
    public static (float R, float G, float B) Of(LandType land) => land switch
    {
        LandType.Clear => (0.62f, 0.66f, 0.58f),
        LandType.Rough => (0.5f, 0.4f, 0.27f),
        LandType.Road => (0.3f, 0.3f, 0.32f),
        LandType.Beach => (0.84f, 0.75f, 0.52f),
        LandType.Shallow => (0.45f, 0.7f, 0.86f),
        LandType.Deep => (0.1f, 0.24f, 0.56f),
        LandType.Rock => (0.47f, 0.47f, 0.5f),
        LandType.Ice => (0.86f, 0.94f, 0.99f),
        _ => throw new ArgumentOutOfRangeException(nameof(land), land, "Unknown land type."),
    };
}
