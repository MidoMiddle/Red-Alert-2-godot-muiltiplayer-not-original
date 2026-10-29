"""Barracks: the Union's infantry building (docs/ra2_buildings/soviets/barracks).

Footprint 2x2 cells (game/data/factions/union/buildings.yaml), origin at the centre of the footprint on the ground.
A pedestal and a statue over it, the statue the larger part, as the owner's reference shows; the parts are our own:
    pedestal  a light step on the concrete plinth, short bollards at its corners; a stone frustum with brick pilasters on its
              four edges, a panel on each face between them, ribbed hoses from the pilasters to the panels; on top a red frame
              with steel clamps round a stone plate
    statue    a conscript in a long flared coat with a belt, boots, a fur cap; his right arm bent across the chest, the left
              holding a rifle upright on the shoulder, its muzzle above the cap
No emblems, no symbols of real armies or states, no text (docs/art/style_guide/02_units_buildings.md): the panels, where the
reference has emblems, take the owner's colour. Height 3.65 to the muzzle. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the step, the bollards, the pedestal, the frame, the hoses and the statue
    team_mask       the four panels of the pedestal: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")
    exit_infantry   empty on the ground in front of the pedestal: where the infantry comes out

In Blender, after tools/blender is on sys.path:
    import buildings.barracks as bk
    print(bk.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "barracks"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


PLATE, STONE, BRICK, RED, STEEL, STATUE, TEAM = (_part(i) for i in range(7))
PARTS = 7

PLINTH_H = 0.08
STEP_Z = 0.16  # the top of the light step, the foot of the pedestal
FOOT_H = 0.78  # the pedestal's half-size at its foot
TOP_Z = 1.22  # the top of the pedestal
TOP_H = 0.48  # its half-size at the top
FRAME_Z = 1.29  # the top of the red frame: the statue stands here
S0 = FRAME_Z
EXIT_POINT = (0.0, -1.2, 0.0)  # in front of the pedestal, on the ground

# The four faces of the pedestal: the outward direction and the direction along the face.
FACES = ((Vector((0.0, -1.0, 0.0)), Vector((1.0, 0.0, 0.0))), (Vector((1.0, 0.0, 0.0)), Vector((0.0, 1.0, 0.0))),
         (Vector((0.0, 1.0, 0.0)), Vector((-1.0, 0.0, 0.0))), (Vector((-1.0, 0.0, 0.0)), Vector((0.0, -1.0, 0.0))))


def _half(z):
    """The pedestal's half-size at height z: straight from FOOT_H at STEP_Z to TOP_H at TOP_Z."""
    return FOOT_H + (TOP_H - FOOT_H) * (z - STEP_Z) / (TOP_Z - STEP_Z)


def _ring(centre, d, radius, sides, phase=0.0):
    """Points of a circle round centre, square to the unit direction d."""
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius
            for a in (phase + 2 * math.pi * i / sides for i in range(sides))]


def _limb(p, a, b, ra, rb, part, sides=8):
    """A tapered round piece from a to b, radius ra at a and rb at b."""
    a, b = Vector(a), Vector(b)
    d = (b - a).normalized()
    p.loft([_ring(a, d, ra, sides, math.pi / sides), _ring(b, d, rb, sides, math.pi / sides)], part)


def _oval(p, stations, part, sides=12, at=(0.0, 0.0)):
    """A vertical body of oval sections: stations are (height, half-width along x, half-depth along y)."""
    rings = []
    for z, rx, ry in stations:
        rings.append([Vector((at[0] + rx * math.cos(a), at[1] + ry * math.sin(a), z))
                      for a in (math.pi / sides + 2 * math.pi * i / sides for i in range(sides))])
    p.loft(rings, part)


def _hose(p, a, c, b):
    """A ribbed hose on a quadratic curve from a over c to b."""
    n = 13
    points = [a * (1 - t) ** 2 + c * 2 * t * (1 - t) + b * t * t for t in (i / (n - 1) for i in range(n))]
    rings = []
    for i, q in enumerate(points):
        d = (points[min(i + 1, n - 1)] - points[max(i - 1, 0)]).normalized()
        rings.append(_ring(q, d, 0.032 if i % 2 else 0.026, 8))
    p.loft(rings, STEEL)


# --- the pedestal -----------------------------------------------------------------

