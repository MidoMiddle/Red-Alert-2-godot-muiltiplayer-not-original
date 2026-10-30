"""Factory hall, element 05 of the industrial set (docs/industrial_elements/05_factory_hall): a long one-storey hall of brick under a
gable roof of corrugated sheet; twelve soldiers hold it (game/data/neutral/civilian.yaml).

Footprint 4x3 cells, origin at the centre of the footprint on the ground, the front to -Y; made upright as the city set (a storey to a
cell, the game squashes it, families/industrial_common.py): the walls to the eaves at 2.6, the ridge at 3.6, the lantern over it to 4.1,
the stack to 4.7. The parts:
    walls      red-brown brick on a darker plinth, piers between the bays, a brick cornice; tall windows of many small panes in dark
               steel frames along both long sides, two on each gable end and a round one high in each gable; drainpipes at the corners
    gate       in the middle of the front: two dark green sliding leaves with braces on a rail, a lamp over them; a sign board above
               (the owner's colour); a side door with steps at the right gable end; a ladder up the left gable end to the roof
    roof       two slopes of corrugated sheet over the eaves, a lantern along the ridge with louvres, skylights in the slopes, two
               turbine vents and a steel stack with bands at the back
    yard       a concrete apron, a loading ramp with pallets and crates at the front right, drums at the back left
Nodes: "body", "roof", "team_mask" (the sign board over the gate), "base" (the apron); "smoke_anchor" at the top of the stack;
fire_1..3 when abandoned. One GLB per state:
    factory_hall                   whole, empty
    factory_hall_occupied          whole, twelve soldiers inside: barrels out of the front windows, sandbags before the gate
    factory_hall_damaged           damaged: half the windows knocked out, a gate leaf torn off and down, holes in the walls, sheets
                                   gone from the roof, bricks at the foot, soot
    factory_hall_damaged_occupied  the same, with the barrels and the sandbags
    factory_hall_abandoned         critically damaged, its soldiers out: the right third fallen in, its walls broken down to stumps,
                                   the trusses bare and charred, the lantern down, rubble inside and out; fire in three places
    factory_hall_ruins             destroyed: stumps of the walls, the left gable standing half, trusses and sheets on the floor,
                                   heaps of brick
Budget 12000 triangles, texture 2048 (docs/industrial_elements/05_factory_hall/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.industrial_factory_hall as fh
    print(fh.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

SIZE = 2048
VARIANTS = {
    "factory_hall": "intact",
    "factory_hall_occupied": "occupied",
    "factory_hall_damaged": "damaged",
    "factory_hall_damaged_occupied": "damaged_occupied",
    "factory_hall_abandoned": "abandoned",
    "factory_hall_ruins": "ruins",
}

(BRICK, PLINTH, GLASS, FRAME, ROOF, GATE, METAL, CONCRETE, DARK, SAND, TEAM, LAMP, WOOD, DRUM, SKY, FLOOR) = (cc.part(i) for i in range(16))
PARTS = 16

X0, Y0 = 1.9, 1.4
APRON = 0.05
EAVE = 2.6
RIDGE = 3.6
OVER = 0.12  # the roof over the walls
WALLS = cc.Facade(X0, Y0)
GATE_HALF = 0.42
PIERS = (-1.38, -0.9, -GATE_HALF, GATE_HALF, 0.9, 1.38)
FRONT_BAYS = (-1.64, -1.14, -0.66, 0.66, 1.14, 1.64)
BACK_BAYS = tuple(-1.64 + 0.47 * k for k in range(8))
SIDE_BAYS = {"left": (-0.75, 0.0, 0.75), "right": (-0.75, 0.75)}
WIN = (0.8, 2.15)  # the windows' sill and head
BREAK_X = 0.75  # the abandoned one: what stands right of it has fallen
STACK = Vector((-1.2, 0.85, 0.0))
FIRE_POINTS = ((1.3, 0.0, 1.2), (1.0, -1.0, 0.6), (0.2, 0.3, RIDGE))


def _slope(t):
    """The height of the roof's underside over the walls at |y| = t (0 at the ridge, Y0 at the eaves)."""
    return RIDGE - (RIDGE - EAVE) * t / Y0


