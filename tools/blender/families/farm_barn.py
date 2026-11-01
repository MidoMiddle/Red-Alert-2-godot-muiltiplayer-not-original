"""Barn, element 09 of the temperate set (docs/temperate_elements/09_barn): a big wooden barn of dark red boards, a wide gate in front.

The map building neutral_barn (game/data/neutral/civilian.yaml): 3x2, 800 hp, no soldiers go in. Footprint 3x2 cells, origin at the
centre of the footprint on the ground, the front (the gate) to -Y. Made a storey to a cell, as the buildings of the map (the game
squashes it upright, game/data/visuals/models.yaml): the side walls to 1.55, a gambrel roof (a gable broken into a steep lower and a
flatter upper slope) to its ridge at 3.25, the cupola on it to 3.9. Its gable faces the front, so the gate, the loft door over it and
the gambrel's outline are all on the side the camera sees. The parts:
    body  walls of weathered dark red vertical boards, hollow inside (the loft's floor at the eaves), on a concrete plinth; white
          corner boards and white bargeboards along the gambrel's edge; a white frame round the gate, a loft door over it with a
          hoist beam and its pulley under the ridge; small ventilation windows in light frames along both sides and beside the loft door
    roof  the gambrel of grey corrugated metal, overhanging, a ridge cap, a small white cupola with louvres and a weathervane
    gate  the two leaves of the gate: dark brown boards in a frame with an X-brace, on black strap hinges at the outer edges
    base  the concrete plinth under the walls and a concrete apron before the gate
No emblems, no text, no team colour.
Nodes: "body", "roof", "gate", "base"; the burning one also fire_1..3. One GLB per state; the whole one in two kinds, each cell picking
one:
    barn            whole, the gate shut
    barn_open       whole, the gate open: its leaves swung back flat against the front, hay inside on the floor
    barn_damaged    damaged: the right leaf torn off and lying before the gate, the left hanging from one hinge, hay spilled out,
                    a hole torn in the roof's upper right slope, a window broken, soot
    barn_abandoned  burning: charred, the roof's upper slopes burnt through at the back, the right lower slope sagging, black rafters
                    over the gaps, the gate's right leaf gone; fire_1..3 in the roof and the doorway
    barn_ruins      burnt down: black stumps of the walls and the corner posts on the plinth, the lower front frame, the roof's metal
                    sheets fallen in, charred beams, a heap of burnt hay, a leaf on the ground
Budget 8000 triangles, texture 1024 (docs/temperate_elements/09_barn/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.farm_barn as barn
    print(barn.run_all())
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "farm"
SIZE = 1024
VARIANTS = {
    "barn": "intact",
    "barn_open": "open",
    "barn_damaged": "damaged",
    "barn_abandoned": "abandoned",
    "barn_ruins": "ruins",
}

# The farm's row in the file: rts_common has no row for it yet, so it is added here while the script runs.
ROW = "15. Ферма"
SLOT0 = 8


def _register():
    if ROW not in rc.ROWS:
        rc.ROWS = rc.ROWS + (ROW,)
    row = rc.ROWS.index(ROW)
    for k, variant_id in enumerate(VARIANTS):
        rc.LAYOUT.setdefault(variant_id, (row, SLOT0 + k))


_register()

BOARDS, TRIM, ROOF, GATE, METAL, GLASS, CONCRETE, DARK, HAY, WOOD = (cc.part(i) for i in range(10))
PARTS = 10

FOUND_H = 0.14
HX, HY = 1.38, 0.86  # the walls' outer faces
WALL_T = 0.08
EAVE = 1.55
KNEE_X, KNEE_Z = 0.92, 2.6
PEAK = 3.25
ROOF_T = 0.05
ROOF_Y = HY + 0.1  # the roof overhangs the gables to ±ROOF_Y
OVER = 0.08  # and the side walls by this, along the lower slope
GATE_X, GATE_TOP = 0.55, 1.45
LOFT_X, LOFT = 0.28, (1.85, 2.45)
FRONT = cc.Facade(HX, HY)
SIDE_WINDOWS = (-0.45, 0.0, 0.45)
SIDE_Z = (0.95, 1.2)
FRONT_WINDOWS = (-0.72, 0.72)
FRONT_Z = (1.92, 2.16)
# The gambrel's outline over the walls, from the left eave to the right one, in (x, z).
GAMBREL = ((-HX, EAVE), (-KNEE_X, KNEE_Z), (0.0, PEAK), (KNEE_X, KNEE_Z), (HX, EAVE))
FIRE_POINTS = ((0.45, 0.4, 2.9), (-0.4, 0.55, 2.75), (0.15, -HY - 0.02, 0.8))


def _xz_prism(p, outline, y0, y1, part):
    """A convex outline in the XZ plane pushed along Y from y0 to y1."""
    p.loft([[Vector((x, y, z)) for x, z in outline] for y in (y0, y1)], part)


# --- the walls ----------------------------------------------------------------------

def _gable_wall(p, y0, y1, opening):
    """A gable wall from y0 to y1: the gambrel over the walls' height; opening: cut for the gate (the leaves cover it)."""
    if not opening:
        _xz_prism(p, ((-HX, FOUND_H), (HX, FOUND_H)) + GAMBREL[::-1], y0, y1, BOARDS)
        return
    _xz_prism(p, ((-HX, GATE_TOP), (HX, GATE_TOP)) + GAMBREL[::-1], y0, y1, BOARDS)
    p.box((-HX, y0, FOUND_H), (-GATE_X, y1, GATE_TOP), BOARDS)
    p.box((GATE_X, y0, FOUND_H), (HX, y1, GATE_TOP), BOARDS)


