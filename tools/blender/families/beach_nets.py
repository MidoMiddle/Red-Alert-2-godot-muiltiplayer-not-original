"""Fishing nets, element 19 of the beach set (docs/beach_elements/19_fishing_nets): nets hung to dry on poles by the water.

The map object fishing_nets (game/data/map/objects.yaml) stops nobody; wheels and tracks crush it. It takes one cell, so the model is
made at the size the game draws a thing on a cell, as its placeholder: three poles along x, 0.8 apart at the ends, 0.55 tall. Origin at
the centre of the cell on the ground. The parts:
    poles  three bleached wooden poles, a little crooked, a rope along their tops; a fish crate and a coil of rope at their feet
    net    the net draped over the rope in two lengths that sag between the poles: its mesh as thin cords, nine down and one across (no transparent texture: the forest's rule, docs/art/model_standards.md); a row of cork floats along its top, dark
           lead weights along its foot, its end piled on the sand
Nodes: "poles", "net". Budget 800 triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.beach_nets as nets
    print(nets.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"
SIZE = 512
VARIANTS = ("fishing_nets",)

(POLE, ROPE, CORD, FLOAT, LEAD, CRATE) = (cc.part(i) for i in range(6))
PARTS = 6

POLES = ((-0.4, 0.02, 0.55), (0.0, -0.02, 0.5), (0.4, 0.03, 0.56))  # x, y, height
SAG = 0.06
FOOT = 0.14  # the net's foot over the sand


def _top(x):
    """The rope's height at x: from pole to pole, sagging in between."""
    for (x0, _, h0), (x1, _, h1) in zip(POLES, POLES[1:]):
        if x0 <= x <= x1:
            t = (x - x0) / (x1 - x0)
            return h0 + (h1 - h0) * t - 0.03 - SAG * math.sin(math.pi * t)
    return POLES[0][2] - 0.03


def _poles(p):
    for k, (x, y, h) in enumerate(POLES):
        p.bar((x, y, 0.0), (x + 0.02 * (k - 1), y, h), 0.011, POLE)
    for (x0, y0, h0), (x1, y1, h1) in zip(POLES, POLES[1:]):  # the rope along their tops, in three lengths to sag
        pts = [Vector((x0 + (x1 - x0) * t, y0 + (y1 - y0) * t, _top(x0 + (x1 - x0) * t))) for t in (0.0, 0.33, 0.66, 1.0)]
        for a, b in zip(pts, pts[1:]):
            p.bar(a, b, 0.004, ROPE)
    p.box((0.18, 0.12, 0.0), (0.36, 0.26, 0.08), CRATE)  # a fish crate
    p.turned((-0.22, 0.16, 0.0), 'Z', [(0.0, 0.07), (0.025, 0.07), (0.025, 0.03), (0.0, 0.03)], ROPE, sides=7)  # a coil of rope


def _net(p):
    xs = [POLES[0][0] + 0.02 + (POLES[-1][0] - POLES[0][0] - 0.04) * k / 10 for k in range(11)]
    y = 0.0
    for x in xs[1:-1]:  # the cords down
        p.bar((x, y, _top(x)), (x, y + 0.01, FOOT), 0.0022, CORD)
    for k in range(1, 2):  # the cord across, following the sag
        f = k / 2
        pts = [Vector((x, y, _top(x) - (_top(x) - FOOT) * f)) for x in xs]
        for a, b in zip(pts, pts[1:]):
            p.bar(a, b, 0.0022, CORD)
    foot = [Vector((x, y + 0.01, FOOT)) for x in xs]
    for a, b in zip(foot, foot[1:]):
        p.bar(a, b, 0.003, CORD)
    for x in xs[::2]:  # the floats along the top, the weights along the foot
        p.box((x - 0.012, y - 0.008, _top(x) - 0.012), (x + 0.012, y + 0.008, _top(x) + 0.006), FLOAT)
        p.box((x - 0.008, y - 0.006, FOOT - 0.016), (x + 0.008, y + 0.016, FOOT), LEAD)
    # The net's end piled on the sand beyond the last pole.
    p.turned((0.47, 0.06, 0.0), 'Z', [(0.0, 0.08), (0.03, 0.07), (0.05, 0.02)], CORD, sides=6)


def _material(name):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(30.0, 3.0)
    pole = g.mix(grain, lin((0.62, 0.58, 0.50)), lin((0.72, 0.68, 0.60)))
    rope = lin((0.62, 0.54, 0.38))
    cord = g.mix(g.noise(12.0, 2.0), lin((0.30, 0.34, 0.24)), lin((0.42, 0.38, 0.28)))  # grey-green to brown
    floats = lin((0.80, 0.62, 0.36))  # cork
    lead = lin((0.20, 0.20, 0.22))
    crate = g.mix(grain, lin((0.52, 0.42, 0.30)), lin((0.60, 0.50, 0.36)))
    albedo = cc.paint(g, mask, (pole, rope, cord, floats, lead, crate))
    rough = cc.weighted(g, mask, (0.85, 0.9, 0.9, 0.7, 0.5, 0.85))
    g.finish(albedo, g.bump(grain, 0.04, 0.004), g.combine(ao, rough, cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.6, 0.0))))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("poles", "net"))
    poles, net = Parts(), Parts()
    _poles(poles)
    _net(net)
    return cc.finish(variant_id, [(poles, "poles"), (net, "net")], 0.0, _material(variant_id + "_mat"))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
