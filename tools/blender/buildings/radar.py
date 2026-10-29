"""Radar tower: the Union's radar (docs/ra2_buildings/soviets/radar_tower).

Footprint 2x2 cells (game/data/factions/union/buildings.yaml), origin at the centre of the footprint on the ground; the front
looks south (-Y in Blender, +Z in Godot). The composition follows the owner's references; the parts are our own:
    pedestal  a square brick pedestal with its top edges cut, a studded ramp on its front, four red feet out of its corners
              with steel shoes, a ring striped red and black on its top
    mast      a dark mast with collars out of the ring
    dishes    over the mast a large parabolic dish with a red feed on three struts and ribs behind, and a small dish on an arm
              lower down; they turn together about the mast
No emblems, no text. The feet take the owner's colour, where the references have the red. Height 2.1 to the rim of the large
dish. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the pedestal, the ramp, the shoes, the ring and the mast
    spin_dish       the top of the mast with both dishes; its origin is on the mast's axis, and the game turns it about its Y
                    while the owner has power (PlayerBuildings)
    team_mask       the four feet: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.radar as ra
    print(ra.run())
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "radar"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, BRICK, TAN, STEEL, DISH, RED, STRIPES, TEAM = (_part(i) for i in range(8))
PARTS = 8

PLINTH_H = 0.06
PED_HALF = 0.6
PED_TOP = 0.48
RING_TOP = 0.6
PIVOT = Vector((0.0, 0.0, 1.05))  # where the turning part sits on the mast
DISH_CENTRE = Vector((0.0, -0.1, 1.58))  # the large dish's apex
DISH_AXIS = Vector((0.0, -1.0, 0.75)).normalized()  # where it looks: forward and up
DISH_R = 0.5
DISH_DEPTH = 0.2


def _ring(centre, d, radius, sides, phase=0.0):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius
            for a in (phase + 2 * math.pi * i / sides for i in range(sides))]


def _dish(p, apex, axis, radius, depth, part, sides=32, steps=6):
    """A parabolic dish, its apex at apex, opening along axis: out along the outside, over the rim, back along the inside."""
    rings = []
    for i in range(1, steps + 1):  # the outside, from near the apex to the rim
        t = i / steps
        rings.append(_ring(apex + axis * (depth * t * t - 0.02), axis, radius * t, sides))
    for i in range(steps, 0, -1):  # the inside, back from the rim
        t = i / steps
        rings.append(_ring(apex + axis * (depth * t * t + 0.01), axis, radius * t * 0.97, sides))
    p.loft(rings, part)


# --- body -------------------------------------------------------------------------

def _pedestal(p):
    h = PED_HALF
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    p.loft([[Vector((sx * r, sy * r, z)) for sx, sy in corners] for z, r in ((PLINTH_H, h), (PED_TOP - 0.06, h), (PED_TOP, h - 0.06))],
           BRICK)
    # The studded ramp on the front, its foot on the ground.
    p.profile_x([(-h, PLINTH_H), (-h - 0.28, PLINTH_H), (-h - 0.28, PLINTH_H + 0.03), (-h, PED_TOP - 0.08)], -0.48, -0.05, TAN)
    # Steel shoes at the ends of the feet.
    for sx, sy in corners:
        d = Vector((sx, sy, 0.0)).normalized()
        foot = d * 0.98
        p.box((foot.x - 0.09, foot.y - 0.09, PLINTH_H), (foot.x + 0.09, foot.y + 0.09, PLINTH_H + 0.07), STEEL)
    # The striped ring on the top, and the mast out of it with its collars.
    p.turned((0.0, 0.0, 0.0), 'Z', [(PED_TOP, 0.42), (RING_TOP, 0.42), (RING_TOP, 0.3)], STRIPES, sides=32)
    p.turned((0.0, 0.0, 0.0), 'Z', [(RING_TOP, 0.2), (RING_TOP + 0.06, 0.2), (RING_TOP + 0.08, 0.1), (PIVOT.z, 0.09)], STEEL, sides=16)
    for z in (0.74, 0.9):
        p.turned((0.0, 0.0, 0.0), 'Z', [(z - 0.02, 0.12), (z + 0.02, 0.12)], STEEL, sides=16)


def _dishes():
    """The turning part: the top of the mast, the large dish with its feed and ribs, the small dish on its arm. Its object's
    origin is the pivot on the mast's axis."""
    p = Parts()
    c = PIVOT
    p.turned(c, 'Z', [(0.0, 0.1), (0.06, 0.1), (0.08, 0.07), (0.5, 0.06)], STEEL, sides=16)  # the top of the mast
    p.bar(c + Vector((0.0, 0.0, 0.45)), DISH_CENTRE - DISH_AXIS * 0.03, 0.05, STEEL)  # the neck to the dish
    _dish(p, DISH_CENTRE, DISH_AXIS, DISH_R, DISH_DEPTH, DISH)
    rim = DISH_CENTRE + DISH_AXIS * DISH_DEPTH
    feed = DISH_CENTRE + DISH_AXIS * 0.42
    for q in _ring(rim, DISH_AXIS, DISH_R * 0.92, 3, math.pi / 2):  # three struts to the feed
        p.bar(q, feed, 0.012, STEEL)
    p.sphere(feed, 'Z', 0.06, RED, sides=12, rings=6)
    for q in _ring(DISH_CENTRE - DISH_AXIS * 0.02, DISH_AXIS, 0.18, 4, math.pi / 4):  # ribs on the back
        p.bar(DISH_CENTRE - DISH_AXIS * 0.04, q + (q - DISH_CENTRE) * 1.4, 0.02, STEEL)
    # The small dish on an arm to the right, lower down.
    arm = c + Vector((0.0, 0.0, 0.15))
    small = arm + Vector((0.3, -0.06, 0.04))
    p.bar(arm, small, 0.025, STEEL)
    small_axis = Vector((0.6, -1.0, 0.25)).normalized()
    _dish(p, small, small_axis, 0.17, 0.07, DISH, sides=20, steps=4)
    p.sphere(small + small_axis * 0.13, 'Z', 0.025, RED, sides=8, rings=4)
    dishes = p.finish(PREFIX + "spin_dish")
    dishes.data.transform(Matrix.Translation(-c))
    dishes.location = c
    return dishes