def _walls(p, variant):
    """Hollow walls of boards, the loft's floor at the eaves, the barn's floor; trims, the windows, the frame round the gate, the
    loft door and the hoist beam."""
    damaged = variant in ("damaged", "abandoned")
    _gable_wall(p, -HY, -HY + WALL_T, True)
    _gable_wall(p, HY - WALL_T, HY, False)
    p.box((-HX, -HY + WALL_T, FOUND_H), (-HX + WALL_T, HY - WALL_T, EAVE), BOARDS)
    p.box((HX - WALL_T, -HY + WALL_T, FOUND_H), (HX, HY - WALL_T, EAVE), BOARDS)
    p.box((-HX + WALL_T, -HY + WALL_T, EAVE - 0.05), (HX - WALL_T, HY - WALL_T, EAVE), WOOD)  # the loft's floor
    p.box((-HX + WALL_T, -HY + WALL_T, FOUND_H), (HX - WALL_T, HY - WALL_T, FOUND_H + 0.01), WOOD)  # the floor
    for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY)):  # the corner boards
        p.box((x - 0.03, y - 0.03, FOUND_H), (x + 0.03, y + 0.03, EAVE), TRIM)
    # The windows: along both sides low under the eaves, and two beside the loft door; the light frames.
    for side in ("left", "right"):
        for k, u in enumerate(SIDE_WINDOWS):
            broken = damaged and (k == 1 if side == "right" else variant == "abandoned")
            FRONT.window(p, side, u, *SIDE_Z, 0.1, GLASS, TRIM, bars=(1, 0), broken=DARK if broken else None)
    for u in FRONT_WINDOWS:
        FRONT.window(p, "front", u, *FRONT_Z, 0.1, GLASS, TRIM, bars=(1, 0), broken=DARK if variant == "abandoned" else None)
    # The white frame round the gate, its lintel; the loft door over it, framed, with its X-brace.
    for a in (-GATE_X - 0.06, GATE_X):
        FRONT.box(p, "front", a, a + 0.06, FOUND_H, GATE_TOP + 0.06, -0.01, 0.03, TRIM)
    FRONT.box(p, "front", -GATE_X - 0.06, GATE_X + 0.06, GATE_TOP, GATE_TOP + 0.06, -0.01, 0.03, TRIM)
    z0, z1 = LOFT
    FRONT.box(p, "front", -LOFT_X, LOFT_X, z0, z1, -0.01, 0.015, DARK if variant == "abandoned" else GATE)
    if variant != "abandoned":
        for a, b in (((-LOFT_X + 0.03, z0 + 0.03), (LOFT_X - 0.03, z1 - 0.03)), ((-LOFT_X + 0.03, z1 - 0.03), (LOFT_X - 0.03, z0 + 0.03))):
            p.bar((a[0], -HY - 0.025, a[1]), (b[0], -HY - 0.025, b[1]), 0.016, GATE)
    for a in (-LOFT_X - 0.05, LOFT_X):
        FRONT.box(p, "front", a, a + 0.05, z0 - 0.03, z1 + 0.05, -0.01, 0.03, TRIM)
    FRONT.box(p, "front", -LOFT_X - 0.05, LOFT_X + 0.05, z1, z1 + 0.05, -0.01, 0.03, TRIM)
    FRONT.box(p, "front", -LOFT_X - 0.07, LOFT_X + 0.07, z0 - 0.05, z0, -0.01, 0.04, TRIM)
    # The hoist beam out of the gable under the ridge, its pulley.
    p.box((-0.035, -ROOF_Y + 0.01, 2.86), (0.035, -HY, 2.93), WOOD)
    p.box((-0.03, -ROOF_Y + 0.02, 2.74), (0.03, -ROOF_Y + 0.07, 2.86), METAL)


