"""Flak cannon: the Union's anti-air gun (docs/ra2_buildings/soviets/flak_cannon).

Footprint 1x1 cell (game/data/factions/union/buildings.yaml), origin at the centre of the footprint on the ground. The turret's
front, as a unit's, looks along +Y in Blender (-Z in Godot) at rest. The composition follows the owner's references; the parts are
our own:
    base      four outriggers in a cross out from a low block, a round pad at the end of each
    turret    a turntable on the block, two cheeks with bolts, a long barrel between them raised steeply, two recoil cylinders
              under it, a band round it
No emblems, no text. The stripes on the outriggers, the cheeks and the band round the barrel take the owner's colour, where the
references have the red. Height 1.15 to the muzzle. Budget 20000 triangles, texture 512.

The game turns the turret to its target (PlayerBuildings).

Nodes (docs/art/model_standards.md, section 4):
    body            the block, the outriggers and the pads
    team_mask       the stripes on the outriggers
    turret          the turntable, the barrel and the cylinders: turns about its Y, its origin on the block's axis
    team_mask_turret under turret: the cheeks and the band
    muzzle_0        under turret: empty at the muzzle
    base            the slab under the block (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.flak_cannon as fc
    print(fc.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "flak_cannon"
PREFIX = ID + "_"
SIZE = 512


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


GREY, DARK, STEEL, TEAM = (_part(i) for i in range(4))
PARTS = 4

PLINTH_H = 0.03
BLOCK_TOP = 0.2
PIVOT = Vector((0.0, 0.0, BLOCK_TOP))
TRUNNION = Vector((0.0, -0.02, 0.4))
ELEVATION = math.radians(60)
BARREL = 0.85


def _along(p, out, r0, r1, half, z0, z1, part):
    side = Vector((-out.y, out.x, 0.0))
    corners = [out * r + side * s for r, s in ((r0, -half), (r1, -half), (r1, half), (r0, half))]
    p.loft([[q + Vector((0.0, 0.0, z)) for q in corners] for z in (z0, z1)], part)


def _base(p):
    p.loft([[Vector((x, y, z)) for x, y in ((-r, -r), (r, -r), (r, r), (-r, r))] for z, r in ((PLINTH_H, 0.2), (BLOCK_TOP, 0.16))], GREY)
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        _along(p, out, 0.12, 0.4, 0.06, PLINTH_H, 0.1, GREY)
        p.turned(out * 0.4, 'Z', [(PLINTH_H, 0.08), (0.06, 0.08), (0.08, 0.05)], DARK, sides=14)  # its pad


def _team(p):
    for k in range(4):
        a = math.pi / 4 + k * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        _along(p, out, 0.2, 0.28, 0.064, 0.098, 0.104, TEAM)


def _turret():
    t = Parts()
    t.turned((0.0, 0.0, 0.0), 'Z', [(BLOCK_TOP, 0.15), (BLOCK_TOP + 0.05, 0.15), (BLOCK_TOP + 0.05, 0.1), (BLOCK_TOP + 0.1, 0.09)], DARK, sides=20)
    t.turned(TRUNNION, 'X', [(-0.11, 0.045), (0.11, 0.045)], STEEL, sides=12)  # the trunnion
    rot = Matrix.Rotation(-(math.pi / 2 - ELEVATION), 3, 'X')  # +Z tipped forward to the elevation
    up = rot @ Vector((0.0, 0.0, 1.0))
    breech = TRUNNION - up * 0.12
    stations = [(0.0, 0.07), (0.25, 0.065), (0.3, 0.05), (BARREL, 0.04), (BARREL, 0.03), (BARREL - 0.04, 0.03)]
    t.loft([[breech + up * s + (rot @ Vector((math.cos(a), math.sin(a), 0.0))) * r for a in (2 * math.pi * i / 16 for i in range(16))]
            for s, r in stations], STEEL)
    for x in (-0.06, 0.06):  # the recoil cylinders under it
        q = breech + rot @ Vector((x, -0.07, 0.0))
        t.loft([[q + up * s + (rot @ Vector((math.cos(a), math.sin(a), 0.0))) * 0.025 for a in (2 * math.pi * i / 10 for i in range(10))]
                for s in (0.05, 0.42)], DARK)
    turret = t.finish(PREFIX + "turret")

    h = Parts()
    for x in (-0.13, 0.13):  # the cheeks, with a slanted top
        h.profile_x([(-0.12, BLOCK_TOP + 0.05), (0.1, BLOCK_TOP + 0.05), (0.1, 0.42), (-0.02, 0.52), (-0.12, 0.46)], x - 0.02, x + 0.02, TEAM)
    band = breech + up * 0.28
    h.loft([[band + up * s + (rot @ Vector((math.cos(a), math.sin(a), 0.0))) * 0.075 for a in (2 * math.pi * i / 16 for i in range(16))]
            for s in (0.0, 0.07)], TEAM)
    cheeks = h.finish(PREFIX + "team_mask_turret")

    c = PIVOT
    turret.data.transform(Matrix.Translation(-c))
    turret.location = c
    cheeks.data.transform(Matrix.Translation(-c))
    cheeks.parent = turret
    m = bpy.data.objects.new(PREFIX + "muzzle_0", None)
    m.empty_display_type = 'PLAIN_AXES'
    m.empty_display_size = 0.04
    m.parent = turret
    m.location = breech + up * (BARREL + 0.02) - c
    bpy.context.scene.collection.objects.link(m)
    bpy.context.view_layer.update()
    return turret, cheeks, m


def _material():
    """Grey steel plates, dark steel, polished steel, a light team zone."""
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

    grey = g.mix(grain, lin((0.44, 0.45, 0.47)), lin((0.52, 0.53, 0.55)))
    dark = g.mix(grain, lin((0.14, 0.14, 0.15)), lin((0.20, 0.20, 0.21)))
    steel = g.mix(grain, lin((0.34, 0.35, 0.38)), lin((0.42, 0.43, 0.46)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = grey
    for m, col in zip(mask[1:], (dark, steel, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.6, 0.5, 0.4, 0.45))
    metallic = weighted((0.5, 0.6, 0.7, 0.3))
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
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    turret, cheeks, muzzle = _turret()
    base = Parts()
    base.box((-0.22, -0.22, 0.0), (0.22, 0.22, PLINTH_H), GREY)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, turret, cheeks, base]
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
