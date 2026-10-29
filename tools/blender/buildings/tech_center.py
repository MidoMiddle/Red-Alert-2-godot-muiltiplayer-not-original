"""Battle lab: the Union's high-tech building (docs/ra2_buildings/soviets/battle_lab), tech_center in the data.

Footprint 3x3 cells (game/data/factions/union/buildings.yaml), origin at the centre of the footprint on the ground; the camera
sees the south (-Y in Blender, +Z in Godot) and east (+X) faces. The composition follows the owner's references; the parts are
our own:
    block     a square brick block with rows of windows, a light cornice, a band round it under the cornice
    turrets   a square turret at each corner and a pinnacle in the middle of each side, each with a small golden spire
    tiers     three round tiers over the block, each smaller than the one under it: a brick drum with windows, a second drum,
              a colonnade; a band round the top of each; on the colonnade a golden onion dome with its spire
    porches   two porches on the south and east faces: steps under a sloping red roof
No emblems, no text. The bands round the tiers and the block take the owner's colour, where the references have the red.
Height 2.7 to the spire. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the block, the turrets, the pinnacles, the tiers, the dome and the porches
    team_mask       the bands round the tiers and round the block: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.tech_center as tc
    print(tc.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "tech_center"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, BRICK, STONE, GOLD, RED, TEAM = (_part(i) for i in range(6))
PARTS = 6

PLINTH_H = 0.06
BLOCK_HALF = 1.1
BLOCK_TOP = 0.74
CORNICE_TOP = 0.82
# The tiers: (bottom, top, radius) of each drum; a band round the top of each.
TIERS = ((CORNICE_TOP, 1.14, 0.92), (1.2, 1.48, 0.66), (1.54, 1.8, 0.44))
DOME_Z = 1.86
# The stone rims of the tiers, from z to z: the material paints the tiers' body there as stone.
RIMS = ((1.14, 1.2), (1.48, 1.54), (1.8, 1.86))
SPIRE_TOP = 2.7


def _spire(p, x, y, z, h, r):
    """A small golden spire: a bulb and a needle."""
    p.turned((x, y, 0.0), 'Z', [(z, r * 0.6), (z + h * 0.2, r), (z + h * 0.45, r * 0.7), (z + h * 0.55, r * 0.3), (z + h, 0.008)],
             GOLD, sides=10)


# --- body -------------------------------------------------------------------------

def _block(p):
    h = BLOCK_HALF
    p.box((-h, -h, PLINTH_H), (h, h, BLOCK_TOP), BRICK)
    p.box((-h - 0.05, -h - 0.05, BLOCK_TOP), (h + 0.05, h + 0.05, CORNICE_TOP), STONE)  # the cornice
    p.box((-h - 0.03, -h - 0.03, PLINTH_H), (h + 0.03, h + 0.03, PLINTH_H + 0.08), STONE)  # the stone foot
    for sx in (-1, 1):  # the corner turrets
        for sy in (-1, 1):
            cx, cy = sx * (h - 0.06), sy * (h - 0.06)
            p.box((cx - 0.17, cy - 0.17, PLINTH_H), (cx + 0.17, cy + 0.17, 1.02), BRICK)
            p.box((cx - 0.2, cy - 0.2, 1.02), (cx + 0.2, cy + 0.2, 1.07), STONE)
            _spire(p, cx, cy, 1.07, 0.32, 0.06)
    for x, y in ((0.0, -h), (0.0, h), (-h, 0.0), (h, 0.0)):  # a pinnacle in the middle of each side
        p.box((x - 0.08, y - 0.08, CORNICE_TOP), (x + 0.08, y + 0.08, 0.98), BRICK)
        _spire(p, x, y, 0.98, 0.22, 0.045)


def _tiers(p):
    # The tiers in one body of revolution, with no hidden faces between them: each drum, its rim, a step in to the next one;
    # the material paints the rims as stone (RIMS).
    stations = []
    for bottom, top, r in TIERS[:2]:
        stations += [(bottom, r), (top, r), (top, r + 0.02), (top + 0.06, r + 0.02)]
    bottom, top, r = TIERS[2]  # the core of the colonnade, its roof ring
    stations += [(bottom, r - 0.12), (top, r - 0.12), (top, r + 0.02), (DOME_Z, r + 0.02), (DOME_Z, 0.1)]
    p.turned((0.0, 0.0, 0.0), 'Z', stations, BRICK, sides=32)
    for i in range(10):  # the columns round the core
        a = 2 * math.pi * i / 10
        p.turned((r * 0.86 * math.cos(a), r * 0.86 * math.sin(a), 0.0), 'Z', [(bottom, 0.035), (top, 0.03)], STONE, sides=8)
    # The onion dome and its spire.
    p.turned((0.0, 0.0, 0.0), 'Z', [(DOME_Z, 0.26), (DOME_Z + 0.1, 0.36), (DOME_Z + 0.24, 0.4), (DOME_Z + 0.38, 0.33),
                                     (DOME_Z + 0.5, 0.18), (DOME_Z + 0.58, 0.07), (DOME_Z + 0.62, 0.04), (SPIRE_TOP, 0.01)],
             GOLD, sides=24)
    p.sphere((0.0, 0.0, DOME_Z + 0.7), 'Z', 0.035, GOLD, sides=10, rings=5)


def _porch(p, out, along, offset):
    """A porch on a face: steps up to the wall under a sloping red roof on two posts."""
    h = BLOCK_HALF
    wall = out * h + along * offset
    for i in range(3):  # the steps
        d0, d1 = 0.34 - i * 0.1, 0.0
        lo = wall + out * d1 - along * 0.26
        hi = wall + out * d0 + along * 0.26
        p.box((min(lo.x, hi.x), min(lo.y, hi.y), PLINTH_H), (max(lo.x, hi.x), max(lo.y, hi.y), PLINTH_H + 0.06 * (i + 1)), STONE)
    for s in (-1, 1):  # the posts
        q = wall + out * 0.28 + along * s * 0.24
        p.box((q.x - 0.035, q.y - 0.035, PLINTH_H + 0.06), (q.x + 0.035, q.y + 0.035, 0.46), BRICK)
    # The roof: a slab sloping from the wall down to the posts.
    corners = [wall + along * s * 0.32 + Vector((0.0, 0.0, 0.62)) for s in (-1, 1)]
    corners += [wall + out * 0.36 + along * s * 0.32 + Vector((0.0, 0.0, 0.44)) for s in (1, -1)]
    p.loft([[q + Vector((0.0, 0.0, dz)) for q in corners] for dz in (0.0, 0.05)], RED)


def _team(p):
    h = BLOCK_HALF
    p.box((-h - 0.02, -h - 0.02, BLOCK_TOP - 0.1), (h + 0.02, h + 0.02, BLOCK_TOP - 0.05), TEAM)  # the band round the block
    for sx in (-1, 1):  # and round the turrets
        for sy in (-1, 1):
            cx, cy = sx * (h - 0.06), sy * (h - 0.06)
            p.box((cx - 0.19, cy - 0.19, 0.86), (cx + 0.19, cy + 0.19, 0.92), TEAM)
    for bottom, top, r in TIERS:  # the bands round the tiers, under their rims
        p.turned((0.0, 0.0, 0.0), 'Z', [(top - 0.08, r + 0.04), (top, r + 0.06), (top, r - 0.02)], TEAM, sides=32)


# --- material -----------------------------------------------------------------------

def _material():
    """Brown brick with rows of dark windows, light stone, gold, red, a light team zone."""
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
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 20.0)), -10.0, 1.0))
    # Windows: tall narrow ones every 0.2 along the walls (x + y runs along every face), in two rows on the block and one on
    # each drum.
    fu = g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 5.0))
    win_u = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', fu, 30.0, -10.5)), g.clamp01(g.math('MULTIPLY_ADD', fu, -30.0, 19.5)))
    fz = g.math('FRACT', g.math('MULTIPLY', z, 3.6))
    win_z = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', fz, 30.0, -6.0)), g.clamp01(g.math('MULTIPLY_ADD', fz, -30.0, 24.0)))
    windows = g.math('MULTIPLY', g.math('MULTIPLY', win_u, win_z), mask[1])
    rims = None
    for z0, z1 in RIMS:
        band = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 200.0, -z0 * 200.0 + 0.5)),
                      g.clamp01(g.math('MULTIPLY_ADD', z, -200.0, z1 * 200.0 + 0.5)))
        rims = band if rims is None else g.math('MAXIMUM', rims, band)
    rims = g.math('MULTIPLY', rims, mask[1])

    concrete = g.mix(grain, lin((0.42, 0.42, 0.40)), lin((0.50, 0.50, 0.48)))
    brick = g.mix(courses, g.mix(grain, lin((0.50, 0.36, 0.28)), lin((0.58, 0.42, 0.33))), lin((0.36, 0.28, 0.24)))
    stone = g.mix(grain, lin((0.72, 0.68, 0.62)), lin((0.80, 0.76, 0.70)))
    gold = g.mix(grain, lin((0.80, 0.62, 0.24)), lin((0.90, 0.72, 0.32)))
    red = g.mix(grain, lin((0.70, 0.10, 0.08)), lin((0.80, 0.14, 0.11)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (brick, stone, gold, red, team)):
        albedo = g.mix(m, albedo, col)
    albedo = g.mix(windows, albedo, lin((0.10, 0.10, 0.12)))
    albedo = g.mix(rims, albedo, stone)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = g.math('MULTIPLY_ADD', windows, -0.6, weighted((0.85, 0.85, 0.7, 0.3, 0.5, 0.5)))
    metallic = weighted((0.0, 0.0, 0.0, 0.85, 0.1, 0.2))
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


def build():
    _clear()
    body = Parts()
    _block(body)
    _tiers(body)
    _porch(body, Vector((0.0, -1.0, 0.0)), Vector((1.0, 0.0, 0.0)), 0.5)  # on the south face
    _porch(body, Vector((1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0)), -0.5)  # on the east face
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-1.5, -1.5, 0.0), (1.5, 1.5, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the drums and the dome smooth, the edges sharp
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
