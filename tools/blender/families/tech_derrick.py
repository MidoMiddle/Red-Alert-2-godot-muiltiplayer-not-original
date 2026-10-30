"""Oil pumpjack, tech_oil_derrick (docs/map_elements/12_tech_buildings): a small oil field on a concrete slab, in detail.

Footprint 2x2 cells, origin at the centre of the footprint on the ground, the front to -Y. The kinds of parts follow the owner's
picture of a tech oil derrick (2026-10-30): a storage tank, the pumpjack, a shed, pipes, ladders and a mast; their shapes, sizes and
places are ours (generation_requirements.md, section 8). The parts:
    slab        the concrete slab, 0.12 high (the base node, docs/art/model_standards.md, 3.1)
    tank        a light grey storage tank at the back left, its dome with a hatch and a vent valve, two hoops, a ladder up its side
                and over the dome; a pipe from the well runs on supports along the back of the pumpjack and climbs the tank into
                its dome, a valve with a red wheel on it
    pumpjack    on a skid of steel rails and a deck of plate: the four-legged Samson post with braces, a bearing on top and a ladder
                up it; the walking beam (an I-beam) with its curved horse head, the equalizer and the two pitman arms down to the
                cranks; the gearbox with its crank arms and yellow counterweights; the motor with fins and a yellow belt guard
    well        the wellhead: a flange, the casing, a tee to the pipe, a valve with a red wheel, the stuffing box and the polished
                rod up to the carrier bar on the two bridle cables from the horse head
    shed        the control shed at the front left: corrugated walls, a door, lit windows, a hip roof; a conduit to the motor
    yard        a mast with a red light and two guy wires, oil drums, a spare tank lying on saddles, a stack of pipes on sleepers
Nodes: "body", "beam" (the walking beam, horse head, equalizer and pitman arms; its origin on the axis it nods about, the game nods
it about its own z, Blender's y), "team_mask" (a band round the tank and the top of the shed's roof), "base". One GLB per state:
    tech_oil_derrick            intact, working: the beam nods
    tech_oil_derrick_abandoned  critically damaged: the beam dropped head down and its pitman arms off, the ladder's top torn
                                away, a hole in the dome, the shed's roof caved in and its windows out, drums knocked over, the mast
                                bent; fire on the tank, the gearbox and the horse head (fire_1..3)
    tech_oil_derrick_ruins      destroyed: the tank fallen across the slab, the Samson post down, the beam and the horse head on the
                                slab, the shed in heaps, the mast across the yard
Budget 8000 triangles (docs/map_elements/12_tech_buildings/prompts.md, 1), texture 2048: the many small parts want it.

In Blender, after rts_common is on sys.path:
    import families.tech_derrick as td
    print(td.run("tech_oil_derrick"))   # or td.run_all()
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin, uv_together

SIZE = 2048
VARIANTS = {
    "tech_oil_derrick": "intact",
    "tech_oil_derrick_abandoned": "abandoned",
    "tech_oil_derrick_ruins": "ruins",
}

(CONCRETE, DECK, TANK, STEEL, DARK, YELLOW, PIPE, SHED, ROOF, GLASS, DOOR, TEAM, RED, DRUM, WOOD,
 HOLE) = (cc.part(i) for i in range(16))
PARTS = 16

S = 0.12  # the top of the slab
BY = -0.18  # the line of the pumpjack along x
PIVOT = Vector((0.32, BY, 1.18))  # the axis of the beam: the origin of its node
HEAD_R = 0.5  # the horse head: an arc about the pivot, so the rod hangs straight
WELL = (0.82, BY)  # the wellhead, under the face of the horse head
T = (-0.45, 0.42)  # the axis of the tank
R = 0.38
TANK_TOP = S + 1.57
SHED = (-0.92, -0.46, -0.92, -0.52)  # x0, x1, y0, y1
SHED_TOP = S + 0.42
MAST = (-0.88, -0.36)
CRANK = (-0.2, S + 0.42)  # the gearbox's output shaft: x, z
CRANK_PIN = (-0.24, S + 0.56)
FIRE_POINTS = ((T[0], T[1], TANK_TOP), (-0.2, BY, 0.7), (0.8, BY, 1.0))


# --- shapes ---------------------------------------------------------------------------

def _tube(p, points, radius, painted, sides=8):
    """A round pipe along the points, its ends capped."""
    pts = [Vector(q) for q in points]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        a = d.cross(Vector((0.0, 0.0, 1.0)))
        if a.length < 1e-4:
            a = d.cross(Vector((1.0, 0.0, 0.0)))
        a.normalize()
        b = d.cross(a).normalized()
        rings.append([q + (a * math.cos(2 * math.pi * k / sides) + b * math.sin(2 * math.pi * k / sides)) * radius for k in range(sides)])
    p.loft(rings, painted)


def _bend(points, steps=3, cut=0.06):
    """The corners of a path rounded off: each inner corner becomes a short arc."""
    pts = [Vector(q) for q in points]
    out = [pts[0]]
    for a, b, c in zip(pts, pts[1:], pts[2:]):
        u, w = (a - b).normalized(), (c - b).normalized()
        k = min(cut, (a - b).length / 2, (c - b).length / 2)
        p0, p2 = b + u * k, b + w * k
        for i in range(steps + 1):
            t = i / steps
            out.append((1 - t) ** 2 * p0 + 2 * (1 - t) * t * b + t * t * p2)
    out.append(pts[-1])
    return out


def _wheel(p, centre, axis, radius=0.05):
    """A valve's handwheel: a red disc on a short stem."""
    p.cylinder(centre, axis, radius, 0.008, RED, sides=10)


