"""Beach shower, element 07 of the beach set (docs/beach_elements/07_beach_shower): a pipe with a shower head over a pad by the way in.

The map object beach_shower (game/data/map/objects.yaml) stops nobody; wheels and tracks crush it. Made at the size the game draws a thing
on a cell, as its placeholder: the pipe to 0.95, its gooseneck bending over the pad. Origin at the centre of the cell on the ground. The
parts:
    pipe  a blue painted steel pipe from the pad's corner, bending at the top into a gooseneck over the pad's middle; a tap with a red
          wheel on it at hand height, a foot tap low down, rust at the joints
    head  the round chrome shower head at the gooseneck's end, facing down
    base  a square pad of light concrete, a dark steel grate in its middle where the water runs off, sand blown on its edges
Nodes: "pipe", "head", "base", and the empty "water_anchor" under the head, where the game may let water fall (particles). Budget 500
triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.beach_shower as shower
    print(shower.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"
SIZE = 512
VARIANTS = ("beach_shower",)

(PIPE, CHROME, RED, CONCRETE, GRATE) = (cc.part(i) for i in range(5))
PARTS = 5

PAD, PAD_H = 0.22, 0.03
FOOT = Vector((-0.15, 0.15, PAD_H))  # where the pipe rises, at the pad's back corner
TOP = 0.95
HEAD = Vector((0.0, 0.0, 0.84))  # the shower head, over the pad's middle


def _pipe(p):
    r = 0.016
    p.bar(FOOT, (FOOT.x, FOOT.y, TOP - 0.05), r, PIPE)
    # The gooseneck: up, over and down to the head, in short straight lengths.
    bend = [Vector((FOOT.x, FOOT.y, TOP - 0.05)), Vector((FOOT.x + 0.02, FOOT.y - 0.02, TOP)), Vector((-0.07, 0.07, TOP + 0.01)),
            Vector((-0.02, 0.02, TOP - 0.03)), HEAD + Vector((0.0, 0.0, 0.03))]
    for a, b in zip(bend, bend[1:]):
        p.bar(a, b, r, PIPE)
    p.cylinder((FOOT.x, FOOT.y, PAD_H + 0.02), 'Z', 0.03, 0.02, PIPE, sides=8)  # its flange on the pad
    # The tap at hand height, its red wheel facing the pad; the foot tap low.
    for z, size in ((0.5, 0.032), (0.16, 0.024)):
        p.bar((FOOT.x, FOOT.y, z), (FOOT.x + 0.045, FOOT.y - 0.045, z), 0.01, PIPE)
        p.cylinder((FOOT.x + 0.05, FOOT.y - 0.05, z), 'Z', size, 0.006, RED, sides=8)
    p.bar((FOOT.x + 0.03, FOOT.y - 0.03, 0.16), (FOOT.x + 0.04, FOOT.y - 0.04, 0.12), 0.008, CHROME)  # the foot tap's spout


def _head(p):
    p.turned(HEAD, 'Z', [(-0.012, 0.055), (0.0, 0.06), (0.02, 0.03), (0.035, 0.012)], CHROME, sides=10)


def _base(p):
    p.box((-PAD, -PAD, 0.0), (PAD, PAD, PAD_H), CONCRETE)
    p.box((-0.11, -0.11, PAD_H), (0.11, 0.11, PAD_H + 0.006), GRATE)


def _material(name):
    """Blue paint on the pipe with rust at its joints and foot; chrome; a red wheel; light concrete with sand on its edges; a dark grate
    of slots, wet."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 3.0)
    pipe = g.mix(g.noise(10.0, 3.0), lin((0.16, 0.36, 0.62)), lin((0.22, 0.44, 0.70)))
    rust = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(14.0, 4.0), 5.0, -2.6)),
                  g.math('ADD', cc.band(g, z, 0.0, 0.12), g.clamp01(g.math('MULTIPLY_ADD', z, 10.0, -9.2))))
    pipe = g.mix(g.clamp01(rust), pipe, lin((0.45, 0.25, 0.12)))
    chrome = g.mix(grain, lin((0.70, 0.71, 0.72)), lin((0.82, 0.83, 0.84)))
    red = lin((0.75, 0.12, 0.10))
    reach = g.math('MAXIMUM', g.math('ABSOLUTE', x), g.math('ABSOLUTE', y))
    sand = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', reach, 30.0, -5.6)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 2.0), 3.0, -0.8)))
    concrete = g.mix(sand, g.mix(grain, lin((0.66, 0.65, 0.62)), lin((0.76, 0.75, 0.72))), lin((0.86, 0.78, 0.62)))
    slots = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', x, 180.0)), 6.0, -2.0))
    grate = g.mix(slots, lin((0.20, 0.21, 0.22)), lin((0.04, 0.04, 0.05)))
    albedo = cc.paint(g, mask, (pipe, chrome, red, concrete, grate))
    rough = cc.weighted(g, mask, (0.5, 0.15, 0.5, 0.9, 0.3))
    metallic = cc.weighted(g, mask, (0.3, 1.0, 0.0, 0.0, 0.8))
    g.finish(albedo, g.bump(g.math('ADD', grain, slots), 0.08, 0.005), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("pipe", "head", "base", "water_anchor"))
    pipe, head, base = Parts(), Parts(), Parts()
    _pipe(pipe)
    _head(head)
    _base(base)
    return cc.finish(variant_id, [(pipe, "pipe"), (head, "head"), (base, "base")], 0.0, _material(variant_id + "_mat"))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, anchors=(("water_anchor", tuple(HEAD - Vector((0.0, 0.0, 0.02)))),),
                  folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
