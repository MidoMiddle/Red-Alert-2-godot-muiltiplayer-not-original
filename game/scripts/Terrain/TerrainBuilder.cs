using Godot;

namespace Rts.Client.Terrain;

/// <summary>
/// Builds the visual terrain mesh from <see cref="TerrainHeights"/> (docs/implementation/M1_visual_spike.md, 4.4):
/// chunks of 16×16 cells, smooth normals, UV0 = world XZ for texture tiling, UV1 = position on the whole map for the
/// layer mask of step 4. Registers the heights in <see cref="WorldCoords.Terrain"/> so the camera, units and picking follow them.
/// Since M2 step 4 the ground is coloured by the land types of the simulation map, with trees and rocks on top
/// (<see cref="LandLayer"/>, <see cref="MapObjects"/>). The hills stay visual until heights become rules in M7.
/// </summary>
public partial class TerrainBuilder : Node3D
{
    public const int ChunkCells = 16;

    private const string GroundShader = "res://shaders/grid.gdshader";

    public TerrainHeights Heights { get; private set; } = null!;

    public override void _Ready()
    {
        var world = GetNode<SimulationHost>("../SimulationHost").Simulation.World;
        Heights = TerrainHeights.Showcase(world.WidthCells, world.HeightCells);
        WorldCoords.Terrain = Heights;

        var material = new ShaderMaterial { Shader = GD.Load<Shader>(GroundShader) };
        material.SetShaderParameter("land_map", LandLayer.CreateTexture(world.Map));
        for (var chunkZ = 0; chunkZ < world.HeightCells; chunkZ += ChunkCells)
        {
            for (var chunkX = 0; chunkX < world.WidthCells; chunkX += ChunkCells)
            {
                AddChild(new MeshInstance3D
                {
                    Name = $"Chunk_{chunkX / ChunkCells}_{chunkZ / ChunkCells}",
                    Mesh = BuildChunk(chunkX, chunkZ),
                    MaterialOverride = material,
                    CastShadow = GeometryInstance3D.ShadowCastingSetting.On,
                });
            }
        }

        MapObjects.Build(this, world.Map, Heights);
    }

    public override void _ExitTree()
    {
        if (WorldCoords.Terrain == Heights)
        {
            WorldCoords.Terrain = null;
        }
    }

    private ArrayMesh BuildChunk(int startX, int startZ)
    {
        var cellsX = Mathf.Min(ChunkCells, Heights.WidthCells - startX);
        var cellsZ = Mathf.Min(ChunkCells, Heights.HeightCells - startZ);
        var rowLength = cellsX + 1;
        var vertexCount = rowLength * (cellsZ + 1);

        var positions = new Vector3[vertexCount];
        var normals = new Vector3[vertexCount];
        var worldUv = new Vector2[vertexCount];
        var mapUv = new Vector2[vertexCount];
        for (var z = 0; z <= cellsZ; z++)
        {
            for (var x = 0; x <= cellsX; x++)
            {
                var mapX = startX + x;
                var mapZ = startZ + z;
                var index = z * rowLength + x;
                positions[index] = new Vector3(mapX, Heights.VertexHeight(mapX, mapZ), mapZ);
                var normal = Heights.NormalAt(mapX, mapZ);
                normals[index] = new Vector3(normal.X, normal.Y, normal.Z);
                worldUv[index] = new Vector2(mapX, mapZ);
                mapUv[index] = new Vector2((float)mapX / Heights.WidthCells, (float)mapZ / Heights.HeightCells);
            }
        }

        // Two triangles per cell, clockwise seen from above: Godot's front faces.
        var indices = new int[cellsX * cellsZ * 6];
        var i = 0;
        for (var z = 0; z < cellsZ; z++)
        {
            for (var x = 0; x < cellsX; x++)
            {
                var topLeft = z * rowLength + x;
                var topRight = topLeft + 1;
                var bottomLeft = topLeft + rowLength;
                var bottomRight = bottomLeft + 1;
                indices[i++] = topLeft;
                indices[i++] = topRight;
                indices[i++] = bottomRight;
                indices[i++] = topLeft;
                indices[i++] = bottomRight;
                indices[i++] = bottomLeft;
            }
        }

        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = positions;
        arrays[(int)Mesh.ArrayType.Normal] = normals;
        arrays[(int)Mesh.ArrayType.TexUV] = worldUv;
        arrays[(int)Mesh.ArrayType.TexUV2] = mapUv;
        arrays[(int)Mesh.ArrayType.Index] = indices;

        var mesh = new ArrayMesh();
        mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return mesh;
    }
}
