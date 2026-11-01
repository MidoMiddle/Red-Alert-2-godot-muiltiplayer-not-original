"""Lifeguard tower, element 11 of the beach set (docs/beach_elements/11_lifeguard_tower): a wooden tower on four legs, a cabin on top.

The map building neutral_lifeguard_tower (game/data/neutral/civilian.yaml): 2x2, 500 hp, three soldiers go into its cabin. Footprint 2x2
cells, origin at the centre of the footprint on the ground, the front (the ladder, the sea) to -Y. Made a storey to a cell, as the
buildings of the map (the game squashes it upright, game/data/visuals/models.yaml): the deck at 1.5, the cabin to 2.2, the roof's peak
at 2.55. The parts:
    body   four light wooden legs on concrete footings, leaning in a little, X-braces between them on every side; the deck on them with
           a railing round it; the ladder from the deck's front down to the sand
    cabin  a small white cabin of boards on the deck, an orange-red band round it under its windows, dark glass on all four sides, a
           dark grey door to the deck in front
    roof   a dark hipped roof over the cabin, overhanging
Nodes: "body", "cabin", "roof"; the burning one also fire_1..2. One GLB per state:
    beach_lifeguard_tower                   whole
    beach_lifeguard_tower_occupied          whole, its soldiers in the cabin: rifle barrels out of its windows
    beach_lifeguard_tower_damaged           damaged: the windows broken, a brace snapped, the ladder's lower half gone, soot
    beach_lifeguard_tower_damaged_occupied  the same, with the barrels
    beach_lifeguard_tower_abandoned         burning: charred, the roof's corner fallen in, the cabin leaning, fire_1..2
    beach_lifeguard_tower_ruins             fallen: the legs broken off low, the cabin crushed on its side on the sand, charred, boards
Budget 5000 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.beach_lifeguard as tower
    print(tower.run_all())
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
    "beach_lifeguard_tower": "intact",
    "beach_lifeguard_tower_occupied": "occupied",
    "beach_lifeguard_tower_damaged": "damaged",
    "beach_lifeguard_tower_damaged_occupied": "damaged_occupied",
    "beach_lifeguard_tower_abandoned": "abandoned",
    "beach_lifeguard_tower_ruins": "ruins",
}

WOOD, CONCRETE, WHITE, BAND, GLASS, ROOF, DARK = (cc.part(i) for i in range(7))
PARTS = 7

FOOT, TOP = 0.5, 0.42  # the legs' half spread at the ground and at the deck
DECK = 1.5
DX, DY = 0.62, 0.62  # the deck's half size
CX, CY = 0.4, 0.34  # the cabin's half size
CABIN_TOP = 2.2
FIRE_POINTS = ((0.0, 0.0, 2.3), (0.2, -0.3, 1.0))


def _legs(p, variant):
    corners = [(sx, sy) for sx in (-1, 1) for sy in (-1, 1)]
    if variant == "ruins":
        for k, (sx, sy) in enumerate(corners):
            h = 0.25 + 0.12 * (k % 3)
            p.bar((sx * FOOT, sy * FOOT, 0.0), (sx * (FOOT - (FOOT - TOP) * h / DECK), sy * (FOOT - (FOOT - TOP) * h / DECK), h), 0.05, WOOD)
            p.box((sx * FOOT - 0.09, sy * FOOT - 0.09, 0.0), (sx * FOOT + 0.09, sy * FOOT + 0.09, 0.08), CONCRETE)
        return
    for sx, sy in corners:
        p.bar((sx * FOOT, sy * FOOT, 0.0), (sx * TOP, sy * TOP, DECK), 0.05, WOOD)
        p.box((sx * FOOT - 0.09, sy * FOOT - 0.09, 0.0), (sx * FOOT + 0.09, sy * FOOT + 0.09, 0.08), CONCRETE)
    # X-braces on every side, between the legs' feet and their tops.
    sides = (((-1, -1), (1, -1)), ((-1, 1), (1, 1)), ((-1, -1), (-1, 1)), ((1, -1), (1, 1)))
    for k, ((ax, ay), (bx, by)) in enumerate(sides):
        lo_a, lo_b = Vector((ax * FOOT, ay * FOOT, 0.15)), Vector((bx * FOOT, by * FOOT, 0.15))
        hi_a, hi_b = Vector((ax * TOP, ay * TOP, DECK - 0.1)), Vector((bx * TOP, by * TOP, DECK - 0.1))
        p.bar(lo_a, hi_b, 0.022, WOOD)
        if variant in ("damaged", "damaged_occupied") and k == 3:
            p.bar(lo_b, lo_b.lerp(hi_a, 0.45), 0.022, WOOD)  # snapped halfway
            continue
        p.bar(lo_b, hi_a, 0.022, WOOD)


def _deck(p, variant):
    p.box((-DX, -DY, DECK), (DX, DY, DECK + 0.06), WOOD)
    # The railing: posts at the corners and along, a rail on them; open where the ladder comes up.
    rail = DECK + 0.38
    for (x0, y0), (x1, y1) in (((-DX, DY), (DX, DY)), ((-DX, -DY), (-DX, DY)), ((DX, -DY), (DX, DY)), ((-DX, -DY), (-0.16, -DY)),
                               ((0.16, -DY), (DX, -DY))):
        p.bar((x0, y0, rail), (x1, y1, rail), 0.016, WHITE)
        for t in (0.0, 1.0):
            x, y = x0 + (x1 - x0) * t, y0 + (y1 - y0) * t
            p.bar((x, y, DECK + 0.06), (x, y, rail), 0.016, WHITE)
    # The ladder down to the sand in front.
    low = 0.0 if variant not in ("damaged", "damaged_occupied") else 0.7
    t = low / DECK
    top_y, foot_y = -DY, -DY - 0.5
    start = Vector((0.0, top_y + (foot_y - top_y) * (1 - t), low))
    for sx in (-0.13, 0.13):
        p.bar((sx, top_y, DECK + 0.03), (sx, start.y, low), 0.02, WOOD)
    for k in range(1, 9):
        z = DECK * k / 9
        if z < low:
            continue
        y = foot_y + (top_y - foot_y) * (z / DECK)
        p.bar((-0.13, y, z), (0.13, y, z), 0.012, WOOD)


def _cabin(p, variant):
    damaged = variant in ("damaged", "damaged_occupied", "abandoned")
    walls = cc.Facade(CX, CY)
    p.box((-CX, -CY, DECK + 0.06), (CX, CY, CABIN_TOP), WHITE)
    p.box((-CX - 0.008, -CY - 0.008, DECK + 0.32), (CX + 0.008, CY + 0.008, DECK + 0.42), BAND)
    # Windows on all four sides, broken when damaged; the door in front.
    for wall, half_u in (("front", 0.3), ("back", 0.3), ("left", 0.24), ("right", 0.24)):
        us = (-0.2, 0.2) if wall in ("back",) else ((0.2,) if wall == "front" else (0.0,))
        for u in us:
            width = 0.13 if wall != "front" else 0.12
            walls.box(p, wall, u - width, u + width, DECK + 0.45, CABIN_TOP - 0.12, -0.01, 0.006, DARK if damaged else GLASS)
    walls.box(p, "front", -0.3, -0.08, DECK + 0.08, CABIN_TOP - 0.15, -0.01, 0.01, DARK if damaged else ROOF)  # the door, dark grey


def _occupied(p):
    """Three soldiers in the cabin: barrels out of the side and back windows."""
    z = DECK + 0.62
    ic.barrels(p, [(-CX - 0.01, 0.0, z)], (-1.0, 0.0, 0.0), DARK, length=0.32)
    ic.barrels(p, [(CX + 0.01, 0.0, z)], (1.0, 0.0, 0.0), DARK, length=0.32)
    ic.barrels(p, [(0.2, -CY - 0.01, z)], (0.0, -1.0, 0.0), DARK, length=0.32)


def _roof(p):
    over = 0.1
    eave = CABIN_TOP
    rings = [[Vector((x, y, eave)) for x, y in ((-CX - over, -CY - over), (CX + over, -CY - over), (CX + over, CY + over), (-CX - over, CY + over))],
             [Vector((x, y, eave + 0.35)) for x, y in ((-0.06, -0.02), (0.06, -0.02), (0.06, 0.02), (-0.06, 0.02))]]
    p.loft(rings, ROOF)


def _lean(p, amount):
    """Leans the cabin and the roof: their tops pushed aside, the roof's corner fallen in."""
    for v in p.bm.verts:
        h = max(0.0, v.co.z - DECK) / (CABIN_TOP + 0.35 - DECK)
        v.co.x += amount * h
        if v.co.x < -0.2 and v.co.y > 0.1 and v.co.z > CABIN_TOP - 0.05:
            v.co.z -= 0.25


