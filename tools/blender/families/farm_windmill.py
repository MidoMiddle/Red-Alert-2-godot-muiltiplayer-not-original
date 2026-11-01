"""Windmill, element 11 of the temperate set (docs/temperate_elements/11_windmill): a whitewashed tower with a dark conical cap and a
cross of four sails on its front, a landmark of the farm.

The map building neutral_windmill: 2x2, no soldiers go in, it burns (wood). Footprint 2x2 cells, origin at the centre of the footprint on
the ground, the front to -Y. Made at the size the game draws it, not squashed (as the lighthouse, families/beach_lighthouse.py): the game
may turn the sails, and a node turning under a squashed parent would shear; the tower to 1.6, the cap to 2.3, the sails' hub at 1.74,
as its placeholder (game/scripts/Terrain/BuildingLooks.cs). The parts:
    base     a round flagstone plinth under the tower
    body     the octagonal tower narrowing upward, cream plaster on a course of field stone; the dark brown door at its foot to the
             front with a step, small windows in light frames up its faces, the dark curb ring at its top; a millstone leaning on the
             front left face, sacks of flour by the door
    roof     the conical cap of dark shingles on the curb, a finial on its top, a little gable at the front where the windshaft comes
             out; the tail pole down the back to the ground with its braces
    sails    the windshaft and its hub, four stocks in a cross (as an X), each with a lattice frame of light wood on its trailing side
             and canvas stretched over it, striped by the frame's bars
Nodes: "body", "roof", "sails", "base"; the sails' node stands on the hub, so the game turns it about its own Y in Blender (the
windshaft's axis, out of the front; Z in the GLB). The abandoned one also fire_1..3. One GLB per state:
    temperate_windmill            whole: four sails
    temperate_windmill_damaged    damaged: a sail broken off at its stock and lying on the ground in front, the canvas of another torn
                                  to its bare lattice, the plaster cracked and fallen off in patches
    temperate_windmill_abandoned  burning: the canvas burnt off two sails, a third broken short, the frames charred; the door burnt
                                  open, smoke out of it and a window, soot up the tower and over the cap; fire_1..3 (the door, the
                                  window, the cap)
    temperate_windmill_ruins      destroyed: the tower slumped to a jagged stump, its cap lying on the ground at the back, the sails
                                  broken on the ground in front, rubble and planks
Budget 10000 triangles, texture 2048 (docs/temperate_elements/11_windmill/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.farm_windmill as windmill
    print(windmill.run_all())
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "farm"
SIZE = 2048
VARIANTS = {
    "temperate_windmill": "intact",
    "temperate_windmill_damaged": "damaged",
    "temperate_windmill_abandoned": "abandoned",
    "temperate_windmill_ruins": "ruins",
}

(PLASTER, STONE, ROOF, WOOD, DOOR, SAIL, DARK, PAD, SACK, FRAME) = (cc.part(i) for i in range(10))
PARTS = 10

PLINTH = 0.06  # the top of the base
FOOT_R, TOP_R = 0.64, 0.38  # the tower's corner radius at its foot and at its top
TOP = 1.6
STONE_TOP = 0.26
HUB = Vector((0.0, -0.62, 1.74))
SAIL_LEN = 0.86
SAIL_ANGLES = tuple(math.radians(45 + 90 * k) for k in range(4))  # in the XZ plane, from +X towards +Z


def _radius(z):
    return FOOT_R + (TOP_R - FOOT_R) * (z - PLINTH) / (TOP - PLINTH)


def _ring(r, z, sides=8, phase=math.pi / 8):
    return [Vector((math.cos(phase + 2 * math.pi * i / sides) * r, math.sin(phase + 2 * math.pi * i / sides) * r, z)) for i in range(sides)]


def _face(k):
    """Face k of the octagon: 0 the front (-Y), counting anticlockwise seen from above (1 front right, 2 right…). Returns (out, along)."""
    a = -math.pi / 2 + k * math.pi / 4
    out = Vector((math.cos(a), math.sin(a), 0.0))
    return out, Vector((-out.y, out.x, 0.0))


def _on_face(k, u, z, d=0.0):
    out, along = _face(k)
    return out * (_radius(z) * math.cos(math.pi / 8) + d) + along * u + Vector((0.0, 0.0, z))


def _panel(p, k, u0, u1, z0, z1, d0, d1, painted):
    """A slab on face k, u0..u1 along it, z0..z1 up it, d0..d1 out of it, following its slope."""
    p.loft([[_on_face(k, u, z, d) for u, z in ((u0, z0), (u1, z0), (u1, z1), (u0, z1))] for d in (d0, d1)], painted)


def _moved(p, build, m):
    """Builds into p, the new vertices moved by the matrix m."""
    before = set(p.bm.verts)
    build()
    for v in p.bm.verts:
        if v not in before:
            v.co = m @ v.co


# --- parts ---------------------------------------------------------------------------

def _base(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.74), (PLINTH - 0.015, 0.74), (PLINTH, 0.72)], PAD, sides=24)


def _tower(p, top=TOP):
    p.loft([_ring(FOOT_R + 0.03, PLINTH), _ring(_radius(STONE_TOP) + 0.03, STONE_TOP)], STONE)
    p.loft([_ring(_radius(STONE_TOP), STONE_TOP - 0.01), _ring(_radius(top), top)], PLASTER)


def _curb(p):
    p.loft([_ring(TOP_R + 0.04, TOP - 0.06, sides=16, phase=0.0), _ring(TOP_R + 0.04, TOP + 0.02, sides=16, phase=0.0)], DARK)


def _openings(p, variant):
    """The door at the foot of the front, its step, the windows up the faces."""
    burnt = variant == "abandoned"
    # Out past the stone course at the foot.
    _panel(p, 0, -0.15, 0.15, PLINTH + 0.02, 0.6, -0.01, 0.055, FRAME)
    _panel(p, 0, -0.12, 0.12, PLINTH + 0.02, 0.56, 0.0, 0.065, DARK if burnt else DOOR)
    if not burnt:
        for u in (-0.06, 0.0, 0.06):  # the door's planks
            _panel(p, 0, u - 0.004, u + 0.004, PLINTH + 0.04, 0.54, 0.065, 0.07, DARK)
    out, _ = _face(0)
    step = out * (_radius(PLINTH) * math.cos(math.pi / 8) + 0.12)
    cc.block(p, (step.x, step.y, PLINTH + 0.02), (0.36, 0.14, 0.05), (0.0, 0.0, 0.0), STONE)
    for k, z in ((0, 1.0), (1, 0.62), (1, 1.2), (2, 0.85), (7, 0.78), (3, 1.15)):
        _panel(p, k, -0.075, 0.075, z - 0.1, z + 0.1, -0.01, 0.018, FRAME)
        _panel(p, k, -0.055, 0.055, z - 0.08, z + 0.08, 0.0, 0.022, DARK)
        _panel(p, k, -0.09, 0.09, z - 0.13, z - 0.1, -0.01, 0.035, FRAME)  # the sill


def _yard(p, rnd, variant):
    """A millstone leaning on the front left face, sacks of flour by the door."""
    out, along = _face(7)
    c = _on_face(7, 0.05, PLINTH + 0.17, 0.07)
    m = Matrix.Translation(c) @ Matrix.Rotation(math.atan2(out.y, out.x), 4, 'Z') @ Matrix.Rotation(-0.25, 4, 'Y')
    _moved(p, lambda: p.turned((0.0, 0.0, 0.0), 'X', [(-0.03, 0.17), (0.03, 0.17), (0.03, 0.04), (-0.03, 0.04)], STONE, sides=14), m)
    if variant == "ruins":
        return
    for k, (u, d, turn, up) in enumerate(((0.28, 0.12, 0.3, 0.0), (0.4, 0.2, -0.4, 0.0), (0.33, 0.15, 0.1, 0.075))):
        q = _on_face(0, u, PLINTH, d)
        cc.bag(p, (q.x, q.y, PLINTH + up), turn, SACK, length=0.17, width=0.12, height=0.08)


def _roof(p, variant):
    z = TOP + 0.02
    p.turned((0.0, 0.0, 0.0), 'Z', [(z, 0.5), (z + 0.04, 0.5), (z + 0.2, 0.36), (z + 0.42, 0.17), (z + 0.6, 0.02)], ROOF, sides=16)
    p.turned((0.0, 0.0, z + 0.58), 'Z', [(0.0, 0.025), (0.06, 0.025)], DARK, sides=6)
    p.sphere((0.0, 0.0, z + 0.1 + 0.58), 'Z', 0.035, DARK, sides=8, rings=4)
    # The little gable at the front where the windshaft comes out.
    y0, y1 = -0.5, -0.24
    rings = [[Vector((-0.16, y, z)), Vector((0.16, y, z)), Vector((0.16, y, z + 0.18)), Vector((0.0, y, z + 0.3)), Vector((-0.16, y, z + 0.18))]
             for y in (y0, y1)]
    p.loft(rings, ROOF)
    p.loft([[Vector((x, y0 - 0.005, zz)) for x, zz in ((-0.14, z + 0.02), (0.14, z + 0.02), (0.14, z + 0.16), (-0.14, z + 0.16))],
            [Vector((x, y0 - 0.015, zz)) for x, zz in ((-0.14, z + 0.02), (0.14, z + 0.02), (0.14, z + 0.16), (-0.14, z + 0.16))]], WOOD)
    # The tail pole down the back, braced to the cap.
    top, foot = Vector((0.0, 0.46, z + 0.03)), Vector((0.0, 0.94, PLINTH + 0.06))
    p.bar(top, foot, 0.028, WOOD)
    for x in (-0.22, 0.22):
        p.bar(Vector((x, 0.3, z + 0.03)), top + (foot - top) * 0.45, 0.016, WOOD)
    p.cylinder(foot + Vector((0.0, 0.0, 0.06)), 'X', 0.08, 0.02, DOOR, sides=10)  # its cart wheel


def _sail(p, a, length, cloth, lattice, broken=False):
    """One sail at angle a about the hub: its stock, the frame on its trailing side, canvas over cloth (from, to) of its length,
    bare lattice bars where lattice; broken: the stock snapped short, nothing on it."""
    d = Vector((math.cos(a), 0.0, math.sin(a)))
    e = Vector((-math.sin(a), 0.0, math.cos(a)))  # the trailing side
    back = Vector((0.0, 0.012, 0.0))
    p.bar(HUB + d * 0.05, HUB + d * length, 0.02, WOOD)
    if broken:
        tip = HUB + d * length
        cc.block(p, tuple(tip + e * 0.015 + d * 0.02), (0.05, 0.03, 0.03), (0.6, 0.0, -a + 0.5), WOOD)  # a splinter
        return
    r0, r1, w = 0.16, length - 0.02, 0.21
    for r in (r0, r1):
        p.bar(HUB + d * r + e * 0.02 + back, HUB + d * r + e * w + back, 0.009, WOOD)
    p.bar(HUB + d * r0 + e * w + back, HUB + d * r1 + e * w + back, 0.01, WOOD)
    p.bar(HUB + d * r0 - e * 0.03 + back, HUB + d * r1 - e * 0.03 + back, 0.006, WOOD)  # the leading board
    if cloth:
        c0, c1 = cloth
        quad = [HUB + d * (r0 + (r1 - r0) * c0) + e * 0.025, HUB + d * (r0 + (r1 - r0) * c1) + e * 0.025,
                HUB + d * (r0 + (r1 - r0) * c1) + e * (w - 0.01), HUB + d * (r0 + (r1 - r0) * c0) + e * (w - 0.01)]
        p.loft([[q + back * 0.5 for q in quad], [q + back * 1.4 for q in quad]], SAIL)
    if lattice:
        l0, l1 = lattice
        for r in cc.steps(r0 + (r1 - r0) * l0, r0 + (r1 - r0) * l1, 0.085):
            p.bar(HUB + d * r + e * 0.02 + back, HUB + d * r + e * w + back, 0.006, WOOD)
        for s in (0.09, 0.15):
            p.bar(HUB + d * (r0 + (r1 - r0) * l0) + e * s + back, HUB + d * (r0 + (r1 - r0) * l1) + e * s + back, 0.005, WOOD)


def _sails(p, variant):
    p.cylinder(Vector((0.0, (HUB.y - 0.3) / 2 + 0.02, HUB.z)), 'Y', 0.045, (-0.3 - HUB.y) / 2 + 0.03, DOOR, sides=10)  # the windshaft
    p.cylinder(HUB, 'Y', 0.075, 0.05, WOOD, sides=10)  # the hub
    p.cylinder(HUB - Vector((0.0, 0.06, 0.0)), 'Y', 0.03, 0.02, DARK, sides=8)
    a = SAIL_ANGLES
    if variant == "intact":
        for x in a:
            _sail(p, x, SAIL_LEN, (0.0, 1.0), None)
    elif variant == "damaged":
        _sail(p, a[0], SAIL_LEN, (0.0, 1.0), None)
        _sail(p, a[1], 0.22, None, None, broken=True)  # its sail lies on the ground in front
        _sail(p, a[2], SAIL_LEN, (0.0, 0.42), (0.42, 1.0))  # the canvas torn off its outer half
        _sail(p, a[3], SAIL_LEN, (0.0, 1.0), None)
    else:  # burning: two burnt to the bare lattice, one broken short, one with charred canvas left on it
        _sail(p, a[0], SAIL_LEN, None, (0.0, 1.0))
        _sail(p, a[1], SAIL_LEN, (0.0, 0.7), (0.7, 1.0))
        _sail(p, a[2], 0.45, None, (0.0, 0.5))
        _sail(p, a[3], SAIL_LEN, None, (0.0, 1.0))


def _fallen_sail(p, at, turn, length=SAIL_LEN, lattice=False):
    """A sail lying on the ground at at, along turn (about the vertical): its stock and frame, canvas or bare lattice."""
    m = Matrix.Translation(Vector(at) + Vector((0.0, 0.0, 0.03))) @ Matrix.Rotation(turn, 4, 'Z') @ Matrix.Rotation(-math.pi / 2, 4, 'X') @ \
        Matrix.Translation(-HUB)
    _moved(p, lambda: _sail(p, 0.0, length, None if lattice else (0.0, 1.0), (0.0, 1.0) if lattice else None), m)


def _ruins(body, roof, rnd):
    # The stump: the stone course and a little of the plaster, jagged pieces of wall standing on it.
    body.loft([_ring(FOOT_R + 0.03, PLINTH), _ring(_radius(STONE_TOP) + 0.03, STONE_TOP)], STONE)
    body.loft([_ring(_radius(STONE_TOP), STONE_TOP - 0.01), _ring(_radius(0.42), 0.42)], PLASTER)
    corners = _ring(_radius(0.42) - 0.05, 0.0)
    walls = [((a.x, a.y), (b.x, b.y)) for a, b in zip(corners, corners[1:] + corners[:1])]
    ic.stumps(body, rnd, walls, 0.41, 0.08, 0.5, PLASTER, step=0.16, gap=0.3, thick=0.08)
    # The cap lying on its side at the back right, its curb ring beside it.
    m = Matrix.Translation((0.5, 0.52, 0.32)) @ Matrix.Rotation(1.75, 4, Vector((1.0, -0.6, 0.0)).normalized()) @ \
        Matrix.Translation((0.0, 0.0, -TOP - 0.3))
    _moved(roof, lambda: roof.turned((0.0, 0.0, 0.0), 'Z', [(TOP + 0.02, 0.5), (TOP + 0.06, 0.5), (TOP + 0.22, 0.36), (TOP + 0.44, 0.17),
                                                             (TOP + 0.62, 0.02)], ROOF, sides=14), m)
    # The sails broken on the ground in front: two crossed, a stock snapped, the windshaft.
    _fallen_sail(body, (-0.62, -0.42, PLINTH), math.radians(-20), lattice=True)
    _fallen_sail(body, (0.1, -0.78, 0.0), math.radians(160), length=0.7)
    body.bar(Vector((0.45, -0.6, 0.04)), Vector((0.86, -0.22, 0.05)), 0.02, WOOD)
    body.cylinder(Vector((-0.15, -0.62, 0.05)), 'X', 0.045, 0.15, DOOR, sides=8)
    # Rubble of plaster and stone, planks.
    ic.rubble(body, rnd, -0.7, 0.7, -0.75, 0.6, PLINTH, 16, 0.1, PLASTER)
    ic.rubble(body, rnd, -0.6, 0.6, -0.6, 0.6, PLINTH, 8, 0.08, STONE)
    for k in range(6):
        a = rnd.uniform(0, 2 * math.pi)
        c = Vector((math.cos(a), math.sin(a), 0.0)) * rnd.uniform(0.5, 0.82)
        cc.block(body, (c.x, c.y, 0.03), (0.4, 0.05, 0.02), (rnd.uniform(-0.1, 0.1), rnd.uniform(-0.1, 0.1), rnd.uniform(0, math.pi)), WOOD)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Cream plaster over field stone, dark shingles, light wood, dark brown door, canvas striped by its lattice, near-black, flagstones,
    burlap, white window frames; cracks and plaster fallen off when damaged, soot and char when burning and in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 4.0)
    plaster = g.mix(g.noise(5.0, 4.0), lin((0.86, 0.83, 0.74)), lin((0.93, 0.90, 0.82)))
    plaster = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -3.0, 1.2)), 0.45), plaster, lin((0.60, 0.55, 0.46)))  # earth splashed low
    stone, joints = ic.brick(g, coord, (0.52, 0.48, 0.42), (0.64, 0.60, 0.52), (0.38, 0.36, 0.33), course=14.0, length=6.0)
    # Shingles: courses round the cap, each other one shifted.
    rows = g.math('MULTIPLY', z, 26.0)
    shift = g.math('MULTIPLY', g.math('MODULO', g.math('FLOOR', rows), 2.0), 0.5)
    seam_r = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', rows), -12.0, 1.4))
    seam_a = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('ADD', g.math(
        'MULTIPLY', g.math('ARCTAN2', y, x), 40.0 / (2 * math.pi)), shift)), 0.5)), 12.0, -5.2))
    shingle = g.mix(g.noise(9.0, 3.0), lin((0.22, 0.19, 0.17)), lin((0.30, 0.26, 0.22)))
    shingle = g.mix(g.math('MULTIPLY', g.math('MAXIMUM', seam_r, seam_a), 0.7), shingle, lin((0.10, 0.09, 0.08)))
    wood = g.mix(g.noise(6.0, 4.0), lin((0.62, 0.54, 0.42)), lin((0.72, 0.64, 0.50)))
    door = g.mix(grain, lin((0.26, 0.17, 0.11)), lin((0.34, 0.23, 0.15)))
    # The canvas: light, striped by the lattice it is stretched over — bars across at steps out from the hub, laths along it. The sails'
    # node has its origin on the hub, the sails lie in its XZ plane as an X.
    rho = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', x, x), g.math('MULTIPLY', z, z)))
    u = g.math('ABSOLUTE', g.math('MULTIPLY', g.math('ADD', x, z), 0.7071))
    v = g.math('ABSOLUTE', g.math('MULTIPLY', g.math('SUBTRACT', x, z), 0.7071))
    side = g.math('MINIMUM', u, v)
    bars = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', rho, 11.7)), 0.5)), 14.0, -6.0))
    laths = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', side, 16.0)), 0.5)), 16.0, -7.0))
    canvas = g.mix(g.noise(7.0, 3.0), lin((0.86, 0.82, 0.72)), lin((0.92, 0.89, 0.80)))
    canvas = g.mix(g.math('MULTIPLY', g.math('MAXIMUM', bars, laths), 0.75), canvas, lin((0.56, 0.48, 0.36)))
    dark = lin((0.05, 0.05, 0.05))
    pad = g.mix(g.noise(4.0, 3.0), lin((0.56, 0.54, 0.50)), lin((0.66, 0.64, 0.60)))
    flag = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, g.math(
        'MULTIPLY', y, 0.6)), 4.0)), 0.5)), 30.0, -14.0))
    pad = g.mix(g.math('MULTIPLY', flag, 0.6), pad, lin((0.36, 0.34, 0.31)))
    sack = g.mix(g.noise(20.0, 3.0), lin((0.80, 0.74, 0.60)), lin((0.88, 0.83, 0.70)))
    frame = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    if variant != "intact":
        # Plaster fallen off to the stone under it, cracks.
        fallen = g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 4.0), 7.0, {"damaged": -4.0, "abandoned": -3.6, "ruins": -3.2}[variant]))
        plaster = g.mix(fallen, plaster, stone)
        plaster = g.mix(g.math('MULTIPLY', cc.cracks(g, 5.0), 0.7), plaster, lin((0.40, 0.38, 0.34)))
    if variant in ("abandoned", "ruins"):
        canvas = g.mix(0.7, canvas, lin((0.10, 0.09, 0.08)))  # charred
        wood = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 3.0), 4.0, -1.0)), 0.85), wood, lin((0.10, 0.09, 0.08)))
    albedo = cc.paint(g, mask, (plaster, stone, shingle, wood, door, canvas, dark, pad, sack, frame))
    if variant != "intact":
        amount = {"damaged": 0.15, "abandoned": 0.6, "ruins": 0.75}[variant]
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.8), albedo, lin((0.08, 0.07, 0.07)))
    rough = cc.weighted(g, mask, (0.9, 0.9, 0.85, 0.8, 0.75, 0.9, 0.9, 0.9, 0.95, 0.6))
    metallic = cc.weighted(g, mask, (0.0,) * PARTS)
    height = g.math('ADD', g.math('MULTIPLY', g.math('MULTIPLY', joints, mask[1]), -1.0), g.math('MULTIPLY', g.noise(50.0, 3.0), 0.4))
    height = g.math('ADD', height, g.math('MULTIPLY', g.math('MULTIPLY', g.math('MAXIMUM', seam_r, seam_a), mask[2]), -0.6))
    g.finish(albedo, g.bump(height, 0.06, 0.006), g.combine(ao, rough, metallic))
    return mat, g


# --- build and run -------------------------------------------------------------------

NODES = ("body", "roof", "sails", "base")


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, NODES)
    rnd = ic.seed(variant_id)
    body, roof, sails, base = Parts(), Parts(), Parts(), Parts()
    _base(base)
    if variant == "ruins":
        _ruins(body, roof, rnd)
        _yard(body, rnd, variant)
        parts = [(body, "body"), (roof, "roof"), (base, "base")]
        return cc.finish(variant_id, parts, PLINTH - 0.001, _material(variant_id + "_mat", variant))
    _tower(body)
    _curb(body)
    _openings(body, variant)
    _yard(body, rnd, variant)
    if variant == "damaged":
        _fallen_sail(body, (-0.42, -0.86, 0.0), math.radians(170))
    _roof(roof, variant)
    _sails(sails, variant)
    parts = [(body, "body"), (roof, "roof"), (sails, "sails"), (base, "base")]
    meshes, mat, graph = cc.finish(variant_id, parts, PLINTH - 0.001, _material(variant_id + "_mat", variant))
    # The sails' node on the hub: its mesh about its own origin, so the game can turn it in place.
    obj = next(o for o in meshes if o.name.split(".")[0].endswith("_sails"))
    obj.data.transform(Matrix.Translation(-HUB))
    obj.location = HUB
    return meshes, mat, graph


def _fire_points():
    """The door, the window over it on the front right face, the cap."""
    return (tuple(_on_face(0, 0.0, 0.3, 0.06)), tuple(_on_face(1, 0.0, 1.2, 0.05)), (0.0, 0.0, 1.98))


def _layout():
    """The places of the farm's models in the file: rts_common's LAYOUT has no farm row yet, so this script adds its own for the run."""
    row = "15. Ферма"
    if row not in rc.ROWS:
        rc.ROWS = rc.ROWS + (row,)
    for k, key in enumerate(VARIANTS):
        rc.LAYOUT.setdefault(key, (rc.ROWS.index(row), 14 + k))


def run(variant_id):
    _layout()
    meshes, mat, graph = build(variant_id)
    fire = _fire_points() if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
