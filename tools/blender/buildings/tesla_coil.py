"""Tesla coil: the Union's electric defence tower (docs/ra2_buildings/soviets/tesla_coil).

Footprint 2x2 cells (game/data/factions/union/buildings.yaml), origin at the centre of the footprint on the ground.
An octagonal steel plinth; a column of three stacked bulbs with flanged necks, fed by four ribbed hoses from the
plinth; four upright fins around it; a dark rod with three segmented rings, narrowing upwards; a polished
discharge sphere on top, 3.0 high. The parts follow the reference picture given by the owner, in the Union's look
(docs/ra2_buildings/generation_guide.md, section 4): dark steel, a muted red accent. No emblems or text.
Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4; docs/ra2_buildings/soviets/tesla_coil/animations.md):
    body            plinth, column, hoses, sockets, rod and rings
    team_mask       the four fins: the game paints them in the owner's colour
    light_sphere    the discharge sphere; the game will flicker its emission while the coil has power
    bolt_origin     empty at the centre of the sphere: where the bolt starts
    light_socket_1..4  empties at the mouths of the charging sockets, where tesla troopers plug in
The game does not light light_sphere or the sockets yet (M1, 4.13).

In Blender, after tools/blender is on sys.path:
    import buildings.tesla_coil as tc
    print(tc.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Vector

import rts_common as rc

ID = "tesla_coil"
PREFIX = ID + "_"
SIZE = 2048
PART = "part"

SPHERE_Z = 2.74
SPHERE_R = 0.27
SOCKET_Z = 0.92
# Rings round the rod: height, radius of the ring, number of bulges.
RINGS = ((1.48, 0.38, 18), (1.76, 0.33, 16), (2.04, 0.28, 14))
# The column as a body of revolution: height, radius. Three bulbs with necks between them.
COLUMN = ((0.12, 0.16), (0.18, 0.21), (0.26, 0.25), (0.36, 0.27), (0.46, 0.27), (0.56, 0.25), (0.62, 0.20),
          (0.66, 0.17), (0.70, 0.20), (0.78, 0.25), (0.88, 0.26), (0.98, 0.25), (1.06, 0.20), (1.10, 0.16),
          (1.14, 0.19), (1.20, 0.21), (1.26, 0.19), (1.32, 0.15))


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


STEEL, CHROME, PLATE, HOSE, RED, TEAM = (_part(i) for i in range(6))


def lin(c):
    """An sRGB colour as the linear value the bake expects: the EMIT bake into an sRGB image encodes it back."""
    return tuple(v / 12.92 if v <= 0.04045 else ((v + 0.055) / 1.055) ** 2.4 for v in c)


# --- geometry -------------------------------------------------------------------

def _ring(centre, d, radius, sides, phase=0.0):
    """Points of a circle round centre, square to the unit direction d."""
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius
            for a in (phase + 2 * math.pi * i / sides for i in range(sides))]


class Parts:
    """A bmesh made of closed lofts, each face painted with its surface part (an RGBA tuple in PART)."""

    def __init__(self):
        self.bm = bmesh.new()
        self.layer = self.bm.loops.layers.float_color.new(PART)

    def _paint(self, faces, part):
        for face in faces:
            for loop in face.loops:
                loop[self.layer] = part

    def loft(self, rings, part, closed=False):
        """Quads between rings of the same length; both ends capped, or the last ring joined to the first."""
        verts = [[self.bm.verts.new(p) for p in ring] for ring in rings]
        faces = []
        n = len(rings[0])
        pairs = list(zip(verts, verts[1:])) + ([(verts[-1], verts[0])] if closed else [])
        for a, b in pairs:
            for i in range(n):
                j = (i + 1) % n
                faces.append(self.bm.faces.new((a[i], a[j], b[j], b[i])))
        if not closed:
            faces.append(self.bm.faces.new(verts[0]))
            faces.append(self.bm.faces.new(list(reversed(verts[-1]))))
        self._paint(faces, part)

    def turned(self, stations, part, sides=24, phase=0.0, at=(0.0, 0.0)):
        """A body of revolution about the vertical through at: stations are (height, radius)."""
        up = Vector((0.0, 0.0, 1.0))
        self.loft([_ring(Vector((at[0], at[1], z)), up, r, sides, phase) for z, r in stations], part)

    def cylinder(self, p1, p2, radius, part, sides=12):
        p1, p2 = Vector(p1), Vector(p2)
        d = (p2 - p1).normalized()
        self.loft([_ring(p, d, radius, sides) for p in (p1, p2)], part)

    def tube(self, points, radii, part, sides=10):
        """A tube through the points, its radius at each point from radii."""
        rings = []
        for i, p in enumerate(points):
            d = (points[min(i + 1, len(points) - 1)] - points[max(i - 1, 0)]).normalized()
            rings.append(_ring(p, d, radii[i], sides))
        self.loft(rings, part)

    def torus(self, z, major, minor, bulges, part, steps=72, sides=8):
        """A horizontal ring at height z, swelling into bulges segments like a ribbed coil."""
        rings = []
        for i in range(steps):
            a = 2 * math.pi * i / steps
            radial = Vector((math.cos(a), math.sin(a), 0.0))
            r = minor * (0.8 + 0.2 * abs(math.sin(math.pi * bulges * i / steps)))
            rings.append([Vector((0.0, 0.0, z)) + radial * (major + r * math.cos(b)) + Vector((0.0, 0.0, r * math.sin(b)))
                          for b in (2 * math.pi * k / sides for k in range(sides))])
        self.loft(rings, part, closed=True)

    def sphere(self, centre, radius, part, sides=24, rings=12):
        c = Vector(centre)
        stations = [(c.z - radius * math.cos(math.pi * i / rings), radius * math.sin(math.pi * i / rings))
                    for i in range(1, rings)]
        self.turned(stations, part, sides, at=(c.x, c.y))

    def slab(self, profile, angle, half, part):
        """A flat plate: a convex profile of (distance from the axis, height), half thick, turned to angle."""
        out = Vector((math.cos(angle), math.sin(angle), 0.0))
        side = Vector((-out.y, out.x, 0.0)) * half
        self.loft([[out * r + Vector((0.0, 0.0, z)) + side * s for r, z in profile] for s in (-1, 1)], part)

    def finish(self, name):
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        return rc.make_object(PREFIX + name, self.bm)


def _hose(p, angle):
    """A ribbed hose from the plinth up and into the lowest bulb, on the diagonal at angle."""
    out = Vector((math.cos(angle), math.sin(angle), 0.0))
    a = out * 0.56 + Vector((0.0, 0.0, 0.12))
    b = out * 0.56 + Vector((0.0, 0.0, 0.46))
    c = out * 0.2 + Vector((0.0, 0.0, 0.5))
    n = 25
    points = [a * (1 - t) ** 2 + b * 2 * t * (1 - t) + c * t * t for t in (i / (n - 1) for i in range(n))]
    p.tube(points, [0.05 if i % 2 else 0.042 for i in range(n)], HOSE)
    p.turned([(0.12, 0.08), (0.15, 0.08)], STEEL, sides=8, at=(a.x, a.y))  # the hose's foot on the plinth


def _body():
    p = Parts()
    # Plinth: a low octagon, a red step on it, bolts round the edge.
    eighth = math.pi / 8
    p.turned([(0.0, 0.92), (0.09, 0.92), (0.12, 0.88)], PLATE, sides=8, phase=eighth)
    p.turned([(0.12, 0.42), (0.17, 0.42), (0.19, 0.38)], RED, sides=8, phase=eighth)
    for i in range(16):
        a = 2 * math.pi * (i + 0.5) / 16
        p.turned([(0.11, 0.022), (0.135, 0.022)], STEEL, sides=6, at=(0.8 * math.cos(a), 0.8 * math.sin(a)))
    # The column and its flanged necks.
    p.turned(COLUMN, STEEL, sides=24)
    for z in (0.66, 1.10):
        p.turned([(z - 0.025, 0.23), (z + 0.025, 0.23)], CHROME, sides=24)
    for i in range(4):
        diag = math.pi / 4 + i * math.pi / 2
        _hose(p, diag)
        out = Vector((math.cos(diag), math.sin(diag), 0.0))
        socket = Vector((0.0, 0.0, SOCKET_Z))
        p.cylinder(socket + out * 0.18, socket + out * 0.32, 0.045, STEEL)           # charging socket
        p.cylinder(socket + out * 0.30, socket + out * 0.33, 0.075, CHROME, sides=16)  # its flange
        # Stays from the column to the fins.
        fin = Vector((math.cos(i * math.pi / 2), math.sin(i * math.pi / 2), 0.0))
        for z in (0.40, 0.84):
            p.cylinder(fin * 0.2 + Vector((0.0, 0.0, z)), fin * 0.34 + Vector((0.0, 0.0, z)), 0.02, CHROME, sides=6)
    # The rod with its collars, the rings on spokes, the neck under the sphere.
    p.turned([(1.30, 0.065), (2.48, 0.065)], STEEL, sides=12)
    for z in (1.34, 1.62, 1.90, 2.18):
        p.turned([(z - 0.03, 0.085), (z + 0.03, 0.085)], STEEL, sides=12)
    for z, major, bulges in RINGS:
        p.torus(z, major, 0.06, bulges, CHROME)
        for k in range(4):
            a = math.pi / 4 + k * math.pi / 2
            out = Vector((math.cos(a), math.sin(a), 0.0))
            p.cylinder(out * 0.06 + Vector((0.0, 0.0, z)), out * (major - 0.04) + Vector((0.0, 0.0, z)), 0.014, STEEL, sides=6)
    p.turned([(2.44, 0.065), (2.50, 0.11), (2.54, 0.11), (2.58, 0.08)], CHROME, sides=16)
    return p.finish("body")


def _fins():
    """Four upright fins round the column, their tops sloping down outwards."""
    p = Parts()
    profile = [(0.34, 0.12), (0.68, 0.12), (0.68, 0.96), (0.46, 1.24), (0.34, 1.24)]
    for i in range(4):
        p.slab(profile, i * math.pi / 2, 0.04, TEAM)
    return p.finish("team_mask")


def _sphere():
    p = Parts()
    p.sphere((0.0, 0.0, SPHERE_Z), SPHERE_R, CHROME, sides=32, rings=16)
    return p.finish("light_sphere")


def _empty(name, location):
    node = bpy.data.objects.new(PREFIX + name, None)
    node.empty_display_type = 'PLAIN_AXES'
    node.empty_display_size = 0.08
    node.location = location
    bpy.context.scene.collection.objects.link(node)
    return node


# --- material -------------------------------------------------------------------

def _material():
    """Dark steel with bands, polished chrome, a worn plate, black rubber hoses, a muted red, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.15)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(6)]
    grain = g.noise(30.0, 4.0)
    scratches = g.clamp01(g.math('MULTIPLY_ADD', g.noise(90.0, 2.0), 6.0, -3.6))
    z = g.axis(g.object_coord(), 'Z')
    # Thin dark bands every 0.12 up the column, where the steel rings are welded.
    bands = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 8.0)), -12.0, 1.0))

    steel = g.mix(bands, g.mix(grain, lin((0.24, 0.25, 0.27)), lin((0.31, 0.32, 0.34))), lin((0.12, 0.12, 0.13)))
    chrome = g.mix(grain, lin((0.70, 0.71, 0.73)), lin((0.80, 0.81, 0.83)))
    plate = g.mix(scratches, g.mix(grain, lin((0.36, 0.36, 0.35)), lin((0.44, 0.44, 0.42))), lin((0.58, 0.58, 0.56)))
    hose = g.mix(grain, lin((0.30, 0.31, 0.32)), lin((0.38, 0.39, 0.40)))
    red = g.mix(grain, lin((0.42, 0.13, 0.11)), lin((0.50, 0.17, 0.14)))
    team = g.mix(scratches, g.mix(grain, lin((0.78, 0.78, 0.78)), lin((0.86, 0.86, 0.86))), lin((0.62, 0.62, 0.62)))

    albedo = steel
    for m, col in zip(mask[1:], (chrome, plate, hose, red, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.5, 0.18, 0.6, 0.45, 0.55, 0.55))
    metallic = weighted((0.8, 1.0, 0.7, 0.8, 0.4, 0.4))
    normal = g.bump(g.math('MAXIMUM', g.noise(80.0, 3.0), g.math('MULTIPLY', scratches, 0.5)), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------

def _clear():
    """Removes what an earlier run left in the session, so names stay free."""
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _uv_together(objs):
    """One smart projection over all meshes, so they share the texture without overlaps at the same texel size."""
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.005)
    bpy.ops.object.mode_set(mode='OBJECT')


def build():
    _clear()
    meshes = [_body(), _fins(), _sphere()]
    empties = [_empty("bolt_origin", (0.0, 0.0, SPHERE_Z))]
    for i in range(4):
        diag = math.pi / 4 + i * math.pi / 2
        empties.append(_empty(f"light_socket_{i + 1}", (0.34 * math.cos(diag), 0.34 * math.sin(diag), SOCKET_Z)))
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # round bodies smooth, plates and edges sharp
    _uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, empties, mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, empties, mat, graph = build()
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"
              for obj in meshes]
    report.append(f"total triangles {sum(rc.tri_count(obj) for obj in meshes)}")
    base = os.path.join(rc.SRC_ROOT, "building", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + empties
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(objs, [o.name[len(PREFIX):] for o in objs]):
        rc.save_blend(objs, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
