"""Construction yard: the core of the Union's base (docs/ra2_buildings/soviets/construction_yard).

Footprint 4x4 cells (game/data/factions/union/buildings.yaml), origin at the centre of the footprint on the ground; the camera
sees the south (-Y in Blender, +Z in Godot) and east (+X) sides. The composition follows the owner's references; the parts are
our own:
    gatehouse a brick gatehouse in the west, its front a sloping brick frame round a roller gate, a steel roof with red bars, two
              slanted radiators on its back
    pad       a raised pad south of the gate with a striped frame, a pit in its middle under two hatch leaves, rails over the pit
              from the gate to the pad's end
    carriage  a low carriage on the rails, a crate on it
    crane     a jointed arm on a block in the east: up from the block, then over to its end above the pad, a hoist with a claw
    tower     a steel tower at the back with a golden onion dome and a spire
    plant     a yellow tank lying at the gatehouse's west side, a red drum, pipes
No emblems, no text. The arm of the crane, the radiators and the bars on the gatehouse take the owner's colour, where the
references have the red. Height 2.2 to the spire. Budget 20000 triangles, texture 2048.

While its owner builds the game runs the cycle of the Alliance's yard (alliance_construction_yard.py, PlayerBuildings): the gate
rolls up, the carriage drives out, the claw lifts the crate, the carriage drives back, the hatch opens, the claw lowers the crate
into the pit, the hatch and the gate shut. The nodes are the same:
    body, roller_gate, carriage, carriage_out, crate, hoist_cable, hoist_hook, hoist_grab, hoist_drop, hatch_a, hatch_b,
    team_mask, base

In Blender, after tools/blender is on sys.path:
    import buildings.construction_yard as cy
    print(cy.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "construction_yard"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


PAVING, BRICK, STEEL, YELLOW, DARK, HAZARD, GOLD, RED, CRATE, TEAM = (_part(i) for i in range(10))
PARTS = 10

PLINTH_H = 0.06
# The gatehouse and its gate.
GATE_X = -0.45
HOUSE = (-1.35, 0.45, -0.55, 0.8)  # x0, x1, y0 (the front), y1
HOUSE_TOP = 1.0
GATE_HALF, GATE_TOP = 0.42, 0.75
# The pad and its pit.
PAD = (GATE_X - 0.6, GATE_X + 0.6, -1.92, HOUSE[2])
PAD_TOP = 0.2
PIT = (GATE_X - 0.3, GATE_X + 0.3, -1.6, -1.0)
PIT_FLOOR = 0.045
RAIL_X = (GATE_X - 0.28, GATE_X + 0.28)
RAIL_TOP = 0.24
CARRIAGE_REST = Vector((GATE_X, -0.3, RAIL_TOP))
CARRIAGE_OUT = Vector((GATE_X, -1.3, RAIL_TOP))
CARRIAGE_TOP = RAIL_TOP + 0.1
CRATE_SIZE = 0.22
# The crane: its block, its elbow, the end of its arm over the pad.
CRANE_BASE = Vector((1.2, -1.05, 0.0))
CRANE_BLOCK_TOP = 0.55
ELBOW = Vector((0.75, -1.2, 1.6))
TIP = Vector((CARRIAGE_OUT.x, CARRIAGE_OUT.y, 1.38))
HOOK_REST = 0.95
HOOK_DROP = PIT_FLOOR - 0.01
TOWER = Vector((0.95, 1.05, 0.0))


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _beam(p, a, b, w, h, part):
    """A box beam from a to b, w wide across and h high."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    side = d.cross(Vector((0.0, 0.0, 1.0)))
    side = (side if side.length > 1e-6 else Vector((1.0, 0.0, 0.0))).normalized() * (w / 2)
    up = side.cross(d).normalized() * (h / 2)
    p.loft([[q + side * s + up * t for s, t in ((-1, -1), (1, -1), (1, 1), (-1, 1))] for q in (a, b)], part)


# --- body -------------------------------------------------------------------------

