"""Alliance Grand Cannon: France's long-range gun (docs/ra2_buildings/allies/grand_cannon).

Footprint 2x2 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground. The turret's
front, as a unit's, looks along +Y in Blender (-Z in Godot) at rest. The composition follows the owner's references; the parts are
our own:
    base      four arms in a cross out from a round plinth, a round pad with a hole at each end, a stepped ring with blocks
    turret    a turntable on the ring; a block housing with a rounded front, a toothed wheel on each side, a tall back shield; a
              long barrel out of it with collars and a forked muzzle brake
No emblems, no text. The housing, the back shield and two collars on the barrel take the owner's colour, where the references have
the blue. Height 0.88 to the top of the shield. Budget 20000 triangles, texture 1024.

The game turns the turret, barrel and all, to its target (PlayerBuildings).

Nodes (docs/art/model_standards.md, section 4):
    body            the arms, the pads, the plinth and the ring
    turret          the turntable, the wheels and the barrel: turns about its Y, its origin on the ring's axis
    team_mask_turret under turret: the housing, the shield and the collars
    muzzle_0        under turret: empty at the muzzle
    base            the round slab under the plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_grand_cannon as agc
    print(agc.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_grand_cannon"
PREFIX = ID + "_"
SIZE = 1024


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, STEEL, DARK, LIGHT, TEAM = (_part(i) for i in range(5))
PARTS = 5

PLINTH_H = 0.04
RING_TOP = 0.22
PIVOT = Vector((0.0, 0.0, RING_TOP))
AXIS_Z = 0.58  # the barrel's axis height
BARREL_TIP = 0.98  # how far along +Y the muzzle reaches


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _base(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLINTH_H, 0.55), (0.1, 0.55), (0.12, 0.5), (0.12, 0.46), (0.16, 0.46), (0.18, 0.42), (RING_TOP, 0.42),
                                     (RING_TOP, 0.3)], STEEL, sides=40)
    for k in range(12):  # blocks round the ring
        a = 2 * math.pi * (k + 0.5) / 12
        q = Vector((0.5 * math.cos(a), 0.5 * math.sin(a), 0.0))
        p.box((q.x - 0.035, q.y - 0.035, 0.1), (q.x + 0.035, q.y + 0.035, 0.15), DARK)
    for k in range(4):  # the arms, tapering out to round pads
        a = math.pi / 4 + k * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        outline = [out * 0.4 + side * 0.22, out * 0.88 + side * 0.07, out * 0.88 - side * 0.07, out * 0.4 - side * 0.22]
        p.loft([[q + Vector((0, 0, z)) for q in outline] for z in (PLINTH_H, 0.09)], STEEL)
        p.bar(out * 0.45 + Vector((0, 0, 0.1)), out * 0.82 + Vector((0, 0, 0.1)), 0.02, DARK)  # a strut along it
        c = out * 0.88
        p.turned(c, 'Z', [(PLINTH_H, 0.1), (0.1, 0.1), (0.1, 0.05), (0.08, 0.05)], LIGHT, sides=16)  # its pad with a hole


def _turret():
    t = Parts()
    t.turned((0.0, 0.0, 0.0), 'Z', [(RING_TOP, 0.32), (RING_TOP + 0.05, 0.32), (RING_TOP + 0.05, 0.25)], DARK, sides=32)
    axis = Vector((0.0, 0.0, AXIS_Z))
    for x in (-0.24, 0.24):  # the toothed wheels on the sides
        c = axis + Vector((x, -0.05, 0.0))
        teeth = []
        for i in range(32):
            a = 2 * math.pi * i / 32
            r = 0.22 if i % 2 == 0 else 0.19
            teeth.append(Vector((0.0, r * math.cos(a), r * math.sin(a))))
        t.loft([[c + q + Vector((dx, 0.0, 0.0)) for q in teeth] for dx in (-0.025, 0.025)], STEEL)
        t.turned(c, 'X', [(-0.05, 0.07), (0.05, 0.07)], LIGHT, sides=14)  # its hub
        t.box((c.x - 0.03, c.y - 0.08, RING_TOP + 0.05), (c.x + 0.03, c.y + 0.08, c.z - 0.05), DARK)  # its support
    # The barrel: out of the housing along +Y, with collars, a forked muzzle brake.
    stations = [(0.2, 0.1), (0.4, 0.1), (0.42, 0.08), (0.62, 0.07), (0.64, 0.085), (0.7, 0.085), (0.72, 0.065), (BARREL_TIP - 0.14, 0.065)]
    t.loft([_ring(axis + Vector((0.0, y, 0.0)), Vector((0.0, 1.0, 0.0)), r, 16) for y, r in stations], STEEL)
    for k in range(4):  # the muzzle brake's four jaws
        a = math.pi / 4 + k * math.pi / 2
        d = Vector((math.cos(a), 0.0, math.sin(a)))
        t.bar(axis + Vector((0.0, BARREL_TIP - 0.16, 0.0)) + d * 0.06, axis + Vector((0.0, BARREL_TIP, 0.0)) + d * 0.08, 0.025, LIGHT)
    turret = t.finish(PREFIX + "turret")

    h = Parts()
    # The housing: a block with a rounded front around the barrel's root.
    h.box((-0.2, -0.32, AXIS_Z - 0.16), (0.2, 0.12, AXIS_Z + 0.16), TEAM)
    h.turned(axis, 'Y', [(0.12, 0.16), (0.22, 0.13)], TEAM, sides=20)
    # The tall back shield, curved, behind the wheels.
    steps = 10
    outer = [Vector((0.34 * math.sin(math.pi * (i / steps - 0.5)), -0.34 - 0.06 * math.cos(math.pi * (i / steps - 0.5)), 0.0)) for i in range(steps + 1)]
    inner = [q + Vector((0.0, 0.05, 0.0)) for q in reversed(outer)]
    h.loft([[q + Vector((0.0, 0.0, z)) for q in outer + inner] for z in (RING_TOP + 0.05, 0.88)], TEAM)
    for y in (0.48, 0.67):  # two collars on the barrel
        h.turned(axis, 'Y', [(y, 0.09), (y + 0.05, 0.09)], TEAM, sides=16)
    housing = h.finish(PREFIX + "team_mask_turret")

    c = PIVOT
    turret.data.transform(Matrix.Translation(-c))
    turret.location = c
    housing.data.transform(Matrix.Translation(-c))
    housing.parent = turret
    m = bpy.data.objects.new(PREFIX + "muzzle_0", None)
    m.empty_display_type = 'PLAIN_AXES'
    m.empty_display_size = 0.05
    m.parent = turret
    m.location = axis + Vector((0.0, BARREL_TIP + 0.03, 0.0)) - c
    bpy.context.scene.collection.objects.link(m)
    bpy.context.view_layer.update()
    return turret, housing, m


def _material():
    """Concrete, grey steel, dark steel, light steel, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.12)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    grain = g.noise(40.0, 4.0)

    concrete = g.mix(grain, lin((0.56, 0.56, 0.55)), lin((0.64, 0.64, 0.63)))
    steel = g.mix(grain, lin((0.50, 0.52, 0.55)), lin((0.58, 0.60, 0.63)))
    dark = g.mix(grain, lin((0.14, 0.15, 0.17)), lin((0.20, 0.21, 0.23)))
    light = g.mix(grain, lin((0.74, 0.75, 0.77)), lin((0.82, 0.83, 0.85)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (steel, dark, light, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.45, 0.5, 0.35, 0.4))
    metallic = weighted((0.0, 0.6, 0.6, 0.6, 0.3))
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
    _base(body)
    body = body.finish(PREFIX + "body")
    turret, housing, muzzle = _turret()
    base = Parts()
    base.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.57), (PLINTH_H, 0.57), (PLINTH_H, 0.01)], CONCRETE, sides=40)
    base = base.finish(PREFIX + "base")
    meshes = [body, turret, housing, base]
    _drop_bottoms([body, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [muzzle], mat, graph


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
