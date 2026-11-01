"""Driftwood and beach debris, element 03 of the beach set (docs/beach_elements/03_driftwood): what the sea throws up on the sand.

The map object driftwood (game/data/map/objects.yaml) stops nobody and tracks crush it, as a small thing of the dressing: so the debris the
document lists apart is a third kind of the same object, and each cell picks one (object/driftwood in game/data/visuals/models.yaml).
Made at the size the game draws a thing on a cell. Origin at the centre of the cell on the ground. One GLB per kind:
    prop_driftwood         a bleached trunk lying along the cell, a flared root plate on its end with three broken roots, two broken
                           branch stubs, a strand
                           of seaweed caught on it
    prop_driftwood_branch  a forked branch, bleached, its two arms lying apart, a short stub
    prop_beach_debris      a heap: three weathered planks across each other, a faded canister on its side, a broken crate's frame, dark
                           strands of seaweed over it; no writing
Node: "body". Budget 800 (driftwood), 500 (debris); texture 512.

In Blender, after tools/blender is on sys.path:
    import families.beach_driftwood as drift
    print(drift.run_all())
"""

import math
import random

import bpy
from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"  # game/assets/models/beach/<id>/, assets_src/models/beach/<id>/
SIZE = 512
VARIANTS = ("prop_driftwood", "prop_driftwood_branch", "prop_beach_debris")

(WOOD, PLANK, PLASTIC, RUST, WEED) = (cc.part(i) for i in range(5))
PARTS = 5