def _pedestal(p):
    p.box((-0.88, -0.88, PLINTH_H), (0.88, 0.88, STEP_Z), PLATE)  # the light step on the plinth
    for sx in (-1, 1):
        for sy in (-1, 1):
            p.turned((0.82 * sx, 0.82 * sy, 0.0), 'Z', [(STEP_Z, 0.055), (STEP_Z + 0.07, 0.055), (STEP_Z + 0.1, 0.03)],
                     PLATE, sides=10)  # a bollard at the corner of the step
    corners = ((-1, -1), (1, -1), (1, 1), (-1, 1))
    p.loft([[Vector((sx * h, sy * h, z)) for sx, sy in corners] for z, h in ((STEP_Z, FOOT_H), (TOP_Z, TOP_H))], STONE)
    for sx, sy in corners:  # the brick pilasters on the edges, a little proud of the faces
        p.bar((sx * (FOOT_H + 0.01), sy * (FOOT_H + 0.01), STEP_Z), (sx * (TOP_H + 0.01), sy * (TOP_H + 0.01), TOP_Z),
              0.08, BRICK)
    for out, along in FACES:  # two hoses on each face, from the pilasters to the panel's edges
        z = 0.70
        h = _half(z)
        for s in (-1, 1):
            a = out * (h + 0.02) + along * s * (h - 0.06) + Vector((0.0, 0.0, z + 0.08))
            b = out * (h + 0.02) + along * s * (h - 0.24) + Vector((0.0, 0.0, z - 0.08))
            c = out * (h + 0.16) + along * s * (h - 0.12) + Vector((0.0, 0.0, z))
            _hose(p, a, c, b)
    # The red frame on top, round a stone plate, with steel clamps at the corners and in the middle of each side.
    p.box((-0.47, -0.47, TOP_Z), (0.47, 0.47, FRAME_Z - 0.02), STONE)
    for lo, hi in (((-0.58, -0.58), (0.58, -0.46)), ((-0.58, 0.46), (0.58, 0.58)),
                   ((-0.58, -0.46), (-0.46, 0.46)), ((0.46, -0.46), (0.58, 0.46))):
        p.box((lo[0], lo[1], TOP_Z - 0.01), (hi[0], hi[1], FRAME_Z), RED)
    for sx, sy in corners:
        p.box((sx * 0.6 - 0.07, sy * 0.6 - 0.07, TOP_Z - 0.06), (sx * 0.6 + 0.07, sy * 0.6 + 0.07, FRAME_Z + 0.03), STEEL)
    for out, along in FACES:
        centre = out * 0.6
        lo = centre - along * 0.1 - out * 0.05
        hi = centre + along * 0.1 + out * 0.04
        p.box((min(lo.x, hi.x), min(lo.y, hi.y), TOP_Z - 0.08), (max(lo.x, hi.x), max(lo.y, hi.y), FRAME_Z + 0.02), STEEL)


def _panels(p):
    """A panel on each face of the pedestal between the pilasters, standing out of the slope: the owner's colour."""
    for out, along in FACES:
        sheets = []
        for off in (0.018, -0.01):
            ring = []
            for z, s in ((0.26, -1), (0.26, 1), (1.08, 1), (1.08, -1)):
                h = _half(z)
                ring.append(out * (h + off) + along * s * (h - 0.17) + Vector((0.0, 0.0, z)))
            sheets.append(ring)
        p.loft(sheets, TEAM)


# --- the statue -------------------------------------------------------------------

