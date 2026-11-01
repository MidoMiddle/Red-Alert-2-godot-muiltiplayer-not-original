"""Beach umbrella, element 04 of the beach set (docs/beach_elements/04_beach_umbrella): a striped canopy on a thin pole in the sand.

The map object beach_umbrella (game/data/map/objects.yaml) stops nobody; wheels and tracks crush it. Made at the size the game draws a
thing on a cell, as its placeholder: the pole to 0.72, the canopy 0.76 across. Origin at the centre of the cell on the ground. The parts:
    pole    a thin grey metal pole stuck in a little heap of sand, a joint halfway up, the ribs' hub under the canopy
    canopy  a low cone of eight panels in two colours, a scalloped valance round its edge in the same stripes, a finial on top; dark
            underneath, so the canopy does not melt into the sand under it
Nodes: "pole", "canopy" (the canopy may sway in code). One GLB per colouring, the shape the same (the document: one model, its colourings
textures; the game picks a file per cell):
    beach_umbrella         red and white
    beach_umbrella_yellow  yellow and blue
    beach_umbrella_green   green and white
Budget 800 triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.beach_umbrella as umbrella
    print(umbrella.run_all())
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
# The two colours of the stripes.
VARIANTS = {
    "beach_umbrella": ((0.82, 0.14, 0.12), (0.94, 0.93, 0.90)),
    "beach_umbrella_yellow": ((0.95, 0.78, 0.18), (0.16, 0.36, 0.72)),
    "beach_umbrella_green": ((0.18, 0.56, 0.30), (0.94, 0.93, 0.90)),
}

(METAL, SAND, CLOTH) = (cc.part(i) for i in range(3))
PARTS = 3

TOP = 0.72  # the canopy's peak
EDGE = 0.6  # the height of its rim
RADIUS = 0.38
PANELS = 8


def _pole(p):
    p.cylinder((0.0, 0.0, TOP / 2 - 0.02), 'Z', 0.013, TOP / 2 + 0.02, METAL, sides=6)
    p.cylinder((0.0, 0.0, 0.34), 'Z', 0.019, 0.02, METAL, sides=6)  # the joint
    p.cylinder((0.0, 0.0, EDGE - 0.03), 'Z', 0.024, 0.02, METAL, sides=6)  # the hub of the ribs
    p.turned((0.0, 0.0, 0.0), 'Z', [(-0.02, 0.07), (0.0, 0.06), (0.018, 0.02)], SAND, sides=8)  # the heap round its foot


def _canopy(p):
    # The cone: its rim scalloped, dipping a little between the ribs.
    sides = PANELS * 2
    rings = []
    for k, (r, z) in enumerate(((RADIUS, EDGE), (RADIUS * 0.55, EDGE + 0.07), (RADIUS * 0.12, TOP - 0.01))):
        ring = []
        for i in range(sides):
            a = 2 * math.pi * i / sides
            dip = 0.02 if (k == 0 and i % 2) else 0.0
            ring.append(Vector((math.cos(a) * r, math.sin(a) * r, z - dip)))
        rings.append(ring)
    p.loft(rings, CLOTH)
    # The valance: a short band hanging under the rim.
    band = []
    for z in (EDGE - 0.004, EDGE - 0.04):
        band.append([Vector((math.cos(2 * math.pi * i / sides) * (RADIUS + 0.004), math.sin(2 * math.pi * i / sides) * (RADIUS + 0.004),
                             z - (0.02 if i % 2 else 0.0))) for i in range(sides)])
    p.loft(band, CLOTH)
    p.sphere((0.0, 0.0, TOP + 0.01), 'Z', 0.016, METAL, sides=6, rings=4)


def _material(name, colours):
    """Eight panels in two colours round the canopy, by the angle; the cloth a little faded at its peak, dark underneath; the pole grey
    metal; the heap of sand."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y = g.axis(coord, 'X'), g.axis(coord, 'Y')
    angle = g.math('ARCTAN2', y, x)
    stripe = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', angle, PANELS / 2)), 40.0, 0.5))
    a, b = colours
    cloth = g.mix(stripe, lin(a), lin(b))
    cloth = g.mix(g.math('MULTIPLY', g.noise(14.0, 3.0), 0.07), cloth, lin((0.9, 0.88, 0.84)))  # faded by the sun
    down = g.clamp01(g.math('MULTIPLY_ADD', g.axis(g.normal_world(), 'Z'), -4.0, -0.6))
    cloth = g.mix(down, cloth, lin((0.10, 0.10, 0.11)))
    metal = g.mix(g.noise(20.0, 2.0), lin((0.55, 0.56, 0.58)), lin((0.66, 0.67, 0.68)))
    sand = g.mix(g.noise(30.0, 2.0), lin((0.82, 0.74, 0.58)), lin((0.90, 0.83, 0.68)))
    albedo = cc.paint(g, mask, (metal, sand, cloth))
    rough = cc.weighted(g, mask, (0.4, 0.95, 0.85))
    metallic = g.math('MULTIPLY', mask[0], 0.8)
    g.finish(albedo, g.bump(g.noise(40.0, 2.0), 0.1, 0.01), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("pole", "canopy"))
    pole, canopy = Parts(), Parts()
    _pole(pole)
    _canopy(canopy)
    return cc.finish(variant_id, [(pole, "pole"), (canopy, "canopy")], 0.0, _material(variant_id + "_mat", VARIANTS[variant_id]))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
