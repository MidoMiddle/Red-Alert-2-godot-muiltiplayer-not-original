"""City bench: element 10 of the city set (docs/city_elements/10_bench) and its states (appearance.md, 4). City climate: dry daylight,
no snow. A map object (bench, game/data/map/objects.yaml): no soldiers, no team zone.

Footprint 1x1 cell, origin at the centre of the cell on the ground, the seat along x, its front to -Y. Made at the size the game draws
it, as the street stall (the game does not squash map objects): 0.76 long, the seat at 0.2, the backrest to 0.42. The parts:
    frame     two dark green cast-iron sides, each a front leg, a back leg running up into the backrest's post, and an armrest
    slats     four warm brown wooden slats on the seat, three on the backrest
No emblems, no text, nobody sitting (prompts.md, 4).
Node: "bench". One GLB per state:
    city_bench              whole
    city_bench_damaged      damaged: two seat slats broken, the backrest gone, a piece of it on the ground
    city_bench_ruins        destroyed: the sides fallen over, the slats on the ground
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one wait for the
client to show them.
Budget 500 triangles, texture 1024 (docs/city_elements/10_bench/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_bench as cbe
    print(cbe.run("city_bench"))   # or cbe.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_bench": "intact",
    "city_bench_damaged": "damaged",
    "city_bench_ruins": "ruins",
}

IRON, WOOD = (cc.part(i) for i in range(2))
PARTS = 2

HALF = 0.38  # half the length of the slats
SIDE_X = 0.33  # where the two sides stand
SEAT_Z = 0.2
SEAT_Y = (-0.13, 0.07)  # the front and the back of the seat
BACK = (Vector((0.0, 0.1, 0.24)), Vector((0.0, 0.16, 0.42)))  # the backrest's line, its foot and its top
SEAT_SLATS = 4
BACK_SLATS = 3


def _side(p, x):
    """One cast-iron side: the front leg, the back leg rising into the backrest's post, the rail under the seat, the armrest."""
    p.bar((x, SEAT_Y[0] + 0.02, 0.0), (x, SEAT_Y[0] + 0.03, SEAT_Z), 0.018, IRON)
    p.bar((x, SEAT_Y[1] + 0.02, 0.0), (x, BACK[0].y, SEAT_Z), 0.018, IRON)
    p.bar((x, BACK[0].y, SEAT_Z), (x, BACK[1].y + 0.01, BACK[1].z), 0.016, IRON)
    p.bar((x, SEAT_Y[0], SEAT_Z - 0.03), (x, SEAT_Y[1] + 0.03, SEAT_Z - 0.03), 0.014, IRON)
    p.bar((x, SEAT_Y[0] + 0.01, 0.29), (x, BACK[0].y + 0.02, 0.3), 0.014, IRON)  # the armrest
    p.bar((x, SEAT_Y[0] + 0.02, SEAT_Z), (x, SEAT_Y[0] + 0.01, 0.29), 0.012, IRON)


def _seat(p, broken=()):
    """The seat's slats, front to back; broken ones are left out, their halves hanging."""
    depth = (SEAT_Y[1] - SEAT_Y[0]) / SEAT_SLATS
    for k in range(SEAT_SLATS):
        y0 = SEAT_Y[0] + k * depth
        if k in broken:
            for sign in (-1, 1):  # the two halves, each hanging from its side
                a = Vector((sign * HALF, y0 + depth / 2, SEAT_Z + 0.015))
                b = Vector((sign * 0.06, y0 + depth / 2, SEAT_Z - 0.06))
                p.bar(a, b, 0.016, WOOD)
            continue
        p.box((-HALF, y0 + 0.005, SEAT_Z), (HALF, y0 + depth - 0.005, SEAT_Z + 0.03), WOOD)


def _back(p):
    """The backrest's slats along its sloped line."""
    for k in range(BACK_SLATS):
        t = (k + 0.5) / BACK_SLATS
        c = BACK[0].lerp(BACK[1], t)
        p.bar((-HALF, c.y, c.z), (HALF, c.y, c.z), 0.022, WOOD)


def _turn(p, faces_before, angle, axis, pivot, shift=Vector()):
    """Turns what was added to p since faces_before about pivot, and moves it."""
    verts = {v for f in p.bm.faces if f not in faces_before for v in f.verts}
    m = Matrix.Rotation(angle, 3, axis)
    for v in verts:
        v.co = m @ (v.co - pivot) + pivot + shift


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Dark green cast iron, warm brown wood with its grain; scuffed when broken."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(30.0, 4.0)
    iron = g.mix(grain, lin((0.12, 0.24, 0.17)), lin((0.17, 0.30, 0.22)))
    wood = g.mix(g.noise(4.0, 8.0), lin((0.52, 0.34, 0.18)), lin((0.62, 0.42, 0.24)))
    if variant != "intact":
        wood = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 3.0), 5.0, -2.8)), wood, lin((0.24, 0.18, 0.12)))
    albedo = cc.paint(g, mask, (iron, wood))
    rough = cc.weighted(g, mask, (0.5, 0.75))
    metallic = cc.weighted(g, mask, (0.6, 0.0))
    normal = g.bump(g.noise(60.0, 3.0), 0.04, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("bench",))
    variant = VARIANTS[variant_id]
    p = Parts()
    if variant == "ruins":
        before = set(p.bm.faces)
        _side(p, 0.0)
        _turn(p, before, math.radians(85), 'Y', Vector((0.0, 0.0, 0.0)), Vector((-0.25, -0.05, 0.02)))
        before = set(p.bm.faces)
        _side(p, 0.0)
        _turn(p, before, math.radians(-80), 'X', Vector((0.0, 0.0, 0.0)), Vector((0.3, 0.12, 0.02)))
        for c, size, turn in (((0.0, -0.2, 0.02), (0.76, 0.05, 0.03), (0.0, 0.0, 0.12)), ((0.05, -0.05, 0.035), (0.7, 0.05, 0.03), (0.0, 0.1, -0.2)),
                              ((-0.15, 0.2, 0.02), (0.4, 0.05, 0.03), (0.0, 0.0, 0.6)), ((0.25, 0.25, 0.02), (0.36, 0.05, 0.03), (0.0, 0.0, -0.9)),
                              ((0.0, 0.05, 0.06), (0.74, 0.05, 0.03), (0.3, 0.0, 0.25))):
            cc.block(p, c, size, turn, WOOD)
    else:
        for x in (-SIDE_X, SIDE_X):
            _side(p, x)
        if variant == "damaged":
            _seat(p, broken=(1, 2))
            cc.block(p, (0.1, 0.3, 0.02), (0.76, 0.05, 0.03), (0.0, 0.0, 0.15), WOOD)  # the backrest, down behind
        else:
            _seat(p)
            _back(p)
    return cc.finish(variant_id, [(p, "bench")], 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the node bench."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
