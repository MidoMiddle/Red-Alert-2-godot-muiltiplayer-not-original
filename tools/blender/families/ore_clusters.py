"""Ore and gem clusters: ore_cluster (gold nuggets) and gem_cluster (gems of many colours).

docs/map_elements/06_ore/prompts.md, sections 1, 3 and 4: one cell footprint, origin at the cell centre on the ground, under 1000
triangles, one node "body", 512 textures (small object, generation_requirements.md section 4). The game puts one cluster on every cell
of ore, sized by the stage (0.45 of the full one at stage 0) and turned by the cell (game/scripts/Terrain/OreField.cs): so a cluster
is not one specimen but a scatter of small pieces over the whole cell and a little past it, the scatters of neighbouring cells
running into one field.
No soil under it: the pieces lie on the ground of the map, a little sunk into it, so that they sit on a slope too.

Ore is small gold nuggets, lumpy and rounded, most of them pebbles and a few bigger, in loose clumps; gold all over, darker in the
hollows. Gems are small six-sided crystals with a pointed tip, alone or two or three from one point, leaning every way, each of its
own colour: red, green, blue, violet, cyan and pink (the owner's choice, 2026-10-30). The colour is written per crystal on the mesh
(the colour attribute GEM) and baked into the texture.
The shapes are ours, not copied from any reference (generation_requirements.md, section 8).

In Blender, after rts_common is on sys.path:
    import families.ore_clusters as oc
    print(oc.run("ore_cluster"))
    print(oc.run("gem_cluster"))
"""

import math
import os
import random

import bmesh
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import lin

SIZE = 512
GEM = "gem"  # the colour attribute of the crystals
REACH = 0.56  # half the side of the square the pieces lie in: past the cell's edge, so that the clusters of neighbouring cells
# overlap into one field at full size, however each is turned

SPECS = {
    "ore_cluster": dict(seed=31, clumps=4, pieces=49, radius=(0.022, 0.045), big=(0.05, 0.07), bigs=5,
                        squash=(0.55, 0.8), elong=(1.0, 1.4)),
    "gem_cluster": dict(seed=23, clumps=4, groups=32, length=(0.07, 0.14), long=(0.15, 0.21), longs=5,
                        width=(0.019, 0.032), tilt=(0.0, 40.0)),
}

# The colours of the gems, sRGB.
GEM_COLOURS = ((0.92, 0.10, 0.14), (0.12, 0.80, 0.28), (0.14, 0.38, 1.0), (0.58, 0.18, 0.95), (0.10, 0.85, 0.92), (1.0, 0.40, 0.72))


def _spot(rnd, clumps):
    """A place in the square of side 2 REACH: mostly anywhere in it, now and then near one of the clumps, so the field is even
    with a few denser spots."""
    while True:
        if rnd.random() < 0.3:
            cx, cy = rnd.choice(clumps)
            r, a = 0.12 * math.sqrt(rnd.random()), rnd.uniform(0.0, 2 * math.pi)
            x, y = cx + r * math.cos(a), cy + r * math.sin(a)
        else:
            x, y = rnd.uniform(-REACH, REACH), rnd.uniform(-REACH, REACH)
        if max(abs(x), abs(y)) <= REACH:
            return x, y


def _clumps(rnd, count):
    return [(r * math.cos(a), r * math.sin(a))
            for r, a in ((0.36 * math.sqrt(rnd.random()), 2 * math.pi * (k + rnd.random() * 0.6) / count) for k in range(count))]


def _nugget(bm, rnd, spec, place, radius):
    """One nugget: a lumpy, squashed and stretched icosahedron, sunk a third into the ground."""
    squash = rnd.uniform(*spec["squash"])
    elong = rnd.uniform(*spec["elong"])
    centre = Vector((place[0], place[1], radius * squash * 0.35))
    turn = Matrix.Rotation(rnd.uniform(0.0, 2 * math.pi), 3, 'Z')
    made = bmesh.ops.create_icosphere(bm, subdivisions=0, radius=1.0)  # 20 triangles: at this size a rounder one shows nothing more
    for v in made["verts"]:
        d = v.co.normalized()
        k = radius * (1.0 + rnd.uniform(-0.2, 0.2))
        v.co = centre + turn @ Vector((d.x * elong * k, d.y * k, d.z * squash * k))


