"""Pipeline, element 04 of the industrial set (docs/industrial_elements/04_pipeline): a low steel line on supports that stops wheels and
tracks, infantry steps over (game/data/map/objects.yaml). A map object, made at its own size (not squashed).

One straight piece a cell long along x, origin at the centre of the cell on the ground, its ends at x = ±0.5 so the pieces of a line
meet; the game turns a piece along its neighbours (MapObjects). The parts:
    pipe       grey-green painted steel, 0.38 across, its axis at 0.34; two weld seams round it, rust in streaks
    flanges    half a flange pair at each end, so two pieces meet in a whole pair: a broad ring with its bolts
    support    in the middle: a concrete footing, a steel saddle of two legs and a cross beam under the pipe, a strap over it
Node "body". Budget 1500 triangles, texture 512 (docs/industrial_elements/04_pipeline/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.industrial_pipeline as pl
    print(pl.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

SIZE = 512
VARIANTS = {"pipeline_straight": "intact"}

PAINT, FLANGE, BOLT, STEEL, CONCRETE = (cc.part(i) for i in range(5))
PARTS = 5

R = 0.19
AXIS = 0.34


def build(variant_id):
    ic.clear(variant_id, ("body", "pipe", "support", "flange"))
    p = Parts()
    p.cylinder((0.0, 0.0, AXIS), 'X', R, 0.5, PAINT, sides=20)
    for x in (-0.25, 0.25):
        p.turned((x, 0.0, AXIS), 'X', [(-0.008, R + 0.006), (0.008, R + 0.006)], FLANGE, sides=20)
    for side in (-1, 1):
        x = side * 0.48
        p.turned((x, 0.0, AXIS), 'X', [(-0.02, R + 0.05), (0.02, R + 0.05)], FLANGE, sides=20)
        for k in range(8):
            a = 2 * math.pi * k / 8 + math.pi / 8
            c = Vector((x - side * 0.025, math.cos(a) * (R + 0.032), AXIS + math.sin(a) * (R + 0.032)))
            p.box(c - Vector((0.012, 0.012, 0.012)), c + Vector((0.012, 0.012, 0.012)), BOLT)
    # The support: a footing, two legs and a beam, a saddle and a strap.
    p.box((-0.14, -0.26, 0.0), (0.14, 0.26, 0.06), CONCRETE)
    for y in (-0.17, 0.17):
        p.box((-0.03, y - 0.025, 0.06), (0.03, y + 0.025, AXIS - R + 0.02), STEEL)
    p.box((-0.05, -0.22, AXIS - R - 0.05), (0.05, 0.22, AXIS - R + 0.0), STEEL)
    p.turned((0.0, 0.0, AXIS), 'X', [(-0.035, R + 0.012), (0.035, R + 0.012)], STEEL, sides=20)
    for y in (-0.21, 0.21):
        p.box((-0.035, y - 0.02, AXIS - 0.03), (0.035, y + 0.02, AXIS + 0.01), STEEL)  # the strap's lugs
    return cc.finish(variant_id, [(p, "body")], -1.0, _material(variant_id + "_mat"))


def _material(name):
    """Grey-green paint with rust in streaks and at the bottom, a lighter steel for the flanges, dark bolts, dark steel, concrete."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(40.0, 4.0)
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(6.0, 5.0), 5.0, -2.7))
    under = g.clamp01(g.math('MULTIPLY_ADD', z, -6.0, 2.2))  # rust gathers on the underside
    paint = g.mix(grain, lin((0.30, 0.38, 0.32)), lin((0.36, 0.44, 0.37)))
    paint = g.mix(g.math('MAXIMUM', rust, g.math('MULTIPLY', under, 0.5)), paint, lin((0.40, 0.21, 0.10)))
    flange = g.mix(grain, lin((0.46, 0.48, 0.48)), lin((0.54, 0.56, 0.56)))
    bolt = lin((0.10, 0.10, 0.10))
    steel = g.mix(grain, lin((0.18, 0.19, 0.20)), lin((0.24, 0.25, 0.26)))
    steel = g.mix(g.math('MULTIPLY', rust, 0.7), steel, lin((0.34, 0.18, 0.09)))
    concrete = g.mix(grain, lin((0.52, 0.51, 0.48)), lin((0.60, 0.59, 0.56)))
    albedo = cc.paint(g, mask, (paint, flange, bolt, steel, concrete))
    rough = cc.weighted(g, mask, (0.55, 0.45, 0.5, 0.6, 0.9))
    metallic = cc.weighted(g, mask, (0.3, 0.7, 0.7, 0.5, 0.0))
    g.finish(albedo, g.bump(g.noise(80.0, 3.0), 0.03, 0.004), g.combine(ao, rough, metallic))
    return mat, g


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