def _bargeboards(p, y0, y1):
    """White boards along the gambrel's edge at a gable end, just under the roof's edge."""
    for a, b in zip(GAMBREL, GAMBREL[1:]):
        _slab(p, a, b, y0, y1, -0.09, TRIM, 0.0, 0.0)


# --- the roof -------------------------------------------------------------------------

def _slab(p, a, b, y0, y1, t, part, out_a, out_b):
    """A slab on the roof line from a to b (x, z), t thick away from the barn (less than 0: under the line), pushed along Y from y0
    to y1; out_a and out_b stretch it past its ends along the line."""
    a, b = Vector((a[0], 0.0, a[1])), Vector((b[0], 0.0, b[1]))
    d = (b - a).normalized()
    n = Vector((-d.z, 0.0, d.x))  # the line's normal, to the left of a→b: up and out for the gambrel taken left to right
    if n.z < 0:
        n = -n
    a, b = a - d * out_a, b + d * out_b
    quad = [a, b, b + n * t, a + n * t] if t > 0 else [a + n * t, b + n * t, b, a]
    p.loft([[Vector((v.x, y, v.z)) for v in quad] for y in (y0, y1)], part)


def _roof(p, variant):
    """The four slopes of the gambrel, a ridge cap; the cupola and its weathervane. Damaged: a hole in the upper right slope, torn
    sheets hanging into it; burning: the upper slopes burnt through at the back, the right lower one sagging."""
    left_lo, left_up, right_up, right_lo = zip(GAMBREL, GAMBREL[1:])
    y0, y1 = -ROOF_Y, ROOF_Y
    if variant == "abandoned":
        _slab(p, *left_lo, y0, y1, ROOF_T, ROOF, OVER, 0.04)
        _slab(p, *left_up, y0, 0.25, ROOF_T, ROOF, 0.04, 0.02)
        _slab(p, *left_up, 0.7, y1, ROOF_T, ROOF, 0.04, 0.02)
        _slab(p, *right_up, y0, -0.15, ROOF_T, ROOF, 0.02, 0.04)
        before = set(p.bm.verts)
        _slab(p, *right_lo, y0, y1, ROOF_T, ROOF, 0.04, OVER)
        for v in p.bm.verts:
            if v not in before and v.co.y > -0.3:
                t = min(1.0, (v.co.y + 0.3) / 0.9)
                v.co.z -= 0.35 * t * max(0.0, (HX - v.co.x) / (HX - KNEE_X))
        p.box((-0.04, y0, PEAK + ROOF_T - 0.03), (0.04, -0.15, PEAK + ROOF_T + 0.03), ROOF)
        return
    _slab(p, *left_lo, y0, y1, ROOF_T, ROOF, OVER, 0.04)
    _slab(p, *left_up, y0, y1, ROOF_T, ROOF, 0.04, 0.02)
    _slab(p, *right_lo, y0, y1, ROOF_T, ROOF, 0.04, OVER)
    if variant == "damaged":
        _slab(p, *right_up, y0, -0.2, ROOF_T, ROOF, 0.02, 0.04)
        _slab(p, *right_up, 0.38, y1, ROOF_T, ROOF, 0.02, 0.04)
        # Torn sheets bent down into the hole.
        (ax, az), (bx, bz) = right_up
        for y, k in ((-0.15, 0.0), (0.3, 1.0)):
            before = set(p.bm.verts)
            _slab(p, (ax + (bx - ax) * 0.25, az + (bz - az) * 0.25), (ax + (bx - ax) * 0.75, az + (bz - az) * 0.75), y - 0.09, y + 0.05, 0.02, ROOF, 0.0, 0.0)
            pivot = Vector((ax + (bx - ax) * 0.5, y - 0.05 if k else y + 0.05, az + (bz - az) * 0.5))
            m = Matrix.Translation(pivot) @ Matrix.Rotation(0.9 if k else -0.9, 4, 'X') @ Matrix.Translation(-pivot)
            for v in p.bm.verts:
                if v not in before:
                    v.co = m @ v.co
    else:
        _slab(p, *right_up, y0, y1, ROOF_T, ROOF, 0.02, 0.04)
    p.box((-0.04, y0, PEAK + ROOF_T - 0.03), (0.04, y1, PEAK + ROOF_T + 0.03), ROOF)  # the ridge cap
    # The cupola on the ridge: white boards, dark louvres on its sides, a small pyramid roof, the weathervane.
    c = PEAK + ROOF_T
    p.box((-0.15, -0.15, c - 0.12), (0.15, 0.15, c + 0.3), TRIM)
    for wall in ("front", "back", "left", "right"):
        cc.Facade(0.15, 0.15).box(p, wall, -0.09, 0.09, c + 0.05, c + 0.25, -0.01, 0.005, DARK)
    p.loft([[Vector((x, y, c + 0.3)) for x, y in ((-0.2, -0.2), (0.2, -0.2), (0.2, 0.2), (-0.2, 0.2))],
            [Vector((x, y, c + 0.5)) for x, y in ((-0.02, -0.02), (0.02, -0.02), (0.02, 0.02), (-0.02, 0.02))]], ROOF)
    p.bar((0.0, 0.0, c + 0.48), (0.0, 0.0, c + 0.68), 0.008, METAL)
    p.box((-0.12, -0.008, c + 0.6), (0.1, 0.008, c + 0.625), METAL)  # the vane's arrow
    p.box((0.06, -0.006, c + 0.585), (0.12, 0.006, c + 0.64), METAL)  # its tail


