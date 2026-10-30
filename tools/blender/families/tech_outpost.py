"""Tech outpost, tech_outpost (docs/ra2_buildings/tech/outpost, docs/map_elements/12_tech_buildings): the neutral outpost an engineer
takes; its owner gets a missile turret, a pad that mends his vehicles beside it and more ground to build on (game/data/neutral/tech.yaml).
The kinds of parts follow the owner's picture of RA2's outpost (2026-10-30): an armoured block with a missile launcher on its roof, an
orange working arm, a round repair pad with a hazard ring; the shapes and the layout are ours (generation_requirements.md, section 8).

Footprint 4x3 cells, origin at the centre of the footprint on the ground, the front to -Y; made at the size the game draws it. The parts:
    pad         a concrete slab over the footprint in plates (the base node); the repair pad at the front right: a steel disc with a
                yellow and black ring, a cross of guide strips, lights round it, two lamp posts
    block       at the back left: a low armoured tier with a sloped front, a vehicle bay door in a white frame with hazard stripes over
                it, panel seams and vents; an upper tier with slanted plates and a row of slit windows; the turret's ring on its roof;
                a mast with a red light, a ladder, pipes along the walls
    plant       at the back right: two tanks lying on cradles, a generator with a fan grille, a cable reel, crates and drums
    turret      on the upper tier: a turntable, a post, a yoke and two launch pods of four tubes tilted up, a small sensor dish; it
                turns about its Y, its origin on the ring's axis; at rest it looks to the front left
    arm         an orange jointed arm on a pedestal right of the block, reaching over the repair pad: arm (the column, turns about
                its Y), arm_upper (the boom, from the shoulder), arm_fore (the forearm and the tool head, from the elbow); hydraulic
                rams on the boom
    team mask   the bands round the pods (team_mask_turret, under turret), a stripe round the upper tier and the flag at the front left
Nodes: "body", "turret" (with "team_mask_turret"), "arm" > "arm_upper" > "arm_fore", "team_mask", "base". The game turns the turret
to its target and works the arm while the outpost mends a vehicle (Buildings, as PlayerBuildings does for the depots). One GLB per state:
    tech_outpost            whole
    tech_outpost_abandoned  critically damaged: holes in the armour, the bay door buckled, windows out, the mast bent, one tank
                            burst, one pod torn open, the arm's tool head hanging, crates thrown about, soot; fire on the block, the
                            plant and the turret's roof (fire_1..3)
    tech_outpost_ruins      destroyed: the stump of the lower tier, the launcher fallen on the slab, the arm down across the pad,
                            heaps, the pad
Budget 8000 triangles, texture 2048 (docs/map_elements/12_tech_buildings/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.tech_outpost as to
    print(to.run_all())
"""

import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin, uv_together

SIZE = 2048
VARIANTS = {
    "tech_outpost": "intact",
    "tech_outpost_abandoned": "abandoned",
    "tech_outpost_ruins": "ruins",
}

(CONCRETE, ARMOUR, STEEL, DARK, GLASS, WHITE, HAZARD, ORANGE, RED, WOOD, DRUM, TEAM, LIGHT, POD) = (cc.part(i) for i in range(14))
PARTS = 14

PAD = 0.06
LOW = (-1.92, 0.22, -0.32, 1.42)  # x0, x1, y0, y1 of the lower tier
LOW_TOP = PAD + 0.56
HIGH = (-1.72, -0.32, 0.12, 1.3)  # the upper tier
HIGH_TOP = LOW_TOP + 0.48
RING = Vector((-1.02, 0.7, HIGH_TOP))  # the turret's ring: the turret's origin
REST_YAW = math.radians(140)  # the turret at rest looks to the front left
TURRET_SCALE = 1.35  # the launcher drawn larger than it is built, to read from afar
POD_X = (-0.23, 0.23)
TILT = math.radians(28)
TRUNNION = 0.2  # over the ring
REPAIR = Vector((1.05, -0.48, PAD))  # the middle of the repair pad
REPAIR_R = 0.6
ARM = Vector((0.5, 0.72, PAD))  # the foot of the arm's column
SHOULDER = 0.42  # over the foot
UPPER = Vector((0.5, 0.0, 0.48))  # the elbow from the shoulder, in the arm's plane (+x out)
FORE = Vector((0.78, 0.0, -0.36))  # the wrist from the elbow
FIRE_POINTS = ((-0.6, 0.0, LOW_TOP), (1.4, 1.1, PAD + 0.45), (-1.3, 0.4, HIGH_TOP + 0.05))


