"""Bridge repair hut (docs/ra2_buildings/tech/bridge_repair_hut, docs/map_elements/12_tech_buildings): the small hut at the end of a
bridge that an engineer goes into to mend it.

The map building tech_bridge_repair_hut (game/data/neutral/tech.yaml): 1x1, nobody's, never destroyed (RA2's hut is immune), so it has
one look, whole. Made at the size the game draws it, as the tech buildings are, not squashed: the roof's high edge at 0.56. Footprint 1x1,
origin at the centre of the cell on the ground, its door to -Y. The parts:
    body  a concrete footing; a hut of dark green painted boards with white corner trims, a lean-to roof of ribbed grey steel sloping
          back, a door of steel with a yellow and black hazard band over it, a small window on the right; a warning lamp on a short
          bracket over the door (node "lamp", the game may light it); a rack of tools on the left wall, a cable drum and a toolbox at
          its foot
Nodes: "body", "lamp". One GLB per climate's look (object of the table: building/tech_bridge_repair_hut, the winter one @winter):
    bridge_repair_hut       bare
    bridge_repair_hut_snow  snow on its roof and its footing's edge
Budget 2000 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.map_bridge_hut as hut
    print(hut.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "map"
SIZE = 1024
VARIANTS = ("bridge_repair_hut", "bridge_repair_hut_snow")

BOARDS, TRIM, ROOF, STEEL, HAZARD, GLASS, CONCRETE, LAMP, WOOD = (cc.part(i) for i in range(9))
PARTS = 9

PAD = 0.04
HX, HY = 0.28, 0.24
FRONT, BACK = 0.52, 0.42  # the walls' top at the front and at the back


def _top(y):
    return FRONT + (BACK - FRONT) * (y + HY) / (2 * HY)


def _hut(p):
    p.box((-0.42, -0.42, 0.0), (0.42, 0.42, PAD), CONCRETE)
    rings = [[Vector((x, y, PAD)) for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY))],
             [Vector((x, y, _top(y))) for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY))]]
    p.loft(rings, BOARDS)
    for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY)):
        p.box((x - 0.016, y - 0.016, PAD), (x + 0.016, y + 0.016, _top(y) - 0.005), TRIM)
    # The roof of ribbed steel, overhanging, its ribs along the slope.
    over = 0.05
    edge = [(-HX - over, -HY - over * 1.5), (HX + over, -HY - over * 1.5), (HX + over, HY + over), (-HX - over, HY + over)]
    p.loft([[Vector((x, y, _top(max(-HY, min(HY, y))) + 0.005 + dz)) for x, y in edge] for dz in (0.0, 0.02)], ROOF)
    for k in range(7):
        x = -HX + (2 * HX) * k / 6
        p.bar((x, -HY - over * 1.5, _top(-HY) + 0.03), (x, HY + over, _top(HY) + 0.03), 0.006, ROOF)
    # The door of steel, the hazard band over it; the window on the right.
    p.box((-0.12, -HY - 0.012, PAD + 0.01), (0.06, -HY, 0.42), STEEL)
    p.box((0.03, -HY - 0.02, 0.22), (0.05, -HY - 0.012, 0.25), STEEL)  # its handle
    p.box((-0.15, -HY - 0.014, 0.43), (0.09, -HY, 0.47), HAZARD)
    p.box((HX, -0.1, 0.24), (HX + 0.01, 0.08, 0.38), GLASS)
    p.box((HX, -0.12, 0.22), (HX + 0.016, 0.1, 0.24), TRIM)


def _lamp(p):
    p.bar((0.15, -HY, 0.48), (0.15, -HY - 0.06, 0.5), 0.006, STEEL)
    p.turned((0.15, -HY - 0.07, 0.5), 'Z', [(-0.02, 0.022), (0.02, 0.022), (0.032, 0.008)], LAMP, sides=6)


def _gear(p):
    for k, z in enumerate((0.16, 0.3)):  # the rack of tools on the left wall: a board and its handles
        p.box((-HX - 0.012, -0.16, z), (-HX, 0.16, z + 0.02), WOOD)
    for k, y in enumerate((-0.12, -0.04, 0.05, 0.13)):
        p.bar((-HX - 0.03, y, 0.07), (-HX - 0.02, y + 0.01, 0.34), 0.007, WOOD if k % 2 else STEEL)
    p.cylinder((0.3, 0.3, 0.12), 'X', 0.08, 0.05, WOOD, sides=10)  # the cable drum, on its side
    p.cylinder((0.3, 0.3, 0.12), 'X', 0.05, 0.052, STEEL, sides=10)
    p.box((-0.36, -0.36, PAD), (-0.22, -0.28, PAD + 0.07), HAZARD)  # the toolbox


def _material(name, snow):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    up = g.axis(g.normal_world(), 'Z')
    grain = g.noise(30.0, 4.0)
    joints = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 18.0)), -14.0, 1.6))
    boards = g.mix(joints, g.mix(g.noise(5.0, 3.0), lin((0.22, 0.34, 0.24)), lin((0.28, 0.40, 0.28))), lin((0.10, 0.14, 0.10)))
    trim = g.mix(grain, lin((0.86, 0.86, 0.84)), lin((0.92, 0.92, 0.90)))
    roof = g.mix(grain, lin((0.46, 0.47, 0.48)), lin((0.56, 0.56, 0.56)))
    steel = g.mix(grain, lin((0.36, 0.37, 0.39)), lin((0.44, 0.45, 0.47)))
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', g.math('ADD', x, z), 70.0)), 40.0, 0.5))
    hazard = g.mix(stripes, lin((0.95, 0.76, 0.12)), lin((0.10, 0.10, 0.10)))
    glass = g.mix(grain, lin((0.28, 0.36, 0.42)), lin((0.42, 0.52, 0.58)))
    concrete = g.mix(grain, lin((0.58, 0.57, 0.54)), lin((0.66, 0.65, 0.62)))
    lamp = lin((0.95, 0.42, 0.12))
    wood = g.mix(grain, lin((0.50, 0.40, 0.28)), lin((0.58, 0.48, 0.34)))
    albedo = cc.paint(g, mask, (boards, trim, roof, steel, hazard, glass, concrete, lamp, wood))
    if snow:
        cover = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', up, 6.0, -4.6)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(8.0, 3.0), 3.0, -0.4)))
        albedo = g.mix(cover, albedo, lin((0.90, 0.92, 0.95)))
    rough = cc.weighted(g, mask, (0.75, 0.6, 0.5, 0.45, 0.6, 0.15, 0.9, 0.3, 0.85))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.6, 0.7, 0.2, 0.0, 0.0, 0.0, 0.0))
    g.finish(albedo, g.bump(g.math('ADD', g.math('MULTIPLY', joints, -0.5), g.math('MULTIPLY', grain, 0.3)), 0.06, 0.005), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body", "lamp"))
    body, lamp = Parts(), Parts()
    _hut(body)
    _gear(body)
    _lamp(lamp)
    return cc.finish(variant_id, [(body, "body"), (lamp, "lamp")], 0.0, _material(variant_id + "_mat", variant_id.endswith("_snow")))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
