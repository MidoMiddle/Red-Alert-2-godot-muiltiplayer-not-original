"""Buoy, element 13 of the beach set (docs/beach_elements/13_buoy): a float with a light, marking the swimmers' water off a beach.

The map object buoy (game/data/map/objects.yaml) stands on the water and stops nobody; a shot sinks it. Made at the size the game draws
a thing on a cell, as its placeholder: the float 0.26 across, the light at 0.44 over the water. Origin at the centre of the cell on the
water's surface; the float goes under it to -0.08, so the waterline is never its edge. The parts:
    body   the float: a broad waist at the waterline and a cone up from it, a band round it, a lifting eye; on it a small cage of three
           steel legs carrying the light, a radar reflector of crossed plates under the light
    light  the lantern on top: a yellow lens in a dark cap (the game may light it)
Nodes: "body", "light". One GLB per colouring (a cell picks one): buoy_red (red, a white band) and buoy_white (white, a red band).
Budget 500 triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.beach_buoy as buoy
    print(buoy.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"
SIZE = 512
# The float's colour, its band's.
VARIANTS = {
    "buoy_red": ((0.80, 0.12, 0.10), (0.94, 0.93, 0.90)),
    "buoy_white": ((0.94, 0.93, 0.90), (0.80, 0.12, 0.10)),
}

(HULL, BAND, STEEL, LENS, CAP, WET) = (cc.part(i) for i in range(6))
PARTS = 6

FLOOR = -0.08


def _body(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(FLOOR, 0.1), (-0.01, 0.13), (0.03, 0.13)], WET, sides=12)  # the waterline, dark and wet
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.03, 0.13), (0.06, 0.12), (0.18, 0.06), (0.2, 0.045)], HULL, sides=12)
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.085, 0.112), (0.12, 0.096)], BAND, sides=12)
    p.cylinder((0.0, 0.0, 0.215), 'Z', 0.02, 0.015, STEEL, sides=6)  # the lifting eye's boss
    for k in range(3):  # the cage's legs
        a = 2 * math.pi * k / 3
        foot = Vector((math.cos(a) * 0.045, math.sin(a) * 0.045, 0.2))
        top = Vector((math.cos(a) * 0.025, math.sin(a) * 0.025, 0.4))
        p.bar(foot, top, 0.006, STEEL)
    p.cylinder((0.0, 0.0, 0.4), 'Z', 0.035, 0.006, STEEL, sides=8)  # the light's seat
    # The radar reflector: two crossed plates under the light.
    p.box((-0.04, -0.003, 0.29), (0.04, 0.003, 0.36), STEEL)
    p.box((-0.003, -0.04, 0.29), (0.003, 0.04, 0.36), STEEL)


def _light(p):
    p.cylinder((0.0, 0.0, 0.43), 'Z', 0.024, 0.024, LENS, sides=8)
    p.cylinder((0.0, 0.0, 0.462), 'Z', 0.03, 0.008, CAP, sides=8)


def _material(name, colours):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(30.0, 3.0)
    hull, band = colours
    hull_c = g.mix(g.math('MULTIPLY', grain, 0.2), lin(hull), lin(tuple(v * 0.75 for v in hull)))
    band_c = g.mix(g.math('MULTIPLY', grain, 0.2), lin(band), lin(tuple(v * 0.75 for v in band)))
    steel = g.mix(grain, lin((0.30, 0.31, 0.32)), lin((0.42, 0.40, 0.38)))
    lens = lin((1.0, 0.86, 0.36))
    cap = lin((0.10, 0.10, 0.11))
    wet = g.mix(grain, lin((0.14, 0.16, 0.13)), lin((0.22, 0.24, 0.16)))  # dark, with weed
    albedo = cc.paint(g, mask, (hull_c, band_c, steel, lens, cap, wet))
    rough = cc.weighted(g, mask, (0.45, 0.45, 0.5, 0.15, 0.5, 0.3))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.7, 0.0, 0.3, 0.0))
    g.finish(albedo, g.bump(grain, 0.04, 0.004), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body", "light"))
    body, light = Parts(), Parts()
    _body(body)
    _light(light)
    return cc.finish(variant_id, [(body, "body"), (light, "light")], FLOOR, _material(variant_id + "_mat", VARIANTS[variant_id]))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