def _pad(base, body, variant):
    base.box((-1.98, -1.48, 0.0), (1.98, 1.48, PAD), CONCRETE)
    if variant == "ruins":
        body.turned(REPAIR, 'Z', [(0.0, REPAIR_R), (0.008, REPAIR_R)], DARK, sides=28)
        return
    # The repair pad: a ring of hazard paint, a steel disc in it, guide strips across, lights round.
    body.turned(REPAIR, 'Z', [(0.0, REPAIR_R), (0.012, REPAIR_R)], HAZARD, sides=32)
    body.turned(REPAIR, 'Z', [(0.0, REPAIR_R - 0.1), (0.016, REPAIR_R - 0.1)], STEEL, sides=32)
    for a in (0.0, math.pi / 2):
        d = Vector((math.cos(a), math.sin(a), 0.0)) * (REPAIR_R - 0.16)
        body.bar(REPAIR + d + Vector((0, 0, 0.018)), REPAIR - d + Vector((0, 0, 0.018)), 0.018, WHITE)
    for k in range(8):
        a = math.pi / 4 * k + math.pi / 8
        c = REPAIR + Vector((math.cos(a), math.sin(a), 0.0)) * (REPAIR_R + 0.05)
        body.box((c.x - 0.025, c.y - 0.025, PAD), (c.x + 0.025, c.y + 0.025, PAD + 0.025), LIGHT)
    for x, y in ((0.35, -1.25), (1.8, -1.25)):
        lamp = variant == "intact" or x < 1.0
        body.cylinder((x, y, PAD + 0.25), 'Z', 0.015, 0.25, STEEL, sides=6)
        if lamp:
            body.box((x - 0.05, y - 0.03, PAD + 0.48), (x + 0.05, y + 0.03, PAD + 0.53), LIGHT)
        else:
            cc.block(body, (x - 0.1, y + 0.1, PAD + 0.02), (0.1, 0.06, 0.05), (0.0, 0.0, 0.5), LIGHT)
    # Plate seams in the slab before the bay door, a drive in from the front.
    for x in (-1.5, -0.7):
        body.box((x - 0.012, -1.45, PAD), (x + 0.012, LOW[2], PAD + 0.004), DARK)


