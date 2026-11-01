"""Shed (docs/map_elements/11_civilian_buildings): a small wooden utility shed by the houses of a village or a town.

The map building neutral_shed (game/data/neutral/civilian.yaml): 1x1, 300 hp, no soldiers go in. Footprint 1x1 cell, origin at the centre
of the cell on the ground, its door to -Y. Made a storey to a cell, as the buildings of the map (the game squashes it upright,
game/data/visuals/models.yaml), as the beach cabin: the eaves at 0.9, the ridge at 1.35. The parts:
    body  walls of weathered red-brown boards on a low stone footing, white corner boards, a gable roof of dark tarred felt along x,
          overhanging; a plank door with a Z-brace, a small window in the right gable end; firewood stacked against the left wall under
          the eaves, a rain barrel at the back corner
    door  the door's leaf; torn off and lying before it when damaged, hanging open when burning
Nodes: "body", "door"; the burning one also fire_1..2. One GLB per state:
    shed            whole
    shed_damaged    damaged: the door torn off, a hole in the right wall, the window broken, the felt torn on the roof, soot
    shed_abandoned  burning: charred, the roof fallen in at one end, the door hanging, fire_1..2
    shed_ruins      burnt down: charred stumps of the corners, a low bit of the back wall, the roof's boards on the ground, the firewood
                    scattered
Budget 2000 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.map_shed as shed
    print(shed.run_all())
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "map"
SIZE = 1024
VARIANTS = {"shed": "intact", "shed_damaged": "damaged", "shed_abandoned": "abandoned", "shed_ruins": "ruins"}

BOARDS, TRIM, FELT, DOOR, GLASS, STONE, LOGS, BARREL, DARK = (cc.part(i) for i in range(9))
PARTS = 9

HX, HY = 0.32, 0.28
FOOT = 0.06
EAVE, RIDGE = 0.9, 1.35
FIRE_POINTS = ((0.0, 0.0, 1.1), (-0.2, -0.15, 0.5))


def _walls(p, variant):
    p.box((-HX - 0.03, -HY - 0.03, 0.0), (HX + 0.03, HY + 0.03, FOOT), STONE)
    ring = [(-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY)]
    p.loft([[Vector((x, y, FOOT)) for x, y in ring], [Vector((x, y, EAVE)) for x, y in ring]], BOARDS)
    for x in (-HX, HX):  # the gable ends over the eaves, along x the ridge
        sign = -1 if x < 0 else 1
        tri = [Vector((x, -HY, EAVE)), Vector((x, HY, EAVE)), Vector((x, 0.0, RIDGE))]
        p.loft([tri, [v + Vector((sign * 0.02, 0, 0)) for v in tri]], BOARDS)
    for x, y in ring:
        p.box((x - 0.022, y - 0.022, FOOT), (x + 0.022, y + 0.022, EAVE), TRIM)
    broken = variant != "intact"
    p.box((HX, -0.08, 0.92), (HX + 0.012, 0.08, 1.08), DARK if broken else GLASS)  # the window in the right gable
    if variant == "damaged":
        p.box((HX, 0.02, 0.2), (HX + 0.006, 0.22, 0.55), DARK)  # the hole in the right wall
    # Firewood against the left wall, a rain barrel at the back corner.
    for row in range(3):
        for k in range(4):
            p.cylinder((-HX - 0.07, -0.2 + 0.11 * k + 0.05 * (row % 2), FOOT + 0.05 + 0.09 * row), 'Y', 0.045, 0.05, LOGS, sides=6)
    p.cylinder((HX + 0.05, HY + 0.05, 0.16), 'Z', 0.08, 0.16, BARREL, sides=10)


def _roof(p, variant):
    over = 0.08
    rings = []
    for x in (-HX - over, HX + over):
        rings.append([Vector((x, -HY - over, EAVE - 0.04)), Vector((x, 0.0, RIDGE + 0.02)), Vector((x, HY + over, EAVE - 0.04)),
                      Vector((x, HY + over, EAVE - 0.01)), Vector((x, 0.0, RIDGE + 0.05)), Vector((x, -HY - over, EAVE - 0.01))])
    if variant == "abandoned":
        for v in rings[1]:
            v.z -= 0.35 if abs(v.y) < 0.2 else 0.2  # the right end fallen in
    p.loft(rings, FELT)
    if variant == "damaged":
        cc.block(p, (0.12, -0.18, RIDGE - 0.15), (0.2, 0.16, 0.012), (0.5, 0.2, 0.3), DARK)  # felt torn off, the boards seen


def _door(p, variant):
    def leaf(m=None):
        before = set(p.bm.verts)
        p.box((-0.13, -HY - 0.025, FOOT + 0.01), (0.13, -HY - 0.005, 0.72), DOOR)
        p.bar((-0.1, -HY - 0.03, FOOT + 0.06), (0.1, -HY - 0.03, 0.66), 0.016, DOOR)
        for z in (0.2, 0.6):
            p.box((-0.15, -HY - 0.035, z - 0.02), (-0.04, -HY - 0.025, z + 0.02), DARK)
        if m is not None:
            for v in p.bm.verts:
                if v not in before:
                    v.co = m @ v.co

    if variant == "intact":
        leaf()
    elif variant == "damaged":
        leaf(Matrix.Translation((0.05, -HY - 0.3, 0.04)) @ Matrix.Rotation(-1.52, 4, 'X') @ Matrix.Rotation(0.3, 4, 'Z') @ Matrix.Translation((0.0, HY, -FOOT)))
    elif variant == "abandoned":
        hinge = Vector((-0.13, -HY, 0.0))
        leaf(Matrix.Translation(hinge) @ Matrix.Rotation(1.3, 4, 'Z') @ Matrix.Translation(-hinge))
    else:
        cc.block(p, (0.25, -HY - 0.35, 0.02), (0.26, 0.6, 0.03), (0.0, 0.0, 0.7), DOOR)


def _ruins(p, rnd):
    p.box((-HX - 0.03, -HY - 0.03, 0.0), (HX + 0.03, HY + 0.03, FOOT), STONE)
    for x, y, h in ((-HX, -HY, 0.35), (HX, -HY, 0.22), (HX, HY, 0.42), (-HX, HY, 0.3)):
        p.box((x - 0.03, y - 0.03, FOOT), (x + 0.03, y + 0.03, FOOT + h), BOARDS)
    p.box((-HX, HY - 0.03, FOOT), (0.1, HY, FOOT + 0.2), BOARDS)
    for k, (c, size, turn) in enumerate((((0.0, 0.0, 0.1), (0.7, 0.45, 0.03), (0.2, -0.1, 0.2)), ((0.1, -0.1, 0.07), (0.6, 0.35, 0.03), (-0.15, 0.1, -0.4)))):
        cc.block(p, c, size, turn, BOARDS)  # the roof's boards, the felt burnt off them
    for k in range(6):
        a = rnd.uniform(0, 2 * math.pi)
        p.cylinder((math.cos(a) * 0.45, math.sin(a) * 0.4, 0.04), 'X' if k % 2 else 'Y', 0.04, 0.05, LOGS, sides=6)


def _material(name, variant):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 4.0)
    joints = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 14.0)), -14.0, 1.6))
    paint = g.mix(g.noise(5.0, 3.0), lin((0.48, 0.24, 0.18)), lin((0.58, 0.32, 0.24)))
    paint = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 4.0), 6.0, -3.6)), paint, lin((0.52, 0.46, 0.38)))  # worn to the wood
    boards = g.mix(joints, paint, lin((0.14, 0.10, 0.08)))
    trim = g.mix(grain, lin((0.84, 0.84, 0.80)), lin((0.90, 0.90, 0.86)))
    felt = g.mix(g.noise(10.0, 3.0), lin((0.18, 0.18, 0.19)), lin((0.24, 0.24, 0.25)))
    door = g.mix(joints, g.mix(grain, lin((0.42, 0.34, 0.24)), lin((0.50, 0.41, 0.30))), lin((0.16, 0.12, 0.09)))
    glass = g.mix(grain, lin((0.30, 0.38, 0.42)), lin((0.46, 0.54, 0.58)))
    stone = g.mix(g.noise(8.0, 3.0), lin((0.48, 0.47, 0.44)), lin((0.58, 0.57, 0.53)))
    logs = g.mix(grain, lin((0.58, 0.46, 0.30)), lin((0.66, 0.54, 0.36)))
    barrel = g.mix(grain, lin((0.22, 0.30, 0.40)), lin((0.28, 0.36, 0.46)))
    dark = lin((0.05, 0.05, 0.05))
    albedo = cc.paint(g, mask, (boards, trim, felt, door, glass, stone, logs, barrel, dark))
    if variant != "intact":
        amount = {"damaged": -3.0, "abandoned": -1.6, "ruins": -2.0}[variant]
        scorch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, amount))
        albedo = g.mix(g.math('MULTIPLY', scorch, 0.85), albedo, lin((0.07, 0.06, 0.06)))
    rough = cc.weighted(g, mask, (0.8, 0.65, 0.85, 0.8, 0.15, 0.9, 0.85, 0.5, 0.9))
    g.finish(albedo, g.bump(g.math('ADD', g.math('MULTIPLY', joints, -0.5), g.math('MULTIPLY', grain, 0.3)), 0.06, 0.005), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "door"))
    body, door = Parts(), Parts()
    if variant == "ruins":
        _ruins(body, ic.seed(variant_id))
    else:
        _walls(body, variant)
        _roof(body, variant)
        if variant != "intact":
            body.box((-0.13, -HY - 0.004, FOOT + 0.01), (0.13, -HY, 0.72), DARK)  # the open doorway
    _door(door, variant)
    return cc.finish(variant_id, [(body, "body"), (door, "door")], 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
