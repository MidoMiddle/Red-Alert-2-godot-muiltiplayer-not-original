"""Broadleaf tree (docs/temperate_elements/01_deciduous_tree, docs/map_elements/07_trees): the forest of the temperate and city maps.

The map's tree (pine_tree) is drawn by the climate's shape (game/scripts/Terrain/TreeShapes.cs): in the temperate climate and the city a
small broadleaf tree (variants 0, 1) or a big one (2, 3), by the sea a small one now and then among the palms. tree/broadleaf_small and
tree/broadleaf_big (game/data/visuals/models.yaml), each cell picking one of its two kinds. Made at the size the game draws a tree, as
its placeholders: the small ones about 0.9 tall, the big ones about 1.1, the crown 0.5 to 0.9 across. Origin at the centre of the cell
on the ground. The parts:
    trunk   a short trunk of six sides, flared at its foot, forking into two or three boughs that go up into the crown
    crown   clumps of leaves, each a lumpy hull of jittered points, heaped round the top of the trunk into one rounded crown; the leaves
            are the material: lighter where they face the sun, darker under the crown and deep between the clumps, in small patches
Node: "body". No team colour. One GLB per kind:
    tree_deciduous_01  small: a young lime, a round crown on a short trunk                      broadleaf_small
    tree_deciduous_02  small: a birch, slender, an oval crown of lighter green, white bark       broadleaf_small
    tree_deciduous_03  big: an oak, a thick short trunk, a broad crown wider than tall           broadleaf_big
    tree_deciduous_04  big: a chestnut, a tall domed crown leaning a little to one side          broadleaf_big
Budget 300 triangles (docs/art/model_standards.md, «the forest of a map»), texture 512: a tree is the most numerous thing on a map.

In Blender, after tools/blender is on sys.path:
    import families.map_broadleaf as broadleaf
    print(broadleaf.run_all())
"""

import math
import random

import bmesh
import bpy
from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "map"
SIZE = 512

# trunk: its height to the fork and its radius at the foot; crown: centre height, radii across and up, clumps; boughs; lean of the crown
# (x, y); birch: the white bark and the lighter leaves.
VARIANTS = {
    "tree_deciduous_01": dict(fork=0.3, foot=0.045, centre=0.6, rx=0.3, rz=0.25, clumps=5, boughs=2, lean=(0.0, 0.0), birch=False, seed=11),
    "tree_deciduous_02": dict(fork=0.36, foot=0.035, centre=0.66, rx=0.22, rz=0.28, clumps=5, boughs=2, lean=(0.03, 0.0), birch=True, seed=12),
    "tree_deciduous_03": dict(fork=0.34, foot=0.07, centre=0.74, rx=0.44, rz=0.3, clumps=6, boughs=3, lean=(0.0, 0.0), birch=False, seed=13),
    "tree_deciduous_04": dict(fork=0.4, foot=0.06, centre=0.8, rx=0.36, rz=0.34, clumps=6, boughs=3, lean=(0.06, -0.04), birch=False, seed=14),
}

(BARK, LEAVES) = (cc.part(i) for i in range(2))
PARTS = 2


def _hull(p, points, painted):
    verts = [p.bm.verts.new(q) for q in points]
    made = bmesh.ops.convex_hull(p.bm, input=verts)
    unused = list({v for v in made["geom_interior"] + made["geom_unused"] if isinstance(v, bmesh.types.BMVert)})
    bmesh.ops.delete(p.bm, geom=unused, context='VERTS')
    for f in (f for f in made["geom"] if isinstance(f, bmesh.types.BMFace)):
        for loop in f.loops:
            loop[p.layer] = painted


def _clump(p, rnd, centre, rx, rz):
    """A clump of leaves: a hull of points on a jittered ellipsoid, flatter underneath."""
    c = Vector(centre)
    pts = []
    count = 18
    for k in range(count):  # spread evenly over the ball (the golden angle), each pushed in or out a little
        a = k * 2.39996 + rnd.uniform(-0.15, 0.15)
        b = math.acos(1 - 2 * (k + 0.5) / count)
        r = rnd.uniform(0.88, 1.08)
        z = math.cos(b) * rz * r
        if z < 0:
            z *= 0.6
        pts.append(c + Vector((math.cos(a) * math.sin(b) * rx * r, math.sin(a) * math.sin(b) * rx * r, z)))
    _hull(p, pts, LEAVES)


