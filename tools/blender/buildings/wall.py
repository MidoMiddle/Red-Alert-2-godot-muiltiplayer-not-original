"""Wall: one segment of the Union's wall, placed by dragging (docs/ra2_buildings/soviets/wall).

Footprint 1x1 cell (game/data/factions/union/buildings.yaml), origin at the centre of the cell on the ground. The composition
follows the owner's references; the parts are our own:
    post      a brick post in the middle of the cell under a dark cap, on a concrete foot
    joins     four half-walls from the post to the cell's edges, one each way: brick, a red band across, a slanted buttress on
              each face, spikes and a coil of wire on top. The game shows the ones that face a neighbouring wall of the same
              owner, so a line of segments is one wall, its corners and its ends right (PlayerBuildings)
No emblems, no text. The red bands take the owner's colour. Height 0.68 to the top of the post's cap. Budget 20000 triangles,
texture 512.

Nodes (docs/art/model_standards.md, section 4); the cells' north (y - 1) is -Z in Godot, +Y in Blender:
    body            the post and its foot
    join_n, join_e, join_s, join_w   the half-walls towards the cells north, east, south and west
    team_mask_n, team_mask_e, team_mask_s, team_mask_w   under each join: its red band, in the owner's colour
    base            the concrete foot under the post (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.wall as w
    print(w.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "wall"
PREFIX = ID + "_"
SIZE = 512


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, BRICK, DARK, STEEL, TEAM = (_part(i) for i in range(5))
PARTS = 5

PLINTH_H = 0.03
POST = 0.19  # the post's half-width
POST_TOP = 0.58
WALL_HALF = 0.14  # the half-walls' half-thickness
WALL_TOP = 0.46
# The joins: their name and the direction they run in Blender.
JOINS = (("n", Vector((0.0, 1.0, 0.0))), ("e", Vector((1.0, 0.0, 0.0))), ("s", Vector((0.0, -1.0, 0.0))), ("w", Vector((-1.0, 0.0, 0.0))))


def _along(p, out, r0, r1, half, z0, z1, part):
    """A box from r0 to r1 along out, half wide across, from z0 to z1."""
    side = Vector((-out.y, out.x, 0.0))
    corners = [out * r + side * s for r, s in ((r0, -half), (r1, -half), (r1, half), (r0, half))]
    p.loft([[q + Vector((0.0, 0.0, z)) for q in corners] for z in (z0, z1)], part)


# --- the post -----------------------------------------------------------------------

def _post(p):
    p.box((-POST, -POST, PLINTH_H), (POST, POST, POST_TOP), BRICK)
    p.loft([[Vector((x, y, z)) for x, y in ((-r, -r), (r, -r), (r, r), (-r, r))] for z, r in ((POST_TOP, POST + 0.04), (POST_TOP + 0.08, POST + 0.04),
                                                                                              (POST_TOP + 0.1, POST - 0.02))], DARK)  # the cap


# --- the joins ------------------------------------------------------------------------

def _join(name, out):
    side = Vector((-out.y, out.x, 0.0))
    p = Parts()
    _along(p, out, POST, 0.5, WALL_HALF, 0.0, WALL_TOP, BRICK)
    for s in (-1, 1):  # a slanted buttress on each face, triangular
        q0 = out * 0.36 + side * s * WALL_HALF
        q1 = out * 0.44 + side * s * WALL_HALF
        foot = side * s * 0.12
        p.loft([[q0 + Vector((0, 0, 0)), q0 + foot, q0 + Vector((0, 0, WALL_TOP + 0.04))],
                [q1 + Vector((0, 0, 0)), q1 + foot, q1 + Vector((0, 0, WALL_TOP + 0.04))]], STEEL)
    tip = out * 0.3 + Vector((0.0, 0.0, WALL_TOP))  # a spike on top
    p.loft([[tip + side * a + out * b for a, b in ((-0.03, -0.03), (0.03, -0.03), (0.03, 0.03), (-0.03, 0.03))],
            [tip + Vector((0, 0, 0.16))] * 4], STEEL)
    # A coil of wire along the top: small rings along it.
    for i in range(3):
        c = out * (POST + 0.06 + i * 0.1) + Vector((0.0, 0.0, WALL_TOP + 0.04))
        rings = []
        for k in range(9):
            a = 2 * math.pi * k / 8
            q = c + side * 0.05 * math.cos(a) + Vector((0.0, 0.0, 0.05 * math.sin(a)))
            rings.append(q)
        for a, b in zip(rings, rings[1:]):
            p.bar(a, b, 0.006, DARK)
    join = p.finish(PREFIX + "join_" + name)

    t = Parts()
    _along(t, out, 0.22, 0.3, WALL_HALF + 0.008, 0.0, WALL_TOP, TEAM)  # the red band across it
    team = t.finish(PREFIX + "team_mask_" + name)
    team.parent = join
    return join, team


# --- material -----------------------------------------------------------------------

def _material():
    """Concrete, pink-brown brick in courses, dark caps and wire, grey steel, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    grain = g.noise(40.0, 4.0)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    course = g.math('MULTIPLY', z, 14.0)
    shift = g.math('MULTIPLY', g.math('FLOOR', course), 0.5)
    along = g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 7.0), shift)
    joints = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', course), -10.0, 1.0)),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', along), -14.0, 1.0)))

    concrete = g.mix(grain, lin((0.50, 0.50, 0.48)), lin((0.58, 0.58, 0.56)))
    brick = g.mix(joints, g.mix(grain, lin((0.62, 0.42, 0.38)), lin((0.70, 0.50, 0.44))), lin((0.70, 0.66, 0.62)))
    dark = g.mix(grain, lin((0.10, 0.10, 0.11)), lin((0.16, 0.16, 0.17)))
    steel = g.mix(grain, lin((0.30, 0.31, 0.33)), lin((0.38, 0.39, 0.41)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (brick, dark, steel, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.85, 0.5, 0.45, 0.45))
    metallic = weighted((0.0, 0.0, 0.5, 0.7, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(80.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', joints, mask[1]), 0.7)), 0.03, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _drop_hidden(obj):
    """Deletes the faces that look down at the ground and the end that sits against the post: never seen, black in the bake."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    hidden = [f for f in bm.faces if (f.normal.z < -0.99 and f.calc_center_median().z < 1e-3)
              or (abs(f.normal.z) < 0.01 and Vector((f.calc_center_median().x, f.calc_center_median().y, 0.0)).length < POST + 1e-3
                  and f.normal.dot(Vector((f.calc_center_median().x, f.calc_center_median().y, 0.0)).normalized()) < -0.99)]
    bmesh.ops.delete(bm, geom=hidden, context='FACES')
    bm.to_mesh(obj.data)
    bm.free()


def build():
    _clear()
    body = Parts()
    _post(body)
    body = body.finish(PREFIX + "body")
    joins = []
    for name, out in JOINS:
        joins += _join(name, out)
    base = Parts()
    base.box((-POST - 0.05, -POST - 0.05, 0.0), (POST + 0.05, POST + 0.05, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, *joins, base]
    for obj in [body, base, *joins]:
        _drop_hidden(obj)
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, mat, graph = build()
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"
              for obj in meshes]
    report.append(f"total triangles {sum(rc.tri_count(obj) for obj in meshes)}")
    base = os.path.join(rc.SRC_ROOT, "building", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    top = [obj for obj in meshes if obj.parent is None]
    rc.arrange(top, ID)  # its place in the file: the row of the players' buildings
    coll = bpy.data.collections[ID]
    for obj in meshes:
        if obj not in top and coll not in obj.users_collection:
            for owner in list(obj.users_collection):
                owner.objects.unlink(obj)
            coll.objects.link(obj)
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(meshes, [o.name[len(PREFIX):] for o in meshes]):
        rc.save_blend(meshes, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(meshes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
