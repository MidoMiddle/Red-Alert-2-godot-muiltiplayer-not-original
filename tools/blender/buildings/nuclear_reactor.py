"""Nuclear reactor: the Union's late power plant, +2000 (docs/ra2_buildings/soviets/nuclear_reactor).

Footprint 4x4 cells (game/data/factions/union/buildings_advanced.yaml), origin at the centre of the footprint on the ground.
The camera looks from the south-east, so the towers stand at the north-west (back), south-west (left) and north-east (right) of
the core and the pump at its south-east (front), as in the owner's references; the parts are our own:
    towers    three brick cooling towers waisted in the middle, each on a round plinth with steel legs round its foot, two
              pipes and a ladder up its side, a dark mouth
    core      a brick drum in the middle with two bands and a dark dome
    pipes     two arched pipes from the top of the core over into each tower
    pump      a small drum at the front with panels on its sides, a pipe up into the core, two elbows down to the ground
No emblems, no text. The bands of the core and the panels of the pump take the owner's colour, where the references have the
red. Height 1.5 to the lips of the towers. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the towers, their plinths, legs, pipes and ladders, the core, the arched pipes and the pump
    team_mask       the bands of the core and the panels of the pump: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")
    smoke_1..3      empties at the mouths of the towers: where steam will rise (the game does not draw it yet)

In Blender, after tools/blender is on sys.path:
    import buildings.nuclear_reactor as nr
    print(nr.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "nuclear_reactor"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, BRICK, STEEL, DARK, RED, TEAM = (_part(i) for i in range(6))
PARTS = 6

PLINTH_H = 0.06
TOWERS = (Vector((-0.92, 0.92, 0.0)), Vector((-0.95, -0.95, 0.0)), Vector((0.95, 0.95, 0.0)))  # back, left, right
TOWER_FOOT = 0.16
TOWER_TOP = 1.5
# The tower's outline: height, radius; waisted in the middle, a lip at the top.
TOWER = ((TOWER_FOOT, 0.56), (0.34, 0.5), (0.7, 0.41), (1.0, 0.37), (1.25, 0.39), (TOWER_TOP - 0.04, 0.45), (TOWER_TOP, 0.47),
         (TOWER_TOP, 0.42), (TOWER_TOP - 0.15, 0.37))
CORE = Vector((0.12, -0.12, 0.0))
CORE_R = 0.46
CORE_TOP = 0.76
PUMP = Vector((0.92, -0.92, 0.0))


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _pipe(p, points, radius, part, sides=12):
    pts = [Vector(q) for q in points]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        rings.append(_ring(q, d, radius, sides))
    p.loft(rings, part)


def _bezier(a, b, c, d, n=14):
    a, b, c, d = Vector(a), Vector(b), Vector(c), Vector(d)
    return [a * (1 - t) ** 3 + b * 3 * t * (1 - t) ** 2 + c * 3 * t * t * (1 - t) + d * t ** 3 for t in (i / (n - 1) for i in range(n))]


def _radius_at(z):
    """The tower's outer radius at height z, from its outline."""
    outer = TOWER[:7]
    for (z0, r0), (z1, r1) in zip(outer, outer[1:]):
        if z0 <= z <= z1 and z1 > z0:
            return r0 + (r1 - r0) * (z - z0) / (z1 - z0)
    return outer[-1][1]


# --- body -------------------------------------------------------------------------

