"""Port warehouse, element 07 of the naval set (docs/naval_elements/07_port_warehouse): a long metal hangar on the quay with wide sliding
gates and a small office of two storeys at one end; four soldiers hold the office.

Footprint 3x3 cells, origin at the centre of the footprint on the ground, the front (the quay) to -Y; made upright as the city and the
industrial sets (a storey to a cell, the game squashes it, families/industrial_common.py): the hangar 2.3 long along the quay and 1.8
deep, its eaves at 2.0, its low ridge at 2.7; the office to 2.4; a yard before the front. The parts:
    hangar     walls of grey-blue corrugated panels on a concrete plinth, a ribbon of small windows high on the back and the left gable
               end, a door in the left end, gutters; the right gable over the office's roof
    gate       two wide sliding leaves of darker corrugated sheet with braces on a rail in the front, a lamp; the owner's band over it
    roof       two slopes of light grey corrugated sheet along x, a ridge vent, skylights, two turbine vents
    office     at the right end: two storeys of white render on a plinth, dark windows in white frames (two on the front, three on the
               side, each storey), a door with a canopy, a flat roof with a parapet, an air conditioner and an aerial
    yard       a concrete apron, pallets and crates at the front left, a yellow forklift by the gate, drums at the back
Nodes: "body" (the hangar and the yard), "roof", "office", "gate", "team_mask" (the band over the gate), "base" (the apron); fire_1..3
when abandoned. One GLB per state:
    port_warehouse                   whole, empty: the gate shut
    port_warehouse_occupied          whole, four soldiers in the office: barrels out of its upper windows, sandbags at its door
    port_warehouse_damaged           damaged: a gate leaf torn off and down, holes in the panels, sheets bent up off the roof, half the
                                     office's windows out, soot
    port_warehouse_damaged_occupied  the same, with the barrels and the sandbags
    port_warehouse_abandoned         critically damaged, its soldiers out: the left half of the roof fallen into the hangar, its trusses
                                     bare and charred, the left end's panels torn down, the office's windows all out, its corner holed;
                                     fire in three places
    port_warehouse_ruins             destroyed: stumps of the panels round the floor, sheets and trusses down on it, the office's ground
                                     floor in stumps, rubble
Budget 12000 triangles, texture 2048 (docs/naval_elements/07_port_warehouse/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.naval_warehouse as pw
    print(pw.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from families.naval_reef import FOLDER
from units.parts import Parts, lin
import rts_common as rc

SIZE = 2048
VARIANTS = {
    "port_warehouse": "intact",
    "port_warehouse_occupied": "occupied",
    "port_warehouse_damaged": "damaged",
    "port_warehouse_damaged_occupied": "damaged_occupied",
    "port_warehouse_abandoned": "abandoned",
    "port_warehouse_ruins": "ruins",
}

(PANEL, PLINTH, ROOF, GATE, WHITE, GLASS, FRAME, METAL, CONCRETE, DARK, SAND, TEAM, WOOD, LAMP, FLOOR, YELLOW) = (cc.part(i) for i in range(16))
PARTS = 16

APRON = 0.05
HX0, HX1 = -1.42, 0.85  # the hangar's ends: long along the quay
HY = 0.9
EAVE = 2.0
RIDGE = 2.7
OVER = 0.1
OX1, OY = 1.42, 0.72  # the office: from HX1 to OX1, y within ±OY
STOREY = 1.1
OFFICE_TOP = APRON + 2 * STOREY
GATE_X = (-1.0, 0.45)
GATE_TOP = 1.7
BREAK_X = -0.35  # the abandoned one: the roof left of it has fallen
FIRE_POINTS = ((-0.9, 0.0, 0.8), (1.1, -0.45, 1.6), (0.1, 0.4, 1.9))
# The office's windows: the front (x) and the right side (y), each storey.
OFFICE_FRONT = (1.0, 1.27)
OFFICE_SIDE = (-0.42, 0.0, 0.42)


def _slope(t):
    """The height of the roof's underside at |y| = t (0 at the ridge, HY at the eaves)."""
    return RIDGE - (RIDGE - EAVE) * t / HY


# --- hangar -------------------------------------------------------------------------

