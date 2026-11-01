"""Beached boat, element 17 of the beach set (docs/beach_elements/17_beached_boat): a wooden rowing boat drawn up on the sand.

The map object beached_boat (game/data/map/objects.yaml) stops everybody on its cell; 300 hp. Made at the size the game draws a thing on
a cell, as its placeholder: 0.86 long along the cell's diagonal, 0.38 wide, its gunwale at 0.24, heeled a little onto its side. Origin
at the centre of the cell on the ground. The parts:
    hull  the hull, open, its planking painted outside, a white band under the gunwale, the bottom red-brown, chips of bare wood; inside
          bare wood, two thwarts across; a rope from its bow to a stake in the sand
    oars  the second oar, lying on the sand beside it (the first lies in the boat across the thwarts, with the hull)
Nodes: "hull", "oars". One GLB per hull's paint (a cell picks one): beached_boat (blue), beached_boat_green, beached_boat_white.
Budget 1500 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.beach_boat as boat
    print(boat.run_all())
"""

import math

from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"
SIZE = 1024
VARIANTS = {
    "beached_boat": (0.16, 0.34, 0.62),
    "beached_boat_green": (0.18, 0.44, 0.30),
    "beached_boat_white": (0.90, 0.89, 0.86),
}

(PAINT, WOOD, BOTTOM, ROPE, OAR) = (cc.part(i) for i in range(5))
PARTS = 5

LENGTH, BEAM, DEPTH = 0.86, 0.38, 0.2
SHEER = 0.24  # the gunwale's height amidships
THICK = 0.014
TURN = math.radians(40)  # the boat lies along the cell's diagonal
HEEL = math.radians(9)


def _section(t):
    """The half beam, the depth and the gunwale's height at t (0 the transom, 1 the bow)."""
    w = 0.5 * BEAM * (0.78 + 0.22 * math.sin(math.pi * min(1.0, t * 1.15))) * (1.0 - max(0.0, t - 0.55) / 0.45) ** 0.9
    w = max(w, 0.012)
    sheer = SHEER + 0.06 * max(0.0, t - 0.6) / 0.4 + 0.02 * (1 - t)
    return w, DEPTH * (0.85 + 0.15 * math.sin(math.pi * t)), sheer


def _hull(p):
    rings = []
    for k in range(9):
        t = k / 8
        y = -LENGTH / 2 + LENGTH * t
        w, d, s = _section(t)
        keel = s - d
        outer = [(-w, s), (-w * 0.85, s - d * 0.6), (0.0, keel), (w * 0.85, s - d * 0.6), (w, s)]
        wi = max(w - THICK, 0.002)
        inner = [(wi, s), (wi * 0.85, s - d * 0.6 + THICK), (0.0, keel + THICK * 1.5), (-wi * 0.85, s - d * 0.6 + THICK), (-wi, s)]
        rings.append([Vector((x, y, z)) for x, z in outer + inner])
    p.loft(rings, PAINT)
    # The thwarts and the transom's knee.
    for y in (-0.12, 0.14):
        w, d, s = _section((y + LENGTH / 2) / LENGTH)
        p.box((-w + THICK, y - 0.035, s - 0.06), (w - THICK, y + 0.035, s - 0.045), WOOD)
    # The rope from the bow's ring down to a stake in the sand ahead.
    bow = Vector((0.0, LENGTH / 2 - 0.01, SHEER + 0.06))
    p.bar(bow, (0.08, LENGTH / 2 + 0.06, 0.01), 0.005, ROPE)


