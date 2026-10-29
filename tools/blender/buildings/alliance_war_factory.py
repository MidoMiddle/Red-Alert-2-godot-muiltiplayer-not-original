"""Alliance war factory: the Alliance's vehicle plant (docs/ra2_buildings/allies/war_factory).

Footprint 5x3 cells (game/data/factions/alliance/buildings.yaml), the hall along x. The gate is in the east end (+X), facing the
exit cell east of the middle row, as the Union's (war_factory.py). Origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    hall      a long arched hall: a light metal skin with dark ribs across it, a spine along its top, a dark foot
    gate      an arched portal with portholes round the east end, a dark opening, and the gate: a leaf hinged at its foot that
              the game lets down outwards into a ramp while a unit comes out, and lifts again
    pylon     a tall horseshoe arch standing over the west end
    tower     a round tower at the north-west corner: a ringed drum, a dome, a mast with a flag
    yard      two turbine housings with fan grilles beside the west part of the hall, their pipes into it; crates, barrels and
              bollards about; the plinth with its corners cut
No emblems, no text. The portal, the bands along the foot of the hall and the flag take the owner's colour, where the references
have the blue. Height 1.75 to the top of the mast. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the hall, the opening, the pylon, the tower, the turbines, the pipes, the crates, the barrels, the bollards
    gate            the gate leaf; its origin is the hinge at its foot, and it opens towards its +X turning about its Z (Godot)
    team_mask       the portal, the bands along the hall and the flag: the game paints them in the owner's colour
    base            the plinth (docs/art/model_standards.md, "Фундамент")
    exit_vehicle    empty on the ground in the exit cell: where the vehicles come out

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_war_factory as awf
    print(awf.run())
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_war_factory"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, SKIN, STEEL, LAMP, DOOR, CRATE, TEAM = (_part(i) for i in range(7))
PARTS = 7

PLINTH_H = 0.08
HALL_X = (-1.3, 1.62)  # the west end under the pylon, the east end with the gate
HALL_RY = 0.85  # the arch's half-width
WALL_Z = 0.25  # where the arch springs (the material paints the skin under it dark)
HALL_RZ = 0.9  # the arch's height over WALL_Z
RIB_X = (-0.95, -0.55, -0.15, 0.25, 0.65, 1.05, 1.42)
STEPS = 16
GATE_HALF = 0.55
GATE_TOP = 0.8
HINGE = Vector((HALL_X[1] + 0.16, 0.0, PLINTH_H + 0.02))  # the gate's hinge, at its foot outside the portal
TOWER = (-1.95, 0.72)
MAST_TOP = 1.75
TURBINES = ((-0.9, -1.13), (-0.9, 1.13))
EXIT_POINT = (3.0, 0.0, 0.0)  # the exit cell, east of the middle row


def _arch(ry, rz, z0, legs=None):
    """A half ellipse over z0 in the YZ plane, from +ry to -ry; with legs, down to that height at both ends."""
    pts = [(ry * math.cos(a), z0 + rz * math.sin(a)) for a in (math.pi * i / STEPS for i in range(STEPS + 1))]
    return ([(ry, legs)] + pts + [(-ry, legs)]) if legs is not None else pts


def _band(ry, rz, z0, width, foot):
    """The outline of an arched band with legs down to foot: the outer arch, then the inner one back."""
    outer = _arch(ry, rz, z0, foot)
    inner = list(reversed(_arch(ry - width, rz - width, z0, foot)))
    return outer + inner


def _along_x(p, outline, x0, x1, part):
    """An outline in the YZ plane pushed along x from x0 to x1."""
    p.loft([[Vector((x, y, z)) for y, z in outline] for x in (x0, x1)], part)


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _pipe(p, a, c, b, radius, part):
    """A round pipe on a quadratic curve from a over c to b."""
    a, c, b = Vector(a), Vector(c), Vector(b)
    n = 9
    pts = [a * (1 - t) ** 2 + c * 2 * t * (1 - t) + b * t * t for t in (i / (n - 1) for i in range(n))]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, n - 1)] - pts[max(i - 1, 0)]).normalized()
        rings.append(_ring(q, d, radius, 10))
    p.loft(rings, part)


# --- body -------------------------------------------------------------------------

def _hall(p):
    x0, x1 = HALL_X
    _along_x(p, _arch(HALL_RY, HALL_RZ, WALL_Z, PLINTH_H), x0, x1, SKIN)  # the skin and its foot in one, no hidden faces
    for x in RIB_X:  # dark ribs across the skin
        _along_x(p, _band(HALL_RY + 0.03, HALL_RZ + 0.03, WALL_Z, 0.06, PLINTH_H), x - 0.035, x + 0.035, STEEL)
    p.box((x0, -0.07, WALL_Z + HALL_RZ - 0.02), (x1, 0.07, WALL_Z + HALL_RZ + 0.07), STEEL)  # the spine along the top
    # The dark opening in the east end, behind the gate.
    p.box((x1, -GATE_HALF, PLINTH_H), (x1 + 0.01, GATE_HALF, GATE_TOP), DOOR)


def _portal_lamps(p):
    """Portholes round the portal, lit."""
    x = HALL_X[1] + 0.14
    for i in range(1, 10):
        a = math.pi * i / 10
        y = (HALL_RY + 0.06) * math.cos(a)
        z = WALL_Z + (HALL_RZ + 0.06) * math.sin(a)
        p.turned((x, y, z), 'X', [(0.0, 0.035), (0.02, 0.035)], LAMP, sides=10)


def _pylon(p):
    """The tall horseshoe arch over the west end, its horns higher than the hall."""
    x = HALL_X[0]
    _along_x(p, _band(1.12, 1.5, 0.2, 0.2, PLINTH_H), x - 0.2, x - 0.02, STEEL)


def _tower(p):
    tx, ty = TOWER
    p.turned((tx, ty, 0.0), 'Z', [(PLINTH_H, 0.36), (0.14, 0.36), (0.16, 0.32), (0.55, 0.32), (0.6, 0.36)], SKIN, sides=20)
    for z in (0.28, 0.42):
        p.turned((tx, ty, 0.0), 'Z', [(z - 0.025, 0.335), (z + 0.025, 0.335)], STEEL, sides=20)
    dome = [(0.6 + 0.3 * math.sin(math.pi / 2 * i / 7), 0.34 * math.cos(math.pi / 2 * i / 7)) for i in range(7)]
    p.turned((tx, ty, 0.0), 'Z', dome + [(0.92, 0.04)], SKIN, sides=20)
    p.turned((tx, ty, 0.0), 'Z', [(0.9, 0.022), (MAST_TOP, 0.016)], STEEL, sides=6)


def _turbine(p, tx, ty):
    """A round housing with a fan grille on top: a rim, six spokes, a hub; its pipe into the hall."""
    p.turned((tx, ty, 0.0), 'Z', [(PLINTH_H, 0.3), (0.38, 0.3), (0.42, 0.27), (0.42, 0.22), (0.36, 0.22)], SKIN, sides=20)
    p.turned((tx, ty, 0.0), 'Z', [(0.3, 0.22), (0.37, 0.22)], DOOR, sides=20)  # the dark well under the grille
    for i in range(6):
        a = math.pi * i / 3
        p.bar((tx, ty, 0.39), (tx + 0.21 * math.cos(a), ty + 0.21 * math.sin(a), 0.39), 0.018, STEEL)
    p.turned((tx, ty, 0.0), 'Z', [(0.36, 0.05), (0.42, 0.05)], STEEL, sides=10)  # the hub
    side = 1 if ty > 0 else -1
    _pipe(p, (tx + 0.25, ty - side * 0.12, 0.22), (tx + 0.55, ty - side * 0.2, 0.2), (tx + 0.75, side * (HALL_RY - 0.02), 0.2),
          0.05, STEEL)


def _yard(p):
    for tx, ty in TURBINES:
        _turbine(p, tx, ty)
    for x, y, s in ((0.1, -1.2, 0.12), (0.26, -1.2, 0.12), (0.18, -1.33, 0.11), (0.18, -1.2, 0.0), (1.0, 1.15, 0.12),
                    (1.15, 1.2, 0.11), (1.08, 1.3, 0.1)):
        if s > 0:  # crates in heaps
            p.box((x - s / 2, y - s / 2, PLINTH_H), (x + s / 2, y + s / 2, PLINTH_H + s), CRATE)
    p.box((0.13, -1.26, PLINTH_H + 0.12), (0.24, -1.15, PLINTH_H + 0.23), CRATE)  # one on top
    for x, y in ((0.55, -1.25), (0.68, -1.3), (-0.3, 1.25), (1.9, 1.15), (1.95, -1.2), (-0.35, -1.28)):  # barrels and bollards
        p.turned((x, y, 0.0), 'Z', [(PLINTH_H, 0.055), (PLINTH_H + 0.15, 0.055), (PLINTH_H + 0.16, 0.045)], STEEL, sides=10)


def _gate():
    """The leaf, upright in the opening when shut; its object's origin is the hinge."""
    p = Parts()
    x = HINGE.x
    p.box((x - 0.03, -GATE_HALF + 0.02, HINGE.z), (x + 0.03, GATE_HALF - 0.02, GATE_TOP), STEEL)
    for i in range(6):  # the treads of the ramp it becomes
        z = HINGE.z + 0.08 + i * 0.105
        p.box((x + 0.03, -GATE_HALF + 0.06, z), (x + 0.045, GATE_HALF - 0.06, z + 0.04), SKIN)
    gate = p.finish(PREFIX + "gate")
    gate.data.transform(Matrix.Translation(-HINGE))
    gate.location = HINGE
    return gate


