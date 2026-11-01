"""Small things of every map: the boulder and the lamp post (docs/map_elements/09_props), the stone wall (08_fences_walls) and the barbed
wire (docs/obstacle_elements/01_barbed_wire).

Map objects of game/data/map/objects.yaml, each on one cell, made at the size the game draws a thing on a cell, as its placeholder.
Origin at the centre of the cell on the ground. A wall and the wire are sections a cell long, made along x and joining their neighbours
at the cell's edges; the game turns a section whose neighbours run along z (MapObjects.AddLines). One GLB per kind:
    prop_boulder            a big grey-brown rock and two small ones at its foot, angular facets (convex hulls), lichen low down
    prop_boulder_snow       the same with snow on its top (the winter maps' boulder: object/boulder@winter)
    prop_lamp_post          a dark green cast-iron street lamp, 0.72 tall: a fluted base, a slim post with a collar, a curved arm and a
                            lantern head with warm glass (the game may light it; node "lamp")
    prop_wall_stone         a dry-stone wall of field stones, 0.42 tall, a coping of flat stones along its top, a few stones fallen at its
                            foot
    prop_wall_stone_snow    the same with snow along its coping (object/wall_stone@winter)
    barbed_wire_section     three crooked wooden stakes with crossed feet and the wire strung between them: two straight strands and a coil
                            of concertina wire along them, its barbs as short ticks
Node "body" (and "lamp" for the lamp post). Budget 1000 (boulder), 1500 (lamp), 500 (wall, wire); texture 512.

In Blender, after tools/blender is on sys.path:
    import families.map_props as props
    print(props.run_all())
"""

import math
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "map"
SIZE = 512
VARIANTS = ("prop_boulder", "prop_boulder_snow", "prop_lamp_post", "prop_wall_stone", "prop_wall_stone_snow", "barbed_wire_section")

(STONE, IRON, GLASS, WOOD, WIRE, RUST, CORE) = (cc.part(i) for i in range(7))
PARTS = 7


def _hull(p, points, painted=STONE):
    verts = [p.bm.verts.new(q) for q in points]
    made = bmesh.ops.convex_hull(p.bm, input=verts)
    unused = list({v for v in made["geom_interior"] + made["geom_unused"] if isinstance(v, bmesh.types.BMVert)})
    bmesh.ops.delete(p.bm, geom=unused, context='VERTS')
    for f in (f for f in made["geom"] if isinstance(f, bmesh.types.BMFace)):
        for loop in f.loops:
            loop[p.layer] = painted


def _stone(p, rnd, centre, size, flat=1.0, sink=0.03, points=14):
    """An angular stone: a hull of jittered points round a squashed ball, its foot sunk a little."""
    cx, cy, cz = centre
    sx, sy, sz = size
    pts = []
    for k in range(points):
        a, b = rnd.uniform(0, 2 * math.pi), math.acos(rnd.uniform(-1, 1))
        r = rnd.uniform(0.75, 1.0)
        pts.append(Vector((cx + math.cos(a) * math.sin(b) * sx * r, cy + math.sin(a) * math.sin(b) * sy * r,
                           max(cz - sink, cz + math.cos(b) * sz * r * flat))))
    _hull(p, pts)


def _boulder(p, rnd):
    _stone(p, rnd, (0.0, 0.0, 0.2), (0.3, 0.26, 0.22), points=24)
    _stone(p, rnd, (0.24, -0.2, 0.05), (0.1, 0.09, 0.07))
    _stone(p, rnd, (-0.26, 0.16, 0.04), (0.08, 0.07, 0.05))


def _lamp(p, lamp):
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.05), (0.04, 0.045), (0.06, 0.03), (0.1, 0.02)], IRON, sides=8)  # the fluted base
    p.cylinder((0.0, 0.0, 0.38), 'Z', 0.012, 0.28, IRON, sides=6)
    p.cylinder((0.0, 0.0, 0.4), 'Z', 0.02, 0.012, IRON, sides=6)  # the collar
    arm = [Vector((0.0, 0.0, 0.64)), Vector((0.0, 0.03, 0.7)), Vector((0.0, 0.08, 0.72)), Vector((0.0, 0.13, 0.7))]
    for a, b in zip(arm, arm[1:]):
        p.bar(a, b, 0.008, IRON)
    head = Vector((0.0, 0.13, 0.66))
    lamp.turned(head, 'Z', [(-0.005, 0.02), (0.03, 0.038), (0.035, 0.038)], GLASS, sides=6)  # the glass, under the cap
    p.turned(head, 'Z', [(0.035, 0.045), (0.05, 0.03), (0.06, 0.008)], IRON, sides=6)  # the cap


def _block(p, rnd, centre, size, jitter=0.25):
    """A field stone laid in a wall: a hull of a box's eight corners, each moved a little, so it is blocky but not square."""
    cx, cy, cz = centre
    sx, sy, sz = size
    pts = [Vector((cx + dx * sx * rnd.uniform(1 - jitter, 1.0), cy + dy * sy * rnd.uniform(1 - jitter, 1.0), cz + dz * sz * rnd.uniform(1 - jitter, 1.0)))
           for dx in (-1, 1) for dy in (-1, 1) for dz in (-1, 1)]
    _hull(p, pts)