def _team(p):
    """The four red feet out of the corners, sloping down to the shoes."""
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        d = Vector((sx, sy, 0.0)).normalized()
        p.bar(d * 0.62 + Vector((0.0, 0.0, 0.34)), d * 0.98 + Vector((0.0, 0.0, PLINTH_H + 0.1)), 0.1, TEAM)


# --- material -----------------------------------------------------------------------

def _material():
    """Grey concrete, tan brick in courses, a studded tan ramp, dark steel, a light grey dish, red, red and black stripes,
    a light team zone."""
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
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 16.0)), -10.0, 1.0))
    # Studs on the ramp: a dot every 0.08 across it and along its slope.
    sx = g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', x, 12.5)), 0.5))
    sz = g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', y, z), 9.0)), 0.5))
    studs = g.clamp01(g.math('MULTIPLY_ADD', g.math('ADD', g.math('MULTIPLY', sx, sx), g.math('MULTIPLY', sz, sz)), -40.0, 2.5))
    # The ring's stripes: twelve red and twelve black round it.
    turn = g.math('MULTIPLY', g.math('ARCTAN2', y, x), 12.0 / math.pi)
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', turn), 40.0, -20.0))

    concrete = g.mix(grain, lin((0.42, 0.42, 0.40)), lin((0.50, 0.50, 0.48)))
    brick = g.mix(courses, g.mix(grain, lin((0.62, 0.46, 0.34)), lin((0.70, 0.54, 0.40))), lin((0.44, 0.34, 0.28)))
    tan = g.mix(studs, g.mix(grain, lin((0.80, 0.66, 0.50)), lin((0.86, 0.72, 0.56))), lin((0.50, 0.40, 0.30)))
    steel = g.mix(grain, lin((0.16, 0.16, 0.17)), lin((0.23, 0.23, 0.24)))
    dish = g.mix(grain, lin((0.74, 0.75, 0.76)), lin((0.82, 0.83, 0.84)))
    red = g.mix(grain, lin((0.74, 0.10, 0.08)), lin((0.84, 0.14, 0.11)))
    stripe = g.mix(stripes, red, lin((0.08, 0.08, 0.08)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (brick, tan, steel, dish, red, stripe, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.85, 0.7, 0.45, 0.35, 0.4, 0.5, 0.5))
    metallic = weighted((0.0, 0.0, 0.2, 0.7, 0.6, 0.2, 0.3, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('ADD', g.math('MULTIPLY', courses, mask[1]),
                                                                                   g.math('MULTIPLY', studs, mask[2])), 0.8)),
                    0.04, 0.006)
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


def build():
    _clear()
    body = Parts()
    _pedestal(body)
    body = body.finish(PREFIX + "body")
    dishes = _dishes()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-1.0, -1.0, 0.0), (1.0, 1.0, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, dishes, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the dishes and the mast smooth, the edges sharp
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
