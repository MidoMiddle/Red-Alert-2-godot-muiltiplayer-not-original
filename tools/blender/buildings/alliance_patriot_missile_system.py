"""Alliance Patriot: the Alliance's anti-air defence (docs/ra2_buildings/allies/patriot_missile_system).

Footprint 1x1 cell (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground. The turret's
front, as a unit's, looks along +Y in Blender (-Z in Godot) at rest. The composition follows the owner's references; the parts are
our own:
    base      a round plinth, a broad ring on it, a polished dome with slots round its foot
    turret    a turntable on the dome, a short post with a coil, a yoke, and in it two launch pods side by side tilted up: each a
              box with four tubes in its face
No emblems, no text. The ring round the base and a band round each pod take the owner's colour, where the references have the
blue. Height 0.82 to the top of the pods. Budget 20000 triangles, texture 512.

The game turns the turret to its target (PlayerBuildings).

Nodes (docs/art/model_standards.md, section 4):
    body            the plinth, the dome
    turret          the turntable, the post, the yoke and the pods: turns about its Y, its origin on the dome's axis
    team_mask_turret under turret: the bands round the pods
    muzzle_0, muzzle_1 under turret: empties at the pods' faces
    team_mask       the ring round the base
    base            the plinth's slab (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_patriot_missile_system as apms
    print(apms.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_patriot_missile_system"
PREFIX = ID + "_"
SIZE = 512


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


GREY, CHROME, DARK, COIL, TEAM = (_part(i) for i in range(5))
PARTS = 5

PLINTH_H = 0.04
DOME_TOP = 0.3
PIVOT = Vector((0.0, 0.0, DOME_TOP))
TRUNNION_Z = 0.52
TILT = math.radians(35)  # the pods' rise
POD_X = (-0.15, 0.15)


def _body(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLINTH_H, 0.44), (0.07, 0.44), (0.07, 0.4)], DARK, sides=32)
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.12, 0.34), (0.16, 0.33), (0.22, 0.29), (0.27, 0.22), (DOME_TOP, 0.15), (DOME_TOP, 0.1)], CHROME, sides=32)
    for k in range(8):  # slots round the dome's foot
        a = 2 * math.pi * (k + 0.5) / 8
        q = Vector((0.335 * math.cos(a), 0.335 * math.sin(a), 0.135))
        p.box((q.x - 0.035, q.y - 0.035, q.z - 0.012), (q.x + 0.035, q.y + 0.035, q.z + 0.012), DARK)


def _pod(p, x):
    """A pod tilted up about X through the trunnion: a box with four tubes in its face."""
    rot = Matrix.Rotation(TILT, 3, 'X')
    o = Vector((x, 0.0, TRUNNION_Z))

    def at(q):
        return o + rot @ Vector(q)

    corners = [(-0.12, -0.12), (0.12, -0.12), (0.12, 0.12), (-0.12, 0.12)]
    p.loft([[at((cx, y, cz)) for cx, cz in corners] for y in (-0.18, 0.2)], GREY)
    d = rot @ Vector((0.0, 1.0, 0.0))
    for tx in (-0.06, 0.06):
        for tz in (-0.06, 0.06):
            c = at((tx, 0.2, tz))
            circle = [rot @ Vector((math.cos(a), 0.0, math.sin(a))) for a in (2 * math.pi * i / 12 for i in range(12))]
            # A tube's mouth: a thin grey rim, its dark hole on it.
            p.loft([[c + q * 0.05 for q in circle], [c + q * 0.05 + d * 0.012 for q in circle], [c + q * 0.04 + d * 0.012 for q in circle]],
                   GREY)
            p.loft([[c + q * 0.038 + d * 0.012 for q in circle], [c + q * 0.038 + d * 0.014 for q in circle]], DARK)
    return rot


def _turret():
    t = Parts()
    c = PIVOT
    t.turned((0.0, 0.0, 0.0), 'Z', [(DOME_TOP, 0.12), (DOME_TOP + 0.03, 0.12), (DOME_TOP + 0.03, 0.05), (TRUNNION_Z - 0.06, 0.05)], DARK, sides=16)
    for i in range(4):  # the coil round the post
        z = DOME_TOP + 0.06 + i * 0.03
        t.turned((0.0, 0.0, 0.0), 'Z', [(z, 0.07), (z + 0.018, 0.07)], COIL, sides=16)
    t.box((-0.03, -0.06, TRUNNION_Z - 0.08), (0.03, 0.06, TRUNNION_Z + 0.02), GREY)  # the yoke
    t.turned(Vector((0.0, 0.0, TRUNNION_Z)), 'X', [(-0.29, 0.035), (0.29, 0.035)], DARK, sides=12)  # the trunnion
    for x in POD_X:
        _pod(t, x)
    turret = t.finish(PREFIX + "turret")

    h = Parts()
    for x in POD_X:
        rot = Matrix.Rotation(TILT, 3, 'X')
        o = Vector((x, 0.0, TRUNNION_Z))
        corners = [(-0.126, -0.126), (0.126, -0.126), (0.126, 0.126), (-0.126, 0.126)]
        h.loft([[o + rot @ Vector((cx, y, cz)) for cx, cz in corners] for y in (-0.06, 0.04)], TEAM)
    bands = h.finish(PREFIX + "team_mask_turret")

    turret.data.transform(Matrix.Translation(-c))
    turret.location = c
    bands.data.transform(Matrix.Translation(-c))
    bands.parent = turret
    muzzles = []
    for i, x in enumerate(POD_X):
        m = bpy.data.objects.new(PREFIX + f"muzzle_{i}", None)
        m.empty_display_type = 'PLAIN_AXES'
        m.empty_display_size = 0.04
        m.parent = turret
        m.location = Vector((x, 0.0, TRUNNION_Z)) + Matrix.Rotation(TILT, 3, 'X') @ Vector((0.0, 0.24, 0.0)) - c
        bpy.context.scene.collection.objects.link(m)
        muzzles.append(m)
    bpy.context.view_layer.update()
    return turret, bands, muzzles


def _material():
    """Light grey panels, polished chrome, dark steel, a blue-grey coil, a light team zone."""
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

    grey = g.mix(grain, lin((0.62, 0.64, 0.70)), lin((0.70, 0.72, 0.78)))
    chrome = g.mix(grain, lin((0.76, 0.78, 0.80)), lin((0.86, 0.87, 0.89)))
    dark = g.mix(grain, lin((0.12, 0.12, 0.14)), lin((0.18, 0.18, 0.20)))
    coil = g.mix(grain, lin((0.40, 0.40, 0.62)), lin((0.48, 0.48, 0.70)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = grey
    for m, col in zip(mask[1:], (chrome, dark, coil, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.45, 0.2, 0.5, 0.35, 0.4))
    metallic = weighted((0.5, 0.6, 0.6, 0.6, 0.3))
    normal = g.bump(g.noise(80.0, 3.0), 0.02, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _drop_bottoms(objs):
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
    _body(body)
    body = body.finish(PREFIX + "body")
    turret, bands, muzzles = _turret()
    team = Parts()
    team.turned((0.0, 0.0, 0.0), 'Z', [(0.07, 0.4), (0.12, 0.38), (0.12, 0.3)], TEAM, sides=32)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.46), (PLINTH_H, 0.46), (PLINTH_H, 0.01)], DARK, sides=32)
    base = base.finish(PREFIX + "base")
    meshes = [body, turret, bands, team, base]
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
