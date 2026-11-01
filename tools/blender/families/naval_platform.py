"""Offshore oil platform, element 04 of the naval set (docs/naval_elements/04_offshore_platform): the oil derrick's tech building at sea;
an engineer takes it from the water and its owner gets the derrick's income.

Footprint 2x2 cells, origin at the centre of the footprint on the water's surface (what lies below 0 is hidden by the water), the front
to -Y. Made at the size the game draws it, as the tech oil derrick (families/tech_derrick.py): the deck at 0.6, the derrick to 2.1. The
parts:
    legs       four thick white legs from under the water to the deck, a rusty band at the waterline, X braces between them, a riser
               pipe down one of them
    deck       a square deck over the legs on a truss of white girders, its plate dark grey with a yellow edge, a railing round it
               (the owner's colour on its top rail)
    derrick    a lattice derrick of grey steel on its drill floor at the back left, tapering up to its crown block and a red light
    pump       a small pumpjack at the right: its Samson post and the walking beam with the horse head over the wellhead
    quarters   a white block of two storeys at the front right with a band of windows on each, an orange lifeboat on davits at its side
    crane      a yellow pedestal crane at the front left, its lattice boom out over the sea, the hook on its cable
    flare      a lattice flare boom out of the back right corner over the sea, its tip dark (the flame is the particles')
    yard       two horizontal tanks at the back, pipes along the deck, a red light on each corner
    helipad    a yellow ring with a white H in the middle of the deck: a transport helicopter sets an engineer down there to take the
               platform (docs/naval_elements/04_offshore_platform)
Nodes: "body", "derrick", "beam" (the walking beam and horse head; its origin on the axis it nods about, as the derrick's beam: the game
nods it about its own z, Blender's y), "team_mask" (the top rail), "flare_anchor" (the flare's tip, for the flame). One GLB per state:
    offshore_platform            whole, working: the beam nods, the flare burns
    offshore_platform_abandoned  critically damaged and burning: paint gone in patches and soot, the railing torn away in places, the
                                 crane's boom down across the deck, the lifeboat gone, the beam dropped head down, the derrick bent at
                                 two thirds, holes in the deck; fire at the pump, the quarters and the derrick's foot (fire_1..3)
    offshore_platform_ruins      destroyed: the top gone, the four legs broken off at different heights over the water, pieces of the
                                 deck tilted into the sea, the derrick lying across two of the stumps
Budget 8000 triangles, texture 2048 (docs/naval_elements/04_offshore_platform/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.naval_platform as op
    print(op.run_all())
"""

import math

from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from families.infrastructure_gate import transformed
from families.naval_reef import FOLDER
from units.parts import Parts, lin
import rts_common as rc

SIZE = 2048
VARIANTS = {
    "offshore_platform": "intact",
    "offshore_platform_abandoned": "abandoned",
    "offshore_platform_ruins": "ruins",
}

(WHITE, STEEL, DECK, YELLOW, DARK, RED, ORANGE, GLASS, TEAM) = (cc.part(i) for i in range(9))
PARTS = 9

UNDER = -0.15
LEG = 0.62  # the legs' centres at (±LEG, ±LEG)
LEG_R = 0.1
DECK_Z = 0.6  # the deck's top
TRUSS = 0.14  # the depth of the girders under the deck
EDGE = 0.86  # the deck's half-width
RAIL = 0.1  # the railing's height
DERRICK = (-0.42, 0.42)  # the derrick's centre
DERRICK_TOP = 2.1
PUMP = (0.42, 0.25)
PIVOT_Z = DECK_Z + 0.26  # the walking beam's axis
HELIPAD = (0.0, -0.08)  # the landing mark in the middle of the deck
FLARE_ROOT = Vector((0.8, 0.8, DECK_Z + 0.05))
FLARE_TIP = Vector((1.22, 1.22, 1.1))
FIRE_POINTS = ((PUMP[0], PUMP[1], DECK_Z + 0.15), (0.45, -0.55, DECK_Z + 0.45), (DERRICK[0], DERRICK[1], DECK_Z + 0.2))


# --- lattice ----------------------------------------------------------------------------