def _tower(p, c, toward_core):
    # Its round plinth and the tower in one body of revolution: no hidden faces between them. The mouth is dark from its depth.
    p.turned(c, 'Z', [(PLINTH_H, 0.74), (TOWER_FOOT, 0.74)] + list(TOWER), BRICK, sides=32)
    for i in range(16):  # the legs round its foot
        a = 2 * math.pi * (i + 0.5) / 16
        out = Vector((math.cos(a), math.sin(a), 0.0))
        p.bar(c + out * 0.66 + Vector((0.0, 0.0, TOWER_FOOT)), c + out * 0.5 + Vector((0.0, 0.0, 0.36)), 0.022, STEEL)
    # Two pipes and a ladder up the side away from the core.
    away = -toward_core
    side = Vector((-away.y, away.x, 0.0))
    heights = [TOWER_FOOT + 0.05, 0.5, 0.85, 1.15, TOWER_TOP - 0.02]
    for s in (-0.16, -0.1, 0.12):
        pts = [c + (away * math.cos(s) + side * math.sin(s)) * (_radius_at(z) + 0.03) + Vector((0.0, 0.0, z)) for z in heights]
        _pipe(p, pts, 0.018, STEEL, sides=6)
    rails = []
    for s in (0.24, 0.3):
        pts = [c + (away * math.cos(s) + side * math.sin(s)) * (_radius_at(z) + 0.02) + Vector((0.0, 0.0, z)) for z in heights]
        _pipe(p, pts, 0.01, RED, sides=4)
        rails.append(pts)
    for i in range(1, 12):  # its rungs
        z = TOWER_FOOT + 0.1 + i * 0.11
        a = [c + (away * math.cos(s) + side * math.sin(s)) * (_radius_at(z) + 0.02) + Vector((0.0, 0.0, z)) for s in (0.24, 0.3)]
        p.bar(a[0], a[1], 0.007, RED)


def _core(p):
    p.turned(CORE, 'Z', [(PLINTH_H, CORE_R + 0.06), (0.14, CORE_R + 0.06), (0.14, CORE_R), (CORE_TOP, CORE_R), (CORE_TOP, CORE_R - 0.06),
                         (CORE_TOP + 0.1, CORE_R - 0.12), (CORE_TOP + 0.16, 0.2), (CORE_TOP + 0.18, 0.05)], BRICK, sides=32)
    # The arched pipes, two to each tower: up out of the top of the core, over, and down into the tower's side.
    for t in TOWERS:
        to = (t - CORE).normalized()
        side = Vector((-to.y, to.x, 0.0))
        for s in (-0.11, 0.11):
            start = CORE + to * 0.22 + side * s + Vector((0.0, 0.0, CORE_TOP + 0.1))
            end_r = _radius_at(0.88)
            end = t - to * (end_r + 0.02) + side * s * 1.4 + Vector((0.0, 0.0, 0.88))
            _pipe(p, _bezier(start, start + Vector((0.0, 0.0, 0.7)), end - to * 0.35 + Vector((0.0, 0.0, 0.5)), end), 0.08, STEEL)
            p.loft([_ring(end - to * 0.02, to, 0.11, 12), _ring(end + to * 0.02, to, 0.11, 12)], STEEL)  # its flange


def _pump(p):
    c = PUMP
    p.turned(c, 'Z', [(PLINTH_H, 0.26), (0.12, 0.26), (0.12, 0.2), (0.46, 0.2), (0.48, 0.24), (0.52, 0.24), (0.52, 0.08), (0.58, 0.08)],
             STEEL, sides=20)
    for i in range(8):  # bolts on its flange
        a = 2 * math.pi * i / 8
        p.turned(c + Vector((0.22 * math.cos(a), 0.22 * math.sin(a), 0.0)), 'Z', [(0.52, 0.016), (0.55, 0.016)], DARK, sides=6)
    to_core = (CORE - c).normalized()
    _pipe(p, _bezier(c + Vector((0.0, 0.0, 0.56)), c + Vector((0.0, 0.0, 0.85)), CORE - to_core * (CORE_R - 0.05) + Vector((0.0, 0.0, 0.9)),
                     CORE - to_core * (CORE_R - 0.12) + Vector((0.0, 0.0, 0.62))), 0.05, STEEL)
    side = Vector((-to_core.y, to_core.x, 0.0))
    for s in (-1, 1):  # two elbows down to flanges on the ground
        a = c + side * s * 0.2 + Vector((0.0, 0.0, 0.3))
        b = c + side * s * 0.42 - to_core * 0.08 + Vector((0.0, 0.0, PLINTH_H + 0.06))
        _pipe(p, _bezier(a, a + side * s * 0.18, b + Vector((0.0, 0.0, 0.18)), b), 0.045, STEEL)
        p.turned(b, 'Z', [(-0.06, 0.08), (0.0, 0.08)], STEEL, sides=12)


