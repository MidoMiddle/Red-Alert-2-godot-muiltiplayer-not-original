using System.Collections.Generic;
using Godot;
using Rts.Client.Controls;
using Rts.Simulation.Geometry;
using Rts.Simulation.Rules;

namespace Rts.Client.Terrain;

/// <summary>
/// The placement preview (docs/implementation/M3_base_economy.md, 4.17) while a building is being placed: a see-through block where
/// it would stand and its cells on the ground, green where the rules allow it and red where they refuse. With Shift a wall shows the
/// line from the last wall, segment by segment, grey where the credits do not reach. Drawn again only when the pointer cell, the
/// building, the Shift key or the tick changes.
/// </summary>
public partial class PlacementGhost : Node3D
{
    private const float Lift = 0.04f;

    private static readonly Color OkColor = new(0.25f, 0.9f, 0.3f, 0.45f);
    private static readonly Color RefusedColor = new(0.95f, 0.2f, 0.15f, 0.5f);
    private static readonly Color UnpaidColor = new(0.55f, 0.55f, 0.55f, 0.45f);

    private readonly ImmediateMesh _cells = new();
    private readonly MeshInstance3D _block = new();
    private SimulationHost _host = null!;
    private PlayerInput _input = null!;
    private object? _drawn;

    public override void _Ready()
    {
        _host = GetNode<SimulationHost>("../../SimulationHost");
        _input = GetParent<PlayerInput>();
        var cellMaterial = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = true,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            NoDepthTest = true,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        AddChild(new MeshInstance3D { Mesh = _cells, MaterialOverride = cellMaterial, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
        _block.CastShadow = GeometryInstance3D.ShadowCastingSetting.Off;
        AddChild(_block);
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("PlacementGhost.process");
        var world = _host.Simulation.World;
        var armed = _input.BuildArmed && _host.BuildIndex >= 0 && _input.PointerGround is not null && WorldCoords.Terrain is not null;
        var def = armed ? _host.Data.Database.Buildings[_host.BuildIndex] : null;
        var corner = def is not null ? PlacementPreview.CornerOf(def.Footprint, _input.PointerGround!.Value) : default;
        var line = def is { Wall: true } && Input.IsKeyPressed(Key.Shift) && _host.LastWall is not null;
        var state = (armed, _host.BuildIndex, corner, line, _host.LastWall, world.CurrentTick);
        if (Equals(state, _drawn))
        {
            return;
        }

        _drawn = state;
        _cells.ClearSurfaces();
        _block.Visible = false;
        if (def is null)
        {
            return;
        }

        var player = SimulationHost.LocalPlayer;
        var cells = line
            ? PlacementPreview.Wall(world, player, def, PlacementPreview.WallLine(_host.LastWall!.Value, corner))
            : PlacementPreview.Footprint(world, player, def, corner);
        DrawCells(cells);
        if (!line)
        {
            DrawBlock(def, corner, AllOk(cells));
        }
    }

    private void DrawCells(IReadOnlyList<PreviewCell> cells)
    {
        if (cells.Count == 0)
        {
            return;
        }

        var terrain = WorldCoords.Terrain!;
        _cells.SurfaceBegin(Mesh.PrimitiveType.Triangles);
        foreach (var (cell, state) in cells)
        {
            if (cell.X < 0 || cell.Y < 0 || cell.X >= terrain.WidthCells || cell.Y >= terrain.HeightCells)
            {
                continue;
            }

            var color = state switch
            {
                PreviewState.Ok => OkColor,
                PreviewState.Refused => RefusedColor,
                _ => UnpaidColor,
            };
            var a = Corner(terrain, cell.X, cell.Y);
            var b = Corner(terrain, cell.X + 1, cell.Y);
            var c = Corner(terrain, cell.X + 1, cell.Y + 1);
            var d = Corner(terrain, cell.X, cell.Y + 1);
            foreach (var point in new[] { a, b, c, a, c, d })
            {
                _cells.SurfaceSetColor(color);
                _cells.SurfaceAddVertex(point);
            }
        }

        _cells.SurfaceEnd();
    }

    /// <summary>The see-through block of the building, green or red as a whole.</summary>
    private void DrawBlock(BuildingDef def, CellPos corner, bool ok)
    {
        var shape = BuildingShape.Of(def);
        var centre = new Vector3(corner.X + def.Footprint.Width / 2f, 0, corner.Y + def.Footprint.Height / 2f);
        var ground = WorldCoords.Terrain!.HeightAt(centre.X, centre.Z);
        var color = ok ? OkColor : RefusedColor;
        _block.Mesh = new BoxMesh
        {
            Size = new Vector3(def.Footprint.Width - 2 * shape.Inset, shape.Height, def.Footprint.Height - 2 * shape.Inset),
            Material = new StandardMaterial3D
            {
                AlbedoColor = new Color(color.R, color.G, color.B, 0.35f),
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            },
        };
        _block.Position = centre + new Vector3(0, ground + shape.Height / 2f, 0);
        _block.Visible = true;
    }

    private static Vector3 Corner(TerrainHeights terrain, int x, int z) => new(x, terrain.VertexHeight(x, z) + Lift, z);

    private static bool AllOk(IReadOnlyList<PreviewCell> cells)
    {
        foreach (var cell in cells)
        {
            if (cell.State != PreviewState.Ok)
            {
                return false;
            }
        }

        return true;
    }
}