def lattice(p, centre, z0, z1, half0, half1, levels, painted, bar=0.012, bend=None):
    """A square lattice tower: four corner legs from half0 at z0 to half1 at z1, a ring of bars and X braces on each face at every
    level. bend: (height, matrix) — the tower above that height turned by the matrix (a derrick bent by the fire)."""
    cx, cy = centre

    def corner(i, t):
        sx, sy = ((-1, -1), (1, -1), (1, 1), (-1, 1))[i]
        h = half0 + (half1 - half0) * t
        q = Vector((cx + sx * h, cy + sy * h, z0 + (z1 - z0) * t))
        if bend is not None and q.z > bend[0]:
            q = bend[1] @ q
        return q

    steps = [k / levels for k in range(levels + 1)]
    for i in range(4):
        for a, b in zip(steps, steps[1:]):
            p.bar(corner(i, a), corner(i, b), bar * 1.4, painted)
    for k, (a, b) in enumerate(zip(steps, steps[1:])):
        for i in range(4):
            j = (i + 1) % 4
            p.bar(corner(i, b), corner(j, b), bar, painted)
            p.bar(corner(i, a), corner(j, b), bar * 0.8, painted)
            p.bar(corner(j, a), corner(i, b), bar * 0.8, painted)


# --- parts --------------------------------------------------------------------------------

def _legs(p, tops):
    """The four legs up to their tops (the deck, or the stumps of the ruins), X braces between them where both still stand."""
    corners = ((-LEG, -LEG), (LEG, -LEG), (LEG, LEG), (-LEG, LEG))
    for (x, y), top in zip(corners, tops):
        p.cylinder((x, y, (UNDER + top) / 2), 'Z', LEG_R, (top - UNDER) / 2, WHITE, sides=12)
        p.cylinder((x, y, 0.04), 'Z', LEG_R + 0.015, 0.06, WHITE, sides=12)  # the collar at the waterline
    for i in range(4):
        j = (i + 1) % 4
        (ax, ay), (bx, by) = corners[i], corners[j]
        low, high = 0.08, min(tops[i], tops[j], DECK_Z - TRUSS) - 0.04
        if high - low < 0.15:
            continue
        for (z0, z1) in ((low, high), (high, low)):
            p.bar((ax, ay, z0), (bx, by, z1), 0.018, WHITE)
        p.bar((ax, ay, low), (bx, by, low), 0.018, WHITE)


def _deck(p, team, variant):
    z0 = DECK_Z - TRUSS
    # The truss of girders: round the edge and across, the plate on top.
    for s in (-1, 1):
        p.box((-EDGE, s * EDGE - 0.04, z0), (EDGE, s * EDGE + 0.04, DECK_Z - 0.03), WHITE)
        p.box((s * EDGE - 0.04, -EDGE, z0), (s * EDGE + 0.04, EDGE, DECK_Z - 0.03), WHITE)
        p.box((-EDGE, s * LEG - 0.035, z0 + 0.03), (EDGE, s * LEG + 0.035, DECK_Z - 0.03), WHITE)
        p.box((s * LEG - 0.035, -EDGE, z0 + 0.03), (s * LEG + 0.035, EDGE, DECK_Z - 0.03), WHITE)
    for k in range(-3, 4):  # diagonals of the edge girders, seen from the sea
        x = k * 0.24
        for s in (-1, 1):
            p.bar((x - 0.1, s * (EDGE + 0.045), z0 + 0.01), (x + 0.1, s * (EDGE + 0.045), DECK_Z - 0.04), 0.008, WHITE)
            p.bar((s * (EDGE + 0.045), x - 0.1, z0 + 0.01), (s * (EDGE + 0.045), x + 0.1, DECK_Z - 0.04), 0.008, WHITE)
    p.box((-EDGE, -EDGE, DECK_Z - 0.03), (EDGE, EDGE, DECK_Z), DECK)
    # The railing: posts, a mid rail, the top rail in the owner's colour; torn away in places when abandoned.
    gaps = ((-0.5, -0.1), (0.3, 0.7)) if variant == "abandoned" else ()
    for side in range(4):
        for k in range(9):
            t = -EDGE + 0.02 + (2 * EDGE - 0.04) * k / 8
            if any(a < t < b for a, b in gaps) and side % 2 == 0:
                continue
            x, y = [(t, -EDGE + 0.02), (EDGE - 0.02, t), (t, EDGE - 0.02), (-EDGE + 0.02, t)][side]
            p.box((x - 0.008, y - 0.008, DECK_Z), (x + 0.008, y + 0.008, DECK_Z + RAIL), YELLOW)
        segments = [(-EDGE, EDGE)]
        if side % 2 == 0 and gaps:
            segments = [(-EDGE, gaps[0][0]), (gaps[0][1], gaps[1][0]), (gaps[1][1], EDGE)]
        for a, b in segments:
            for z, owner, half in ((DECK_Z + RAIL * 0.5, p, 0.005), (DECK_Z + RAIL, team, 0.011)):
                if side % 2 == 0:
                    y = -EDGE + 0.02 if side == 0 else EDGE - 0.02
                    owner.box((a, y - half, z - half), (b, y + half, z + half), YELLOW if owner is p else TEAM)
                else:
                    x = EDGE - 0.02 if side == 1 else -EDGE + 0.02
                    owner.box((x - half, a, z - half), (x + half, b, z + half), YELLOW if owner is p else TEAM)


