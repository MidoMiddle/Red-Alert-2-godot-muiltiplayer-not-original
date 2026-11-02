"""Bridge repair hut (docs/ra2_buildings/tech/bridge_repair_hut, docs/map_elements/12_tech_buildings): the small hut at the end of a
bridge that an engineer goes into to mend it, and a commando to bring the bridge down.

The map building tech_bridge_repair_hut (game/data/neutral/tech.yaml): 1x1, nobody's, never destroyed (RA2's hut is immune), so it has
one look, whole. Made at the size the game draws it, as the tech buildings are, not squashed. It must be found at a glance by the
bridge (the owner, 2026-11-02, after RA2's): squat and filling its cell, its roof bright yellow. Footprint 1x1, origin at the centre of
the cell on the ground, its door to -Y. The parts:
    body  a concrete pad as wide as the cell; low walls of dark tarred timber between steel corner posts, a steel door with a band of
          yellow and black hazard stripes over it, a small window on each side; a hipped roof of yellow painted steel over the whole
          hut, overhanging, framed at its eaves by a dark steel rim, a dark cap along its short ridge; a cable drum and a toolbox by
          the wall
    lamp  the orange warning lamp on the ridge (the game may light it)
Nodes: "body", "lamp". One GLB per climate's look (object of the table: building/tech_bridge_repair_hut, the winter one @winter):
    bridge_repair_hut       bare
    bridge_repair_hut_snow  snow on the roof and the pad's edge, in patches: the yellow still shows
Budget 2000 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.map_bridge_hut as hut
    print(hut.run_all())
"""

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
HX, HY = 0.36, 0.33  # the walls
EAVE = 0.27
RIDGE = 0.53
OVER = 0.06  # the roof over the walls


def _hut(p):
    p.box((-0.47, -0.47, 0.0), (0.47, 0.47, PAD), CONCRETE)
    p.box((-HX, -HY, PAD), (HX, HY, EAVE), BOARDS)
    for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY)):  # the steel corner posts
        p.box((x - 0.025, y - 0.025, PAD), (x + 0.025, y + 0.025, EAVE), TRIM)
    # The hipped roof: its eaves over the walls, its ridge along x, short; the rim of dark steel round the eaves.
    ex, ey = HX + OVER, HY + OVER
    eaves = [Vector((-ex, -ey, EAVE)), Vector((ex, -ey, EAVE)), Vector((ex, ey, EAVE)), Vector((-ex, ey, EAVE))]
    ridge = [Vector((-0.12, 0.0, RIDGE)), Vector((0.12, 0.0, RIDGE)), Vector((0.12, 0.0, RIDGE)), Vector((-0.12, 0.0, RIDGE))]
    p.loft([[v - Vector((0, 0, 0.03)) for v in eaves], eaves, ridge], ROOF)
    for (a, b) in ((eaves[0], eaves[1]), (eaves[1], eaves[2]), (eaves[2], eaves[3]), (eaves[3], eaves[0])):
        p.bar(a - Vector((0, 0, 0.015)), b - Vector((0, 0, 0.015)), 0.018, TRIM)
    p.bar((-0.13, 0.0, RIDGE + 0.008), (0.13, 0.0, RIDGE + 0.008), 0.018, TRIM)  # the ridge cap
    # The door of steel and the hazard band over it; a window on each side.
    p.box((-0.11, -HY - 0.012, PAD + 0.01), (0.07, -HY, 0.19), STEEL)
    p.box((0.04, -HY - 0.02, 0.1), (0.06, -HY - 0.012, 0.13), TRIM)  # its handle
    p.box((-0.3, -HY - 0.014, 0.195), (0.3, -HY, 0.235), HAZARD)
    for x in (-HX, HX):
        out = 1 if x > 0 else -1
        p.box((x - 0.006 * (1 - out), -0.09, 0.13), (x + 0.006 * (1 + out), 0.09, 0.21), GLASS)
        p.box((x - 0.008 * (1 - out), -0.1, 0.12), (x + 0.008 * (1 + out), 0.1, 0.13), TRIM)


def _lamp(p):
    p.turned((0.0, 0.0, RIDGE + 0.02), 'Z', [(0.0, 0.03), (0.035, 0.03), (0.05, 0.012)], LAMP, sides=8)


def _gear(p):
    p.cylinder((0.4, 0.3, 0.1), 'X', 0.065, 0.04, WOOD, sides=10)  # the cable drum, on its side
    p.cylinder((0.4, 0.3, 0.1), 'X', 0.04, 0.042, STEEL, sides=10)
    p.box((-0.44, -0.44, PAD), (-0.3, -0.37, PAD + 0.06), HAZARD)  # the toolbox


def _material(name, snow):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    up = g.axis(g.normal_world(), 'Z')
    grain = g.noise(30.0, 4.0)
    joints = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 22.0)), -14.0, 1.6))  # the timbers, lying
    boards = g.mix(joints, g.mix(g.noise(5.0, 3.0), lin((0.24, 0.22, 0.20)), lin((0.30, 0.28, 0.25))), lin((0.12, 0.11, 0.10)))
    trim = g.mix(grain, lin((0.22, 0.23, 0.24)), lin((0.28, 0.29, 0.30)))
    ribs = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', g.math('ADD', x, y), 60.0)), 0.5, 0.5))
    roof = g.mix(ribs, lin((0.86, 0.66, 0.10)), lin((0.94, 0.76, 0.16)))
    roof = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(14.0, 4.0), 5.0, -3.4)), roof, lin((0.56, 0.44, 0.22)))  # worn to the steel
    steel = g.mix(grain, lin((0.36, 0.37, 0.39)), lin((0.44, 0.45, 0.47)))
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', g.math('ADD', x, z), 70.0)), 40.0, 0.5))
    hazard = g.mix(stripes, lin((0.95, 0.76, 0.12)), lin((0.10, 0.10, 0.10)))
    glass = g.mix(grain, lin((0.28, 0.36, 0.42)), lin((0.42, 0.52, 0.58)))
    concrete = g.mix(grain, lin((0.58, 0.57, 0.54)), lin((0.66, 0.65, 0.62)))
    lamp = lin((0.95, 0.42, 0.12))
    wood = g.mix(grain, lin((0.50, 0.40, 0.28)), lin((0.58, 0.48, 0.34)))
    albedo = cc.paint(g, mask, (boards, trim, roof, steel, hazard, glass, concrete, lamp, wood))
    if snow:
        cover = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', up, 6.0, -4.0)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(7.0, 3.0), 3.0, -1.0)))
        albedo = g.mix(cover, albedo, lin((0.90, 0.92, 0.95)))
    rough = cc.weighted(g, mask, (0.85, 0.5, 0.45, 0.45, 0.6, 0.15, 0.9, 0.3, 0.85))
    metallic = cc.weighted(g, mask, (0.0, 0.6, 0.3, 0.7, 0.2, 0.0, 0.0, 0.0, 0.0))
    g.finish(albedo, g.bump(g.math('ADD', g.math('MULTIPLY', joints, -0.5), g.math('MULTIPLY', ribs, 0.4)), 0.06, 0.005), g.combine(ao, rough, metallic))
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