def _team(p):
    for z0, z1 in ((0.44, 0.54), (0.64, CORE_TOP)):  # the bands of the core
        p.turned(CORE, 'Z', [(z0, CORE_R + 0.025), (z1, CORE_R + 0.025)], TEAM, sides=32)
    to_core = (CORE - PUMP).normalized()
    side = Vector((-to_core.y, to_core.x, 0.0))
    for s in (-1, 1):  # the panels on the sides of the pump
        a = math.atan2((side * s).y, (side * s).x)
        corners = [PUMP + Vector((0.205 * math.cos(a + da), 0.205 * math.sin(a + da), 0.0)) for da in (-0.45, 0.45)]
        quad = [corners[0] + Vector((0, 0, 0.16)), corners[1] + Vector((0, 0, 0.16)), corners[1] + Vector((0, 0, 0.44)), corners[0] + Vector((0, 0, 0.44))]
        out = side * s * 0.03
        p.loft([[q + out * k for q in quad] for k in (0.0, 1.0)], TEAM)


# --- material -----------------------------------------------------------------------

def _material():
    """Grey concrete, red-brown brick in courses, dark steel, the dark mouths, red, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    grain = g.noise(30.0, 4.0)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 22.0)), -10.0, 1.0))
    plinths = g.math('MULTIPLY', mask[1], g.clamp01(g.math('MULTIPLY_ADD', z, -80.0, TOWER_FOOT * 80.0 + 0.5)))  # under the towers

    concrete = g.mix(grain, lin((0.46, 0.46, 0.44)), lin((0.54, 0.54, 0.52)))
    brick = g.mix(courses, g.mix(grain, lin((0.62, 0.42, 0.34)), lin((0.70, 0.50, 0.40))), lin((0.44, 0.34, 0.30)))
    steel = g.mix(grain, lin((0.20, 0.20, 0.21)), lin((0.27, 0.27, 0.28)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.05)), lin((0.08, 0.08, 0.08)))
    red = g.mix(grain, lin((0.72, 0.10, 0.08)), lin((0.82, 0.14, 0.11)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (brick, steel, dark, red, team)):
        albedo = g.mix(m, albedo, col)
    albedo = g.mix(plinths, albedo, concrete)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.85, 0.45, 0.9, 0.5, 0.5))
    metallic = weighted((0.0, 0.0, 0.7, 0.0, 0.2, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', courses, mask[1]), 0.7)), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    """Removes what an earlier run left in the session, so names stay free."""
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _empty(name, point):
    node = bpy.data.objects.new(PREFIX + name, None)
    node.empty_display_type = 'PLAIN_AXES'
    node.empty_display_size = 0.12
    node.location = Vector(point)
    bpy.context.scene.collection.objects.link(node)
    return node


def _pack(objs):
    """Packs the UV islands of all meshes tighter: the many thin pipes and legs leave the smart projection's texture half empty."""
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')


def _drop_bottom(obj):
    """Deletes the faces of obj that look down at the ground: the underside of the plinth is never seen, and black in the bake."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    bmesh.ops.delete(bm, geom=[f for f in bm.faces if f.normal.z < -0.99 and f.calc_center_median().z < 1e-4], context='FACES')
    bm.to_mesh(obj.data)
    bm.free()


def build():
    _clear()
    body = Parts()
    for t in TOWERS:
        _tower(body, t, (CORE - t).normalized())
    _core(body)
    _pump(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-2.0, -2.0, 0.0), (2.0, 2.0, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the towers and the pipes smooth, the edges sharp
    uv_together(meshes)
    _pack(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    _drop_bottom(base)
    smoke = [_empty(f"smoke_{i + 1}", t + Vector((0.0, 0.0, TOWER_TOP + 0.02))) for i, t in enumerate(TOWERS)]
    return meshes, smoke, mat, graph


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