def _hangar(p, variant):
    abandoned = variant == "abandoned"
    x0 = HX0 + (0.9 if abandoned else 0.0)  # the left end torn down when abandoned
    p.box((x0, -HY, APRON), (HX1, HY, EAVE), PANEL)
    p.box((x0 - 0.03, -HY - 0.03, APRON), (HX1, HY + 0.03, APRON + 0.25), PLINTH)
    # Gutters along the eaves.
    for s in (-1, 1):
        p.box((x0 - 0.02, s * HY - 0.05, EAVE - 0.08), (HX1 + 0.02, s * HY + 0.05, EAVE), METAL)
    # The gables: the left end and the right one over the office.
    for x, inward in ((HX0, 1), (HX1, -1)):
        if abandoned and x < 0:
            continue
        p.profile_x([(-HY, EAVE), (HY, EAVE), (0.0, RIDGE)], min(x, x + inward * 0.06), max(x, x + inward * 0.06), PANEL)
    # A ribbon of small windows high on the back wall and on the left end.
    for k in range(8):
        x = HX0 + 0.22 + 0.28 * k
        if abandoned and x < x0 + 0.1:
            continue
        p.box((x - 0.09, HY, 1.4), (x + 0.09, HY + 0.012, 1.65), GLASS if variant in ("intact", "occupied") or k % 2 else DARK)
        p.box((x - 0.1, HY, 1.38), (x + 0.1, HY + 0.02, 1.4), FRAME)
    if not abandoned:
        for y in (-0.55, -0.3, 0.3, 0.55):
            p.box((HX0 - 0.012, y - 0.08, 1.4), (HX0, y + 0.08, 1.65), GLASS)
        p.box((HX0 - 0.015, -0.18, APRON + 0.2), (HX0, 0.18, 1.2), DARK if variant != "intact" else GATE)  # the door in the end
        p.box((HX0 - 0.22, -0.26, 1.24), (HX0, 0.26, 1.29), METAL)


def _gate(g, team, p, variant):
    """The sliding gate in the front: the opening, two leaves on a rail (one torn off and down when damaged), the band over it."""
    y = -HY
    torn = variant in ("damaged", "damaged_occupied", "abandoned")
    p.box((GATE_X[0], y - 0.01, APRON), (GATE_X[1], y + 0.005, GATE_TOP), DARK)  # the opening
    mid = (GATE_X[0] + GATE_X[1]) / 2
    for k, (x0, x1) in enumerate(((GATE_X[0] + 0.02, mid - 0.005), (mid + 0.005, GATE_X[1] - 0.02))):
        if torn and k == 1:
            cc.block(g, ((x0 + x1) / 2 + 0.1, y - 0.55, APRON + 0.03), (x1 - x0, 0.9, 0.04), (0.0, 0.0, 0.2), GATE)
            continue
        g.box((x0, y - 0.04, APRON + 0.02), (x1, y - 0.01, GATE_TOP - 0.02), GATE)
        for z in (APRON + 0.5, GATE_TOP - 0.5):
            g.box((x0 + 0.03, y - 0.055, z - 0.03), (x1 - 0.03, y - 0.04, z + 0.03), GATE)
        g.bar((x0 + 0.05, y - 0.05, APRON + 0.53), (x1 - 0.05, y - 0.05, GATE_TOP - 0.53), 0.018, GATE)
        g.box((x1 - 0.08, y - 0.07, 0.85), (x1 - 0.06, y - 0.04, 1.1), METAL)  # a handle
    p.box((GATE_X[0] - 0.6, y - 0.1, GATE_TOP + 0.02), (GATE_X[1] + 0.3, y - 0.01, GATE_TOP + 0.08), METAL)  # the rail
    # The lamp and the owner's band over the gate.
    a = Vector((mid, y, GATE_TOP + 0.4))
    p.bar(a, a + Vector((0, -0.16, -0.04)), 0.012, METAL)
    p.turned(a + Vector((0, -0.16, -0.06)), 'Z', [(-0.06, 0.0), (-0.05, 0.06), (0.0, 0.08), (0.01, 0.0)], METAL, sides=10)
    p.turned(a + Vector((0, -0.16, -0.13)), 'Z', [(0.0, 0.0), (0.02, 0.04), (0.06, 0.0)], LAMP, sides=8)
    team.box((GATE_X[0] - 0.05, y - 0.04, GATE_TOP + 0.12), (GATE_X[1] + 0.05, y - 0.01, GATE_TOP + 0.3), TEAM)


