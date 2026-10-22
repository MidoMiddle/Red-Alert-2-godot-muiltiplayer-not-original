using System.Collections.Generic;
using Godot;
using Rts.Simulation.Map;

namespace Rts.Client.Terrain;

/// <summary>
/// The ore of a match (docs/implementation/M3_base_economy.md, 4.12, step 9): a yellow diamond on every cell of ore and a blue one on
/// every cell of gems, larger the higher the stage. Rebuilt when the ore changes (<see cref="MapGrid.OreVersion"/>): it grows every
/// 30 seconds and the harvesters of step 10 take it.
/// </summary>
public partial class OreField : Node3D
{
    public static readonly Color OreColor = new(0.95f, 0.8f, 0.15f);
    public static readonly Color GemColor = new(0.35f, 0.8f, 1f);

    private object? _builtMap;
    private int _builtVersion = -1;

    public override void _Process(double delta)
    {
        var map = GetParent<TerrainBuilder>().Map;
        // The editor draws its own ore (EditorMarks), from the map it edits.
        if (GetNodeOrNull<SimulationHost>("../../SimulationHost") is null || WorldCoords.Terrain is not { } terrain
            || (ReferenceEquals(map, _builtMap) && map.OreVersion == _builtVersion))
        {
            return;
        }

        _builtMap = map;
        _builtVersion = map.OreVersion;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var ore = new List<Transform3D>();
        var gems = new List<Transform3D>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var cell = map.OreAt(new Rts.Simulation.Geometry.CellPos(x, y));
                if (!cell.IsEmpty)
                {
                    var gem = cell.Kind == OreKind.Gems;
                    var size = (gem ? 0.2f : 0.12f) + (cell.Stage + 1) * (gem ? 0.02f : 0.018f);
                    (gem ? gems : ore).Add(Diamond(terrain, x, y, size));
                }
            }
        }

        AddDiamonds(this, "Ore", ore, OreColor);
        AddDiamonds(this, "Gems", gems, GemColor);
    }

    /// <summary>A diamond of <paramref name="size"/> standing on the middle of the cell.</summary>
    public static Transform3D Diamond(TerrainHeights terrain, int x, int y, float size)
    {
        var at = new Vector3(x + 0.5f, terrain.HeightAt(x + 0.5f, y + 0.5f) + size, y + 0.5f);
        return new Transform3D(new Basis(Vector3.Forward, Mathf.Pi / 4f).Scaled(new Vector3(size, size, size)), at);
    }

    /// <summary>Adds the diamonds under <paramref name="parent"/> as one multi-mesh.</summary>
    public static void AddDiamonds(Node3D parent, string name, List<Transform3D> transforms, Color color)
    {
        if (transforms.Count == 0)
        {
            return;
        }

        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            Mesh = new BoxMesh { Size = Vector3.One, Material = new StandardMaterial3D { AlbedoColor = color } },
            InstanceCount = transforms.Count,
        };
        for (var i = 0; i < transforms.Count; i++)
        {
            multiMesh.SetInstanceTransform(i, transforms[i]);
        }

        parent.AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multiMesh });
    }
}
