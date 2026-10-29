"""Alliance spy satellite uplink: reveals the map to its owner (docs/ra2_buildings/allies/spy_satellite_uplink).

Footprint 2x2 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground; the camera
sees the south (-Y in Blender, +Z in Godot) and east (+X) sides. The composition follows the owner's references; the parts are
our own:
    tower     in the south-west (on the left of the camera's view): a round foot ringed by eight pipes standing round it, a pale column with a seam, a bowl at its top,
              a small flat antenna on a fork that turns while the owner has power
    dish      in the north-east (on its right): a round plinth, a box frame on it, a large dish tilted up to the south-east (towards the camera) on it with a ribbed face
              and a feed held by three struts
No emblems, no text. The rings round the tower and the dish's plinth and the bowl take the owner's colour, where the references
have the blue. Height 1.6 to the top of the dish. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the tower, the pipes, the dish's plinth, frame, dish and feed
    spin_antenna    the antenna on the tower: the game turns it about its Y while the owner has power
    team_mask       the rings and the bowl: the game paints them in the owner's colour
    base            the slab under both (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_spy_satellite_uplink as assu
    print(assu.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_spy_satellite_uplink"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, STEEL, PALE, DARK, DISH, BRONZE, TEAM = (_part(i) for i in range(7))
PARTS = 7

PLINTH_H = 0.04
TOWER = Vector((-0.5, -0.45, 0.0))
TOWER_TOP = 1.05
DISH_BASE = Vector((0.38, 0.35, 0.0))
DISH_C = Vector((0.38, 0.35, 0.95))  # the dish's apex
DISH_AXIS = Vector((0.3, -0.75, 0.65)).normalized()
DISH_R = 0.55
DISH_DEPTH = 0.22


def _ring(centre, d, radius, sides, phase=0.0):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (phase + 2 * math.pi * i / sides for i in range(sides))]


def _tower(p):
    c = TOWER
    p.turned(c, 'Z', [(PLINTH_H, 0.36), (0.12, 0.36), (0.14, 0.3), (0.32, 0.3), (0.36, 0.22)], STEEL, sides=24)  # the foot
    for k in range(8):  # pipes standing round it, open at the top
        a = 2 * math.pi * k / 8
        q = c + Vector((0.33 * math.cos(a), 0.33 * math.sin(a), 0.0))
        p.turned(q, 'Z', [(PLINTH_H, 0.035), (0.4, 0.035), (0.4, 0.05), (0.46, 0.05), (0.46, 0.03), (0.42, 0.03)], DARK, sides=10)
    p.turned(c, 'Z', [(0.36, 0.18), (TOWER_TOP - 0.1, 0.16)], PALE, sides=24)  # the column
    p.box((c.x - 0.02, c.y - 0.17, 0.4), (c.x + 0.02, c.y - 0.15, TOWER_TOP - 0.12), DARK)  # its seam
    p.turned(c, 'Z', [(TOWER_TOP, 0.12), (TOWER_TOP + 0.01, 0.04)], DARK, sides=16)  # the bowl's floor


def _antenna():
    p = Parts()
    c = TOWER + Vector((0.0, 0.0, TOWER_TOP))
    for x in (-0.04, 0.04):  # the fork
        p.box((c.x + x - 0.012, c.y - 0.012, c.z), (c.x + x + 0.012, c.y + 0.012, c.z + 0.16), STEEL)
    p.turned(c + Vector((0.0, 0.0, 0.2)), 'Y', [(-0.05, 0.035), (0.08, 0.035)], DARK, sides=10)  # the hub
    plate = [Vector((x, -0.02, z)) for x, z in ((-0.24, 0.1), (0.24, 0.1), (0.28, 0.16), (0.28, 0.3), (0.24, 0.34), (-0.24, 0.34),
                                                (-0.28, 0.3), (-0.28, 0.16))]
    p.loft([[c + q + Vector((0.0, dy, 0.0)) for q in plate] for dy in (0.0, -0.03)], BRONZE)
    obj = p.finish(PREFIX + "spin_antenna")
    obj.data.transform(Matrix.Translation(-c))
    obj.location = c
    return obj


def _dish(p):
    c = DISH_BASE
    p.turned(c, 'Z', [(PLINTH_H, 0.5), (0.16, 0.48), (0.18, 0.44), (0.18, 0.2)], STEEL, sides=32)  # its plinth
    p.box((c.x - 0.22, c.y - 0.22, 0.18), (c.x + 0.22, c.y + 0.22, 0.6), DARK)  # the box frame
    p.turned(c, 'Z', [(0.6, 0.14), (0.75, 0.1)], STEEL, sides=12)  # the mount
    # The dish: outward along its axis, over the rim, back inside.
    rings = []
    steps = 7
    for i in range(1, steps + 1):
        t = i / steps
        rings.append(_ring(DISH_C + DISH_AXIS * (DISH_DEPTH * t * t - 0.03), DISH_AXIS, DISH_R * t, 36))
    for i in range(steps, 0, -1):
        t = i / steps
        rings.append(_ring(DISH_C + DISH_AXIS * (DISH_DEPTH * t * t + 0.005), DISH_AXIS, DISH_R * t * 0.97, 36))
    p.loft(rings, DISH)
    p.bar(c + Vector((0.0, 0.0, 0.72)), DISH_C - DISH_AXIS * 0.02, 0.05, STEEL)
    rim = DISH_C + DISH_AXIS * DISH_DEPTH
    feed = DISH_C + DISH_AXIS * 0.5
    for q in _ring(rim, DISH_AXIS, DISH_R * 0.9, 3, math.pi / 2):
        p.bar(q, feed, 0.012, DARK)
    p.sphere(feed, 'Z', 0.05, PALE, sides=12, rings=6)


def _team(p):
    p.turned(TOWER, 'Z', [(0.24, 0.305), (0.3, 0.305)], TEAM, sides=24)
    p.turned(TOWER, 'Z', [(TOWER_TOP - 0.12, 0.17), (TOWER_TOP - 0.06, 0.22), (TOWER_TOP, 0.22), (TOWER_TOP, 0.12)], TEAM, sides=24)  # the bowl
    p.turned(DISH_BASE, 'Z', [(0.1, 0.49), (0.14, 0.485), (0.14, 0.44)], TEAM, sides=32)


def _material():
    """Concrete, grey steel, a pale column, dark steel, a cream dish in a grid, bronze, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.15)
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
    grid = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 8.0)), -20.0, 1.0)),
                  g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 8.0)), -20.0, 1.0)))

    concrete = g.mix(grain, lin((0.50, 0.48, 0.44)), lin((0.58, 0.56, 0.52)))
    steel = g.mix(grain, lin((0.56, 0.57, 0.60)), lin((0.64, 0.65, 0.68)))
    pale = g.mix(grain, lin((0.76, 0.78, 0.86)), lin((0.84, 0.86, 0.92)))
    dark = g.mix(grain, lin((0.10, 0.10, 0.12)), lin((0.16, 0.16, 0.18)))
    dish = g.mix(grid, g.mix(grain, lin((0.78, 0.72, 0.56)), lin((0.86, 0.80, 0.64))), lin((0.30, 0.28, 0.24)))
    bronze = g.mix(grain, lin((0.56, 0.46, 0.30)), lin((0.64, 0.54, 0.36)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (steel, pale, dark, dish, bronze, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.45, 0.3, 0.5, 0.55, 0.5, 0.4))
    metallic = weighted((0.0, 0.6, 0.5, 0.6, 0.3, 0.6, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(80.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', grid, mask[4]), 0.6)), 0.02, 0.004)
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


def _pack(objs):
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')


def build():
    _clear()
    body = Parts()
    _tower(body)
    _dish(body)
    body = body.finish(PREFIX + "body")
    antenna = _antenna()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-0.95, -0.85, 0.0), (0.95, 0.85, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, antenna, team, base]
    _drop_bottoms([body, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    _pack(meshes)
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
