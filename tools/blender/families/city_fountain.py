"""City fountain: element 09 of the city set (docs/city_elements/09_fountain) and its states (appearance.md, 4). City climate: dry
daylight, no snow. A building of the map (neutral_fountain, game/data/neutral/civilian.yaml) with no soldiers and no team zone.

Footprint 2x2 cells, origin at the centre of the footprint on the ground. Made at the size the game draws it, not a storey to a cell
(models.yaml does not squash it): the rim at 0.19, the upper bowl at 0.52, the finial at 0.66; lower than the houses round the square.
The parts:
    basin     a wide round basin of light grey stone in segments, its rim rounded on top, a darker water line inside; a column in the
              middle with a flared foot, a shallow upper bowl and a finial
    water     the water in the basin and in the upper bowl: a node of its own, for the client to hide or to give its shader; the jets
              and the spray are the client's (prompts.md, 4)
    plaza     a round paving of stone round it
No emblems, no text.
Nodes: "basin" (the basin, the column and the bowl, and the pieces of a state), "water", "base" (the plaza). One GLB per state:
    city_fountain               whole
    city_fountain_damaged       damaged: cracks, the water lower, two chips of the rim on the plaza
    city_fountain_abandoned     critically damaged: the rim broken through on one side, the column fallen across the basin, the
                                water low, a pool of it spilled on the plaza through the gap
    city_fountain_ruins         destroyed: an empty basin, its rim broken in places, pieces of it and of the column round it, the
                                stump of the column
Budget 3000 triangles, texture 1024 (docs/city_elements/09_fountain/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_fountain as cfo
    print(cfo.run("city_fountain"))   # or cfo.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_fountain": "intact",
    "city_fountain_damaged": "damaged",
    "city_fountain_abandoned": "abandoned",
    "city_fountain_ruins": "ruins",
}

STONE, WATER, PAVE, DARK = (cc.part(i) for i in range(4))
PARTS = 4

PAVE_H = 0.03
SEGMENTS = 28
# The section of the basin's wall, (radius, height), round its outline: the outer face, the rounded rim, the inner face.
WALL = ((0.84, PAVE_H), (0.84, 0.15), (0.825, 0.185), (0.8, 0.195), (0.74, 0.195), (0.72, 0.18), (0.71, 0.08), (0.71, PAVE_H))
FLOOR = 0.08
WATER_Z = {"intact": 0.15, "damaged": 0.13, "abandoned": 0.1}
# The column: (height, radius) up from the floor of the basin to the finial.
COLUMN = ((FLOOR, 0.17), (0.12, 0.17), (0.15, 0.12), (0.19, 0.075), (0.41, 0.065), (0.44, 0.09), (0.49, 0.24), (0.52, 0.26),
          (0.53, 0.24), (0.51, 0.08), (0.56, 0.035), (0.6, 0.03), (0.63, 0.05), (0.66, 0.01))
# The segments of the rim that are gone: the gap of the abandoned one, the breaks of the ruins.
GONE = {"abandoned": {0, 1}, "ruins": {0, 1, 2, 9, 10, 17, 22, 23}}


# --- the basin -----------------------------------------------------------------------

def _wall(p, gone):
    """The wall of the basin as segments, each its section swept between two angles: so a broken one can leave some out."""
    for k in range(SEGMENTS):
        if k in gone:
            continue
        a0, a1 = 2 * math.pi * k / SEGMENTS, 2 * math.pi * (k + 1) / SEGMENTS
        p.loft([[Vector((r * math.cos(a), r * math.sin(a), z)) for r, z in WALL] for a in (a0, a1)], STONE)


def _column(p, variant):
    """The column with its upper bowl. Abandoned: fallen across the basin, its foot left; ruins: only a stump."""
    if variant == "ruins":
        p.turned((0.0, 0.0, 0.0), 'Z', [(FLOOR, 0.17), (0.12, 0.17), (0.15, 0.12), (0.19, 0.075), (0.26, 0.065), (0.27, 0.01)], STONE, sides=14)
        return
    if variant == "abandoned":
        p.turned((0.0, 0.0, 0.0), 'Z', [(FLOOR, 0.17), (0.12, 0.17), (0.15, 0.12), (0.19, 0.075), (0.23, 0.065), (0.24, 0.01)], STONE, sides=14)
        fallen = cc.made(p, lambda: p.turned((0.0, 0.0, 0.0), 'Z', list(COLUMN[3:]), STONE, sides=14))
        verts = list({v for f in fallen for v in f.verts})
        m = Matrix.Rotation(math.radians(80), 3, 'Y')
        for v in verts:
            v.co = m @ (v.co - Vector((0.0, 0.0, COLUMN[3][0]))) + Vector((0.12, 0.05, FLOOR + 0.27))
        return
    p.turned((0.0, 0.0, 0.0), 'Z', list(COLUMN), STONE, sides=16)


def _water(p, variant):
    z = WATER_Z[variant]
    p.turned((0.0, 0.0, 0.0), 'Z', [(z - 0.01, 0.712), (z, 0.712), (z, 0.01)], WATER, sides=SEGMENTS)
    if variant == "intact":
        p.turned((0.0, 0.0, 0.0), 'Z', [(0.505, 0.22), (0.515, 0.22), (0.515, 0.01)], WATER, sides=16)  # in the upper bowl
    if variant == "abandoned":  # the pool spilled on the plaza through the gap
        p.loft([[Vector((0.84 + 0.5 * r * math.cos(a), 0.16 + 0.32 * r * math.sin(a), z0)) for a in (2 * math.pi * i / 12 for i in range(12))]
                for r, z0 in ((1.0, PAVE_H + 0.002), (0.98, PAVE_H + 0.006))], WATER)


PIECES = {"damaged": (((0.95, 0.4, PAVE_H + 0.03), (0.1, 0.06, 0.05), (0.2, 0.1, 0.6)), ((-0.6, -0.8, PAVE_H + 0.03), (0.08, 0.06, 0.05), (0.1, 0.3, 2.0))),
          "abandoned": (((0.95, 0.05, PAVE_H + 0.04), (0.14, 0.08, 0.07), (0.2, 0.1, 0.4)), ((0.92, 0.38, PAVE_H + 0.03), (0.1, 0.07, 0.05), (0.1, 0.3, 1.2)),
                        ((0.75, 0.2, FLOOR + 0.04), (0.12, 0.08, 0.06), (0.3, 0.2, 0.3))),
          "ruins": (((0.95, 0.1, PAVE_H + 0.04), (0.16, 0.09, 0.07), (0.2, 0.1, 0.4)), ((-0.3, 0.92, PAVE_H + 0.04), (0.14, 0.08, 0.06), (0.1, 0.2, 1.6)),
                    ((0.2, -0.93, PAVE_H + 0.04), (0.15, 0.08, 0.06), (0.3, 0.1, 0.2)), ((0.3, 0.2, FLOOR + 0.05), (0.3, 0.1, 0.09), (1.4, 0.2, 0.7)),
                    ((-0.25, -0.3, FLOOR + 0.06), (0.4, 0.4, 0.04), (0.3, 0.2, 0.4)), ((-0.5, 0.4, FLOOR + 0.04), (0.12, 0.1, 0.07), (0.2, 0.3, 1.0)),
                    ((0.9, -0.6, PAVE_H + 0.03), (0.1, 0.08, 0.05), (0.0, 0.2, 0.3)))}


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Light grey stone, darker low down and inside, a dark water line; blue-green water; round paving; per state, cracks and the
    stains of water gone."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.15)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(25.0, 4.0)
    radius = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', x, x), g.math('MULTIPLY', y, y)))

    stone = g.mix(grain, lin((0.66, 0.65, 0.62)), lin((0.76, 0.75, 0.72)))
    inside = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', radius, -60.0, 43.5)), g.clamp01(g.math('MULTIPLY_ADD', z, -12.0, 2.6)))
    stone = g.mix(inside, stone, lin((0.46, 0.46, 0.44)))  # darker inside the basin, where the water stands
    line = g.math('MULTIPLY', cc.band(g, z, WATER_Z["intact"], WATER_Z["intact"] + 0.025), g.clamp01(g.math('MULTIPLY_ADD', radius, -60.0, 43.5)))
    stone = g.mix(g.math('MULTIPLY', line, 0.8), stone, lin((0.28, 0.30, 0.26)))  # the water line
    water = g.mix(g.noise(6.0, 3.0), lin((0.18, 0.38, 0.40)), lin((0.30, 0.52, 0.52)))
    rings = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', radius, 6.0)), -30.0, 1.5))
    pave = g.mix(rings, g.mix(grain, lin((0.58, 0.56, 0.52)), lin((0.66, 0.64, 0.60))), lin((0.42, 0.41, 0.39)))
    dark = lin((0.08, 0.08, 0.08))

    albedo = cc.paint(g, mask, (stone, water, pave, dark))
    if variant != "intact":
        albedo = g.mix(g.math('MULTIPLY', mask[0], cc.cracks(g, 8.0)), albedo, lin((0.22, 0.22, 0.21)))
    if variant == "ruins":
        albedo = g.mix(g.math('MULTIPLY', g.math('ADD', mask[0], mask[2]), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 4.0, -1.8))),
                       albedo, lin((0.36, 0.34, 0.31)))  # dust and mud where the water was

    rough = cc.weighted(g, mask, (0.8, 0.05, 0.85, 0.9))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[2], g.math('MULTIPLY', rings, -1.0), g.noise(50.0, 3.0)), 0.04, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("basin", "water", "base"))
    variant = VARIANTS[variant_id]
    basin = Parts()
    _wall(basin, GONE.get(variant, set()))
    basin.turned((0.0, 0.0, 0.0), 'Z', [(PAVE_H, 0.715), (FLOOR, 0.715), (FLOOR, 0.01)], STONE, sides=SEGMENTS)  # the floor
    _column(basin, variant)
    for centre, size, turn in PIECES.get(variant, ()):
        cc.block(basin, centre, size, turn, STONE)
    parts = [(basin, "basin")]
    if variant != "ruins":
        water = Parts()
        _water(water, variant)
        parts.append((water, "water"))
    base = Parts()
    base.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.98), (PAVE_H, 0.97), (PAVE_H, 0.01)], PAVE, sides=32)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes basin, water and base."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
