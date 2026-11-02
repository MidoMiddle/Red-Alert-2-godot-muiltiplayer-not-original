"""Slipway and breakwater, elements 15 and 16 of the beach set (docs/beach_elements/15_slipway, 16_breakwater).

Map objects of game/data/map/objects.yaml, made at the size the game draws them:
    slipway  a ramp of ribbed concrete on a cell of the beach, running down into the water beside it (MapObjects.AddSlipways turns it so
             its +Z in Godot, -Y here, faces the water; no spin in the table). Its top edge at the land side of the cell (y +0.5), a
             concrete apron there; the ramp 0.8 wide falls to 0.12 under the water half a cell out (y -1.0). Dark ribs across it; rusty
             steel fenders along both edges with mooring rings, a bollard at each top corner; the concrete darker and green with weed
             where the water reaches. Origin at the centre of the cell on the ground.
             slipway
    breakwater  a heap of rough granite blocks filling its cell of water (to its edges, so the three blocks of a chain the generator
             lays join into one wall along x or along y); the tallest 0.42 over the water, their feet under it. Grey with brown
             stains, a dark wet band and weed at the waterline, pale chipped tops. Origin at the centre of the cell on the water's
             surface (the level the buoy and the reef stand on). Three heaps, each cell picks one; no spin, so the wall keeps its line.
             breakwater_block_01, breakwater_block_02, breakwater_block_03
Node: "body". No states: a map object destroyed is hidden by the game. Budget 1000 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.beach_shore_works as works
    print(works.run_all())
"""

import math
import random

import bmesh
from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"
SIZE = 1024
VARIANTS = ("slipway", "breakwater_block_01", "breakwater_block_02", "breakwater_block_03")

CONCRETE, RIB, STEEL, STONE, BRIGHT = (cc.part(i) for i in range(5))
PARTS = 5

TOP, BOTTOM = (0.5, 0.03), (-1.0, -0.12)  # the ramp's ends: (y, z)


def _ramp_z(y):
    t = (TOP[0] - y) / (TOP[0] - BOTTOM[0])
    return TOP[1] + (BOTTOM[1] - TOP[1]) * t


def _slipway(p):
    w = 0.4
    # The ramp: a slab, its top falling from the land to the water, 0.06 thick.
    top = [Vector((x, y, _ramp_z(y))) for y, x in ((TOP[0], -w), (TOP[0], w), (BOTTOM[0], w), (BOTTOM[0], -w))]
    p.loft([[v - Vector((0, 0, 0.06)) for v in top], top], CONCRETE)
    # The apron at the top, flush with the beach, and the ribs across the ramp.
    p.box((-0.48, 0.32, -0.02), (0.48, 0.5, 0.035), CONCRETE)
    for k in range(9):
        y = 0.28 - k * 0.13
        z = _ramp_z(y)
        p.box((-w + 0.03, y - 0.012, z - 0.01), (w - 0.03, y + 0.012, z + 0.012), RIB)
    # The fenders along the edges, the rings on them, the bollards at the top.
    for x in (-w, w):
        out = 1 if x > 0 else -1
        p.bar((x + out * 0.02, TOP[0] - 0.02, _ramp_z(TOP[0]) + 0.02), (x + out * 0.02, BOTTOM[0] + 0.05, _ramp_z(BOTTOM[0]) + 0.02), 0.022, STEEL)
        for y in (0.05, -0.45):
            c = Vector((x + out * 0.05, y, _ramp_z(y) + 0.03))
            p.turned(tuple(c), 'X', [(-0.006, 0.022), (0.006, 0.022)], STEEL, sides=8)
        p.turned((x * 1.05, 0.42, 0.035), 'Z', [(0.0, 0.03), (0.06, 0.026), (0.075, 0.036), (0.085, 0.03)], STEEL, sides=8)


def _hull(p, points, painted):
    verts = [p.bm.verts.new(q) for q in points]
    made = bmesh.ops.convex_hull(p.bm, input=verts)
    unused = list({v for v in made["geom_interior"] + made["geom_unused"] if isinstance(v, bmesh.types.BMVert)})
    bmesh.ops.delete(p.bm, geom=unused, context='VERTS')
    for f in (f for f in made["geom"] if isinstance(f, bmesh.types.BMFace)):
        for loop in f.loops:
            loop[p.layer] = painted


