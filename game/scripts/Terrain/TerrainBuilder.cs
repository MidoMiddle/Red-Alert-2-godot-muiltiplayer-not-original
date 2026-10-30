using Godot;
using Rts.Client.Climates;

namespace Rts.Client.Terrain;

/// <summary>
/// Builds the terrain from the heights of the rules (docs/implementation/M1_visual_spike.md, 4.4; M7_maps.md, 4.12): chunks of
/// <see cref="TerrainMesh.ChunkCells"/> cells from <see cref="TerrainMesh"/>, UV0 = world XZ, UV1 = position on the whole map.
/// Registers the heights in <see cref="WorldCoords.Terrain"/> so the camera, units and picking follow them. The ground is
/// textured by the map's look (<see cref="MapLook"/>): the materials of its climate by its texture layer
/// (<see cref="MaterialSlots"/>, <see cref="TerrainTextures"/>; flat colours of the land without the textures), cliffs in the
/// climate's cliff material, and the scene is lit by its time of day (<see cref="SceneLighting"/>); with map objects and buildings
/// on top (<see cref="MapObjects"/>, <see cref="Buildings"/>). What is drawn is the rules: since M7 step 9 every map, the ASCII
/// ones too, has only the relief of its rules. When the map changes (a corner raised, a land type changed — the editor of
/// steps 11–12), the land texture is painted again and only the chunks with a changed corner are rebuilt. Round the map lies
/// its skirt (<see cref="MapSkirt"/>): land that goes on beyond the edge, as in RA2, made again only when the edge changes. The
/// map comes from the match, or from the editor (<see cref="IMapSource"/>).
/// </summary>
public partial class TerrainBuilder : Node3D
{
    private const string GroundShader = "res://shaders/grid.gdshader";

    private readonly System.Collections.Generic.Dictionary<(int X, int Z), MeshInstance3D> _chunks = new();
    private System.Func<Rts.Simulation.Map.MapGrid> _source = null!;
    private Rts.Simulation.Map.MapGrid? _builtMap;
    private int _builtVersion;
    private ImageTexture _landTexture = null!;
    private ImageTexture _materialTexture = null!;
    private ImageTexture? _roadTexture;
    private System.Func<MapLook> _lookSource = null!;
    private MapLook? _builtLook;
    private ClimateLook? _climate;
    private MapSkirt? _skirt;
    private uint _skirtSignature;
    private ClimateLook? _skirtClimate;

    public TerrainHeights Heights { get; private set; } = null!;

    /// <summary>The map the terrain shows now; map objects drawn on it read it from here.</summary>
    public Rts.Simulation.Map.MapGrid Map => _builtMap!;

    /// <summary>The land beyond the edge of <see cref="Map"/>; its pines are drawn by <see cref="MapObjects"/>.</summary>
    public MapSkirt Skirt => _skirt!;

    /// <summary>The look of the climate shown now, or null before the first map; its trees are drawn by <see cref="MapObjects"/>.</summary>
    public ClimateLook? Climate => _climate;

    /// <summary>The ground material of every chunk; the debug cell overlay sets its overlay parameters.</summary>
    public ShaderMaterial GroundMaterial { get; private set; } = null!;

    public override void _Ready()
    {
        _source = MapSources.Find(this);
        _lookSource = MapSources.FindLook(this);
        var map = _source();
        var look = _lookSource();
        _landTexture = LandLayer.CreateTexture(map);
        GroundMaterial = new ShaderMaterial { Shader = GD.Load<Shader>(GroundShader) };
        GroundMaterial.SetShaderParameter("land_map", _landTexture);
        GroundMaterial.SetShaderParameter("level_height", TerrainHeights.LevelHeight);
        SetLook(look, map, sameSize: false);
        Heights = TerrainHeights.FromRules(map.Heights, _skirt!);
        WorldCoords.Terrain = Heights;
        WorldCoords.Map = map;
        foreach (var chunk in TerrainMesh.Chunks(Heights))
        {
            BuildChunk(chunk);
        }

        (_builtMap, _builtVersion, _builtLook) = (map, map.GroundVersion, look);
        AddChild(new MapObjects { Name = "MapObjects" });
        AddChild(new Buildings { Name = "Buildings" });
        AddChild(new PlayerBuildings { Name = "PlayerBuildings" });
        AddChild(new OreField { Name = "OreField" });
        AddChild(new OreSources { Name = "OreSources" });
    }