def _derrick(p, variant):
    cx, cy = DERRICK
    p.box((cx - 0.26, cy - 0.26, DECK_Z), (cx + 0.26, cy + 0.26, DECK_Z + 0.12), STEEL)  # the drill floor
    p.box((cx - 0.3, cy - 0.3, DECK_Z + 0.12), (cx + 0.3, cy + 0.3, DECK_Z + 0.14), DECK)
    bend = None
    if variant == "abandoned":
        h = DECK_Z + 0.14 + (DERRICK_TOP - DECK_Z) * 0.62
        bend = (h, Matrix.Translation((cx, cy, h)) @ Matrix.Rotation(-0.45, 4, 'X') @ Matrix.Rotation(0.2, 4, 'Y') @ Matrix.Translation((-cx, -cy, -h)))
    lattice(p, DERRICK, DECK_Z + 0.14, DERRICK_TOP - 0.1, 0.2, 0.06, 7, STEEL, bend=bend)
    top = Vector((cx, cy, DERRICK_TOP - 0.1))
    if bend is not None:
        top = bend[1] @ top
    p.box(top - Vector((0.08, 0.08, 0.0)), top + Vector((0.08, 0.08, 0.08)), DARK)  # the crown block
    p.sphere(top + Vector((0.0, 0.0, 0.12)), 'Z', 0.025, RED if variant == "intact" else DARK, sides=8, rings=4)


def _pump(p, beam, variant):
    """The pumpjack's post and wellhead into p, the beam with its horse head into beam (its origin the pivot)."""
    x, y = PUMP
    p.box((x - 0.24, y - 0.1, DECK_Z), (x + 0.24, y + 0.1, DECK_Z + 0.03), DARK)  # the skid
    for s in (-1, 1):
        p.bar((x - 0.08, y + s * 0.07, DECK_Z + 0.03), (x, y + s * 0.02, PIVOT_Z), 0.012, YELLOW)
        p.bar((x + 0.08, y + s * 0.07, DECK_Z + 0.03), (x, y + s * 0.02, PIVOT_Z), 0.012, YELLOW)
    p.box((x + 0.1, y - 0.05, DECK_Z + 0.03), (x + 0.2, y + 0.05, DECK_Z + 0.12), DARK)  # the gearbox
    p.cylinder((x - 0.2, y, DECK_Z + 0.06), 'Z', 0.03, 0.03, DARK, sides=8)  # the wellhead
    p.bar((x - 0.2, y, DECK_Z + 0.09), (x - 0.2, y, DECK_Z + 0.16), 0.006, STEEL)
    # The beam in its own frame, round the pivot: the horse head over the well at -x; dropped head down when abandoned.
    def walking_beam():
        beam.box((-0.2, -0.015, -0.02), (0.16, 0.015, 0.02), YELLOW)
        beam.profile_x([(-0.03, -0.09), (0.03, -0.09), (0.03, 0.05), (-0.03, 0.05)], -0.25, -0.2, DARK)
        beam.box((0.12, -0.03, -0.1), (0.16, 0.03, -0.02), DARK)  # the pitman arm
    transformed(beam, Matrix.Rotation(0.45, 4, 'Y') if variant == "abandoned" else Matrix.Identity(4), walking_beam)


