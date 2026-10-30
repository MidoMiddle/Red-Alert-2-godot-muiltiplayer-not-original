using System;
using System.Collections.Generic;
using System.Numerics;

namespace Rts.Client.Terrain;

/// <summary>The arrays of one terrain chunk: positions, UV0 = world XZ, UV1 = position on the whole map, triangle indices.</summary>
public sealed record TerrainChunkMesh(Vector3[] Positions, Vector2[] WorldUv, Vector2[] MapUv, int[] Indices);

/// <summary>
/// The terrain mesh (docs/implementation/M7_maps.md, 4.12, step 9), pure math without Godot so xUnit covers it. Chunks of
/// <see cref="ChunkCells"/> × <see cref="ChunkCells"/> cells. Every cell is four triangles around a centre vertex at the mean
/// of its corners, the bilinear centre the rules use: on the edges of a cell the mesh is the rules exactly, a ramp stays one
/// plane, and a corner ramp no longer leaves units hanging over one diagonal. A cliff cell is a grid of
/// <see cref="CliffSteps"/> × <see cref="CliffSteps"/> squares on its wall (<see cref="TerrainHeights.CliffHeight"/>); where it
/// meets a cell that is no cliff, its edge is straight, so its points lie on that cell's edge. Chunks read their border corners
/// from the same heights, so neighbours meet without gaps.
/// </summary>
public static class TerrainMesh
{
    public const int ChunkCells = 32;

    /// <summary>Squares across a cliff cell: eight make the wall's steep band two or three rows of them.</summary>
    public const int CliffSteps = 8;

    /// <summary>The chunk whose north-west cell is (<paramref name="startX"/>, <paramref name="startZ"/>).</summary>
    public static TerrainChunkMesh BuildChunk(TerrainHeights heights, int startX, int startZ)
    {
        var cellsX = Math.Min(ChunkCells, heights.WidthCells + heights.Skirt - startX);
        var cellsZ = Math.Min(ChunkCells, heights.HeightCells + heights.Skirt - startZ);
        var rowLength = cellsX + 1;
        var corners = rowLength * (cellsZ + 1);
        var cliffs = 0;
        for (var z = 0; z < cellsZ; z++)
        {
            for (var x = 0; x < cellsX; x++)
            {
                cliffs += heights.IsCliff(startX + x, startZ + z) ? 1 : 0;
            }
        }

        const int cliffRow = CliffSteps + 1;
        var count = corners + cellsX * cellsZ + cliffs * cliffRow * cliffRow;
        var positions = new Vector3[count];
        var worldUv = new Vector2[count];
        var mapUv = new Vector2[count];

        void Set(int index, float x, float z, float y)
        {
            positions[index] = new Vector3(x, y, z);
            worldUv[index] = new Vector2(x, z);
            mapUv[index] = new Vector2(x / heights.WidthCells, z / heights.HeightCells);
        }

        // Corners first, row by row; then the centres of the cells; then the grids of the cliff cells.
        for (var z = 0; z <= cellsZ; z++)
        {
            for (var x = 0; x <= cellsX; x++)
            {
                Set(z * rowLength + x, startX + x, startZ + z, heights.VertexHeight(startX + x, startZ + z));
            }
        }

        for (var z = 0; z < cellsZ; z++)
        {
            for (var x = 0; x < cellsX; x++)
            {
                var (mapX, mapZ) = (startX + x, startZ + z);
                var mean = (heights.VertexHeight(mapX, mapZ) + heights.VertexHeight(mapX + 1, mapZ)
                    + heights.VertexHeight(mapX, mapZ + 1) + heights.VertexHeight(mapX + 1, mapZ + 1)) / 4f;
                // A cliff cell draws its grid instead; its centre still sits on its surface.
                Set(corners + z * cellsX + x, mapX + 0.5f, mapZ + 0.5f, heights.IsCliff(mapX, mapZ) ? heights.CliffHeight(mapX, mapZ, 0.5f, 0.5f) : mean);
            }
        }

        // Four triangles per cell, clockwise seen from above (north-west, north-east, south-east, south-west around the
        // centre): Godot's front faces, the winding the terrain always had. A cliff cell: two per square of its grid, the same way.
        var indices = new int[(cellsX * cellsZ - cliffs) * 12 + cliffs * CliffSteps * CliffSteps * 6];
        var i = 0;
        var grid = corners + cellsX * cellsZ;
        for (var z = 0; z < cellsZ; z++)
        {
            for (var x = 0; x < cellsX; x++)
            {
                var (mapX, mapZ) = (startX + x, startZ + z);
                if (heights.IsCliff(mapX, mapZ))
                {
                    for (var gz = 0; gz <= CliffSteps; gz++)
                    {
                        for (var gx = 0; gx <= CliffSteps; gx++)
                        {
                            var (fx, fz) = (gx / (float)CliffSteps, gz / (float)CliffSteps);
                            Set(grid + gz * cliffRow + gx, mapX + fx, mapZ + fz, heights.CliffHeight(mapX, mapZ, fx, fz));
                        }
                    }

                    for (var gz = 0; gz < CliffSteps; gz++)
                    {
                        for (var gx = 0; gx < CliffSteps; gx++)
                        {
                            var northWest = grid + gz * cliffRow + gx;
                            var southWest = northWest + cliffRow;
                            foreach (var corner in (ReadOnlySpan<int>)[northWest, northWest + 1, southWest + 1, northWest, southWest + 1, southWest])
                            {
                                indices[i++] = corner;
                            }
                        }
                    }

                    grid += cliffRow * cliffRow;
                    continue;
                }

                var cellNorthWest = z * rowLength + x;
                var northEast = cellNorthWest + 1;
                var cellSouthWest = cellNorthWest + rowLength;
                var southEast = cellSouthWest + 1;
                var centre = corners + z * cellsX + x;
                foreach (var (a, b) in (ReadOnlySpan<(int, int)>)[(cellNorthWest, northEast), (northEast, southEast), (southEast, cellSouthWest), (cellSouthWest, cellNorthWest)])
                {
                    indices[i++] = a;
                    indices[i++] = b;
                    indices[i++] = centre;
                }
            }
        }

        return new TerrainChunkMesh(positions, worldUv, mapUv, indices);
    }

