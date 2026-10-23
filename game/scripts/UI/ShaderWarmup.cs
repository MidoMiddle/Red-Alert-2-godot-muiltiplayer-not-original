using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Rts.Client.UI;

/// <summary>
/// Draws one frame with every material of the match and with the kinds the effects make, in a viewport that nobody sees, so the
/// shaders are compiled before the first blast and not during it (docs/implementation/M6_menus_skirmish.md, 4.14). The materials
/// of the scene are taken from the scene itself. The effects make theirs when a shot fires (<c>CombatEffects</c>,
/// <c>ProjectileViews</c>), so their kinds are made here: unshaded and transparent for blasts and stains, vertex-coloured for the
/// lines of tracers, lit and unshaded for the vehicles and the marks.
/// </summary>
public static class ShaderWarmup
{
    /// <summary>Frames to wait: the first draws the viewport, the second is the one the viewport is read in.</summary>
    private const int FramesToWait = 2;

    private const int ViewSize = 128;
    private const int Columns = 10;
    private const float Spacing = 1.1f;

    /// <summary>Draws the materials once and frees the viewport again. <paramref name="owner"/> must be in the tree.</summary>
    public static async Task RunAsync(Node owner, Node scene)
    {
        var materials = new HashSet<Material>();
        Collect(scene, materials);

        var instances = new List<MeshInstance3D>();
        foreach (var material in materials)
        {
            instances.Add(new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = material });
        }

        instances.Add(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.5f, Height = 1f }, MaterialOverride = Flat() });
        instances.Add(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = 0.02f }, MaterialOverride = Flat() });
        instances.Add(new MeshInstance3D { Mesh = Line(), MaterialOverride = LineMaterial() });
        instances.Add(new MeshInstance3D { Mesh = new BoxMesh(), MaterialOverride = new StandardMaterial3D() });
        instances.Add(new MeshInstance3D { Mesh = new CapsuleMesh(), MaterialOverride = new StandardMaterial3D { ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded } });

        var viewport = new SubViewport
        {
            Size = new Vector2I(ViewSize, ViewSize),
            RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            OwnWorld3D = true,
        };
        for (var i = 0; i < instances.Count; i++)
        {
            // Every item in view of the camera, on a grid; more items than cells share a cell, which costs nothing extra.
            var cell = i % (Columns * Columns);
            instances[i].Position = new Vector3(((cell % Columns) - (Columns - 1) / 2f) * Spacing, (cell / Columns - (Columns - 1) / 2f) * Spacing, 0f);
            viewport.AddChild(instances[i]);
        }

        viewport.AddChild(new DirectionalLight3D
        {
            Basis = Basis.LookingAt(Main.SunDirection, Vector3.Up),
            ShadowEnabled = true,
        });
        var camera = new Camera3D
        {
            Projection = Camera3D.ProjectionType.Orthogonal,
            Size = Columns * Spacing,
            Position = new Vector3(0f, 0f, 10f),
        };
        viewport.AddChild(camera);

        owner.AddChild(viewport);
        camera.MakeCurrent();
        for (var frame = 0; frame < FramesToWait; frame++)
        {
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }

        viewport.QueueFree();
        GD.Print($"[loading] shaders warmed up: {instances.Count} draws, {materials.Count} materials of the scene");
    }

    /// <summary>Every material the scene uses: the overrides of the nodes and the materials of their meshes.</summary>
    private static void Collect(Node node, HashSet<Material> materials)
    {
        if (node is GeometryInstance3D { MaterialOverride: { } overridden })
        {
            materials.Add(overridden);
        }

        if (node is MeshInstance3D { Mesh: { } mesh })
        {
            for (var surface = 0; surface < mesh.GetSurfaceCount(); surface++)
            {
                if (mesh.SurfaceGetMaterial(surface) is { } material)
                {
                    materials.Add(material);
                }
            }
        }

        foreach (var child in node.GetChildren())
        {
            Collect(child, materials);
        }
    }

    /// <summary>The blasts and the stains of <c>CombatEffects</c>: flat, unshaded, transparent.</summary>
    private static StandardMaterial3D Flat() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        AlbedoColor = new Color(1f, 1f, 1f, 0.5f),
    };

    /// <summary>The tracers and the lightning of <c>CombatEffects</c>: lines with a colour on every vertex.</summary>
    private static StandardMaterial3D LineMaterial() => new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        VertexColorUseAsAlbedo = true,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
    };

    private static ImmediateMesh Line()
    {
        var mesh = new ImmediateMesh();
        mesh.SurfaceBegin(Mesh.PrimitiveType.Lines);
        mesh.SurfaceSetColor(Colors.Yellow);
        mesh.SurfaceAddVertex(new Vector3(-0.5f, 0f, 0f));
        mesh.SurfaceSetColor(Colors.Yellow);
        mesh.SurfaceAddVertex(new Vector3(0.5f, 0f, 0f));
        mesh.SurfaceEnd();
        return mesh;
    }
}
