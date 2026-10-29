"""Tesla reactor: the Union's power plant, +150 (docs/ra2_buildings/soviets/tesla_reactor).

Footprint 3x2 cells (game/data/factions/union/buildings.yaml): 3 along x, 2 along the depth; the front looks south (-Y in
Blender, +Z in Godot). Origin at the centre of the footprint on the ground. The composition follows the owner's references; the
parts are our own:
    towers    two brick towers at the ends, a sloping buttress on the front of each with a bolted plate, a bolted plate on top,
              two bent pipes down the front, thick pipe elbows from the outer side into the ground
    sphere    a large polished sphere between the towers, held by steel rods from both
    pit       a dark pit with a frame under the sphere, where the game will draw the discharges
No emblems, no text. The plates on the buttresses and on the tops take the owner's colour, where the references have the red.
Height 1.5 to the tops of the towers. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4; docs/ra2_buildings/soviets/tesla_reactor/animations.md):
    body            the towers, the pipes, the rods, the bolts and the pit
    light_sphere    the sphere; the game will light it while the reactor runs
    team_mask       the plates on the buttresses and on the tops: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")
    arc_origin      empty under the sphere, over the pit: where the discharges start

In Blender, after tools/blender is on sys.path:
    import buildings.reactor as tr
    print(tr.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "reactor"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, BRICK, STEEL, LILAC, CHROME, DARK, TEAM = (_part(i) for i in range(7))
PARTS = 7

PLINTH_H = 0.08
TOWER_X = 0.97  # the towers' centres, either way along x
TOWER_HALF = 0.33  # their half-width along x
TOWER_Y = (-0.55, 0.72)  # their front and back faces
TOWER_TOP = 1.5
SLOPE = ((-0.95, PLINTH_H + 0.1), (-0.55, 0.78))  # the buttress's slope: its foot at the front, its top on the tower
SPHERE = Vector((0.0, 0.08, 0.9))
SPHERE_R = 0.58


def _ring(centre, d, radius, sides, phase=0.0):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius
            for a in (phase + 2 * math.pi * i / sides for i in range(sides))]


def _tube(p, points, radius, part, sides=12):
    """A round tube through the points."""
    pts = [Vector(q) for q in points]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        rings.append(_ring(q, d, radius, sides))
    p.loft(rings, part)


def _bezier(a, c, b, n=9):
    a, c, b = Vector(a), Vector(c), Vector(b)
    return [a * (1 - t) ** 2 + c * 2 * t * (1 - t) + b * t * t for t in (i / (n - 1) for i in range(n))]


def _bolt(p, centre, normal, radius=0.035, height=0.025):
    """A hexagonal bolt head standing out of a surface along its normal."""
    centre, normal = Vector(centre), Vector(normal).normalized()
    p.loft([_ring(centre, normal, radius, 6), _ring(centre + normal * height, normal, radius, 6)], STEEL)


def _slope_frame():
    """The slope's direction up and its outward normal, in the YZ plane."""
    (y0, z0), (y1, z1) = SLOPE
    d = Vector((0.0, y1 - y0, z1 - z0)).normalized()
    n = Vector((0.0, -d.z, d.y))  # out of the slope, to the front and up
    return Vector((0.0, y0, z0)), d, n


# --- body -------------------------------------------------------------------------

def _tower(p, cx):
    x0, x1 = cx - TOWER_HALF, cx + TOWER_HALF
    y0, y1 = TOWER_Y
    p.box((x0, y0, PLINTH_H), (x1, y1, TOWER_TOP), BRICK)
    (sy, sz), (ty, tz) = SLOPE
    p.profile_x([(y0, PLINTH_H), (sy, PLINTH_H), (sy, sz), (ty, tz)], x0, x1, BRICK)  # the buttress
    for sx in (-1, 1):  # bolts at the corners of the top
        for y in (y0 + 0.07, y1 - 0.07):
            _bolt(p, (cx + sx * (TOWER_HALF - 0.07), y, TOWER_TOP), (0, 0, 1), 0.04, 0.04)
    for dx in (-0.11, 0.11):  # two bent pipes down the front of the tower, over the buttress
        top = Vector((cx + dx, y0, 1.32))
        foot = Vector((cx + dx, y0, 0.86))
        _tube(p, [top + Vector((0, 0.02, 0)), top + Vector((0, -0.07, -0.02)), top + Vector((0, -0.08, -0.1)),
                  foot + Vector((0, -0.08, 0.1)), foot + Vector((0, -0.07, 0.02)), foot + Vector((0, 0.02, 0))], 0.035, LILAC, 10)
    # Thick pipe elbows from the outer side into the ground.
    side = 1 if cx > 0 else -1
    xo = cx + side * TOWER_HALF
    for y, z in ((-0.25, 0.95), (0.3, 0.6)):
        _tube(p, _bezier((xo - side * 0.02, y, z), (xo + side * 0.13, y, z), (xo + side * 0.09, y - 0.05, PLINTH_H + 0.02)), 0.075,
              STEEL, 14)
        p.turned((xo, y, z), 'X', [(-0.01 * side, 0.1), (0.03 * side, 0.1)], STEEL, sides=14)  # its flange on the wall


def _slope_bolts(p, cx):
    base, d, n = _slope_frame()
    length = (Vector((0.0, *SLOPE[1])) - Vector((0.0, *SLOPE[0]))).length
    for t in (0.35, 0.65):
        for dx in (-0.16, 0.0, 0.16):
            centre = base + d * (t * length) + n * 0.02 + Vector((cx + dx, 0.0, 0.0))
            _bolt(p, centre, n)


