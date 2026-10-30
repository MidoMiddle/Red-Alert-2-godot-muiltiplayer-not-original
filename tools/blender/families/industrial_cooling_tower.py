"""Cooling tower, element 03 of the industrial set (docs/industrial_elements/03_cooling_tower): a broad concrete tower of hyperbolic
profile, open at the top, a landmark (game/data/neutral/civilian.yaml).

Footprint 2x2 cells, origin at the centre of the footprint on the ground; made upright as the city set (the game squashes it, families/
industrial_common.py): the shell from 0.45 to 5.0. The parts:
    plate      a concrete plate (the base node) with a low fence of posts and rails round it
    basin      a round concrete basin wall at the foot, dark water in it, seen between the legs
    legs       a ring of slanted columns in pairs, crossing, that carry the shell
    shell      light grey concrete, waist at two thirds of its height, a thickened rim; dark inside, struts across under the rim; the
               lifts of its pouring as faint bands, streaks of wet; a ladder up its outside at the front right
    pump house a small concrete house at the front right corner with a steel door, two big pipes from it into the basin
Nodes: "body", "base"; "steam_anchor" over the top; fire_1..2 when abandoned. One GLB per state:
    cooling_tower            whole
    cooling_tower_damaged    damaged: cracks and dark stains, a notch broken out of the rim, two fence posts down
    cooling_tower_abandoned  critically damaged: a hole blown in its front, the rim broken in two places, chunks at its foot; fire in
                             the pump house and in the basin (fire_1, fire_2)
    cooling_tower_ruins      fallen in: a ring of jagged shell pieces on the legs, heaps of concrete in the basin, the pump house
                             wrecked
Budget 6000 triangles, texture 2048 (docs/industrial_elements/03_cooling_tower/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.industrial_cooling_tower as ct
    print(ct.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

SIZE = 2048
VARIANTS = {
    "cooling_tower": "intact",
    "cooling_tower_damaged": "damaged",
    "cooling_tower_abandoned": "abandoned",
    "cooling_tower_ruins": "ruins",
}

(SHELL, INSIDE, CONCRETE, WATER, METAL, DOOR, PIPE, DARK) = (cc.part(i) for i in range(8))
PARTS = 8

PLATE = 0.05
BOTTOM, WAIST, TOP = 0.45, 3.7, 5.0
WAIST_R, C = 0.6, 2.695  # r(z) = WAIST_R * sqrt(1 + ((z - WAIST) / C)^2): 0.94 at the bottom, 0.67 at the top
LADDER = math.radians(-60)
PUMP = Vector((0.68, -0.72, 0.0))
FIRE_POINTS = ((PUMP.x, PUMP.y, 0.4), (-0.3, 0.2, 0.2))


def radius(z):
    return WAIST_R * math.sqrt(1.0 + ((z - WAIST) / C) ** 2)


def _dir(a):
    return Vector((math.cos(a), math.sin(a), 0.0))


def _plate(base, p, variant, rnd):
    base.box((-0.98, -0.98, 0.0), (0.98, 0.98, PLATE), CONCRETE)
    # The fence round its edge: posts and two rails, a gap at the pump house.
    posts = []
    for t in cc.steps(-0.94, 0.94, 0.235):
        posts += [(t, -0.94), (t, 0.94), (-0.94, t), (0.94, t)]
    posts = sorted(set((round(x, 3), round(y, 3)) for x, y in posts))
    for k, (x, y) in enumerate(posts):
        if variant != "intact" and k in (3, 17):
            cc.block(p, (x + 0.08, y, PLATE + 0.02), (0.18, 0.015, 0.015), (0.0, 0.0, 0.4), METAL)
            continue
        p.box((x - 0.01, y - 0.01, PLATE), (x + 0.01, y + 0.01, PLATE + 0.3), METAL)
    for z in (PLATE + 0.15, PLATE + 0.28):
        for (x0, y0), (x1, y1) in (((-0.94, -0.94), (0.94, -0.94)), ((-0.94, 0.94), (0.94, 0.94)), ((-0.94, -0.94), (-0.94, 0.94)), ((0.94, -0.94), (0.94, 0.94))):
            p.bar((x0, y0, z), (x1, y1, z), 0.006, METAL)


def _basin(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLATE, 0.97), (PLATE + 0.14, 0.97), (PLATE + 0.14, 0.92), (PLATE, 0.92)], CONCRETE, sides=32)
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLATE, 0.0), (PLATE, 0.92), (PLATE + 0.08, 0.92), (PLATE + 0.08, 0.0)], WATER, sides=32)


def _legs(p, broken=()):
    n = 18
    for k in range(n):
        if k in broken:
            continue
        a = 2 * math.pi * k / n
        foot = _dir(a) * 0.95 + Vector((0, 0, PLATE + 0.14))
        for s in (-1, 1):
            top = _dir(a + s * math.pi / n) * (radius(BOTTOM) - 0.03) + Vector((0, 0, BOTTOM + 0.02))
            p.bar(foot, top, 0.02, CONCRETE)


def _shell(p, variant):
    """The shell as one body of revolution: up the outside, over the thickened rim, down the inside and shut deep in it; the inside
    painted dark."""
    zs = cc.steps(BOTTOM, TOP, 0.35)
    outer = [(z, radius(z)) for z in zs]
    stations = [(BOTTOM - 0.01, radius(BOTTOM) - 0.06)] + outer + [(TOP + 0.04, radius(TOP) + 0.02), (TOP + 0.06, radius(TOP) - 0.06)]
    stations += [(z, radius(z) - 0.06) for z in reversed(cc.steps(TOP - 1.4, TOP - 0.05, 0.35))] + [(TOP - 1.45, 0.0)]
    faces = cc.made(p, lambda: p.turned((0.0, 0.0, 0.0), 'Z', stations, SHELL, sides=36))
    inside = [f for f in faces if (lambda c: c.z > TOP - 1.5 and math.hypot(c.x, c.y) < radius(min(c.z, TOP)) - 0.03)(f.calc_center_median())]
    cc.repaint(p, inside, INSIDE)
    # The struts across under the rim.
    z = TOP - 0.3
    for a in (0.0, math.pi / 3, 2 * math.pi / 3):
        d = _dir(a) * (radius(z) - 0.07)
        p.bar(Vector((0, 0, z)) - d, Vector((0, 0, z)) + d, 0.015, INSIDE)
    p.turned((0.0, 0.0, z), 'Z', [(-0.015, 0.16), (0.015, 0.16), (0.015, 0.12), (-0.015, 0.12)], INSIDE, sides=12)


def _ladder(p, top):
    out = _dir(LADDER)
    side = _dir(LADDER + math.pi / 2)
    zs = cc.steps(BOTTOM, top, 0.3)
    for s in (-0.04, 0.04):
        pts = [out * (radius(z) + 0.035) + side * s + Vector((0, 0, z)) for z in zs]
        for a, b in zip(pts, pts[1:]):
            p.bar(a, b, 0.006, METAL)
    for z in cc.steps(BOTTOM + 0.1, top - 0.05, 0.2):
        c = out * (radius(z) + 0.035) + Vector((0, 0, z))
        p.bar(c - side * 0.04, c + side * 0.04, 0.005, METAL)
    a = _dir(LADDER) * 0.97 + Vector((0, 0, PLATE + 0.14))  # up from the basin wall to the shell
    p.bar(a, out * (radius(BOTTOM) + 0.035) + Vector((0, 0, BOTTOM)), 0.006, METAL)


def _pump(p, variant):
    """The pump house at the front right corner, its pipes into the basin."""
    x, y = PUMP.x, PUMP.y
    if variant == "ruins":
        p.box((x - 0.18, y - 0.13, PLATE), (x + 0.18, y + 0.13, PLATE + 0.25), CONCRETE)
        cc.block(p, (x + 0.05, y - 0.05, PLATE + 0.3), (0.4, 0.3, 0.04), (0.2, 0.3, 0.5), CONCRETE)
        return
    p.box((x - 0.18, y - 0.13, PLATE), (x + 0.18, y + 0.13, PLATE + 0.55), CONCRETE)
    p.box((x - 0.2, y - 0.15, PLATE + 0.55), (x + 0.2, y + 0.15, PLATE + 0.6), METAL)
    p.box((x - 0.07, y - 0.135, PLATE), (x + 0.07, y - 0.125, PLATE + 0.4), DARK if variant == "abandoned" else DOOR)
    for k in range(2):
        a = Vector((x - 0.18, y + 0.05 - 0.1 * k, PLATE + 0.12))
        b = Vector((x - 0.42, y + 0.25 - 0.06 * k, PLATE + 0.12))
        p.bar(a, b, 0.045, PIPE)
        p.bar(b, b + Vector((-0.02, 0.0, 0.1)), 0.045, PIPE)


def _damage(p, variant, rnd):
    """A notch out of the rim; when abandoned a hole in the front, a second break in the rim, chunks at the foot."""
    breaks = [math.radians(70)] + ([math.radians(-150)] if variant == "abandoned" else [])
    for a in breaks:
        c = _dir(a) * radius(TOP) + Vector((0, 0, TOP - 0.1))
        cc.block(p, c, (0.3, 0.12, 0.35), (0.0, 0.0, a + math.pi / 2), INSIDE)
    if variant == "abandoned":
        a = math.radians(-95)
        for k, z in enumerate((1.6, 2.0, 2.4)):
            c = _dir(a + 0.08 * (k - 1)) * (radius(z) + 0.005) + Vector((0, 0, z))
            cc.block(p, c, (0.42 - 0.08 * k, 0.04, 0.45), (0.0, 0.0, a + math.pi / 2), INSIDE)
        ic.rubble(p, rnd, -0.5, 0.2, -0.95, -0.6, PLATE, 10, 0.16, SHELL, height=0.1)


def _ruins(p, rnd):
    """A ring of jagged pieces of the shell standing on the legs, heaps in the basin."""
    n = 24
    for k in range(n):
        if rnd.random() < 0.3:
            continue
        a = 2 * math.pi * k / n
        h = rnd.uniform(0.2, 1.5)
        z = BOTTOM + h / 2
        c = _dir(a) * radius(z) + Vector((0, 0, z))
        cc.block(p, c, (2 * math.pi * radius(BOTTOM) / n * 1.02, 0.06, h), (0.0, -0.18 * (h / 1.5), a + math.pi / 2), SHELL)
    p.turned((0.0, 0.0, 0.0), 'Z', [(BOTTOM - 0.01, radius(BOTTOM) - 0.06), (BOTTOM - 0.01, radius(BOTTOM)), (BOTTOM + 0.1, radius(BOTTOM + 0.1)),
                                    (BOTTOM + 0.1, radius(BOTTOM) - 0.06)], SHELL, sides=36)
    ic.rubble(p, rnd, -0.7, 0.7, -0.7, 0.7, PLATE + 0.08, 30, 0.26, SHELL, height=0.35)
    ic.rubble(p, rnd, -0.95, 0.95, -0.98, -0.85, PLATE, 6, 0.15, SHELL)


def _material(name, variant):
    """Light grey concrete with the bands of its lifts and streaks of wet, dark inside, plain concrete, dark water, grey metal, a grey
    door, rusty pipes, near-black; cracks and stains when damaged, soot low down when abandoned, all over in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    lifts = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', z, 2.6)), 0.5)), -40.0, 19.0))
    streaks = g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.2, 5.0), 3.0, -1.5))
    shell = g.mix(g.noise(6.0, 3.0), lin((0.64, 0.64, 0.62)), lin((0.72, 0.72, 0.70)))
    shell = g.mix(g.math('MULTIPLY', lifts, 0.35), shell, lin((0.48, 0.48, 0.46)))
    shell = g.mix(g.math('MULTIPLY', streaks, 0.45), shell, lin((0.44, 0.45, 0.44)))
    if variant != "intact":
        shell = g.mix(cc.cracks(g, 5.0), shell, lin((0.20, 0.20, 0.19)))
    inside = g.mix(grain, lin((0.06, 0.06, 0.06)), lin((0.12, 0.12, 0.12)))
    concrete = g.mix(grain, lin((0.54, 0.53, 0.50)), lin((0.62, 0.61, 0.58)))
    water = g.mix(g.noise(8.0, 2.0), lin((0.05, 0.10, 0.12)), lin((0.10, 0.18, 0.20)))
    metal = g.mix(grain, lin((0.30, 0.31, 0.32)), lin((0.38, 0.39, 0.40)))
    door = g.mix(grain, lin((0.36, 0.40, 0.42)), lin((0.42, 0.46, 0.48)))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 4.0), 5.0, -2.6))
    pipe = g.mix(rust, lin((0.24, 0.30, 0.26)), lin((0.42, 0.22, 0.10)))
    dark = lin((0.03, 0.03, 0.03))
    albedo = cc.paint(g, mask, (shell, inside, concrete, water, metal, door, pipe, dark))
    amount = {"intact": 0.0, "damaged": 0.15, "abandoned": 0.45, "ruins": 0.6}[variant]
    if amount:
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.7), albedo, lin((0.08, 0.08, 0.08)))
    rough = cc.weighted(g, mask, (0.9, 0.95, 0.9, 0.1, 0.5, 0.5, 0.6, 0.9))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.6, 0.4, 0.4, 0.0))
    height = g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', lifts, -0.5), g.noise(60.0, 3.0))
    g.finish(albedo, g.bump(height, 0.04, 0.006), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "base"))
    rnd = ic.seed(variant_id)
    body, base = Parts(), Parts()
    _plate(base, body, variant, rnd)
    _basin(body)
    _pump(body, variant)
    if variant == "ruins":
        _legs(body, broken=(2, 3, 9, 14))
        _ruins(body, rnd)
    else:
        _legs(body)
        _shell(body, variant)
        _ladder(body, TOP if variant == "intact" else TOP - 0.6)
        if variant != "intact":
            _damage(body, variant, rnd)
    return cc.finish(variant_id, [(body, "body"), (base, "base")], PLATE - 0.001, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    fire = FIRE_POINTS if variant == "abandoned" else ()
    anchors = (("steam_anchor", (0.0, 0.0, TOP)),) if variant in ("intact", "damaged") else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, anchors)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
