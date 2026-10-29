"""Naval shipyard: the Union's shipyard on deep water (docs/ra2_buildings/soviets/naval_shipyard).

Footprint 4x4 cells (game/data/factions/union/buildings_advanced.yaml), origin at the centre of the footprint on the water: the
water is the ground's surface there (TerrainBuilder), so the platform stands on legs from z 0 and has no concrete plinth. The
composition follows the owner's references; the parts are our own:
    platform  a square steel deck on legs over the water, a red band round its edge, pipes bending down from it into the water
    blocks    three brick blocks on the deck with slanted radiators on their tops, steps up to them
    plant     a yellow tank lying on the deck, a green box, crates
    crane     a tapered tower at the back-left corner with a turning crane on it: a yellow housing, a counter-jib, a lattice jib
              out to the east, a cable and a red ball with a hook; it swings left and right and lets the hook down and up while
              the shipyard builds or repairs a ship
No emblems, no text. The red band and the radiators take the owner's colour, where the references have the red. Height 2.3 to
the top of the crane's housing. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the deck, its legs, the pipes, the blocks, the steps, the plant and the crane's tower
    crane           the housing and the jibs: turns about its Y at its origin, on the tower's axis
    cable, hook     under crane: the cable and the hook, their origins at the jib's end; the cable hangs down along -Y
    team_mask       the band round the deck and the radiators: the game paints them in the owner's colour

In Blender, after tools/blender is on sys.path:
    import buildings.naval_shipyard as ns
    print(ns.run())
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "naval_shipyard"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


STEEL, BRICK, DARK, YELLOW, RED, GREEN, CRATE, TEAM = (_part(i) for i in range(8))
PARTS = 8

DECK_HALF = 1.85
DECK_Z = (0.28, 0.4)
BLOCKS = (Vector((-1.15, -1.05, 0.0)), Vector((0.1, -1.15, 0.0)), Vector((1.2, 0.25, 0.0)))
BLOCK_HALF = 0.32
BLOCK_TOP = 1.05
TOWER = Vector((-1.2, 1.15, 0.0))
TOWER_TOP = 1.6
PIVOT = TOWER + Vector((0.0, 0.0, TOWER_TOP))
JIB = 2.75  # the jib's reach from the tower's axis, out to the east
JIB_Z = 0.4  # the jib's height over the pivot
HOOK_Z = 0.85  # where the hook hangs at rest


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


def _bezier(a, c, b, n=9):
    a, c, b = Vector(a), Vector(c), Vector(b)
    return [a * (1 - t) ** 2 + c * 2 * t * (1 - t) + b * t * t for t in (i / (n - 1) for i in range(n))]


# --- body -------------------------------------------------------------------------

def _platform(p):
    h = DECK_HALF
    p.box((-h, -h, DECK_Z[0]), (h, h, DECK_Z[1]), STEEL)
    for sx in (-1, 1):  # the legs, into the water
        for sy in (-1, 1):
            for k in (0.0, 0.5):
                x, y = sx * (h - 0.2 - k * 1.6), sy * (h - 0.2)
                p.box((x - 0.12, y - 0.12, -0.05), (x + 0.12, y + 0.12, DECK_Z[0]), DARK)
    for x, y, out in ((-1.6, -h, Vector((0, -1, 0))), (0.95, -h, Vector((0, -1, 0))), (h, -1.2, Vector((1, 0, 0)))):
        a = Vector((x, y, DECK_Z[1] + 0.12)) - out * 0.25  # a pipe out of the deck, over its edge, down into the water
        _pipe(p, _bezier(a, a + out * 0.32, a + out * 0.22 + Vector((0.0, 0.0, -0.6))), 0.07, DARK)


def _blocks(p):
    for c in BLOCKS:
        b = BLOCK_HALF
        p.box((c.x - b, c.y - b, DECK_Z[1]), (c.x + b, c.y + b, BLOCK_TOP), BRICK)
        p.box((c.x - b - 0.03, c.y - b - 0.03, BLOCK_TOP - 0.06), (c.x + b + 0.03, c.y + b + 0.03, BLOCK_TOP), STEEL)
    for i in range(3):  # steps up to the front blocks
        y = BLOCKS[0].y - BLOCK_HALF - 0.08 * (i + 1)
        p.box((BLOCKS[0].x + 0.36, y, DECK_Z[1]), (BLOCKS[0].x + 0.6, y + 0.08, DECK_Z[1] + 0.2 - 0.06 * i), STEEL)


def _plant(p):
    p.turned((0.25, 0.1, 0.62), 'X', [(-0.36, 0.17), (-0.32, 0.22), (0.32, 0.22), (0.36, 0.17)], YELLOW, sides=16)
    p.box((0.55, 0.55, DECK_Z[1]), (0.85, 0.8, DECK_Z[1] + 0.22), GREEN)
    for x, y, s in ((-0.45, 0.35, 0.18), (-0.25, 0.4, 0.15), (-0.35, 0.18, 0.14), (0.8, -0.55, 0.16)):
        p.box((x - s / 2, y - s / 2, DECK_Z[1]), (x + s / 2, y + s / 2, DECK_Z[1] + s), CRATE)


def _tower(p):
    c = TOWER
    p.box((c.x - 0.38, c.y - 0.38, DECK_Z[1]), (c.x + 0.38, c.y + 0.38, 0.7), BRICK)  # its brick base
    p.loft([[Vector((c.x + sx * r, c.y + sy * r, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
            for z, r in ((0.7, 0.26), (TOWER_TOP - 0.05, 0.13))], DARK)  # the tapered mast
    p.turned(c, 'Z', [(TOWER_TOP - 0.06, 0.2), (TOWER_TOP, 0.2)], STEEL, sides=16)
    p.turned(c, 'Z', [(0.95, 0.24), (1.08, 0.21)], RED, sides=4)  # a red band on the mast


def _crane_nodes():
    """The crane, the cable and the hook; built along +X (east) from the pivot, at rest so."""
    c = PIVOT
    crane = Parts()
    crane.box((c.x - 0.25, c.y - 0.2, 0.02 + c.z), (c.x + 0.2, c.y + 0.2, c.z + 0.5), YELLOW)  # the housing
    crane.turned(c + Vector((-0.05, 0.0, 0.5)), 'Y', [(-0.18, 0.12), (0.18, 0.12)], YELLOW, sides=14)  # its rounded top
    crane.box((c.x - 0.75, c.y - 0.1, c.z + 0.28), (c.x - 0.25, c.y + 0.1, c.z + 0.38), STEEL)  # the counter-jib
    crane.box((c.x - 0.78, c.y - 0.16, c.z + 0.12), (c.x - 0.55, c.y + 0.16, c.z + 0.38), DARK)  # its weight
    root = c + Vector((0.18, 0.0, JIB_Z))
    tip = c + Vector((JIB, 0.0, JIB_Z - 0.15))
    chords = ((-0.09, -0.07), (0.09, -0.07), (0.0, 0.1))
    for y, z in chords:
        crane.bar(root + Vector((0.0, y, z)), tip + Vector((-0.05, y * 0.5, z * 0.5)), 0.02, YELLOW)
    n = 10
    for i in range(n):
        t0, t1 = i / n, (i + 1) / n
        a = [root.lerp(tip + Vector((-0.05, 0, 0)), t0) + Vector((0.0, y * (1 - 0.5 * t0), z * (1 - 0.5 * t0))) for y, z in chords]
        b = [root.lerp(tip + Vector((-0.05, 0, 0)), t1) + Vector((0.0, y * (1 - 0.5 * t1), z * (1 - 0.5 * t1))) for y, z in chords]
        crane.bar(a[0], a[1], 0.01, YELLOW)
        crane.bar(a[0], b[2], 0.01, YELLOW)
        crane.bar(a[1], b[2], 0.01, YELLOW)
    top = c + Vector((-0.05, 0.0, 0.85))
    crane.bar(c + Vector((-0.05, 0.0, 0.5)), top, 0.025, STEEL)  # the mast for the stays
    crane.bar(top, tip + Vector((0.0, 0.0, 0.05)), 0.007, DARK)  # the stays
    crane.bar(top, c + Vector((-0.75, 0.0, 0.38)), 0.007, DARK)
    crane.turned(tip, 'Y', [(-0.06, 0.07), (0.06, 0.07)], DARK, sides=14)  # the sheave
    crane = crane.finish(PREFIX + "crane")

    end = tip - Vector((0.0, 0.0, 0.07))
    drop = end.z - (HOOK_Z + 0.2)
    cable = Parts()
    cable.turned((0.0, 0.0, 0.0), 'Z', [(-drop, 0.01), (0.0, 0.01)], DARK, sides=6)
    cable = cable.finish(PREFIX + "cable")
    hook = Parts()
    hook.sphere((0.0, 0.0, -drop - 0.1), 'Z', 0.1, RED, sides=14, rings=7)  # the red ball
    hook.turned((0.0, 0.0, 0.0), 'Z', [(-drop - 0.26, 0.015), (-drop - 0.19, 0.015)], DARK, sides=6)
    hook.bar(Vector((0, 0, -drop - 0.26)), Vector((0.06, 0, -drop - 0.31)), 0.015, DARK)  # the hook's curve
    hook.bar(Vector((0.06, 0, -drop - 0.31)), Vector((0.08, 0, -drop - 0.25)), 0.015, DARK)
    hook = hook.finish(PREFIX + "hook")

    crane.data.transform(Matrix.Translation(-c))
    crane.location = c
    for node in (cable, hook):
        node.parent = crane
        node.location = end - c
    bpy.context.view_layer.update()
    return crane, cable, hook


def _team(p):
    h = DECK_HALF + 0.02
    w = 0.08
    z0, z1 = DECK_Z[0] - 0.04, DECK_Z[0] + 0.06  # the red band round the edge of the deck
    for lo, hi in (((-h, -h), (h, -h + w)), ((-h, h - w), (h, h)), ((-h, -h + w), (-h + w, h - w)), ((h - w, -h + w), (h, h - w))):
        p.box((lo[0], lo[1], z0), (hi[0], hi[1], z1), TEAM)
    for c in BLOCKS:  # the slanted radiators on the blocks
        b = BLOCK_HALF - 0.02
        p.loft([[Vector((x, y, z)) for y, z in ((c.y - b, BLOCK_TOP), (c.y + b, BLOCK_TOP), (c.y + b, BLOCK_TOP + 0.35),
                                                 (c.y + b - 0.15, BLOCK_TOP + 0.35))] for x in (c.x - b, c.x + b)], TEAM)


# --- material -----------------------------------------------------------------------

def _material():
    """Dark steel plates, brick in courses, dark, yellow, red, green, crates, a light team zone."""
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
    rust = g.noise(5.0, 5.0)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    plates = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 2.0)), -40.0, 1.0)),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 2.0)), -40.0, 1.0)))
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 16.0)), -10.0, 1.0))

    steel = g.mix(plates, g.mix(rust, lin((0.34, 0.33, 0.32)), lin((0.44, 0.38, 0.32))), lin((0.18, 0.18, 0.18)))
    brick = g.mix(courses, g.mix(grain, lin((0.60, 0.42, 0.34)), lin((0.68, 0.50, 0.40))), lin((0.44, 0.36, 0.32)))
    dark = g.mix(grain, lin((0.12, 0.12, 0.13)), lin((0.18, 0.18, 0.19)))
    yellow = g.mix(rust, lin((0.86, 0.62, 0.16)), lin((0.78, 0.50, 0.18)))
    red = g.mix(grain, lin((0.72, 0.10, 0.08)), lin((0.82, 0.14, 0.11)))
    green = g.mix(grain, lin((0.24, 0.48, 0.42)), lin((0.30, 0.56, 0.48)))
    crate = g.mix(grain, lin((0.52, 0.36, 0.24)), lin((0.60, 0.44, 0.30)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = steel
    for m, col in zip(mask[1:], (brick, dark, yellow, red, green, crate, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.6, 0.85, 0.6, 0.45, 0.45, 0.5, 0.8, 0.45))
    metallic = weighted((0.6, 0.0, 0.5, 0.3, 0.2, 0.2, 0.0, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('ADD', g.math('MULTIPLY', plates, mask[0]),
                                                                                   g.math('MULTIPLY', courses, mask[1])), 0.7)),
                    0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _pack(objs):
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')


def build():
    """The meshes (each parent before its children) and the material."""
    _clear()
    body = Parts()
    _platform(body)
    _blocks(body)
    _plant(body)
    _tower(body)
    body = body.finish(PREFIX + "body")
    crane, cable, hook = _crane_nodes()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    meshes = [body, crane, cable, hook, team]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    _pack(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, mat, graph = build()
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"
              for obj in meshes]
    report.append(f"total triangles {sum(rc.tri_count(obj) for obj in meshes)}")
    base = os.path.join(rc.SRC_ROOT, "building", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    top = [obj for obj in meshes if obj.parent is None]
    rc.arrange(top, ID)  # its place in the file: the row of the players' buildings
    coll = bpy.data.collections[ID]
    for obj in meshes:
        if obj not in top and coll not in obj.users_collection:
            for owner in list(obj.users_collection):
                owner.objects.unlink(obj)
            coll.objects.link(obj)
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(meshes, [o.name[len(PREFIX):] for o in meshes]):
        rc.save_blend(meshes, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(meshes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