def _gatehouse(p):
    x0, x1, y0, y1 = HOUSE
    p.box((x0, y0 + 0.15, PLINTH_H), (x1, y1, HOUSE_TOP - 0.1), BRICK)
    p.box((x0 - 0.02, y0 + 0.13, HOUSE_TOP - 0.1), (x1 + 0.02, y1 + 0.02, HOUSE_TOP), STEEL)  # the steel roof
    # The sloping brick frame round the gate: two cheeks leaning back, a lintel over the opening.
    for xa, xb in ((GATE_X - GATE_HALF - 0.22, GATE_X - GATE_HALF), (GATE_X + GATE_HALF, GATE_X + GATE_HALF + 0.22)):
        p.loft([[Vector((x, y, z)) for y, z in ((y0, PLINTH_H), (y0 + 0.16, PLINTH_H), (y0 + 0.16, HOUSE_TOP + 0.12),
                                                 (y0 + 0.08, HOUSE_TOP + 0.12))] for x in (xa, xb)], BRICK)
    p.loft([[Vector((x, y, z)) for y, z in ((y0 + 0.02, GATE_TOP), (y0 + 0.16, GATE_TOP), (y0 + 0.16, HOUSE_TOP + 0.12),
                                             (y0 + 0.08, HOUSE_TOP + 0.12))] for x in (GATE_X - GATE_HALF, GATE_X + GATE_HALF)], BRICK)
    p.box((GATE_X - GATE_HALF, y0 + 0.14, PLINTH_H), (GATE_X + GATE_HALF, y0 + 0.15, GATE_TOP), DARK)  # the dark opening
    # The plant at the west side: a yellow tank lying along y, a red drum, pipes into the wall.
    p.turned((x0 - 0.28, -0.2, 0.26), 'Y', [(-0.38, 0.14), (-0.34, 0.19), (0.34, 0.19), (0.38, 0.14)], YELLOW, sides=16)
    p.turned((x0 - 0.3, 0.45, 0.0), 'Z', [(PLINTH_H, 0.15), (0.48, 0.15), (0.52, 0.11)], RED, sides=14)
    for y in (-0.35, -0.05):
        p.bar((x0 - 0.12, y, 0.32), (x0 + 0.01, y, 0.32), 0.035, STEEL)


def _pad(p):
    x0, x1, y0, y1 = PAD
    px0, px1, py0, py1 = PIT
    for lo, hi in (((x0, y0), (x1, py0)), ((x0, py1), (x1, y1)), ((x0, py0), (px0, py1)), ((px1, py0), (x1, py1))):
        p.box((lo[0], lo[1], PLINTH_H), (hi[0], hi[1], PAD_TOP), HAZARD)
    p.box((px0, py0, PLINTH_H), (px1, py1, PIT_FLOOR), DARK)
    for x in RAIL_X:
        p.box((x - 0.03, y0 + 0.02, PAD_TOP), (x + 0.03, HOUSE[2] + 0.4, RAIL_TOP), STEEL)


def _crane(p):
    c = CRANE_BASE
    p.box((c.x - 0.32, c.y - 0.32, PLINTH_H), (c.x + 0.32, c.y + 0.32, CRANE_BLOCK_TOP), STEEL)
    p.box((c.x - 0.34, c.y - 0.34, CRANE_BLOCK_TOP - 0.08), (c.x + 0.34, c.y + 0.34, CRANE_BLOCK_TOP - 0.02), YELLOW)
    p.turned(c, 'Z', [(CRANE_BLOCK_TOP, 0.22), (CRANE_BLOCK_TOP + 0.1, 0.22)], STEEL, sides=16)  # the turntable
    root = c + Vector((0.0, 0.0, CRANE_BLOCK_TOP + 0.1))
    _beam(p, root + (ELBOW - root).normalized() * 0.42, ELBOW + (root - ELBOW).normalized() * 0.3, 0.06, 0.06, STEEL)  # the ram
    p.turned(ELBOW, 'Y', [(-0.13, 0.11), (0.13, 0.11)], STEEL, sides=14)  # the elbow's pin
    p.turned(TIP, 'Y', [(-0.1, 0.08), (0.1, 0.08)], STEEL, sides=14)  # the drum at the end


def _tower(p):
    c = TOWER
    p.box((c.x - 0.38, c.y - 0.38, PLINTH_H), (c.x + 0.38, c.y + 0.38, 0.62), STEEL)
    p.box((c.x - 0.28, c.y - 0.28, 0.62), (c.x + 0.28, c.y + 0.28, 0.98), STEEL)
    p.turned(c, 'Z', [(0.98, 0.2), (1.25, 0.2), (1.29, 0.24)], STEEL, sides=16)
    for i in range(8):  # columns round the drum
        a = 2 * math.pi * i / 8
        p.turned(c + Vector((0.21 * math.cos(a), 0.21 * math.sin(a), 0.0)), 'Z', [(0.98, 0.025), (1.25, 0.025)], GOLD, sides=6)
    p.turned(c, 'Z', [(1.29, 0.17), (1.38, 0.25), (1.52, 0.28), (1.66, 0.24), (1.78, 0.14), (1.87, 0.06), (1.92, 0.035),
                      (2.2, 0.01)], GOLD, sides=20)