def _roof(r, variant):
    abandoned = variant == "abandoned"
    x0 = BREAK_X if abandoned else HX0 - OVER
    t = 0.05
    for side in (-1, 1):
        ye = side * (HY + OVER)
        ze = _slope(HY + OVER)
        r.profile_x([(ye, ze), (0.0, RIDGE), (0.0, RIDGE + t), (ye, ze + t)] if side > 0 else
                    [(0.0, RIDGE), (ye, ze), (ye, ze + t), (0.0, RIDGE + t)], x0, HX1 + 0.04, ROOF)
        for xs in ((-1.1, -0.7), (-0.3, 0.1)):  # skylights
            if abandoned and xs[0] < BREAK_X:
                continue
            ya, yb = side * 0.22, side * 0.68
            za, zb = _slope(0.22) + t + 0.005, _slope(0.68) + t + 0.005
            r.loft([[Vector((x, ya, za)), Vector((x, yb, zb)), Vector((x, yb, zb + 0.015)), Vector((x, ya, za + 0.015))] for x in xs], GLASS)
        if variant in ("damaged", "damaged_occupied"):
            for x in (-0.9, 0.25):  # sheets bent up off the slope, the hole under them
                yh = side * 0.45
                tilt = side * math.atan2(RIDGE - EAVE, HY)
                cc.block(r, (x, yh, _slope(0.45) + t + 0.005), (0.3, 0.26, 0.01), (tilt, 0.0, 0.0), DARK)
                cc.block(r, (x + 0.05, yh + side * 0.1, _slope(0.45) + t + 0.1), (0.3, 0.24, 0.02), (tilt + side * 0.7, 0.1, 0.1), ROOF)
    # The ridge vent along the ridge, two turbine vents.
    r.box((x0 + 0.1, -0.12, RIDGE), (HX1 - 0.1, 0.12, RIDGE + 0.14), METAL)
    r.box((x0 + 0.05, -0.17, RIDGE + 0.14), (HX1 - 0.05, 0.17, RIDGE + 0.17), ROOF)
    for x in (-0.6, 0.2):
        if abandoned and x < BREAK_X:
            continue
        c = Vector((x, 0.55, _slope(0.55) + t))
        r.turned(c, 'Z', [(-0.05, 0.07), (0.1, 0.07), (0.12, 0.1), (0.26, 0.1), (0.32, 0.05), (0.35, 0.0)], METAL, sides=12)
    if abandoned:
        # The fallen half: bare charred trusses, the sheets down inside.
        for x in (-1.25, -0.8):
            r.bar((x, -HY, EAVE * 0.7), (x, 0.0, RIDGE * 0.75), 0.04, DARK)
            r.bar((x, 0.0, RIDGE * 0.75), (x, HY, EAVE * 0.65), 0.04, DARK)
        r.bar((BREAK_X, -0.2, RIDGE - 0.1), (-1.2, 0.3, APRON + 0.4), 0.045, DARK)
        cc.block(r, (-0.85, -0.55, APRON + 0.45), (0.9, 0.7, 0.03), (0.4, -0.3, 0.1), ROOF)
        cc.block(r, (-0.8, 0.6, APRON + 0.7), (0.8, 0.6, 0.03), (-0.6, 0.2, -0.2), ROOF)


# --- office --------------------------------------------------------------------------