def _rafters(p):
    """The burning one's black rafters over the burnt-through slopes, and hay in the loft under them."""
    for y in (0.4, 0.62, 0.84):
        p.bar((-KNEE_X, y, KNEE_Z + 0.02), (0.0, y, PEAK - 0.02), 0.025, DARK)
    for y in (-0.05, 0.25, 0.55):
        p.bar((0.0, y, PEAK - 0.02), (KNEE_X, y, KNEE_Z - 0.2 * (y > 0.2)), 0.025, DARK)
    p.bar((0.0, -0.15, PEAK - 0.02), (0.05, 0.6, PEAK - 0.45), 0.03, DARK)  # the ridge beam, broken and sagging


def _hay_loft(p, rnd, count=10):
    """Loose hay in the loft, seen through the holes of the roof."""
    ic.rubble(p, rnd, -0.7, 0.8, -0.2, 0.7, EAVE, count, 0.3, HAY, 0.12)


# --- the gate -----------------------------------------------------------------------

def _leaf(p, x0, x1, transform=None):
    """One leaf: boards in a frame with an X-brace, strap hinges at its outer edge; transform: where it hangs or lies."""
    before = set(p.bm.verts)
    yf = -HY - 0.035
    p.box((x0, yf, FOUND_H + 0.02), (x1, -HY - 0.005, GATE_TOP - 0.01), GATE)
    zb, zt = FOUND_H + 0.06, GATE_TOP - 0.05
    xa, xb = x0 + 0.04, x1 - 0.04
    for (ua, za), (ub, zb_) in (((xa, zb), (xb, zb)), ((xa, zt), (xb, zt)), ((xa, (zb + zt) / 2), (xb, (zb + zt) / 2)), ((xa, zb), (xa, zt)),
                                ((xb, zb), (xb, zt)), ((xa, zb), (xb, zt)), ((xa, zt), (xb, zb))):
        p.bar((ua, yf - 0.012, za), (ub, yf - 0.012, zb_), 0.022, GATE)
    outer = x0 if abs(x0) > abs(x1) else x1
    for z in (FOUND_H + 0.22, (FOUND_H + GATE_TOP) / 2, GATE_TOP - 0.22):
        a, b = (outer, outer + 0.24) if outer < 0 else (outer - 0.24, outer)
        p.box((a, yf - 0.04, z - 0.025), (b, yf - 0.03, z + 0.025), METAL)
    if transform is not None:
        for v in p.bm.verts:
            if v not in before:
                v.co = transform @ v.co


