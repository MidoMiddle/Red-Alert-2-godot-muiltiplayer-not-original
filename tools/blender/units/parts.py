"""Shared steps for the Blender builds of units: closed lofts painted with surface parts, colours for the bake.

A unit module in tools/blender/units/ builds its objects from Parts, writes a material that reads the PART
attribute and bakes it with rts_common. Rules for units: docs/art/model_standards.md.
"""

import math

import bmesh
import bpy
from mathutils import Vector

import rts_common as rc

PART = "part"


def lin(c):
    """An sRGB colour as the linear value the bake expects: the EMIT bake into an sRGB image encodes it back."""
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


def _axis(name):
    d = Vector({'X': (1, 0, 0), 'Y': (0, 1, 0), 'Z': (0, 0, 1)}[name])
    u = Vector({'X': (0, 1, 0), 'Y': (0, 0, 1), 'Z': (1, 0, 0)}[name])
    return d, u, d.cross(u)


class Parts:
    """A bmesh made of closed lofts, each face painted with its surface part (an RGBA tuple in PART)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.layer = self.bm.loops.layers.float_color.new(PART)

    def loft(self, rings, part):
        """Quads between rings of the same length, both ends capped."""
        verts = [[self.bm.verts.new(p) for p in ring] for ring in rings]
        faces = []
        n = len(rings[0])
        for a, b in zip(verts, verts[1:]):
            for i in range(n):
                j = (i + 1) % n
                faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        faces.append(self.bm.faces.new(verts[0]))
        faces.append(self.bm.faces.new(list(reversed(verts[-1]))))
        for face in faces:
            for loop in face.loops:
                loop[self.layer] = part

    def box(self, lo, hi, part):
        x0, y0, z0 = lo
        x1, y1, z1 = hi
        self.loft([[Vector((x0, y0, z)), Vector((x1, y0, z)), Vector((x1, y1, z)), Vector((x0, y1, z))]
                   for z in (z0, z1)], part)

    def profile_x(self, yz, x0, x1, part):
        """A convex profile in the YZ plane pushed along X from x0 to x1."""
        self.loft([[Vector((x, y, z)) for y, z in yz] for x in (x0, x1)], part)

    def turned(self, centre, axis, stations, part, sides=16):
        """A body of revolution about the axis 'X', 'Y' or 'Z': stations are (offset along the axis, radius)."""
        d, u, v = _axis(axis)
        c = Vector(centre)
        circle = [u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides) for i in range(sides)]
        self.loft([[c + d * t + p * r for p in circle] for t, r in stations], part)

    def cylinder(self, centre, axis, radius, half, part, sides=16):
        """Capped cylinder of the given half-length."""
        self.turned(centre, axis, [(-half, radius), (half, radius)], part, sides)

    def sphere(self, centre, axis, radius, part, sides=16, rings=8):
        """A sphere as a body of revolution, its poles on the axis cut flat just short of the tips."""
        stations = [(-radius * math.cos(math.pi * i / rings), radius * math.sin(math.pi * i / rings))
                    for i in range(1, rings)]
        self.turned(centre, axis, stations, part, sides)

    def bar(self, p1, p2, half, part):
        """A square bar from p1 to p2."""
        p1, p2 = Vector(p1), Vector(p2)
        d = (p2 - p1).normalized()
        a = d.cross(Vector((0.0, 0.0, 1.0)))
        if a.length < 1e-6:
            a = d.cross(Vector((1.0, 0.0, 0.0)))
        a = a.normalized() * half
        b = d.cross(a).normalized() * half
        self.loft([[p + a + b, p - a + b, p - a - b, p + a - b] for p in (p1, p2)], part)

    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        return rc.make_object(name, self.bm)


def uv_together(objs):
    """One smart projection over all meshes, so they share the texture without overlaps at the same texel size."""
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.01)
    bpy.ops.object.mode_set(mode='OBJECT')


def attach(child, parent, location):
    child.parent = parent
    child.location = location


def empty(name, parent, location):
    """An empty node (a point for the game) under parent."""
    node = bpy.data.objects.new(name, None)
    node.empty_display_type = 'PLAIN_AXES'
    node.empty_display_size = 0.05
    bpy.context.scene.collection.objects.link(node)
    attach(node, parent, location)
    return node


def save_blend(objs, mat, imgs, path):
    """Writes the .blend. Run in the background, the whole file: its scene holds the model, so File > Open shows it.

    In a live session the file gets only the model's blocks (rc.save_blend), as the session has other things in
    it; such a file opens empty and the model comes in by Append. A partial write with a scene of its own would
    do for both, but Blender 5.2 crashes copying the scene's view layer.
    """
    if bpy.app.background:
        bpy.ops.wm.save_as_mainfile(filepath=path, copy=True, compress=True)
    else:
        rc.save_blend(objs, mat, imgs, path)
