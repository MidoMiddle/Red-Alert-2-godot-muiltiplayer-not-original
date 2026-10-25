using System;
using Godot;

namespace Rts.Client.Terrain;

/// <summary>
/// Godot's round primitives are made for a close look: a sphere of 64 × 32 segments is about 4 000 triangles, a cylinder of 64 about 800.
/// A crown of a tree, a lamp or a dome is a few dozen pixels on the field, and a map draws thousands of them (the trees of a big map
/// and its skirt were 34 million triangles, a hundred million with the shadows: 7 frames a second, the owner, 2026-10-25). Here
/// they get as few segments as still look round from the camera.
/// </summary>
public static class LowPoly
{
    private const int Around = 12;
    private const int Down = 6;

    public static T Of<T>(T mesh)
        where T : PrimitiveMesh
    {
        switch (mesh)
        {
            case SphereMesh sphere:
                sphere.RadialSegments = Math.Min(sphere.RadialSegments, Around);
                sphere.Rings = Math.Min(sphere.Rings, Down);
                break;
            case CylinderMesh cylinder:
                cylinder.RadialSegments = Math.Min(cylinder.RadialSegments, Around);
                cylinder.Rings = Math.Min(cylinder.Rings, 1);
                break;
            case CapsuleMesh capsule:
                capsule.RadialSegments = Math.Min(capsule.RadialSegments, Around);
                capsule.Rings = Math.Min(capsule.Rings, 2);
                break;
            case TorusMesh torus:
                torus.Rings = Math.Min(torus.Rings, Around * 2);
                torus.RingSegments = Math.Min(torus.RingSegments, Down);
                break;
        }

        return mesh;
    }
}