def log(p, a, b, r0, r1, part, sides=6, twist=0.0):
    """A tapered round log from a to b, radius r0 at a and r1 at b; four rings along it, a little uneven."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    u = d.cross(Vector((0.0, 0.0, 1.0)))
    if u.length < 1e-6:
        u = d.cross(Vector((1.0, 0.0, 0.0)))
    u = u.normalized()
    v = d.cross(u).normalized()
    rings = []
    for k in range(4):
        t = k / 3
        c = a + (b - a) * t
        r = r0 + (r1 - r0) * t
        rings.append([c + (u * math.cos(2 * math.pi * i / sides + twist) + v * math.sin(2 * math.pi * i / sides + twist))
                      * r * (1.0 + 0.12 * math.sin(i * 2.1 + k)) for i in range(sides)])
    p.loft(rings, part)


def strand(p, points, width, part):
    """A flat strand of seaweed along the points, a hair over the ground."""
    pts = [Vector(q) for q in points]
    rings = []
    for k, q in enumerate(pts):
        d = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        side = Vector((-d.y, d.x, 0.0)) * width
        rings.append([q - side, q + side, q + side + Vector((0, 0, 0.006)), q - side + Vector((0, 0, 0.006))])
    p.loft(rings, part)


def _driftwood(p):
    # The trunk along x, half sunk in the sand; its root plate at -x.
    log(p, (-0.3, 0.02, 0.07), (0.42, -0.06, 0.05), 0.085, 0.045, WOOD, sides=8)
    # The root plate: a broad flared disc on the trunk's end, standing upright, its lower edge in the sand; three short broken roots
    # out of its rim.
    p.turned((-0.34, 0.02, 0.09), 'X', [(-0.035, 0.07), (-0.01, 0.15), (0.015, 0.13), (0.04, 0.07)], WOOD, sides=7)
    for k, a in enumerate((0.5, 1.6, 2.6)):
        rim = Vector((-0.35, 0.02 + 0.14 * math.cos(a), 0.09 + 0.14 * math.sin(a)))
        out = Vector((-0.07, 0.09 * math.cos(a), 0.06 * math.sin(a)))
        log(p, rim, rim + out, 0.022, 0.008, WOOD, sides=4, twist=k)
    # Broken branch stubs, rising off the trunk.
    log(p, (0.0, -0.01, 0.1), (0.1, 0.12, 0.24), 0.03, 0.02, WOOD, sides=5)
    log(p, (0.22, -0.04, 0.08), (0.3, -0.18, 0.15), 0.025, 0.016, WOOD, sides=5)
    strand(p, [(0.1, 0.22, 0.004), (0.12, 0.09, 0.03), (0.14, -0.02, 0.14), (0.17, -0.12, 0.06), (0.2, -0.24, 0.004)], 0.024, WEED)


def _branch(p):
    log(p, (-0.36, -0.12, 0.035), (0.0, 0.0, 0.04), 0.04, 0.034, WOOD, sides=6)
    log(p, (0.0, 0.0, 0.04), (0.38, 0.2, 0.03), 0.034, 0.016, WOOD, sides=6)
    log(p, (0.0, 0.0, 0.04), (0.32, -0.16, 0.025), 0.028, 0.012, WOOD, sides=6)
    log(p, (-0.18, -0.06, 0.05), (-0.16, 0.06, 0.12), 0.018, 0.01, WOOD, sides=5)


def _debris(p, rnd):
    # Three planks across each other.
    for k, (c, turn, z) in enumerate((((-0.05, 0.02), 0.3, 0.012), ((0.04, -0.06), -0.5, 0.034), ((0.0, 0.1), 1.2, 0.056))):
        before = set(p.bm.verts)
        p.box((-0.26, -0.035, -0.011), (0.26, 0.035, 0.011), PLANK)
        m = Matrix.Translation((c[0], c[1], z)) @ Matrix.Rotation(turn, 4, 'Z') @ Matrix.Rotation(0.05 * (k - 1), 4, 'X')
        for v in p.bm.verts:
            if v not in before:
                v.co = m @ v.co
    # The canister on its side, its handle and cap.
    before = set(p.bm.verts)
    p.box((-0.07, -0.045, 0.0), (0.07, 0.045, 0.11), PLASTIC)
    p.box((-0.04, -0.012, 0.11), (0.03, 0.012, 0.135), PLASTIC)
    p.cylinder((0.05, 0.0, 0.12), 'Z', 0.014, 0.012, RUST, sides=6)
    m = Matrix.Translation((0.2, 0.17, 0.045)) @ Matrix.Rotation(0.6, 4, 'Z') @ Matrix.Rotation(-math.pi / 2, 4, 'X')
    for v in p.bm.verts:
        if v not in before:
            v.co = m @ v.co
    # The frame of a broken crate: four slats.
    for (x0, y0, x1, y1) in ((-0.32, -0.12, -0.14, -0.12), (-0.32, -0.27, -0.14, -0.27), (-0.32, -0.27, -0.32, -0.12), (-0.18, -0.29, -0.12, -0.1)):
        p.bar((x0, y0, 0.016), (x1, y1, 0.016 + 0.01 * rnd.random()), 0.013, PLANK)
    p.bar((-0.14, -0.27, 0.02), (-0.14, -0.27, 0.12), 0.012, RUST)  # a bent nail-plate
    for pts in (((-0.2, 0.22, 0.004), (-0.06, 0.12, 0.045), (0.06, 0.08, 0.07), (0.16, -0.02, 0.004)),
                ((0.1, -0.2, 0.004), (0.02, -0.08, 0.05), (-0.08, -0.02, 0.03), (-0.2, 0.02, 0.004))):
        strand(p, pts, 0.022, WEED)


def _material(name):
    """Silver-grey bleached wood, its grain along it and dark cracks; the planks faded brown; the canister's plastic faded blue; rust;
    the seaweed dark green-brown; sand caught low on everything."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(22.0, 5.0)
    wood = g.mix(g.noise(4.0, 3.0), lin((0.46, 0.44, 0.41)), lin((0.60, 0.57, 0.52)))
    wood = g.mix(g.math('MULTIPLY', grain, 0.6), wood, lin((0.38, 0.35, 0.31)))  # the grain darker in streaks
    wood = g.mix(g.math('MULTIPLY', cc.cracks(g, 14.0), 0.8), wood, lin((0.30, 0.27, 0.24)))
    plank = g.mix(grain, lin((0.42, 0.34, 0.25)), lin((0.54, 0.46, 0.35)))
    plastic = g.mix(g.noise(10.0, 2.0), lin((0.44, 0.58, 0.68)), lin((0.58, 0.68, 0.74)))
    rust = g.mix(grain, lin((0.42, 0.24, 0.12)), lin((0.30, 0.20, 0.14)))
    weed = g.mix(g.noise(16.0, 2.0), lin((0.16, 0.20, 0.08)), lin((0.26, 0.22, 0.10)))
    albedo = cc.paint(g, mask, (wood, plank, plastic, rust, weed))
    sand = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 1.4)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 2.0), 3.0, -1.0)))
    sand = g.math('MULTIPLY', sand, g.math('SUBTRACT', 1.0, mask[4]))  # the seaweed lies on top
    albedo = g.mix(sand, albedo, lin((0.86, 0.78, 0.62)))
    rough = cc.weighted(g, mask, (0.85, 0.85, 0.55, 0.8, 0.5))
    g.finish(albedo, g.bump(g.math('ADD', grain, g.math('MULTIPLY', cc.cracks(g, 14.0), 0.5)), 0.3, 0.01), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body",))
    rnd = ic.seed(variant_id)
    body = Parts()
    {"prop_driftwood": _driftwood, "prop_driftwood_branch": _branch}.get(variant_id, lambda p: _debris(p, rnd))(body)
    return cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat"))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