def _team(p):
    x0, x1, y0, y1 = HOUSE
    for xc in (x0 + 0.35, x0 + 1.05):  # the slanted radiators on the back of the roof
        p.loft([[Vector((x, y, z)) for y, z in ((y1 - 0.45, HOUSE_TOP), (y1 - 0.05, HOUSE_TOP), (y1 - 0.05, HOUSE_TOP + 0.38),
                                                 (y1 - 0.22, HOUSE_TOP + 0.38))] for x in (xc - 0.22, xc + 0.22)], TEAM)
    for x in (GATE_X - 0.25, GATE_X + 0.15):  # red bars across the roof
        p.box((x, y0 + 0.2, HOUSE_TOP), (x + 0.1, y1 - 0.5, HOUSE_TOP + 0.04), TEAM)
    # The arm of the crane: up from the turntable to the elbow, then over to its end.
    root = CRANE_BASE + Vector((0.0, 0.0, CRANE_BLOCK_TOP + 0.1))
    _beam(p, root, ELBOW, 0.2, 0.2, TEAM)
    _beam(p, ELBOW, TIP + (TIP - ELBOW).normalized() * 0.06, 0.16, 0.18, TEAM)


# --- the moving parts ----------------------------------------------------------------

def _node(name, build, origin):
    p = Parts()
    build(p)
    obj = p.finish(PREFIX + name)
    obj.data.transform(Matrix.Translation(-origin))
    obj.location = origin
    return obj


def _moving():
    y0 = HOUSE[2] + 0.14  # the face of the opening
    gate_origin = Vector((GATE_X, y0 - 0.035, GATE_TOP))

    def gate(p):
        p.box((GATE_X - GATE_HALF, y0 - 0.05, PLINTH_H), (GATE_X + GATE_HALF, y0 - 0.02, GATE_TOP), STEEL)
        for i in range(8):
            z = PLINTH_H + 0.04 + i * 0.08
            p.box((GATE_X - GATE_HALF + 0.02, y0 - 0.06, z), (GATE_X + GATE_HALF - 0.02, y0 - 0.05, z + 0.03), DARK)

    def carriage(p):
        c = CARRIAGE_REST
        p.box((c.x - 0.3, c.y - 0.25, c.z + 0.03), (c.x + 0.3, c.y + 0.25, CARRIAGE_TOP), RED)
        for x in RAIL_X:
            for y in (c.y - 0.17, c.y + 0.17):
                p.turned((x, y, c.z + 0.035), 'X', [(-0.03, 0.035), (0.03, 0.035)], DARK, sides=10)

    crate_top = Vector((CARRIAGE_REST.x, CARRIAGE_REST.y, CARRIAGE_TOP + CRATE_SIZE))

    def crate(p):
        h = CRATE_SIZE / 2
        p.box((crate_top.x - h, crate_top.y - h, CARRIAGE_TOP), (crate_top.x + h, crate_top.y + h, crate_top.z), CRATE)

    cable_origin = TIP - Vector((0.0, 0.0, 0.07))
    hook_origin = Vector((TIP.x, TIP.y, HOOK_REST))

    def cable(p):
        p.turned((TIP.x, TIP.y, 0.0), 'Z', [(hook_origin.z + 0.22, 0.012), (cable_origin.z, 0.012)], STEEL, sides=6)

    def hook(p):
        o = hook_origin
        p.box((o.x - 0.08, o.y - 0.08, o.z + 0.12), (o.x + 0.08, o.y + 0.08, o.z + 0.22), STEEL)  # the block
        for s in (-1, 1):  # two red jaws, curved in
            p.bar(o + Vector((s * 0.06, 0, 0.13)), o + Vector((s * 0.13, 0, 0.05)), 0.03, RED)
            p.bar(o + Vector((s * 0.13, 0, 0.05)), o + Vector((s * 0.08, 0, -0.02)), 0.03, RED)

    px0, px1, py0, py1 = PIT
    mid = (py0 + py1) / 2

    def leaf_a(p):
        p.box((px0, py0, PAD_TOP - 0.02), (px1, mid - 0.005, PAD_TOP - 0.002), STEEL)

    def leaf_b(p):
        p.box((px0, mid + 0.005, PAD_TOP - 0.02), (px1, py1, PAD_TOP - 0.002), STEEL)

    return [
        _node("roller_gate", gate, gate_origin),
        _node("carriage", carriage, CARRIAGE_REST),
        _node("crate", crate, crate_top),
        _node("hoist_cable", cable, cable_origin),
        _node("hoist_hook", hook, hook_origin),
        _node("hatch_a", leaf_a, Vector((GATE_X, py0, PAD_TOP - 0.011))),
        _node("hatch_b", leaf_b, Vector((GATE_X, py1, PAD_TOP - 0.011))),
    ]


