"""Boat shed, element 18 of the beach set (docs/beach_elements/18_boat_shed): a wooden shed by the water, big doors to the sea.

The map building neutral_boat_shed (game/data/neutral/civilian.yaml): 2x2, 600 hp, no soldiers go in. Footprint 2x2 cells, origin at the
centre of the footprint on the ground, the front (its doors, the water) to -Y. Made a storey to a cell, as the buildings of the map (the
game squashes it upright, game/data/visuals/models.yaml), as the beach cabin: the eaves at 1.2, the ridge at 2.0. The parts:
    body  walls of vertical boards in faded blue paint, chips of bare wood, white corner boards; a small window high in each side; a net
          hung to dry on the left wall, two oars leaning by it; a short plank ramp before the doors
    roof  a gable of grey boards, its ridge running from the doors to the back, overhanging
    door  the two leaves of the big doors in the gable end, light boards with a Z-brace, dark hinges
Nodes: "body", "roof", "door"; the burning one also fire_1..2. One GLB per state:
    beach_boat_shed            whole
    beach_boat_shed_damaged    damaged: one leaf of the doors torn off and lying on the ramp, boards knocked out of the right wall, the
                               paint peeled, soot
    beach_boat_shed_abandoned  burning: charred, the roof fallen in at the back, a door leaf hanging, fire_1..2
    beach_boat_shed_ruins      burnt down: the charred stumps of the corners and the front frame, the roof's boards on the ground
Budget 6000 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.beach_boat_shed as shed
    print(shed.run_all())
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "beach"
SIZE = 1024
VARIANTS = {
    "beach_boat_shed": "intact",
    "beach_boat_shed_damaged": "damaged",
    "beach_boat_shed_abandoned": "abandoned",
    "beach_boat_shed_ruins": "ruins",
}

BOARDS, TRIM, ROOF, DOOR, NET, WOOD, DARK, GLASS = (cc.part(i) for i in range(8))
PARTS = 8

HX, HY = 0.78, 0.82
EAVE, RIDGE = 1.2, 2.0
DOOR_HALF, DOOR_TOP = 0.5, 1.15
FIRE_POINTS = ((0.0, 0.3, 1.5), (-0.4, -0.3, 0.6))


def _walls(p, variant):
    """The four walls in one loft whose top follows the gable at the front and back; the corner boards; the windows; the net, the oars
    and the ramp."""
    def top(x):
        return RIDGE - (RIDGE - EAVE) * abs(x) / HX

    xs = (-HX, -HX * 0.5, 0.0, HX * 0.5, HX)
    ring_lo = [Vector((x, y, 0.0)) for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY))]
    p.loft([ring_lo, [Vector((v.x, v.y, EAVE)) for v in ring_lo]], BOARDS)
    for y in (-HY, HY):  # the gables over the eaves, front and back
        sign = -1 if y < 0 else 1
        pts = [Vector((x, y, top(x))) for x in xs]
        rings = [[Vector((-HX, y, EAVE)), Vector((HX, y, EAVE)), pts[3], pts[2], pts[1]],
                 [Vector((-HX, y + sign * 0.02, EAVE)), Vector((HX, y + sign * 0.02, EAVE)), pts[3] + Vector((0, sign * 0.02, 0)),
                  pts[2] + Vector((0, sign * 0.02, 0)), pts[1] + Vector((0, sign * 0.02, 0))]]
        p.loft(rings, BOARDS)
    for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY)):
        p.box((x - 0.03, y - 0.03, 0.0), (x + 0.03, y + 0.03, EAVE), TRIM)
    damaged = variant != "intact"
    for x in (-HX, HX):  # a window high in each side
        out = -1 if x < 0 else 1
        p.box((x + out * 0.0 - (0.012 if out > 0 else 0.0), -0.15, 0.8), (x + (0.012 if out > 0 else 0.0), 0.15, 1.02), DARK if damaged else GLASS)
    # The ramp of planks before the doors.
    p.box((-DOOR_HALF, -HY - 0.32, 0.0), (DOOR_HALF, -HY, 0.04), WOOD)
    if variant in ("intact", "damaged"):
        # A net hung to dry on the left wall, two oars leaning by it.
        p.box((-HX - 0.016, -0.5, 0.35), (-HX - 0.006, 0.35, 0.95), NET)
        for k, y in enumerate((0.45, 0.58)):
            p.bar((-HX - 0.12, y, 0.0), (-HX - 0.02, y + 0.04 * k, 1.0), 0.014, WOOD)
    if variant == "damaged":
        p.box((HX, 0.1, 0.25), (HX + 0.006, 0.45, 0.7), DARK)  # boards knocked out of the right wall
        for k in range(3):
            cc.block(p, (HX + 0.18 + 0.06 * k, 0.25 + 0.1 * k, 0.02), (0.45, 0.09, 0.02), (0.0, 0.1, 0.4 * k + 0.6), BOARDS)


def _roof(p, variant):
    over = 0.12
    rings = []
    for y in (-HY - over, HY + over):
        rings.append([Vector((-HX - over, y, EAVE - over * 0.5)), Vector((0.0, y, RIDGE + 0.02)), Vector((HX + over, y, EAVE - over * 0.5)),
                      Vector((HX + over, y, EAVE - over * 0.5 + 0.05)), Vector((0.0, y, RIDGE + 0.08)), Vector((-HX - over, y, EAVE - over * 0.5 + 0.05))])
    if variant == "abandoned":
        # The back half fallen in: its far end sags down into the shed.
        for v in rings[1]:
            v.z -= 0.6 if abs(v.x) < 0.3 else 0.3
    p.loft(rings, ROOF)


def _doors(p, variant):
    """The two leaves in the gable end, each with its Z-brace and hinges; torn off or hanging per state."""
    def leaf(x0, x1, transform=None):
        before = set(p.bm.verts)
        p.box((x0, -HY - 0.03, 0.04), (x1, -HY - 0.005, DOOR_TOP), DOOR)
        p.bar((x0 + 0.04, -HY - 0.035, 0.12), (x1 - 0.04, -HY - 0.035, DOOR_TOP - 0.1), 0.02, DOOR)  # the Z-brace
        for z in (0.25, DOOR_TOP - 0.2):
            hx = x0 if x0 < 0 else x1
            p.box((hx - 0.02 if x0 < 0 else hx - 0.14, -HY - 0.04, z - 0.025), (hx + 0.14 if x0 < 0 else hx + 0.02, -HY - 0.03, z + 0.025), DARK)
        if transform is not None:
            for v in p.bm.verts:
                if v not in before:
                    v.co = transform @ v.co

    if variant == "intact":
        leaf(-DOOR_HALF, -0.01)
        leaf(0.01, DOOR_HALF)
        return
    if variant == "damaged":
        leaf(-DOOR_HALF, -0.01)
        lying = Matrix.Translation((0.25, -HY - 0.3, 0.06)) @ Matrix.Rotation(-1.5, 4, 'X') @ Matrix.Rotation(0.3, 4, 'Z') @ Matrix.Translation((-0.25, HY, -0.04))
        leaf(0.01, DOOR_HALF, lying)  # torn off, lying on the ramp
        return
    if variant == "abandoned":
        leaf(-DOOR_HALF, -0.01)
        hinge = Vector((DOOR_HALF, -HY, 0.0))
        hang = Matrix.Translation(hinge) @ Matrix.Rotation(-1.2, 4, 'Z') @ Matrix.Translation(-hinge)
        leaf(0.01, DOOR_HALF, hang)  # hanging open from its hinges
        return
    cc.block(p, (0.3, -HY - 0.4, 0.03), (0.5, 0.06, 0.6), (1.5, 0.0, 0.5), DOOR)  # ruins: a leaf on the sand


def _doorway(p):
    p.box((-DOOR_HALF, -HY - 0.004, 0.04), (DOOR_HALF, -HY, DOOR_TOP), DARK)


def _ruins(p):
    for x, y, h in ((-HX, -HY, 0.55), (HX, -HY, 0.35), (HX, HY, 0.6), (-HX, HY, 0.4), (-DOOR_HALF, -HY, 0.7), (DOOR_HALF, -HY, 0.45)):
        p.box((x - 0.04, y - 0.04, 0.0), (x + 0.04, y + 0.04, h), BOARDS)
    p.box((-HX, HY - 0.04, 0.0), (0.2, HY, 0.28), BOARDS)  # a low bit of the back wall
    for k, (c, size, turn) in enumerate((((-0.2, 0.15, 0.12), (1.3, 0.8, 0.04), (0.25, -0.12, 0.15)), ((0.35, -0.2, 0.1), (1.0, 0.7, 0.04), (-0.2, 0.15, -0.3)),
                                         ((-0.5, -0.5, 0.05), (0.7, 0.1, 0.04), (0.0, 0.1, 0.9)), ((0.55, 0.55, 0.05), (0.6, 0.1, 0.04), (0.1, 0.0, -0.5)))):
        cc.block(p, c, size, turn, ROOF if k < 2 else BOARDS)
    p.box((-DOOR_HALF, -HY - 0.32, 0.0), (DOOR_HALF, -HY, 0.04), WOOD)  # the ramp


def _material(name, variant):
    """Faded blue boards with dark joints and chips of bare wood, white corner boards, grey roof boards, light doors, a brown net, wood,
    dark hinges and openings, glass; peeled, sooted and charred per state."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.15)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 4.0)
    wood = g.mix(grain, lin((0.50, 0.44, 0.36)), lin((0.60, 0.53, 0.43)))
    joints = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 10.0)), -14.0, 1.6))
    paint = g.mix(g.noise(5.0, 3.0), lin((0.32, 0.50, 0.62)), lin((0.44, 0.60, 0.70)))
    chips = g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 4.0), 6.0, -3.6 if variant == "intact" else -2.6))
    boards = g.mix(joints, g.mix(chips, paint, wood), lin((0.12, 0.12, 0.12)))
    trim = g.mix(grain, lin((0.86, 0.86, 0.84)), lin((0.92, 0.92, 0.90)))
    roof_j = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 8.0)), -14.0, 1.6))
    roof = g.mix(roof_j, g.mix(grain, lin((0.42, 0.43, 0.44)), lin((0.52, 0.52, 0.52))), lin((0.22, 0.22, 0.23)))
    door = g.mix(joints, g.mix(grain, lin((0.80, 0.76, 0.66)), lin((0.88, 0.84, 0.74))), lin((0.30, 0.28, 0.24)))
    mesh = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', g.math('ADD', y, z), 120.0)), 3.0, -1.0))
    net = g.mix(mesh, lin((0.42, 0.32, 0.20)), lin((0.24, 0.18, 0.12)))
    dark = lin((0.05, 0.05, 0.05))
    glass = g.mix(grain, lin((0.30, 0.38, 0.42)), lin((0.46, 0.56, 0.60)))
    albedo = cc.paint(g, mask, (boards, trim, roof, door, net, wood, dark, glass))
    if variant != "intact":
        amount = {"damaged": -3.0, "abandoned": -1.6, "ruins": 0.2}[variant]
        scorch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, amount))
        albedo = g.mix(g.math('MULTIPLY', scorch, 0.85), albedo, lin((0.07, 0.06, 0.06)))
        if variant == "ruins":
            albedo = g.mix(0.55, albedo, lin((0.14, 0.12, 0.10)))
    sand = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -15.0, 1.3)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(10.0, 2.0), 3.0, -1.0)))
    albedo = g.mix(g.math('MULTIPLY', sand, 0.3 if variant == "ruins" else 1.0), albedo, lin((0.86, 0.78, 0.62)))
    rough = cc.weighted(g, mask, (0.8, 0.7, 0.85, 0.75, 0.9, 0.85, 0.9, 0.15))
    normal = g.bump(g.math('ADD', g.math('MULTIPLY', g.math('ADD', joints, roof_j), -0.5), g.math('MULTIPLY', grain, 0.3)), 0.06, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "roof", "door"))
    body, roof, door = Parts(), Parts(), Parts()
    if variant == "ruins":
        _ruins(body)
    else:
        _walls(body, variant)
        _roof(roof, variant)
        if variant != "intact":
            _doorway(body)
    _doors(door, variant)
    parts = [(body, "body"), (door, "door")] + ([] if variant == "ruins" else [(roof, "roof")])
    return cc.finish(variant_id, parts, 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