def _oar(p, a, b):
    """An oar from a towards b: its loom, then its blade, flat."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    p.bar(a, a + d * 0.42, 0.007, OAR)
    blade = a + d * 0.42
    p.bar(blade, blade + d * 0.14, 0.004, OAR)
    side = d.cross(Vector((0.0, 0.0, 1.0))).normalized()
    p.loft([[blade + side * 0.025 + Vector((0, 0, -0.002)), blade - side * 0.025 + Vector((0, 0, -0.002)),
             blade - side * 0.025 + Vector((0, 0, 0.002)), blade + side * 0.025 + Vector((0, 0, 0.002))],
            [q + d * 0.15 for q in (blade + side * 0.03 + Vector((0, 0, -0.002)), blade - side * 0.03 + Vector((0, 0, -0.002)),
                                    blade - side * 0.03 + Vector((0, 0, 0.002)), blade + side * 0.03 + Vector((0, 0, 0.002)))]], OAR)


def _place(meshes):
    """Heels the boat onto its side a little, sets its keel a hair into the sand, and lays it and the oar beside it along the cell's
    diagonal."""
    hull = next(obj for obj in meshes if obj.name.endswith("hull"))
    hull.data.transform(Matrix.Rotation(HEEL, 4, 'Y'))
    low = min(v.co.z for v in hull.data.vertices)
    hull.data.transform(Matrix.Translation((0.0, 0.0, -low - 0.012)))
    for obj in meshes:
        obj.data.transform(Matrix.Rotation(TURN, 4, 'Z'))
        obj.data.update()


def _material(name, paint):
    """The painted planking with chips of bare wood and a white band under the gunwale; the bottom red-brown; bare wood inside and on
    the thwarts; rope; the oars' light wood; sand on its foot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(26.0, 4.0)
    wood = g.mix(grain, lin((0.56, 0.44, 0.30)), lin((0.66, 0.54, 0.38)))
    planks = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 30.0)), -12.0, 1.4))
    paint_c = g.mix(g.math('MULTIPLY', g.noise(8.0, 3.0), 0.2), lin(paint), lin(tuple(v * 0.8 for v in paint)))
    paint_c = g.mix(cc.band(g, z, 0.2, 0.26), paint_c, lin((0.93, 0.92, 0.88)))  # the band under the gunwale
    chips = g.clamp01(g.math('MULTIPLY_ADD', g.noise(14.0, 4.0), 7.0, -4.3))
    paint_c = g.mix(chips, paint_c, wood)
    paint_c = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, -25.0, 2.6)), paint_c, lin((0.42, 0.18, 0.12)))  # the red-brown bottom
    inside = g.clamp01(g.math('MULTIPLY_ADD', g.axis(g.normal_world(), 'Z'), 3.0, -0.6))  # the faces looking up are inside
    hull = g.mix(inside, paint_c, wood)
    hull = g.mix(g.math('MULTIPLY', planks, 0.5), hull, lin((0.12, 0.10, 0.08)))
    rope = lin((0.62, 0.55, 0.40))
    oar = g.mix(grain, lin((0.72, 0.62, 0.46)), lin((0.80, 0.70, 0.54)))
    albedo = cc.paint(g, mask, (hull, wood, lin((0.42, 0.18, 0.12)), rope, oar))
    sand = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 1.4)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(10.0, 2.0), 3.0, -1.0)))
    albedo = g.mix(sand, albedo, lin((0.86, 0.78, 0.62)))
    rough = cc.weighted(g, mask, (0.6, 0.85, 0.7, 0.9, 0.75))
    g.finish(albedo, g.bump(g.math('ADD', g.math('MULTIPLY', planks, -0.5), grain), 0.08, 0.005), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("hull", "oars"))
    hull, oars = Parts(), Parts()
    _hull(hull)
    _oar(hull, (-0.1, -0.3, SHEER - 0.03), (0.06, 0.3, SHEER - 0.02))  # in the boat, across the thwarts
    _oar(oars, (0.3, -0.3, 0.008), (0.26, 0.3, 0.008))  # on the sand beside it
    meshes, mat, graph = cc.finish(variant_id, [(hull, "hull"), (oars, "oars")], -1.0, _material(variant_id + "_mat", VARIANTS[variant_id]))
    _place(meshes)
    return meshes, mat, graph


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
