"""Sun lounger, element 05 of the beach set (docs/beach_elements/05_sun_lounger): a low lounger with its back raised, on short legs.

The map object sun_lounger (game/data/map/objects.yaml) stops nobody; wheels and tracks crush it. Made at the size the game draws a thing
on a cell, as its placeholder: 0.8 long along y, 0.28 wide, the seat at 0.1, the back raised to 0.31 at the head (-y). Origin at the
centre of the cell on the ground. The parts:
    frame   a white tubular frame: two side rails along the seat and up the back, cross bars at the foot, the knee and the head
    legs    four short dark legs, so the frame does not melt into the sand
    cloth   the fabric stretched over the seat and the back, sagging a little between the rails
Node: "body". One GLB per fabric (the document: one model, its fabrics textures; the game picks a file per cell):
    beach_sun_lounger            blue and white stripes along it
    beach_sun_lounger_turquoise  plain turquoise
    beach_sun_lounger_yellow     plain yellow
Budget 500 triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.beach_lounger as lounger
    print(lounger.run_all())
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
# The fabric's two colours (the same twice: plain).
VARIANTS = {
    "beach_sun_lounger": ((0.16, 0.38, 0.70), (0.94, 0.93, 0.90)),
    "beach_sun_lounger_turquoise": ((0.16, 0.66, 0.68), (0.16, 0.66, 0.68)),
    "beach_sun_lounger_yellow": ((0.95, 0.76, 0.20), (0.95, 0.76, 0.20)),
}

(FRAME, LEG, CLOTH) = (cc.part(i) for i in range(3))
PARTS = 3

HALF = 0.13  # half the width, to the rails
SEAT = 0.1  # the seat's height
FOOT = 0.4  # the foot end, +y
KNEE = -0.04  # where the back hinges
HEAD = (-0.34, 0.31)  # the head end of the back: y, z


def _rails(p):
    for x in (-HALF, HALF):
        p.bar((x, FOOT, SEAT), (x, KNEE, SEAT), 0.009, FRAME)
        p.bar((x, KNEE, SEAT), (x, HEAD[0], HEAD[1]), 0.009, FRAME)
    for y, z in ((FOOT, SEAT), (KNEE, SEAT), HEAD):
        p.bar((-HALF, y, z), (HALF, y, z), 0.008, FRAME)
    for x in (-HALF, HALF):
        for y in (FOOT - 0.03, KNEE + 0.02):
            p.bar((x, y, SEAT - 0.005), (x, y, 0.0), 0.011, LEG)
        # The back's prop, from the frame to the ground under the back.
        p.bar((x, -0.2, 0.2), (x, -0.16, 0.0), 0.008, LEG)


def _cloth(p):
    """The fabric over the seat and the back: one sheet, three across (sagging in the middle), along the rails."""
    stations = []
    for t in (0.0, 0.5, 1.0):  # along the seat, foot to knee
        stations.append((FOOT - 0.02 + (KNEE - FOOT + 0.02) * t, SEAT + 0.006))
    for t in (0.5, 1.0):  # up the back
        stations.append((KNEE + (HEAD[0] + 0.02 - KNEE) * t, SEAT + 0.006 + (HEAD[1] - 0.012 - SEAT) * t))
    rings = []
    for y, z in stations:
        rings.append([Vector((-HALF + 0.008, y, z)), Vector((0.0, y, z - 0.012)), Vector((HALF - 0.008, y, z)),
                      Vector((HALF - 0.008, y, z + 0.008)), Vector((0.0, y, z - 0.004)), Vector((-HALF + 0.008, y, z + 0.008))])
    p.loft(rings, CLOTH)


def _material(name, colours):
    """The frame white enamel, the legs dark grey; the fabric in its colours, stripes along it, a little faded, its underside darker."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    stripe = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', x, 96.0)), 40.0, 0.5))
    a, b = colours
    cloth = g.mix(stripe, lin(a), lin(b))
    cloth = g.mix(g.math('MULTIPLY', g.noise(14.0, 3.0), 0.08), cloth, lin((0.9, 0.88, 0.84)))
    down = g.clamp01(g.math('MULTIPLY_ADD', g.axis(g.normal_world(), 'Z'), -4.0, -0.6))
    cloth = g.mix(g.math('MULTIPLY', down, 0.6), cloth, lin((0.12, 0.12, 0.13)))
    frame = g.mix(g.noise(20.0, 2.0), lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    leg = lin((0.20, 0.21, 0.22))
    albedo = cc.paint(g, mask, (frame, leg, cloth))
    rough = cc.weighted(g, mask, (0.35, 0.5, 0.85))
    g.finish(albedo, g.bump(g.noise(60.0, 2.0), 0.08, 0.01), g.combine(ao, rough, g.math('MULTIPLY', mask[1], 0.6)))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body",))
    body = Parts()
    _rails(body)
    _cloth(body)
    return cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat", VARIANTS[variant_id]))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