def _team(p):
    x1 = HALL_X[1]
    _along_x(p, _band(HALL_RY + 0.14, HALL_RZ + 0.14, WALL_Z, 0.16, PLINTH_H), x1 - 0.02, x1 + 0.13, TEAM)  # the portal
    for s in (-1, 1):  # the bands along the foot of the hall
        p.box((HALL_X[0], min(s * HALL_RY, s * (HALL_RY + 0.035)), WALL_Z - 0.06),
              (x1, max(s * HALL_RY, s * (HALL_RY + 0.035)), WALL_Z + 0.01), TEAM)
    tx, ty = TOWER
    p.box((tx + 0.02, ty - 0.008, MAST_TOP - 0.25), (tx + 0.44, ty + 0.008, MAST_TOP - 0.02), TEAM)  # the flag


# --- material -----------------------------------------------------------------------

def _material():
    """A dark plinth, a light blue-white skin in panels with a dark foot, dark steel, lit portholes, khaki crates."""
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
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    # The skin's panels: thin dark seams along the hall every 0.15 across it, and the dark foot under WALL_Z.
    seams = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 6.5)), -25.0, 1.0))
    foot = g.math('MULTIPLY', mask[1], g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, (WALL_Z - 0.04) * 60.0)))

    concrete = g.mix(grain, lin((0.26, 0.27, 0.28)), lin((0.33, 0.34, 0.35)))
    skin = g.mix(seams, g.mix(grain, lin((0.72, 0.76, 0.84)), lin((0.80, 0.84, 0.92))), lin((0.42, 0.45, 0.52)))
    steel = g.mix(grain, lin((0.22, 0.25, 0.30)), lin((0.29, 0.32, 0.38)))
    lamp = g.mix(grain, lin((0.95, 0.85, 0.45)), lin((1.0, 0.92, 0.55)))
    door = g.mix(grain, lin((0.06, 0.07, 0.08)), lin((0.09, 0.10, 0.11)))
    crate = g.mix(grain, lin((0.50, 0.46, 0.34)), lin((0.60, 0.55, 0.40)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (skin, steel, lamp, door, crate, team)):
        albedo = g.mix(m, albedo, col)
    albedo = g.mix(foot, albedo, steel)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.35, 0.45, 0.2, 0.7, 0.8, 0.5))
    metallic = weighted((0.0, 0.75, 0.8, 0.0, 0.3, 0.0, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', seams, 0.6)), 0.03, 0.005)
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
    _hall(body)
    _portal_lamps(body)
    _pylon(body)
    _tower(body)
    _yard(body)
    body = body.finish(PREFIX + "body")
    gate = _gate()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    cut = 0.35  # the plinth's corners cut
    outline = [(2.5, -1.5 + cut), (2.5, 1.5 - cut), (2.5 - cut, 1.5), (-2.5 + cut, 1.5), (-2.5, 1.5 - cut), (-2.5, -1.5 + cut),
               (-2.5 + cut, -1.5), (2.5 - cut, -1.5)]
    base.loft([[Vector((x, y, z)) for x, y in outline] for z in (0.0, PLINTH_H)], CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, gate, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the arches, the tower and the turbines smooth, edges sharp
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [_empty("exit_vehicle", EXIT_POINT)], mat, graph


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