def _drum(p, x, y, lying=None):
    """An oil drum with two rims; lying: the direction (radians) it lies along."""
    if lying is None:
        p.turned((x, y, S), 'Z', [(0.0, 0.055), (0.16, 0.055)], DRUM, sides=10)
        for z in (0.05, 0.11):
            p.turned((x, y, S + z), 'Z', [(0.0, 0.059), (0.01, 0.059)], DARK, sides=10)
        return
    d = Vector((math.cos(lying), math.sin(lying), 0.0))
    c = Vector((x, y, S + 0.055))
    _tube(p, [c - d * 0.08, c + d * 0.08], 0.055, DRUM, sides=10)


# --- the tank -------------------------------------------------------------------------

def _tank(p, team, variant):
    tx, ty = T
    if variant == "ruins":
        # Fallen along x across the back of the slab, crushed a little.
        p.turned((tx - 0.32, 0.62, S + 0.3), 'X', [(0.0, 0.3), (1.15, 0.3), (1.28, 0.22), (1.32, 0.1)], TANK, sides=14)
        return
    p.turned((tx, ty, S), 'Z', [(0.0, R + 0.03), (0.05, R + 0.03), (0.05, R), (1.32, R), (1.40, R * 0.93), (1.48, R * 0.76),
                                (1.54, R * 0.5), (1.57, 0.13)], TANK, sides=20)
    p.turned((tx, ty, S + 0.45), 'Z', [(0.0, R + 0.014), (0.04, R + 0.014)], STEEL, sides=20)  # a hoop
    team.turned((tx, ty, S + 0.9), 'Z', [(0.0, R + 0.016), (0.16, R + 0.016)], TEAM, sides=20)  # the owner's band
    if variant == "abandoned":
        cc.block(p, (tx + 0.18, ty - 0.2, TANK_TOP - 0.1), (0.2, 0.05, 0.14), (0.5, 0.0, 0.6), HOLE)  # a hole torn in the dome
    else:
        p.cylinder((tx, ty, TANK_TOP + 0.03), 'Z', 0.1, 0.03, STEEL, sides=12)  # the hatch
        p.cylinder((tx + 0.06, ty + 0.06, TANK_TOP + 0.1), 'Z', 0.022, 0.07, PIPE, sides=8)  # the vent
        _wheel(p, (tx + 0.06, ty + 0.06, TANK_TOP + 0.18), 'Z', 0.04)
    _tank_ladder(p, variant)


