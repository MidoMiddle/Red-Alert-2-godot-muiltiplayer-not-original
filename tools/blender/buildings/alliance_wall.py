"""Alliance wall: one segment of the Alliance's wall, placed by dragging (docs/ra2_buildings/allies/wall).

Footprint 1x1 cell (game/data/factions/alliance/buildings.yaml), origin at the centre of the cell on the ground. The composition
follows the owner's references; the parts are our own:
    post      a square steel post in the middle of the cell, a broader foot, a sphere on top
    joins     four half-walls from the post to the cell's edges, one each way: a heavy horizontal pipe on a stone foot, a band
              round it by the post. The game shows the ones that face a neighbouring wall of the same owner, as the Union's wall
              (wall.py, PlayerBuildings)
No emblems, no text. The bands round the pipes and a band round the post take the owner's colour, where the references have the
blue. Height 0.62 to the top of the sphere. Budget 20000 triangles, texture 512.

Nodes (docs/art/model_standards.md, section 4); the cells' north (y - 1) is -Z in Godot, +Y in Blender:
    body            the post, its foot and the sphere
    team_mask       the band round the post
    join_n, join_e, join_s, join_w   the half-walls towards the cells north, east, south and west
    team_mask_n, team_mask_e, team_mask_s, team_mask_w   under each join: its band, in the owner's colour
    base            the stone foot under the post (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_wall as aw
    print(aw.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_wall"
PREFIX = ID + "_"
SIZE = 512


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


STONE, STEEL, LIGHT, DARK, TEAM = (_part(i) for i in range(5))
PARTS = 5

PLINTH_H = 0.04
POST = 0.15
POST_TOP = 0.46
PIPE_Z = 0.24
PIPE_R = 0.1
JOINS = (("n", Vector((0.0, 1.0, 0.0))), ("e", Vector((1.0, 0.0, 0.0))), ("s", Vector((0.0, -1.0, 0.0))), ("w", Vector((-1.0, 0.0, 0.0))))


def _along(p, out, r0, r1, half, z0, z1, part):
    """A box from r0 to r1 along out, half wide across, from z0 to z1."""
    side = Vector((-out.y, out.x, 0.0))
    corners = [out * r + side * s for r, s in ((r0, -half), (r1, -half), (r1, half), (r0, half))]
    p.loft([[q + Vector((0.0, 0.0, z)) for q in corners] for z in (z0, z1)], part)


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _post(p):
    p.loft([[Vector((x, y, z)) for x, y in ((-r, -r), (r, -r), (r, r), (-r, r))] for z, r in ((PLINTH_H, POST + 0.04), (0.1, POST + 0.04),
                                                                                              (0.12, POST), (POST_TOP, POST), (POST_TOP + 0.02, POST - 0.02))],
           STEEL)
    p.sphere((0.0, 0.0, POST_TOP + 0.09), 'Z', 0.11, LIGHT, sides=20, rings=10)


def _join(name, out):
    p = Parts()
    _along(p, out, POST + 0.04, 0.5, 0.17, 0.0, 0.1, STONE)  # the stone foot
    c0 = out * POST + Vector((0.0, 0.0, PIPE_Z))
    c1 = out * 0.5 + Vector((0.0, 0.0, PIPE_Z))
    p.loft([_ring(c0, out, PIPE_R, 16), _ring(c1, out, PIPE_R, 16)], STEEL)  # the pipe
    p.loft([_ring(out * 0.42 + Vector((0, 0, PIPE_Z)), out, PIPE_R + 0.015, 16), _ring(out * 0.47 + Vector((0, 0, PIPE_Z)), out, PIPE_R + 0.015, 16)],
           DARK)  # a dark collar near the edge
    join = p.finish(PREFIX + "join_" + name)
    t = Parts()
    t.loft([_ring(out * 0.2 + Vector((0, 0, PIPE_Z)), out, PIPE_R + 0.018, 16), _ring(out * 0.3 + Vector((0, 0, PIPE_Z)), out, PIPE_R + 0.018, 16)],
           TEAM)
    team = t.finish(PREFIX + "team_mask_" + name)
    team.parent = join
    return join, team


def _material():
    """Mossy stone, grey steel in panels, a light silvery sphere, dark collars, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    grain = g.noise(40.0, 4.0)
    moss = g.noise(8.0, 4.0)

    stone = g.mix(moss, lin((0.40, 0.40, 0.32)), lin((0.30, 0.33, 0.24)))
    steel = g.mix(grain, lin((0.62, 0.64, 0.68)), lin((0.70, 0.72, 0.76)))
    light = g.mix(grain, lin((0.70, 0.72, 0.80)), lin((0.80, 0.82, 0.88)))
    dark = g.mix(grain, lin((0.14, 0.15, 0.17)), lin((0.20, 0.21, 0.23)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = stone
    for m, col in zip(mask[1:], (steel, light, dark, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.9, 0.4, 0.2, 0.5, 0.4))
    metallic = weighted((0.0, 0.6, 0.6, 0.6, 0.3))
    normal = g.bump(g.noise(80.0, 3.0), 0.03, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _drop_hidden(obj):
    """Deletes the faces on the ground and the ends that sit against the post: never seen, black in the bake."""
    bm = bmesh.new()
    bm.from_mesh(obj.data)
    hidden = []
    for f in bm.faces:
        c = f.calc_center_median()
        flat = Vector((c.x, c.y, 0.0))
        if f.normal.z < -0.99 and c.z < PLINTH_H + 1e-3:
            hidden.append(f)
        elif abs(f.normal.z) < 0.01 and flat.length < POST + 0.05 and flat.length > 1e-3 and f.normal.dot(flat.normalized()) < -0.99:
            hidden.append(f)
    bmesh.ops.delete(bm, geom=hidden, context='FACES')
    bm.to_mesh(obj.data)
    bm.free()


def build():
    _clear()
    body = Parts()
    _post(body)
    body = body.finish(PREFIX + "body")
    t = Parts()
    t.loft([[Vector((x, y, z)) for x, y in ((-r, -r), (r, -r), (r, r), (-r, r))] for z, r in ((0.3, POST + 0.012), (0.36, POST + 0.012))], TEAM)
    team = t.finish(PREFIX + "team_mask")
    joins = []
    for name, out in JOINS:
        joins += _join(name, out)
    base = Parts()
    base.box((-POST - 0.08, -POST - 0.08, 0.0), (POST + 0.08, POST + 0.08, PLINTH_H), STONE)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, *joins, base]
    for obj in [body, base, *joins]:
        _drop_hidden(obj)
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, mat, graph = build()
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"
              for obj in meshes]
    report.append(f"total triangles {sum(rc.tri_count(obj) for obj in meshes)}")
    base = os.path.join(rc.SRC_ROOT, "building", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    top = [obj for obj in meshes if obj.parent is None]
    rc.arrange(top, ID)  # its place in the file: the row of the players' buildings
    coll = bpy.data.collections[ID]
    for obj in meshes:
        if obj not in top and coll not in obj.users_collection:
            for owner in list(obj.users_collection):
                owner.objects.unlink(obj)
            coll.objects.link(obj)
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(meshes, [o.name[len(PREFIX):] for o in meshes]):
        rc.save_blend(meshes, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(meshes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
