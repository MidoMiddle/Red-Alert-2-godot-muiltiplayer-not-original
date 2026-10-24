"""Snow drifts snowdrift_01 and snowdrift_02 (docs/map_elements/20_snowdrift/prompts.md).

One cell footprint, origin at the cell centre on the ground, one node "body",
at most 500 triangles, 512 textures (small object, generation_requirements.md section 4).
The track is a decal and is not part of the model. There is no damaged variant:
a crushed drift disappears and the engine leaves clean ground.

In Blender, after rts_common is on sys.path:
    import families.snowdrift as sd
    print(sd.run("snowdrift_01"))
"""

import math
import os

import bmesh

import rts_common as rc

SIZE = 512
SNOW_SHADE = (0.52, 0.62, 0.80)   # blue-grey in the hollows
SNOW_LIGHT = (0.93, 0.94, 0.97)
RIDGE_LIGHT = (0.98, 0.98, 1.00)  # the crest catches the sun


def _profile(d, gentle, steep):
    """Ridge profile across the wind: a long gentle slope for d < 0, a short steep edge for d > 0."""
    if d <= 0.0:
        return max(0.0, 1.0 - (-d) / gentle) ** 2
    return max(0.0, 1.0 - d / steep)


def _taper(x, power):
    """Falls to zero at the cell edges along x, so the base sits on the ground."""
    return max(0.0, 1.0 - (2.0 * x) ** 2) ** power


def _grid_01():
    """One curved ridge; the crest is sharp on the steep side and rounded on the gentle one."""
    xs = [-0.5 + i / 12 for i in range(13)]
    ss = [-0.45, -0.36, -0.27, -0.18, -0.09, -0.04, -0.02, 0.0,
          0.02, 0.04, 0.06, 0.08, 0.10, 0.12]
    rows = []
    for x in xs:
        ridge = 0.05 * math.sin(math.pi * (x + 0.5))
        crest = 0.4 * (0.9 + 0.1 * math.cos(2 * math.pi * x)) * _taper(x, 0.5)
        rows.append([(x, ridge + s, crest * _profile(s, 0.45, 0.12)) for s in ss])
    return rows


def _grid_02():
    """Long and low, with two parallel ridges and a shallow valley between them."""
    xs = [-0.5 + i / 16 for i in range(17)]
    ys = [-0.4 + 0.8 * j / 12 for j in range(13)]
    rows = []
    for x in xs:
        taper = _taper(x, 0.3)
        row = []
        for y in ys:
            f = max(_profile(y - c, 0.2, 0.07) for c in (-0.13, 0.13))
            row.append((x, y, 0.3 * (0.95 + 0.05 * math.cos(4 * math.pi * x)) * f * taper))
        rows.append(row)
    return rows


GRIDS = {"snowdrift_01": _grid_01, "snowdrift_02": _grid_02}


def _grid_bmesh(rows):
    ys = [p[1] for row in rows for p in row]
    mid = 0.5 * (min(ys) + max(ys))  # centre the footprint on the cell origin
    bm = bmesh.new()
    verts = [[bm.verts.new((x, y - mid, z)) for x, y, z in row] for row in rows]
    for i in range(len(verts) - 1):
        for j in range(len(verts[0]) - 1):
            bm.faces.new((verts[i][j], verts[i + 1][j], verts[i + 1][j + 1], verts[i][j + 1]))
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    # Planar UV on the plan: a heightfield has one surface point per (x, y), so islands never overlap.
    uv = bm.loops.layers.uv.new("UVMap")
    for face in bm.faces:
        for loop in face.loops:
            loop[uv].uv = (loop.vert.co.x + 0.5, loop.vert.co.y + 0.5)
    return bm


def _snow_material(name):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.12)
    up = g.clamp01(g.math('MULTIPLY_ADD', g.axis(g.normal_world(), 'Z'), 2.0, -1.0))
    albedo = g.mix(ao, SNOW_SHADE, SNOW_LIGHT)
    albedo = g.mix(g.math('MULTIPLY', up, 0.4), albedo, RIDGE_LIGHT)
    normal = g.bump(g.noise(9.0, 3.0), 0.1, 0.02)
    orm = g.combine(ao, 0.9, 0.0)
    g.finish(albedo, normal, orm)
    return mat, g


def build(name):
    bm = _grid_bmesh(GRIDS[name]())
    obj = rc.make_object(name + "_body", bm, smooth=True)
    mat, graph = _snow_material(name + "_snow")
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
