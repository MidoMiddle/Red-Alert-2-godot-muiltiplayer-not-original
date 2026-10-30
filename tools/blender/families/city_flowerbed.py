"""City flowerbed: element 21 of the city set (docs/city_elements/21_flowerbed) and its states (appearance.md, 4). City climate: dry
daylight, no snow. A map object (flowerbed, game/data/map/objects.yaml): no soldiers, no team zone.

Footprint 1x1 cell, origin at the centre of the cell on the ground, its length along x. Made at the size the game draws it, as the bench
(the game does not squash map objects): 0.86 long and 0.44 wide, the edge at 0.12, the shrubs to about 0.26: low, under the bench, so
it does not read as an obstacle (appearance.md, 6). The parts:
    edge      a raised border of grey stone blocks round the bed
    soil      dark brown soil with mulch inside it
    plants    three low dark green shrubs down the middle and clumps of red and yellow flowers along the edges
No emblems, no text.
Nodes: "edge", "soil", "plants" (prompts.md, 4). One GLB per state:
    city_flowerbed              whole
    city_flowerbed_damaged      damaged: a block of the edge cracked and pushed out, the flowers on one side pressed flat
    city_flowerbed_ruins        destroyed: the edge broken, its blocks about, the soil spilled over the pavement, the plants crushed
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one wait for the
client to show them.
Budget 500 triangles, texture 1024 (docs/city_elements/21_flowerbed/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_flowerbed as cfb
    print(cfb.run("city_flowerbed"))   # or cfb.run_all()
"""

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_flowerbed": "intact",
    "city_flowerbed_damaged": "damaged",
    "city_flowerbed_ruins": "ruins",
}

STONE, SOIL, LEAF, RED, YELLOW = (cc.part(i) for i in range(5))
PARTS = 5

HX, HY = 0.43, 0.22
EDGE_T = 0.05
EDGE_H = 0.12
SOIL_Z = 0.1
SHRUBS = ((-0.25, 0.0), (0.0, 0.02), (0.25, -0.01))
FLOWERS = tuple((x, y, RED if k % 2 else YELLOW) for k, (x, y) in enumerate(
    ((-0.36, -0.12), (-0.36, 0.12), (-0.125, -0.12), (-0.125, 0.12), (0.125, -0.12), (0.125, 0.12), (0.36, -0.12), (0.36, 0.12))))


def _edge(p, variant):
    """The border: four runs of stone blocks. Damaged: one block in the front pushed out and tilted; destroyed: broken low, the
    blocks about."""
    blocks = []
    for x0, x1 in ((-HX, -0.14), (-0.14, 0.14), (0.14, HX)):
        blocks.append(((x0, -HY, 0.0), (x1, -HY + EDGE_T, EDGE_H)))
        blocks.append(((x0, HY - EDGE_T, 0.0), (x1, HY, EDGE_H)))
    blocks.append(((-HX, -HY + EDGE_T, 0.0), (-HX + EDGE_T, HY - EDGE_T, EDGE_H)))
    blocks.append(((HX - EDGE_T, -HY + EDGE_T, 0.0), (HX, HY - EDGE_T, EDGE_H)))
    for k, (lo, hi) in enumerate(blocks):
        if variant == "damaged" and k == 2:
            cc.block(p, (0.0, -HY - 0.03, 0.05), (0.27, EDGE_T, EDGE_H), (0.3, 0.0, 0.15), STONE)
            continue
        if variant == "ruins":
            if k in (0, 2, 3, 6):
                c = ((lo[0] + hi[0]) / 2, (lo[1] + hi[1]) / 2, 0.025)
                size = (hi[0] - lo[0], hi[1] - lo[1], 0.05)
                p.box((c[0] - size[0] / 2, c[1] - size[1] / 2, 0.0), (c[0] + size[0] / 2, c[1] + size[1] / 2, 0.05), STONE)
            continue
        p.box(lo, hi, STONE)
    if variant == "ruins":
        for c, s, t in (((0.05, -HY - 0.12, 0.03), (0.26, 0.05, 0.06), (0.2, 0.1, 0.4)), ((-0.3, HY + 0.1, 0.03), (0.25, 0.05, 0.06), (0.1, 0.3, -0.5)),
                        ((HX + 0.08, 0.05, 0.03), (0.05, 0.3, 0.06), (0.2, 0.0, 0.3)), ((0.32, HY + 0.12, 0.025), (0.12, 0.05, 0.05), (0.3, 0.1, 1.2))):
            cc.block(p, c, s, t, STONE)