def _crystal(bm, layer, rnd, spec, place, length, colour):
    """One six-sided crystal with a pointed tip, its foot a little in the ground, leaning a random way; no bottom face."""
    width = rnd.uniform(*spec["width"]) * (1.0 + 0.6 * (length - spec["length"][0]) / spec["long"][1])
    tilt = math.radians(rnd.uniform(*spec["tilt"]))
    m = (Matrix.Translation((place[0], place[1], -0.015)) @ Matrix.Rotation(rnd.uniform(0.0, 2 * math.pi), 4, 'Z')
         @ Matrix.Rotation(tilt, 4, 'Y'))
    top_z = 0.7 * length
    bottom, top = [], []
    for i in range(6):
        a = 2 * math.pi * i / 6 + rnd.uniform(-0.1, 0.1)
        r0, r1 = width * rnd.uniform(0.9, 1.1), width * rnd.uniform(0.85, 1.0)
        bottom.append(bm.verts.new(m @ Vector((r0 * math.cos(a), r0 * math.sin(a), 0.0))))
        top.append(bm.verts.new(m @ Vector((r1 * math.cos(a), r1 * math.sin(a), top_z))))
    tip = bm.verts.new(m @ Vector((rnd.uniform(-0.1, 0.1) * width, rnd.uniform(-0.1, 0.1) * width, length)))
    faces = []
    for i in range(6):
        j = (i + 1) % 6
        faces.append(bm.faces.new((bottom[i], bottom[j], top[j], top[i])))
        faces.append(bm.faces.new((top[i], top[j], tip)))
    for f in faces:
        for loop in f.loops:
            loop[layer] = colour


def _ore(bm, rnd, spec):
    clumps = _clumps(rnd, spec["clumps"])
    for k in range(spec["pieces"]):
        radius = rnd.uniform(*spec["big"]) if k < spec["bigs"] else rnd.uniform(*spec["radius"])
        _nugget(bm, rnd, spec, _spot(rnd, clumps), radius)


def _gems(bm, rnd, spec):
    layer = bm.loops.layers.float_color.new(GEM)
    clumps = _clumps(rnd, spec["clumps"])
    for k in range(spec["groups"]):
        place = _spot(rnd, clumps)
        colour = (*lin(GEM_COLOURS[k % len(GEM_COLOURS)]), 1.0)
        for n in range(rnd.choice((1, 1, 1, 2, 3))):  # alone, or two or three from one point, of one colour
            length = rnd.uniform(*spec["long"]) if k < spec["longs"] and n == 0 else rnd.uniform(*spec["length"])
            _crystal(bm, layer, rnd, spec, (place[0] + rnd.uniform(-0.012, 0.012), place[1] + rnd.uniform(-0.012, 0.012)), length, colour)


def _gold_material(mat_name):
    """Gold all over, warmer and lighter in patches, darker in the hollows; half metal, so it keeps its colour without reflections."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    grain = g.noise(40.0, 4.0)
    patches = g.noise(9.0, 2.0)
    gold = g.mix(patches, lin((0.95, 0.68, 0.14)), lin((1.0, 0.86, 0.32)))
    vein = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', grain, 0.5)), -14.0, 1.0))
    albedo = g.mix(g.math('MULTIPLY', vein, 0.5), gold, lin((0.62, 0.40, 0.08)))  # thin darker veins
    rough = g.math('MULTIPLY_ADD', vein, 0.3, 0.3)
    normal = g.bump(g.noise(60.0, 3.0), 0.1, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, 0.55))
    return mat, g


def _gem_material(mat_name):
    """Each crystal its colour (the attribute GEM), lighter towards its tip and in streaks; glossy, not metal."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    attr = g.add('ShaderNodeAttribute', attribute_name=GEM)
    colour = rc.sock(attr, 'Color', outputs=True)
    z = g.axis(g.object_coord(), 'Z')
    light = g.clamp01(g.math('MULTIPLY_ADD', z, 4.0, g.math('MULTIPLY', g.noise(25.0, 3.0), 0.35)))
    albedo = g.mix(g.math('MULTIPLY', light, 0.45), colour, lin((0.95, 0.95, 1.0)))
    normal = g.bump(g.noise(50.0, 2.0), 0.02, 0.004)
    g.finish(albedo, normal, g.combine(ao, 0.08, 0.0))
    return mat, g


def build(name):
    cc.clear(name, ("body",))
    spec = SPECS[name]
    rnd = random.Random(spec["seed"])
    bm = bmesh.new()
    if name == "ore_cluster":
        _ore(bm, rnd, spec)
        mat, graph = _gold_material(name + "_mat")
    else:
        _gems(bm, rnd, spec)
        mat, graph = _gem_material(name + "_mat")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    obj = rc.make_object(name + "_body", bm, smooth=name == "ore_cluster")
    rc.uv_smart(obj)
    cc.repack([obj], 4.0 / SIZE)  # a gap between the islands, or one gem's colour bleeds into the next
    obj.data.materials.append(mat)
    return obj, mat, graph


def run(name):
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    obj, mat, graph = build(name)
    rc.arrange([obj], name)
    base = os.path.join(rc.SRC_ROOT, "map", name)
    report = [f"{name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"]
    imgs = rc.bake_material(obj, graph, SIZE, name, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "map", name, name + ".glb")
    with rc.node_names([obj], ["body"]):
        rc.save_blend([obj], mat, imgs, os.path.join(base, name + ".blend"))
        rc.export_glb([obj], glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
