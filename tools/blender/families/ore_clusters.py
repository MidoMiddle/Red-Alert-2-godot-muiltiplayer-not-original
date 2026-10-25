"""Ore and gem clusters: ore_cluster (gold stones) and gem_cluster (blue crystals).

docs/map_elements/06_ore/prompts.md, sections 1, 3 and 4: one cell footprint, origin at the cell
centre on the ground, height about half a cell, under 1000 triangles, one node "body", 512 textures
(small object, generation_requirements.md section 4). The game places several ore_cluster
instances in one cell by density; the other gem colours are material variants made in Godot, so
only the blue gem model is made here.

Ore is gold-bearing stone: pebbles and nuggets, rounded and lumpy, with gold patches in grey rock.
Gems are six-sided crystals with a pointed tip, leaning outwards from the centre of the cell.
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

SIZE = 512

SPECS = {
    "ore_cluster": dict(seed=31, stones=8, reach=0.26, radius=(0.09, 0.16),
                        squash=(0.6, 0.85), elong=(1.0, 1.25)),
    "gem_cluster": dict(seed=23, count=5, reach=0.26, length=(0.28, 0.50),
                        width=(0.06, 0.10), tilt=(5.0, 22.0)),
}


def _base(bm, rnd):
    """Dark soil under the cluster: a flat, slightly irregular disc."""
    sides = 10
    bottom, top = [], []
    for i in range(sides):
        a = 2 * math.pi * i / sides
        r = 0.33 * rnd.uniform(0.9, 1.05)
        bottom.append(bm.verts.new((r * math.cos(a), r * math.sin(a), 0.0)))
        top.append(bm.verts.new((0.92 * r * math.cos(a), 0.92 * r * math.sin(a), 0.03)))
    centre = bm.verts.new((0.0, 0.0, 0.03))
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
        bm.faces.new((top[i], top[j], centre))
    bm.faces.new(bottom)


def _stone(bm, rnd, spec):
    """One pebble or nugget: a noisy, squashed icosphere. Stones near the centre sit higher, as a pile."""
    radius = rnd.uniform(*spec["radius"])
    squash = rnd.uniform(*spec["squash"])
    elong = rnd.uniform(*spec["elong"])
    reach = spec["reach"] * math.sqrt(rnd.random())
    theta = rnd.uniform(0.0, 2 * math.pi)
    lift = 0.45 * (1.0 - reach / spec["reach"])
    centre = Vector((reach * math.cos(theta), reach * math.sin(theta),
                     radius * squash * 0.45 + lift))
    turn = Matrix.Rotation(rnd.uniform(0.0, 2 * math.pi), 3, 'Z')
    made = bmesh.ops.create_icosphere(bm, subdivisions=2, radius=1.0)
    for v in made["verts"]:
        d = v.co.normalized()
        k = radius * (1.0 + rnd.uniform(-0.14, 0.14))
        v.co = centre + turn @ Vector((d.x * elong * k, d.y * k, d.z * squash * k))


def _crystal(bm, rnd, spec):
    """One six-sided crystal with a pointed tip, standing near the centre of the cell."""
    length = rnd.uniform(*spec["length"])
    width = rnd.uniform(*spec["width"])
    reach = spec["reach"] * math.sqrt(rnd.random())
    theta = rnd.uniform(0.0, 2 * math.pi)
    tilt = math.radians(rnd.uniform(*spec["tilt"]))
    lean = theta + rnd.uniform(-0.3, 0.3)  # outwards from the centre, a little at random
    place = Matrix.Translation((reach * math.cos(theta), reach * math.sin(theta), -0.01))
    m = place @ Matrix.Rotation(lean, 4, 'Z') @ Matrix.Rotation(tilt, 4, 'Y')
    top_z = 0.72 * length
    bottom, top = [], []
    for i in range(6):
        a = 2 * math.pi * i / 6 + rnd.uniform(-0.12, 0.12)
        r0 = width * rnd.uniform(0.92, 1.08)
        r1 = width * rnd.uniform(0.82, 0.98)
        bottom.append(bm.verts.new(m @ Vector((r0 * math.cos(a), r0 * math.sin(a), 0.0))))
        top.append(bm.verts.new(m @ Vector((r1 * math.cos(a), r1 * math.sin(a), top_z))))
    tip = bm.verts.new(m @ Vector((rnd.uniform(-0.1, 0.1) * width,
                                   rnd.uniform(-0.1, 0.1) * width, length)))
    for i in range(6):
        j = (i + 1) % 6
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
        bm.faces.new((top[i], top[j], tip))
    bm.faces.new(bottom)


def _gold_material(mat_name):
    """Gold patches in grey rock on dark soil. The soil is picked by height in object space."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    z = g.axis(g.object_coord(), 'Z')
    soil = g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 2.4))  # 1 up to the top of the base, 0 from z = 0.06
    grain = g.noise(16.0, 4.0)
    rock = g.clamp01(g.math('MULTIPLY_ADD', grain, 2.0, -0.7))  # grey rock where the noise is high
    gold = g.mix(grain, (0.92, 0.70, 0.26), (1.0, 0.82, 0.42))
    rock_col = g.mix(grain, (0.30, 0.28, 0.25), (0.42, 0.40, 0.37))
    stone = g.mix(rock, gold, rock_col)
    albedo = g.mix(soil, stone, (0.16, 0.13, 0.10))
    rough = g.math('MULTIPLY_ADD', rock, 0.5, 0.35)  # gold 0.35, rock 0.85
    metal = g.math('MULTIPLY', g.math('MULTIPLY_ADD', rock, -0.45, 0.45),
                   g.math('MULTIPLY_ADD', soil, -1.0, 1.0))  # gold is half metal, so it keeps its colour without reflections
    normal = g.bump(g.noise(45.0, 3.0), 0.08, 0.01)
    orm = g.combine(ao, rough, metal)
    g.finish(albedo, normal, orm)
    return mat, g


def _gem_material(mat_name):
    """Glossy blue gems on dark soil. Gems are not metal."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.08)
    z = g.axis(g.object_coord(), 'Z')
    soil = g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 2.4))
    grain = g.noise(18.0, 4.0)
    gem = g.mix(grain, (0.06, 0.20, 0.70), (0.16, 0.40, 0.95))
    albedo = g.mix(soil, gem, (0.16, 0.13, 0.10))
    rough = g.math('MULTIPLY_ADD', soil, 0.82, 0.08)  # gem 0.08 (glossy), soil 0.9
    normal = g.bump(g.noise(45.0, 3.0), 0.02, 0.01)
    orm = g.combine(ao, rough, 0.0)
    g.finish(albedo, normal, orm)
    return mat, g


def build(name):
    spec = SPECS[name]
    rnd = random.Random(spec["seed"])
    bm = bmesh.new()
    _base(bm, rnd)
    if name == "ore_cluster":
        for _ in range(spec["stones"]):
            _stone(bm, rnd, spec)
        mat, graph = _gold_material(name + "_mat")
    else:
        for _ in range(spec["count"]):
            _crystal(bm, rnd, spec)
        mat, graph = _gem_material(name + "_mat")
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    obj = rc.make_object(name + "_body", bm)
    rc.uv_smart(obj)
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