def _rods(p):
    """Steel rods from both towers into the sphere."""
    for side in (-1, 1):
        x = side * (TOWER_X - TOWER_HALF)
        for y, z in ((0.0, 1.08), (0.25, 0.66), (-0.2, 0.62)):
            target = SPHERE + (Vector((side * SPHERE_R * 0.8, y - SPHERE.y, z - SPHERE.z)))
            _tube(p, [(x, y, z), (target.x, y, z)], 0.035, STEEL, 10)
            p.turned((x, y, z), 'X', [(0.0, 0.06), (side * -0.04, 0.06)], STEEL, sides=10)  # its collar on the tower


def _pit(p):
    """A dark pit under the sphere with a steel frame, between the towers."""
    w = TOWER_X - TOWER_HALF - 0.02
    p.box((-w, -0.75, PLINTH_H), (w, 0.68, PLINTH_H + 0.01), DARK)
    for lo, hi in (((-w, -0.8), (w, -0.75)), ((-w, 0.68), (w, 0.73))):
        p.box((lo[0], lo[1], PLINTH_H), (hi[0], hi[1], PLINTH_H + 0.04), STEEL)
    for x, y, a in ((-0.25, -0.2, 0.4), (0.18, 0.1, -0.6), (0.05, -0.45, 1.2)):  # scrap in the pit
        d = Vector((math.cos(a), math.sin(a), 0.0)) * 0.14
        p.bar((x - d.x, y - d.y, PLINTH_H + 0.03), (x + d.x, y + d.y, PLINTH_H + 0.05), 0.03, STEEL)


def _team(p):
    base, d, n = _slope_frame()
    length = (Vector((0.0, *SLOPE[1])) - Vector((0.0, *SLOPE[0]))).length
    for cx in (-TOWER_X, TOWER_X):
        # The plate on the buttress, a little out of the slope.
        lo, hi = base + d * (0.12 * length), base + d * (0.88 * length)
        corners = [(lo, -1), (lo, 1), (hi, 1), (hi, -1)]
        sheets = [[q + n * off + Vector((cx + s * (TOWER_HALF - 0.04), 0.0, 0.0)) for q, s in corners] for off in (0.02, -0.005)]
        p.loft(sheets, TEAM)
        # The plate on top.
        y0, y1 = TOWER_Y
        p.box((cx - TOWER_HALF + 0.12, y0 + 0.14, TOWER_TOP), (cx + TOWER_HALF - 0.12, y1 - 0.14, TOWER_TOP + 0.015), TEAM)


# --- material -----------------------------------------------------------------------

def _material():
    """Red brick in courses, rusty concrete, dark steel, lilac pipes, polished steel, the dark pit, a light team zone."""
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
    rust = g.noise(6.0, 5.0)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    # The bricks: a course every 1/14 up, a joint every brick along both x and y, shifted half a brick every other course.
    course = g.math('MULTIPLY', z, 14.0)
    shift = g.math('MULTIPLY', g.math('FLOOR', course), 0.5)
    along = g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 7.0), shift)
    joints = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', course), -10.0, 1.0)),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', along), -14.0, 1.0)))

    concrete = g.mix(rust, lin((0.40, 0.40, 0.38)), lin((0.46, 0.38, 0.30)))
    brick = g.mix(joints, g.mix(grain, lin((0.58, 0.30, 0.24)), lin((0.70, 0.40, 0.32))), lin((0.78, 0.74, 0.70)))
    steel = g.mix(grain, lin((0.22, 0.22, 0.23)), lin((0.30, 0.30, 0.31)))
    lilac = g.mix(grain, lin((0.52, 0.46, 0.60)), lin((0.60, 0.54, 0.68)))
    chrome = g.mix(grain, lin((0.70, 0.71, 0.74)), lin((0.82, 0.83, 0.85)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.09, 0.09, 0.10)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (brick, steel, lilac, chrome, dark, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.85, 0.45, 0.35, 0.25, 0.8, 0.5))
    metallic = weighted((0.0, 0.0, 0.7, 0.6, 0.65, 0.2, 0.3))  # not 1: a full metal mirrors the dark sky
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', joints, mask[1]), 0.8)),
                    0.04, 0.006)
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


def _empty(name, point):
    node = bpy.data.objects.new(PREFIX + name, None)
    node.empty_display_type = 'PLAIN_AXES'
    node.empty_display_size = 0.12
    node.location = Vector(point)
    bpy.context.scene.collection.objects.link(node)
    return node


def build():
    _clear()
    body = Parts()
    for cx in (-TOWER_X, TOWER_X):
        _tower(body, cx)
        _slope_bolts(body, cx)
    _rods(body)
    _pit(body)
    body = body.finish(PREFIX + "body")
    sphere = Parts()
    sphere.sphere(SPHERE, 'Z', SPHERE_R, CHROME, sides=32, rings=16)
    sphere = sphere.finish(PREFIX + "light_sphere")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-1.5, -1.0, 0.0), (1.5, 1.0, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, sphere, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the sphere and the pipes smooth, the bricks sharp
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [_empty("arc_origin", (SPHERE.x, SPHERE.y, SPHERE.z - SPHERE_R - 0.02))], mat, graph


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
