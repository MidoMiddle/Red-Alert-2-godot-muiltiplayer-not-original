"""Low bridge over water: bridge_span and bridge_span_damaged (docs/map_elements/05_bridges/prompts.md).

One cell footprint, the span runs along X, origin at the cell centre, the bottom of the piers at the
water line (z = 0). One node "body" each, at most 3000 triangles, 512 textures (1x1 footprint,
generation_requirements.md section 4). The game turns the span for a bridge that runs along Y.

The look follows the docs: concrete piers, a wooden deck with snow on it, low metal rails with frost.
The damaged span has a gap in the deck, twisted beams sticking out and debris in the water.
Ramps to the banks and the broken ends are variants made later in Blender, not part of this family.

In Blender, after rts_common is on sys.path:
    import families.bridge as br
    print(br.run("bridge_span"))
    print(br.run("bridge_span_damaged"))
"""

import os

import bmesh
from mathutils import Vector

import rts_common as rc

SIZE = 512


def _piers(bm):
    for x in (-0.38, 0.38):
        rc.add_box(bm, (x - 0.08, -0.28, 0.0), (x + 0.08, 0.28, 0.08))


def _rails(bm, x0, x1, posts):
    """Two low rails with posts on both sides of the deck, from x0 to x1."""
    for s in (-1.0, 1.0):
        y = s * 0.41
        rc.add_box(bm, (x0, y - 0.02, 0.20), (x1, y + 0.02, 0.22))  # top rail
        rc.add_box(bm, (x0, y - 0.02, 0.16), (x1, y + 0.02, 0.18))  # bottom rail
        for x in posts:
            rc.add_box(bm, (x - 0.015, y - 0.02, 0.16), (x + 0.015, y + 0.02, 0.23))


def _beam(bm, p1, p2, half=0.018):
    """A bent steel beam from p1 to p2: a square section that stays perpendicular to its length."""
    d = (p2 - p1).normalized()
    a = d.cross(Vector((0.0, 0.0, 1.0)))
    if a.length < 1e-6:
        a = d.cross(Vector((1.0, 0.0, 0.0)))
    a = a.normalized() * half
    b = d.cross(a).normalized() * half
    rc.add_prism(bm, p1, p2, a, b)


def _span_geometry():
    bm = bmesh.new()
    rc.add_box(bm, (-0.5, -0.45, 0.12), (0.5, 0.45, 0.16))  # deck
    rc.add_box(bm, (-0.5, -0.36, 0.08), (0.5, 0.36, 0.12))  # girder under the deck
    _piers(bm)
    _rails(bm, -0.5, 0.5, posts=(-0.47, 0.0, 0.47))
    return bm


def _span_damaged_geometry():
    bm = bmesh.new()
    rc.add_box(bm, (-0.5, -0.45, 0.12), (-0.18, 0.45, 0.16))  # left half of the deck
    rc.add_box(bm, (0.22, -0.45, 0.12), (0.5, 0.45, 0.16))    # right half
    rc.add_box(bm, (-0.5, -0.36, 0.08), (-0.18, 0.36, 0.12))  # left girder
    rc.add_box(bm, (0.22, -0.36, 0.08), (0.5, 0.36, 0.12))    # right girder
    _piers(bm)
    _rails(bm, -0.5, -0.18, posts=(-0.47, -0.2))
    _rails(bm, 0.22, 0.5, posts=(0.24, 0.47))
    _beam(bm, Vector((-0.18, -0.2, 0.14)), Vector((-0.06, -0.2, 0.34)))
    _beam(bm, Vector((-0.18, 0.12, 0.14)), Vector((-0.02, 0.12, 0.02)))
    _beam(bm, Vector((0.22, 0.25, 0.14)), Vector((0.10, 0.25, 0.36)))
    _beam(bm, Vector((0.22, -0.30, 0.14)), Vector((0.12, -0.30, -0.02)))
    for x, y in ((-0.05, 0.30), (0.02, -0.25), (0.08, 0.05), (-0.12, -0.05)):
        rc.add_box(bm, (x - 0.04, y - 0.03, -0.02), (x + 0.04, y + 0.03, 0.03))  # debris in the water
    return bm


def _material(mat_name):
    """Wooden deck, concrete piers and girders, steel rails; snow on the upward faces above the deck."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.12)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    y = g.axis(coord, 'Y')
    concrete = g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 7.5))  # piers and girders: below z = 0.125
    steel = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', y), 60.0, -22.8)),
                   g.clamp01(g.math('MULTIPLY_ADD', z, 60.0, -9.0)))  # rails: |y| above 0.38, z above 0.15
    grain = g.noise(30.0, 3.0)
    wood_col = g.mix(grain, (0.30, 0.26, 0.22), (0.42, 0.37, 0.31))
    concrete_col = g.mix(grain, (0.50, 0.50, 0.47), (0.60, 0.60, 0.57))
    base = g.mix(concrete, wood_col, concrete_col)
    base = g.mix(steel, base, (0.30, 0.31, 0.33))
    patches = g.clamp01(g.math('MULTIPLY_ADD', grain, 3.0, -0.8))  # snow in patches, planks show through
    albedo = g.mix(g.math('MULTIPLY', g.snow_mask(0.15, 0.6), patches), base, (0.93, 0.94, 0.97))
    rough = g.math('MULTIPLY_ADD', steel, -0.35, 0.85)  # steel 0.5, wood and concrete 0.85
    metal = g.math('MULTIPLY_ADD', steel, 0.7, 0.0)
    normal = g.bump(g.noise(35.0, 3.0), 0.05, 0.01)
    orm = g.combine(ao, rough, metal)
    g.finish(albedo, normal, orm)
    return mat, g


def build(name):
    bm = _span_damaged_geometry() if name == "bridge_span_damaged" else _span_geometry()
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    obj = rc.make_object(name + "_body", bm)
    rc.uv_smart(obj)
    mat, graph = _material(name + "_mat")
    obj.data.materials.append(mat)
    return obj, mat, graph


def run(name):
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    obj, mat, graph = build(name)
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
