"""The buildings of the desert (docs/desert_elements, 07_adobe_house and 08_ancient_ruins): an adobe house and the ruins of a temple.

Map buildings of game/data/neutral/civilian.yaml, 2x2: neutral_adobe_house (600 hp, three soldiers go in, the civilians hide in it) and
neutral_ancient_ruins (2500 hp). Made a storey to a cell, as the buildings of the map (the game squashes them upright,
game/data/visuals/models.yaml): the house's roof at 1.15, its upper room's at 1.95; the ruins' columns to 2.0. Origin at the centre of
the footprint on the ground, the front to -Y.

The adobe house: a cube of rendered mud brick, its corners and edges softened, a low parapet round its flat roof with water spouts, a
smaller upper room at the back left; small windows with dark wooden grilles, a wooden door, beam ends (vigas) out of the walls under the
roof, an outside stair up to the roof on the right, clay pots by the door. Nodes: "body", "team_mask" (the parapet's band, the owner's
colour when it is held), "base" (a pad of beaten earth); fire_1..2 when abandoned. States:
    desert_adobe_house                   whole
    desert_adobe_house_occupied          its soldiers in: rifle barrels out of the windows
    desert_adobe_house_damaged           cracks, a corner of the parapet broken, the door gone, soot
    desert_adobe_house_damaged_occupied  the same, with the barrels
    desert_adobe_house_abandoned         a wall's top collapsed, the upper room's roof fallen in, burnt, fire_1..2
    desert_adobe_house_ruins             low stumps of the walls, mounds of mud brick, beams
The ruins: a stepped stone platform, three columns of drums with capitals along the back, an architrave on two of them, a broken wall
on the left with a doorway, fallen drums and blocks on the sand. Node "body". States:
    desert_ruins_stone                   standing
    desert_ruins_stone_damaged           the architrave down, a column broken to half
    desert_ruins_stone_ruins             only the platform, column stumps and blocks
Budget 8000 (house), 6000 (ruins); texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.desert_buildings as buildings
    print(buildings.run_all())
"""

import math
import random

from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "desert"
SIZE = 1024
VARIANTS = {
    "desert_adobe_house": "intact", "desert_adobe_house_occupied": "occupied", "desert_adobe_house_damaged": "damaged",
    "desert_adobe_house_damaged_occupied": "damaged_occupied", "desert_adobe_house_abandoned": "abandoned",
    "desert_adobe_house_ruins": "ruins",
    "desert_ruins_stone": "intact", "desert_ruins_stone_damaged": "damaged", "desert_ruins_stone_ruins": "ruins",
}

ADOBE, WOOD, DARK, EARTH, POT, STONE, TEAM = (cc.part(i) for i in range(7))
PARTS = 7

HX, HY, ROOF = 0.72, 0.7, 1.15
UPPER = (-0.72, -0.1, 0.0, 0.7, 1.95)  # the upper room: x0, x1, y0, y1, top
FIRE_POINTS = ((-0.35, 0.35, 1.7), (0.3, -0.3, 1.2))