def _quarters(p, variant):
    x0, x1, y0, y1 = 0.18, 0.78, -0.8, -0.32
    for k, (z0, z1) in enumerate(((DECK_Z, DECK_Z + 0.22), (DECK_Z + 0.22, DECK_Z + 0.44))):
        inset = 0.03 * k
        p.box((x0 + inset, y0 + inset, z0), (x1 - inset, y1, z1), WHITE)
        glass = DARK if variant == "abandoned" and k == 1 else GLASS
        p.box((x0 + 0.05 + inset, y0 + inset - 0.006, z0 + 0.08), (x1 - 0.05 - inset, y0 + inset, z0 + 0.15), glass)
        p.box((x1 - inset, y0 + 0.06 + inset, z0 + 0.08), (x1 - inset + 0.006, y1 - 0.06, z0 + 0.15), glass)
    p.box((x0 + 0.02, y0 + 0.02, DECK_Z + 0.44), (x1 - 0.02, y1 - 0.01, DECK_Z + 0.47), DECK)  # the roof
    p.bar((x0 + 0.1, y1 - 0.08, DECK_Z + 0.47), (x0 + 0.1, y1 - 0.08, DECK_Z + 0.75), 0.006, STEEL)  # an aerial
    if variant == "intact":
        # The lifeboat on its davits at the right side.
        p.turned((EDGE + 0.06, (y0 + y1) / 2, DECK_Z + 0.2), 'Y', [(-0.2, 0.0), (-0.17, 0.05), (0.15, 0.055), (0.2, 0.0)], ORANGE, sides=10)
        for dy in (-0.12, 0.12):
            p.bar((EDGE - 0.02, (y0 + y1) / 2 + dy, DECK_Z), (EDGE + 0.06, (y0 + y1) / 2 + dy, DECK_Z + 0.3), 0.008, WHITE)


def _crane(p, variant):
    x, y = -0.55, -0.55
    p.cylinder((x, y, DECK_Z + 0.12), 'Z', 0.06, 0.12, YELLOW, sides=10)
    p.box((x - 0.07, y - 0.06, DECK_Z + 0.24), (x + 0.07, y + 0.09, DECK_Z + 0.36), YELLOW)  # the cab
    p.box((x - 0.075, y - 0.065, DECK_Z + 0.29), (x + 0.075, y - 0.05, DECK_Z + 0.34), GLASS)
    root = Vector((x, y - 0.02, DECK_Z + 0.3))
    if variant == "abandoned":
        tip = Vector((x + 0.75, y + 0.35, DECK_Z + 0.05))  # down across the deck
    else:
        tip = Vector((x - 0.55, y - 0.55, DECK_Z + 0.75))  # out over the sea
    side = (tip - root).cross(Vector((0, 0, 1))).normalized() * 0.04
    for s in (-1, 1):
        p.bar(root + side * s, tip, 0.01, YELLOW)
    for k in range(1, 6):
        a = root + (tip - root) * (k / 6)
        b = root + (tip - root) * ((k + 0.5) / 6)
        p.bar(a + side * (1 - k / 6), b - side * (1 - k / 6), 0.006, YELLOW)
    if variant != "abandoned":
        p.bar(tip, tip - Vector((0, 0, 0.45)), 0.003, DARK)
        p.box(tip - Vector((0.015, 0.015, 0.5)), tip - Vector((-0.015, -0.015, 0.45)), RED)


def _flare(p, variant):
    side = Vector((-1.0, 1.0, 0.0)).normalized() * 0.05
    for s in (-1, 1):
        p.bar(FLARE_ROOT + side * s, FLARE_TIP, 0.01, STEEL)
    p.bar(FLARE_ROOT + Vector((0, 0, 0.12)), FLARE_TIP, 0.008, STEEL)
    for k in range(1, 5):
        a = FLARE_ROOT + (FLARE_TIP - FLARE_ROOT) * (k / 5)
        p.bar(a + side * (1 - k / 5), a - side * (1 - k / 5) + Vector((0, 0, 0.1 * (1 - k / 5))), 0.005, STEEL)
    p.cylinder(FLARE_TIP + Vector((0, 0, 0.04)), 'Z', 0.025, 0.05, DARK, sides=8)


