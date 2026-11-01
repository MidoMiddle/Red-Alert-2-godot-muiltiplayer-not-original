"""Quay edge, element 09 of the naval set (docs/naval_elements/09_quay_wall): what stands on the edge of a quay wall. The wall itself is
the relief, a cliff of two levels over the water dressed in the quay's concrete (families/ground_textures.py, quay_concrete); the rings
and the steps the documents left to small models are this one.

Footprint 1x1 cell, origin at the centre of the cell on the top of the wall, the wall's face at y = -0.5 going down to the water two
levels below (0.81; a level is 104 of 256 leptons a cell). Made along x, at the size the game draws it. The parts:
    coping     a concrete coping along the edge, its outer face yellow and black
    bollards   two cast iron mooring bollards on it, a rope coiled round one
    fenders    two old tyres hanging on chains down the face
    ladder     a steel ladder down the face to the water, its grab rails curved over the coping
Node: "body". One GLB per state:
    quay_edge          whole, with the ladder
    quay_edge_plain    whole, no ladder: for the cells between the ladders along a quay
    quay_edge_damaged  damaged: the coping chipped along its edge with pieces fallen, a bollard knocked over, a tyre gone (its chain
                       hanging), the ladder bent, rust and soot
Budget 1500 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.naval_quay_edge as qe
    print(qe.run_all())
"""

import math

from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from families.infrastructure_gate import transformed
from families.naval_reef import FOLDER
from units.parts import Parts, lin
import rts_common as rc

SIZE = 1024
VARIANTS = {
    "quay_edge": "intact",
    "quay_edge_plain": "plain",
    "quay_edge_damaged": "damaged",
}

(CONCRETE, HAZARD, IRON, ROPE, RUBBER, STEEL) = (cc.part(i) for i in range(6))
PARTS = 6

FACE = -0.5  # the wall's face
DROP = 2 * 104 / 256  # the wall's height over the water: two levels
COPING = (0.1, 0.035)  # its depth and height


def _coping(p, damaged, rnd):
    d, h = COPING
    if damaged:
        for x0, x1 in ((-0.5, -0.12), (0.06, 0.5)):
            p.box((x0, FACE, 0.0), (x1, FACE + d, h), CONCRETE)
            p.box((x0, FACE - 0.004, 0.0), (x1, FACE, h), HAZARD)
        for k in range(4):  # the pieces fallen off the gap
            cc.block(p, (rnd.uniform(-0.12, 0.06), FACE + rnd.uniform(0.05, 0.25), 0.012), (0.04, 0.05, 0.025),
                     (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), rnd.uniform(0, math.pi)), CONCRETE)
        return
    p.box((-0.5, FACE, 0.0), (0.5, FACE + d, h), CONCRETE)
    p.box((-0.5, FACE - 0.004, 0.0), (0.5, FACE, h), HAZARD)


def _bollard(p, x, fallen=False):
    """A cast iron bollard: a foot plate, a waisted post, a cap that overhangs; on its side when fallen."""
    def make():
        p.box((-0.035, -0.035, 0.0), (0.035, 0.035, 0.008), IRON)
        p.turned((0.0, 0.0, 0.0), 'Z', [(0.008, 0.026), (0.03, 0.02), (0.05, 0.022), (0.056, 0.032), (0.066, 0.032), (0.07, 0.0)], IRON, sides=10)
    at = Matrix.Translation((x, FACE + 0.17, 0.0))
    if fallen:
        at = Matrix.Translation((x + 0.02, FACE + 0.2, 0.032)) @ Matrix.Rotation(1.5, 4, 'X') @ Matrix.Rotation(0.6, 4, 'Z')
    transformed(p, at, make)


def _fender(p, x, gone=False):
    """An old tyre hanging on two chains from the coping, down the face."""
    top = Vector((x, FACE - 0.005, COPING[1] - 0.01))
    centre = Vector((x, FACE - 0.03, -0.22))
    for dx in (-0.04, 0.04):
        end = centre + Vector((dx, 0.0, 0.06)) if not gone else top + Vector((dx * 0.3, -0.02, -0.12 - abs(dx)))
        p.bar(top + Vector((dx * 0.5, 0.0, 0.0)), end, 0.003, IRON)
    if gone:
        return
    p.turned(centre, 'Y', [(-0.022, 0.035), (-0.03, 0.06), (0.0, 0.07), (0.03, 0.06), (0.022, 0.035)], RUBBER, sides=12)