    /// <summary>The north-west cells of every chunk of a map, row by row.</summary>
    public static IEnumerable<(int X, int Z)> Chunks(int widthCells, int heightCells) => Chunks(widthCells, heightCells, 0);

    /// <summary>The north-west cells of every chunk of a map and its skirt, row by row from the north-west corner of the skirt.</summary>
    public static IEnumerable<(int X, int Z)> Chunks(TerrainHeights heights) => Chunks(heights.WidthCells, heights.HeightCells, heights.Skirt);

    private static IEnumerable<(int X, int Z)> Chunks(int widthCells, int heightCells, int skirt)
    {
        for (var z = -skirt; z < heightCells + skirt; z += ChunkCells)
        {
            for (var x = -skirt; x < widthCells + skirt; x += ChunkCells)
            {
                yield return (x, z);
            }
        }
    }

    /// <summary>
    /// The chunks a change of heights touches: those with at least one corner, their border corners included, at another
    /// height. A corner on a border belongs to every chunk around it. Both heights must be of the same map and skirt size.
    /// </summary>
    public static List<(int X, int Z)> ChangedChunks(TerrainHeights before, TerrainHeights after)
    {
        var changed = new List<(int X, int Z)>();
        foreach (var (startX, startZ) in Chunks(after))
        {
            var endX = Math.Min(startX + ChunkCells, after.WidthCells + after.Skirt);
            var endZ = Math.Min(startZ + ChunkCells, after.HeightCells + after.Skirt);
            if (Differs(before, after, startX, startZ, endX, endZ))
            {
                changed.Add((startX, startZ));
            }
        }

        return changed;
    }

    private static bool Differs(TerrainHeights before, TerrainHeights after, int startX, int startZ, int endX, int endZ)
    {
        for (var z = startZ; z <= endZ; z++)
        {
            for (var x = startX; x <= endX; x++)
            {
                if (before.VertexHeight(x, z) != after.VertexHeight(x, z))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