    public override void _Process(double delta)
    {
        using var frameTimer = Rts.Client.Debug.PerfLog.Time("TerrainBuilder.process");
        // Runs before the children: map objects and buildings rebuilt on the same change read the new heights.
        var map = _source();
        var look = _lookSource();
        if (ReferenceEquals(map, _builtMap) && map.GroundVersion == _builtVersion)
        {
            if (!ReferenceEquals(look, _builtLook))
            {
                // Only the look changed (a texture painted, another time of day): the mesh stays.
                SetLook(look, map, sameSize: true);
                _builtLook = look;
            }

            return;
        }

        // The same map changed, or a new match (R) started on a map of the same size: rebuild what differs. Another size: all.
        var sameSize = map.Width == Heights.WidthCells && map.Height == Heights.HeightCells;
        SetLook(look, map, sameSize);
        var heights = TerrainHeights.FromRules(map.Heights, _skirt!);
        var changed = sameSize ? TerrainMesh.ChangedChunks(Heights, heights) : [.. TerrainMesh.Chunks(heights)];
        if (sameSize)
        {
            LandLayer.Update(_landTexture, map);
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
        }

        Heights = heights;
        WorldCoords.Terrain = heights;
        WorldCoords.Map = map;
        foreach (var chunk in changed)
        {
            BuildChunk(chunk);
        }

        (_builtMap, _builtVersion, _builtLook) = (map, map.GroundVersion, look);
    }

    public override void _ExitTree()
    {
        if (WorldCoords.Terrain == Heights)
        {
            WorldCoords.Terrain = null;
            WorldCoords.Map = null;
        }
    }

    /// <summary>
    /// Shows the look of the map: the materials of its cells, the textures of its climate when the climate is another, and the
    /// light of its time of day on the sun, the environment and the water of the ground. The skirt is made again when the edge
    /// of the map changed (<see cref="MapSkirt.EdgeSignature"/>); the materials of the map's own cells always come fresh.
    /// </summary>
    private void SetLook(MapLook look, Rts.Simulation.Map.MapGrid map, bool sameSize)
    {
        var climate = ClimateLooks.Get(look.Climate);
        if (!ReferenceEquals(climate, _climate) || _builtLook is null)
        {
            _climate = climate;
            SetGroundTextures(climate);
        }

        var cells = climate is null
            ? new byte[map.Width * map.Height * MaterialSlots.BytesPerCell]
            : MaterialSlots.Build(look, climate, map);
        var signature = MapSkirt.EdgeSignature(map, cells);
        if (_skirt is null || !sameSize || signature != _skirtSignature || !ReferenceEquals(climate, _skirtClimate))
        {
            _skirt = MapSkirt.Build(map, cells, SkirtSlotsOf(climate));
            (_skirtSignature, _skirtClimate) = (signature, climate);
        }

        var all = _skirt.WithMapCells(cells);
        if (sameSize && _materialTexture is not null)
        {
            LandLayer.UpdateMaterials(_materialTexture, all, _skirt.CellsX, _skirt.CellsZ);
        }
        else
        {
            _materialTexture = LandLayer.CreateMaterialTexture(all, _skirt.CellsX, _skirt.CellsZ);
            GroundMaterial.SetShaderParameter("material_map", _materialTexture);
            GroundMaterial.SetShaderParameter("map_origin", new Vector2(-_skirt.Skirt, -_skirt.Skirt));
        }

        // The markings of the asphalt (RoadMarkings): one texel a cell of the map, no skirt.
        var roads = Image.CreateFromData(map.Width, map.Height, false, Image.Format.Rgba8, RoadMarkings.Build(look, map));
        if (sameSize && _roadTexture is not null)
        {
            _roadTexture.Update(roads);
        }
        else
        {
            _roadTexture = ImageTexture.CreateFromImage(roads);
            GroundMaterial.SetShaderParameter("road_map", _roadTexture);
        }

        if (climate is not null && (_builtLook?.Lighting != look.Lighting || _builtLook?.Climate != look.Climate))
        {
            var preset = climate.Light(look.Lighting);
            SceneLighting.Apply(preset, GetNodeOrNull<DirectionalLight3D>("../Sun"), GetNodeOrNull<WorldEnvironment>("../WorldEnvironment"));
            GroundMaterial.SetShaderParameter("sun_direction", SceneLighting.SunDirection(preset));
        }
    }