def _block(p, team, variant):
    x0, x1, y0, y1 = LOW
    if variant == "ruins":
        # The floor of the lower tier, jagged stumps of its walls round it, the upper tier fallen in a heap of plates, bent rods.
        p.box((x0, y0, PAD), (x1, y1, PAD + 0.08), STEEL)
        rnd = random.Random(5)
        edges = [((x, y0 + 0.04), 0.0) for x in cc.steps(x0 + 0.1, x1 - 0.1, 0.22)]
        edges += [((x, y1 - 0.04), 0.0) for x in cc.steps(x0 + 0.1, x1 - 0.1, 0.22)]
        edges += [((x0 + 0.04, y), math.pi / 2) for y in cc.steps(y0 + 0.2, y1 - 0.2, 0.22)]
        for (x, y), turn in edges:
            h = rnd.uniform(0.05, 0.34)
            if rnd.random() < 0.25:
                continue
            cc.block(p, (x, y, PAD + 0.08 + h / 2), (0.22, 0.08, h), (rnd.uniform(-0.15, 0.15), rnd.uniform(-0.1, 0.1), turn), ARMOUR)
        for c, s, t in (((-1.0, 0.7, PAD + 0.2), (0.9, 0.7, 0.16), (0.12, 0.2, 0.3)), ((-1.3, 0.3, PAD + 0.3), (0.5, 0.45, 0.1), (0.4, 0.1, 1.0)),
                        ((-0.6, 1.0, PAD + 0.26), (0.6, 0.3, 0.1), (0.0, 0.35, 0.2)), ((-0.4, 0.1, PAD + 0.14), (0.5, 0.3, 0.1), (0.2, 0.0, 0.7))):
            cc.block(p, c, s, t, ARMOUR)
        for a, b in (((-0.9, 0.2, PAD + 0.1), (-0.8, 0.0, PAD + 0.5)), ((-1.5, 1.1, PAD + 0.1), (-1.3, 1.2, PAD + 0.45)), ((-0.2, 0.6, PAD + 0.1), (0.0, 0.5, PAD + 0.38))):
            p.bar(a, b, 0.008, DARK)  # bent rods
        p.box((-1.55, y0 - 0.04, PAD), (-0.65, y0 + 0.03, PAD + 0.06), HAZARD)  # the lintel of the bay, down
        return
    # The lower tier: its front leans back; a skirt of darker plates round its foot.
    slope = 0.14
    p.loft([[Vector(v) for v in ((x0, y0, PAD), (x1, y0, PAD), (x1, y1, PAD), (x0, y1, PAD))],
            [Vector(v) for v in ((x0, y0 + slope, LOW_TOP), (x1, y0 + slope, LOW_TOP), (x1, y1, LOW_TOP), (x0, y1, LOW_TOP))]], ARMOUR)
    p.box((x0 - 0.02, y0 - 0.02, PAD), (x1 + 0.02, y1 + 0.02, PAD + 0.07), STEEL)
    p.box((x0 - 0.01, y0 + slope - 0.02, LOW_TOP - 0.01), (x1 + 0.01, y1 + 0.01, LOW_TOP + 0.03), STEEL)  # the coping
    # The vehicle bay: a dark door in a white frame, hazard stripes over it.
    bx0, bx1, top = -1.55, -0.65, PAD + 0.32
    lean = lambda z: y0 + slope * (z - PAD) / (LOW_TOP - PAD) - 0.012  # the face of the sloped front at a height
    door = [Vector((bx0, lean(PAD), PAD)), Vector((bx1, lean(PAD), PAD)), Vector((bx1, lean(top), top)), Vector((bx0, lean(top), top))]
    p.loft([door, [v + Vector((0, 0.01, 0)) for v in door]], DARK if variant == "intact" else STEEL)
    for k, z in enumerate(cc.steps(PAD + 0.04, top - 0.03, 0.05)):
        p.box((bx0 + 0.02, lean(z) - 0.004, z), (bx1 - 0.02, lean(z) + 0.002, z + 0.012), STEEL)  # the door's slats
    for x in (bx0 - 0.04, bx1):
        p.loft([[Vector((x, lean(z) - 0.01, z)), Vector((x + 0.04, lean(z) - 0.01, z)), Vector((x + 0.04, lean(z) + 0.02, z)),
                 Vector((x, lean(z) + 0.02, z))] for z in (PAD, top + 0.04)], WHITE)
    p.loft([[Vector((x, lean(z) - 0.012, z)) for x in (bx0 - 0.04, bx1 + 0.04)] + [Vector((x, lean(z) + 0.02, z)) for x in (bx1 + 0.04, bx0 - 0.04)]
            for z in (top, top + 0.09)], HAZARD)
    if variant == "abandoned":
        cc.block(p, (-1.1, y0 - 0.12, PAD + 0.1), (0.5, 0.05, 0.22), (0.5, 0.2, 0.15), STEEL)  # a buckled leaf of the door
    # Panel seams and vents on the front and the sides.
    for x in cc.steps(x0 + 0.15, x1 - 0.1, 0.42):
        if bx0 - 0.08 < x < bx1 + 0.08:
            continue
        p.loft([[Vector((x - 0.008, lean(z) - 0.006, z)), Vector((x + 0.008, lean(z) - 0.006, z)), Vector((x + 0.008, lean(z) + 0.01, z)),
                 Vector((x - 0.008, lean(z) + 0.01, z))] for z in (PAD + 0.08, LOW_TOP - 0.03)], DARK)
    for y in (0.3, 0.8):
        p.box((x0 - 0.02, y - 0.12, PAD + 0.18), (x0 + 0.01, y + 0.12, PAD + 0.32), DARK)
        for z in cc.steps(PAD + 0.2, PAD + 0.3, 0.03):
            p.box((x0 - 0.03, y - 0.11, z), (x0 - 0.01, y + 0.11, z + 0.008), STEEL)
    p.box((-0.3, y0 + slope * 0.4 - 0.02, PAD + 0.2), (0.05, y0 + slope * 0.4, PAD + 0.32), GLASS if variant == "intact" else DARK)
    # The upper tier: slanted plates on its front, slit windows, a team stripe round it.
    hx0, hx1, hy0, hy1 = HIGH
    p.loft([[Vector(v) for v in ((hx0, hy0, LOW_TOP), (hx1, hy0, LOW_TOP), (hx1, hy1, LOW_TOP), (hx0, hy1, LOW_TOP))],
            [Vector(v) for v in ((hx0 + 0.06, hy0 + 0.12, HIGH_TOP), (hx1 - 0.06, hy0 + 0.12, HIGH_TOP), (hx1 - 0.06, hy1 - 0.04, HIGH_TOP),
                                 (hx0 + 0.06, hy1 - 0.04, HIGH_TOP))]], ARMOUR)
    glass = GLASS if variant == "intact" else DARK
    for x in cc.steps(hx0 + 0.2, hx1 - 0.2, 0.17):
        z = LOW_TOP + 0.24
        y = hy0 + 0.12 * (z - LOW_TOP) / (HIGH_TOP - LOW_TOP) - 0.008
        p.box((x - 0.05, y - 0.006, z), (x + 0.05, y + 0.012, z + 0.045), glass)
    team.loft([[Vector(v) for v in ((hx0 - 0.005, hy0 - 0.005, z), (hx1 + 0.005, hy0 - 0.005, z), (hx1 + 0.005, hy1 + 0.005, z),
                                    (hx0 - 0.005, hy1 + 0.005, z))] for z in (LOW_TOP + 0.05, LOW_TOP + 0.11)], TEAM)
    # The turret's ring on the roof.
    p.turned(RING, 'Z', [(-0.01, 0.34), (0.04, 0.34), (0.04, 0.28)], STEEL, sides=24)
    # The mast at the back with its red light, bent when abandoned.
    foot = Vector((hx0 + 0.15, hy1 - 0.12, HIGH_TOP))
    tip = foot + (Vector((0.0, 0.0, 0.55)) if variant == "intact" else Vector((0.25, -0.1, 0.35)))
    p.bar(foot, tip, 0.014, STEEL)
    p.bar(foot + (tip - foot) * 0.6 + Vector((-0.08, 0, 0)), foot + (tip - foot) * 0.6 + Vector((0.08, 0, 0)), 0.008, STEEL)
    p.sphere(tip, 'Z', 0.03, RED, sides=8, rings=4)
    # A ladder up the right side of the lower tier, pipes along the back.
    for y in (0.95, 1.12):
        p.bar((x1 + 0.02, y, PAD), (x1 + 0.02, y, LOW_TOP + 0.08), 0.008, STEEL)
    for z in cc.steps(PAD + 0.08, LOW_TOP, 0.07):
        p.bar((x1 + 0.02, 0.95, z), (x1 + 0.02, 1.12, z), 0.005, STEEL)
    for z, r in ((PAD + 0.14, 0.022), (PAD + 0.22, 0.016)):
        p.cylinder(((x0 + x1) / 2, y1 + 0.03, z), 'X', r, (x1 - x0) / 2 - 0.05, STEEL, sides=8)
    if variant == "abandoned":
        # Holes torn in the armour: dark patches with bent plates.
        for c, s, t in (((-0.3, y0 + 0.1, PAD + 0.36), (0.26, 0.04, 0.16), (0.3, 0.0, 0.2)), ((-1.75, 0.4, HIGH_TOP - 0.05), (0.2, 0.3, 0.04), (0.2, 0.1, 0.0))):
            cc.block(p, c, s, t, DARK)
        cc.block(p, (-0.2, y0 - 0.06, PAD + 0.42), (0.2, 0.03, 0.12), (0.9, 0.0, 0.4), ARMOUR)