def _walls(p, variant):
    abandoned = variant == "abandoned"
    x1 = BREAK_X if abandoned else X0
    p.box((-X0, -Y0, APRON), (x1, Y0, EAVE), BRICK)
    p.box((-X0 - 0.025, -Y0 - 0.025, APRON), (x1 + (0.025 if not abandoned else 0.0), Y0 + 0.025, APRON + 0.35), PLINTH)
    p.box((-X0 - 0.04, -Y0 - 0.04, EAVE - 0.14), (x1 + (0.04 if not abandoned else 0.0), Y0 + 0.04, EAVE), PLINTH)  # the cornice
    # The gables: the left one whole; the right one when the hall stands.
    for x, inward in ((-X0, 1), (X0, -1)):
        if abandoned and x > 0:
            continue
        p.profile_x([(-Y0, EAVE), (Y0, EAVE), (0.0, RIDGE)], min(x, x + inward * 0.12), max(x, x + inward * 0.12), BRICK)
        c = WALLS.point("left" if x < 0 else "right", 0.0, EAVE + 0.45, 0.0)
        p.turned(c, 'X', [(-0.02, 0.2), (0.025, 0.2), (0.025, 0.14), (-0.02, 0.14)], PLINTH, sides=16)  # the round window's ring
        p.turned(c - Vector((0.012 * inward, 0, 0)), 'X', [(-0.01, 0.14), (0.01, 0.14)], DARK if variant != "intact" and variant != "occupied" else GLASS, sides=16)
    # Piers between the bays on the long sides.
    for wall, us in (("front", PIERS + (-X0 + 0.06, X0 - 0.06)), ("back", tuple(-1.88 + 0.47 * k for k in range(9)))):
        for u in us:
            if abandoned and u > BREAK_X:
                continue
            WALLS.box(p, wall, u - 0.06, u + 0.06, APRON, EAVE - 0.14, -0.01, 0.05, BRICK)
    # Drainpipes at the corners.
    for x, y in ((-X0 - 0.05, -Y0 - 0.05), (-X0 - 0.05, Y0 + 0.05)) + ((() if abandoned else ((X0 + 0.05, -Y0 - 0.05), (X0 + 0.05, Y0 + 0.05)))):
        p.cylinder((x, y, (APRON + EAVE) / 2), 'Z', 0.03, (EAVE - APRON) / 2, METAL, sides=8)


def _windows(p, variant):
    broken_ones = variant in ("damaged", "damaged_occupied", "abandoned")
    k = 0
    for wall, bays in (("front", FRONT_BAYS), ("back", BACK_BAYS), ("left", SIDE_BAYS["left"]), ("right", SIDE_BAYS["right"])):
        for u in bays:
            if variant == "abandoned" and (wall in ("front", "back") and u > BREAK_X - 0.2 or wall == "right"):
                continue
            k += 1
            broken = DARK if broken_ones and k % 2 == 0 else None
            WALLS.window(p, wall, u, WIN[0], WIN[1], 0.17, GLASS, FRAME, bars=(2, 4), broken=broken)