def _statue(p):
    for x in (-0.13, 0.13):
        p.box((x - 0.08, -0.19, S0), (x + 0.08, 0.08, S0 + 0.1), STATUE)  # a boot, its toe forward
        _limb(p, (x, -0.02, S0 + 0.08), (x, -0.01, S0 + 0.46), 0.085, 0.09, STATUE)  # the shin
    # The coat: flared at the hem, narrow at the waist, wide at the shoulders.
    _oval(p, [(S0 + 0.40, 0.40, 0.29), (S0 + 0.75, 0.35, 0.26), (S0 + 1.12, 0.30, 0.22), (S0 + 1.40, 0.35, 0.23),
              (S0 + 1.58, 0.42, 0.24), (S0 + 1.68, 0.34, 0.20), (S0 + 1.72, 0.15, 0.13)], STATUE)
    _oval(p, [(S0 + 1.10, 0.315, 0.235), (S0 + 1.18, 0.315, 0.235)], STEEL)  # the belt
    _limb(p, (0.0, 0.0, S0 + 1.68), (0.0, 0.0, S0 + 1.78), 0.075, 0.075, STATUE)  # the neck
    _oval(p, [(S0 + 1.76, 0.11, 0.12), (S0 + 1.84, 0.135, 0.14), (S0 + 1.96, 0.13, 0.135), (S0 + 2.0, 0.11, 0.11)],
          STATUE, sides=8)  # the head
    _oval(p, [(S0 + 1.90, 0.20, 0.20), (S0 + 2.12, 0.19, 0.19), (S0 + 2.17, 0.15, 0.15)], STATUE, sides=8)  # the fur cap
    # His right arm (at -x, he faces -y): the upper arm down, the forearm across the chest, the fist.
    shoulder_r = Vector((-0.40, 0.0, S0 + 1.58))
    elbow_r = Vector((-0.47, -0.16, S0 + 1.34))
    fist_r = Vector((-0.12, -0.46, S0 + 1.38))
    _limb(p, shoulder_r, elbow_r, 0.10, 0.085, STATUE)
    _limb(p, elbow_r, fist_r, 0.075, 0.065, STATUE)
    p.sphere(elbow_r, 'Z', 0.075, STATUE, sides=8, rings=4)
    p.sphere(fist_r, 'Z', 0.075, STATUE, sides=8, rings=4)
    # His left arm: the upper arm down, the forearm up to the rifle.
    shoulder_l = Vector((0.40, 0.0, S0 + 1.58))
    elbow_l = Vector((0.47, 0.02, S0 + 1.30))
    fist_l = Vector((0.45, -0.08, S0 + 1.50))
    _limb(p, shoulder_l, elbow_l, 0.10, 0.085, STATUE)
    _limb(p, elbow_l, fist_l, 0.075, 0.065, STATUE)
    p.sphere(elbow_l, 'Z', 0.075, STATUE, sides=8, rings=4)
    p.sphere(fist_l, 'Z', 0.07, STATUE, sides=8, rings=4)
    # The rifle upright against the left shoulder: stock, body, magazine, barrel.
    rx, ry = 0.53, 0.06
    p.box((rx - 0.04, ry - 0.07, S0 + 1.12), (rx + 0.04, ry + 0.05, S0 + 1.42), STEEL)  # the stock
    p.bar((rx, ry, S0 + 1.40), (rx, ry, S0 + 2.0), 0.045, STEEL)  # the body
    p.box((rx - 0.03, ry - 0.11, S0 + 1.62), (rx + 0.03, ry - 0.03, S0 + 1.76), STEEL)  # the magazine
    _limb(p, (rx, ry, S0 + 1.98), (rx, ry, S0 + 2.36), 0.026, 0.022, STEEL, sides=6)  # the barrel


# --- material -----------------------------------------------------------------------

def _material():
    """Light concrete, pinkish stone and brick, a bright red frame, dark steel, a grey statue in courses, a light team zone."""
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
    z = g.axis(g.object_coord(), 'Z')
    # Thin dark joints: the courses of the bricks every 1/16 and the stone courses of the statue every 1/10.
    bricks = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 16.0)), -14.0, 1.0))
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 10.0)), -16.0, 1.0))

    plate = g.mix(grain, lin((0.78, 0.77, 0.74)), lin((0.86, 0.85, 0.82)))
    stone = g.mix(grain, lin((0.58, 0.52, 0.50)), lin((0.66, 0.60, 0.57)))
    brick = g.mix(bricks, g.mix(grain, lin((0.80, 0.70, 0.66)), lin((0.88, 0.78, 0.74))), lin((0.55, 0.47, 0.45)))
    red = g.mix(grain, lin((0.72, 0.10, 0.08)), lin((0.82, 0.14, 0.11)))
    steel = g.mix(grain, lin((0.36, 0.37, 0.39)), lin((0.46, 0.47, 0.49)))
    statue = g.mix(courses, g.mix(grain, lin((0.58, 0.58, 0.57)), lin((0.68, 0.68, 0.66))), lin((0.40, 0.40, 0.40)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = plate
    for m, col in zip(mask[1:], (stone, brick, red, steel, statue, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.8, 0.85, 0.5, 0.45, 0.7, 0.55))
    metallic = weighted((0.0, 0.0, 0.0, 0.1, 0.75, 0.0, 0.2))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('ADD', bricks, courses), 0.6)), 0.04, 0.006)
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
    _pedestal(body)
    _statue(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _panels(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-1.0, -1.0, 0.0), (1.0, 1.0, PLINTH_H), PLATE)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # round limbs smooth, stone edges sharp
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [_empty("exit_infantry", EXIT_POINT)], mat, graph


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
