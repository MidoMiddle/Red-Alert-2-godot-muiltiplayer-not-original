"""Volleyball net, element 09 of the beach set (docs/beach_elements/09_volleyball_net): a net on two posts across a court in the sand.

The map object volleyball_net (game/data/map/objects.yaml) stops nobody; wheels and tracks crush it. It takes one cell, so the model is
made at the size the game draws a thing on a cell, as its placeholder: the posts 0.9 apart along x and 0.62 tall. Origin at the centre
of the cell on the ground. The parts:
    posts  two blue steel posts in dark sleeves in the sand, two guy ropes from each top out either side to pegs, and the court's
           boundary of white tapes laid flat on the sand round it
    net    the net between them: a white tape along its top, a cord along its foot, and its mesh as thin strings, nine upright and three
           across, so it shows as a grid with the sand seen through it (no transparent texture: the forest's rule,
           docs/art/model_standards.md)
Nodes: "posts", "net". Budget 800 triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.beach_volleyball as volleyball
    print(volleyball.run_all())
"""

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"
SIZE = 512
VARIANTS = ("beach_volleyball_net",)

(POST, SLEEVE, TAPE, STRING, ROPE) = (cc.part(i) for i in range(5))
PARTS = 5

HALF = 0.45  # the posts at x = ±HALF
TOP = 0.62
NET_TOP, NET_FOOT = 0.58, 0.36


def _posts(p):
    for x in (-HALF, HALF):
        p.cylinder((x, 0.0, TOP / 2), 'Z', 0.014, TOP / 2, POST, sides=6)
        p.cylinder((x, 0.0, 0.05), 'Z', 0.024, 0.05, SLEEVE, sides=6)
        p.sphere((x, 0.0, TOP + 0.01), 'Z', 0.018, POST, sides=6, rings=3)
        for y in (-0.22, 0.22):  # two guy ropes, out either side of the net, to their pegs
            p.bar((x, 0.0, TOP - 0.01), (x, y, 0.01), 0.004, ROPE)
            p.box((x - 0.008, y - 0.008, 0.0), (x + 0.008, y + 0.008, 0.03), SLEEVE)
    # The court's boundary: white tapes flat on the sand, a hair over it.
    for (x0, y0, x1, y1) in ((-0.46, -0.3, 0.46, -0.28), (-0.46, 0.28, 0.46, 0.3), (-0.46, -0.3, -0.44, 0.3), (0.44, -0.3, 0.46, 0.3)):
        p.box((x0, y0, 0.0), (x1, y1, 0.004), TAPE)


def _net(p):
    w = HALF - 0.016
    p.box((-w, -0.004, NET_TOP - 0.03), (w, 0.004, NET_TOP), TAPE)  # the top tape
    p.bar((-w, 0.0, NET_FOOT), (w, 0.0, NET_FOOT), 0.003, ROPE)  # the foot cord
    for k in range(1, 10):
        x = -w + 2 * w * k / 10
        p.bar((x, 0.0, NET_FOOT), (x, 0.0, NET_TOP - 0.03), 0.0022, STRING)
    for k in range(1, 4):
        z = NET_FOOT + (NET_TOP - 0.03 - NET_FOOT) * k / 4
        p.bar((-w, 0.0, z), (w, 0.0, z), 0.0022, STRING)


def _material(name):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(30.0, 2.0)
    post = g.mix(grain, lin((0.16, 0.34, 0.62)), lin((0.22, 0.42, 0.70)))
    sleeve = lin((0.16, 0.16, 0.17))
    tape = g.mix(grain, lin((0.92, 0.92, 0.90)), lin((0.97, 0.97, 0.95)))
    string = lin((0.88, 0.88, 0.86))
    rope = lin((0.70, 0.64, 0.52))
    albedo = cc.paint(g, mask, (post, sleeve, tape, string, rope))
    rough = cc.weighted(g, mask, (0.4, 0.6, 0.8, 0.8, 0.9))
    metallic = cc.weighted(g, mask, (0.5, 0.2, 0.0, 0.0, 0.0))
    g.finish(albedo, g.bump(grain, 0.05, 0.005), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("posts", "net"))
    posts, net = Parts(), Parts()
    _posts(posts)
    _net(net)
    return cc.finish(variant_id, [(posts, "posts"), (net, "net")], 0.0, _material(variant_id + "_mat"))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