def _gate(p, team, variant):
    """The sliding gate in the middle of the front, its rail, a lamp, the sign board over it (the owner's colour)."""
    y = -Y0
    top = 1.85
    torn = variant in ("damaged", "damaged_occupied", "abandoned")
    WALLS.box(p, "front", -GATE_HALF, GATE_HALF, APRON, top, -0.02, 0.0, DARK)  # the opening
    for k, (u0, u1) in enumerate(((-GATE_HALF + 0.02, -0.01), (0.01, GATE_HALF - 0.02))):
        if torn and k == 1:
            # The right leaf off its rail, lying on the apron before the gate.
            cc.block(p, (0.3, y - 0.42, APRON + 0.03), (0.42, 0.75, 0.04), (0.0, 0.0, 0.25), GATE)
            continue
        WALLS.box(p, "front", u0, u1, APRON + 0.02, top - 0.02, 0.0, 0.03, GATE)
        for z in (APRON + 0.45, top - 0.45):
            WALLS.box(p, "front", u0 + 0.02, u1 - 0.02, z - 0.03, z + 0.03, 0.03, 0.045, GATE)
        a = WALLS.point("front", u0 + 0.04, APRON + 0.48, 0.04)
        b = WALLS.point("front", u1 - 0.04, top - 0.48, 0.04)
        p.bar(a, b, 0.018, GATE)  # a brace
        WALLS.box(p, "front", u1 - 0.07, u1 - 0.05, (APRON + top) / 2 - 0.1, (APRON + top) / 2 + 0.1, 0.045, 0.06, METAL)  # a handle
    WALLS.box(p, "front", -GATE_HALF - 0.35, GATE_HALF + 0.35, top + 0.02, top + 0.08, 0.0, 0.06, METAL)  # the rail
    WALLS.box(p, "front", -GATE_HALF - 0.06, GATE_HALF + 0.06, top, top + 0.02, -0.01, 0.03, METAL)
    # The lamp over the gate on a bracket.
    a = WALLS.point("front", 0.0, top + 0.3, 0.0)
    p.bar(a, a + Vector((0, -0.18, -0.05)), 0.012, METAL)
    p.turned(a + Vector((0, -0.18, -0.06)), 'Z', [(-0.06, 0.0), (-0.05, 0.06), (0.0, 0.08), (0.01, 0.0)], METAL, sides=10)
    p.turned(a + Vector((0, -0.18, -0.13)), 'Z', [(0.0, 0.0), (0.02, 0.04), (0.06, 0.0)], LAMP, sides=8)
    # The sign board: a frame and its face in the owner's colour.
    WALLS.box(p, "front", -0.7, 0.7, top + 0.22, top + 0.62, 0.0, 0.03, METAL)
    WALLS.box(team, "front", -0.66, 0.66, top + 0.26, top + 0.58, 0.03, 0.045, TEAM)


def _side_door(p, variant):
    """A door with a little canopy and steps at the right gable end; a ladder up the left one."""
    if variant != "abandoned":
        WALLS.box(p, "right", -0.2, 0.2, APRON + 0.18, 1.3, -0.01, 0.02, DARK if variant != "intact" else GATE)
        WALLS.box(p, "right", -0.28, 0.28, 1.36, 1.42, 0.0, 0.28, METAL)
        for k in range(3):
            WALLS.box(p, "right", -0.3, 0.3, APRON, APRON + 0.06 * (3 - k), 0.0, 0.12 + 0.12 * k, CONCRETE)
    # The ladder: two rails and rungs from the apron to the eaves, its rails bent over the roof's edge.
    for u in (0.95, 1.13):
        a = WALLS.point("left", u, APRON, 0.12)
        p.bar(a, WALLS.point("left", u, EAVE + 0.35, 0.12), 0.012, METAL)
        p.bar(WALLS.point("left", u, EAVE + 0.35, 0.12), WALLS.point("left", u, EAVE + 0.35, -0.12), 0.012, METAL)
    for z in cc.steps(APRON + 0.2, EAVE + 0.3, 0.2):
        p.bar(WALLS.point("left", 0.95, z, 0.12), WALLS.point("left", 1.13, z, 0.12), 0.008, METAL)
    for z in cc.steps(1.0, EAVE + 0.2, 0.35):  # the cage hoops
        p.bar(WALLS.point("left", 0.93, z, 0.12), WALLS.point("left", 0.93, z, 0.3), 0.006, METAL)
        p.bar(WALLS.point("left", 1.15, z, 0.12), WALLS.point("left", 1.15, z, 0.3), 0.006, METAL)
        p.bar(WALLS.point("left", 0.93, z, 0.3), WALLS.point("left", 1.15, z, 0.3), 0.006, METAL)