def _soil(p, variant):
    if variant == "ruins":  # spilled out over the pavement in a low heap
        p.loft([[Vector((x * s, y * s, z)) for x, y in ((HX, -HY), (HX, HY), (-HX, HY), (-HX, -HY))]
                for s, z in ((1.25, 0.0), (1.0, 0.04), (0.6, 0.07))], SOIL)
    else:
        p.box((-HX + EDGE_T, -HY + EDGE_T, 0.0), (HX - EDGE_T, HY - EDGE_T, SOIL_Z), SOIL)


def _plants(p, variant):
    """The shrubs as low lumpy domes, the flowers as clumps of two small cushions of colour. Damaged: the front flowers flat;
    destroyed: the shrubs pressed low and pushed about, a few flowers left."""
    base = 0.07 if variant == "ruins" else SOIL_Z
    for k, (x, y) in enumerate(SHRUBS):
        r = 0.11 + 0.015 * (k % 2)
        squash = 0.45 if variant == "ruins" else 1.0
        shift = (0.06 * (k - 1), 0.05) if variant == "ruins" else (0.0, 0.0)
        p.turned((x + shift[0], y + shift[1], base), 'Z', [(0.0, r * 0.8), (0.06 * squash, r), (0.12 * squash, r * 0.85),
                                                           (0.16 * squash, r * 0.45), (0.17 * squash, 0.01)], LEAF, sides=7)
    for k, (x, y, colour) in enumerate(FLOWERS):
        flat = variant == "damaged" and y < 0 or variant == "ruins" and k % 3
        if variant == "ruins" and k % 3 == 0:
            continue
        top = base + (0.012 if flat else 0.07)
        for dx, dy in ((0.0, 0.0), (0.04, 0.025)):  # two heads a clump, each a small cushion of colour
            hx, hy = x + dx + (0.04 if flat else 0.0), y + dy * (1 if y < 0 else -1)
            p.box((hx - 0.026, hy - 0.026, base), (hx + 0.026, hy + 0.026, top + 0.025), colour)


def _material(name, variant):
    """Grey stone, dark brown soil with lighter mulch, dark green leaves, red and yellow flowers; dirt over the stone when broken."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(30.0, 4.0)
    stone = g.mix(grain, lin((0.56, 0.56, 0.54)), lin((0.66, 0.66, 0.63)))
    if variant != "intact":
        stone = g.mix(g.math('MULTIPLY', cc.cracks(g, 10.0), 1.0), stone, lin((0.25, 0.24, 0.22)))
    soil = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(40.0, 3.0), 4.0, -2.4)), lin((0.20, 0.13, 0.08)), lin((0.42, 0.30, 0.18)))
    leaf = g.mix(g.noise(20.0, 4.0), lin((0.12, 0.28, 0.10)), lin((0.20, 0.40, 0.15)))
    red = g.mix(grain, lin((0.78, 0.10, 0.10)), lin((0.88, 0.18, 0.15)))
    yellow = g.mix(grain, lin((0.94, 0.78, 0.14)), lin((0.98, 0.86, 0.24)))
    albedo = cc.paint(g, mask, (stone, soil, leaf, red, yellow))
    rough = cc.weighted(g, mask, (0.85, 0.95, 0.8, 0.6, 0.6))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.noise(50.0, 3.0), 0.06, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    cc.clear(variant_id, ("edge", "soil", "plants"))
    variant = VARIANTS[variant_id]
    edge, soil, plants = Parts(), Parts(), Parts()
    _edge(edge, variant)
    _soil(soil, variant)
    _plants(plants, variant)
    return cc.finish(variant_id, [(edge, "edge"), (soil, "soil"), (plants, "plants")], 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes edge, soil and plants."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