# --- material -----------------------------------------------------------------------

def _material():
    """Light concrete tiles, brick in courses, dark steel, yellow, dark, yellow and black stripes, gold, red, crates, a light
    team zone."""
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
    tiles = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 2.5)), -40.0, 1.0)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 2.5)), -40.0, 1.0)))
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 16.0)), -10.0, 1.0))
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 6.0)), 40.0, -20.0))

    paving = g.mix(tiles, g.mix(grain, lin((0.56, 0.56, 0.54)), lin((0.64, 0.64, 0.62))), lin((0.34, 0.34, 0.33)))
    brick = g.mix(courses, g.mix(grain, lin((0.62, 0.34, 0.28)), lin((0.70, 0.42, 0.34))), lin((0.70, 0.66, 0.62)))
    steel = g.mix(grain, lin((0.26, 0.27, 0.29)), lin((0.34, 0.35, 0.37)))
    yellow = g.mix(grain, lin((0.92, 0.70, 0.14)), lin((0.98, 0.78, 0.22)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.08, 0.08, 0.09)))
    hazard = g.mix(stripes, lin((0.90, 0.72, 0.12)), lin((0.10, 0.10, 0.10)))
    gold = g.mix(grain, lin((0.82, 0.64, 0.24)), lin((0.92, 0.74, 0.32)))
    red = g.mix(grain, lin((0.72, 0.10, 0.08)), lin((0.82, 0.14, 0.11)))
    crate = g.mix(grain, lin((0.52, 0.36, 0.24)), lin((0.60, 0.44, 0.30)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = paving
    for m, col in zip(mask[1:], (brick, steel, yellow, dark, hazard, gold, red, crate, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.9, 0.85, 0.45, 0.4, 0.8, 0.55, 0.3, 0.45, 0.8, 0.45))
    metallic = weighted((0.0, 0.0, 0.7, 0.3, 0.2, 0.1, 0.85, 0.2, 0.0, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('ADD', g.math('MULTIPLY', tiles, mask[0]),
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


def _empty(name, point):
    node = bpy.data.objects.new(PREFIX + name, None)
    node.empty_display_type = 'PLAIN_AXES'
    node.empty_display_size = 0.08
    node.location = Vector(point)
    bpy.context.scene.collection.objects.link(node)
    return node


def _drop_bottoms(objs):
    """Deletes the faces that look down onto the plinth or the ground: never seen, and black in the bake."""
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
    _gatehouse(body)
    _pad(body)
    _crane(body)
    _tower(body)
    body = body.finish(PREFIX + "body")
    moving = _moving()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    cut = 0.4
    outline = [(2.0, -2.0 + cut), (2.0, 2.0 - cut), (2.0 - cut, 2.0), (-2.0 + cut, 2.0), (-2.0, 2.0 - cut), (-2.0, -2.0 + cut),
               (-2.0 + cut, -2.0), (2.0 - cut, -2.0)]
    base.loft([[Vector((x, y, z)) for x, y in outline] for z in (0.0, PLINTH_H)], PAVING)
    base = base.finish(PREFIX + "base")
    meshes = [body, *moving, team, base]
    bpy.context.view_layer.update()
    _drop_bottoms([body, team, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    _pack(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    empties = [_empty("carriage_out", CARRIAGE_OUT),
               _empty("hoist_grab", (TIP.x, TIP.y, CARRIAGE_TOP + CRATE_SIZE)),
               _empty("hoist_drop", (TIP.x, TIP.y, HOOK_DROP))]
    return meshes, empties, mat, graph


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