def _yard(p, variant):
    for k, y in enumerate((0.55, 0.72)):
        if variant == "abandoned" and k == 1:
            cc.block(p, (0.2, 0.62, DECK_Z + 0.06), (0.3, 0.12, 0.1), (0.0, 0.3, 0.5), STEEL)  # knocked off its saddles
            continue
        p.turned((0.05, y, DECK_Z + 0.08), 'X', [(-0.0, 0.0), (0.0, 0.06), (0.3, 0.06), (0.3, 0.0)], STEEL, sides=10)
    for z in (DECK_Z + 0.03, DECK_Z + 0.07):  # pipes along the back
        p.bar((-0.1, EDGE - 0.08, z), (0.7, EDGE - 0.08, z), 0.012, DARK)
    p.bar((LEG + LEG_R + 0.02, LEG, UNDER), (LEG + LEG_R + 0.02, LEG, DECK_Z), 0.015, DARK)  # the riser down a leg
    for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1)):
        p.sphere((sx * (EDGE - 0.03), sy * (EDGE - 0.03), DECK_Z + RAIL + 0.03), 'Z', 0.018, RED if variant == "intact" else DARK, sides=6, rings=3)


def _helipad(p):
    """The landing mark in the middle of the deck, where a helicopter sets an engineer down: a yellow ring, a white H in it."""
    x, y = HELIPAD
    p.cylinder((x, y, DECK_Z + 0.001), 'Z', 0.21, 0.001, YELLOW, sides=24)
    p.cylinder((x, y, DECK_Z + 0.0025), 'Z', 0.18, 0.0005, DECK, sides=24)
    for dx in (-0.07, 0.07):
        p.box((x + dx - 0.018, y - 0.1, DECK_Z + 0.003), (x + dx + 0.018, y + 0.1, DECK_Z + 0.004), WHITE)
    p.box((x - 0.07, y - 0.016, DECK_Z + 0.003), (x + 0.07, y + 0.016, DECK_Z + 0.004), WHITE)


def _ruins(p, rnd):
    tops = (0.42, 0.18, 0.3, 0.1)
    _legs(p, tops)
    corners = ((-LEG, -LEG), (LEG, -LEG), (LEG, LEG), (-LEG, LEG))
    for (x, y), top in zip(corners, tops):  # jagged breaks
        for k in range(4):
            a = 2 * math.pi * k / 4 + rnd.uniform(-0.3, 0.3)
            cc.block(p, (x + math.cos(a) * LEG_R * 0.7, y + math.sin(a) * LEG_R * 0.7, top + 0.03), (0.06, 0.03, rnd.uniform(0.04, 0.1)),
                     (rnd.uniform(-0.3, 0.3), 0.0, a), WHITE)
    # Pieces of the deck tilted into the sea, the derrick lying across two stumps.
    for c, s, t in (((-0.35, -0.2, 0.08), (0.8, 0.6, 0.05), (0.25, -0.2, 0.3)), ((0.4, 0.35, 0.05), (0.7, 0.5, 0.05), (-0.3, 0.15, -0.4)),
                    ((0.55, -0.45, 0.03), (0.4, 0.35, 0.04), (0.1, 0.35, 1.0))):
        cc.block(p, c, s, t, DECK)
        cc.block(p, (c[0], c[1], c[2] - 0.06), (s[0], s[1], 0.06), t, WHITE)
    lie = Matrix.Translation((-0.45, 0.1, 0.25)) @ Matrix.Rotation(-0.18, 4, 'X') @ Matrix.Rotation(-1.45, 4, 'Y') @ Matrix.Translation((0.0, 0.0, -0.75))
    transformed(p, lie, lambda: lattice(p, (0.0, 0.0), 0.0, 1.4, 0.18, 0.07, 5, STEEL))
    ic.rubble(p, rnd, -0.8, 0.8, -0.8, 0.8, -0.01, 18, 0.07, WHITE)


# --- material -----------------------------------------------------------------------------