def _fallen(p):
    """The ruins' cabin: on its side on the sand, before the stumps; boards about."""
    before = set(p.bm.verts)
    _cabin(p, "abandoned")
    _roof(p)
    m = Matrix.Translation((0.3, 0.45, 0.4)) @ Matrix.Rotation(1.45, 4, 'Y') @ Matrix.Rotation(0.3, 4, 'Z') @ Matrix.Translation((0.0, 0.0, -DECK - 0.4))
    for v in p.bm.verts:
        if v not in before:
            v.co = m @ v.co
            v.co.z = max(0.0, v.co.z) * 0.6  # crushed as it fell
    for k, (c, size, turn) in enumerate((((-0.4, -0.3, 0.04), (0.9, 0.12, 0.05), (0.1, 0.0, 0.4)), ((0.0, -0.6, 0.03), (0.7, 0.1, 0.04), (0.0, 0.1, -0.7)),
                                         ((-0.6, 0.3, 0.05), (0.6, 0.6, 0.05), (0.15, -0.1, 0.2)))):
        cc.block(p, c, size, turn, WOOD)


def _material(name, variant):
    """Light wood darkening low down, concrete footings, white boards, an orange-red band, dark glass, a dark roof; soot per state."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 4.0)
    wood = g.mix(grain, lin((0.66, 0.56, 0.42)), lin((0.74, 0.64, 0.50)))
    wood = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, -2.0, 0.8)), wood, lin((0.40, 0.32, 0.24)))  # darker low, wet from the sea
    concrete = g.mix(grain, lin((0.62, 0.61, 0.58)), lin((0.70, 0.69, 0.66)))
    joints = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 18.0)), -14.0, 1.6))
    white = g.mix(joints, g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.95, 0.95, 0.93))), lin((0.62, 0.62, 0.60)))
    band = g.mix(grain, lin((0.90, 0.30, 0.12)), lin((0.96, 0.38, 0.16)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, 2.0, -3.6)), lin((0.14, 0.20, 0.26)), lin((0.42, 0.56, 0.66)))
    roof = g.mix(grain, lin((0.20, 0.21, 0.23)), lin((0.27, 0.28, 0.30)))
    dark = lin((0.05, 0.05, 0.05))
    albedo = cc.paint(g, mask, (wood, concrete, white, band, glass, roof, dark))
    amount = {"intact": None, "occupied": None, "damaged": -3.0, "damaged_occupied": -3.0, "abandoned": -1.6, "ruins": -0.6}[variant]
    if amount is not None:
        scorch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, amount))
        albedo = g.mix(g.math('MULTIPLY', scorch, 0.85), albedo, lin((0.08, 0.07, 0.06)))
        if variant == "ruins":
            albedo = g.mix(0.5, albedo, lin((0.14, 0.12, 0.10)))  # fallen and burnt
    sand = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -12.0, 1.3)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(10.0, 2.0), 3.0, -1.0)))
    albedo = g.mix(g.math('MULTIPLY', sand, 0.4 if variant == "ruins" else 1.0), albedo, lin((0.86, 0.78, 0.62)))
    rough = cc.weighted(g, mask, (0.85, 0.9, 0.7, 0.6, 0.12, 0.8, 0.9))
    normal = g.bump(g.math('ADD', g.math('MULTIPLY', joints, -0.5), g.math('MULTIPLY', grain, 0.4)), 0.05, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "cabin", "roof"))
    body, cabin, roof = Parts(), Parts(), Parts()
    _legs(body, variant)
    parts = [(body, "body")]
    if variant == "ruins":
        _fallen(body)
    else:
        _deck(body, variant)
        _cabin(cabin, variant)
        if variant in ("occupied", "damaged_occupied"):
            _occupied(cabin)
        _roof(roof)
        if variant == "abandoned":
            _lean(cabin, 0.08)
            _lean(roof, 0.08)
        parts += [(cabin, "cabin"), (roof, "roof")]
    return cc.finish(variant_id, parts, 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
