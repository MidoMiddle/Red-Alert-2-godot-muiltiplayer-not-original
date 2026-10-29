"""Alliance weather control device: the Alliance's storm superweapon (docs/ra2_buildings/allies/weather_control_device).

Footprint 3x3 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    plinth    a round plinth with four pads out on the diagonals, a channel round it, a railing on the inner ring
    stem      a tall stem in the middle, flared at its foot, holding a large sphere in panels; two curved ribs stand up round
              the sphere
    pods      a smaller sphere on each pad, a curved shield behind it, two bent pipes beside it
No emblems, no text. The channel round the plinth and the ribs round the large sphere take the owner's colour, where the
references have the blue. Height 2.45 to the top of the ribs. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the plinth, the pads, the railing, the stem, the spheres, the shields and the pipes
    team_mask       the channel and the ribs: the game paints them in the owner's colour
    base            the round plinth's slab (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_weather_control_device as awcd
    print(awcd.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_weather_control_device"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, STEEL, LIGHT, DARK, TEAM = (_part(i) for i in range(5))
PARTS = 5

PLINTH_H = 0.06
DECK_TOP = 0.16
RING_R = 0.95
SPHERE = Vector((0.0, 0.0, 1.85))
SPHERE_R = 0.5
PODS = [Vector((1.0 * math.cos(a), 1.0 * math.sin(a), 0.0)) for a in (math.pi / 4 + k * math.pi / 2 for k in range(4))]
POD_R = 0.28


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _pipe(p, points, radius, part, sides=10):
    pts = [Vector(q) for q in points]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        rings.append(_ring(q, d, radius, sides))
    p.loft(rings, part)


def _arc(p, centre, radius, width, thick, axis_angle, a0, a1, part, steps=14):
    """A curved rib: a band of a circle round centre in the vertical plane at axis_angle, from a0 to a1 (radians from the top),
    width across, thick out."""
    out = Vector((math.cos(axis_angle), math.sin(axis_angle), 0.0))
    side = Vector((-out.y, out.x, 0.0)) * (width / 2)
    up = Vector((0.0, 0.0, 1.0))
    rings = []
    for i in range(steps + 1):
        a = a0 + (a1 - a0) * i / steps
        d = up * math.cos(a) + out * math.sin(a)
        q0, q1 = centre + d * radius, centre + d * (radius + thick)
        rings.append([q0 - side, q1 - side, q1 + side, q0 + side])
    p.loft(rings, part)


# --- body -------------------------------------------------------------------------

def _plinth(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLINTH_H, 1.2), (DECK_TOP - 0.03, 1.2), (DECK_TOP, 1.17), (DECK_TOP, 0.2)], CONCRETE, sides=48)
    for c in PODS:  # the pads out on the diagonals
        out = Vector((c.x, c.y, 0.0)).normalized()
        side = Vector((-out.y, out.x, 0.0))
        corners = [out * r + side * s for r, s in ((0.7, -0.4), (1.38, -0.4), (1.38, 0.4), (0.7, 0.4))]
        p.loft([[q + Vector((0.0, 0.0, z)) for q in corners] for z in (PLINTH_H, DECK_TOP)], CONCRETE)
    p.turned((0.0, 0.0, 0.0), 'Z', [(DECK_TOP, 0.62), (DECK_TOP + 0.04, 0.62), (DECK_TOP + 0.04, 0.2)], STEEL, sides=40)  # inner ring
    for i in range(24):  # its railing
        a = 2 * math.pi * i / 24
        q = Vector((0.6 * math.cos(a), 0.6 * math.sin(a), DECK_TOP + 0.04))
        p.box((q.x - 0.01, q.y - 0.01, q.z), (q.x + 0.01, q.y + 0.01, q.z + 0.12), DARK)
    p.turned((0.0, 0.0, 0.0), 'Z', [(DECK_TOP + 0.15, 0.61), (DECK_TOP + 0.165, 0.61), (DECK_TOP + 0.165, 0.59), (DECK_TOP + 0.15, 0.59)], DARK, sides=40)


def _stem(p):
    # Flared at its foot, narrowing up to a neck under the sphere.
    p.turned((0.0, 0.0, 0.0), 'Z', [(DECK_TOP + 0.04, 0.55), (0.3, 0.38), (0.5, 0.24), (0.8, 0.16), (1.2, 0.13), (SPHERE.z - SPHERE_R + 0.05, 0.12)],
             STEEL, sides=24)
    for k in range(4):  # four fins down the flare
        a = k * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0)) * 0.03
        prof = [(0.12, 1.2), (0.12, DECK_TOP + 0.04), (0.6, DECK_TOP + 0.04)]
        p.loft([[out * r + Vector((0.0, 0.0, z)) + side * s for r, z in prof] for s in (-1, 1)], STEEL)
    p.sphere(SPHERE, 'Z', SPHERE_R, LIGHT, sides=32, rings=16)


def _pods(p):
    for c in PODS:
        out = Vector((c.x, c.y, 0.0)).normalized()
        side = Vector((-out.y, out.x, 0.0))
        p.turned(c, 'Z', [(DECK_TOP, 0.2), (DECK_TOP + 0.12, 0.16)], STEEL, sides=16)  # its seat
        p.sphere(c + Vector((0.0, 0.0, DECK_TOP + 0.12 + POD_R * 0.9)), 'Z', POD_R, LIGHT, sides=24, rings=12)
        _arc(p, c + Vector((0.0, 0.0, DECK_TOP + 0.12 + POD_R * 0.9)), POD_R + 0.02, 0.14, 0.05, math.atan2(out.y, out.x), -0.4, 1.6, STEEL)
        for s in (-1, 1):  # two bent pipes beside it
            foot = c + out * 0.22 + side * s * 0.2 + Vector((0.0, 0.0, DECK_TOP))
            _pipe(p, [foot, foot + Vector((0, 0, 0.3)), foot + Vector((0, 0, 0.36)) - out * 0.05, foot + Vector((0, 0, 0.36)) - out * 0.15,
                      foot + Vector((0, 0, 0.3)) - out * 0.2], 0.03, STEEL)


def _team(p):
    # The channel round the plinth and out to the pads.
    p.turned((0.0, 0.0, 0.0), 'Z', [(DECK_TOP, 0.9), (DECK_TOP + 0.012, 0.9), (DECK_TOP + 0.012, 0.78), (DECK_TOP, 0.78)], TEAM, sides=48)
    for c in PODS:
        out = Vector((c.x, c.y, 0.0)).normalized()
        side = Vector((-out.y, out.x, 0.0))
        corners = [out * r + side * s for r, s in ((0.88, -0.05), (1.3, -0.05), (1.3, 0.05), (0.88, 0.05))]
        p.loft([[q + Vector((0.0, 0.0, z)) for q in corners] for z in (DECK_TOP, DECK_TOP + 0.012)], TEAM)
    # Two curved ribs standing round the large sphere, across each other.
    for a in (0.0, math.pi / 2):
        _arc(p, SPHERE, SPHERE_R + 0.02, 0.12, 0.06, a, -1.9, 1.9, TEAM, steps=24)


# --- material -----------------------------------------------------------------------

def _material():
    """Light concrete, grey steel in panels, light grey spheres in panels, dark, a light team zone."""
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
    turn = g.math('MULTIPLY', g.math('ARCTAN2', y, x), 6.0 / math.pi)  # six panels round each sphere
    gores = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', turn), 0.5)), 40.0, -18.5))

    concrete = g.mix(grain, lin((0.60, 0.60, 0.60)), lin((0.68, 0.68, 0.68)))
    steel = g.mix(grain, lin((0.46, 0.48, 0.52)), lin((0.54, 0.56, 0.60)))
    light = g.mix(gores, g.mix(grain, lin((0.72, 0.74, 0.78)), lin((0.80, 0.82, 0.86))), lin((0.40, 0.42, 0.46)))
    dark = g.mix(grain, lin((0.10, 0.11, 0.13)), lin((0.15, 0.16, 0.18)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (steel, light, dark, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.4, 0.3, 0.6, 0.4))
    metallic = weighted((0.0, 0.6, 0.5, 0.5, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', gores, mask[2]), 0.6)), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _drop_bottoms(objs):
    """Deletes the faces that look down onto the plinth or the ground: never seen, and black in the bake."""
    for obj in objs:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        low = [f for f in bm.faces if f.normal.z < -0.99 and (obj.matrix_world @ f.calc_center_median()).z < PLINTH_H + 1e-3]
        bmesh.ops.delete(bm, geom=low, context='FACES')
        bm.to_mesh(obj.data)
        bm.free()


def _pack(objs):
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')


def build():
    _clear()
    body = Parts()
    _plinth(body)
    _stem(body)
    _pods(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 1.25), (PLINTH_H, 1.25), (PLINTH_H, 0.01)], CONCRETE, sides=48)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    _drop_bottoms([body, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    _pack(meshes)
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
