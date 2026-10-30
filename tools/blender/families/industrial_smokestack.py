"""Smokestack, element 02 of the industrial set (docs/industrial_elements/02_smokestack): a tall brick chimney narrowing upward, a
landmark (game/data/neutral/civilian.yaml).

Footprint 1x1 cell, origin at the centre of the cell on the ground; made upright as the city set (the game squashes it, families/
industrial_common.py): the pedestal to 0.4, the shaft to 6.0. The parts:
    pedestal   a square concrete foundation stepped in, an octagonal brick plinth on it with a cleanout door in an iron frame
    shaft      red-brown brick narrowing from 0.34 to 0.2, iron bands round it, a corbelled brick head and a dark heat-proof cap
               with the flue dark inside; iron rungs up the front right, a lightning rod and a red light at the top
Nodes: "body", "base" (the foundation's slab); "smoke_anchor" at the top; fire_1..2 when abandoned. One GLB per state:
    smokestack            whole
    smokestack_damaged    damaged: the head chipped, cracks, bricks knocked out at the foot and lying round it
    smokestack_abandoned  critically damaged: broken off at two thirds, its top jagged, bricks heaped at the foot; fire at the break
                          and at the foot (fire_1, fire_2)
    smokestack_ruins      fallen: a jagged stump on the plinth, a heap of brick
Budget 4000 triangles, texture 1024 (docs/industrial_elements/02_smokestack/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.industrial_smokestack as ss
    print(ss.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

SIZE = 1024
VARIANTS = {
    "smokestack": "intact",
    "smokestack_damaged": "damaged",
    "smokestack_abandoned": "abandoned",
    "smokestack_ruins": "ruins",
}

(BRICK, PLINTH, CONCRETE, IRON, CAP, DARK, RED) = (cc.part(i) for i in range(7))
PARTS = 7

PED = 0.4  # the foundation's top
FOOT = PED + 0.35  # the octagonal plinth's top
TOP = 6.0
R0, R1 = 0.32, 0.2
RUNGS = math.radians(-60)
FIRE_POINTS = {"abandoned": ((0.0, 0.0, 4.0), (0.25, -0.35, PED))}


def radius(z):
    return R0 + (R1 - R0) * (z - FOOT) / (TOP - FOOT)


def _dir(a):
    return Vector((math.cos(a), math.sin(a), 0.0))


def _pedestal(base, p):
    base.box((-0.47, -0.47, 0.0), (0.47, 0.47, 0.05), CONCRETE)
    p.box((-0.44, -0.44, 0.05), (0.44, 0.44, PED - 0.12), CONCRETE)
    p.loft([[Vector((sx * s, sy * s, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))] for s, z in ((0.44, PED - 0.12), (0.4, PED))], CONCRETE)
    p.turned((0.0, 0.0, 0.0), 'Z', [(PED, R0 + 0.06), (FOOT - 0.06, R0 + 0.06), (FOOT, R0 + 0.01)], PLINTH, sides=8)
    # The cleanout door in the front of the plinth.
    y = -(R0 + 0.06) * math.cos(math.pi / 8) - 0.005
    p.box((-0.09, y - 0.01, PED + 0.02), (0.09, y + 0.01, FOOT - 0.08), IRON)
    p.box((-0.07, y - 0.015, PED + 0.04), (0.07, y + 0.005, FOOT - 0.1), DARK)


def _shaft(p, top, variant, rnd):
    """The shaft up to top; a jagged top when it is broken there."""
    whole = top >= TOP
    p.turned((0.0, 0.0, 0.0), 'Z', [(FOOT - 0.01, 0.0), (FOOT - 0.01, R0), (top, radius(top))] + ([(top, 0.0)] if not whole else [(top, radius(top) - 0.05), (top - 0.6, radius(top - 0.6) - 0.05), (top - 0.6, 0.0)]),
             BRICK, sides=20)
    for z in cc.steps(FOOT + 0.5, min(top, TOP - 0.5) - 0.2, 0.75):
        r = radius(z) + 0.012
        p.turned((0.0, 0.0, z), 'Z', [(-0.025, r), (0.025, r), (0.025, r - 0.015), (-0.025, r - 0.015)], IRON, sides=20)
    if whole:
        return
    # The jagged edge of the break: bricks standing up round it.
    n = 12
    for k in range(n):
        if rnd.random() < 0.35:
            continue
        a = 2 * math.pi * k / n
        h = rnd.uniform(0.1, 0.5)
        c = _dir(a) * (radius(top) - 0.03) + Vector((0, 0, top + h / 2))
        cc.block(p, c, (2 * math.pi * radius(top) / n, 0.06, h), (0.0, rnd.uniform(-0.15, 0.15), a + math.pi / 2), BRICK)


def _head(p, variant, rnd):
    """The corbelled head, the dark cap with the flue in it; a piece chipped off when damaged."""
    z0 = TOP - 0.35
    r = radius(z0)
    p.turned((0.0, 0.0, 0.0), 'Z', [(z0, r), (z0 + 0.1, r + 0.04), (z0 + 0.2, r + 0.04), (z0 + 0.25, r + 0.07), (TOP, r + 0.07), (TOP, r - 0.04),
                                    (TOP - 0.5, r - 0.06), (TOP - 0.5, 0.0)], BRICK, sides=20)
    p.turned((0.0, 0.0, TOP), 'Z', [(0.0, R1 + 0.09), (0.08, R1 + 0.09), (0.08, R1 - 0.03), (-0.3, R1 - 0.05), (-0.3, 0.0)], CAP, sides=20)
    if variant == "damaged":
        a = math.radians(120)
        cc.block(p, _dir(a) * (R1 + 0.06) + Vector((0, 0, TOP - 0.05)), (0.2, 0.12, 0.3), (0.0, 0.0, a), DARK)
    # The lightning rod and the red light.
    p.bar((R1 * 0.6, 0.0, TOP + 0.08), (R1 * 0.6, 0.0, TOP + 0.6), 0.008, IRON)
    p.sphere(_dir(math.radians(-90)) * (R1 + 0.1) + Vector((0, 0, TOP - 0.2)), 'Z', 0.03, RED, sides=8, rings=4)


def _rungs(p, top):
    out, side = _dir(RUNGS), _dir(RUNGS + math.pi / 2)
    for z in cc.steps(FOOT + 0.2, top - 0.15, 0.22):
        r = radius(z) + 0.03
        a, b = out * r - side * 0.05, out * r + side * 0.05
        p.bar((a.x, a.y, z), (b.x, b.y, z), 0.006, IRON)
        for q in (a, b):
            p.bar((q.x, q.y, z), (q.x * (r - 0.03) / r, q.y * (r - 0.03) / r, z), 0.005, IRON)


def _foot_damage(p, rnd, count):
    """Bricks knocked out of the foot, lying round it."""
    for k in range(3):
        a = math.radians(-70 + 90 * k)
        cc.block(p, _dir(a) * (R0 + 0.005) + Vector((0, 0, FOOT + 0.2 + 0.25 * k)), (0.1, 0.03, 0.18), (0.0, 0.0, a + math.pi / 2), DARK)
    ic.rubble(p, rnd, -0.42, 0.42, -0.45, -0.3, PED, count, 0.07, BRICK)
    ic.rubble(p, rnd, 0.3, 0.45, -0.3, 0.3, PED, count // 2, 0.07, BRICK)


def _material(name, variant):
    """Red-brown brick in courses, sooty at the foot and at the head, a darker plinth, grey concrete, dark iron bands, a near-black cap,
    black inside the flue, a red light; cracks when damaged, soot over all of it when it burns or lies fallen."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    brick, joints = ic.brick(g, coord, (0.52, 0.24, 0.16), (0.62, 0.30, 0.20), (0.60, 0.56, 0.50), course=10.0, length=12.0)
    foot = g.clamp01(g.math('MULTIPLY_ADD', z, -1.2, 1.4))
    head = g.clamp01(g.math('MULTIPLY_ADD', z, 1.4, -7.3))
    brick = g.mix(g.math('MULTIPLY', g.math('MAXIMUM', foot, head), 0.7), brick, lin((0.10, 0.07, 0.06)))
    if variant != "intact":
        brick = g.mix(cc.cracks(g, 7.0), brick, lin((0.06, 0.05, 0.05)))
    plinth, _ = ic.brick(g, coord, (0.30, 0.15, 0.11), (0.36, 0.19, 0.13), (0.40, 0.37, 0.33))
    concrete = g.mix(grain, lin((0.50, 0.49, 0.46)), lin((0.58, 0.57, 0.54)))
    iron = g.mix(grain, lin((0.10, 0.10, 0.10)), lin((0.16, 0.15, 0.15)))
    cap = g.mix(grain, lin((0.06, 0.06, 0.06)), lin((0.12, 0.11, 0.11)))
    dark = lin((0.02, 0.02, 0.02))
    red = lin((0.85, 0.08, 0.05))
    albedo = cc.paint(g, mask, (brick, plinth, concrete, iron, cap, dark, red))
    amount = {"intact": 0.0, "damaged": 0.2, "abandoned": 0.6, "ruins": 0.8}[variant]
    if amount:
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.85), albedo, lin((0.05, 0.04, 0.04)))
    rough = cc.weighted(g, mask, (0.9, 0.9, 0.9, 0.5, 0.7, 0.9, 0.3))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.6, 0.2, 0.0, 0.0))
    height = g.math('MULTIPLY_ADD', g.math('ADD', mask[0], mask[1]), g.math('MULTIPLY', joints, -1.0), g.noise(60.0, 3.0))
    g.finish(albedo, g.bump(height, 0.05, 0.006), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "base"))
    rnd = ic.seed(variant_id)
    body, base = Parts(), Parts()
    _pedestal(base, body)
    if variant == "ruins":
        _shaft(body, 1.4, variant, rnd)
        ic.rubble(body, rnd, -0.45, 0.45, -0.45, 0.45, PED, 26, 0.12, BRICK, height=0.15)
        ic.rubble(body, rnd, -0.45, 0.45, -0.48, -0.36, 0.05, 6, 0.1, BRICK)
    else:
        top = 4.0 if variant == "abandoned" else TOP
        _shaft(body, top, variant, rnd)
        if top >= TOP:
            _head(body, variant, rnd)
        _rungs(body, top)
        if variant != "intact":
            _foot_damage(body, rnd, 8 if variant == "damaged" else 18)
    return cc.finish(variant_id, [(body, "body"), (base, "base")], 0.049, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    anchors = (("smoke_anchor", (0.0, 0.0, TOP)),) if variant in ("intact", "damaged") else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, FIRE_POINTS.get(variant, ()), anchors)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
