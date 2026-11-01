"""Reef, element 02 of the naval set (docs/naval_elements/02_reef): rock outcrops rising just out of the sea; they stop ships and amphibians.

Footprint 1x1 cell, origin at the centre of the cell on the water's surface (the ground of a water cell is the water: what lies below 0 is
hidden). Made at the size the game draws it, as the boulder it stands for at sea: the tallest outcrop to 0.55 over the water. The parts:
    rocks      six outcrops of dark grey stone, each two angular chunks with big flat facets (convex hulls of jittered rings): a
               broad foot from under the water and a narrower crown leaning over it, worn flat on top; two flat rocks just awash; brown weed and a dark wet band at the waterline, white salt and spray chips on the tops, cracks
Node: "body". No foam in the model: the water's foam and spray are the shader's and the particles' (prompts.md, section 4). One GLB per
state:
    reef_rocks          whole
    reef_rocks_damaged  cracked: the tallest outcrop broken off at 0.36, its fresh pale break, the top lying tilted beside it, chips
    reef_rocks_ruins    destroyed: the outcrops broken down to the waterline, low stumps and a scatter of stones just awash
Budget 1000 triangles, texture 1024 (docs/naval_elements/02_reef/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.naval_reef as reef
    print(reef.run_all())
"""

import math
import random

import bpy
import bmesh
from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "naval"  # game/assets/models/naval/<id>/, assets_src/models/naval/<id>/
SIZE = 1024
VARIANTS = {
    "reef_rocks": "intact",
    "reef_rocks_damaged": "damaged",
    "reef_rocks_ruins": "ruins",
}

(ROCK, FRESH) = (cc.part(i) for i in range(2))
PARTS = 2

FLOOR = -0.15  # the rocks go this far under the water, so the waterline is never their edge
# The outcrops: centre (x, y), radius at the waterline, height over the water, lean of the top (x, y).
OUTCROPS = (
    ((-0.08, 0.06), 0.26, 0.55, (0.06, -0.03)),
    ((0.22, -0.12), 0.18, 0.38, (0.05, -0.04)),
    ((-0.3, -0.2), 0.14, 0.26, (-0.04, -0.03)),
    ((0.18, 0.3), 0.12, 0.18, (0.02, 0.05)),
    ((-0.32, 0.24), 0.1, 0.12, (-0.03, 0.02)),
    ((0.38, 0.12), 0.08, 0.08, (0.02, 0.0)),
)
AWASH = (((0.36, -0.36), 0.09), ((-0.06, -0.36), 0.08))  # flat rocks just out of the water
BREAK = 0.36  # where the tallest outcrop broke when damaged


def _hull(p, points, cut, painted):
    """The convex hull of the points as faces of p: an angular chunk of rock, big flat facets; clipped flat at cut (the clipped faces
    FRESH)."""
    verts = [p.bm.verts.new(q) for q in points]
    made = bmesh.ops.convex_hull(p.bm, input=verts)
    unused = list({v for v in made["geom_interior"] + made["geom_unused"] if isinstance(v, bmesh.types.BMVert)})
    bmesh.ops.delete(p.bm, geom=unused, context='VERTS')
    faces = [f for f in made["geom"] if isinstance(f, bmesh.types.BMFace)]
    if cut is not None:
        for f in faces:
            for v in f.verts:
                v.co.z = min(v.co.z, cut)
    for f in faces:
        f.normal_update()
        broken = cut is not None and f.normal.z > 0.97 and f.calc_center_median().z > cut - 0.01
        for loop in f.loops:
            loop[p.layer] = FRESH if broken else painted


def rock(p, centre, radius, top, seed, lean=(0.0, 0.0), cut=None, painted=ROCK):
    """An outcrop of two angular chunks: a broad foot under the water up to two thirds of its height, a narrower crown on it leaning
    over, worn flat on top; cut flat at cut."""
    rnd = random.Random(seed)
    height = top - FLOOR

    def ring(t, r, n, jitter):
        a0 = rnd.uniform(0, 2 * math.pi)
        out = []
        for k in range(n):
            a = a0 + 2 * math.pi * k / n + rnd.uniform(-0.35, 0.35)
            rr = r * rnd.uniform(0.6, 1.25)
            out.append(Vector((centre[0] + lean[0] * t + math.cos(a) * rr, centre[1] + lean[1] * t + math.sin(a) * rr * 0.85,
                               FLOOR + t * height + rnd.uniform(-jitter, jitter) * height)))
        return out

    foot = ring(0.0, radius, 7, 0.0) + ring(0.3, radius * 1.0, 6, 0.08) + ring(0.5, radius * 0.85, 5, 0.08) + ring(0.68, radius * 0.6, 4, 0.06)
    _hull(p, foot, cut, painted)
    if cut is None or cut > FLOOR + 0.62 * height:
        crown = ring(0.55, radius * 0.6, 5, 0.03) + ring(0.85, radius * 0.5, 5, 0.04) + ring(1.0, radius * 0.32, 4, 0.0)
        for q in crown[-4:]:
            q.z = min(q.z, top)
        _hull(p, crown, cut, painted)


