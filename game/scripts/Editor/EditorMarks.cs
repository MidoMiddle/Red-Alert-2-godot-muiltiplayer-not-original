using System.Collections.Generic;
using Godot;
using Rts.Client.Maps;
using Rts.Client.Terrain;
using Rts.Data.Maps.Editing;
using Rts.Simulation.Geometry;

namespace Rts.Client.Editor;

/// <summary>
/// What the editor shows on its map (docs/implementation/M7_maps.md, 4.13, step 12): ore as yellow diamonds and gems as blue ones,
/// larger the denser (the diamonds of a match, <see cref="OreField"/>, from the density of the file rather than the stage), and every
/// start as a pillar in its slot's colour with its number. Rebuilt when the map changes.
/// </summary>
public partial class EditorMarks : Node3D
{
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
                (value > 9 ? gems : ore).Add(OreField.Diamond(terrain, x, y, size));
            }
        }

        OreField.AddDiamonds(this, "Ore", ore, OreField.OreColor);
        OreField.AddDiamonds(this, "Gems", gems, OreField.GemColor);

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
}
