"""Alliance battle lab: the Alliance's high-tech building (docs/ra2_buildings/allies/battle_lab).

Footprint 3x3 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground; the front
looks south (-Y in Blender, +Z in Godot). The composition follows the owner's references; the parts are our own:
    plinth    a rounded plate with a lit ring set in it, a lower rounded step
    spine     a tall rounded core in the middle, a plate on its front with a long panel down it, a chevron and a block at its foot
    pods      large rounded pods in panels on both sides at different heights, a smaller one low on the right, two small ones on
              the top: the building is wide above and narrow below
    antennas  thin masts out of the top, coils on them
No emblems, no text. The panel on the front, the block at its foot, the coils and the ring in the plinth take the owner's colour,
where the references have the blue. Height 3.1 to the tallest mast. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the step, the spine, the front plate, the pods and the masts
    team_mask       the long panel and the block on the front, the coils and the ring: the game paints them in the owner's colour
    base            the rounded plate (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_battle_lab as abl
    print(abl.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_battle_lab"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, WHITE, STEEL, DARK, TEAM = (_part(i) for i in range(5))
PARTS = 5

PLINTH_H = 0.08
STEP_Z = 0.16
SPINE_TOP = 2.05
# The pods: centre x, y, half-sizes along x and y, bottom, top.
PODS = ((-0.72, 0.05, 0.55, 0.6, 0.95, 1.5), (0.74, 0.08, 0.55, 0.62, 1.08, 1.62), (0.62, -0.05, 0.38, 0.42, 0.55, 0.88),
        (-0.36, 0.28, 0.36, 0.36, 1.62, 1.98), (0.4, 0.3, 0.34, 0.34, 1.7, 2.02))
# The masts: x, y, foot, top.
MASTS = ((0.0, 0.18, SPINE_TOP, 3.1), (-0.14, 0.3, SPINE_TOP, 2.75), (0.16, 0.36, SPINE_TOP, 2.85), (-0.36, 0.28, 1.98, 2.55),
         (0.42, 0.3, 2.02, 2.6))


def _rounded(cx, cy, hx, hy, z, n=4.0, steps=40):
    """A ring of points round a rounded rectangle (a superellipse of power n) at height z."""
    pts = []
    for i in range(steps):
        a = 2 * math.pi * i / steps
        c, s = math.cos(a), math.sin(a)
        pts.append(Vector((cx + hx * math.copysign(abs(c) ** (2 / n), c), cy + hy * math.copysign(abs(s) ** (2 / n), s), z)))
    return pts


def _rounded_body(p, cx, cy, hx, hy, z0, z1, part, bevel=0.05, n=4.0):
    """A rounded block with bevelled top and bottom edges."""
    k = 1.0 - bevel / max(min(hx, hy), 1e-6)
    p.loft([_rounded(cx, cy, hx * s, hy * s, z, n) for z, s in ((z0, k), (z0 + bevel, 1.0), (z1 - bevel, 1.0), (z1, k))], part)


# --- body -------------------------------------------------------------------------

def _body(p):
    _rounded_body(p, 0.0, 0.0, 1.1, 1.1, PLINTH_H, STEP_Z, CONCRETE, bevel=0.03)  # the lower step
    _rounded_body(p, 0.0, 0.08, 0.36, 0.32, STEP_Z, SPINE_TOP, WHITE, bevel=0.04)  # the spine
    # The front plate, its chevron below the panel.
    p.box((-0.24, -0.44, STEP_Z), (0.24, -0.22, SPINE_TOP + 0.05), WHITE)
    p.profile_x([(-0.44, 0.62), (-0.52, 0.62), (-0.44, 0.48)], -0.2, 0.2, WHITE)  # the chevron, sticking out
    for z in (0.3, 0.42):  # dark slots across the foot of the plate
        p.box((-0.2, -0.455, z), (0.2, -0.44, z + 0.04), DARK)
    for pod in PODS:
        cx, cy, hx, hy, z0, z1 = pod
        _rounded_body(p, cx, cy, hx, hy, z0, z1, WHITE, bevel=0.07, n=3.0)
        _rounded_body(p, cx, cy, hx * 0.92, hy * 0.92, z0 - 0.06, z0 + 0.01, STEEL, bevel=0.02, n=3.0)  # the dark ring under it
    for x, y, foot, top in MASTS:
        p.turned((x, y, 0.0), 'Z', [(foot, 0.035), (foot + 0.1, 0.03), (top, 0.012)], STEEL, sides=8)
        p.sphere((x, y, top + 0.02), 'Z', 0.025, WHITE, sides=8, rings=4)


def _team(p):
    p.box((-0.12, -0.455, 0.56), (0.12, -0.43, SPINE_TOP - 0.1), TEAM)  # the long panel down the front plate
    p.box((-0.22, -0.48, STEP_Z), (0.22, -0.3, 0.26), TEAM)  # the block at its foot
    for x, y, foot, top in MASTS[:3]:  # the coils on the masts
        for i in range(4):
            z = foot + 0.15 + i * 0.045
            p.turned((x, y, 0.0), 'Z', [(z, 0.045), (z + 0.03, 0.045)], TEAM, sides=10)
    # The lit ring set in the plinth.
    outer = _rounded(0.0, 0.0, 1.22, 1.22, 0.0)
    inner = _rounded(0.0, 0.0, 1.15, 1.15, 0.0)
    for i in range(len(outer)):  # the ring in segments: a thin quad block on each
        j = (i + 1) % len(outer)
        quad = [outer[i], outer[j], inner[j], inner[i]]
        p.loft([[q + Vector((0.0, 0.0, z)) for q in quad] for z in (PLINTH_H, PLINTH_H + 0.01)], TEAM)


# --- material -----------------------------------------------------------------------

def _material():
    """Dark grey concrete, white panels with seams, dark steel, the dark slots, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    grain = g.noise(30.0, 4.0)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    # Panel seams on the white: a line every 0.18 up and every 0.25 along the walls.
    rows = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 5.5)), -30.0, 1.0))
    cols = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 4.0)), -30.0, 1.0))
    seams = g.math('MAXIMUM', rows, cols)

    concrete = g.mix(grain, lin((0.30, 0.31, 0.32)), lin((0.37, 0.38, 0.39)))
    white = g.mix(seams, g.mix(grain, lin((0.80, 0.81, 0.83)), lin((0.88, 0.89, 0.91))), lin((0.52, 0.53, 0.56)))
    steel = g.mix(grain, lin((0.22, 0.23, 0.26)), lin((0.29, 0.30, 0.33)))
    dark = g.mix(grain, lin((0.06, 0.06, 0.07)), lin((0.09, 0.09, 0.10)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (white, steel, dark, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.4, 0.45, 0.8, 0.45))
    metallic = weighted((0.0, 0.45, 0.7, 0.2, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', seams, mask[1]), 0.6)), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    """Removes what an earlier run left in the session, so names stay free."""
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def build():
    _clear()
    body = Parts()
    _body(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    _rounded_body(base, 0.0, 0.0, 1.45, 1.45, 0.0, PLINTH_H, CONCRETE, bevel=0.02)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the rounded pods smooth, the edges sharp
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [], mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, empties, mat, graph = build()
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"
              for obj in meshes]
    report.append(f"total triangles {sum(rc.tri_count(obj) for obj in meshes)}")
    base = os.path.join(rc.SRC_ROOT, "building", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + empties
    rc.arrange(objs, ID)  # its place in the file: the row of the players' buildings
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(objs, [o.name[len(PREFIX):] for o in objs]):
        rc.save_blend(objs, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