def _outcrops(p, variant, rnd):
    for k, ((x, y), radius, top, lean) in enumerate(OUTCROPS):
        cut = None
        if variant == "ruins":
            top, cut = min(top, 0.12 + 0.03 * k), 0.06 + 0.025 * (k % 2)
        elif variant == "damaged" and k == 0:
            cut = BREAK
        elif variant == "damaged" and k == 1:
            cut = top - 0.06  # a chip off its top
        rock(p, (x, y), radius, top, k + 1, lean, cut)
    for k, ((x, y), radius) in enumerate(AWASH):
        rock(p, (x, y), radius, 0.03 + 0.01 * k, 11 + k)
    if variant == "damaged":
        # The broken crown of the tallest outcrop, fallen on its side against it; chips round its foot.
        before = set(p.bm.verts)
        rock(p, (0.0, 0.0), 0.13, 0.12, 7)
        lie = Matrix.Translation((0.2, 0.26, 0.05)) @ Matrix.Rotation(0.7, 4, 'Z') @ Matrix.Rotation(1.35, 4, 'X') @ Matrix.Translation((0, 0, -FLOOR - 0.13))
        for v in p.bm.verts:
            if v not in before:
                v.co = lie @ v.co
        for k in range(6):
            a = 2 * math.pi * k / 6 + rnd.uniform(-0.3, 0.3)
            c = (math.cos(a) * 0.3 - 0.05, math.sin(a) * 0.3 + 0.05, 0.02)
            cc.block(p, c, (rnd.uniform(0.04, 0.07), rnd.uniform(0.03, 0.06), rnd.uniform(0.03, 0.05)),
                     (rnd.uniform(-0.5, 0.5), rnd.uniform(-0.5, 0.5), a), FRESH if k % 2 else ROCK)
    if variant == "ruins":
        ic.rubble(p, rnd, -0.42, 0.42, -0.42, 0.42, -0.02, 12, 0.07, ROCK)


def _material(name, variant):
    """Dark grey stone in lumps of lighter and browner; at the waterline a dark wet band and brown weed under it, white salt and spray
    chips on the tops; the fresh breaks pale; cracks when damaged."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    up = g.clamp01(g.math('MULTIPLY_ADD', g.axis(g.normal_world(), 'Z'), 3.0, -1.5))
    grain = g.noise(18.0, 4.0)
    stone = g.mix(g.noise(5.0, 3.0), lin((0.27, 0.28, 0.29)), lin((0.40, 0.40, 0.40)))
    stone = g.mix(g.math('MULTIPLY', grain, 0.5), stone, lin((0.36, 0.32, 0.27)))  # browner lumps
    # Wet and dark from the waterline up to the spray, weed below it.
    wet = cc.band(g, z, -0.2, 0.09)
    stone = g.mix(g.math('MULTIPLY', wet, 0.7), stone, lin((0.13, 0.13, 0.13)))
    weed = g.math('MULTIPLY', cc.band(g, z, -0.2, 0.04), g.clamp01(g.math('MULTIPLY_ADD', g.noise(9.0, 3.0), 4.0, -1.6)))
    stone = g.mix(weed, stone, lin((0.26, 0.22, 0.10)))
    # Salt and spray on the upward faces high up, in chips.
    salt = g.math('MULTIPLY', g.math('MULTIPLY', up, g.clamp01(g.math('MULTIPLY_ADD', z, 5.0, -0.6))),
                  g.clamp01(g.math('MULTIPLY_ADD', g.noise(30.0, 2.0), 6.0, -3.2)))
    stone = g.mix(salt, stone, lin((0.84, 0.84, 0.80)))
    fresh = g.mix(grain, lin((0.56, 0.55, 0.52)), lin((0.66, 0.65, 0.62)))
    albedo = cc.paint(g, mask, (stone, fresh))
    if variant != "intact":
        albedo = g.mix(g.math('MULTIPLY', mask[0], cc.cracks(g, 6.0)), albedo, lin((0.08, 0.08, 0.08)))
    rough = g.math('MULTIPLY_ADD', wet, -0.5, 0.9)
    height = g.math('ADD', g.noise(14.0, 4.0), g.math('MULTIPLY', g.noise(60.0, 2.0), 0.4))
    g.finish(albedo, g.bump(height, 0.25, 0.01), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body",))
    rnd = ic.seed(variant_id)
    body = Parts()
    _outcrops(body, variant, rnd)
    meshes, mat, graph = cc.finish(variant_id, [(body, "body")], FLOOR, _material(variant_id + "_mat", variant))
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(12))  # the facets of the rock stay sharp
    return meshes, mat, graph


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