def _plant(p, variant):
    """At the back right: two tanks on cradles, a generator, a cable reel, crates and drums."""
    if variant == "ruins":
        # One tank squashed on its cradles, the other rolled off and split; the bare frame of the generator.
        for x in (0.9, 1.65):
            p.box((x - 0.04, 1.18, PAD), (x + 0.04, 1.42, PAD + 0.1), DARK)
        squash = Matrix.Translation((1.27, 1.3, PAD + 0.17)) @ Matrix.Scale(0.55, 4, Vector((0, 0, 1)))
        tank = Parts()
        tank.turned((0.0, 0.0, 0.0), 'X', [(-0.5, 0.11), (-0.46, 0.15), (0.46, 0.15), (0.5, 0.11)], STEEL, sides=12)
        bmesh.ops.transform(tank.bm, matrix=squash, verts=list(tank.bm.verts))
        _copy(tank, p)
        cc.block(p, (1.2, 0.85, PAD + 0.12), (0.7, 0.24, 0.2), (0.0, 0.0, 0.25), STEEL)
        cc.block(p, (1.45, 0.82, PAD + 0.16), (0.2, 0.05, 0.18), (0.6, 0.2, 0.9), STEEL)
        for x in (1.47, 1.88):
            for y in (0.34, 0.7):
                p.bar((x, y, PAD), (x + 0.03, y, PAD + 0.22), 0.012, DARK)
        p.bar((1.47, 0.34, PAD + 0.22), (1.88, 0.34, PAD + 0.2), 0.01, DARK)
        for k, (x, y) in enumerate(((1.8, -0.1), (0.3, 1.25), (1.9, 1.05))):
            cc.block(p, (x, y, PAD + 0.04), (0.12, 0.1, 0.07), (0.3, 0.2, 0.8 * k), WOOD if k else DRUM)
        return
    for k, y in enumerate((0.98, 1.3)):
        burst = variant == "abandoned" and k == 0
        for x in (0.9, 1.65):
            p.box((x - 0.04, y - 0.12, PAD), (x + 0.04, y + 0.12, PAD + 0.1), DARK)  # a cradle
        if burst:
            p.turned((1.27, y, PAD + 0.24), 'X', [(-0.5, 0.13), (-0.48, 0.15), (0.05, 0.15), (0.1, 0.11)], STEEL, sides=14)
            cc.block(p, (1.5, y - 0.1, PAD + 0.18), (0.3, 0.05, 0.2), (0.4, 0.3, -0.5), STEEL)  # its end torn off
        else:
            p.turned((1.27, y, PAD + 0.24), 'X', [(-0.52, 0.0), (-0.5, 0.11), (-0.46, 0.15), (0.46, 0.15), (0.5, 0.11), (0.52, 0.0)], STEEL, sides=14)
            for x in (0.95, 1.6):
                p.turned((x, y, PAD + 0.24), 'X', [(-0.015, 0.155), (0.015, 0.155)], DARK, sides=14)
    p.bar((0.75, 0.98, PAD + 0.3), (0.3, 0.98, PAD + 0.3), 0.02, STEEL)  # a pipe from the tanks to the block
    p.bar((0.3, 0.98, PAD + 0.3), (0.25, 0.98, PAD + 0.3), 0.03, DARK)
    # The generator with a fan grille on its top.
    p.box((1.45, 0.32, PAD), (1.9, 0.72, PAD + 0.28), ARMOUR)
    p.box((1.44, 0.31, PAD), (1.91, 0.73, PAD + 0.05), STEEL)
    p.turned((1.67, 0.52, PAD + 0.28), 'Z', [(0.0, 0.15), (0.02, 0.15), (0.02, 0.12)], DARK, sides=16)
    for a in (0.2, 0.2 + math.pi / 3, 0.2 + 2 * math.pi / 3):
        d = Vector((math.cos(a), math.sin(a), 0.0)) * 0.12
        p.bar(Vector((1.67, 0.52, PAD + 0.3)) - d, Vector((1.67, 0.52, PAD + 0.3)) + d, 0.006, STEEL)
    p.box((1.44, 0.4, PAD + 0.08), (1.45, 0.62, PAD + 0.2), DARK)
    # A cable reel by the pad, crates and drums.
    p.turned((0.38, -0.2, PAD + 0.11), 'Y', [(-0.06, 0.1), (-0.05, 0.1), (-0.05, 0.05), (0.05, 0.05), (0.05, 0.1), (0.06, 0.1)], DARK, sides=12)
    p.turned((0.38, -0.2, PAD + 0.11), 'Y', [(-0.048, 0.08), (0.048, 0.08)], HAZARD, sides=12)
    crates = ((1.85, -0.05), (1.85, 0.12), (1.72, 0.03))
    for k, (x, y) in enumerate(crates):
        if variant == "intact":
            p.box((x - 0.06, y - 0.06, PAD), (x + 0.06, y + 0.06, PAD + 0.11), WOOD)
        else:
            cc.block(p, (x - 0.15 * k, y - 0.2 * k, PAD + 0.05), (0.12, 0.12, 0.1), (0.3 * k, 0.1, 0.6 * k), WOOD)
    for x, y in ((0.4, 1.3), (0.4, 1.17)):
        p.turned((x, y, PAD), 'Z', [(0.0, 0.045), (0.13, 0.045)], DRUM, sides=8)