    /// <summary>The materials the skirt paints far from the edge: the first of the climate as plain ground, its grass and dirt.</summary>
    private static SkirtSlots SkirtSlotsOf(ClimateLook? climate) => climate is null
        ? new SkirtSlots(0, -1, -1, -1, -1)
        : new SkirtSlots(0, climate.SlotOf("grass"), climate.SlotOf("dirt"), climate.SlotOf("asphalt"), climate.SlotOf("water"));

    /// <summary>Gives the ground shader the materials of a climate; without its textures it draws the flat colours of the land types.</summary>
    private void SetGroundTextures(ClimateLook? climate)
    {
        var textures = climate is null ? null : TerrainTextures.Load(climate);
        GroundMaterial.SetShaderParameter("use_textures", textures is not null);
        if (climate is null || textures is null)
        {
            return;
        }

        var tiling = new float[ClimateLook.MaxMaterials];
        var roughness = new float[ClimateLook.MaxMaterials];
        var water = new float[ClimateLook.MaxMaterials];
        var frost = new float[ClimateLook.MaxMaterials];
        var tint = new Vector3[ClimateLook.MaxMaterials];
        var painted = new float[ClimateLook.MaxMaterials];
        var color = new Vector3[ClimateLook.MaxMaterials];
        var patches = new Vector3[ClimateLook.MaxMaterials];
        var share = new float[ClimateLook.MaxMaterials];
        for (var i = 0; i < ClimateLook.MaxMaterials; i++)
        {
            var material = climate.Materials[System.Math.Min(i, climate.Materials.Length - 1)];
            tiling[i] = material.Tiling;
            roughness[i] = material.Roughness;
            water[i] = material.Water ? 1f : 0f;
            frost[i] = material.Frost;
            // The shader takes the tint as linear light, like the albedo it multiplies.
            var linear = SceneLighting.Color(material.Tint).SrgbToLinear();
            tint[i] = new Vector3(linear.R, linear.G, linear.B);
            if (material.Color is { } own && material.Patches is { } second)
            {
                painted[i] = 1f;
                color[i] = Linear(own);
                patches[i] = Linear(second);
                share[i] = material.PatchesShare;
            }
        }

        GroundMaterial.SetShaderParameter("cliff_slot", climate.SlotOf(climate.Cliff));
        GroundMaterial.SetShaderParameter("grass_slot", climate.SlotOf("grass"));
        GroundMaterial.SetShaderParameter("ground_albedo", textures.Albedo);
        GroundMaterial.SetShaderParameter("tex_water_normal", textures.WaterNormal);
        GroundMaterial.SetShaderParameter("mat_tiling", tiling);
        GroundMaterial.SetShaderParameter("mat_roughness", roughness);
        GroundMaterial.SetShaderParameter("mat_water", water);
        GroundMaterial.SetShaderParameter("mat_frost", frost);
        GroundMaterial.SetShaderParameter("mat_tint", tint);
        GroundMaterial.SetShaderParameter("mat_painted", painted);
        GroundMaterial.SetShaderParameter("mat_color", color);
        GroundMaterial.SetShaderParameter("mat_patches", patches);
        GroundMaterial.SetShaderParameter("mat_patches_share", share);
        GroundMaterial.SetShaderParameter("deep_water_color", SceneLighting.Color(climate.DeepWater));
    }

    // A colour of the look as linear light, the way the shader multiplies colours.
    private static Vector3 Linear(LookColor color)
    {
        var linear = SceneLighting.Color(color).SrgbToLinear();
        return new Vector3(linear.R, linear.G, linear.B);
    }

    // The normal of the ground at a point from its heights half a cell to each side.
    private Vector3 SmoothNormal(float x, float z)
    {
        const float reach = 0.5f;
        var slopeX = (Heights.HeightAt(x + reach, z) - Heights.HeightAt(x - reach, z)) / (2f * reach);
        var slopeZ = (Heights.HeightAt(x, z + reach) - Heights.HeightAt(x, z - reach)) / (2f * reach);
        return new Vector3(-slopeX, 1f, -slopeZ).Normalized();
    }

    private void BuildChunk((int X, int Z) start)
    {
        var data = TerrainMesh.BuildChunk(Heights, start.X, start.Z);
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = System.Array.ConvertAll(data.Positions, p => new Vector3(p.X, p.Y, p.Z));
        // The shader lights every face by its own normal, but the asphalt by these: the slope of the ground round the vertex, so a
        // road up or down a hill reads as one even slope, not a row of facets (the owner, 2026-10-30). They feed the shadow bias too.
        arrays[(int)Mesh.ArrayType.Normal] = System.Array.ConvertAll(data.Positions, p => SmoothNormal(p.X, p.Z));
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
