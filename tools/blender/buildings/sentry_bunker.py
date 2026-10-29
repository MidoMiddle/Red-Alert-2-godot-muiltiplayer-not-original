"""Sentry gun: the Union's light defence, two gatlings on a turning mount (docs/ra2_buildings/soviets/sentry_gun), sentry_bunker in
the data.

Footprint 1x1 cell (game/data/factions/union/buildings.yaml), origin at the centre of the footprint on the ground. The turret's
front, as a unit's, looks along +Y in Blender (-Z in Godot) at rest. The composition follows the owner's references; the parts are
our own:
    base      a square concrete plinth with its corners cut, a round drum on it, four bent pipes from the drum down to the plinth
    turret    a turntable on the drum, a yoke, and on it two gatlings side by side: each a red housing at the back, a barrel
              casing with collars, six barrels round its axis at the front
No emblems, no text. The red housings take the owner's colour. Height 0.8 to the top of the housings. Budget 20000 triangles,
texture 512.

The game turns the turret to its target, and spins the gatlings' barrels while it fires (PlayerBuildings).

Nodes (docs/art/model_standards.md, section 4):
    body            the plinth, the drum and the pipes
    turret          the turntable, the yoke and the casings: turns about its Y, its origin on the drum's axis
    gatling_0, gatling_1    under turret: the barrels of each gun, their origins on their own axes: the game spins them about Z
    team_mask_turret        under turret: the red housings, in the owner's colour
    muzzle_0, muzzle_1      under turret: empties at the muzzles
    base            the plinth's slab (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.sentry_bunker as sb
    print(sb.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "sentry_bunker"
PREFIX = ID + "_"
SIZE = 512


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, STEEL, DARK, TEAM = (_part(i) for i in range(4))
PARTS = 4

PLINTH_H = 0.04
DRUM_TOP = 0.34
PIVOT = Vector((0.0, 0.0, DRUM_TOP + 0.04))
GUN_Z = 0.58  # the guns' axis height
GUN_X = (-0.12, 0.12)
MUZZLE_Y = 0.46


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _pipe(p, points, radius, part, sides=10):
    pts = [Vector(q) for q in points]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        rings.append(_ring(q, d, radius, sides))
    p.loft(rings, part)


# --- body -------------------------------------------------------------------------

def _base(p):
    cut = 0.12
    outline = [(0.45, -0.45 + cut), (0.45, 0.45 - cut), (0.45 - cut, 0.45), (-0.45 + cut, 0.45), (-0.45, 0.45 - cut), (-0.45, -0.45 + cut),
               (-0.45 + cut, -0.45), (0.45 - cut, -0.45)]
    p.loft([[Vector((x, y, z)) for x, y in outline] for z in (PLINTH_H, 0.1)], CONCRETE)
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.1, 0.27), (0.16, 0.27), (0.2, 0.24), (DRUM_TOP, 0.24), (DRUM_TOP, 0.2)], STEEL, sides=24)
    for k in range(4):  # bent pipes from the drum down to the plinth, on the diagonals
        a = math.pi / 4 + k * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        start = out * 0.22 + Vector((0.0, 0.0, 0.26))
        _pipe(p, [start, start + out * 0.08, start + out * 0.14 + Vector((0, 0, -0.04)), start + out * 0.15 + Vector((0, 0, -0.14))], 0.03, STEEL)
        foot = start + out * 0.15 + Vector((0.0, 0.0, -0.16))
        p.turned(foot, 'Z', [(0.0, 0.045), (0.02, 0.045)], DARK, sides=10)


# --- the turret -------------------------------------------------------------------

def _turret():
    """The turret and the nodes under it, built in place at rest (the guns along +Y)."""
    t = Parts()
    c = PIVOT
    t.turned((0.0, 0.0, 0.0), 'Z', [(DRUM_TOP, 0.2), (c.z, 0.2), (c.z, 0.12)], DARK, sides=24)  # the turntable
    for x in (-0.24, 0.24):  # the yoke's cheeks
        t.box((x - 0.025, -0.08, c.z), (x + 0.025, 0.08, GUN_Z + 0.04), STEEL)
    t.box((-0.24, -0.06, c.z), (0.24, 0.06, c.z + 0.05), STEEL)
    for x in GUN_X:  # the casings with collars, from the housing forwards
        a = Vector((x, -0.02, GUN_Z))
        t.turned(a, 'Y', [(0.0, 0.085), (0.18, 0.085), (0.18, 0.1), (0.22, 0.1), (0.22, 0.07), (0.3, 0.07), (0.3, 0.085), (0.33, 0.085),
                          (0.33, 0.06)], STEEL, sides=16)
    turret = t.finish(PREFIX + "turret")

    h = Parts()  # the red housings at the back, in the owner's colour
    for x in GUN_X:
        h.box((x - 0.1, -0.3, GUN_Z - 0.09), (x + 0.1, -0.02, GUN_Z + 0.1), TEAM)
        h.loft([[Vector((xx, y, z)) for xx, z in ((x - 0.1, GUN_Z - 0.09), (x + 0.1, GUN_Z - 0.09), (x + 0.1, GUN_Z + 0.03), (x - 0.1, GUN_Z + 0.03))]
                for y in (-0.3, -0.36)], TEAM)
    housings = h.finish(PREFIX + "team_mask_turret")

    guns = []
    for i, x in enumerate(GUN_X):
        g = Parts()
        axis = Vector((x, 0.0, GUN_Z))
        for k in range(6):
            a = 2 * math.pi * k / 6
            q = Vector((x + 0.035 * math.cos(a), 0.0, GUN_Z + 0.035 * math.sin(a)))
            g.turned(q + Vector((0.0, 0.3, 0.0)), 'Y', [(0.0, 0.016), (MUZZLE_Y - 0.3, 0.016)], DARK, sides=8)
        g.turned(axis + Vector((0.0, 0.31, 0.0)), 'Y', [(0.0, 0.06), (0.03, 0.06)], STEEL, sides=12)  # the clamp round them
        g.turned(axis + Vector((0.0, MUZZLE_Y - 0.06, 0.0)), 'Y', [(0.0, 0.058), (0.03, 0.058)], STEEL, sides=12)
        gun = g.finish(PREFIX + f"gatling_{i}")
        gun.data.transform(Matrix.Translation(-axis))
        gun.location = axis
        guns.append(gun)

    turret.data.transform(Matrix.Translation(-c))
    turret.location = c
    housings.data.transform(Matrix.Translation(-c))
    housings.parent = turret
    for gun in guns:
        gun.location = gun.location - c
        gun.parent = turret
    muzzles = []
    for i, x in enumerate(GUN_X):
        m = bpy.data.objects.new(PREFIX + f"muzzle_{i}", None)
        m.empty_display_type = 'PLAIN_AXES'
        m.empty_display_size = 0.04
        m.parent = turret
        m.location = Vector((x, MUZZLE_Y + 0.02, GUN_Z)) - c
        bpy.context.scene.collection.objects.link(m)
        muzzles.append(m)
    bpy.context.view_layer.update()
    return turret, [housings, *guns], muzzles


# --- material -----------------------------------------------------------------------

def _material():
    """Grey concrete, grey steel, dark steel, a light team zone."""
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

    concrete = g.mix(grain, lin((0.50, 0.50, 0.48)), lin((0.58, 0.58, 0.56)))
    steel = g.mix(grain, lin((0.46, 0.47, 0.50)), lin((0.56, 0.57, 0.60)))
    dark = g.mix(grain, lin((0.14, 0.14, 0.15)), lin((0.20, 0.20, 0.21)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (steel, dark, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.45, 0.5, 0.45))
    metallic = weighted((0.0, 0.7, 0.7, 0.3))
    normal = g.bump(g.noise(80.0, 3.0), 0.02, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _drop_bottoms(objs):
    """Deletes the faces that look down onto the plinth or the ground: never seen, and black in the bake."""
    for obj in objs:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        low = [f for f in bm.faces if f.normal.z < -0.99 and (obj.matrix_world @ f.calc_center_median()).z < PLINTH_H + 1e-3]
        bmesh.ops.delete(bm, geom=low, context='FACES')
        bm.to_mesh(obj.data)
        bm.free()


def build():
    _clear()
    body = Parts()
    _base(body)
    body = body.finish(PREFIX + "body")
    turret, under, muzzles = _turret()
    base = Parts()
    base.box((-0.47, -0.47, 0.0), (0.47, 0.47, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, turret, *under, base]
    _drop_bottoms([body, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, muzzles, mat, graph


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
    top = [obj for obj in objs if obj.parent is None]
    rc.arrange(top, ID)  # its place in the file: the row of the players' buildings
    coll = bpy.data.collections[ID]
    for obj in objs:
        if obj not in top and coll not in obj.users_collection:
            for owner in list(obj.users_collection):
                owner.objects.unlink(obj)
            coll.objects.link(obj)
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(objs, [o.name[len(PREFIX):] for o in objs]):
        rc.save_blend(objs, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