def _material(name, variant):
    """White paint, grey steel, a dark grey deck plate with a yellow edge, safety yellow, dark iron, red, the lifeboat's orange, dark
    blue glass, the light team zone; rust at the waterline and streaking down from the deck, more and soot when abandoned and ruined."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = g.axis(coord, 'X'), g.axis(coord, 'Y'), g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    white = g.mix(grain, lin((0.84, 0.85, 0.84)), lin((0.92, 0.92, 0.91)))
    rust = g.mix(g.noise(25.0, 3.0), lin((0.38, 0.20, 0.10)), lin((0.52, 0.30, 0.15)))
    amount = {"intact": 0.0, "abandoned": 0.35, "ruins": 0.5}[variant]
    waterline = g.clamp01(g.math('MULTIPLY_ADD', z, -6.0, 1.15 + amount))
    streaks = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 2.0), 5.0, -2.6 + 2.0 * amount)),
                     g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 9.0)), 3.0, -1.6)))
    rusty = g.clamp01(g.math('MAXIMUM', g.math('MULTIPLY', waterline, g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 3.0), 3.0, -0.6))),
                                       g.math('MULTIPLY', streaks, 0.6)))
    white = g.mix(rusty, white, rust)
    steel = g.mix(rusty, g.mix(grain, lin((0.46, 0.48, 0.50)), lin((0.56, 0.58, 0.60))), rust)
    edge = g.clamp01(g.math('MULTIPLY_ADD', g.math('MAXIMUM', g.math('ABSOLUTE', x), g.math('ABSOLUTE', y)), 40.0, -40.0 * (EDGE - 0.05)))
    deck = g.mix(edge, g.mix(grain, lin((0.24, 0.25, 0.26)), lin((0.30, 0.31, 0.32))), lin((0.88, 0.72, 0.14)))
    yellow = g.mix(grain, lin((0.86, 0.68, 0.10)), lin((0.92, 0.76, 0.16)))
    dark = g.mix(grain, lin((0.08, 0.08, 0.09)), lin((0.14, 0.14, 0.15)))
    red = lin((0.80, 0.10, 0.08))
    orange = lin((0.96, 0.46, 0.08))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, 6.0, -4.0)), lin((0.10, 0.16, 0.22)), lin((0.32, 0.44, 0.52)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    albedo = cc.paint(g, mask, (white, steel, deck, yellow, dark, red, orange, glass, team))
    if variant != "intact":
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, 0.5 if variant == "abandoned" else 0.6), 0.85), albedo, lin((0.06, 0.05, 0.05)))
    rough = cc.weighted(g, mask, (0.5, 0.45, 0.8, 0.5, 0.55, 0.4, 0.5, 0.1, 0.6))
    metallic = cc.weighted(g, mask, (0.2, 0.6, 0.3, 0.2, 0.6, 0.1, 0.0, 0.0, 0.0))
    g.finish(albedo, g.bump(g.noise(60.0, 3.0), 0.04, 0.004), g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------------

def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "derrick", "beam", "team_mask"))
    rnd = ic.seed(variant_id)
    body = Parts()
    if variant == "ruins":
        _ruins(body, rnd)
        return cc.finish(variant_id, [(body, "body")], UNDER, _material(variant_id + "_mat", variant)), {}
    derrick, beam, team = Parts(), Parts(), Parts()
    _legs(body, (DECK_Z - TRUSS,) * 4)
    _deck(body, team, variant)
    _derrick(derrick, variant)
    _pump(body, beam, variant)
    _quarters(body, variant)
    _crane(body, variant)
    _flare(body, variant)
    _yard(body, variant)
    if variant == "intact":
        _helipad(body)
    if variant == "abandoned":
        for c, s in (((0.0, -0.1), 0.18), ((-0.1, 0.55), 0.14)):  # holes in the deck
            body.box((c[0] - s, c[1] - s, DECK_Z), (c[0] + s, c[1] + s, DECK_Z + 0.003), DARK)
        ic.rubble(body, rnd, -0.6, 0.6, -0.6, 0.6, DECK_Z, 10, 0.05, WHITE)
    parts = [(body, "body"), (derrick, "derrick"), (beam, "beam"), (team, "team_mask")]
    finished = cc.finish(variant_id, parts, UNDER, _material(variant_id + "_mat", variant))
    return finished, {"beam": (PUMP[0], PUMP[1], PIVOT_Z)}


def run(variant_id):
    """Builds, bakes, saves the .blend and exports the GLB: the beam at its pivot, the flare's anchor at its tip (when it burns), the
    fire nodes when abandoned."""
    (meshes, mat, graph), origins = build(variant_id)
    for obj in meshes:
        node = obj.name.split(".")[0][len(variant_id) + 1:]
        if node in origins:
            obj.location = origins[node]
    variant = VARIANTS[variant_id]
    fire = FIRE_POINTS if variant == "abandoned" else ()
    anchors = (("flare_anchor", tuple(FLARE_TIP + Vector((0, 0, 0.1)))),) if variant == "intact" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, anchors, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