def _tree(p, v):
    rnd = random.Random(v["seed"])
    lx, ly = v["lean"]
    crown = Vector((lx, ly, v["centre"]))
    # The trunk to the fork, then the boughs up into the crown.
    f = v["foot"]
    p.turned((0.0, 0.0, 0.0), 'Z', [(-0.02, f * 1.5), (0.05, f), (v["fork"], f * 0.75)], BARK, sides=6)
    top = Vector((lx * 0.4, ly * 0.4, v["fork"]))
    for k in range(v["boughs"]):
        a = 2 * math.pi * k / v["boughs"] + rnd.uniform(-0.3, 0.3)
        end = crown + Vector((math.cos(a) * v["rx"] * 0.45, math.sin(a) * v["rx"] * 0.45, rnd.uniform(-0.05, 0.05)))
        p.bar(top - Vector((0, 0, 0.03)), end, f * 0.45, BARK)
    # The clumps: one in the middle and up, the rest round it, a little lower, so the crown is round and lumpy.
    _clump(p, rnd, crown + Vector((0.0, 0.0, v["rz"] * 0.25)), v["rx"] * 0.62, v["rz"] * 0.75)
    ring = v["clumps"] - 1
    for k in range(ring):
        a = 2 * math.pi * k / ring + rnd.uniform(-0.25, 0.25)
        d = v["rx"] * rnd.uniform(0.45, 0.55)
        c = crown + Vector((math.cos(a) * d, math.sin(a) * d, rnd.uniform(-0.12, 0.05) * v["rz"]))
        _clump(p, rnd, c, v["rx"] * rnd.uniform(0.45, 0.55), v["rz"] * rnd.uniform(0.55, 0.7))


def _material(name, birch):
    """Grey-brown bark (a birch's white, its black marks); leaves green, lighter where they face up, darker under the crown and low."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    up = g.axis(g.normal_world(), 'Z')
    grain = g.noise(40.0, 3.0)
    if birch:
        marks = g.clamp01(g.math('MULTIPLY_ADD', g.noise(25.0, 2.0), 8.0, -5.0))
        bark = g.mix(marks, g.mix(grain, lin((0.80, 0.78, 0.72)), lin((0.88, 0.86, 0.80))), lin((0.12, 0.11, 0.10)))
        dark, light = lin((0.22, 0.40, 0.14)), lin((0.46, 0.62, 0.24))
    else:
        bark = g.mix(grain, lin((0.30, 0.25, 0.19)), lin((0.40, 0.33, 0.25)))
        dark, light = lin((0.14, 0.30, 0.11)), lin((0.36, 0.52, 0.19))
    leaf = g.noise(22.0, 4.0)  # the clusters of leaves
    sun = g.clamp01(g.math('MULTIPLY_ADD', up, 0.55, 0.45))
    tone = g.clamp01(g.math('ADD', g.math('MULTIPLY', sun, 0.75), g.math('MULTIPLY_ADD', leaf, 0.5, -0.2)))
    leaves = g.mix(tone, dark, light)
    low = g.clamp01(g.math('MULTIPLY_ADD', z, -2.5, 1.6))  # darker low in the crown, where the light does not reach
    leaves = g.mix(g.math('MULTIPLY', low, 0.35), leaves, lin((0.08, 0.16, 0.07)))
    albedo = cc.paint(g, mask, (bark, leaves))
    rough = cc.weighted(g, mask, (0.9, 0.75))
    g.finish(albedo, g.bump(g.math('ADD', leaf, g.math('MULTIPLY', grain, 0.5)), 0.4, 0.015), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body",))
    body = Parts()
    v = VARIANTS[variant_id]
    _tree(body, v)
    meshes, mat, graph = cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat", v["birch"]))
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth()  # the clumps round, not faceted
    return meshes, mat, graph


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