def _wall(p, rnd):
    """Courses of field stones along x, the cell's length, laid tight over a dark core (the gaps between them), a coping of flat stones
    on top, one fallen at its foot."""
    p.box((-0.5, -0.07, 0.0), (0.5, 0.07, 0.34), CORE)  # the core, dark between the stones
    for row, (z, h) in enumerate(((0.06, 0.06), (0.17, 0.055), (0.275, 0.05))):
        x = -0.5 - (0.06 if row % 2 else 0.0)
        while x < 0.5:
            w = rnd.uniform(0.13, 0.2)
            x0, x1 = max(x, -0.5), min(x + w, 0.5)
            if x1 - x0 > 0.03:
                _block(p, rnd, ((x0 + x1) / 2, 0.0, z), ((x1 - x0) / 2 - 0.006, 0.1 - 0.01 * row, h))
            x += w
    x = -0.5
    while x < 0.5:
        w = rnd.uniform(0.17, 0.25)
        x1 = min(x + w, 0.5)
        _block(p, rnd, ((x + x1) / 2, 0.0, 0.36), ((x1 - x) / 2 - 0.005, 0.11, 0.03), jitter=0.15)
        x += w
    _block(p, rnd, (0.2, -0.17, 0.035), (0.06, 0.05, 0.035))


def _wire(p, rnd):
    for x in (-0.36, 0.0, 0.36):
        top = Vector((x + rnd.uniform(-0.02, 0.02), rnd.uniform(-0.02, 0.02), 0.42))
        p.bar((x, 0.0, 0.0), top, 0.014, WOOD)
        p.bar((x - 0.05, -0.08, 0.0), (x + 0.03, 0.06, 0.28), 0.01, WOOD)  # the crossed foot
    for z in (0.18, 0.36):  # two straight strands, the cell's length
        p.bar((-0.5, 0.0, z), (0.5, 0.0, z - 0.01), 0.004, WIRE)
    # The concertina along them: a helix of short straight lengths, its barbs as ticks out of it.
    turns, steps = 4, 28
    pts = [Vector((-0.5 + t / steps, 0.12 * math.sin(2 * math.pi * turns * t / steps), 0.2 + 0.12 * math.cos(2 * math.pi * turns * t / steps)))
           for t in range(steps + 1)]
    for k, (a, b) in enumerate(zip(pts, pts[1:])):
        p.bar(a, b, 0.004, RUST if k % 5 == 0 else WIRE)
    for a in pts[1:-1:5]:
        p.bar(a, a + Vector((0.0, 0.0, 0.025)), 0.003, WIRE)


def _material(name, snow):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    up = g.axis(g.normal_world(), 'Z')
    grain = g.noise(24.0, 4.0)
    stone = g.mix(g.noise(5.0, 3.0), lin((0.44, 0.42, 0.38)), lin((0.58, 0.55, 0.50)))
    stone = g.mix(g.math('MULTIPLY', grain, 0.35), stone, lin((0.36, 0.32, 0.27)))  # browner lumps
    lichen = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -6.0, 1.0)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(12.0, 3.0), 5.0, -2.8)))
    stone = g.mix(lichen, stone, lin((0.38, 0.44, 0.24)))
    if snow:
        cover = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', up, 4.0, -2.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(8.0, 3.0), 3.0, -0.6)))
        stone = g.mix(cover, stone, lin((0.90, 0.92, 0.95)))
    iron = g.mix(grain, lin((0.16, 0.26, 0.19)), lin((0.21, 0.32, 0.24)))
    glass = lin((1.0, 0.88, 0.58))
    wood = g.mix(grain, lin((0.30, 0.23, 0.16)), lin((0.40, 0.31, 0.22)))
    wire = lin((0.48, 0.49, 0.50))
    rust = lin((0.46, 0.27, 0.14))
    core = lin((0.16, 0.15, 0.13))
    albedo = cc.paint(g, mask, (stone, iron, glass, wood, wire, rust, core))
    rough = cc.weighted(g, mask, (0.9, 0.5, 0.2, 0.85, 0.45, 0.8, 0.95))
    metallic = cc.weighted(g, mask, (0.0, 0.6, 0.0, 0.0, 0.8, 0.3, 0.0))
    g.finish(albedo, g.bump(g.math('ADD', grain, g.noise(60.0, 2.0)), 0.3, 0.01), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    ic.clear(variant_id, ("body", "lamp"))
    rnd = random.Random(variant_id.replace("_snow", ""))
    body = Parts()
    parts = [(body, "body")]
    if variant_id.startswith("prop_boulder"):
        _boulder(body, rnd)
    elif variant_id == "prop_lamp_post":
        lamp = Parts()
        _lamp(body, lamp)
        parts.append((lamp, "lamp"))
    elif variant_id.startswith("prop_wall_stone"):
        _wall(body, rnd)
    else:
        _wire(body, rnd)
    meshes, mat, graph = cc.finish(variant_id, parts, -1.0, _material(variant_id + "_mat", variant_id.endswith("_snow")))
    if variant_id.startswith(("prop_boulder", "prop_wall")):
        rc.select_only(meshes)
        bpy.ops.object.shade_smooth_by_angle(angle=math.radians(12))  # the stones' facets stay sharp
    return meshes, mat, graph


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