def _tank_ladder(p, variant):
    """Up the front of the tank and over the dome; abandoned, its top torn away."""
    tx, ty = T
    y = ty - R - 0.05
    top = S + 1.32 if variant == "intact" else S + 0.9
    for x in (tx + 0.04, tx + 0.16):
        p.bar((x, y, S), (x, y, top), 0.011, STEEL)
        if variant == "intact":
            p.bar((x, y, top), (x, ty - 0.14, TANK_TOP + 0.02), 0.011, STEEL)
    for z in cc.steps(S + 0.12, top - 0.05, 0.1):
        p.bar((tx + 0.04, y, z), (tx + 0.16, y, z), 0.008, STEEL)


def _pipeline(p, variant):
    """From the well's tee back along the pumpjack, across on supports and up the tank into its dome; a valve on the way."""
    wx, wy = WELL
    tx, ty = T
    path = [(wx, wy + 0.08, S + 0.3), (wx, 0.32, S + 0.3), (wx - 0.04, 0.34, S + 0.2), (0.04, 0.34, S + 0.2),
            (0.0, 0.34, S + 0.3), (0.0, 0.34, S + 1.2), (-0.06, 0.34, S + 1.36), (tx + R - 0.24, 0.34, S + 1.44)]
    if variant == "ruins":
        _tube(p, _bend(path[:4]), 0.03, PIPE)
        return
    _tube(p, _bend(path), 0.03, PIPE)
    for x in (0.62, 0.3):
        p.box((x - 0.03, 0.31, S), (x + 0.03, 0.37, S + 0.18), STEEL)  # the supports
    p.box((0.42, 0.3, S + 0.16), (0.5, 0.38, S + 0.25), DARK)  # the valve
    p.cylinder((0.46, 0.34, S + 0.29), 'Z', 0.01, 0.04, STEEL, sides=6)
    if variant == "intact":
        _wheel(p, (0.46, 0.34, S + 0.33), 'Z')


# --- the pumpjack ---------------------------------------------------------------------

def _skid(p):
    """The deck of plate and the steel rails the pumpjack, the gearbox and the motor stand on."""
    p.box((-0.66, BY - 0.36, S), (0.62, BY + 0.36, S + 0.02), DECK)
    for y in (BY - 0.27, BY + 0.27):
        p.box((-0.64, y - 0.03, S + 0.02), (0.6, y + 0.03, S + 0.08), STEEL)
    for x in (-0.6, -0.1, 0.55):
        p.box((x - 0.03, BY - 0.24, S + 0.02), (x + 0.03, BY + 0.24, S + 0.07), STEEL)


def _post(p, variant):
    """The Samson post: four legs to the bearing under the beam, braces, a ladder; ruins: down across the deck."""
    px, _, pz = PIVOT
    if variant == "ruins":
        for dy, turn in ((-0.2, 0.15), (0.12, -0.1)):
            cc.block(p, (0.25, BY + dy, S + 0.1), (1.0, 0.05, 0.05), (0.0, 0.08, turn), STEEL)
        cc.block(p, (px, BY - 0.02, S + 0.12), (0.14, 0.18, 0.08), (0.2, 0.0, 0.4), STEEL)
        return
    top = pz - 0.12
    feet = [(px + dx, BY + dy, S + 0.08) for dx in (-0.2, 0.2) for dy in (-0.26, 0.26)]
    heads = [(px + 0.04 * (1 if dx > 0 else -1), BY + 0.07 * (1 if dy > 0 else -1), top) for (dx, dy) in ((f[0] - px, f[1] - BY) for f in feet)]
    for f, h in zip(feet, heads):
        p.bar(f, h, 0.022, STEEL)

    def on(f, h, t):
        return tuple(a + (b - a) * t for a, b in zip(f, h))

    for t in (0.35, 0.68):
        for i, j in ((0, 2), (1, 3), (0, 1), (2, 3)):
            p.bar(on(feet[i], heads[i], t), on(feet[j], heads[j], t), 0.012, STEEL)
    p.box((px - 0.07, BY - 0.1, top), (px + 0.07, BY + 0.1, pz - 0.06), STEEL)  # the bearing
    p.cylinder((px, BY, pz), 'Y', 0.026, 0.13, DARK, sides=8)  # its shaft
    # A ladder up the front legs.
    for y in (BY - 0.33, BY - 0.24):
        p.bar((px + 0.32, y, S + 0.08), (px + 0.06, y, top - 0.04), 0.009, STEEL)
    for t in cc.steps(0.1, 0.9, 0.12):
        x, z = px + 0.32 + (0.06 - 0.32) * t, S + 0.08 + (top - 0.04 - S - 0.08) * t
        p.bar((x, BY - 0.33, z), (x, BY - 0.24, z), 0.007, STEEL)