def _block(p, rnd, centre, size, turn, painted=STONE):
    """A rough block of granite: a box with its corners pushed about, turned about z and tipped a little."""
    cx, cy, cz = centre
    sx, sy, sz = size
    ca, sa = math.cos(turn), math.sin(turn)
    tilt = rnd.uniform(-0.15, 0.15)
    pts = []
    for dx in (-1, 1):
        for dy in (-1, 1):
            for dz in (-1, 1):
                x = dx * sx * rnd.uniform(0.75, 1.0)
                y = dy * sy * rnd.uniform(0.75, 1.0)
                z = dz * sz * rnd.uniform(0.8, 1.0) + x * tilt
                pts.append(Vector((cx + x * ca - y * sa, cy + x * sa + y * ca, cz + z)))
    for k in range(8):  # chips off the edges and the top, so the hull is not a plain box
        a = rnd.uniform(0, 2 * math.pi)
        r = rnd.uniform(0.8, 1.05)
        pts.append(Vector((cx + math.cos(a) * sx * r, cy + math.sin(a) * sy * r, cz + sz * rnd.uniform(0.3, 1.1))))
    _hull(p, pts, painted)


def _breakwater(p, seed):
    rnd = random.Random(seed)
    # The bottom layer: four blocks to the cell's edges, so the neighbours' meet them; a top layer of two or three on them.
    for k, (x, y) in enumerate(((-0.25, -0.25), (0.25, -0.25), (-0.25, 0.25), (0.25, 0.25))):
        _block(p, rnd, (x + rnd.uniform(-0.03, 0.03), y + rnd.uniform(-0.03, 0.03), 0.0),
               (rnd.uniform(0.24, 0.29), rnd.uniform(0.24, 0.29), rnd.uniform(0.12, 0.16)), rnd.uniform(-0.35, 0.35))
    for k in range(rnd.choice((2, 3))):
        a = 2 * math.pi * k / 3 + rnd.uniform(0, 1.0)
        _block(p, rnd, (math.cos(a) * 0.14, math.sin(a) * 0.14, 0.24),
               (rnd.uniform(0.15, 0.2), rnd.uniform(0.13, 0.18), rnd.uniform(0.1, 0.14)), rnd.uniform(0, math.pi), BRIGHT if k == 0 else STONE)


def _material(name):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    y, z = g.axis(coord, 'Y'), g.axis(coord, 'Z')
    up = g.axis(g.normal_world(), 'Z')
    grain = g.noise(40.0, 4.0)
    wet = g.clamp01(g.math('MULTIPLY_ADD', z, -14.0, 1.0))  # under 0.07 the water reaches: dark, with weed
    weed = g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 3.0), 4.0, -1.8))
    concrete = g.mix(g.noise(6.0, 3.0), lin((0.62, 0.61, 0.58)), lin((0.72, 0.71, 0.68)))
    soaked = g.clamp01(g.math('MULTIPLY_ADD', y, -4.0, -1.2))  # the ramp's end out over the water, from y -0.3 down
    concrete = g.mix(soaked, concrete, g.mix(weed, lin((0.32, 0.33, 0.30)), lin((0.22, 0.30, 0.18))))
    rib = g.mix(soaked, lin((0.40, 0.40, 0.39)), lin((0.24, 0.26, 0.22)))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(15.0, 4.0), 4.0, -1.8))
    steel = g.mix(rust, lin((0.30, 0.31, 0.32)), lin((0.46, 0.26, 0.14)))
    stone = g.mix(g.noise(5.0, 4.0), lin((0.40, 0.40, 0.40)), lin((0.52, 0.51, 0.49)))
    stone = g.mix(g.math('MULTIPLY', g.noise(30.0, 4.0), 0.5), stone, lin((0.30, 0.30, 0.31)))  # the grain of the granite
    stone = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, -3.2)), 0.6), stone, lin((0.50, 0.42, 0.32)))  # brown stains
    stone = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', up, 4.0, -2.8)), 0.3), stone, lin((0.66, 0.66, 0.64)))  # pale tops
    stone = g.mix(wet, stone, g.mix(weed, lin((0.26, 0.27, 0.26)), lin((0.20, 0.28, 0.16))))
    bright = g.mix(g.noise(5.0, 4.0), lin((0.52, 0.51, 0.48)), lin((0.60, 0.59, 0.56)))
    bright = g.mix(wet, bright, lin((0.28, 0.29, 0.27)))
    albedo = cc.paint(g, mask, (concrete, rib, steel, stone, bright))
    rough = cc.weighted(g, mask, (0.9, 0.85, 0.6, 0.85, 0.85))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.5, 0.0, 0.0))
    g.finish(albedo, g.bump(g.math('ADD', grain, g.noise(10.0, 3.0)), 0.5, 0.015), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body",))
    body = Parts()
    if variant_id == "slipway":
        _slipway(body)
    else:
        _breakwater(body, variant_id)
    meshes, mat, graph = cc.finish(variant_id, [(body, "body")], -1.0, _material(variant_id + "_mat"))
    return meshes, mat, graph


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