def _ladder(p, x, bent=False):
    bottom = -DROP - 0.02
    for dx in (-0.05, 0.05):
        y = FACE - 0.02
        p.bar((x + dx, y, bottom), (x + dx, y, -0.02), 0.006, STEEL)
        # The grab rails curved over the coping.
        p.bar((x + dx, y, -0.02), (x + dx, y, 0.12), 0.006, STEEL)
        p.bar((x + dx, y, 0.12), (x + dx, FACE + 0.06, 0.12), 0.006, STEEL)
        p.bar((x + dx, FACE + 0.06, 0.12), (x + dx, FACE + 0.06, COPING[1]), 0.006, STEEL)
    for z in cc.steps(bottom + 0.06, -0.06, 0.07):
        kink = 0.03 if bent and -0.45 < z < -0.25 else 0.0
        p.bar((x - 0.05, FACE - 0.02 - kink, z), (x + 0.05, FACE - 0.02 - kink, z), 0.004, STEEL)


def _material(name, variant):
    """Light grey concrete; yellow and black hazard stripes on the coping's face; dark cast iron; a hemp rope; black rubber; galvanised
    steel; rust, more and soot when damaged."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    grain = g.noise(30.0, 4.0)
    concrete = g.mix(grain, lin((0.62, 0.62, 0.59)), lin((0.70, 0.70, 0.67)))
    stripe = g.math('GREATER_THAN', g.math('FRACT', g.math('MULTIPLY', x, 8.0)), 0.5)
    hazard = g.mix(stripe, lin((0.08, 0.08, 0.07)), lin((0.88, 0.70, 0.12)))
    iron = g.mix(grain, lin((0.10, 0.10, 0.11)), lin((0.18, 0.17, 0.17)))
    rope = g.mix(g.noise(50.0, 2.0), lin((0.56, 0.46, 0.30)), lin((0.66, 0.56, 0.38)))
    rubber = g.mix(grain, lin((0.05, 0.05, 0.05)), lin((0.10, 0.10, 0.10)))
    steel = g.mix(grain, lin((0.52, 0.54, 0.55)), lin((0.62, 0.64, 0.65)))
    albedo = cc.paint(g, mask, (concrete, hazard, iron, rope, rubber, steel))
    amount = 0.4 if variant == "damaged" else 0.15
    rusty = g.math('MULTIPLY', g.math('ADD', mask[2], mask[5]), g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 3.0), 5.0, -3.0 + 2.0 * amount)))
    albedo = g.mix(rusty, albedo, lin((0.44, 0.24, 0.12)))
    if variant == "damaged":
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, 0.35), 0.8), albedo, lin((0.08, 0.07, 0.07)))
    rough = cc.weighted(g, mask, (0.9, 0.6, 0.6, 0.9, 0.85, 0.45))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.5, 0.0, 0.0, 0.7))
    g.finish(albedo, g.bump(g.noise(60.0, 3.0), 0.05, 0.004), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    damaged = variant == "damaged"
    ic.clear(variant_id, ("body",))
    rnd = ic.seed(variant_id)
    p = Parts()
    _coping(p, damaged, rnd)
    _bollard(p, -0.3)
    _bollard(p, 0.3, fallen=damaged)
    # A rope coiled round the left bollard.
    for k in range(3):
        p.turned((-0.3, FACE + 0.17, 0.03 + 0.008 * k), 'Z', [(-0.004, 0.034), (0.004, 0.034), (0.004, 0.026), (-0.004, 0.026)], ROPE, sides=10)
    p.bar((-0.27, FACE + 0.17, 0.035), (-0.12, FACE + 0.06, 0.03), 0.005, ROPE)
    _fender(p, -0.15)
    _fender(p, 0.38, gone=damaged)
    if variant != "plain":
        _ladder(p, 0.12, bent=damaged)
    return cc.finish(variant_id, [(p, "body")], -1.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