def _roof(r, variant):
    """The two slopes, the lantern on the ridge, skylights, vents and the stack."""
    abandoned = variant == "abandoned"
    x1 = BREAK_X - 0.05 if abandoned else X0 + OVER
    holes = variant in ("damaged", "damaged_occupied")
    t = 0.05
    for side in (-1, 1):
        ye = side * (Y0 + OVER)
        ze = _slope(Y0 + OVER)
        r.profile_x([(ye, ze), (0.0, RIDGE), (0.0, RIDGE + t), (ye, ze + t)] if side > 0 else
                    [(0.0, RIDGE), (ye, ze), (ye, ze + t), (0.0, RIDGE + t)], -X0 - OVER, x1, ROOF)
        # Skylights: strips of glass in the slope.
        for xs in ((-1.5, -0.9), (-0.3, 0.3), (0.9, 1.5)):
            if abandoned and xs[1] > BREAK_X:
                continue
            ya, yb = side * 0.35, side * 0.95
            za, zb = _slope(0.35) + t + 0.005, _slope(0.95) + t + 0.005
            r.loft([[Vector((x, ya, za)), Vector((x, yb, zb)), Vector((x, yb, zb + 0.015)), Vector((x, ya, za + 0.015))] for x in xs], SKY)
        if holes:
            for x in (-0.6, 1.2):
                yh = side * 0.6
                cc.block(r, (x, yh, _slope(0.6) + t + 0.01), (0.35, 0.3, 0.012), (side * math.atan2(RIDGE - EAVE, Y0), 0.0, 0.0), DARK)
    if abandoned:
        # Bare charred trusses over the fallen part, one down across it.
        for x in (1.05, 1.5):
            r.bar((x, -Y0, EAVE * 0.75), (x, 0.0, RIDGE * 0.8), 0.04, DARK)
            r.bar((x, 0.0, RIDGE * 0.8), (x, Y0, EAVE * 0.7), 0.04, DARK)
            r.bar((x, -Y0, EAVE * 0.75), (x, Y0, EAVE * 0.7), 0.03, DARK)
        r.bar((BREAK_X, -0.3, RIDGE - 0.1), (1.7, 0.4, APRON + 0.5), 0.045, DARK)
        cc.block(r, (1.25, -0.6, APRON + 0.3), (0.8, 0.6, 0.03), (0.3, -0.5, 0.2), ROOF)
        cc.block(r, (1.4, 0.6, APRON + 0.6), (0.7, 0.5, 0.03), (-0.6, 0.3, -0.3), ROOF)
    # The lantern along the ridge: low walls of louvres under a small gable.
    lx1 = BREAK_X - 0.3 if abandoned else 1.3
    r.box((-1.3, -0.3, RIDGE - 0.15), (lx1, 0.3, RIDGE + 0.32), METAL)
    for x in cc.steps(-1.25, lx1 - 0.05, 0.12):
        for side in (-1, 1):
            r.box((x - 0.04, side * 0.3 - 0.008, RIDGE + 0.02), (x + 0.04, side * 0.3 + 0.008, RIDGE + 0.28), DARK)
    for side in (-1, 1):
        r.profile_x([(0.0, RIDGE + 0.62), (side * 0.4, RIDGE + 0.3), (side * 0.4, RIDGE + 0.34), (0.0, RIDGE + 0.66)] if side < 0 else
                    [(side * 0.4, RIDGE + 0.3), (0.0, RIDGE + 0.62), (0.0, RIDGE + 0.66), (side * 0.4, RIDGE + 0.34)], -1.36, lx1 + 0.06, ROOF)
    for x in (-1.3, lx1):
        r.profile_x([(-0.3, RIDGE + 0.32), (0.3, RIDGE + 0.32), (0.0, RIDGE + 0.6)], x - 0.02, x + 0.02, METAL)
    # Turbine vents on the front slope.
    for x in ((-0.5, 0.5) if not abandoned else (-0.5,)):
        c = Vector((x, -0.95, _slope(0.95) + t))
        r.turned(c, 'Z', [(-0.05, 0.08), (0.12, 0.08), (0.14, 0.11), (0.3, 0.11), (0.36, 0.06), (0.4, 0.0)], METAL, sides=12)
    # The stack at the back left, with bands.
    top = 4.7
    base_z = _slope(STACK.y) + t
    r.turned((STACK.x, STACK.y, 0.0), 'Z', [(base_z - 0.1, 0.12), (top, 0.1), (top, 0.08), (top - 0.3, 0.08)], METAL, sides=14)
    for z in cc.steps(base_z + 0.25, top - 0.1, 0.35):
        r.turned((STACK.x, STACK.y, z), 'Z', [(-0.02, 0.118), (0.02, 0.118)], DARK, sides=14)


