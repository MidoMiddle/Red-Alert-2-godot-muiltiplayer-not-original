"""Ice floe, element 03 of the naval set (docs/naval_elements/03_ice_floe): a flat slab of sea ice drifting on the winter sea; it stops
ships.

Footprint 1x1 cell, origin at the centre of the cell on the water's surface (what lies below 0 is hidden by the water). Made at the size
the game draws it: about 0.9 across, its top 0.13 over the water. The parts:
    floe       an irregular slab with bevelled edges, from 0.12 under the water to 0.13 over it, pale blue ice deepening to blue-green at
               the waterline, fine dark blue cracks on its bare top
    snow       a thin cover of snow over most of the top, its edge irregular
    ridge      a pressure ridge on one side: slabs of ice pushed up and tilted, snow on their tops
    shards     small pieces of ice afloat by its edge
Node: "body". The rocking on the waves is the game's (prompts.md, section 4). One GLB per state:
    ice_floe          whole
    ice_floe_damaged  cracked: a piece broken off one side and drifting, the edge crumbling into blocks, long cracks over it
    ice_floe_ruins    broken: two smaller floes with water between them, tilted, more shards about
Budget 800 triangles, texture 1024 (docs/naval_elements/03_ice_floe/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.naval_ice_floe as floe
    print(floe.run_all())
"""

import math
import random

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from families.naval_reef import FOLDER
from units.parts import Parts, lin
import rts_common as rc

SIZE = 1024
VARIANTS = {
    "ice_floe": "intact",
    "ice_floe_damaged": "damaged",
    "ice_floe_ruins": "ruins",
}

(ICE, SNOW) = (cc.part(i) for i in range(2))
PARTS = 2

UNDER = -0.12  # the floe's bottom: under the water, so the waterline is never its edge
TOP = 0.13
SNOW_TOP = 0.155
RADIUS = 0.45


def outline(rnd, centre, radius, n=14):
    """An irregular ring of points round centre, counter-clockwise."""
    pts = []
    for k in range(n):
        a = 2 * math.pi * k / n + rnd.uniform(-0.12, 0.12)
        r = radius * rnd.uniform(0.78, 1.08)
        pts.append(Vector((centre[0] + math.cos(a) * r, centre[1] + math.sin(a) * r)))
    return pts


def clip(points, normal, offset, rnd, jag=0.03):
    """The part of the polygon on the side where dot(p, normal) <= offset, its cut edge jagged."""
    n = Vector(normal).normalized()
    out = []
    for a, b in zip(points, points[1:] + points[:1]):
        da, db = a.dot(n) - offset, b.dot(n) - offset
        if da <= 0:
            out.append(a)
        if (da <= 0) != (db <= 0):
            out.append(a + (b - a) * (da / (da - db)))
    # Jag the cut: points along it, pushed in and out.
    for i, (a, b) in enumerate(zip(out, out[1:] + out[:1])):
        if abs(a.dot(n) - offset) < 1e-6 and abs(b.dot(n) - offset) < 1e-6:
            mids = [a + (b - a) * t + n * rnd.uniform(-jag, jag) * 0.5 - n * jag * 0.5 for t in (0.25, 0.5, 0.75)]
            return out[:i + 1] + mids + out[i + 1:]
    return out


def slab(p, ring, centre, bottom, top, tilt=(0.0, 0.0), painted=ICE):
    """A slab over the ring: its sides bevelled in at the top and at the bottom; tilted by tilt (rise per unit along x, y)."""
    c = Vector(centre)

    def at(scale, z):
        return [Vector((c.x + (q.x - c.x) * scale, c.y + (q.y - c.y) * scale,
                        z + tilt[0] * (q.x - c.x) + tilt[1] * (q.y - c.y))) for q in ring]

    p.loft([at(0.9, bottom), at(1.0, 0.0), at(0.99, top - 0.03), at(0.93, top)], painted)


def floe(p, rnd, ring, centre, tilt=(0.0, 0.0), snow=0.86):
    """A floe over the ring with its cover of snow."""
    slab(p, ring, centre, UNDER, TOP, tilt)
    c = Vector(centre)
    cover = [Vector((c.x + (q.x - c.x) * snow * rnd.uniform(0.9, 1.04), c.y + (q.y - c.y) * snow * rnd.uniform(0.9, 1.04))) for q in ring]
    z = TOP - 0.005
    p.loft([[Vector((q.x, q.y, z + tilt[0] * (q.x - c.x) + tilt[1] * (q.y - c.y))) for q in cover],
            [Vector((c.x + (q.x - c.x) * 0.9, c.y + (q.y - c.y) * 0.9, SNOW_TOP - TOP + z + tilt[0] * (q.x - c.x) * 0.9 + tilt[1] * (q.y - c.y) * 0.9))
             for q in cover]], SNOW)