def _booth(p, variant):
    """A guard booth at the front left, by the drive to the bay: a small block with a band of windows under a flat roof."""
    x0, x1, y0, y1, top = -1.9, -1.58, -1.4, -1.0, PAD + 0.4
    if variant == "ruins":
        p.box((x0, y0, PAD), (x1, y1, PAD + 0.12), ARMOUR)
        return
    p.box((x0, y0, PAD), (x1, y1, top), ARMOUR)
    glass = GLASS if variant == "intact" else DARK
    p.box((x0 + 0.03, y0 - 0.01, PAD + 0.2), (x1 - 0.03, y0, PAD + 0.33), glass)
    p.box((x1, y0 + 0.04, PAD + 0.2), (x1 + 0.01, y1 - 0.04, PAD + 0.33), glass)
    p.box((x0 - 0.03, y0 - 0.03, top), (x1 + 0.03, y1 + 0.03, top + 0.04), STEEL)
    p.box((x1, y1 - 0.14, PAD), (x1 + 0.012, y1 - 0.03, PAD + 0.18), DARK)  # its door
    # The barrier arm across the drive, up when abandoned.
    p.box((x1 + 0.02, y0 + 0.02, PAD), (x1 + 0.08, y0 + 0.08, PAD + 0.16), HAZARD)
    end = Vector((-0.7, y0 + 0.05, PAD + 0.15)) if variant == "intact" else Vector((x1 + 0.3, y0 + 0.05, PAD + 0.6))
    p.bar((x1 + 0.05, y0 + 0.05, PAD + 0.15), end, 0.012, HAZARD)


def _flag(team, variant):
    if variant == "ruins":
        return
    x, y, top = -1.86, -0.62, 1.3
    team.cylinder((x, y, (PAD + top) / 2), 'Z', 0.015, (top - PAD) / 2, WHITE, sides=6)
    if variant == "abandoned":
        cc.block(team, (x + 0.12, y, top - 0.12), (0.22, 0.01, 0.16), (0.0, -0.25, 0.0), TEAM)
    else:
        team.loft([[Vector((x + 0.02, y + d, top - 0.02)), Vector((x + 0.42, y + d + 0.03, top - 0.04)), Vector((x + 0.42, y + d + 0.03, top - 0.28)),
                    Vector((x + 0.02, y + d, top - 0.26))] for d in (-0.006, 0.006)], TEAM)


