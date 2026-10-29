"""Alliance prism tower: the Alliance's beam defence (docs/ra2_buildings/allies/prism_tower).

Footprint 1x1 cell (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    base      an eight-sided plinth, a round housing with slots, four blocks on its diagonals
    column    a dark core with six pipes round it up to a studded collar, a grey cuff with windows over it, a short neck
    crown     a ring of eight mirror panels leaning out round a dark hub at the top: it turns while the owner has power
No emblems, no text. The blocks on the base and the bands under the collar and the cuff take the owner's colour, where the
references have the blue. Height 2.0 to the top of the crown. Budget 20000 triangles, texture 1024.

Nodes (docs/art/model_standards.md, section 4):
    body            the plinth, the housing, the column, the collar, the cuff and the neck
    spin_mirrors    the crown: the game turns it about its Y while the owner has power, its origin on the axis
    beam_origin     empty in the crown's middle: where the beam starts
    team_mask       the blocks and the bands: the game paints them in the owner's colour
    base            the plinth's slab (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_prism_tower as apt
    print(apt.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_prism_tower"
PREFIX = ID + "_"
SIZE = 1024


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


GREY, DARK, MIRROR, PIPE, GOLD, TEAM = (_part(i) for i in range(6))
PARTS = 6

PLINTH_H = 0.04
HOUSING_TOP = 0.3
COLLAR_Z = 1.15
CUFF = (1.24, 1.42)
CROWN_Z = 1.52


def _octagon(r, z):
    return [Vector((r * math.cos(a), r * math.sin(a), z)) for a in (math.pi / 8 + i * math.pi / 4 for i in range(8))]


def _body(p):
    p.loft([_octagon(0.44, z) for z in (PLINTH_H, 0.08)], GREY)
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.08, 0.34), (0.22, 0.32), (HOUSING_TOP, 0.24), (HOUSING_TOP, 0.12)], GREY, sides=24)
    for k in range(6):  # dark slots round the housing
        a = 2 * math.pi * (k + 0.5) / 6
        q = Vector((0.33 * math.cos(a), 0.33 * math.sin(a), 0.16))
        p.box((q.x - 0.03, q.y - 0.03, q.z - 0.04), (q.x + 0.03, q.y + 0.03, q.z + 0.04), DARK)
    p.turned((0.0, 0.0, 0.0), 'Z', [(HOUSING_TOP, 0.07), (COLLAR_Z, 0.07)], DARK, sides=12)  # the core
    for k in range(6):  # the pipes round it, on spools at their feet
        a = 2 * math.pi * k / 6
        c = Vector((0.15 * math.cos(a), 0.15 * math.sin(a), 0.0))
        p.turned(c, 'Z', [(HOUSING_TOP, 0.05), (HOUSING_TOP + 0.06, 0.05), (HOUSING_TOP + 0.06, 0.03), (COLLAR_Z, 0.03)], PIPE, sides=10)
    p.turned((0.0, 0.0, 0.0), 'Z', [(COLLAR_Z, 0.22), (COLLAR_Z + 0.07, 0.22), (COLLAR_Z + 0.07, 0.15), (CUFF[0], 0.15)], DARK, sides=24)
    for k in range(10):  # studs on the collar
        a = 2 * math.pi * k / 10
        p.sphere((0.225 * math.cos(a), 0.225 * math.sin(a), COLLAR_Z + 0.035), 'Z', 0.022, GOLD, sides=8, rings=4)
    p.turned((0.0, 0.0, 0.0), 'Z', [(CUFF[0], 0.2), (CUFF[1], 0.17), (CUFF[1], 0.09), (CROWN_Z, 0.08)], GREY, sides=24)  # cuff, neck
    for k in range(8):  # its windows
        a = 2 * math.pi * k / 8
        q = Vector((0.188 * math.cos(a), 0.188 * math.sin(a), (CUFF[0] + CUFF[1]) / 2))
        p.box((q.x - 0.02, q.y - 0.02, q.z - 0.025), (q.x + 0.02, q.y + 0.02, q.z + 0.025), DARK)


def _crown():
    p = Parts()
    c = Vector((0.0, 0.0, CROWN_Z))
    p.turned(c, 'Z', [(0.0, 0.09), (0.3, 0.1), (0.32, 0.06)], DARK, sides=16)  # the hub
    for k in range(8):  # eight mirrors leaning out
        a = 2 * math.pi * k / 8
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        foot = c + out * 0.1 + Vector((0.0, 0.0, 0.02))
        top = c + out * 0.24 + Vector((0.0, 0.0, 0.42))
        panel = [foot - side * 0.05, foot + side * 0.05, top + side * 0.075, top - side * 0.075]
        p.loft([[q + out * d for q in panel] for d in (0.0, 0.035)], MIRROR)
        p.loft([[q + out * d + Vector((0, 0, 0)) for q in (foot - side * 0.06, foot + side * 0.06, foot + side * 0.06 + Vector((0, 0, 0.06)),
                                                            foot - side * 0.06 + Vector((0, 0, 0.06)))] for d in (-0.02, 0.05)], GREY)  # its clamp
    crown = p.finish(PREFIX + "spin_mirrors")
    crown.data.transform(Matrix.Translation(-c))
    crown.location = c
    return crown


def _team(p):
    for k in range(4):  # the blocks on the diagonals of the base
        a = math.pi / 4 + k * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        prof = [(0.22, 0.08), (0.42, 0.08), (0.42, 0.16), (0.3, 0.3), (0.22, 0.3)]
        p.loft([[out * r + Vector((0, 0, z)) + side * s for r, z in prof] for s in (-0.07, 0.07)], TEAM)
    p.turned((0.0, 0.0, 0.0), 'Z', [(COLLAR_Z - 0.04, 0.2), (COLLAR_Z, 0.2), (COLLAR_Z, 0.1)], TEAM, sides=24)  # band under the collar
    p.turned((0.0, 0.0, 0.0), 'Z', [(CUFF[1] - 0.02, 0.18), (CUFF[1] + 0.02, 0.18), (CUFF[1] + 0.02, 0.1)], TEAM, sides=24)  # over the cuff


def _material():
    """Grey steel, dark steel, light mirrors, blue-grey pipes, gold studs, a light team zone."""
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

    grey = g.mix(grain, lin((0.56, 0.58, 0.60)), lin((0.64, 0.66, 0.68)))
    dark = g.mix(grain, lin((0.08, 0.08, 0.09)), lin((0.13, 0.13, 0.14)))
    mirror = g.mix(grain, lin((0.80, 0.86, 0.92)), lin((0.88, 0.93, 0.98)))
    pipe = g.mix(grain, lin((0.46, 0.48, 0.62)), lin((0.54, 0.56, 0.70)))
    gold = g.mix(grain, lin((0.86, 0.62, 0.18)), lin((0.94, 0.72, 0.26)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = grey
    for m, col in zip(mask[1:], (dark, mirror, pipe, gold, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.45, 0.4, 0.12, 0.35, 0.3, 0.4))
    metallic = weighted((0.6, 0.6, 0.6, 0.6, 0.85, 0.3))
    normal = g.bump(g.noise(80.0, 3.0), 0.02, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _empty(name, point):
    node = bpy.data.objects.new(PREFIX + name, None)
    node.empty_display_type = 'PLAIN_AXES'
    node.empty_display_size = 0.06
    node.location = Vector(point)
    bpy.context.scene.collection.objects.link(node)
    return node


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
    crown = _crown()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.loft([_octagon(0.47, z) for z in (0.0, PLINTH_H)], GREY)
    base = base.finish(PREFIX + "base")
    meshes = [body, crown, team, base]
    _drop_bottoms([body, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [_empty("beam_origin", (0.0, 0.0, CROWN_Z + 0.3))], mat, graph


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
    rc.arrange(objs, ID)  # its place in the file: the row of the players' buildings
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(objs, [o.name[len(PREFIX):] for o in objs]):
        rc.save_blend(objs, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