def _yard(p, base, variant):
    base.box((-1.98, -1.48, 0.0), (1.98, 1.48, APRON), CONCRETE)
    if variant == "ruins":
        return
    # A loading ramp at the front right with pallets and crates on it.
    p.loft([[Vector((x, -Y0, APRON)), Vector((x, -Y0, APRON + 0.3)), Vector((x, -Y0 - 0.38, APRON + 0.3)), Vector((x, -Y0 - 0.5, APRON))]
            for x in (0.95, 1.75)], CONCRETE)
    if variant != "abandoned":
        for x in (1.1, 1.45):
            p.box((x - 0.13, -Y0 - 0.33, APRON + 0.3), (x + 0.13, -Y0 - 0.07, APRON + 0.35), WOOD)  # pallets
        p.box((1.05 - 0.1, -Y0 - 0.3, APRON + 0.35), (1.05 + 0.1, -Y0 - 0.1, APRON + 0.6), WOOD)
        p.box((1.5 - 0.09, -Y0 - 0.28, APRON + 0.35), (1.5 + 0.09, -Y0 - 0.12, APRON + 0.55), WOOD)
    # Drums at the back left.
    for k, (x, y) in enumerate(((-1.7, Y0 + 0.2), (-1.55, Y0 + 0.22), (-1.62, Y0 + 0.08))):
        if variant in ("abandoned",) and k == 2:
            p.turned((x + 0.1, y + 0.05, APRON + 0.08), 'X', [(-0.15, 0.08), (0.15, 0.08)], DRUM, sides=10)
            continue
        p.turned((x, y, APRON), 'Z', [(0.0, 0.075), (0.32, 0.075)], DRUM, sides=10)


def _occupied(p):
    """Twelve soldiers: a barrel out of each front window, sandbags before the gate."""
    z = (WIN[0] + WIN[1]) / 2 + 0.2
    ic.barrels(p, [WALLS.point("front", u, z, -0.02) for u in FRONT_BAYS], (0.0, -1.0, 0.0), DARK)
    ic.barrels(p, [WALLS.point("back", u, z, -0.02) for u in BACK_BAYS[::2]], (0.0, 1.0, 0.0), DARK)
    ic.sandbags(p, cc.steps(-0.5, 0.5, 0.22), -Y0 - 0.25, APRON, SAND)


def _damage(p, rnd, variant):
    """Holes in the walls and bricks at their foot."""
    for wall, u, z in (("front", -1.38, 1.5), ("back", 0.9, 0.9), ("left", -0.4, 1.9)):
        WALLS.box(p, wall, u - 0.14, u + 0.14, z - 0.12, z + 0.16, -0.01, 0.012, DARK)
    for x, y in ((-1.4, -Y0 - 0.12), (0.9, Y0 + 0.12), (-X0 - 0.12, -0.4), (1.7, -Y0 - 0.15)):
        ic.rubble(p, rnd, x - 0.12, x + 0.12, y - 0.05, y + 0.05, APRON, 3, 0.1, BRICK)


def _abandoned(p, rnd):
    """The fallen right third: stumps of its walls, the floor inside, rubble."""
    p.box((BREAK_X, -Y0, APRON), (X0, Y0, APRON + 0.02), FLOOR)
    ic.stumps(p, rnd, [((BREAK_X, -Y0 + 0.05), (X0 - 0.05, -Y0 + 0.05)), ((X0 - 0.05, -Y0 + 0.05), (X0 - 0.05, Y0 - 0.05)),
                       ((X0 - 0.05, Y0 - 0.05), (BREAK_X, Y0 - 0.05))], APRON, 0.2, 1.5, BRICK, step=0.25, gap=0.15)
    # The edge of the standing part torn: brick hanging off its end.
    for z in cc.steps(0.6, EAVE - 0.3, 0.45):
        cc.block(p, (BREAK_X + rnd.uniform(0.0, 0.1), rnd.uniform(-Y0, Y0), z), (0.18, 0.3, 0.2), (0.1, 0.2, rnd.uniform(0, 1)), BRICK)
    ic.rubble(p, rnd, BREAK_X + 0.1, X0 - 0.1, -Y0 + 0.2, Y0 - 0.2, APRON, 18, 0.2, BRICK, height=0.3)
    ic.rubble(p, rnd, X0 - 0.1, X0 + 0.08, -Y0, Y0, APRON, 6, 0.15, BRICK)
    ic.rubble(p, rnd, BREAK_X, X0, -Y0 - 0.1, -Y0 + 0.05, APRON, 6, 0.15, BRICK)