def _adobe(p, team, variant, rnd):
    damaged = variant in ("damaged", "damaged_occupied", "abandoned")
    if variant == "ruins":
        for (x0, y0, x1, y1, h) in ((-HX, -HY, HX, -HY + 0.12, 0.35), (-HX, -HY, -HX + 0.12, HY, 0.5), (HX - 0.12, 0.0, HX, HY, 0.25),
                                    (-HX, HY - 0.12, 0.2, HY, 0.4)):
            p.box((x0, y0, 0.04), (x1, y1, h), ADOBE)
        for k in range(6):
            cc.block(p, (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), 0.08), (0.28, 0.22, 0.14), (0.2, 0.1, rnd.uniform(0, 3)), ADOBE)
        for k in range(3):
            p.bar((rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), 0.06), (rnd.uniform(-0.6, 0.6), rnd.uniform(-0.6, 0.6), 0.12), 0.025, WOOD)
        return
    walls = cc.Facade(HX, HY)
    top = ROOF if variant != "abandoned" else ROOF
    p.box((-HX, -HY, 0.04), (HX, HY, top), ADOBE)
    # The parapet round the roof, broken at a corner when damaged; its band the owner's colour.
    for (x0, y0, x1, y1) in ((-HX, -HY, HX, -HY + 0.08), (-HX, HY - 0.08, HX, HY), (-HX, -HY, -HX + 0.08, HY), (HX - 0.08, -HY, HX, HY)):
        if damaged and x0 == HX - 0.08:
            y1 = 0.2
        p.box((x0, y0, top), (x1, y1, top + 0.16), ADOBE)
    team.box((-HX - 0.006, -HY - 0.006, top + 0.02), (HX + 0.006, -HY + 0.004, top + 0.07), TEAM)
    team.box((-HX - 0.006, -HY - 0.006, top + 0.02), (-HX + 0.004, HY + 0.006, top + 0.07), TEAM)
    # The upper room at the back left.
    x0, x1, y0, y1, utop = UPPER
    if variant == "abandoned":
        p.box((x0, y0, top), (x1, y1, utop - 0.4), ADOBE)  # its roof fallen in
        p.box((HX - 0.5, -HY, top - 0.35), (HX, -HY + 0.08, top), DARK)  # the wall's top collapsed
    else:
        p.box((x0, y0, top), (x1, y1, utop), ADOBE)
        for (a0, b0, a1, b1) in ((x0, y0, x1, y0 + 0.06), (x0, y1 - 0.06, x1, y1), (x0, y0, x0 + 0.06, y1), (x1 - 0.06, y0, x1, y1)):
            p.box((a0, b0, utop), (a1, b1, utop + 0.1), ADOBE)
    # Windows with grilles, the door, the beam ends, the spouts.
    win = DARK if damaged else WOOD
    for u in (-0.4, 0.35):
        walls.box(p, "front", u - 0.1, u + 0.1, 0.55, 0.78, -0.01, 0.006, DARK)
        if not damaged:
            for k in range(3):
                walls.box(p, "front", u - 0.07 + 0.07 * k - 0.008, u - 0.07 + 0.07 * k + 0.008, 0.55, 0.78, 0.0, 0.015, win)
    walls.box(p, "right", -0.35, -0.15, 0.55, 0.78, -0.01, 0.006, DARK)
    walls.box(p, "front", -0.05, 0.17, 0.04, 0.52, -0.01, 0.01, DARK if variant != "intact" and variant != "occupied" else WOOD)
    for u in (-0.6, -0.3, 0.0, 0.3, 0.6):
        walls.box(p, "front", u - 0.03, u + 0.03, top - 0.12, top - 0.06, 0.0, 0.08, WOOD)
    for u in (-0.4, 0.4):
        walls.box(p, "back", u - 0.03, u + 0.03, top + 0.05, top + 0.09, 0.0, 0.14, WOOD)  # the spouts
    # The outside stair up the right side to the roof.
    for k in range(6):
        z = 0.04 + 0.19 * (k + 1)
        p.box((HX, 0.15 + 0.08 * k, 0.04), (HX + 0.22, 0.25 + 0.08 * k, z), ADOBE)
    for k, (x, y) in enumerate(((0.3, -HY - 0.12), (0.42, -HY - 0.1))):  # the pots by the door
        p.turned((x, y, 0.04), 'Z', [(0.0, 0.03), (0.05, 0.055), (0.1, 0.03), (0.12, 0.025)], POT, sides=8)
    if variant in ("occupied", "damaged_occupied"):
        ic.barrels(p, [(u, -HY - 0.02, 0.66) for u in (-0.4, 0.35)], (0.0, -1.0, 0.0), DARK, length=0.3)
        ic.barrels(p, [(HX + 0.02, -0.25, 0.66)], (1.0, 0.0, 0.0), DARK, length=0.3)


def _temple(p, rnd, variant):
    # The stepped platform.
    for k, (r, z0, z1) in enumerate(((0.92, 0.0, 0.08), (0.84, 0.08, 0.16))):
        p.box((-r, -r, z0), (r, r, z1), STONE)
    floor = 0.16
    columns = ((-0.55, 0.45), (0.0, 0.45), (0.55, 0.45))
    for k, (x, y) in enumerate(columns):
        if variant == "ruins":
            h = 0.3 + 0.15 * k
        elif variant == "damaged" and k == 2:
            h = 0.95
        else:
            h = 1.8
        p.box((x - 0.15, y - 0.15, floor), (x + 0.15, y + 0.15, floor + 0.08), STONE)  # its base
        drums = max(1, int(h / 0.32))
        for d in range(drums):
            z0 = floor + 0.08 + (h - 0.08) * d / drums
            z1 = floor + 0.08 + (h - 0.08) * (d + 1) / drums - 0.008
            p.cylinder((x + rnd.uniform(-0.008, 0.008), y, (z0 + z1) / 2), 'Z', 0.11, (z1 - z0) / 2, STONE, sides=8)
        if h >= 1.8:
            p.box((x - 0.15, y - 0.15, floor + h), (x + 0.15, y + 0.15, floor + h + 0.08), STONE)  # the capital
    if variant == "intact":
        p.box((-0.72, 0.3, floor + 1.88), (0.18, 0.6, floor + 2.0), STONE)  # the architrave on two of them
    else:
        cc.block(p, (0.25, -0.35, floor + 0.08), (0.9, 0.3, 0.12), (0.0, 0.08, 0.35), STONE)  # fallen
    # The broken wall on the left with its doorway.
    if variant != "ruins":
        p.box((-0.84, -0.6, floor), (-0.7, -0.15, floor + 0.9), STONE)
        p.box((-0.84, 0.05, floor), (-0.7, 0.3, floor + 0.55), STONE)
    for k in range(5 if variant == "intact" else 9):  # fallen drums and blocks
        a = rnd.uniform(0, 2 * math.pi)
        c = (math.cos(a) * rnd.uniform(0.55, 0.95), math.sin(a) * rnd.uniform(0.55, 0.95) - 0.1, 0.07)
        if k % 2:
            p.cylinder(c, 'X', 0.1, 0.12, STONE, sides=8)
        else:
            cc.block(p, c, (0.22, 0.16, 0.13), (0.0, 0.0, a), STONE)


