using Godot;

namespace Rts.Client.Terrain;

/// <summary>
/// Builds the terrain from the heights of the rules (docs/implementation/M1_visual_spike.md, 4.4; M7_maps.md, 4.12): chunks of
/// <see cref="TerrainMesh.ChunkCells"/> cells from <see cref="TerrainMesh"/>, UV0 = world XZ, UV1 = position on the whole map.
/// Registers the heights in <see cref="WorldCoords.Terrain"/> so the camera, units and picking follow them. The ground is
/// textured by the land types of the simulation map (<see cref="TerrainTextures"/>; flat colours without the textures), cliffs in
/// rock (<see cref="LandLayer"/>), with map objects and buildings
/// on top (<see cref="MapObjects"/>, <see cref="Buildings"/>). What is drawn is the rules: since M7 step 9 every map, the ASCII
/// ones too, has only the relief of its rules. When the map changes (a corner raised, a land type changed — the editor of
/// steps 11–12), the land texture is painted again and only the chunks with a changed corner are rebuilt. The map comes from
/// the match, or from the editor (<see cref="IMapSource"/>).
/// </summary>
public partial class TerrainBuilder : Node3D
{
    private const string GroundShader = "res://shaders/grid.gdshader";

    private readonly System.Collections.Generic.Dictionary<(int X, int Z), MeshInstance3D> _chunks = new();
    private System.Func<Rts.Simulation.Map.MapGrid> _source = null!;
    private Rts.Simulation.Map.MapGrid? _builtMap;
    private int _builtVersion;
    private ImageTexture _landTexture = null!;
    private ImageTexture _typeTexture = null!;

    public TerrainHeights Heights { get; private set; } = null!;

    /// <summary>The map the terrain shows now; map objects drawn on it read it from here.</summary>
    public Rts.Simulation.Map.MapGrid Map => _builtMap!;

    /// <summary>The ground material of every chunk; the debug cell overlay sets its overlay parameters.</summary>
    public ShaderMaterial GroundMaterial { get; private set; } = null!;

    public override void _Ready()
    {
        _source = MapSources.Find(this);
        var map = _source();
        Heights = TerrainHeights.FromRules(map.Heights);
        WorldCoords.Terrain = Heights;

        _landTexture = LandLayer.CreateTexture(map);
        GroundMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(GroundShader) };
        GroundMaterial.SetShaderParameter("land_map", _landTexture);
        _typeTexture = LandLayer.CreateTypeTexture(map);
        GroundMaterial.SetShaderParameter("type_map", _typeTexture);
        SetGroundTextures(TerrainTextures.Load());
        GroundMaterial.SetShaderParameter("level_height", TerrainHeights.LevelHeight);
        foreach (var chunk in TerrainMesh.Chunks(map.Width, map.Height))
        {
            BuildChunk(chunk);
        }

        (_builtMap, _builtVersion) = (map, map.Version);
        AddChild(new MapObjects { Name = "MapObjects" });
        AddChild(new Buildings { Name = "Buildings" });
        AddChild(new PlayerBuildings { Name = "PlayerBuildings" });
        AddChild(new OreField { Name = "OreField" });
    }

    public override void _Process(double delta)
    {
        // Runs before the children: map objects and buildings rebuilt on the same change read the new heights.
        var map = _source();
        if (ReferenceEquals(map, _builtMap) && map.Version == _builtVersion)
        {
            return;
        }

        // The same map changed, or a new match (R) started on a map of the same size: rebuild what differs. Another size: all.
        var heights = TerrainHeights.FromRules(map.Heights);
        var sameSize = heights.WidthCells == Heights.WidthCells && heights.HeightCells == Heights.HeightCells;
        var changed = sameSize ? TerrainMesh.ChangedChunks(Heights, heights) : [.. TerrainMesh.Chunks(map.Width, map.Height)];
        if (sameSize)
        {
            LandLayer.Update(_landTexture, map);
            LandLayer.UpdateType(_typeTexture, map);
        }
        else
        {
            foreach (var instance in _chunks.Values)
            {
                instance.QueueFree();
            }

            _chunks.Clear();
            _landTexture = LandLayer.CreateTexture(map);
            GroundMaterial.SetShaderParameter("land_map", _landTexture);
            _typeTexture = LandLayer.CreateTypeTexture(map);
            GroundMaterial.SetShaderParameter("type_map", _typeTexture);
        }

        Heights = heights;
        WorldCoords.Terrain = heights;
        foreach (var chunk in changed)
        {
            BuildChunk(chunk);
        }

        (_builtMap, _builtVersion) = (map, map.Version);
    }

    public override void _ExitTree()
    {
        if (WorldCoords.Terrain == Heights)
        {
            WorldCoords.Terrain = null;
        }
    }

    /// <summary>Gives the ground shader its textures; without them it draws the flat colours of the land types.</summary>
    private void SetGroundTextures(GroundTextures? textures)
    {
        GroundMaterial.SetShaderParameter("use_textures", textures is not null);
        if (textures is null)
        {
            return;
        }

        GroundMaterial.SetShaderParameter("tex_snow", textures.Snow);
        GroundMaterial.SetShaderParameter("tex_grass", textures.Grass);
        GroundMaterial.SetShaderParameter("tex_gravel", textures.Gravel);
        GroundMaterial.SetShaderParameter("tex_rock", textures.Rock);
        GroundMaterial.SetShaderParameter("tex_ice", textures.Ice);
        GroundMaterial.SetShaderParameter("tex_water_normal", textures.WaterNormal);
    }

    private void BuildChunk((int X, int Z) start)
    {
        var data = TerrainMesh.BuildChunk(Heights, start.X, start.Z);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = System.Array.ConvertAll(data.Positions, p => new Vector3(p.X, p.Y, p.Z));
        // The shader lights every face by its own normal; the vertex normal only feeds the shadow bias.
        arrays[(int)Mesh.ArrayType.Normal] = System.Array.ConvertAll(data.Positions, _ => Vector3.Up);
        arrays[(int)Mesh.ArrayType.TexUV] = System.Array.ConvertAll(data.WorldUv, p => new Vector2(p.X, p.Y));
        arrays[(int)Mesh.ArrayType.TexUV2] = System.Array.ConvertAll(data.MapUv, p => new Vector2(p.X, p.Y));
        arrays[(int)Mesh.ArrayType.Index] = data.Indices;
        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);

        if (!_chunks.TryGetValue(start, out var instance))
        {
            instance = new MeshInstance3D
            {
                Name = $"Chunk_{start.X / TerrainMesh.ChunkCells}_{start.Z / TerrainMesh.ChunkCells}",
                MaterialOverride = GroundMaterial,
                CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
            };
            _chunks.Add(start, instance);
            AddChild(instance);
        }

        instance.Mesh = mesh;
    }
}
