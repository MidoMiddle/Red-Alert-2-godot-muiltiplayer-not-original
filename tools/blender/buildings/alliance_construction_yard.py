"""Alliance construction yard: the core of the Alliance's base (docs/ra2_buildings/allies/construction_yard).

Footprint 4x4 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground; the camera
sees the south (-Y in Blender, +Z in Godot) and east (+X) sides. The composition follows the owner's references; the parts are
our own:
    hangar    an arched hangar in the east, its roller gate on the south end, pillars along its sides, lit panes along its top
    pad       a raised pad south of the gate with a striped frame, a pit in its middle under two hatch leaves, rails over the pit
              from the gate to the pad's end
    carriage  a low carriage on the rails, a crate on it
    crane     a round tower south-west of the pad on a ring, its yellow lattice jib out over the pad, a hoist with a claw
    platform  a raised deck in the north-west with railings and steps, crates on it; two round fan housings and two stacks
              behind, two slanted fins at the corner
No emblems, no text. The pillars, the fins and the bands of the tower take the owner's colour, where the references have the blue.
Height 1.6 to the top of the tower. Budget 20000 triangles, texture 2048.

While its owner builds (PlayerBuildings), the game runs a cycle: the roller gate rolls up, the carriage drives out to the pad,
the claw goes down and lifts the crate, the carriage drives back in, the hatch opens, the claw lowers the crate into the pit,
the hatch and the gate shut.

Nodes (docs/art/model_standards.md, section 4):
    body            the hangar, the pad and its rails, the tower and the jib, the platform, the fans, the stacks
    roller_gate     the gate; its origin at the top of the opening, the gate hanging down along -Y: the game shrinks its Y
    carriage        the carriage at rest inside the hangar; carriage_out (empty) is where it stops on the pad
    crate           the crate; its origin the middle of its top, on the carriage at rest
    hoist_cable     the cable; its origin under the jib's end, hanging down along -Y: the game stretches it down to the claw
    hoist_hook      the claw; its origin the grab point under it. hoist_grab (empty): where it takes the crate; hoist_drop
                    (empty): where it lets the crate's top down to, in the pit
    hatch_a, hatch_b the two leaves over the pit; their origins on their hinges along X: the game turns hatch_a about its X by
                    -85° and hatch_b by +85°, so they drop into the pit
    team_mask       the pillars, the fins and the bands of the tower: the game paints them in the owner's colour
    base            the paved plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_construction_yard as acy
    print(acy.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_construction_yard"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


PAVING, SKIN, STEEL, YELLOW, DARK, HAZARD, LAMP, CRATE, TEAM = (_part(i) for i in range(9))
PARTS = 9

PLINTH_H = 0.06
# The hangar: its middle along x, its half-width, its south (gate) and north ends, where its arch springs, the arch's rise.
HANGAR_X, HANGAR_RX, HANGAR_Y0, HANGAR_Y1 = 1.05, 0.85, -0.55, 1.9
HANGAR_WALL, HANGAR_RZ = 0.35, 0.75
GATE_HALF, GATE_TOP = 0.45, 0.75
# The pad and its pit.
PAD = (0.45, 1.65, -1.92, HANGAR_Y0)  # x0, x1, y0, y1: up to the gate
PAD_TOP = 0.2
PIT = (0.75, 1.35, -1.6, -1.0)
PIT_FLOOR = 0.045
RAIL_X = (HANGAR_X - 0.28, HANGAR_X + 0.28)
RAIL_TOP = 0.24
CARRIAGE_REST = Vector((HANGAR_X, -0.3, RAIL_TOP))
CARRIAGE_OUT = Vector((HANGAR_X, -1.3, RAIL_TOP))
CARRIAGE_TOP = RAIL_TOP + 0.1
CRATE_SIZE = 0.22
# The crane.
TOWER = Vector((-0.45, -0.75, 0.0))
TOWER_TOP = 1.6
JIB_ROOT_Z = 1.25
TIP = Vector((CARRIAGE_OUT.x, CARRIAGE_OUT.y, 1.32))  # the jib's end, over the carriage on the pad
HOOK_REST = 0.95  # the claw's grab point at rest
HOOK_DROP = PIT_FLOOR - 0.01  # how deep the crate's top goes into the pit
# The platform.
DECK = (-1.92, -0.1, 0.1, 1.92)
DECK_TOP = 0.3
FANS = (Vector((-0.5, 1.32, 0.0)), Vector((-1.28, 1.32, 0.0)))


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


# --- body -------------------------------------------------------------------------

def _hangar(p):
    # The walls and the arch in one outline across the hangar, pushed along y: its ends are the south and north walls.
    steps = 18
    arch = [(HANGAR_X + HANGAR_RX * math.cos(a), HANGAR_WALL + HANGAR_RZ * math.sin(a)) for a in (math.pi * i / steps for i in range(steps + 1))]
    outline = [(HANGAR_X + HANGAR_RX, PLINTH_H)] + arch + [(HANGAR_X - HANGAR_RX, PLINTH_H)]
    p.loft([[Vector((x, y, z)) for x, z in outline] for y in (HANGAR_Y0, HANGAR_Y1)], SKIN)
    for y in (0.2, 0.95):  # steel ribs over the arch
        rib_out = [(HANGAR_X + (HANGAR_RX + 0.03) * math.cos(a), HANGAR_WALL + (HANGAR_RZ + 0.03) * math.sin(a)) for a in (math.pi * i / steps for i in range(steps + 1))]
        rib_in = [(HANGAR_X + (HANGAR_RX - 0.01) * math.cos(a), HANGAR_WALL + (HANGAR_RZ - 0.01) * math.sin(a)) for a in (math.pi * i / steps for i in reversed(range(steps + 1)))]
        p.loft([[Vector((x, yy, z)) for x, z in rib_out + rib_in] for yy in (y - 0.04, y + 0.04)], STEEL)
    for y in (-0.15, 0.5, 1.15, 1.6):  # lit panes along the top
        p.box((HANGAR_X - 0.09, y - 0.09, HANGAR_WALL + HANGAR_RZ - 0.005), (HANGAR_X + 0.09, y + 0.09, HANGAR_WALL + HANGAR_RZ + 0.015), LAMP)
    # The frame of the gate and the dark opening behind the gate.
    y0 = HANGAR_Y0
    p.box((HANGAR_X - GATE_HALF - 0.08, y0 - 0.06, PLINTH_H), (HANGAR_X - GATE_HALF, y0 + 0.01, GATE_TOP + 0.08), STEEL)
    p.box((HANGAR_X + GATE_HALF, y0 - 0.06, PLINTH_H), (HANGAR_X + GATE_HALF + 0.08, y0 + 0.01, GATE_TOP + 0.08), STEEL)
    p.box((HANGAR_X - GATE_HALF - 0.08, y0 - 0.06, GATE_TOP), (HANGAR_X + GATE_HALF + 0.08, y0 + 0.01, GATE_TOP + 0.08), STEEL)
    p.box((HANGAR_X - GATE_HALF, y0 - 0.012, PLINTH_H), (HANGAR_X + GATE_HALF, y0 - 0.005, GATE_TOP), DARK)


def _pad(p):
    x0, x1, y0, y1 = PAD
    px0, px1, py0, py1 = PIT
    for lo, hi in (((x0, y0), (x1, py0)), ((x0, py1), (x1, y1)), ((x0, py0), (px0, py1)), ((px1, py0), (x1, py1))):
        p.box((lo[0], lo[1], PLINTH_H), (hi[0], hi[1], PAD_TOP), HAZARD)  # the frame round the pit
    p.box((px0, py0, PLINTH_H), (px1, py1, PIT_FLOOR), DARK)  # the pit's floor
    for x in RAIL_X:  # the rails from the gate over the pit to the pad's end
        p.box((x - 0.03, y0 + 0.02, PAD_TOP), (x + 0.03, HANGAR_Y0 + 0.4, RAIL_TOP), STEEL)


def _crane_tower(p):
    c = TOWER
    p.turned(c, 'Z', [(PLINTH_H, 0.44), (0.16, 0.44), (0.16, 0.32), (0.22, 0.3)], STEEL, sides=24)  # the ring it stands on
    p.turned(c, 'Z', [(0.22, 0.28), (TOWER_TOP - 0.12, 0.28), (TOWER_TOP - 0.06, 0.24), (TOWER_TOP, 0.16)], SKIN, sides=24)
    p.turned(c, 'Z', [(JIB_ROOT_Z - 0.18, 0.31), (JIB_ROOT_Z + 0.12, 0.31)], YELLOW, sides=24)  # the yellow collar for the jib
    # The jib: a lattice of yellow bars from the collar out to the tip.
    to = Vector((TIP.x - c.x, TIP.y - c.y, 0.0)).normalized()
    side = Vector((-to.y, to.x, 0.0))
    root = c + to * 0.3
    lower = [root + side * s + Vector((0.0, 0.0, JIB_ROOT_Z - 0.12)) for s in (-0.11, 0.11)]
    upper = [root + side * s + Vector((0.0, 0.0, JIB_ROOT_Z + 0.08)) for s in (-0.08, 0.08)]
    end = TIP + to * 0.02
    ends_low = [end + side * s * 0.6 + Vector((0.0, 0.0, -0.04)) for s in (-0.11, 0.11)]
    ends_up = [end + side * s * 0.6 + Vector((0.0, 0.0, 0.06)) for s in (-0.08, 0.08)]
    for a, b in zip(lower + upper, ends_low + ends_up):
        p.bar(a, b, 0.022, YELLOW)
    n = 6
    for i in range(n + 1):
        t = i / n
        ql = [a.lerp(b, t) for a, b in zip(lower, ends_low)]
        qu = [a.lerp(b, t) for a, b in zip(upper, ends_up)]
        p.bar(ql[0], ql[1], 0.012, YELLOW)
        p.bar(qu[0], qu[1], 0.012, YELLOW)
        p.bar(ql[0], qu[0], 0.012, YELLOW)
        p.bar(ql[1], qu[1], 0.012, YELLOW)
        if i < n:
            t2 = (i + 1) / n
            p.bar(ql[0], upper[0].lerp(ends_up[0], t2), 0.01, YELLOW)
            p.bar(ql[1], upper[1].lerp(ends_up[1], t2), 0.01, YELLOW)
    p.turned(TIP, 'X', [(-0.06, 0.07), (0.06, 0.07)], STEEL, sides=14)  # the drum at the tip


def _platform(p):
    x0, x1, y0, y1 = DECK
    p.box((x0, y0, PLINTH_H), (x1, y1, DECK_TOP), STEEL)
    for i in range(10):  # the railing along the south and the east edges
        x = x0 + 0.1 + i * (x1 - x0 - 0.2) / 9
        p.box((x - 0.012, y0, DECK_TOP), (x + 0.012, y0 + 0.024, DECK_TOP + 0.16), STEEL)
    for i in range(10):
        y = y0 + 0.1 + i * (y1 - y0 - 0.2) / 9
        p.box((x1 - 0.024, y - 0.012, DECK_TOP), (x1, y + 0.012, DECK_TOP + 0.16), STEEL)
    p.box((x0, y0, DECK_TOP + 0.15), (x1, y0 + 0.024, DECK_TOP + 0.17), STEEL)
    p.box((x1 - 0.024, y0, DECK_TOP + 0.15), (x1, y1, DECK_TOP + 0.17), STEEL)
    for i in range(4):  # the steps down to the south
        y = y0 - 0.06 * (i + 1)
        p.box((-1.62, y, PLINTH_H), (-1.3, y + 0.06, DECK_TOP - 0.06 * (i + 1)), STEEL)
    for x, y, s in ((-1.6, 0.4, 0.14), (-1.42, 0.38, 0.12), (-1.52, 0.55, 0.12)):  # crates on the deck
        p.box((x - s / 2, y - s / 2, DECK_TOP), (x + s / 2, y + s / 2, DECK_TOP + s), CRATE)
    for c in FANS:  # the fan housings, a grille on top
        p.turned(c, 'Z', [(DECK_TOP, 0.36), (0.78, 0.36), (0.9, 0.31), (0.9, 0.26), (0.84, 0.26)], SKIN, sides=24)
        p.turned(c, 'Z', [(0.84, 0.26), (0.841, 0.02)], DARK, sides=24)
        for k in range(8):
            a = math.pi * k / 4
            p.bar(c + Vector((0.0, 0.0, 0.86)), c + Vector((0.24 * math.cos(a), 0.24 * math.sin(a), 0.86)), 0.012, STEEL)
        p.turned(c, 'Z', [(0.84, 0.05), (0.9, 0.04)], STEEL, sides=10)
    for x, y, h in ((-1.75, 1.0, 1.05), (-1.75, 0.72, 0.95)):  # the stacks
        p.turned((x, y, 0.0), 'Z', [(DECK_TOP, 0.08), (h, 0.08), (h, 0.06)], DARK, sides=12)


def _team(p):
    for x in (HANGAR_X - HANGAR_RX - 0.07, HANGAR_X + HANGAR_RX):  # the pillars along the sides of the hangar
        for y in (-0.3, 0.5, 1.3):
            p.box((x, y - 0.1, PLINTH_H), (x + 0.07, y + 0.1, HANGAR_WALL + 0.12), TEAM)
    for y in (0.25, 1.35):  # the slanted fins at the north-west corner, standing on the deck
        p.profile_x([(y, DECK_TOP), (y + 0.42, DECK_TOP), (y + 0.42, 1.0), (y + 0.28, 1.0)], -1.92, -1.84, TEAM)
    for z0, z1 in ((0.3, 0.42), (TOWER_TOP - 0.2, TOWER_TOP - 0.1)):  # the bands of the tower
        p.turned(TOWER, 'Z', [(z0, 0.295), (z1, 0.295)], TEAM, sides=24)


# --- the moving parts ----------------------------------------------------------------

def _node(name, build, origin):
    """A mesh node built in the model's frame, its origin moved to origin."""
    p = Parts()
    build(p)
    obj = p.finish(PREFIX + name)
    obj.data.transform(Matrix.Translation(-origin))
    obj.location = origin
    return obj


