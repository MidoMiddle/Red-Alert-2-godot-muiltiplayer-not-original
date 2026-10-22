using System.Collections.Generic;
using Godot;
using Rts.Client.Maps;
using Rts.Client.Terrain;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;

namespace Rts.Client.Editor;

/// <summary>
/// What only the editor shows (docs/implementation/M7_maps.md, 4.13, step 12): ore as yellow diamonds and gems as blue ones,
/// larger the denser (ore has no rules before M3, so the field and the previews do not draw it yet), and every start as a
/// pillar in its slot's colour with its number. Rebuilt when the map changes.
/// </summary>
public partial class EditorMarks : Node3D
{
    private static readonly Color OreColor = new(0.95f, 0.8f, 0.15f);
    private static readonly Color GemColor = new(0.35f, 0.8f, 1f);

    private int _builtVersion = -1;

    public void Refresh(MapEditSession session)
    {
        if (session.Version == _builtVersion || WorldCoords.Terrain is not { } terrain)
        {
            return;
        }

        _builtVersion = session.Version;
        foreach (var child in GetChildren())
        {
            child.QueueFree();
        }

        var map = session.Map;
        var ore = new List<Transform3D>();
        var gems = new List<Transform3D>();
        for (var y = 0; y < map.Height; y++)
        {
            for (var x = 0; x < map.Width; x++)
            {
                var value = map.OreAt(new CellPos(x, y));
                if (value == 0)
                {
                    continue;
                }

                var density = value > 9 ? value - 9 : value;
                var size = value > 9 ? 0.2f + density * 0.08f : 0.12f + density * 0.025f;
                var at = new Vector3(x + 0.5f, terrain.HeightAt(x + 0.5f, y + 0.5f) + size, y + 0.5f);
                var basis = new Basis(Vector3.Forward, Mathf.Pi / 4f).Scaled(new Vector3(size, size, size));
                (value > 9 ? gems : ore).Add(new Transform3D(basis, at));
            }
        }

        AddDiamonds("Ore", ore, OreColor);
        AddDiamonds("Gems", gems, GemColor);

        foreach (var start in map.Starts)
        {
            var (r, g, b) = MapPreview.SlotColor(start.Slot);
            var ground = terrain.HeightAt(start.Cell.X + 0.5f, start.Cell.Y + 0.5f);
            AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.25f, BottomRadius = 0.35f, Height = 1.6f, Material = new StandardMaterial3D { AlbedoColor = new Color(r, g, b) } },
                Position = new Vector3(start.Cell.X + 0.5f, ground + 0.8f, start.Cell.Y + 0.5f),
            });
            AddChild(new Label3D
            {
                Text = start.Slot.ToString(System.Globalization.CultureInfo.InvariantCulture),
                Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
                FontSize = 96,
                OutlineSize = 16,
                NoDepthTest = true,
                Position = new Vector3(start.Cell.X + 0.5f, ground + 2.1f, start.Cell.Y + 0.5f),
            });
        }
    }

    private void AddDiamonds(string name, List<Transform3D> transforms, Color color)
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

        AddChild(new MultiMeshInstance3D { Name = name, Multimesh = multiMesh });
    }
}