def _pod(t, x, torn):
    """A launch pod at x on the trunnion: a box tilted up, four tubes in its face, its cap at the back; in the turret's space."""
    rot = Matrix.Rotation(TILT, 3, 'X')
    o = Vector((x, 0.0, TRUNNION))
    corners = [(-0.1, -0.1), (0.1, -0.1), (0.1, 0.1), (-0.1, 0.1)]
    t.loft([[o + rot @ Vector((cx, y, cz)) for cx, cz in corners] for y in (-0.22, 0.2)], POD)
    if torn:
        cc.block(t, o + rot @ Vector((0.05, 0.22, 0.06)), (0.12, 0.03, 0.1), (0.6, 0.0, 0.4), POD)
        return
    for cx in (-0.048, 0.048):
        for cz in (-0.048, 0.048):
            c = o + rot @ Vector((cx, 0.2, cz))
            d = rot @ Vector((0.0, 1.0, 0.0))
            ring = [c + rot @ Vector((0.036 * math.cos(2 * math.pi * i / 10), 0.0, 0.036 * math.sin(2 * math.pi * i / 10))) for i in range(10)]
            t.loft([[q + d * 0.002 for q in ring], [q + d * 0.012 for q in ring]], DARK)
    t.loft([[o + rot @ Vector((cx * 1.04, y, cz * 1.04)) for cx, cz in corners] for y in (-0.24, -0.2)], STEEL)


def _turret(variant):
    """The launcher as its own node: origin on the ring's axis, its front along +Y; the bands of the pods under it."""
    t = Parts()
    t.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.27), (0.06, 0.27), (0.06, 0.2), (0.1, 0.18)], STEEL, sides=20)
    t.box((-0.05, -0.08, 0.08), (0.05, 0.08, TRUNNION + 0.02), ARMOUR)  # the post
    for x in (-0.03, 0.03):
        t.box((x - 0.02, -0.06, TRUNNION - 0.05), (x + 0.02, 0.06, TRUNNION + 0.05), DARK)  # the yoke
    t.turned(Vector((0.0, 0.0, TRUNNION)), 'X', [(-0.26, 0.032), (0.26, 0.032)], DARK, sides=12)
    for k, x in enumerate(POD_X):
        _pod(t, x, variant == "abandoned" and k == 1)
    # A small sensor dish behind the pods, an aerial.
    t.bar((0.0, -0.1, 0.1), (0.0, -0.18, 0.3), 0.01, STEEL)
    t.turned((0.0, -0.19, 0.32), 'Y', [(-0.01, 0.0), (0.0, 0.07), (0.02, 0.08)], WHITE, sides=12)
    turret = t.finish("turret_tmp")

    h = Parts()
    rot = Matrix.Rotation(TILT, 3, 'X')
    for x in POD_X:
        o = Vector((x, 0.0, TRUNNION))
        corners = [(-0.104, -0.104), (0.104, -0.104), (0.104, 0.104), (-0.104, 0.104)]
        h.loft([[o + rot @ Vector((cx, y, cz)) for cx, cz in corners] for y in (-0.08, 0.0)], TEAM)
    bands = h.finish("bands_tmp")
    for obj in (turret, bands):
        obj.data.transform(Matrix.Scale(TURRET_SCALE, 4))
    return turret, bands


