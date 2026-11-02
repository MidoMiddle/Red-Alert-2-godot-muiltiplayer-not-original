"""The rocks of the desert (docs/desert_elements, 02_rock_mesa and 09_rock_arch): a mesa and a natural arch of layered sandstone.

Map buildings of game/data/neutral/civilian.yaml that stop everybody: neutral_rock_mesa (2x2, 4000 hp) and neutral_rock_arch (2x1, 3000
hp). Made at the size the game draws them, not squashed (rock is no storey of a building): the mesa to 1.05, the arch to 1.6. Origin at
the centre of the footprint on the ground. The rock is stacked slabs, each a hull of a jittered ring, a little narrower and offset from
the one under it, so its sides step in ledges; the material bands it by height in ochre, red-brown and grey-yellow, its top bleached,
scree and dark sand round its foot. Node "body". One GLB per state:
    rock_mesa             a flat-topped butte, wider than tall: a skirt of scree and sheer cliffs in three bands, a talus of boulders
    rock_mesa_damaged     the same with a slab split off its top tier and fallen at its foot, cracks
    rock_mesa_ruins       broken down to its two lower tiers, the upper ones in blocks on the ground
    rock_arch             two thick legs and a flat lintel over a span of about a cell, along x
    rock_arch_damaged     the lintel cracked, chips of it at the foot
    rock_arch_ruins       the lintel fallen between the legs, one leg broken low
Budget 6000 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.desert_rocks as rocks
    print(rocks.run_all())
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

FOLDER = "desert"
SIZE = 1024
VARIANTS = ("rock_mesa", "rock_mesa_damaged", "rock_mesa_ruins", "rock_arch", "rock_arch_damaged", "rock_arch_ruins")

(ROCK, FRESH) = (cc.part(i) for i in range(2))
PARTS = 2


def _hull(p, points, painted=ROCK):
    verts = [p.bm.verts.new(q) for q in points]
    made = bmesh.ops.convex_hull(p.bm, input=verts)
    unused = list({v for v in made["geom_interior"] + made["geom_unused"] if isinstance(v, bmesh.types.BMVert)})
    bmesh.ops.delete(p.bm, geom=unused, context='VERTS')
    for f in (f for f in made["geom"] if isinstance(f, bmesh.types.BMFace)):
        for loop in f.loops:
            loop[p.layer] = painted


def _slab(p, rnd, centre, rx, ry, z0, z1, sides=9, painted=ROCK):
    """A slab of rock: two jittered rings, its sides near upright."""
    cx, cy = centre
    a0 = rnd.uniform(0, 2 * math.pi)
    pts = []
    for z, shrink in ((z0, 1.0), (z1, rnd.uniform(0.9, 0.97))):
        for k in range(sides):
            a = a0 + 2 * math.pi * k / sides
            r = rnd.uniform(0.85, 1.1) * shrink
            pts.append(Vector((cx + math.cos(a) * rx * r, cy + math.sin(a) * ry * r, z + rnd.uniform(-0.015, 0.015))))
    _hull(p, pts, painted)


def _block(p, rnd, centre, size, painted=ROCK):
    cx, cy, cz = centre
    sx, sy, sz = size
    _hull(p, [Vector((cx + dx * sx * rnd.uniform(0.7, 1.0), cy + dy * sy * rnd.uniform(0.7, 1.0), max(0.0, cz + dz * sz * rnd.uniform(0.7, 1.0))))
              for dx in (-1, 1) for dy in (-1, 1) for dz in (-1, 1)], painted)


def _talus(p, rnd, rx, ry, count):
    for k in range(count):
        a = 2 * math.pi * k / count + rnd.uniform(-0.2, 0.2)
        r = rnd.uniform(1.0, 1.15)
        s = rnd.uniform(0.05, 0.1)
        _block(p, rnd, (math.cos(a) * rx * r, math.sin(a) * ry * r, s * 0.6), (s, s * 0.8, s * 0.6))


def _mesa(p, rnd, state):
    # A skirt of scree, then sheer cliffs in three bands, each only a little narrower: wider than tall, its top flat and broad.
    tiers = ((0.0, 0.16, 0.9, 0.86), (0.16, 0.5, 0.82, 0.78), (0.5, 0.82, 0.78, 0.74), (0.82, 1.05, 0.74, 0.7))
    keep = 2 if state == "ruins" else 4
    off = Vector((0.0, 0.0))
    for k, (z0, z1, rx, ry) in enumerate(tiers[:keep]):
        off += Vector((rnd.uniform(-0.03, 0.03), rnd.uniform(-0.03, 0.03)))
        if state == "damaged" and k == 3:
            _slab(p, rnd, (off.x - 0.18, off.y), rx * 0.62, ry, z0, z1)  # what is left of the top tier
            _block(p, rnd, (0.72, -0.3, 0.12), (0.2, 0.16, 0.12), FRESH)  # the slab split off it, at the foot
            continue
        _slab(p, rnd, tuple(off), rx, ry, z0, z1)
    if state == "ruins":
        for k, c in enumerate(((0.6, 0.55), (-0.65, 0.4), (0.5, -0.65), (-0.4, -0.7))):
            _block(p, rnd, (c[0], c[1], 0.12), (0.2, 0.16, 0.12), FRESH if k % 2 else ROCK)
    _talus(p, rnd, 0.86, 0.82, 12)


def _arch(p, rnd, state):
    # The legs along x at ±0.62, the lintel over them; the footprint 2x1 is x in ±1, y in ±0.5.
    for k, x in enumerate((-0.62, 0.62)):
        top = 0.45 if (state == "ruins" and k == 1) else 1.2
        _slab(p, rnd, (x, 0.0), 0.3, 0.32, 0.0, top * 0.5)
        _slab(p, rnd, (x + 0.02 * (1 if x < 0 else -1), 0.0), 0.26, 0.28, top * 0.5, top)
    if state == "ruins":
        _block(p, rnd, (0.0, 0.05, 0.16), (0.55, 0.26, 0.16), ROCK)  # the lintel fallen between the legs
        _block(p, rnd, (0.82, -0.3, 0.1), (0.14, 0.12, 0.1), FRESH)
    else:
        _slab(p, rnd, (0.0, 0.0), 0.95, 0.3, 1.18, 1.42)
        _slab(p, rnd, (0.05, 0.0), 0.7, 0.26, 1.42, 1.6)
        if state == "damaged":
            for k in range(4):
                _block(p, rnd, (rnd.uniform(-0.3, 0.3), rnd.uniform(-0.3, 0.3), 0.05), (0.07, 0.06, 0.05), FRESH)
    _talus(p, rnd, 0.95, 0.42, 8)


def _material(name, state):
    """Bands of ochre, red-brown and grey-yellow by height; the top bleached; dark sand and scree low; fresh breaks paler; cracks."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    up = g.axis(g.normal_world(), 'Z')
    wobble = g.math('ADD', z, g.math('MULTIPLY', g.noise(3.0, 2.0), 0.08))
    band = g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', wobble, 21.0)), 0.5, 0.5)
    band2 = g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', wobble, 47.0)), 0.5, 0.5)
    rock = g.mix(band, lin((0.70, 0.42, 0.24)), lin((0.78, 0.58, 0.34)))
    rock = g.mix(g.math('MULTIPLY', band2, 0.5), rock, lin((0.74, 0.66, 0.46)))
    rock = g.mix(g.math('MULTIPLY', g.noise(14.0, 4.0), 0.35), rock, lin((0.52, 0.32, 0.20)))
    top = g.clamp01(g.math('MULTIPLY_ADD', up, 5.0, -3.8))
    rock = g.mix(g.math('MULTIPLY', top, 0.6), rock, lin((0.88, 0.76, 0.56)))  # bleached on top
    low = g.clamp01(g.math('MULTIPLY_ADD', z, -6.0, 1.0))
    rock = g.mix(g.math('MULTIPLY', low, 0.5), rock, lin((0.46, 0.36, 0.26)))
    fresh = g.mix(g.noise(20.0, 3.0), lin((0.84, 0.66, 0.46)), lin((0.90, 0.74, 0.54)))
    albedo = cc.paint(g, mask, (rock, fresh))
    if state != "intact":
        albedo = g.mix(g.math('MULTIPLY', mask[0], cc.cracks(g, 4.0)), albedo, lin((0.22, 0.14, 0.10)))
    height = g.math('ADD', g.math('MULTIPLY', band, 0.6), g.noise(30.0, 4.0))
    g.finish(albedo, g.bump(height, 0.35, 0.02), g.combine(ao, 0.9, 0.0))
    return mat, g


def build(variant_id):
    state = "damaged" if variant_id.endswith("_damaged") else "ruins" if variant_id.endswith("_ruins") else "intact"
    ic.clear(variant_id, ("body",))
    rnd = random.Random(variant_id.split("_damaged")[0].split("_ruins")[0])
    body = Parts()
    (_mesa if variant_id.startswith("rock_mesa") else _arch)(body, rnd, state)
    meshes, mat, graph = cc.finish(variant_id, [(body, "body")], -1.0, _material(variant_id + "_mat", state))
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(15))  # the ledges stay sharp
    return meshes, mat, graph


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
