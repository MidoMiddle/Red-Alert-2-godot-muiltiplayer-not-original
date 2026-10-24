"""Fences and neutral walls: fence_wood_section, fence_mesh_section, wall_stone_section.

docs/map_elements/08_fences_walls/prompts.md, section 1: each section is one cell long,
origin at the section centre on the ground, one node "body", at most 500 triangles,
512 textures (small object, generation_requirements.md section 4). Corners, ends and turns
are made later from the same section, so they are not part of this family.

In Blender, after rts_common is on sys.path:
    import families.fences_walls as fw
    print(fw.run("fence_wood_section"))
"""

import os
import random

import bmesh
from mathutils import Vector

import rts_common as rc

SIZE = 512
SNOW = (0.93, 0.94, 0.97)


# --- wooden picket fence: 7 boards, two rails, two posts ---------------------------

def _wood_geometry():
    bm = bmesh.new()
    rnd = random.Random(7)
    for i in range(7):
        cx = -0.45 + 0.15 * i
        top = 0.25 - rnd.uniform(0.0, 0.01)
        rc.add_box(bm, (cx - 0.05, -0.01, 0.0), (cx + 0.05, 0.01, top))
    rc.add_box(bm, (-0.5, -0.03, 0.05), (0.5, -0.01, 0.09))
    rc.add_box(bm, (-0.5, -0.03, 0.17), (0.5, -0.01, 0.21))
    for x in (-0.455, 0.455):
        rc.add_box(bm, (x - 0.025, -0.03, 0.0), (x + 0.025, 0.03, 0.27))
    return bm


def _wood_material():
    mat = rc.new_material("fence_wood_section_wood")
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    grain = g.noise(25.0, 4.0)
    wood = g.mix(grain, (0.20, 0.19, 0.18), (0.36, 0.34, 0.31))
    wood = g.mix(ao, (0.05, 0.05, 0.05), wood)  # contact shadow: ao = 1 keeps the wood colour
    albedo = g.mix(g.snow_mask(0.15, 0.6), wood, SNOW)
    normal = g.bump(g.noise(40.0, 3.0), 0.15, 0.01)
    orm = g.combine(ao, 0.85, 0.0)
    g.finish(albedo, normal, orm)
    return mat, g


# --- steel mesh fence: diamond lattice on two posts, top and bottom wires ----------

def _mesh_geometry():
    bm = bmesh.new()
    height, t = 0.5, 0.008
    xmax, zmin, zmax = 0.47, 0.03, height - 0.03  # lattice ends stay inside the posts and the wires
    for x in (-0.47, 0.47):
        rc.add_box(bm, (x - 0.02, -0.02, 0.0), (x + 0.02, 0.02, height))
    rc.add_box(bm, (-0.5, -t, height - 0.02), (0.5, t, height))
    rc.add_box(bm, (-0.5, -t, 0.0), (0.5, t, 0.03))
    depth = Vector((0.0, t, 0.0))
    for sign in (1, -1):  # wires of slope +1 and -1 in the plane of the fence (x, z)
        for k in range(-12, 13):
            c = 0.12 * k
            if sign > 0:
                lo, hi = max(-xmax, zmin - c), min(xmax, zmax - c)
            else:
                lo, hi = max(-xmax, c - zmax), min(xmax, c - zmin)
            if hi - lo < 0.02:
                continue
            p1 = Vector((lo, 0.0, sign * lo + c))
            p2 = Vector((hi, 0.0, sign * hi + c))
            d = (p2 - p1).normalized()
            across = Vector((-d.z, 0.0, d.x)) * t
            rc.add_prism(bm, p1, p2, across, depth)
    return bm


def _mesh_material():
    mat = rc.new_material("fence_mesh_section_steel")
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    steel = g.mix(ao, (0.10, 0.11, 0.13), (0.42, 0.44, 0.47))
    albedo = g.mix(g.snow_mask(0.02, 0.5), steel, SNOW)  # frost on the upward faces
    normal = g.bump(g.noise(60.0, 2.0), 0.05, 0.01)
    orm = g.combine(ao, 0.45, 0.8)
    g.finish(albedo, normal, orm)
    return mat, g


# --- stone wall: one body with a snow cap on top -----------------------------------

def _stone_geometry():
    bm = bmesh.new()
    rc.add_box(bm, (-0.5, -0.15, 0.0), (0.5, 0.15, 0.56))
    rc.add_box(bm, (-0.5, -0.17, 0.56), (0.5, 0.17, 0.6))
    return bm


def _stone_material():
    mat = rc.new_material("wall_stone_section_stone")
    g = rc.Graph(mat)
    ao = g.ao(0.15)
    coord = g.object_coord()
    brick = g.add('ShaderNodeTexBrick')
    g.put(brick, 'Color1', (0.46, 0.45, 0.43, 1.0))
    g.put(brick, 'Color2', (0.38, 0.37, 0.35, 1.0))
    g.put(brick, 'Mortar', (0.18, 0.18, 0.17, 1.0))
    g.put(brick, 'Scale', 3.0)
    g.put(brick, 'Mortar Size', 0.02)
    g.put(brick, 'Brick Width', 0.5)
    g.put(brick, 'Row Height', 0.25)
    # Vertical walls: rows run along z, the wall's length runs along x + y.
    g.link(g.join(g.math('ADD', g.axis(coord, 'X'), g.axis(coord, 'Y')), g.axis(coord, 'Z'), 0.0),
           brick, 'Vector')
    stone = rc.sock(brick, 'Color', outputs=True)
    wall = g.mix(ao, (0.10, 0.10, 0.10), stone)
    albedo = g.mix(g.snow_mask(0.555, 0.6), wall, SNOW)
    normal = g.bump(rc.sock(brick, 'Fac', outputs=True), 0.6, 0.01)
    orm = g.combine(ao, 0.9, 0.0)
    g.finish(albedo, normal, orm)
    return mat, g


SECTIONS = {
    "fence_wood_section": (_wood_geometry, _wood_material),
    "fence_mesh_section": (_mesh_geometry, _mesh_material),
    "wall_stone_section": (_stone_geometry, _stone_material),
}


def build(name):
    geometry, material = SECTIONS[name]
    bm = geometry()
    obj = rc.make_object(name + "_body", bm)
    rc.uv_smart(obj)
    mat, graph = material()
    obj.data.materials.append(mat)
    return obj, mat, graph


def run(name):
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    obj, mat, graph = build(name)
    base = os.path.join(rc.SRC_ROOT, "map", name)
    report = [f"{name}: triangles {rc.tri_count(obj)}, "
              f"size {tuple(round(v, 3) for v in obj.dimensions)}"]
    imgs = rc.bake_material(obj, graph, SIZE, name, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "map", name, name + ".glb")
    with rc.node_names([obj], ["body"]):
        rc.save_blend([obj], mat, imgs, os.path.join(base, name + ".blend"))
        rc.export_glb([obj], glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
