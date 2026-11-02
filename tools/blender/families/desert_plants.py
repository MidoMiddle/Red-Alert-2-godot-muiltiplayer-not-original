"""The plants of the desert (docs/desert_elements, 04_cactus and 05_dry_shrub): the cactus and the dry shrub.

Made at the size the game draws them, origin at the centre of the cell on the ground:
    cactus_01   a tall columnar cactus with two arms bending up, ribbed, 0.72 tall
    cactus_02   a low cluster: three short barrel stems of different heights round a few stones
    cactus_03   a tall stem, a single arm low on one side, a broken-off stub on the other
        The map's tree (pine_tree) in the desert is a palm or a cactus by its cell (game/scripts/Terrain/TreeShapes.cs): tree/cactus,
        each cell picking one of the three. Its ribs are eight-sided stems, its spines the texture's dots; a few pale yellow flowers on
        the tall ones' tops.
    desert_dry_shrub  a tuft of grey-brown dry twigs fanning out of a little mound, 0.24 tall
    tumbleweed        a loose ball of tangled twigs lying on the sand, 0.3 across
        The map object dry_shrub (game/data/map/objects.yaml): everyone tramples it; object/dry_shrub, each cell one of the two.
Node "body". Budget 300 (cactus, the forest's rule), 500 (shrub); texture 512.

In Blender, after tools/blender is on sys.path:
    import families.desert_plants as plants
    print(plants.run_all())
"""

import math
import random

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "desert"
SIZE = 512
VARIANTS = ("cactus_01", "cactus_02", "cactus_03", "desert_dry_shrub", "tumbleweed")

(STEM, FLOWER, TWIG, SAND) = (cc.part(i) for i in range(4))
PARTS = 4


def _stem(p, points, radius, sides=8, flower=False):
    """A ribbed stem along the points, its top rounded; flowers on it."""
    pts = [Vector(q) for q in points]
    rings = []
    for k, c in enumerate(pts):
        d = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        u = d.cross(Vector((0.0, 1.0, 0.0)))
        if u.length < 1e-6:
            u = d.cross(Vector((1.0, 0.0, 0.0)))
        u = u.normalized()
        v = d.cross(u).normalized()
        r = radius * (1.0 if k < len(pts) - 1 else 0.7)
        rings.append([c + (u * math.cos(2 * math.pi * i / sides) + v * math.sin(2 * math.pi * i / sides)) * r * (1.0 if i % 2 == 0 else 0.86)
                      for i in range(sides)])
    tip = pts[-1] + (pts[-1] - pts[-2]).normalized() * radius * 0.5
    rings.append([tip + (q - pts[-1]) * 0.35 for q in rings[-1]])
    p.loft(rings, STEM)
    if flower:
        for k in range(3):
            a = 2 * math.pi * k / 3
            p.box(tuple(tip + Vector((math.cos(a) * radius * 0.4 - 0.012, math.sin(a) * radius * 0.4 - 0.012, -0.01))),
                  tuple(tip + Vector((math.cos(a) * radius * 0.4 + 0.012, math.sin(a) * radius * 0.4 + 0.012, 0.012))), FLOWER)


def _mound(p, radius=0.12):
    p.turned((0.0, 0.0, 0.0), 'Z', [(-0.01, radius), (0.02, radius * 0.7), (0.035, radius * 0.25)], SAND, sides=7)


def _cactus(p, kind):
    _mound(p)
    if kind == "cactus_01":
        _stem(p, [(0, 0, 0.0), (0, 0, 0.35), (0, 0, 0.66)], 0.06, flower=True)
        _stem(p, [(0.05, 0, 0.28), (0.14, 0, 0.3), (0.16, 0, 0.42), (0.16, 0, 0.52)], 0.038)
        _stem(p, [(-0.05, 0, 0.36), (-0.13, 0.02, 0.38), (-0.14, 0.02, 0.48)], 0.034)
    elif kind == "cactus_02":
        for (x, y, h, r) in ((0.0, 0.0, 0.2, 0.065), (0.12, 0.05, 0.13, 0.05), (-0.08, 0.1, 0.1, 0.045)):
            _stem(p, [(x, y, 0.0), (x, y, h * 0.6), (x, y, h)], r, flower=h > 0.15)
        for (x, y) in ((0.14, -0.1), (-0.12, -0.08)):
            p.box((x - 0.03, y - 0.025, 0.0), (x + 0.03, y + 0.025, 0.03), SAND)
    else:
        _stem(p, [(0, 0, 0.0), (0, 0, 0.3), (0.01, 0, 0.6)], 0.055, flower=True)
        _stem(p, [(0.04, 0, 0.18), (0.12, 0, 0.2), (0.13, 0, 0.32)], 0.034)
        _stem(p, [(-0.04, 0, 0.34), (-0.09, 0, 0.35)], 0.03)  # the broken-off stub


def _twigs(p, rnd, centre, count, reach, height, ball=False):
    c = Vector(centre)
    for k in range(count):
        if ball:
            a, b = rnd.uniform(0, 2 * math.pi), math.acos(rnd.uniform(-1, 1))
            d = Vector((math.cos(a) * math.sin(b), math.sin(a) * math.sin(b), math.cos(b)))
            start = c - d * reach * 0.9
            end = c + d * reach
            mid = (start + end) / 2 + Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * reach * 0.3
        else:
            a = 2 * math.pi * k / count + rnd.uniform(-0.3, 0.3)
            lean = rnd.uniform(0.25, 0.8)
            start = c
            end = c + Vector((math.cos(a) * reach * lean, math.sin(a) * reach * lean, height * rnd.uniform(0.6, 1.0)))
            mid = c + (end - c) * 0.5 + Vector((0, 0, 0.02))
        p.bar(start, mid, 0.004, TWIG)
        p.bar(mid, end, 0.0028, TWIG)


def _material(name):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 3.0)
    angle = g.math('ARCTAN2', y, x)
    ribs = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', angle, 4.0)), 1.5, 0.5))
    stem = g.mix(ribs, lin((0.20, 0.36, 0.26)), lin((0.30, 0.46, 0.32)))
    spines = g.clamp01(g.math('MULTIPLY_ADD', g.noise(90.0, 1.0), 9.0, -6.0))
    stem = g.mix(spines, stem, lin((0.86, 0.82, 0.66)))
    flower = lin((0.96, 0.84, 0.36))
    twig = g.mix(grain, lin((0.36, 0.31, 0.25)), lin((0.50, 0.44, 0.36)))
    sand = g.mix(grain, lin((0.80, 0.68, 0.48)), lin((0.88, 0.76, 0.56)))
    albedo = cc.paint(g, mask, (stem, flower, twig, sand))
    rough = cc.weighted(g, mask, (0.6, 0.6, 0.85, 0.95))
    g.finish(albedo, g.bump(g.math('ADD', ribs, grain), 0.2, 0.01), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body",))
    rnd = random.Random(variant_id)
    body = Parts()
    if variant_id.startswith("cactus"):
        _cactus(body, variant_id)
    elif variant_id == "desert_dry_shrub":
        _mound(body, 0.1)
        _twigs(body, rnd, (0.0, 0.0, 0.02), 13, 0.2, 0.24)
    else:
        _twigs(body, rnd, (0.0, 0.0, 0.14), 19, 0.15, 0.0, ball=True)
    return cc.finish(variant_id, [(body, "body")], -1.0, _material(variant_id + "_mat"))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
