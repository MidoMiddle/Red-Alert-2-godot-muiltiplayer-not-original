"""Spruce, element 07 of the map set (docs/map_elements/07_trees): the snowy fir of the winter maps.

The map's tree (pine_tree) is drawn by the climate's shape: on the winter maps it is tree/pine (game/data/visuals/models.yaml), each cell
picking one of the four below. Made at the size the game draws a tree, as its placeholder: about 1.1 tall, the lowest tier 0.4 across.
Origin at the centre of the cell on the ground. The parts:
    trunk   a short six-sided trunk, dark brown, a flare at its foot
    crown   tiers of dark green needles stacked up the trunk, each a low cone whose rim droops in points between its boughs, the next
            one narrower above it, the top a spike; snow lies on what faces up — the tiers' tops, thicker on the lower ones — and a
            little on the rims (the material, by the face's slope, no geometry)
Node: "body". No team colour. One GLB per kind:
    tree_spruce_01  slim and tall, five tiers
    tree_spruce_02  broad and full, five tiers
    tree_spruce_03  young and short, four tiers
    tree_spruce_04  leaning a little and lopsided, a gap where a bough broke
Budget 300 triangles (docs/art/model_standards.md, «the forest of a map»), texture 512: a tree is the most numerous thing on a map.

In Blender, after tools/blender is on sys.path:
    import families.map_spruce as spruce
    print(spruce.run_all())
"""

import math

from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "map"
SIZE = 512

# height, width of the lowest tier, tiers, the crown's foot, lean (radians), a tier with a broken bough (or None), turn of each tier.
VARIANTS = {
    "tree_spruce_01": dict(height=1.15, width=0.36, tiers=5, foot=0.16, lean=0.0, broken=None, seed=1),
    "tree_spruce_02": dict(height=1.05, width=0.46, tiers=5, foot=0.12, lean=0.0, broken=None, seed=2),
    "tree_spruce_03": dict(height=0.8, width=0.32, tiers=4, foot=0.1, lean=0.0, broken=None, seed=3),
    "tree_spruce_04": dict(height=1.08, width=0.4, tiers=5, foot=0.18, lean=0.07, broken=2, seed=4),
}

(BARK, NEEDLES) = (cc.part(i) for i in range(2))
PARTS = 2
SIDES = 6  # the boughs round a tier


def _tier(p, z0, height, radius, turn, broken=False):
    """A tier: a rim of SIDES points drooping out and down, the hollow under it shut by a flat underside; the top narrows to the
    trunk. The points between the boughs tuck in."""
    rim, top = [], []
    for k in range(SIDES * 2):
        a = turn + math.pi * k / SIDES
        bough = k % 2 == 0
        r = radius * (1.0 if bough else 0.62)
        if broken and k in (0, 1, 2):
            r *= 0.45
        droop = 0.05 * radius / 0.2 if bough else 0.0
        rim.append(Vector((math.cos(a) * r, math.sin(a) * r, z0 - droop)))
    for k in range(SIDES * 2):
        a = turn + math.pi * k / SIDES
        top.append(Vector((math.cos(a) * radius * 0.08, math.sin(a) * radius * 0.08, z0 + height)))
    p.loft([rim, top], NEEDLES)


def _tree(p, v):
    rnd = __import__("random").Random(v["seed"])
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.05), (0.04, 0.035), (v["foot"] + 0.04, 0.03)], BARK, sides=6)
    tiers = v["tiers"]
    span = v["height"] - v["foot"]
    for k in range(tiers):
        t = k / tiers
        z0 = v["foot"] + span * t * 0.92
        radius = v["width"] * (1.0 - 0.8 * t) * rnd.uniform(0.93, 1.05)
        height = max(radius * 0.8, span * 0.2)  # every tier the same slope, so the snow lies alike on all
        _tier(p, z0, height, radius, rnd.uniform(0, math.pi), broken=v["broken"] == k)
    top = v["foot"] + span * 0.92 + 0.02
    p.turned((0.0, 0.0, top), 'Z', [(0.0, 0.035), (v["height"] - top, 0.004)], NEEDLES, sides=5)  # the spike
    if v["lean"]:
        m = Matrix.Rotation(v["lean"], 4, Vector((1.0, 0.4, 0.0)).normalized())
        for vert in p.bm.verts:
            vert.co = m @ vert.co


def _material(name):
    """Dark brown bark; dark green needles, darker deep in the tiers; snow on the faces looking up, thicker low down, in patches."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    up = g.axis(g.normal_world(), 'Z')
    grain = g.noise(40.0, 3.0)
    bark = g.mix(grain, lin((0.22, 0.16, 0.11)), lin((0.30, 0.22, 0.15)))
    needles = g.mix(g.noise(18.0, 3.0), lin((0.14, 0.30, 0.17)), lin((0.20, 0.38, 0.21)))
    snow_mask = g.clamp01(g.math('MULTIPLY_ADD', up, 5.0, -2.6))
    snow_mask = g.math('MULTIPLY', snow_mask, g.clamp01(g.math('MULTIPLY_ADD', g.noise(9.0, 3.0), 4.0, -1.4)))
    snow_mask = g.math('MULTIPLY', snow_mask, g.clamp01(g.math('MULTIPLY_ADD', z, -0.3, 1.1)))  # a little thicker low down
    needles = g.mix(snow_mask, needles, lin((0.88, 0.90, 0.94)))
    albedo = cc.paint(g, mask, (bark, needles))
    rough = cc.weighted(g, mask, (0.9, 0.85))
    g.finish(albedo, g.bump(g.math('ADD', grain, g.noise(60.0, 2.0)), 0.25, 0.01), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body",))
    body = Parts()
    _tree(body, VARIANTS[variant_id])
    return cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat"))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
