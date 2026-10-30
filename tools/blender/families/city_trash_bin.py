"""City trash bin: element 11 of the city set (docs/city_elements/11_trash_bin) and its states (appearance.md, 4). City climate: dry
daylight, no snow. A map object (trash_bin, game/data/map/objects.yaml): no soldiers, no team zone.

Footprint 1x1 cell, origin at the centre of the cell on the ground, its long side along x, its front to -Y. Made at the size the game
draws it, as the bench (the game does not squash map objects): 0.7 long and 0.48 high with its lid, a little bigger than the bench, not
the prompt's 0.6 high a whole cell long, which would dwarf it. The parts:
    body      a dark green steel container, wider at the top, a rim round its mouth, lifting pockets on its sides, four small wheels,
              rust at its corners and low down
    lid       two halves of a darker green lid hinged at the back, sloping a little to the front
No emblems, no text. Burning (smoke, a charred lid) is the client's (prompts.md, 4).
Nodes: "body" (and the rubbish of a state), "lid" (a node of its own, prompts.md, 4). One GLB per state:
    city_trash_bin              whole
    city_trash_bin_damaged      damaged: dented in front, more rust, one half of the lid thrown open, a bag of rubbish beside it
    city_trash_bin_ruins        destroyed: the container on its side, the lid off on the ground, bags and paper scattered round it
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one wait for the
client to show them.
Budget 500 triangles, texture 1024 (docs/city_elements/11_trash_bin/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_trash_bin as ctb
    print(ctb.run("city_trash_bin"))   # or ctb.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_trash_bin": "intact",
    "city_trash_bin_damaged": "damaged",
    "city_trash_bin_ruins": "ruins",
}

STEEL, LID, BLACK, BAG, PAPER = (cc.part(i) for i in range(5))
PARTS = 5

FOOT = (0.32, 0.18, 0.07)  # half length, half width, height of the bottom
MOUTH = (0.35, 0.21, 0.42)  # the same at the top
HINGE_Y = MOUTH[1] + 0.01


def _body(p, dent=False):
    """The container, wider at the top, its rim, its lifting pockets and its wheels. A dent pushes the middle of its front in."""
    (fx, fy, fz), (mx, my, mz) = FOOT, MOUTH
    mid = [(mx - 0.015, my - 0.015, (fz + mz) / 2)]
    rings = [[Vector((sx * x, sy * y, z)) for sx, sy in ((1, -1), (1, 1), (-1, 1), (-1, -1))] for x, y, z in ((fx, fy, fz), *mid, (mx, my, mz))]
    if dent:
        rings[1][0] = rings[1][0] + Vector((-0.04, 0.06, -0.02))  # the front right corner pushed in
        rings[1][3] = rings[1][3] + Vector((0.0, 0.03, 0.0))
    p.loft(rings, STEEL)
    p.loft([[Vector((sx * (mx + 0.012), sy * (my + 0.012), z)) for sx, sy in ((1, -1), (1, 1), (-1, 1), (-1, -1))] for z in (mz - 0.04, mz)], STEEL)
    for sx in (-1, 1):  # the lifting pockets
        p.box((sx * (fx + 0.035) - 0.03, -0.1, 0.24), (sx * (fx + 0.035) + 0.03, 0.1, 0.3), STEEL)
    for sx in (-1, 1):
        for sy in (-1, 1):
            c = Vector((sx * (fx - 0.05), sy * (fy - 0.04), 0.045))
            p.cylinder(c, 'Y', 0.045, 0.015, BLACK, sides=8)
            p.box(c + Vector((-0.02, -0.02, 0.02)), c + Vector((0.02, 0.02, FOOT[2] - 0.045 + 0.01)), STEEL)


def _lid(p, open_right=False):
    """Two halves of the lid, hinged at the back. Open: the right half thrown back on its hinge."""
    mx, my, mz = MOUTH
    for x0, x1 in ((-mx - 0.01, -0.005), (0.005, mx + 0.01)):
        rings = [[Vector((x, -my - 0.02, mz + 0.005)), Vector((x, HINGE_Y, mz + 0.045)), Vector((x, HINGE_Y, mz + 0.07)),
                  Vector((x, -my - 0.02, mz + 0.03))] for x in (x0, x1)]
        if open_right and x0 > 0:
            hinge = Vector((0.0, HINGE_Y, mz + 0.05))
            m = Matrix.Rotation(math.radians(-115), 3, 'X')
            rings = [[hinge + m @ (v - hinge) for v in ring] for ring in rings]
        p.loft(rings, LID)
        if not (open_right and x0 > 0):
            p.box((x0 + 0.05, -my - 0.05, mz), (x0 + 0.12, -my - 0.02, mz + 0.03), LID)  # its handle at the front


# --- the rubbish ------------------------------------------------------------------------

def _bag(p, centre, size, turn):
    """A black bag of rubbish: a soft lump, narrower at its knot."""
    c = Vector(centre)
    m = Matrix.Rotation(turn, 3, 'Z')
    sx, sy, sz = size
    p.loft([[c + m @ Vector((u * a, v * b, h)) for u, v in ((1, 1), (-1, 1), (-1, -1), (1, -1))]
            for a, b, h in ((sx * 0.8, sy * 0.8, 0.0), (sx, sy, sz * 0.45), (sx * 0.6, sy * 0.6, sz * 0.85), (sx * 0.15, sy * 0.15, sz))], BAG)


PAPERS = tuple(((x, y, 0.004), (0.05, 0.04, 0.005), (0.0, 0.0, 1.3 * k))
               for k, (x, y) in enumerate(((-0.4, -0.35), (0.3, -0.38), (0.42, 0.1), (-0.1, -0.42), (0.1, 0.4), (-0.42, 0.3))))


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Dark green galvanised steel with rust at the corners and low down, a darker lid, black rubber and bags, white paper."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    steel = g.mix(grain, lin((0.14, 0.28, 0.20)), lin((0.19, 0.34, 0.25)))
    lid = g.mix(grain, lin((0.09, 0.19, 0.14)), lin((0.13, 0.24, 0.18)))
    # Rust: at the corners (far from both middles) and low down, in patches; more of it on the damaged and the destroyed one.
    corner = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', x), 20.0, -6.0)),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', y), 20.0, -3.2)))
    low = g.clamp01(g.math('MULTIPLY_ADD', z, -12.0, 1.4))
    patch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(9.0, 4.0), 6.0, -3.3 if variant == "intact" else -2.5))
    rust = g.math('MULTIPLY', g.math('MAXIMUM', corner, low), patch)
    rust_col = g.mix(grain, lin((0.38, 0.20, 0.10)), lin((0.50, 0.28, 0.14)))
    steel = g.mix(rust, steel, rust_col)
    lid = g.mix(g.math('MULTIPLY', rust, 0.6), lid, rust_col)
    black = g.mix(grain, lin((0.05, 0.05, 0.05)), lin((0.09, 0.09, 0.09)))
    bag = g.mix(g.noise(12.0, 3.0), lin((0.04, 0.04, 0.05)), lin((0.12, 0.12, 0.14)))
    paper = g.mix(grain, lin((0.82, 0.80, 0.74)), lin((0.92, 0.90, 0.84)))
    albedo = cc.paint(g, mask, (steel, lid, black, bag, paper))
    rough = cc.weighted(g, mask, (0.55, 0.5, 0.85, 0.3, 0.9))
    metallic = cc.weighted(g, mask, (0.6, 0.6, 0.0, 0.0, 0.0))
    normal = g.bump(g.noise(50.0, 3.0), 0.05, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _turn_all(p, angle, axis, pivot, shift):
    m = Matrix.Rotation(angle, 3, axis)
    for v in p.bm.verts:
        v.co = m @ (v.co - pivot) + pivot + shift


def build(variant_id):
    cc.clear(variant_id, ("body", "lid"))
    variant = VARIANTS[variant_id]
    body = Parts()
    lid = Parts()
    if variant == "ruins":
        _body(body, dent=True)
        _turn_all(body, math.radians(-95), 'X', Vector((0.0, -FOOT[1], 0.0)), Vector((0.0, 0.05, MOUTH[1] + 0.02)))
        _lid(lid)
        _turn_all(lid, math.radians(25), 'Z', Vector((0.0, 0.0, 0.0)), Vector((0.1, -0.25, -MOUTH[2] - 0.005)))
        for c, s, t in (((-0.3, -0.3, 0.0), (0.09, 0.08, 0.12), 0.4), ((0.35, -0.2, 0.0), (0.08, 0.07, 0.1), 1.2),
                        ((-0.38, 0.15, 0.0), (0.07, 0.07, 0.09), 2.0)):
            _bag(body, c, s, t)
        for centre, size, turn in PAPERS:
            cc.block(body, centre, size, turn, PAPER)
    else:
        _body(body, dent=variant == "damaged")
        _lid(lid, open_right=variant == "damaged")
        if variant == "damaged":
            _bag(body, (0.45, -0.25, 0.0), (0.09, 0.08, 0.13), 0.5)
            for centre, size, turn in PAPERS[:2]:
                cc.block(body, centre, size, turn, PAPER)
    return cc.finish(variant_id, [(body, "body"), (lid, "lid")], 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body and lid."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