def _gate(p, variant):
    left, right = (-GATE_X, -0.005), (0.005, GATE_X)
    if variant == "intact":
        _leaf(p, *left)
        _leaf(p, *right)
        return
    if variant == "open":
        # Swung back flat against the front, beside the opening, each about its hinge.
        for (x0, x1), hinge_x in ((left, -GATE_X), (right, GATE_X)):
            hinge = Vector((hinge_x, -HY - 0.045, 0.0))
            _leaf(p, x0, x1, Matrix.Translation(hinge) @ Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Translation(-hinge))
        return
    if variant == "damaged":
        # The left hangs from its lower hinge, askew; the right is torn off and propped against the front beside the gate.
        hinge = Vector((-GATE_X, -HY - 0.03, FOUND_H + 0.2))
        hang = Matrix.Translation(hinge) @ Matrix.Rotation(0.5, 4, 'Z') @ Matrix.Rotation(-0.12, 4, 'Y') @ Matrix.Translation(-hinge)
        _leaf(p, *left, hang)
        foot = Vector((0.88, -HY - 0.035, FOUND_H + 0.02))
        propped = (Matrix.Translation((0.0, -0.03, -FOUND_H + 0.03)) @ Matrix.Translation(foot) @ Matrix.Rotation(-0.03, 4, 'X')
                   @ Matrix.Rotation(-0.18, 4, 'Y') @ Matrix.Translation(-foot) @ Matrix.Translation((0.88 - 0.28, 0.0, 0.0)))
        _leaf(p, *right, propped)
        return
    if variant == "abandoned":
        hinge = Vector((-GATE_X, -HY - 0.03, 0.0))
        _leaf(p, *left, Matrix.Translation(hinge) @ Matrix.Rotation(1.1, 4, 'Z') @ Matrix.Translation(-hinge))
        return
    # Ruins: one leaf fallen flat inside the gate, on the floor among the rubble, charred.
    lying = (Matrix.Translation((-0.25, -0.5, FOUND_H + 0.06)) @ Matrix.Rotation(-1.35, 4, 'Z') @ Matrix.Rotation(1.5, 4, 'X')
             @ Matrix.Translation((-0.28, HY + 0.02, -(FOUND_H + GATE_TOP) / 2)))
    _leaf(p, *right, lying)


def _hay_floor(p, rnd, spill):
    """Hay on the barn's floor inside the gate; spill: some of it out before the gate."""
    ic.rubble(p, rnd, -0.6, 0.6, -HY + 0.2, 0.2, FOUND_H, 9, 0.3, HAY, 0.15)
    if spill:
        ic.rubble(p, rnd, -0.45, 0.2, -HY - 0.02, -HY + 0.15, FOUND_H - 0.04, 5, 0.14, HAY)


# --- the ruins -------------------------------------------------------------------------

