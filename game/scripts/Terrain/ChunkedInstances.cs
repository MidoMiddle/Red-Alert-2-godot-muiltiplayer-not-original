using System;
using System.Collections.Generic;
using Godot;

namespace Rts.Client.Terrain;

/// <summary>
/// Many copies of one mesh in pieces of the map (the performance log, 2026-10-26): one MultiMesh for every <see cref="ChunkCells"/> ×
/// <see cref="ChunkCells"/> cells holding copies, each a node of its own bounds, so that the renderer leaves out — for the picture and
/// for the shadows — the pieces off the screen. One MultiMesh for the whole map drew all its copies every frame: the 20 000 trees of War
/// Pro and their shadows were 14.6 million triangles a frame. The cells of a copy are its place on the ground: x and z, a cell a unit.
/// </summary>
public static class ChunkedInstances
{
    /// <summary>The side of a piece, in cells.</summary>
    public const int ChunkCells = 16;

    /// <summary>
    /// Adds the copies of <paramref name="mesh"/> at <paramref name="places"/> under <paramref name="parent"/>, a node named
    /// <c>&lt;name&gt;_&lt;x&gt;_&lt;z&gt;</c> a piece; <paramref name="placed"/> is told, for each copy by its index in
    /// <paramref name="places"/>, the MultiMesh and the index it has there — to hide it and show it again.
    /// </summary>
    public static void Add(Node3D parent, string name, Mesh mesh, IReadOnlyList<Transform3D> places, Action<int, MultiMesh, int>? placed = null)
    {
        if (places.Count == 0)
        {
            return;
        }

        var pieces = new SortedDictionary<(int X, int Z), List<int>>();
        for (var i = 0; i < places.Count; i++)
        {
            var origin = places[i].Origin;
            var key = ((int)Mathf.Floor(origin.X / ChunkCells), (int)Mathf.Floor(origin.Z / ChunkCells));
            if (!pieces.TryGetValue(key, out var copies))
            {
                copies = [];
                pieces[key] = copies;
            }

            copies.Add(i);
        }

        foreach (var ((x, z), copies) in pieces)
        {
            var multiMesh = new MultiMesh { TransformFormat = MultiMesh.TransformFormatEnum.Transform3D, Mesh = mesh, InstanceCount = copies.Count };
            for (var k = 0; k < copies.Count; k++)
            {
                multiMesh.SetInstanceTransform(k, places[copies[k]]);
                placed?.Invoke(copies[k], multiMesh, k);
            }

            parent.AddChild(new MultiMeshInstance3D { Name = $"{name}_{x}_{z}", Multimesh = multiMesh });
        }
    }
}
