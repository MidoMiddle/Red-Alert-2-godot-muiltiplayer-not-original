"""Ore drill ore_source: a tower with a turning drill head (docs/map_elements/06_ore/prompts.md).

One cell footprint, about three cells tall, origin at the cell centre on the ground, budget 3000
triangles. Two nodes: "tower" (base, pole, cabinet, hose) stays still, "drill" (screw and head)
is turned by the game around the vertical axis through the origin. One model serves ore and gems:
the game colours the head for the resource (docs/map_elements/06_ore/README.md, question 3), so the
head has no colour of its own here. Glow and the flying ore are not in the model
(generation_requirements.md, section 4; 06_ore prompts, section 4).

Texture 1024: the drill is a tall structure, not a small prop, so the 512 rule does not apply.
The layout is our own. The reference picture gave the parts (pole with a screw, cap, cabinet,
hose, base), not the shapes, markings or numbers (generation_requirements.md, section 8).

In Blender, after rts_common is on sys.path:
    import families.ore_source as ore
    print(ore.run())
"""

import math
import os

import bmesh
from mathutils import Vector

import rts_common as rc

SIZE = 1024


def _ring(radius, z, sides):
    return [Vector((radius * math.cos(2 * math.pi * i / sides),
                    radius * math.sin(2 * math.pi * i / sides), z)) for i in range(sides)]


def add_frustum(bm, r0, z0, r1, z1, sides=16):
    """Cylinder or cone with caps, around the vertical axis through the origin."""
    bottom = [bm.verts.new(p) for p in _ring(r0, z0, sides)]
    top = [bm.verts.new(p) for p in _ring(r1, z1, sides)]
    for i in range(sides):
        j = (i + 1) % sides
        bm.faces.new((bottom[i], bottom[j], top[j], top[i]))
    bm.faces.new(bottom)
    bm.faces.new(top)


def _helix(radius, z0, z1, turns, per_turn):
    n = turns * per_turn
    return [Vector((radius * math.cos(2 * math.pi * i / per_turn),
                    radius * math.sin(2 * math.pi * i / per_turn),
                    z0 + (z1 - z0) * i / n)) for i in range(n + 1)]


def _bezier(p, t):
    u = 1.0 - t
    return u ** 3 * p[0] + 3 * u * u * t * p[1] + 3 * u * t * t * p[2] + t ** 3 * p[3]


def _tube(bm, pts, half_a, half_b):
    """Square tube through the points: one box per segment, its sides perpendicular to the segment."""
    for p1, p2 in zip(pts, pts[1:]):
        d = (p2 - p1).normalized()
        a = d.cross(Vector((0.0, 0.0, 1.0)))
        if a.length < 1e-6:
            a = d.cross(Vector((1.0, 0.0, 0.0)))
        a = a.normalized() * half_a
        b = d.cross(a).normalized() * half_b
        rc.add_prism(bm, p1, p2, a, b)


def _tower_geometry():
    bm = bmesh.new()
    add_frustum(bm, 0.42, 0.0, 0.42, 0.08)                       # concrete base plate
    add_frustum(bm, 0.065, 0.08, 0.065, 2.5, sides=12)           # pole the screw turns around
    rc.add_box(bm, (0.17, -0.16, 0.18), (0.46, 0.16, 1.05))      # cabinet on the side
    rc.add_box(bm, (0.06, -0.025, 0.45), (0.18, 0.025, 0.50))    # bracket from the pole to the cabinet
    ctrl = [Vector((0.30, 0.0, 1.10)), Vector((0.36, 0.0, 1.40)),
            Vector((0.30, 0.0, 2.00)), Vector((0.22, 0.0, 2.30))]
    _tube(bm, [_bezier(ctrl, i / 14) for i in range(15)], 0.03, 0.03)  # hose up to the head
    return bm


def _drill_geometry():
    bm = bmesh.new()
    _tube(bm, _helix(0.13, 0.22, 2.22, turns=8, per_turn=12), 0.03, 0.025)  # screw
    add_frustum(bm, 0.24, 2.30, 0.24, 2.36)                      # flange under the funnel
    add_frustum(bm, 0.24, 2.36, 0.16, 2.52)                      # funnel
    add_frustum(bm, 0.16, 2.52, 0.16, 2.72)                      # drill housing
    add_frustum(bm, 0.19, 2.72, 0.19, 2.78)                      # upper flange
    add_frustum(bm, 0.14, 2.78, 0.14, 2.92)                      # motor cap
    add_frustum(bm, 0.14, 2.92, 0.04, 3.00)                      # round top
    return bm


def _place_uv(obj, offset):
    """Moves the object's UV layout into one half of the texture, so the two objects do not overlap."""
    for item in obj.data.uv_layers.active.data:
        item.uv = (offset + 0.5 * item.uv.x, item.uv.y)


def _material():
    """Steel with a green cabinet and a concrete base, picked by position in object space."""
    mat = rc.new_material("ore_source_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.12)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    z = g.axis(coord, 'Z')
    # Cabinet: x 0.17..0.46, z 0.18..1.05. Concrete: z below 0.08.
    cabinet = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', x, 60.0, -10.2)),
                     g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 63.0)),
                            g.clamp01(g.math('MULTIPLY_ADD', z, 60.0, -10.8))))
    concrete = g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 5.0))
    steel = g.clamp01(g.math('MULTIPLY_ADD', g.clamp01(g.math('ADD', cabinet, concrete)), -1.0, 1.0))
    grain = g.noise(14.0, 4.0)
    steel_col = g.mix(grain, (0.36, 0.37, 0.39), (0.56, 0.58, 0.61))
    green_col = g.mix(grain, (0.27, 0.33, 0.22), (0.36, 0.42, 0.29))
    concrete_col = g.mix(grain, (0.50, 0.50, 0.47), (0.60, 0.60, 0.57))
    albedo = g.mix(cabinet, g.mix(concrete, steel_col, concrete_col), green_col)
    rough = g.math('MULTIPLY_ADD', concrete, 0.9,
                   g.math('MULTIPLY_ADD', cabinet, 0.6, g.math('MULTIPLY', steel, 0.42)))
    metal = g.math('MULTIPLY_ADD', cabinet, 0.12, g.math('MULTIPLY', steel, 0.9))
    normal = g.bump(g.noise(60.0, 3.0), 0.04, 0.01)
    orm = g.combine(ao, rough, metal)
    g.finish(albedo, normal, orm)
    return mat, g


def build():
    tower = rc.make_object("ore_source_tower", _tower_geometry())
    drill = rc.make_object("ore_source_drill", _drill_geometry())
    rc.uv_smart(tower)
    _place_uv(tower, 0.0)
    rc.uv_smart(drill)
    _place_uv(drill, 0.5)
    mat, graph = _material()
    tower.data.materials.append(mat)
    drill.data.materials.append(mat)
    return tower, drill, mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    tower, drill, mat, graph = build()
    base = os.path.join(rc.SRC_ROOT, "map", "ore_source")
    report = [f"tower: triangles {rc.tri_count(tower)}, size {tuple(round(v, 3) for v in tower.dimensions)}",
              f"drill: triangles {rc.tri_count(drill)}, size {tuple(round(v, 3) for v in drill.dimensions)}"]
    imgs = rc.bake_material([tower, drill], graph, SIZE, "ore_source", os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "map", "ore_source", "ore_source.glb")
    with rc.node_names([tower, drill], ["tower", "drill"]):
        rc.save_blend([tower, drill], mat, imgs, os.path.join(base, "ore_source.blend"))
        rc.export_glb([tower, drill], glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