def _ruins(p, rnd):
    p.box((-X0, -Y0, APRON), (X0, Y0, APRON + 0.02), FLOOR)
    ic.stumps(p, rnd, [((-X0 + 0.05, -Y0 + 0.05), (X0 - 0.05, -Y0 + 0.05)), ((X0 - 0.05, -Y0 + 0.05), (X0 - 0.05, Y0 - 0.05)),
                       ((X0 - 0.05, Y0 - 0.05), (-X0 + 0.05, Y0 - 0.05))], APRON, 0.15, 1.1, BRICK, step=0.25, gap=0.2)
    # The left gable end standing half, its wall jagged on top.
    p.box((-X0, -Y0, APRON), (-X0 + 0.12, -0.1, 2.2), BRICK)
    p.box((-X0, -0.1, APRON), (-X0 + 0.12, 0.6, 2.9), BRICK)
    ic.stumps(p, rnd, [((-X0 + 0.06, 0.6), (-X0 + 0.06, Y0 - 0.05))], APRON, 0.6, 1.6, BRICK, step=0.2, gap=0.0, thick=0.12)
    WALLS.window(p, "left", -0.75, WIN[0], WIN[1], 0.17, GLASS, FRAME, broken=DARK)
    for k in range(4):  # trusses and sheets down on the floor
        x = -1.2 + 0.8 * k
        p.bar((x - 0.2, -Y0 + 0.3, APRON + 0.1), (x + 0.25, Y0 - 0.3, APRON + 0.35 + 0.1 * k), 0.04, DARK)
        cc.block(p, (x + 0.1, rnd.uniform(-0.6, 0.6), APRON + 0.12), (0.7, 0.5, 0.03), (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(0, 1)), ROOF)
    ic.rubble(p, rnd, -X0 + 0.2, X0 - 0.2, -Y0 + 0.2, Y0 - 0.2, APRON, 34, 0.24, BRICK, height=0.4)
    p.turned((STACK.x + 0.4, STACK.y - 0.3, APRON + 0.1), 'X', [(-0.6, 0.1), (0.6, 0.09)], METAL, sides=12)  # the stack, fallen