def _beam(p, variant):
    """The walking beam, built in place: an I-beam, the horse head at its front, the equalizer and the pitman arms at its back."""
    px, _, pz = PIVOT
    x0, x1 = px - 0.62, px + 0.4
    p.box((x0, BY - 0.05, pz + 0.045), (x1, BY + 0.05, pz + 0.065), STEEL)  # the top flange
    p.box((x0, BY - 0.05, pz - 0.065), (x1, BY + 0.05, pz - 0.045), STEEL)  # the bottom one
    p.box((x0, BY - 0.012, pz - 0.045), (x1, BY + 0.012, pz + 0.045), STEEL)  # the web
    # The horse head: an arc about the pivot from below the beam to above it, a curved face and two sides.
    arc = [math.radians(a) for a in range(-34, 16, 5)]
    outer = [(px + HEAD_R * math.cos(a), pz + HEAD_R * math.sin(a)) for a in arc]
    inner = [(px + (HEAD_R - 0.11) * math.cos(a), pz + (HEAD_R - 0.11) * math.sin(a)) for a in reversed(arc)]
    profile = outer + inner
    p.loft([[Vector((x, y, z)) for x, z in profile] for y in (BY - 0.075, BY + 0.075)], YELLOW)
    # The equalizer under the back of the beam and the two pitman arms down to the crank pins.
    p.box((x0 + 0.02, BY - 0.21, pz - 0.11), (x0 + 0.1, BY + 0.21, pz - 0.065), STEEL)
    if variant == "intact":
        for y in (BY - 0.19, BY + 0.19):
            p.bar((x0 + 0.06, y, pz - 0.1), (CRANK_PIN[0], y, CRANK_PIN[1]), 0.016, STEEL)



def _gearbox(p, variant):
    cx, cz = CRANK
    if variant == "ruins":
        p.box((cx - 0.15, BY - 0.12, S + 0.02), (cx + 0.15, BY + 0.12, S + 0.3), DARK)
        for y in (BY - 0.3, BY + 0.34):
            cc.block(p, (cx + 0.05, y, S + 0.06), (0.26, 0.05, 0.16), (math.pi / 2, 0.0, 0.3), YELLOW)
        return
    p.box((cx - 0.15, BY - 0.12, S + 0.08), (cx + 0.15, BY + 0.12, cz - 0.02), DARK)
    p.cylinder((cx, BY, cz - 0.02), 'Y', 0.15, 0.12, DARK, sides=12)
    p.cylinder((cx, BY, cz), 'Y', 0.03, 0.22, STEEL, sides=8)  # the output shaft
    for y in (BY - 0.19, BY + 0.19):
        p.bar((cx, y, cz), (CRANK_PIN[0], y, CRANK_PIN[1]), 0.03, STEEL)  # the crank arm
        p.cylinder((CRANK_PIN[0], y, CRANK_PIN[1]), 'Y', 0.025, 0.03, DARK, sides=8)
        cc.block(p, (cx + 0.02, y, cz - 0.17), (0.28, 0.05, 0.16), (0.0, 0.25, 0.0), YELLOW)  # the counterweight