def _office(o, variant):
    abandoned = variant == "abandoned"
    broken_ones = variant in ("damaged", "damaged_occupied")
    x0, x1 = HX1, OX1
    o.box((x0, -OY, APRON), (x1, OY, OFFICE_TOP), WHITE)
    o.box((x0, -OY - 0.03, APRON), (x1 + 0.03, OY + 0.03, APRON + 0.18), PLINTH)
    o.box((x0, -OY - 0.03, OFFICE_TOP), (x1 + 0.03, OY + 0.03, OFFICE_TOP + 0.15), WHITE)  # the parapet
    o.box((x0 + 0.05, -OY + 0.05, OFFICE_TOP + 0.05), (x1 - 0.05, OY - 0.05, OFFICE_TOP + 0.08), DARK)  # the roof inside it
    o.box((x0, -OY - 0.02, APRON + STOREY - 0.03), (x1 + 0.02, OY + 0.02, APRON + STOREY + 0.03), PLINTH)  # the band between storeys
    k = 0
    for storey in range(2):
        z0 = APRON + storey * STOREY + 0.35
        z1 = z0 + 0.55
        for x in OFFICE_FRONT:
            if storey == 0 and x > 1.1:
                continue  # the door is there
            k += 1
            glass = DARK if abandoned or broken_ones and k % 2 else GLASS
            o.box((x - 0.14, -OY - 0.012, z0), (x + 0.14, -OY, z1), glass)
            o.box((x - 0.17, -OY - 0.03, z0 - 0.04), (x + 0.17, -OY, z0), WHITE)
            for a in (x - 0.17, x + 0.14):
                o.box((a, -OY - 0.02, z0), (a + 0.03, -OY, z1), FRAME)
        for y in OFFICE_SIDE:
            k += 1
            glass = DARK if abandoned or broken_ones and k % 2 else GLASS
            o.box((x1, y - 0.14, z0), (x1 + 0.012, y + 0.14, z1), glass)
            o.box((x1, y - 0.17, z0 - 0.04), (x1 + 0.03, y + 0.17, z0), WHITE)
            for a in (y - 0.17, y + 0.14):
                o.box((x1, a, z0), (x1 + 0.02, a + 0.03, z1), FRAME)
    # The door on the front, its canopy and step.
    o.box((1.15, -OY - 0.015, APRON + 0.15), (1.37, -OY, 0.95), DARK if variant != "intact" else FRAME)
    o.box((1.1, -OY - 0.3, 1.0), (1.42, -OY, 1.05), METAL)
    o.box((1.12, -OY - 0.2, APRON), (1.4, -OY, APRON + 0.08), CONCRETE)
    if abandoned:
        # The front right corner holed through both storeys.
        o.box((OX1 - 0.35, -OY - 0.02, 0.5), (OX1 + 0.02, -OY + 0.3, 1.6), DARK)
    else:
        # The air conditioner and the aerial on the roof.
        o.box((0.95, 0.2, OFFICE_TOP + 0.08), (1.2, 0.5, OFFICE_TOP + 0.3), METAL)
        o.bar((1.3, 0.5, OFFICE_TOP + 0.08), (1.3, 0.5, OFFICE_TOP + 0.9), 0.012, METAL)


def _occupied(o):
    """Four soldiers: barrels out of the office's upper windows, sandbags at its door."""
    z = APRON + STOREY + 0.65
    ic.barrels(o, [(x, -OY - 0.02, z) for x in OFFICE_FRONT], (0.0, -1.0, 0.0), DARK)
    ic.barrels(o, [(OX1 + 0.02, y, z) for y in OFFICE_SIDE[::2]], (1.0, 0.0, 0.0), DARK)
    ic.sandbags(o, cc.steps(1.0, 1.42, 0.2), -OY - 0.35, APRON, SAND)


# --- yard, damage, ruins --------------------------------------------------------------

def _yard(p, base, variant):
    base.box((-1.48, -1.48, 0.0), (1.48, 1.48, APRON), CONCRETE)
    if variant == "ruins":
        return
    # Pallets and crates at the front left.
    for x, y, h in ((-1.25, -1.33, 0.22), (-0.95, -1.3, 0.38), (-1.25, -1.08, 0.3)):
        p.box((x - 0.12, y - 0.1, APRON), (x + 0.12, y + 0.1, APRON + 0.05), WOOD)
        p.box((x - 0.1, y - 0.08, APRON + 0.05), (x + 0.1, y + 0.08, APRON + h), WOOD)
    # The forklift by the gate.
    if variant not in ("abandoned",):
        fx, fy = 0.45, -1.42
        p.box((fx - 0.12, fy - 0.08, APRON + 0.05), (fx + 0.12, fy + 0.08, APRON + 0.2), YELLOW)
        p.box((fx - 0.1, fy - 0.07, APRON + 0.2), (fx + 0.02, fy + 0.07, APRON + 0.4), METAL)  # the cage
        for x in (fx - 0.08, fx + 0.08):
            p.cylinder((x, fy, APRON + 0.05), 'Y', 0.045, 0.09, DARK, sides=8)
        p.box((fx + 0.12, fy - 0.07, APRON + 0.05), (fx + 0.14, fy + 0.07, APRON + 0.5), DARK)  # the mast
        for y in (fy - 0.04, fy + 0.04):
            p.box((fx + 0.14, y - 0.01, APRON + 0.05), (fx + 0.3, y + 0.01, APRON + 0.07), DARK)  # the forks
    # Drums at the back left.
    for k, (x, y) in enumerate(((-1.2, HY + 0.12), (-1.05, HY + 0.13))):
        p.turned((x, y, APRON), 'Z', [(0.0, 0.07), (0.3, 0.07)], DARK if variant == "abandoned" else YELLOW, sides=10)


