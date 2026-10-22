using Godot;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Terrain;

/// <summary>
/// The fog of war on screen (docs/implementation/M4_combat.md, 4.15, M4 step 12). Every frame each cell of the local player's
/// <see cref="VisibilityGrid"/> moves its shade toward shroud, fog or visible (<see cref="FogShades"/>); the shades go into a texture
/// of one texel per cell, which <c>shaders/fog.gdshader</c> lays over the whole picture in one pass, and into a black mask for the
/// minimap. The debug key <c>debug_reveal</c> (F10) shows the whole map — the ground, the enemies, their buildings as they stand —
/// without touching the simulation. Only one exists at a time: <see cref="Current"/>.
/// </summary>
public partial class FogOfWar : Node3D
{
    private const string FogShader = "res://shaders/fog.gdshader";

    /// <summary>How dark the fog is on the minimap, of 255; the shroud is black.</summary>
    private const int MinimapFog = 110;

    private SimulationHost? _host;
    private ShaderMaterial? _material;
    private ImageTexture? _fogTexture;
    private byte[] _shades = [];
    private byte[] _mask = [];
    private Image? _image;
    private Image? _maskImage;
    private GameWorld? _world;

    /// <summary>The fog of the scene, or null where there is none (the map editor, tests).</summary>
    public static FogOfWar? Current { get; private set; }

    /// <summary>The texture of the fog for the minimap: black, opaque on the shroud, half on the fog, clear where it is seen.</summary>
    public ImageTexture? MinimapMask { get; private set; }

    /// <summary>True while the whole map is shown (debug, and for a player who lost the match).</summary>
    public bool Revealed { get; private set; }

    /// <summary>Shows the whole map from now on: a player who lost the match watches the rest of it (M5 step 10).</summary>
    public void Reveal() => Revealed = true;

    /// <summary>
    /// Whether the local player is to see <paramref name="entity"/> on screen: its own always, another's while seen (step 11), all
    /// while the fog is revealed or where there is no fog.
    /// </summary>
    public static bool Shows(GameWorld world, Entity entity) =>
        Current is not { Revealed: false } || entity.Owner == SimulationHost.LocalPlayer || Vision.Sees(world, SimulationHost.LocalPlayer, entity);

    public override void _Ready()
    {
        Current = this;
        _host = GetNodeOrNull<SimulationHost>("../SimulationHost");
        // First of the transparent things (M4 step 13): the pass redraws the opaque picture, so effects, markers and lines drawn after
        // it stay on top. They show only where the player sees anyway.
        _material = new ShaderMaterial { Shader = GD.Load<Shader>(FogShader), RenderPriority = (int)Material.RenderPriorityMin };
        AddChild(new MeshInstance3D
        {
            Name = "FogPass",
            Mesh = new QuadMesh { Size = new Vector2(2, 2), FlipFaces = true },
            MaterialOverride = _material,
            ExtraCullMargin = 16384f,
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    public override void _ExitTree()
    {
        if (Current == this)
        {
            Current = null;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event.IsActionPressed("debug_reveal", false, true))
        {
            Revealed = !Revealed;
            GetViewport().SetInputAsHandled();
        }
    }

    public override void _Process(double delta)
    {
        if (_host is null || _material is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        if (!ReferenceEquals(world, _world))
        {
            Start(world);
        }

        var grid = world.VisionOf(SimulationHost.LocalPlayer);
        var changed = false;
        for (var y = 0; y < grid.Height; y++)
        {
            for (var x = 0; x < grid.Width; x++)
            {
                var index = y * grid.Width + x;
                var target = FogShades.Target(grid.StateAt(new Rts.Simulation.Geometry.CellPos(x, y)), Revealed);
                var shade = _shades[index];
                if (shade == target)
                {
                    continue;
                }

                shade = FogShades.Step(shade, target, delta);
                _shades[index] = shade;
                _mask[index * 4 + 3] = FogShades.MinimapAlpha(shade, MinimapFog);
                changed = true;
            }
        }

        if (changed)
        {
            _image!.SetData(grid.Width, grid.Height, false, Image.Format.R8, _shades);
            _fogTexture!.Update(_image);
            _maskImage!.SetData(grid.Width, grid.Height, false, Image.Format.Rgba8, _mask);
            MinimapMask!.Update(_maskImage);
        }
    }

    /// <summary>A new session: everything starts in the shroud and opens as the player sees.</summary>
    private void Start(GameWorld world)
    {
        _world = world;
        var (width, height) = (world.WidthCells, world.HeightCells);
        _shades = new byte[width * height];
        _mask = new byte[width * height * 4];
        for (var i = 0; i < width * height; i++)
        {
            _mask[i * 4 + 3] = FogShades.MinimapAlpha(FogShades.Shroud, MinimapFog);
        }

        _image = Image.CreateFromData(width, height, false, Image.Format.R8, _shades);
        _maskImage = Image.CreateFromData(width, height, false, Image.Format.Rgba8, _mask);
        MinimapMask = ImageTexture.CreateFromImage(_maskImage);
        _fogTexture = ImageTexture.CreateFromImage(_image);
        _material!.SetShaderParameter("fog_map", _fogTexture);
        _material.SetShaderParameter("map_size", new Vector2(width, height));
    }
}
