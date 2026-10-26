using System.Collections.Generic;
using Godot;
using Rts.Simulation.Visibility;
using Rts.Simulation.World;

namespace Rts.Client.Terrain;

/// <summary>
/// The fog of war on screen (docs/implementation/M4_combat.md, 4.15, M4 step 12). Each cell moves its shade toward shroud, fog or visible
/// as the local player's team knows it — the <see cref="VisibilityGrid"/>s of the player and its allies together (M6, A6) — by
/// <see cref="FogShades"/>. What the team knows changes only when a grid does (<see cref="Vision.Version"/>): then (or with the reveal)
/// the targets of all the cells are worked out at once (<see cref="Vision.States"/>), and every frame only the cells on their way to
/// theirs move (the performance log,
/// 2026-10-26: every cell every frame took 6.7 ms on a map of 250 × 250). The shades go into a texture
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
    private byte[] _targets = [];
    private CellVisibility[] _states = [];
    private readonly List<int> _moving = new();
    private bool[] _isMoving = [];
    private long _statesVersion = -1;
    private bool _statesRevealed;
    private PlayerId _statesPlayer;
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

    /// <summary>The fog as the player sees it again: a new match after the one lost (M5 step 12).</summary>
    public void Conceal() => Revealed = false;

    /// <summary>
    /// Whether the local player is to see <paramref name="entity"/> on screen: its own and its allies' always (M6, A6), another's while
    /// its team sees it (step 11), all while the fog is revealed or where there is no fog.
    /// </summary>
    public static bool Shows(GameWorld world, Entity entity) =>
        Current is not { Revealed: false } || world.AreAllies(SimulationHost.LocalPlayer, entity.Owner)
        || Vision.Sees(world, SimulationHost.LocalPlayer, entity);

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
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("FogOfWar.process");
        if (_host is null || _material is null)
        {
            return;
        }

        var world = _host.Simulation.World;
        if (!ReferenceEquals(world, _world))
        {
            Start(world);
        }

        var (width, height) = (world.WidthCells, world.HeightCells);
        // A change of the grids, the reveal or another player to look as (F11, the debug switch).
        if (Vision.Version(world) != _statesVersion || Revealed != _statesRevealed || SimulationHost.LocalPlayer != _statesPlayer)
        {
            Targets(world);
        }

        // Only the cells on their way to their target; one there leaves the list.
        var changed = _moving.Count > 0;
        for (var i = _moving.Count - 1; i >= 0; i--)
        {
            var index = _moving[i];
            var shade = FogShades.Step(_shades[index], _targets[index], delta);
            _shades[index] = shade;
            _mask[index * 4 + 3] = FogShades.MinimapAlpha(shade, MinimapFog);
            if (shade == _targets[index])
            {
                _isMoving[index] = false;
                _moving[i] = _moving[^1];
                _moving.RemoveAt(_moving.Count - 1);
            }
        }

        if (changed)
        {
            using var uploadTimer = Rts.Client.Debug.PerfLog.Time("FogOfWar.upload");
            _image!.SetData(width, height, false, Image.Format.R8, _shades);
            _fogTexture!.Update(_image);
            _maskImage!.SetData(width, height, false, Image.Format.Rgba8, _mask);
            MinimapMask!.Update(_maskImage);
        }
    }

    /// <summary>The targets of every cell after a tick (or the reveal); a cell whose shade is not its target joins the moving ones.</summary>
    private void Targets(GameWorld world)
    {
        using var partTimer = Rts.Client.Debug.PerfLog.Time("FogOfWar.targets");
        (_statesVersion, _statesRevealed, _statesPlayer) = (Vision.Version(world), Revealed, SimulationHost.LocalPlayer);
        using (Rts.Client.Debug.PerfLog.Time("FogOfWar.states"))
        {
            Vision.States(world, SimulationHost.LocalPlayer, _states);
        }

        for (var index = 0; index < _states.Length; index++)
        {
            var target = FogShades.Target(_states[index], Revealed);
            _targets[index] = target;
            if (_shades[index] != target && !_isMoving[index])
            {
                _isMoving[index] = true;
                _moving.Add(index);
            }
        }
    }

    /// <summary>A new session: everything starts in the shroud and opens as the player sees.</summary>
    private void Start(GameWorld world)
    {
        _world = world;
        var (width, height) = (world.WidthCells, world.HeightCells);
        _shades = new byte[width * height];
        _targets = new byte[width * height];
        _states = new CellVisibility[width * height];
        _isMoving = new bool[width * height];
        _moving.Clear();
        _statesVersion = -1;
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