def _moving():
    y0 = HANGAR_Y0
    gate_origin = Vector((HANGAR_X, y0 - 0.035, GATE_TOP))

    def gate(p):
        p.box((HANGAR_X - GATE_HALF, y0 - 0.05, PLINTH_H), (HANGAR_X + GATE_HALF, y0 - 0.02, GATE_TOP), STEEL)
        for i in range(8):  # the slats
            z = PLINTH_H + 0.04 + i * 0.08
            p.box((HANGAR_X - GATE_HALF + 0.02, y0 - 0.06, z), (HANGAR_X + GATE_HALF - 0.02, y0 - 0.05, z + 0.03), SKIN)

    def carriage(p):
        c = CARRIAGE_REST
        p.box((c.x - 0.3, c.y - 0.25, c.z + 0.03), (c.x + 0.3, c.y + 0.25, CARRIAGE_TOP), HAZARD)
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
        p.turned((0.0, 0.0, 0.0), 'Z', [(hook_origin.z + 0.22, 0.012), (cable_origin.z, 0.012)], STEEL, sides=6)
        # Built on the axis through the origin: move it over the tip.
        for v in p.bm.verts:
            v.co.x += TIP.x
            v.co.y += TIP.y

    def hook(p):
        o = hook_origin
        p.box((o.x - 0.07, o.y - 0.07, o.z + 0.12), (o.x + 0.07, o.y + 0.07, o.z + 0.22), YELLOW)  # the block
        for k in range(3):  # the claws
            a = 2 * math.pi * k / 3
            out = Vector((math.cos(a), math.sin(a), 0.0))
            p.bar(o + Vector((0, 0, 0.12)) + out * 0.05, o + out * 0.1 + Vector((0, 0, 0.04)), 0.014, YELLOW)
            p.bar(o + out * 0.1 + Vector((0, 0, 0.04)), o + out * 0.07 + Vector((0, 0, -0.02)), 0.014, YELLOW)

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
        _node("hatch_a", leaf_a, Vector((HANGAR_X, py0, PAD_TOP - 0.011))),
        _node("hatch_b", leaf_b, Vector((HANGAR_X, py1, PAD_TOP - 0.011))),
    ]