def _motor(p, variant):
    if variant == "ruins":
        cc.block(p, (-0.5, BY - 0.02, S + 0.1), (0.22, 0.16, 0.16), (0.0, 0.0, 0.5), DARK)
        return
    p.box((-0.6, BY - 0.1, S + 0.08), (-0.4, BY + 0.1, S + 0.12), STEEL)  # its mount
    p.cylinder((-0.5, BY, S + 0.22), 'X', 0.09, 0.1, DARK, sides=12)
    for x in (-0.56, -0.5, -0.44):
        p.cylinder((x, BY, S + 0.22), 'X', 0.1, 0.008, STEEL, sides=12)  # fins
    p.box((-0.44, BY + 0.12, S + 0.14), (-0.24, BY + 0.18, CRANK[1] - 0.05), YELLOW)  # the belt guard to the gearbox
    p.box((-0.75, BY - 0.04, S), (-0.66, BY + 0.04, S + 0.3), DARK)  # the switch box
    _tube(p, _bend([(-0.7, BY - 0.04, S + 0.02), (-0.7, -0.46, S + 0.02), (SHED[1] + 0.005, -0.58, S + 0.02)]), 0.012, DARK, sides=6)  # the conduit


def _well(p, variant):
    wx, wy = WELL
    p.cylinder((wx, wy, S + 0.02), 'Z', 0.09, 0.02, STEEL, sides=12)  # the flange
    if variant == "ruins":
        p.cylinder((wx, wy, S + 0.12), 'Z', 0.05, 0.1, PIPE, sides=10)
        return
    p.cylinder((wx, wy, S + 0.2), 'Z', 0.05, 0.18, PIPE, sides=10)  # the casing
    p.cylinder((wx, wy + 0.05, S + 0.3), 'Y', 0.038, 0.05, PIPE, sides=8)  # the tee
    p.box((wx - 0.05, wy - 0.12, S + 0.2), (wx + 0.05, wy - 0.04, S + 0.3), DARK)  # a valve
    if variant == "intact":
        _wheel(p, (wx, wy - 0.13, S + 0.25), 'Y')
    p.cylinder((wx, wy, S + 0.43), 'Z', 0.035, 0.05, DARK, sides=8)  # the stuffing box
    rod_top = 0.86 if variant == "intact" else 0.6
    p.cylinder((wx, wy, (S + 0.48 + rod_top) / 2), 'Z', 0.011, (rod_top - S - 0.48) / 2, STEEL, sides=6)  # the polished rod
    if variant == "intact":
        p.box((wx - 0.02, wy - 0.06, rod_top), (wx + 0.02, wy + 0.06, rod_top + 0.03), DARK)  # the carrier bar
        for y in (wy - 0.04, wy + 0.04):
            p.bar((wx + 0.005, y, rod_top + 0.03), (wx + 0.005, y, PIVOT.z), 0.005, DARK)  # the bridle cables


# --- the shed and the yard ------------------------------------------------------------

def _shed(p, team, variant):
    x0, x1, y0, y1 = SHED
    if variant == "ruins":
        for lo, hi in (((x0, y0, S), (x1, y0 + 0.05, S + 0.18)), ((x0, y0, S), (x0 + 0.05, y1, S + 0.25))):
            p.box(lo, hi, SHED)
        cc.block(p, (x0 + 0.25, y0 + 0.22, S + 0.12), (0.5, 0.42, 0.04), (0.35, 0.2, 0.3), ROOF)
        cc.block(p, (x0 + 0.3, y0 + 0.15, S + 0.05), (0.3, 0.2, 0.1), (0.1, 0.1, 0.7), SHED)
        return
    p.box((x0, y0, S), (x1, y1, SHED_TOP), SHED)
    o, top = 0.03, SHED_TOP + 0.16
    sag = 0.12 if variant == "abandoned" else 0.0
    eaves = [Vector((x0 - o, y0 - o, SHED_TOP)), Vector((x1 + o, y0 - o, SHED_TOP - sag)), Vector((x1 + o, y1 + o, SHED_TOP)),
             Vector((x0 - o, y1 + o, SHED_TOP))]
    ridge = [Vector((x0 + 0.12, y0 + 0.1, top)), Vector((x1 - 0.12, y0 + 0.1, top - sag * 1.5)), Vector((x1 - 0.12, y1 - 0.1, top)),
             Vector((x0 + 0.12, y1 - 0.1, top))]
    p.loft([eaves, ridge], ROOF)
    if variant == "intact":
        team.box((x0 + 0.13, y0 + 0.11, top - 0.005), (x1 - 0.13, y1 - 0.11, top + 0.02), TEAM)  # the top of the roof
    else:
        team.box((x0 + 0.13, y1 - 0.2, top - 0.005), (x0 + 0.24, y1 - 0.11, top + 0.02), TEAM)  # what is left of it
    lit = HOLE if variant == "abandoned" else GLASS
    p.box((x0 + 0.06, y0 - 0.006, S + 0.15), (x0 + 0.18, y0 + 0.01, S + 0.32), lit)  # a front window
    p.box((x0 + 0.26, y0 - 0.006, S), (x0 + 0.37, y0 + 0.01, S + 0.34), HOLE if variant == "abandoned" else DOOR)  # the door
    for y in (y0 + 0.06, y0 + 0.24):
        p.box((x1 - 0.01, y, S + 0.15), (x1 + 0.006, y + 0.12, S + 0.32), lit)  # the side windows
    p.box((x0 + 0.24, y0 - 0.1, S), (x0 + 0.39, y0, S + 0.03), CONCRETE)  # the step