def _material(name, variant):
    """Red-brown brick in courses, a darker plinth and cornice, dark glass, dark steel frames, grey corrugated sheet with rust at its
    edges, dark green gate, dark metal, concrete with stains, sandbags, the light team zone, a warm lamp, wood, red drums, skylight
    glass, a dusty floor; soot when damaged, more when abandoned, the ruins sooty all over."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    brick, joints = ic.brick(g, coord, (0.50, 0.22, 0.15), (0.60, 0.28, 0.19), (0.62, 0.58, 0.52))
    grime = g.clamp01(g.math('MULTIPLY_ADD', z, -0.8, 0.9))  # dark at the foot
    brick = g.mix(g.math('MULTIPLY', grime, 0.5), brick, lin((0.18, 0.10, 0.08)))
    plinth, _ = ic.brick(g, coord, (0.30, 0.14, 0.10), (0.36, 0.18, 0.12), (0.40, 0.37, 0.33))
    glass = g.mix(g.noise(4.0, 2.0), lin((0.10, 0.13, 0.14)), lin((0.22, 0.27, 0.28)))
    frame = g.mix(grain, lin((0.10, 0.11, 0.11)), lin((0.16, 0.17, 0.17)))
    corrugation = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', x, 14.0)), 0.5)), 4.0, -0.6))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 4.0), 5.0, -2.9))
    roof = g.mix(corrugation, lin((0.50, 0.52, 0.53)), lin((0.38, 0.40, 0.41)))
    roof = g.mix(rust, roof, lin((0.42, 0.22, 0.10)))
    gate = g.mix(grain, lin((0.14, 0.24, 0.17)), lin((0.18, 0.30, 0.21)))
    gate = g.mix(g.math('MULTIPLY', rust, 0.6), gate, lin((0.36, 0.20, 0.10)))
    metal = g.mix(grain, lin((0.20, 0.21, 0.22)), lin((0.28, 0.29, 0.30)))
    stain = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 3.0, -1.7))
    concrete = g.mix(grain, lin((0.52, 0.51, 0.48)), lin((0.60, 0.59, 0.56)))
    concrete = g.mix(g.math('MULTIPLY', stain, 0.6), concrete, lin((0.24, 0.23, 0.21)))
    dark = g.mix(grain, lin((0.04, 0.04, 0.04)), lin((0.08, 0.07, 0.07)))
    sand = g.mix(g.noise(20.0, 3.0), lin((0.56, 0.50, 0.36)), lin((0.64, 0.58, 0.42)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    lamp = lin((1.0, 0.86, 0.55))
    wood = g.mix(g.noise(5.0, 5.0), lin((0.50, 0.36, 0.20)), lin((0.60, 0.44, 0.26)))
    drum = g.mix(grain, lin((0.56, 0.14, 0.08)), lin((0.64, 0.18, 0.10)))
    sky = g.mix(g.noise(6.0, 2.0), lin((0.30, 0.38, 0.40)), lin((0.44, 0.52, 0.54)))
    floor = g.mix(g.noise(8.0, 3.0), lin((0.26, 0.24, 0.22)), lin((0.34, 0.31, 0.28)))
    albedo = cc.paint(g, mask, (brick, plinth, glass, frame, roof, gate, metal, concrete, dark, sand, team, lamp, wood, drum, sky, floor))
    amount = {"intact": 0.0, "occupied": 0.0, "damaged": 0.35, "damaged_occupied": 0.35, "abandoned": 0.7, "ruins": 0.85}[variant]
    if amount:
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.85), albedo, lin((0.06, 0.05, 0.05)))
    rough = cc.weighted(g, mask, (0.9, 0.9, 0.15, 0.5, 0.55, 0.6, 0.5, 0.9, 0.9, 0.95, 0.5, 0.3, 0.85, 0.5, 0.15, 0.95))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.6, 0.6, 0.3, 0.6, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.4, 0.0, 0.0))
    height = g.math('MULTIPLY_ADD', g.math('ADD', mask[0], mask[1]), g.math('MULTIPLY', joints, -1.0),
                    g.math('MULTIPLY_ADD', mask[4], corrugation, g.noise(60.0, 3.0)))
    g.finish(albedo, g.bump(height, 0.05, 0.006), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "roof", "team_mask", "base"))
    rnd = ic.seed(variant_id)
    body, roof, team, base = Parts(), Parts(), Parts(), Parts()
    _yard(body, base, variant)
    if variant == "ruins":
        _ruins(body, rnd)
        parts = [(body, "body"), (base, "base")]
    else:
        _walls(body, variant)
        _windows(body, variant)
        _gate(body, team, variant)
        _side_door(body, variant)
        _roof(roof, variant)
        if variant in ("occupied", "damaged_occupied"):
            _occupied(body)
        if variant in ("damaged", "damaged_occupied", "abandoned"):
            _damage(body, rnd, variant)
        if variant == "abandoned":
            _abandoned(body, rnd)
        parts = [(body, "body"), (roof, "roof"), (team, "team_mask"), (base, "base")]
    return cc.finish(variant_id, parts, APRON - 0.001, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    fire = FIRE_POINTS if variant == "abandoned" else ()
    anchors = () if variant in ("abandoned", "ruins") else (("smoke_anchor", (STACK.x, STACK.y, 4.7)),)
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, anchors)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
