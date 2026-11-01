"""Palm, element 02 of the beach set (docs/beach_elements/02_palm): the tree of the coast, and of the desert's oases.

The map's tree (pine_tree) is drawn by the climate's shape: by the sea and in the desert a palm is tree/palm (game/data/visuals/models.yaml),
each cell picking one of the three below. Made at the size the game draws a tree, as the palm it stands for: the trunk to 1.0, the
crown spreading to 0.45 round it. Origin at the centre of the cell on the ground. The parts:
    trunk   six sides, curved, a little bulb at the foot, slimmer up; grey-brown with dark rings where the old fronds fell, lighter at
            the top under the crown
    fronds  long pinnate leaves from the top of the trunk, each folded in a shallow V along its rib, rising a little then drooping;
            green going yellowish, the leaflets as stripes across, brown and dry at the tips
    nuts    a cluster of coconuts under the crown (01, 02)
Node: "body". No team colour. One GLB per kind:
    tree_palm_01  a gentle curve, seven fronds, nuts
    tree_palm_02  the trunk curving strongly to one side, seven fronds, nuts
    tree_palm_03  straighter, a fuller crown of eight fronds, no nuts
Budget 300 triangles (docs/art/model_standards.md, «the forest of a map»), texture 512: a tree is the most numerous thing on a map.

In Blender, after tools/blender is on sys.path:
    import families.beach_palm as palm
    print(palm.run_all())
"""

import math

import bpy
from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"  # game/assets/models/beach/<id>/, assets_src/models/beach/<id>/
SIZE = 512

# bend: how far the top leans out, and which way (degrees); fronds; nuts; turn of the crown (degrees).
VARIANTS = {
    "tree_palm_01": {"bend": 0.16, "towards": 30.0, "fronds": 7, "nuts": True, "turn": 0.0, "height": 1.0},
    "tree_palm_02": {"bend": 0.3, "towards": -60.0, "fronds": 7, "nuts": True, "turn": 20.0, "height": 0.92},
    "tree_palm_03": {"bend": 0.06, "towards": 120.0, "fronds": 8, "nuts": False, "turn": 10.0, "height": 1.06},
}

(TRUNK, FROND, NUT) = (cc.part(i) for i in range(3))
PARTS = 3

SIDES = 6
FROND_LENGTH = 0.46
FROND_WIDTH = 0.07


def _spine(bend, towards, height):
    """The trunk's axis at t (0 foot, 1 top): it rises straight, then leans out more and more."""
    a = math.radians(towards)
    out = Vector((math.cos(a), math.sin(a), 0.0))
    return lambda t: out * (bend * t * t) + Vector((0.0, 0.0, height * t))


def _trunk(p, spine):
    stations = (0.0, 0.06, 0.25, 0.5, 0.75, 0.92, 1.0)
    radii = (0.075, 0.058, 0.05, 0.046, 0.042, 0.04, 0.034)
    rings = []
    for t, r in zip(stations, radii):
        c = spine(t)
        rings.append([c + Vector((math.cos(2 * math.pi * k / SIDES) * r, math.sin(2 * math.pi * k / SIDES) * r, 0.0)) for k in range(SIDES)])
    p.loft(rings, TRUNK)


def _frond(p, top, angle, lift, length, droop):
    """A leaf from the top of the trunk outwards along angle: five stations along it, each a V across (edge, rib, edge)."""
    d = Vector((math.cos(angle), math.sin(angle), 0.0))
    side = Vector((-d.y, d.x, 0.0))
    rings = []
    for k in range(5):
        s = k / 4
        w = max(0.006, FROND_WIDTH * math.sin(math.pi * min(1.0, 0.15 + 0.85 * s)) ** 0.8)
        c = top + d * (length * s) + Vector((0.0, 0.0, lift * s - droop * s * s))
        up = Vector((0.0, 0.0, 1.0))
        rings.append([c - side * w - up * (0.35 * w), c + up * 0.012, c + side * w - up * (0.35 * w)])
    p.loft(rings, FROND)


def _crown(p, top, fronds, turn):
    for k in range(fronds):
        angle = math.radians(turn) + 2 * math.pi * k / fronds + 0.25 * math.sin(k * 2.3)
        long = FROND_LENGTH * (0.85 + 0.15 * math.sin(k * 1.7 + 0.4))
        # Every other leaf hangs lower: the old leaves droop, the young stand up.
        droop, lift = (0.32, 0.08) if k % 2 else (0.2, 0.12)
        _frond(p, top, angle, lift, long, droop)


def _nuts(p, top, towards):
    a = math.radians(towards + 180.0)
    for k, (dx, dy) in enumerate(((0.035, 0.0), (-0.01, 0.035))):
        c = top + Vector((math.cos(a) * 0.04 + dx, math.sin(a) * 0.04 + dy, -0.05 - 0.01 * k))
        p.turned(c, 'Z', [(-0.022, 0.02), (0.0, 0.03), (0.022, 0.02)], NUT, sides=4)


def _material(name, centre):
    """The trunk grey-brown, dark rings round it where the fronds fell, lighter up under the crown; the fronds green going yellowish
    out along them, the leaflets as stripes across, dry brown at the tips; the nuts green-brown."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(40.0, 3.0)
    rings = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', z, 95.0)), 3.0, -2.0))
    bark = g.mix(g.noise(8.0, 3.0), lin((0.60, 0.50, 0.38)), lin((0.70, 0.61, 0.47)))
    bark = g.mix(g.math('MULTIPLY', rings, 0.5), bark, lin((0.38, 0.30, 0.21)))
    bark = g.mix(cc.band(g, z, 0.8, 1.2), bark, lin((0.76, 0.68, 0.52)))
    # Out along a frond: its distance from the crown's middle, over the trunk's top.
    dx, dy = g.math('SUBTRACT', x, centre.x), g.math('SUBTRACT', y, centre.y)
    reach = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', dx, dx), g.math('MULTIPLY', dy, dy)))
    leaflets = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', reach, 210.0)), 2.5, -0.6))
    leaf = g.mix(g.clamp01(g.math('MULTIPLY_ADD', reach, 2.2, -0.15)), lin((0.18, 0.40, 0.12)), lin((0.42, 0.52, 0.16)))
    leaf = g.mix(g.math('MULTIPLY', leaflets, 0.55), leaf, lin((0.10, 0.25, 0.08)))
    dry = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', reach, 9.0, -3.4)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(6.0, 2.0), 3.0, -0.9)))
    leaf = g.mix(dry, leaf, lin((0.52, 0.40, 0.20)))
    nut = g.mix(grain, lin((0.30, 0.34, 0.14)), lin((0.40, 0.30, 0.16)))
    albedo = cc.paint(g, mask, (bark, leaf, nut))
    rough = cc.weighted(g, mask, (0.9, 0.6, 0.7))
    height = g.math('ADD', g.math('MULTIPLY', rings, 0.6), g.math('MULTIPLY', grain, 0.4))
    g.finish(albedo, g.bump(height, 0.3, 0.01), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    v = VARIANTS[variant_id]
    ic.clear(variant_id, ("body",))
    spine = _spine(v["bend"], v["towards"], v["height"])
    body = Parts()
    _trunk(body, spine)
    top = spine(1.0) + Vector((0.0, 0.0, -0.01))
    _crown(body, top, v["fronds"], v["turn"])
    if v["nuts"]:
        _nuts(body, top, v["towards"])
    meshes, mat, graph = cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat", top))
    return meshes, mat, graph


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