def _mast(p, variant):
    mx, my = MAST
    p.box((mx - 0.05, my - 0.05, S), (mx + 0.05, my + 0.05, S + 0.03), STEEL)
    if variant == "ruins":
        p.bar((mx, my, S + 0.03), (mx + 1.2, my + 0.55, S + 0.03), 0.014, STEEL)
        return
    top = Vector((mx, my, S + 1.75)) if variant == "intact" else Vector((mx + 0.3, my - 0.1, S + 1.55))
    bend = Vector((mx, my, S + 1.0))
    p.bar((mx, my, S + 0.03), bend, 0.016, STEEL)
    p.bar(bend, top, 0.014, STEEL)
    p.bar(top - Vector((0.08, 0.0, 0.2)), top + Vector((0.08, 0.0, -0.2)), 0.008, STEEL)  # a crossbar
    p.sphere(top + Vector((0.0, 0.0, 0.02)), 'Z', 0.025, RED, sides=8, rings=4)  # the light
    for gx, gy in ((-0.62, -0.47), (-0.93, 0.0)):
        p.bar(bend + Vector((0.0, 0.0, 0.4)), (gx, gy, S + 0.01), 0.004, DARK)  # the guy wires


def _yard(p, variant):
    """Drums by the shed, a spare tank on saddles at the back right, a stack of pipes on sleepers at the front."""
    if variant == "intact":
        for x, y in ((-0.36, -0.8), (-0.22, -0.82), (-0.3, -0.68)):
            _drum(p, x, y)
    else:
        _drum(p, -0.36, -0.8)
        _drum(p, -0.2, -0.7, lying=0.4)
        _drum(p, -0.8, -0.04, lying=1.9)
    if variant != "ruins":
        _tube(p, [(0.24, 0.68, S + 0.21), (0.76, 0.68, S + 0.21)], 0.15, TANK, sides=14)  # the spare tank
        for x in (0.34, 0.66):
            p.box((x - 0.03, 0.56, S), (x + 0.03, 0.8, S + 0.1), STEEL)  # its saddles
    else:
        cc.block(p, (0.5, 0.65, S + 0.13), (0.5, 0.26, 0.26), (0.0, 0.0, 0.5), TANK)
    for x in (0.12, 0.58):
        p.box((x - 0.03, -0.86, S), (x + 0.03, -0.66, S + 0.04), WOOD)  # the sleepers
    for y, z in ((-0.82, 0.07), (-0.76, 0.07), (-0.7, 0.07), (-0.79, 0.12), (-0.73, 0.12)):
        _tube(p, [(0.05, y, S + z), (0.66, y, S + z)], 0.027, PIPE, sides=8)