# --- material -----------------------------------------------------------------------

def _material():
    """Dark paving in tiles, a light skin in panels, dark steel, yellow, dark, yellow and black stripes, lit panes, crates,
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
    cells = g.add('ShaderNodeTexVoronoi', feature='DISTANCE_TO_EDGE')
    g.link(coord, cells, 'Vector')
    g.put(cells, 'Scale', 3.0)
    joints = g.clamp01(g.math('MULTIPLY_ADD', rc.sock(cells, 'Distance', outputs=True), -30.0, 1.0))
    seams = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 3.0)), -30.0, 1.0))
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 6.0)), 40.0, -20.0))

    paving = g.mix(joints, g.mix(grain, lin((0.30, 0.30, 0.28)), lin((0.36, 0.36, 0.34))), lin((0.16, 0.16, 0.15)))
    skin = g.mix(seams, g.mix(grain, lin((0.70, 0.72, 0.75)), lin((0.78, 0.80, 0.83))), lin((0.44, 0.46, 0.50)))
    steel = g.mix(grain, lin((0.20, 0.21, 0.23)), lin((0.27, 0.28, 0.30)))
    yellow = g.mix(grain, lin((0.92, 0.70, 0.14)), lin((0.98, 0.78, 0.22)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.08, 0.08, 0.09)))
    hazard = g.mix(stripes, lin((0.90, 0.72, 0.12)), lin((0.10, 0.10, 0.10)))
    lamp = g.mix(grain, lin((0.40, 0.85, 1.0)), lin((0.52, 0.92, 1.0)))
    crate = g.mix(grain, lin((0.52, 0.36, 0.24)), lin((0.60, 0.44, 0.30)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = paving
    for m, col in zip(mask[1:], (skin, steel, yellow, dark, hazard, lamp, crate, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.9, 0.4, 0.45, 0.4, 0.8, 0.55, 0.2, 0.8, 0.45))
    metallic = weighted((0.0, 0.6, 0.7, 0.3, 0.2, 0.1, 0.0, 0.0, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', joints, mask[0]), 0.8)), 0.03, 0.005)
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
    """Packs the UV islands tighter: the many thin bars leave the smart projection's texture half empty."""
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')


def build():
    _clear()
    body = Parts()
    _hangar(body)
    _pad(body)
    _crane_tower(body)
    _platform(body)
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
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the tower, the fans and the arch smooth, the edges sharp
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