def _arm(variant):
    """The jointed arm in three nodes, each built in its own space (the arm's plane is its x-z, out along +x)."""
    col, up, fore = Parts(), Parts(), Parts()
    # The column on its pedestal, the shoulder's hinge on top.
    col.box((-0.14, -0.14, 0.0), (0.14, 0.14, 0.06), STEEL)
    col.turned((0.0, 0.0, 0.0), 'Z', [(0.06, 0.1), (0.1, 0.1), (0.1, 0.08), (SHOULDER - 0.02, 0.07)], ORANGE, sides=14)
    col.turned((0.0, 0.0, SHOULDER), 'Y', [(-0.07, 0.06), (0.07, 0.06)], DARK, sides=12)
    for y in (-0.06, 0.06):
        col.box((-0.05, y - 0.012, SHOULDER - 0.12), (0.05, y + 0.012, SHOULDER + 0.02), ORANGE)
    # The boom: a box beam up to the elbow, a ram under it.
    up.bar((0.0, 0.0, 0.0), UPPER, 0.05, ORANGE)
    up.turned(UPPER, 'Y', [(-0.055, 0.045), (0.055, 0.045)], DARK, sides=12)
    up.bar((0.06, 0.0, -0.12), UPPER * 0.6 + Vector((0.02, 0.0, -0.03)), 0.014, STEEL)  # the ram
    up.bar((0.06, 0.0, -0.12), (0.2, 0.0, 0.04), 0.022, DARK)
    # The forearm down to the wrist, the tool head hanging under it.
    fore.bar((0.0, 0.0, 0.0), FORE, 0.04, ORANGE)
    fore.bar((0.1, 0.0, 0.05), FORE * 0.7 + Vector((0.0, 0.0, 0.04)), 0.01, STEEL)
    head = FORE + (Vector((0.0, 0.0, -0.08)) if variant == "intact" else Vector((0.05, 0.03, -0.12)))
    fore.turned(FORE, 'Y', [(-0.04, 0.035), (0.04, 0.035)], DARK, sides=10)
    fore.bar(FORE, head, 0.02, STEEL)
    fore.turned(head, 'Z', [(0.0, 0.05), (-0.05, 0.05), (-0.08, 0.025)], DARK, sides=10)
    for a in (0.0, math.pi * 2 / 3, math.pi * 4 / 3):
        d = Vector((math.cos(a), math.sin(a), 0.0)) * 0.04
        fore.bar(head + d + Vector((0, 0, -0.06)), head + d * 1.3 + Vector((0, 0, -0.13)), 0.007, STEEL)  # the claws
    return col.finish("arm_tmp"), up.finish("arm_upper_tmp"), fore.finish("arm_fore_tmp")


def _arm_down(p):
    """The ruins: the arm fallen across the pad, in the body."""
    a = ARM + Vector((0.05, -0.1, 0.05))
    p.box((ARM.x - 0.14, ARM.y - 0.14, PAD), (ARM.x + 0.14, ARM.y + 0.14, PAD + 0.06), STEEL)
    b = Vector((0.75, -0.3, PAD + 0.06))
    c = Vector((1.35, -0.75, PAD + 0.04))
    p.bar(a, b, 0.04, ORANGE)
    p.bar(b, c, 0.03, ORANGE)
    cc.block(p, (1.0, 0.4, PAD + 0.08), (0.5, 0.18, 0.16), (0.0, 0.3, 0.9), POD)  # the launcher fallen off the block
    cc.block(p, (0.85, 0.2, PAD + 0.05), (0.3, 0.3, 0.1), (0.2, 0.1, 0.3), STEEL)


def _copy(src, dst):
    """Copies the faces of one Parts into another, part by part."""
    for face in src.bm.faces:
        new = dst.bm.faces.new([dst.bm.verts.new(v.co) for v in face.verts])
        for loop, old in zip(new.loops, face.loops):
            loop[dst.layer] = old[src.layer]


def _rubble(p, variant):
    spots = (((0.2, -0.9, PAD + 0.05), (0.2, 0.15, 0.1), 0.4), ((-1.2, -1.0, PAD + 0.04), (0.18, 0.14, 0.08), 1.1))
    if variant == "ruins":
        spots += (((-0.3, -0.6, PAD + 0.06), (0.3, 0.2, 0.12), 0.3), ((1.6, -0.2, PAD + 0.06), (0.22, 0.2, 0.12), 0.9),
                  ((-1.6, 0.9, PAD + 0.25), (0.3, 0.25, 0.14), 0.5))
    for c, s, t in spots:
        cc.block(p, c, s, (0.2, 0.1, t), ARMOUR)