def _damage(p, rnd):
    """Holes in the panels, torn sheets at their foot."""
    for c, s in (((-1.2, -HY), (0.3, 0.3)), ((0.4, HY), (0.25, 0.35))):
        x, y = c
        d = -0.012 if y < 0 else 0.0
        p.box((x - s[0] / 2, y + d, 0.9), (x + s[0] / 2, y + d + 0.012, 0.9 + s[1]), DARK)
        cc.block(p, (x, y - 0.15 if y < 0 else y + 0.15, APRON + 0.05), (0.3, 0.2, 0.02), (0.3, 0.1, rnd.uniform(0, 1)), PANEL)
    ic.rubble(p, rnd, HX1 + 0.1, OX1, -OY - 0.3, -OY - 0.05, APRON, 4, 0.08, WHITE)


def _abandoned(p, rnd):
    """The left end torn down: stumps of its panels, the floor, sheets and rubble."""
    p.box((HX0, -HY, APRON), (HX0 + 0.9, HY, APRON + 0.02), FLOOR)
    ic.stumps(p, rnd, [((HX0 + 0.9, -HY + 0.03), (HX0 + 0.03, -HY + 0.03)), ((HX0 + 0.03, -HY + 0.03), (HX0 + 0.03, HY - 0.03)),
                       ((HX0 + 0.03, HY - 0.03), (HX0 + 0.9, HY - 0.03))], APRON, 0.3, 1.6, PANEL, step=0.22, gap=0.2, thick=0.04)
    ic.rubble(p, rnd, HX0 + 0.1, HX0 + 0.9, -HY + 0.2, HY - 0.2, APRON, 10, 0.15, PANEL, height=0.2)
    ic.rubble(p, rnd, HX1 + 0.1, OX1, -OY - 0.35, -OY - 0.05, APRON, 8, 0.1, WHITE)


def _ruins(p, rnd):
    p.box((HX0, -HY, APRON), (OX1, HY, APRON + 0.02), FLOOR)
    ic.stumps(p, rnd, [((HX1, -HY + 0.03), (HX0 + 0.03, -HY + 0.03)), ((HX0 + 0.03, -HY + 0.03), (HX0 + 0.03, HY - 0.03)),
                       ((HX0 + 0.03, HY - 0.03), (HX1, HY - 0.03))], APRON, 0.1, 1.0, PANEL, step=0.22, gap=0.25, thick=0.04)
    ic.stumps(p, rnd, [((HX1, -OY + 0.05), (OX1 - 0.05, -OY + 0.05)), ((OX1 - 0.05, -OY + 0.05), (OX1 - 0.05, OY - 0.05)),
                       ((OX1 - 0.05, OY - 0.05), (HX1, OY - 0.05))], APRON, 0.3, 1.1, WHITE, step=0.2, gap=0.15, thick=0.12)
    for k in range(4):  # trusses and sheets down on the floor
        x = -1.15 + 0.5 * k
        p.bar((x - 0.2, -HY + 0.3, APRON + 0.1), (x + 0.25, HY - 0.3, APRON + 0.3 + 0.08 * k), 0.04, DARK)
        cc.block(p, (x + 0.1, rnd.uniform(-0.6, 0.6), APRON + 0.12), (0.6, 0.5, 0.03), (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(0, 1)), ROOF)
    ic.rubble(p, rnd, HX1 + 0.1, OX1 - 0.1, -OY + 0.1, OY - 0.1, APRON, 14, 0.18, WHITE, height=0.3)
    ic.rubble(p, rnd, HX0 + 0.2, HX1 - 0.1, -HY + 0.2, HY - 0.2, APRON, 10, 0.15, PANEL, height=0.15)


# --- material -------------------------------------------------------------------------

