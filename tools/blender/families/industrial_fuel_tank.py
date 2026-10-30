"""Fuel tank, element 01 of the industrial set (docs/industrial_elements/01_fuel_tank): a steel tank on short legs that blows up when it
falls (game/data/neutral/civilian.yaml).

Footprint 1x1 cell, origin at the centre of the cell on the ground; made upright as the city set (the game squashes it, families/
industrial_common.py): the shell from 0.4 to 2.5, the dome to 2.7. A shape that must keep its proportions in the game (the hazard sign)
is made 1 / 0.35 times taller than wide. The parts:
    pad        light concrete with oil stains (the base node), a low kerb round it
    legs       four short I-section legs with braces, on footings
    shell      grey-blue steel in courses with darker ribs round it, a shallow dome with a hatch and a vent; a railing round the dome
    ladder     up the front right side in a cage of hoops, to the railing
    sign       a yellow triangle with a black flame on the front: the danger of it
    piping     an outlet at the back left with a red valve wheel, down to the ground
Nodes: "body", "base"; fire_1..2 when abandoned. One GLB per state:
    fuel_tank            whole
    fuel_tank_damaged    damaged: a dent, fuel running out of it in streaks to a dark pool on the pad, scratches
    fuel_tank_abandoned  burning: a hole torn in the shell, its steel petals bent out, the sign black, soot; fire at the hole and on the
                         dome (fire_1, fire_2)
    fuel_tank_ruins      blown up: a ring of torn plates on the legs, the dome on the pad, curled sheets, a black scorch
Budget 4000 triangles, texture 1024 (docs/industrial_elements/01_fuel_tank/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.industrial_fuel_tank as ft
    print(ft.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

SIZE = 1024
VARIANTS = {
    "fuel_tank": "intact",
    "fuel_tank_damaged": "damaged",
    "fuel_tank_abandoned": "abandoned",
    "fuel_tank_ruins": "ruins",
}

(STEEL, RIB, LEG, CONCRETE, HAZARD, BLACK, RAIL, DARK, OIL, VALVE) = (cc.part(i) for i in range(10))
PARTS = 10

PAD = 0.05
R = 0.38
LOW, HIGH = 0.4, 2.5  # the shell
DOME = 2.7
SQUASH = 0.35
LADDER = math.radians(-55)  # where the ladder stands round the tank
FIRE_POINTS = ((0.0, -R - 0.05, 1.4), (0.0, 0.0, DOME))


def _dir(a):
    return Vector((math.cos(a), math.sin(a), 0.0))


def _pad(base, p, variant):
    base.box((-0.48, -0.48, 0.0), (0.48, 0.48, PAD), CONCRETE)
    for (x0, y0), (x1, y1) in (((-0.48, -0.48), (0.48, -0.44)), ((-0.48, 0.44), (0.48, 0.48)), ((-0.48, -0.44), (-0.44, 0.44)), ((0.44, -0.44), (0.48, 0.44))):
        p.box((x0, y0, PAD), (x1, y1, PAD + 0.06), CONCRETE)  # the kerb
    if variant != "intact":
        # A pool of fuel, or the scorch of the blast.
        r = 0.3 if variant == "damaged" else 0.42
        c = Vector((0.05, -0.18, PAD)) if variant == "damaged" else Vector((0.0, 0.0, PAD))
        p.turned(c, 'Z', [(0.0, r), (0.003, r)], OIL, sides=18)


def _legs(p, variant):
    for k in range(4):
        a = math.pi / 4 + math.pi / 2 * k
        c = _dir(a) * (R * 0.72)
        bent = variant == "ruins" and k == 1
        top = Vector((c.x, c.y, LOW)) + (Vector((0.08, -0.05, -0.15)) if bent else Vector())
        p.box((c.x - 0.07, c.y - 0.07, PAD), (c.x + 0.07, c.y + 0.07, PAD + 0.05), CONCRETE)  # a footing
        p.bar((c.x, c.y, PAD + 0.05), top, 0.03, LEG)
        p.bar((c.x - 0.03, c.y, PAD + 0.05), top + Vector((-0.03, 0, 0)), 0.012, LEG)
    for k in range(4):  # braces between the legs
        a, b = math.pi / 4 + math.pi / 2 * k, math.pi / 4 + math.pi / 2 * (k + 1)
        p.bar(_dir(a) * (R * 0.72) + Vector((0, 0, PAD + 0.08)), _dir(b) * (R * 0.72) + Vector((0, 0, LOW - 0.04)), 0.01, LEG)


def _shell(p, variant):
    p.turned((0.0, 0.0, 0.0), 'Z', [(LOW - 0.02, 0.0), (LOW, R * 0.9), (LOW + 0.04, R), (HIGH, R), (HIGH + 0.08, R * 0.92),
                                    (DOME - 0.08, R * 0.5), (DOME, 0.0)], STEEL, sides=24)
    for z in cc.steps(LOW + 0.15, HIGH - 0.05, 0.42):
        p.turned((0.0, 0.0, z), 'Z', [(-0.02, R + 0.012), (0.02, R + 0.012)], RIB, sides=24)
    p.turned((0.0, 0.0, HIGH), 'Z', [(-0.03, R + 0.015), (0.03, R + 0.015)], RIB, sides=24)
    # The hatch and the vent on the dome.
    p.cylinder((0.1, 0.08, DOME - 0.02), 'Z', 0.06, 0.04, RIB, sides=10)
    p.turned((-0.12, -0.05, DOME - 0.05), 'Z', [(0.0, 0.025), (0.18, 0.025), (0.18, 0.05), (0.22, 0.05), (0.24, 0.0)], RAIL, sides=8)


def _railing(p):
    z = HIGH + 0.06
    posts = 12
    for k in range(posts):
        a = 2 * math.pi * k / posts
        c = _dir(a) * (R * 0.94)
        p.bar((c.x, c.y, z), (c.x, c.y, z + 0.26), 0.007, RAIL)
    p.turned((0.0, 0.0, z + 0.26), 'Z', [(-0.008, R * 0.94 + 0.008), (0.008, R * 0.94 + 0.008), (0.008, R * 0.94 - 0.008), (-0.008, R * 0.94 - 0.008)], RAIL, sides=24)
    p.turned((0.0, 0.0, z + 0.14), 'Z', [(-0.006, R * 0.94 + 0.006), (0.006, R * 0.94 + 0.006), (0.006, R * 0.94 - 0.006), (-0.006, R * 0.94 - 0.006)], RAIL, sides=24)


def _ladder(p):
    out, side = _dir(LADDER), _dir(LADDER + math.pi / 2)
    for s in (-0.06, 0.06):
        a = out * (R + 0.06) + side * s
        p.bar((a.x, a.y, PAD), (a.x, a.y, HIGH + 0.3), 0.008, RAIL)
    for z in cc.steps(PAD + 0.15, HIGH + 0.2, 0.17):
        a, b = out * (R + 0.06) - side * 0.06, out * (R + 0.06) + side * 0.06
        p.bar((a.x, a.y, z), (b.x, b.y, z), 0.006, RAIL)
    for z in cc.steps(0.9, HIGH + 0.2, 0.32):  # the cage
        pts = [out * (R + 0.06) + side * (-0.08), out * (R + 0.17) + side * (-0.08), out * (R + 0.17) + side * 0.08, out * (R + 0.06) + side * 0.08]
        for a, b in zip(pts, pts[1:]):
            p.bar((a.x, a.y, z), (b.x, b.y, z), 0.005, RAIL)


def _sign(p, variant):
    """The hazard sign on the front: a yellow triangle, a black flame on it; black when the tank burns."""
    w, z0 = 0.36, 1.05
    h = w * 0.866 / SQUASH
    y = -R - 0.004
    tri = [Vector((-w / 2, y, z0)), Vector((w / 2, y, z0)), Vector((0.0, y, z0 + h))]
    burnt = variant in ("abandoned",)
    p.loft([tri, [v + Vector((0, -0.012, 0)) for v in tri]], BLACK if burnt else HAZARD)
    if burnt:
        return
    # The flame: a teardrop of a few points, a lick to one side.
    pts = [(0.0, 0.05), (0.06, 0.12), (0.07, 0.24), (0.03, 0.36), (0.01, 0.3), (-0.03, 0.42), (-0.07, 0.26), (-0.06, 0.12)]
    flame = [Vector((fx, y - 0.013, z0 + 0.1 + fz * 1.4)) for fx, fz in pts]
    p.loft([flame, [v + Vector((0, -0.006, 0)) for v in flame]], BLACK)


def _piping(p):
    a = _dir(math.radians(215))
    start = a * R + Vector((0, 0, LOW + 0.2))
    bend = a * (R + 0.18) + Vector((0, 0, LOW + 0.2))
    p.bar(start, bend, 0.035, LEG)
    p.bar(bend, Vector((bend.x, bend.y, PAD + 0.08)), 0.035, LEG)
    foot = Vector((bend.x, bend.y, PAD + 0.08))
    p.bar(foot, foot + a * 0.15, 0.035, LEG)
    p.turned(start + a * 0.1, 'Z', [(-0.03, 0.05), (0.03, 0.05)], RIB, sides=10)
    wheel = start + a * 0.1 + Vector((0, 0, 0.12))
    p.turned(wheel, 'Z', [(-0.006, 0.06), (0.006, 0.06), (0.006, 0.05), (-0.006, 0.05)], VALVE, sides=12)
    p.bar(start + a * 0.1 + Vector((0, 0, 0.03)), wheel, 0.008, VALVE)


def _dent(p, rnd):
    """A dent at the front left: a ring of darker, bent steel; fuel running out in streaks."""
    a = math.radians(-115)
    c = _dir(a) * (R - 0.02) + Vector((0, 0, 1.5))
    cc.block(p, c, (0.2, 0.06, 0.45), (0.0, 0.0, a + math.pi / 2), RIB)
    for k in range(3):
        s = _dir(a + 0.12 * (k - 1)) * (R + 0.005)
        p.bar(Vector((s.x, s.y, 1.35)), Vector((s.x, s.y, LOW + 0.05 * k)), 0.006, OIL)


def _hole(p, rnd):
    """A hole torn in the front of the shell, its petals of steel bent out."""
    a = math.radians(-100)
    c = _dir(a) * (R + 0.005) + Vector((0, 0, 1.4))
    cc.block(p, c, (0.28, 0.03, 0.7), (0.0, 0.0, a + math.pi / 2), DARK)
    for k in range(6):
        t = 2 * math.pi * k / 6
        off = _dir(a + math.pi / 2) * (0.15 * math.cos(t)) + Vector((0, 0, 0.36 * math.sin(t)))
        cc.block(p, c + off + _dir(a) * 0.05, (0.1, 0.012, 0.22), (0.4 * math.sin(t), 0.5, a + math.pi / 2 + 0.6 * math.cos(t)), STEEL)


def _ruins(p, rnd):
    """A ring of torn plates on the legs, the dome on the pad, curled sheets about."""
    p.turned((0.0, 0.0, 0.0), 'Z', [(LOW - 0.02, 0.0), (LOW, R * 0.9), (LOW + 0.04, R), (LOW + 0.1, R), (LOW + 0.1, 0.0)], STEEL, sides=20)
    n = 14
    for k in range(n):
        if rnd.random() < 0.3:
            continue
        a = 2 * math.pi * k / n
        h = rnd.uniform(0.15, 0.8)
        c = _dir(a) * R + Vector((0, 0, LOW + 0.1 + h / 2))
        cc.block(p, c + _dir(a) * h * 0.15, (2 * math.pi * R / n, 0.015, h), (0.0, rnd.uniform(-0.2, 0.2) + 0.25 * h, a + math.pi / 2), STEEL)
    p.turned((0.2, 0.3, PAD), 'Z', [(0.0, R * 0.8), (0.06, R * 0.75), (0.14, R * 0.4), (0.16, 0.0)], STEEL, sides=16)  # the dome
    for k in range(4):
        c = Vector((rnd.uniform(-0.35, 0.35), rnd.uniform(-0.35, 0.35), PAD + 0.03))
        cc.block(p, c, (0.22, 0.012, 0.18), (rnd.uniform(0.8, 1.4), 0.2, rnd.uniform(0, 3)), STEEL)


def _material(name, variant):
    """Grey-blue steel in courses, darker ribs, dark grey legs, light concrete with oil, yellow and black, dark rails, near-black, oil,
    a red valve; rust low on the legs; soot when it burns, black all over in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', z, 4.8)), 0.5)), -60.0, 29.5))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 4.0), 5.0, -3.1))
    steel = g.mix(g.noise(3.0, 2.0), lin((0.42, 0.50, 0.56)), lin((0.50, 0.58, 0.64)))
    steel = g.mix(g.math('MULTIPLY', courses, 0.5), steel, lin((0.28, 0.33, 0.37)))
    steel = g.mix(g.math('MULTIPLY', rust, 0.6), steel, lin((0.42, 0.24, 0.12)))
    rib = g.mix(grain, lin((0.30, 0.35, 0.39)), lin((0.36, 0.41, 0.45)))
    low = g.clamp01(g.math('MULTIPLY_ADD', z, -3.0, 1.2))  # rust at the foot of the legs
    leg = g.mix(grain, lin((0.18, 0.19, 0.20)), lin((0.24, 0.25, 0.26)))
    leg = g.mix(g.math('MULTIPLY', low, 0.8), leg, lin((0.36, 0.18, 0.08)))
    stain = g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 3.0), 3.0, -1.6))
    concrete = g.mix(grain, lin((0.62, 0.61, 0.58)), lin((0.70, 0.69, 0.66)))
    concrete = g.mix(g.math('MULTIPLY', stain, 0.7), concrete, lin((0.22, 0.21, 0.19)))
    hazard = lin((0.92, 0.74, 0.08))
    black = lin((0.03, 0.03, 0.03))
    rail = g.mix(grain, lin((0.14, 0.15, 0.16)), lin((0.20, 0.21, 0.22)))
    dark = g.mix(grain, lin((0.02, 0.02, 0.02)), lin((0.06, 0.05, 0.05)))
    oil = g.mix(g.noise(10.0, 3.0), lin((0.03, 0.03, 0.02)), lin((0.08, 0.07, 0.05)))
    valve = lin((0.70, 0.10, 0.06))
    albedo = cc.paint(g, mask, (steel, rib, leg, concrete, hazard, black, rail, dark, oil, valve))
    amount = {"intact": 0.0, "damaged": 0.2, "abandoned": 0.75, "ruins": 0.95}[variant]
    if amount:
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.9), albedo, lin((0.05, 0.04, 0.04)))
    rough = cc.weighted(g, mask, (0.45, 0.5, 0.6, 0.9, 0.5, 0.5, 0.5, 0.9, 0.15, 0.5))
    metallic = cc.weighted(g, mask, (0.6, 0.6, 0.5, 0.0, 0.0, 0.0, 0.6, 0.0, 0.0, 0.3))
    height = g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', courses, -1.0), g.noise(60.0, 3.0))
    g.finish(albedo, g.bump(height, 0.04, 0.006), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "base"))
    rnd = ic.seed(variant_id)
    body, base = Parts(), Parts()
    _pad(base, body, variant)
    _legs(body, variant)
    if variant == "ruins":
        _ruins(body, rnd)
    else:
        _shell(body, variant)
        _railing(body)
        _ladder(body)
        _sign(body, variant)
        _piping(body)
        if variant == "damaged":
            _dent(body, rnd)
        if variant == "abandoned":
            _hole(body, rnd)
    return cc.finish(variant_id, [(body, "body"), (base, "base")], PAD - 0.001, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