def _material(name, variant):
    """Concrete in plates, olive-grey armour, steel, dark iron, blue-green glass, white, yellow and black hazard stripes, the orange of
    the arm, red, wood, olive drums, the light team zone, warm lamps, the launcher's grey; soot when abandoned, in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    plates = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 2.0)), -40.0, 1.5)),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 2.0)), -40.0, 1.5)))
    stain = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 3.0, -1.7))
    concrete = g.mix(plates, g.mix(grain, lin((0.58, 0.57, 0.54)), lin((0.66, 0.65, 0.62))), lin((0.40, 0.39, 0.37)))
    concrete = g.mix(g.math('MULTIPLY', stain, 0.5), concrete, lin((0.33, 0.31, 0.28)))
    seams = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, z), 3.0)), -30.0, 1.0))
    armour = g.mix(g.noise(6.0, 3.0), lin((0.40, 0.42, 0.36)), lin((0.48, 0.50, 0.43)))
    armour = g.mix(g.math('MULTIPLY', seams, 0.6), armour, lin((0.24, 0.25, 0.22)))
    armour = g.mix(g.math('MULTIPLY', stain, 0.4), armour, lin((0.28, 0.27, 0.24)))
    steel = g.mix(grain, lin((0.50, 0.52, 0.54)), lin((0.60, 0.62, 0.64)))
    dark = g.mix(grain, lin((0.08, 0.08, 0.09)), lin((0.14, 0.14, 0.15)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.8)), lin((0.12, 0.24, 0.30)), lin((0.34, 0.52, 0.58)))
    white = g.mix(grain, lin((0.86, 0.86, 0.84)), lin((0.93, 0.93, 0.91)))
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 8.0)), 0.5)), -40.0, 10.5))
    hazard = g.mix(stripes, lin((0.06, 0.06, 0.06)), lin((0.90, 0.72, 0.10)))
    orange = g.mix(grain, lin((0.86, 0.36, 0.06)), lin((0.94, 0.44, 0.10)))
    orange = g.mix(g.math('MULTIPLY', stain, 0.35), orange, lin((0.30, 0.16, 0.06)))
    red = g.mix(grain, lin((0.70, 0.10, 0.06)), lin((0.80, 0.14, 0.08)))
    wood = g.mix(g.noise(5.0, 5.0), lin((0.52, 0.38, 0.22)), lin((0.62, 0.46, 0.28)))
    drum = g.mix(grain, lin((0.30, 0.34, 0.20)), lin((0.36, 0.40, 0.24)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    light = lin((0.98, 0.86, 0.50))
    pod = g.mix(grain, lin((0.62, 0.64, 0.66)), lin((0.72, 0.74, 0.76)))
    albedo = cc.paint(g, mask, (concrete, armour, steel, dark, glass, white, hazard, orange, red, wood, drum, team, light, pod))
    if variant != "intact":
        soot = g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.8, 2.0), 6.0, -2.9 if variant == "abandoned" else -2.4))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.07, 0.06, 0.06)))
    rough = cc.weighted(g, mask, (0.9, 0.6, 0.45, 0.6, 0.1, 0.6, 0.6, 0.5, 0.6, 0.85, 0.5, 0.45, 0.3, 0.45))
    metallic = cc.weighted(g, mask, (0.0, 0.4, 0.7, 0.3, 0.0, 0.1, 0.1, 0.3, 0.1, 0.0, 0.4, 0.0, 0.0, 0.5))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[1], g.math('MULTIPLY', seams, -1.0), g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', plates, -1.0),
                                                                                         g.noise(60.0, 3.0))), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def _hang(obj, parent, origin, name, yaw=0.0):
    """Makes obj a node at origin in its parent's space (turned about its Y — Blender's Z — by yaw), its mesh already in its own space."""
    obj.name = name
    obj.data.name = name
    obj.parent = parent
    obj.matrix_parent_inverse = Matrix.Identity(4)
    obj.location = origin
    obj.rotation_euler = (0.0, 0.0, yaw)


def build(variant_id):
    variant = VARIANTS[variant_id]
    cc.clear(variant_id, ("body", "team_mask", "base", "turret", "team_mask_turret", "arm", "arm_upper", "arm_fore"))
    body, team, base = Parts(), Parts(), Parts()
    _pad(base, body, variant)
    _block(body, team, variant)
    _plant(body, variant)
    _booth(body, variant)
    _flag(team, variant)
    if variant != "intact":
        _rubble(body, variant)
    if variant == "ruins":
        _arm_down(body)
    top = [(body, "body"), (base, "base")] if variant == "ruins" else [(body, "body"), (team, "team_mask"), (base, "base")]
    meshes = [p.finish(f"{variant_id}_{name}") for p, name in top]
    cc.drop_bottoms(meshes, PAD - 0.001)
    nodes = []
    if variant != "ruins":
        turret, bands = _turret(variant)
        _hang(turret, None, RING, f"{variant_id}_turret", REST_YAW)
        _hang(bands, turret, (0.0, 0.0, 0.0), f"{variant_id}_team_mask_turret")
        col, up, fore = _arm(variant)
        reach = REPAIR - ARM
        _hang(col, None, ARM, f"{variant_id}_arm", math.atan2(reach.y, reach.x))
        _hang(up, col, (0.0, 0.0, SHOULDER), f"{variant_id}_arm_upper")
        _hang(fore, up, UPPER, f"{variant_id}_arm_fore")
        nodes = [turret, bands, col, up, fore]
        bpy.context.view_layer.update()
    meshes += nodes
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    cc.repack(meshes)
    mat, graph = _material(variant_id + "_mat", variant)
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with its nodes (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    fires = rc.fire_nodes(FIRE_POINTS) if variant == "abandoned" else []
    names = [o.name.split(".")[0][len(variant_id) + 1:] for o in meshes]
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes)}"]
    cc.repack(meshes, cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material(meshes, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + fires
    top = [o for o in objs if o.parent is None]
    rc.arrange(top, variant_id)
    coll = bpy.data.collections[variant_id]
    for obj in objs:
        if obj not in top and coll not in obj.users_collection:
            for owner in list(obj.users_collection):
                owner.objects.unlink(obj)
            coll.objects.link(obj)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    with rc.node_names(objs, names + rc.fire_names(fires)):
        rc.save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