def _material(name, variant, temple):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    adobe = g.mix(g.noise(4.0, 4.0), lin((0.78, 0.60, 0.42)), lin((0.86, 0.70, 0.50)))
    stains = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.0, 3.0), 4.0, -2.0)), g.clamp01(g.math('MULTIPLY_ADD', z, 1.0, -0.2)))
    adobe = g.mix(g.math('MULTIPLY', stains, 0.4), adobe, lin((0.62, 0.46, 0.32)))  # streaks of the rains down from the roof
    adobe = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, -5.0, 1.0)), adobe, lin((0.70, 0.56, 0.40)))  # darker low down
    wood = g.mix(grain, lin((0.34, 0.24, 0.16)), lin((0.42, 0.30, 0.20)))
    dark = lin((0.06, 0.05, 0.05))
    earth = g.mix(grain, lin((0.72, 0.58, 0.40)), lin((0.80, 0.66, 0.46)))
    pot = g.mix(grain, lin((0.70, 0.36, 0.22)), lin((0.78, 0.44, 0.28)))
    stone = g.mix(g.noise(6.0, 4.0), lin((0.74, 0.64, 0.46)), lin((0.84, 0.76, 0.58)))
    stone = g.mix(g.math('MULTIPLY', g.noise(18.0, 3.0), 0.4), stone, lin((0.58, 0.50, 0.38)))
    team = lin((0.9, 0.9, 0.9))
    albedo = cc.paint(g, mask, (adobe, wood, dark, earth, pot, stone, team))
    if variant != "intact" and not (temple and variant == "occupied"):
        albedo = g.mix(g.math('MULTIPLY', g.math('ADD', mask[0], mask[5]), cc.cracks(g, 4.0)), albedo, lin((0.26, 0.20, 0.15)))
    if variant in ("damaged", "damaged_occupied", "abandoned", "ruins") and not temple:
        amount = {"damaged": -3.0, "damaged_occupied": -3.0, "abandoned": -1.6, "ruins": -2.0}[variant]
        scorch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, amount))
        albedo = g.mix(g.math('MULTIPLY', scorch, 0.8), albedo, lin((0.10, 0.08, 0.07)))
    sand = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -12.0, 1.3)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(8.0, 2.0), 3.0, -1.0)))
    albedo = g.mix(g.math('MULTIPLY', sand, 0.7), albedo, lin((0.88, 0.74, 0.52)))
    rough = cc.weighted(g, mask, (0.92, 0.85, 0.9, 0.95, 0.8, 0.9, 0.6))
    g.finish(albedo, g.bump(g.math('ADD', grain, g.noise(5.0, 3.0)), 0.15, 0.01), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    temple = variant_id.startswith("desert_ruins_stone")
    ic.clear(variant_id, ("body", "team_mask", "base"))
    rnd = random.Random(variant_id)
    body = Parts()
    if temple:
        _temple(body, rnd, variant)
        parts = [(body, "body")]
    else:
        team, base = Parts(), Parts()
        base.box((-0.95, -0.95, 0.0), (0.95, 0.95, 0.04), EARTH)
        _adobe(body, team, variant, rnd)
        parts = [(body, "body"), (base, "base")] + ([] if variant == "ruins" else [(team, "team_mask")])
    meshes, mat, graph = cc.finish(variant_id, parts, -1.0, _material(variant_id + "_mat", variant, temple))
    return meshes, mat, graph


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