def ridge(p, rnd, start, end, count):
    """A pressure ridge: slabs of ice pushed up along a line, tilted, snow on the higher ones."""
    a, b = Vector(start), Vector(end)
    turn = math.atan2(b.y - a.y, b.x - a.x)
    for k in range(count):
        c = a + (b - a) * ((k + 0.5) / count)
        h = rnd.uniform(0.07, 0.14)
        cc.block(p, (c.x + rnd.uniform(-0.03, 0.03), c.y + rnd.uniform(-0.03, 0.03), TOP + h * 0.3),
                 (rnd.uniform(0.09, 0.14), rnd.uniform(0.03, 0.05), h), (rnd.uniform(-0.6, 0.6), rnd.uniform(-0.3, 0.3), turn + rnd.uniform(-0.4, 0.4)),
                 SNOW if k % 3 == 1 else ICE)


def shards(p, rnd, count, reach):
    """Small pieces of ice afloat round the floe."""
    for _ in range(count):
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(reach, reach + 0.08)
        cc.block(p, (math.cos(a) * r, math.sin(a) * r, 0.0), (rnd.uniform(0.04, 0.08), rnd.uniform(0.03, 0.06), rnd.uniform(0.03, 0.05)),
                 (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), rnd.uniform(0, math.pi)), ICE)


def _build(p, variant, rnd):
    ring = outline(rnd, (0.0, 0.0), RADIUS)
    if variant == "ruins":
        # Broken in two along a line across it; the halves drifted apart and tilted.
        normal = (0.6, 1.0)
        n = Vector(normal).normalized()
        a = clip(ring, normal, -0.03, rnd)
        b = clip(ring, (-normal[0], -normal[1]), -0.03, rnd)
        for piece, shift, tilt in ((a, -n * 0.05, (0.04, 0.06)), (b, n * 0.06, (-0.05, -0.03))):
            moved = [q + shift for q in piece]
            centre = sum(moved, Vector((0.0, 0.0))) / len(moved)
            floe(p, rnd, moved, centre, tilt, snow=0.8)
        ridge(p, rnd, (-0.3, -0.2), (-0.05, -0.32), 3)
        shards(p, rnd, 9, 0.38)
        return
    if variant == "damaged":
        # A piece broken off the right side and drifting a little off; the edge left crumbling.
        main = clip(ring, (1.0, 0.25), 0.1, rnd)
        floe(p, rnd, main, (-0.04, 0.0))
        off = clip(ring, (-1.0, -0.25), -0.14, rnd)
        moved = [q + Vector((0.07, -0.02)) for q in off]
        centre = sum(moved, Vector((0.0, 0.0))) / len(moved)
        floe(p, rnd, moved, centre, (0.0, 0.08), snow=0.75)
        for k in range(4):
            cc.block(p, (0.1 + rnd.uniform(-0.02, 0.03), -0.22 + 0.13 * k, 0.02), (0.06, 0.07, 0.05),
                     (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), rnd.uniform(0, math.pi)), ICE)
        ridge(p, rnd, (-0.32, 0.12), (-0.08, 0.34), 4)
        shards(p, rnd, 6, 0.42)
        return
    floe(p, rnd, ring, (0.0, 0.0))
    ridge(p, rnd, (-0.32, 0.12), (-0.04, 0.36), 5)
    shards(p, rnd, 3, 0.44)


def _material(name, variant):
    """Pale blue ice, deeper blue-green towards the waterline and under it, a slushy band at the waterline; fine dark blue cracks on the
    bare ice of the top, more when it cracked; white snow, a little blue in its hollows."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(20.0, 4.0)
    deep = g.clamp01(g.math('MULTIPLY_ADD', z, -10.0, 1.0))  # 1 at the waterline and under it, 0 from 0.1 up
    ice = g.mix(grain, lin((0.74, 0.86, 0.93)), lin((0.84, 0.93, 0.98)))
    ice = g.mix(deep, ice, lin((0.32, 0.60, 0.70)))
    slush = cc.band(g, z, -0.02, 0.02)
    ice = g.mix(g.math('MULTIPLY', slush, 0.6), ice, lin((0.80, 0.84, 0.86)))
    crack = cc.cracks(g, {"intact": 5.0, "damaged": 3.5, "ruins": 3.5}[variant])
    ice = g.mix(g.math('MULTIPLY', crack, g.math('SUBTRACT', 1.0, deep)), ice, lin((0.20, 0.38, 0.58)))
    snow = g.mix(g.noise(9.0, 3.0), lin((0.82, 0.87, 0.94)), lin((0.95, 0.96, 0.98)))
    albedo = cc.paint(g, mask, (ice, snow))
    if variant != "intact":
        # Cracks through the snow too, where the floe split.
        albedo = g.mix(g.math('MULTIPLY', mask[1], g.math('MULTIPLY', cc.cracks(g, 3.0), 0.7)), albedo, lin((0.36, 0.52, 0.70)))
    rough = cc.weighted(g, mask, (0.15, 0.85))
    height = g.math('ADD', g.math('MULTIPLY', mask[1], g.noise(12.0, 3.0)), g.math('MULTIPLY', crack, -0.5))
    g.finish(albedo, g.bump(height, 0.2, 0.006), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body",))
    rnd = ic.seed(variant_id)
    body = Parts()
    _build(body, variant, rnd)
    return cc.finish(variant_id, [(body, "body")], UNDER, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