def _ruins(p):
    rnd = ic.seed("barn_ruins")
    walls = [((HX, -HY), (HX, HY)), ((HX, HY), (-HX, HY)), ((-HX, HY), (-HX, -HY))]
    ic.stumps(p, rnd, walls, FOUND_H, 0.15, 0.75, BOARDS, step=0.26, gap=0.25, thick=WALL_T)
    ic.stumps(p, rnd, [((-HX, -HY), (-GATE_X - 0.06, -HY)), ((GATE_X + 0.06, -HY), (HX, -HY))], FOUND_H, 0.3, 1.0, BOARDS, step=0.22,
              gap=0.1, thick=WALL_T)
    for x, y, h in ((-HX, -HY, 1.25), (HX, -HY, 0.8), (HX, HY, 1.0), (-HX, HY, 0.6), (-GATE_X - 0.03, -HY, 1.3), (GATE_X + 0.03, -HY, 0.95)):
        p.box((x - 0.04, y - 0.04, FOUND_H), (x + 0.04, y + 0.04, FOUND_H + h), DARK)  # the posts, charred
    p.bar((-GATE_X - 0.03, -HY, FOUND_H + 1.3), (GATE_X + 0.2, -HY - 0.02, FOUND_H + 0.7), 0.035, DARK)  # the lintel, fallen at one end
    # The roof's sheets fallen in across the stumps; charred beams; burnt hay.
    for c, size, turn in (((-0.55, 0.15, 0.42), (1.2, 1.4, 0.03), (0.0, 0.32, 0.12)), ((0.6, 0.2, 0.36), (1.1, 1.3, 0.03), (0.05, -0.38, -0.1)),
                          ((0.05, -0.35, 0.3), (0.9, 0.7, 0.03), (0.3, 0.1, 0.5)), ((-0.85, -0.72, 0.18), (0.6, 0.12, 0.02), (0.0, 0.05, 0.3))):
        cc.block(p, c, size, turn, ROOF)
    for a, b in (((-1.1, -0.5, 0.2), (0.4, 0.6, 0.55)), ((0.9, -0.6, 0.18), (-0.2, 0.7, 0.5)), ((-0.3, 0.7, 0.2), (1.1, 0.4, 0.75))):
        p.bar(a, b, 0.04, DARK)
    ic.rubble(p, rnd, -0.9, 0.9, -0.5, 0.6, FOUND_H, 12, 0.22, DARK, 0.1)
    ic.rubble(p, rnd, -0.5, 0.4, -0.4, 0.3, FOUND_H, 5, 0.3, HAY, 0.05)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Weathered dark red vertical boards, white trims, grey corrugated metal, dark brown gate boards, black iron, dusty glass,
    concrete, dark openings, straw, bare wood; soot per state, charred when burnt down."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 4.0)
    joints = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 11.0)), -14.0, 1.6))
    red = g.mix(g.noise(4.0, 4.0), lin((0.46, 0.12, 0.09)), lin((0.58, 0.17, 0.12)))
    faded = g.clamp01(g.math('MULTIPLY_ADD', g.noise(7.0, 4.0), 6.0, -3.4))
    red = g.mix(faded, red, lin((0.62, 0.34, 0.28)))  # the paint faded and worn to pink-grey in places
    streaks = g.clamp01(g.math('MULTIPLY_ADD', g.noise(40.0, 2.0), 3.0, -1.4))
    red = g.mix(g.math('MULTIPLY', streaks, 0.35), red, lin((0.25, 0.07, 0.05)))
    boards = g.mix(joints, red, lin((0.14, 0.05, 0.04)))
    trim = g.mix(grain, lin((0.84, 0.83, 0.78)), lin((0.92, 0.91, 0.87)))
    ribs = g.math('ABSOLUTE', g.math('SINE', g.math('MULTIPLY', y, 90.0)))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 4.0), 5.0, -3.0))
    metal_sheet = g.mix(ribs, lin((0.44, 0.46, 0.48)), lin((0.62, 0.63, 0.64)))
    roof = g.mix(g.math('MULTIPLY', rust, 0.6), metal_sheet, lin((0.46, 0.30, 0.20)))
    gate = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 14.0)), -14.0, 1.6)),
                 g.mix(g.noise(5.0, 4.0), lin((0.28, 0.18, 0.11)), lin((0.36, 0.24, 0.15))), lin((0.12, 0.08, 0.05)))
    metal = g.mix(grain, lin((0.08, 0.08, 0.08)), lin((0.16, 0.15, 0.14)))
    glass = g.mix(g.noise(8.0, 3.0), lin((0.30, 0.34, 0.35)), lin((0.48, 0.53, 0.54)))
    concrete = g.mix(grain, lin((0.56, 0.55, 0.52)), lin((0.64, 0.63, 0.60)))
    dark = g.mix(grain, lin((0.04, 0.04, 0.04)), lin((0.09, 0.08, 0.07)))
    hay = g.mix(g.noise(45.0, 6.0), lin((0.72, 0.58, 0.28)), lin((0.90, 0.78, 0.42)))
    wood = g.mix(g.noise(4.0, 5.0), lin((0.40, 0.33, 0.25)), lin((0.50, 0.42, 0.32)))
    albedo = cc.paint(g, mask, (boards, trim, roof, gate, metal, glass, concrete, dark, hay, wood))
    if variant in ("damaged", "abandoned"):
        soot = ic.soot(g, 0.9 if variant == "abandoned" else -0.3)
        # Soot licking up from the openings: over the gate, the loft door and the hole in the roof.
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.06, 0.05, 0.05)))
    if variant == "ruins":
        charred = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 2.0), 3.0, -0.8)), 0.6),
                        g.mix(0.4, albedo, lin((0.16, 0.14, 0.12))), lin((0.07, 0.06, 0.06)))
        charred = g.mix(g.math('MULTIPLY', mask[0], 0.55), charred, lin((0.10, 0.07, 0.06)))  # the boards burnt black
        albedo = g.mix(g.math('MULTIPLY', mask[6], 0.7), charred, albedo)  # the concrete plinth only sooted
    rough = cc.weighted(g, mask, (0.85, 0.65, 0.5, 0.8, 0.5, 0.2, 0.9, 0.95, 0.95, 0.85))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.55, 0.0, 0.6, 0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('ADD', g.math('MULTIPLY', joints, g.math('MULTIPLY', mask[0], -0.6)),
                           g.math('ADD', g.math('MULTIPLY', ribs, g.math('MULTIPLY', mask[2], 0.8)), g.math('MULTIPLY', grain, 0.25))), 0.06, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _base(p):
    p.box((-HX - 0.05, -HY - 0.05, 0.0), (HX + 0.05, HY + 0.05, FOUND_H), CONCRETE)
    p.loft([[Vector((x, y, z)) for x, y, z in ((-0.68, -HY - 0.05, 0.0), (0.68, -HY - 0.05, 0.0), (0.68, -HY - 0.05, FOUND_H - 0.02),
                                                (-0.68, -HY - 0.05, FOUND_H - 0.02))],
            [Vector((x, y, z)) for x, y, z in ((-0.68, -0.99, 0.0), (0.68, -0.99, 0.0), (0.68, -0.99, 0.02), (-0.68, -0.99, 0.02))]], CONCRETE)


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "roof", "gate", "base"))
    rnd = ic.seed(variant_id)
    body, roof, gate, base = Parts(), Parts(), Parts(), Parts()
    parts = [(body, "body")]
    if variant == "ruins":
        _ruins(body)
    else:
        _walls(body, variant)
        _bargeboards(body, -ROOF_Y + 0.005, -ROOF_Y + 0.035)
        _bargeboards(body, ROOF_Y - 0.035, ROOF_Y - 0.005)
        if variant != "intact":
            _hay_floor(body, rnd, variant == "damaged")
        if variant in ("damaged", "abandoned"):
            _hay_loft(body, rnd)
        if variant == "abandoned":
            _rafters(body)
        _roof(roof, variant)
        parts.append((roof, "roof"))
    _gate(gate, variant)
    _base(base)
    parts += [(gate, "gate"), (base, "base")]
    return cc.finish(variant_id, parts, FOUND_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, gate and base (fire_1..3 when burning)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
