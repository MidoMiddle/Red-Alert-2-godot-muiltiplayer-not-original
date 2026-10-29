"""Alliance gap generator: veils the cells round it from the enemy (docs/ra2_buildings/allies/gap_generator).

Footprint 1x1 cell (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    base      a square plinth, four drums on its diagonals with ribbed fins round their feet and caps on top, a low hub
              between them, arched straps over the drums' outer sides
    column    four pipes bending up from the drums' caps into a column, coils round them, a collar over them
    crown     a dark disc on the collar, four prongs standing up out of it with light panels on their inner faces
No emblems, no text. The straps and the collar take the owner's colour, where the references have the blue. Height 1.55 to the
tips of the prongs. Budget 20000 triangles, texture 1024.

Nodes (docs/art/model_standards.md, section 4):
    body            the plinth, the drums, the fins, the hub, the pipes, the coils and the crown
    team_mask       the straps and the collar: the game paints them in the owner's colour
    base            the plinth's slab (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_gap_generator as agg
    print(agg.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_gap_generator"
PREFIX = ID + "_"
SIZE = 1024


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, STEEL, LIGHT, DARK, PANEL, TEAM = (_part(i) for i in range(6))
PARTS = 6

PLINTH_H = 0.04
DRUMS = [Vector((0.18 * math.cos(a), 0.18 * math.sin(a), 0.0)) for a in (math.pi / 4 + k * math.pi / 2 for k in range(4))]
DRUM_TOP = 0.36
COLUMN_Z = (0.62, 0.95)
COLLAR_Z = 1.0
CROWN_Z = 1.06


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


def _body(p):
    p.box((-0.42, -0.42, PLINTH_H), (0.42, 0.42, 0.07), CONCRETE)
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.07, 0.1), (0.3, 0.1), (0.32, 0.08)], STEEL, sides=16)  # the hub
    for c in DRUMS:
        p.turned(c, 'Z', [(0.07, 0.12), (0.24, 0.12), (0.26, 0.11), (0.28, 0.12), (DRUM_TOP - 0.03, 0.12), (DRUM_TOP, 0.1), (DRUM_TOP, 0.06)],
                 STEEL, sides=20)
        p.turned(c, 'Z', [(DRUM_TOP, 0.06), (DRUM_TOP + 0.04, 0.05)], DARK, sides=12)  # its cap
        out = Vector((c.x, c.y, 0.0)).normalized()
        for i in range(9):  # ribbed fins round its outer foot
            a = math.atan2(out.y, out.x) - 1.2 + 2.4 * i / 8
            d = Vector((math.cos(a), math.sin(a), 0.0))
            q0, q1 = c + d * 0.12, c + d * 0.17
            side = Vector((-d.y, d.x, 0.0)) * 0.006
            p.loft([[q + Vector((0, 0, z)) for q in (q0 - side, q1 - side, q1 + side, q0 + side)] for z in (0.07, 0.24)], DARK)
        # A pipe bending up from the cap into the column.
        top = c + Vector((0.0, 0.0, DRUM_TOP + 0.04))
        mid = c * 0.35 + Vector((0.0, 0.0, COLUMN_Z[0]))
        _pipe(p, [top, top + Vector((0, 0, 0.1)), top + Vector((0, 0, 0.18)) - out * 0.03, mid - Vector((0, 0, 0.05)), mid,
                  mid + Vector((0, 0, COLUMN_Z[1] - COLUMN_Z[0]))], 0.03, LIGHT)
    for z in (0.7, 0.78, 0.86):  # coils round the column
        p.turned((0.0, 0.0, 0.0), 'Z', [(z - 0.015, 0.1), (z, 0.11), (z + 0.015, 0.1)], LIGHT, sides=16)
    p.turned((0.0, 0.0, 0.0), 'Z', [(COLUMN_Z[1], 0.12), (CROWN_Z, 0.13), (CROWN_Z + 0.04, 0.1), (CROWN_Z + 0.06, 0.06)], DARK, sides=20)
    for k in range(4):  # the prongs
        a = math.pi / 4 + k * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        prof = [(0.05, CROWN_Z), (0.2, CROWN_Z), (0.24, CROWN_Z + 0.08), (0.2, 1.55), (0.15, 1.4), (0.13, CROWN_Z + 0.1), (0.05, CROWN_Z + 0.08)]
        p.loft([[out * r + Vector((0, 0, z)) + side * s for r, z in prof] for s in (-0.035, 0.035)], DARK)
        # The light panel on its inner face.
        q = [out * 0.128 + Vector((0, 0, z)) + side * s for z, s in ((CROWN_Z + 0.14, -0.022), (CROWN_Z + 0.14, 0.022), (1.38, 0.022),
                                                                    (1.38, -0.022))]
        p.loft([q, [v - out * 0.006 for v in q]], PANEL)


def _team(p):
    for c in DRUMS:
        out = Vector((c.x, c.y, 0.0)).normalized()
        side = Vector((-out.y, out.x, 0.0))
        # An arched strap over the drum's outer side: up from the plinth, over, down.
        prof = [(0.07, 0.17), (0.3, 0.17), (0.38, 0.12), (0.38, 0.06), (0.32, 0.06), (0.3, 0.13), (0.07, 0.13)]
        p.loft([[c + out * r + Vector((0, 0, z)) + side * s for z, r in prof] for s in (-0.04, 0.04)], TEAM)
    p.turned((0.0, 0.0, 0.0), 'Z', [(COLUMN_Z[1] - 0.03, 0.14), (COLLAR_Z, 0.15), (COLLAR_Z + 0.03, 0.14), (COLLAR_Z + 0.03, 0.1)], TEAM, sides=24)


def _material():
    """Light concrete, grey steel, light pipes, dark steel, yellow panels, a light team zone."""
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

    concrete = g.mix(grain, lin((0.66, 0.66, 0.64)), lin((0.74, 0.74, 0.72)))
    steel = g.mix(grain, lin((0.44, 0.45, 0.48)), lin((0.52, 0.53, 0.56)))
    light = g.mix(grain, lin((0.70, 0.71, 0.73)), lin((0.78, 0.79, 0.81)))
    dark = g.mix(grain, lin((0.14, 0.14, 0.16)), lin((0.20, 0.20, 0.22)))
    panel = g.mix(grain, lin((0.92, 0.68, 0.14)), lin((0.98, 0.76, 0.22)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (steel, light, dark, panel, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.45, 0.35, 0.5, 0.3, 0.4))
    metallic = weighted((0.0, 0.6, 0.6, 0.6, 0.2, 0.3))
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
        low = [f for f in bm.faces if f.normal.z < -0.99 and (obj.matrix_world @ f.calc_center_median()).z < 0.07 + 1e-3]
        bmesh.ops.delete(bm, geom=low, context='FACES')
        bm.to_mesh(obj.data)
        bm.free()


def build():
    _clear()
    body = Parts()
    _body(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-0.45, -0.45, 0.0), (0.45, 0.45, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    _drop_bottoms([body, team, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [], mat, graph


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