def _rubble(p, variant):
    spots = (((-0.1, -0.6, S + 0.04), (0.16, 0.12, 0.08), 0.4), ((0.6, 0.15, S + 0.04), (0.14, 0.1, 0.07), 1.1))
    if variant == "ruins":
        spots += (((-0.3, 0.1, S + 0.05), (0.2, 0.16, 0.1), 0.2), ((0.1, 0.55, S + 0.05), (0.18, 0.14, 0.09), 0.9),
                  ((0.75, -0.6, S + 0.04), (0.15, 0.12, 0.08), 1.5))
    for c, s, t in spots:
        cc.block(p, c, s, (0.2, 0.1, t), CONCRETE)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Concrete, dark steel plate, light grey tank paint, blue-grey painted steel, dark machinery, safety yellow, grey-green pipes,
    a corrugated shed, a dark roof, warm lit glass, a dark door, a light team zone, red wheels, blue drums, wood, black holes; rust
    over the metal; cracks and soot when abandoned, soot in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    slabs = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 2.5)), -40.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 2.5)), -40.0, 1.5)))
    concrete = g.mix(slabs, g.mix(grain, lin((0.58, 0.57, 0.54)), lin((0.66, 0.65, 0.62))), lin((0.42, 0.41, 0.39)))
    tread = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 40.0)), -8.0, 1.6))
    deck = g.mix(tread, lin((0.26, 0.27, 0.28)), lin((0.34, 0.35, 0.36)))
    streak = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 2.5, -1.4))
    tank = g.mix(grain, lin((0.70, 0.71, 0.70)), lin((0.80, 0.81, 0.80)))
    tank = g.mix(g.math('MULTIPLY', streak, g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 11.0)), 3.0, -1.8))),
                 tank, lin((0.48, 0.38, 0.30)))  # rust streaks down the tank
    steel = g.mix(grain, lin((0.34, 0.38, 0.42)), lin((0.42, 0.46, 0.50)))
    dark = g.mix(grain, lin((0.14, 0.15, 0.16)), lin((0.21, 0.22, 0.23)))
    yellow = g.mix(grain, lin((0.80, 0.62, 0.10)), lin((0.88, 0.70, 0.16)))
    pipe = g.mix(grain, lin((0.40, 0.44, 0.38)), lin((0.48, 0.52, 0.46)))
    corrugated = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 45.0)), 0.5)), 4.0, -0.6))
    shed = g.mix(corrugated, lin((0.56, 0.58, 0.52)), lin((0.46, 0.48, 0.42)))
    roof = g.mix(grain, lin((0.22, 0.22, 0.24)), lin((0.28, 0.28, 0.30)))
    glass = g.mix(grain, lin((0.95, 0.86, 0.52)), lin((1.0, 0.92, 0.62)))
    door = g.mix(grain, lin((0.24, 0.28, 0.30)), lin((0.30, 0.34, 0.36)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    red = g.mix(grain, lin((0.70, 0.08, 0.06)), lin((0.80, 0.12, 0.08)))
    drum = g.mix(grain, lin((0.14, 0.26, 0.52)), lin((0.18, 0.32, 0.60)))
    wood = g.mix(g.noise(4.0, 6.0), lin((0.34, 0.24, 0.14)), lin((0.42, 0.30, 0.18)))
    hole = lin((0.03, 0.03, 0.03))
    albedo = cc.paint(g, mask, (concrete, deck, tank, steel, dark, yellow, pipe, shed, roof, glass, door, team, red, drum, wood, hole))
    metal_parts = g.math('ADD', g.math('ADD', g.math('ADD', mask[3], mask[4]), g.math('ADD', mask[5], mask[6])), mask[13])
    rust = g.math('MULTIPLY', metal_parts, g.clamp01(g.math('MULTIPLY_ADD', g.noise(8.0, 3.0), 5.0, -3.0)))
    albedo = g.mix(rust, albedo, lin((0.42, 0.22, 0.10)))
    if variant == "abandoned":
        albedo = g.mix(g.math('MULTIPLY', g.math('ADD', mask[0], mask[2]), cc.cracks(g, 7.0)), albedo, lin((0.12, 0.11, 0.10)))
        soot = g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.6, 2.0), 6.0, -2.8))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.85), albedo, lin((0.06, 0.05, 0.05)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.5, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.85), albedo, lin((0.06, 0.05, 0.05)))
    rough = cc.weighted(g, mask, (0.9, 0.6, 0.55, 0.5, 0.5, 0.5, 0.55, 0.6, 0.8, 0.2, 0.6, 0.45, 0.5, 0.5, 0.9, 1.0))
    rough = g.math('MULTIPLY_ADD', rust, 0.4, rough)
    metallic = cc.weighted(g, mask, (0.0, 0.7, 0.3, 0.4, 0.5, 0.3, 0.4, 0.3, 0.2, 0.0, 0.3, 0.0, 0.2, 0.3, 0.0, 0.0))
    metallic = g.math('MULTIPLY_ADD', rust, -0.4, metallic)
    normal = g.bump(g.math('MULTIPLY_ADD', mask[1], tread, g.math('MULTIPLY_ADD', mask[7], corrugated, g.noise(60.0, 3.0))), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    """The parts as meshes: body, beam (its origin moved to the pivot), team_mask, base; one packed UV layout, the material."""
    variant = VARIANTS[variant_id]
    cc.clear(variant_id, ("body", "beam", "beam_down", "team_mask", "base"))
    body, beam, team, base = Parts(), Parts(), Parts(), Parts()
    base.box((-0.95, -0.95, 0.0), (0.95, 0.95, S), CONCRETE)
    _skid(body)
    _tank(body, team, variant)
    _pipeline(body, variant)
    _post(body, variant)
    _gearbox(body, variant)
    _motor(body, variant)
    _well(body, variant)
    _shed(body, team, variant)
    _mast(body, variant)
    _yard(body, variant)
    if variant != "intact":
        _rubble(body, variant)
    if variant == "ruins":
        # The beam and the horse head down on the slab, in the body: nothing nods.
        _beam(beam, variant)
        bmesh.ops.transform(beam.bm, matrix=Matrix.Translation((0.05, 0.3, S + 0.08)) @ Matrix.Rotation(-0.35, 4, 'Z')
                            @ Matrix.Rotation(math.pi / 2, 4, 'X') @ Matrix.Translation(-PIVOT), verts=list(beam.bm.verts))
        parts = [(body, "body"), (beam, "beam_down"), (base, "base")]
    else:
        _beam(beam, variant)
        if variant == "abandoned":  # dropped head down
            bmesh.ops.transform(beam.bm, matrix=Matrix.Translation(PIVOT) @ Matrix.Rotation(0.42, 4, 'Y') @ Matrix.Translation(-PIVOT),
                                verts=list(beam.bm.verts))
        parts = [(body, "body"), (beam, "beam"), (team, "team_mask"), (base, "base")]
    meshes = [p.finish(f"{variant_id}_{name}") for p, name in parts]
    cc.drop_bottoms(meshes, S - 0.001)
    if variant == "ruins":
        # The fallen beam goes into the body.
        rc.select_only(meshes[:2])
        bpy.context.view_layer.objects.active = meshes[0]
        bpy.ops.object.join()
        meshes = [meshes[0], meshes[2]]
    else:
        beam_obj = meshes[1]
        beam_obj.data.transform(Matrix.Translation(-PIVOT))
        beam_obj.location = PIVOT  # the node's origin on the axis it nods about
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    cc.repack(meshes)
    mat, graph = _material(variant_id + "_mat", variant)
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, beam, team_mask and base (fire_1..3 when abandoned;
    body and base in the ruins)."""
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    nodes = rc.fire_nodes(FIRE_POINTS) if variant == "abandoned" else []
    names = [o.name.split(".")[0][len(variant_id) + 1:] for o in meshes]
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes)}, "
              + ", ".join(f"{n} {rc.tri_count(o)}" for n, o in zip(names, meshes))]
    cc.repack(meshes, cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material(meshes, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + nodes
    rc.arrange(objs, variant_id)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    with rc.node_names(objs, names + rc.fire_names(nodes)):
        rc.save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