def _material(name, variant):
    """Grey-blue corrugated panels, a concrete plinth, light grey corrugated roof sheet with rust at its edges, the gate's darker sheet,
    white render, dark glass, dark frames, dark metal, stained concrete, sandbags, the light team zone, wood, a warm lamp, a dusty
    floor, safety yellow; soot when damaged, more when abandoned, the ruins sooty all over."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = g.axis(coord, 'X'), g.axis(coord, 'Y'), g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    ribs = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 10.0)), 0.5)), 4.0, -0.6))
    grime = g.clamp01(g.math('MULTIPLY_ADD', z, -0.8, 0.9))
    panel = g.mix(ribs, lin((0.46, 0.53, 0.60)), lin((0.36, 0.42, 0.49)))
    panel = g.mix(g.math('MULTIPLY', grime, 0.4), panel, lin((0.22, 0.24, 0.26)))
    plinth = g.mix(grain, lin((0.50, 0.49, 0.46)), lin((0.58, 0.57, 0.54)))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 4.0), 5.0, -2.9))
    # The roof's corrugations run down the slopes: they vary along x only.
    roof_ribs = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', x, 12.0)), 0.5)), 4.0, -0.6))
    roof = g.mix(roof_ribs, lin((0.72, 0.74, 0.75)), lin((0.58, 0.60, 0.61)))
    roof = g.mix(g.math('MULTIPLY', rust, 0.7), roof, lin((0.44, 0.24, 0.12)))
    gate = g.mix(ribs, lin((0.30, 0.37, 0.44)), lin((0.22, 0.28, 0.34)))
    white = g.mix(grain, lin((0.84, 0.84, 0.82)), lin((0.92, 0.92, 0.90)))
    white = g.mix(g.math('MULTIPLY', grime, 0.35), white, lin((0.50, 0.48, 0.44)))
    glass = g.mix(g.noise(4.0, 2.0), lin((0.10, 0.13, 0.16)), lin((0.24, 0.30, 0.34)))
    frame = g.mix(grain, lin((0.12, 0.13, 0.14)), lin((0.18, 0.19, 0.20)))
    metal = g.mix(grain, lin((0.24, 0.25, 0.26)), lin((0.32, 0.33, 0.34)))
    stain = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 3.0, -1.7))
    concrete = g.mix(grain, lin((0.54, 0.53, 0.50)), lin((0.62, 0.61, 0.58)))
    concrete = g.mix(g.math('MULTIPLY', stain, 0.6), concrete, lin((0.26, 0.25, 0.23)))
    dark = g.mix(grain, lin((0.04, 0.04, 0.04)), lin((0.08, 0.07, 0.07)))
    sand = g.mix(g.noise(20.0, 3.0), lin((0.56, 0.50, 0.36)), lin((0.64, 0.58, 0.42)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    wood = g.mix(g.noise(5.0, 5.0), lin((0.50, 0.36, 0.20)), lin((0.60, 0.44, 0.26)))
    lamp = lin((1.0, 0.86, 0.55))
    floor = g.mix(g.noise(8.0, 3.0), lin((0.28, 0.27, 0.25)), lin((0.36, 0.34, 0.31)))
    yellow = g.mix(grain, lin((0.86, 0.66, 0.10)), lin((0.92, 0.74, 0.16)))
    albedo = cc.paint(g, mask, (panel, plinth, roof, gate, white, glass, frame, metal, concrete, dark, sand, team, wood, lamp, floor, yellow))
    amount = {"intact": 0.0, "occupied": 0.0, "damaged": 0.35, "damaged_occupied": 0.35, "abandoned": 0.7, "ruins": 0.85}[variant]
    if amount:
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.85), albedo, lin((0.06, 0.05, 0.05)))
    rough = cc.weighted(g, mask, (0.5, 0.9, 0.5, 0.55, 0.85, 0.15, 0.5, 0.5, 0.9, 0.9, 0.95, 0.5, 0.85, 0.3, 0.95, 0.5))
    metallic = cc.weighted(g, mask, (0.5, 0.0, 0.6, 0.5, 0.0, 0.0, 0.5, 0.6, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.2))
    height = g.math('MULTIPLY_ADD', g.math('ADD', mask[0], mask[3]), ribs, g.math('MULTIPLY_ADD', mask[2], roof_ribs, g.noise(60.0, 3.0)))
    g.finish(albedo, g.bump(height, 0.05, 0.006), g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "roof", "office", "gate", "team_mask", "base"))
    rnd = ic.seed(variant_id)
    body, base = Parts(), Parts()
    _yard(body, base, variant)
    if variant == "ruins":
        _ruins(body, rnd)
        return cc.finish(variant_id, [(body, "body"), (base, "base")], APRON - 0.001, _material(variant_id + "_mat", variant))
    roof, office, gate, team = Parts(), Parts(), Parts(), Parts()
    _hangar(body, variant)
    _gate(gate, team, body, variant)
    _roof(roof, variant)
    _office(office, variant)
    if variant in ("occupied", "damaged_occupied"):
        _occupied(office)
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        _damage(body, rnd)
    if variant == "abandoned":
        _abandoned(body, rnd)
    parts = [(body, "body"), (roof, "roof"), (office, "office"), (gate, "gate"), (team, "team_mask"), (base, "base")]
    return cc.finish(variant_id, parts, APRON - 0.001, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
