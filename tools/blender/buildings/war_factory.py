"""War factory: the Union's vehicle plant (docs/ra2_buildings/soviets/war_factory).

Footprint 5x3 cells (game/data/factions/union/buildings.yaml), the hall along x. The gate is in the east end of the hall (+X),
facing the exit cell east of the middle row, as RA2's; the tower stands in the middle of the west end. Origin at the centre of
the footprint on the ground. The composition follows the owner's reference; the parts are our own:
    hall      a long brick hall under a white gabled roof with steel ribs, a light cornice; red buttresses at the foot of both
              long walls and red slatted vents leaning out over the eaves
    gate      a steel frame in the east gable, a dark opening, and the gate itself: a leaf hinged at its foot that the game lets
              down outwards into a ramp while a unit comes out, and lifts again; a panel in the gable over it
    tower     a brick tower in the middle of the west end, a dark drum on it, a golden onion dome with a spire
    yard      brass shells on small plinths along the south wall, a tank at the south-west corner with a pipe into the end wall,
              barrels at the north-east corner
No emblems, no symbols of real armies or states, no text: the buttresses, the vents and the panel over the gate take the owner's
colour, where the reference has the red. Height 2.6 to the spire. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the hall and its roof, the frame and the opening, the tower, the shells, the tank, the pipe and the barrels
    gate            the gate leaf; its origin is the hinge at its foot, and it opens towards its +X turning about its Z
                    (Godot), as PlayerBuildings lets it down
    team_mask       the buttresses, the vents and the panel over the gate: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")
    exit_vehicle    empty on the ground in the exit cell: where the vehicles come out

In Blender, after tools/blender is on sys.path:
    import buildings.war_factory as wf
    print(wf.run())
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "war_factory"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, BRICK, STEEL, GOLD, DOOR, RED, TEAM = (_part(i) for i in range(7))
PARTS = 7

PLINTH_H = 0.08
HALL_X = (-1.75, 1.65)  # the west end, the east end with the gate
HALL_Y = (-1.0, 1.0)  # the south wall, the north wall
EAVE_Z = 1.05  # the top of the walls: the roof starts here (the material paints the brick under it, the roof over it)
RIDGE_Z = 1.5
OVERHANG = 0.07
ROOF_RIBS = (-1.3, -0.65, 0.0, 0.65, 1.3)  # steel ribs across the roof
SIDE_X = (-1.15, -0.4, 0.35, 1.1)  # the buttresses and the vents on both long walls
SHELL_X = (-0.78, -0.03, 0.72)
GATE_HALF = 0.6  # the opening's half-width
GATE_TOP = 0.9
HINGE = Vector((HALL_X[1] + 0.07, 0.0, PLINTH_H + 0.02))  # the gate's hinge, at its foot outside the east wall
TOWER = (-2.15, 0.0)
TANK = (-2.15, -1.05)
EXIT_POINT = (3.0, 0.0, 0.0)  # the exit cell, east of the middle row


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _pipe(p, points, radius, part):
    """A round pipe through the points."""
    pts = [Vector(q) for q in points]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        rings.append(_ring(q, d, radius, 12))
    p.loft(rings, part)


def _bezier(a, c, b, n):
    a, c, b = Vector(a), Vector(c), Vector(b)
    return [a * (1 - t) ** 2 + c * 2 * t * (1 - t) + b * t * t for t in (i / (n - 1) for i in range(n))]


# --- body -------------------------------------------------------------------------

def _hall(p):
    y0, y1 = HALL_Y
    mid = (y0 + y1) / 2
    # The walls and the gabled roof in one outline across the hall, pushed along x: no hidden faces between them. Its ends are
    # the gable walls.
    outline = [(y0, PLINTH_H), (y1, PLINTH_H), (y1, EAVE_Z), (y1 + OVERHANG, EAVE_Z), (y1 + OVERHANG, EAVE_Z + 0.04),
               (mid, RIDGE_Z), (y0 - OVERHANG, EAVE_Z + 0.04), (y0 - OVERHANG, EAVE_Z), (y0, EAVE_Z)]
    p.loft([[Vector((x, y, z)) for y, z in outline] for x in HALL_X], BRICK)
    rib = [(y0 - OVERHANG - 0.01, EAVE_Z + 0.03), (y1 + OVERHANG + 0.01, EAVE_Z + 0.03), (y1 + OVERHANG + 0.01, EAVE_Z + 0.08),
           (mid, RIDGE_Z + 0.05), (y0 - OVERHANG - 0.01, EAVE_Z + 0.08)]
    for x in ROOF_RIBS:  # steel ribs across the roof, a little over it
        p.loft([[Vector((x + dx, y, z)) for y, z in rib] for dx in (-0.03, 0.03)], STEEL)


def _gate_frame(p):
    x1 = HALL_X[1]
    p.box((x1 - 0.02, -GATE_HALF - 0.12, PLINTH_H), (x1 + 0.05, GATE_HALF + 0.12, GATE_TOP + 0.1), STEEL)  # the frame
    p.box((x1 + 0.05, -GATE_HALF, PLINTH_H), (x1 + 0.055, GATE_HALF, GATE_TOP), DOOR)  # the dark opening behind the gate
    for y in (-GATE_HALF - 0.08, GATE_HALF + 0.08):  # red posts at the sides of the opening
        p.bar((x1 + 0.09, y, PLINTH_H), (x1 + 0.09, y, 0.5), 0.03, RED)


def _gate():
    """The leaf, upright in the opening when shut; its object's origin is the hinge."""
    p = Parts()
    x = HINGE.x
    p.box((x - 0.03, -GATE_HALF + 0.02, HINGE.z), (x + 0.03, GATE_HALF - 0.02, GATE_TOP), STEEL)
    for y in (-0.36, 0.0, 0.36):  # red bands down the leaf: the ramp's treads when it lies down
        p.box((x + 0.03, y - 0.06, HINGE.z + 0.05), (x + 0.045, y + 0.06, GATE_TOP - 0.05), RED)
    gate = p.finish(PREFIX + "gate")
    gate.data.transform(Matrix.Translation(-HINGE))
    gate.location = HINGE
    return gate


def _tower(p):
    tx, ty = TOWER
    p.box((tx - 0.3, ty - 0.3, PLINTH_H), (tx + 0.3, ty + 0.3, 1.25), BRICK)
    p.box((tx - 0.34, ty - 0.34, 1.25), (tx + 0.34, ty + 0.34, 1.32), CONCRETE)  # its cornice
    p.turned((tx, ty, 0.0), 'Z', [(1.32, 0.24), (1.62, 0.24), (1.67, 0.28)], STEEL, sides=16)  # the drum
    p.turned((tx, ty, 0.0), 'Z', [(1.67, 0.22), (1.76, 0.3), (1.9, 0.33), (2.04, 0.29), (2.17, 0.18), (2.27, 0.08),
                                  (2.32, 0.045), (2.6, 0.012)], GOLD, sides=16)  # the onion dome and its spire


def _yard(p):
    y0, y1 = HALL_Y
    for x in SHELL_X:  # brass shells on small plinths along the south wall
        p.box((x - 0.09, y0 - 0.32, PLINTH_H), (x + 0.09, y0 - 0.14, PLINTH_H + 0.08), STEEL)
        p.turned((x, y0 - 0.23, 0.0), 'Z', [(0.16, 0.065), (0.32, 0.07), (0.42, 0.065), (0.49, 0.045), (0.53, 0.012)],
                 GOLD, sides=12)
    tx, ty = TANK  # the tank and its pipe into the west wall
    p.turned((tx, ty, 0.0), 'Z', [(PLINTH_H, 0.24), (0.56, 0.24), (0.64, 0.18), (0.68, 0.09)], STEEL, sides=16)
    _pipe(p, _bezier((tx, ty, 0.6), (tx, ty, 0.86), (HALL_X[0] - 0.02, y0 + 0.3, 0.78), 9), 0.06, STEEL)
    for x, y in ((2.2, 1.22), (2.38, 1.06)):  # barrels at the north-east corner
        p.turned((x, y, 0.0), 'Z', [(PLINTH_H, 0.07), (PLINTH_H + 0.2, 0.07), (PLINTH_H + 0.21, 0.06)], RED, sides=10)


def _team(p):
    y0, y1 = HALL_Y
    for x in SIDE_X:
        # The buttresses at the foot of both long walls.
        p.profile_x([(y0, PLINTH_H), (y0 - 0.32, PLINTH_H), (y0 - 0.32, 0.17), (y0, 0.56)], x - 0.12, x + 0.12, TEAM)
        p.profile_x([(y1, PLINTH_H), (y1, 0.56), (y1 + 0.32, 0.17), (y1 + 0.32, PLINTH_H)], x - 0.12, x + 0.12, TEAM)
        # The slatted vents over the eaves, leaning out.
        p.profile_x([(y0 + 0.08, 0.86), (y0 - 0.06, 0.86), (y0 - 0.22, 1.5), (y0 - 0.08, 1.5)], x - 0.16, x + 0.16, TEAM)
        p.profile_x([(y1 - 0.08, 0.86), (y1 + 0.08, 1.5), (y1 + 0.22, 1.5), (y1 + 0.06, 0.86)], x - 0.16, x + 0.16, TEAM)
    x1 = HALL_X[1]
    p.box((x1, -0.3, GATE_TOP + 0.14), (x1 + 0.03, 0.3, GATE_TOP + 0.34), TEAM)  # the panel in the gable over the gate


# --- material -----------------------------------------------------------------------

def _material():
    """Brick in courses with a light cornice, a white roof in panels, dark steel, gold and brass, red, a light team zone."""
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
    z = g.axis(coord, 'Z')
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 18.0)), -12.0, 1.0))
    panels = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 2.4)), -30.0, 1.0))
    roof = g.math('MULTIPLY', mask[1], g.clamp01(g.math('MULTIPLY_ADD', z, 80.0, -EAVE_Z * 80.0 + 0.5)))
    cornice = g.math('MULTIPLY', mask[1], g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 80.0, -(EAVE_Z - 0.08) * 80.0)),
                                                  g.clamp01(g.math('MULTIPLY_ADD', z, -80.0, EAVE_Z * 80.0 + 0.5))))

    concrete = g.mix(grain, lin((0.50, 0.49, 0.46)), lin((0.58, 0.57, 0.54)))
    brick = g.mix(courses, g.mix(grain, lin((0.62, 0.42, 0.32)), lin((0.72, 0.50, 0.38))), lin((0.42, 0.34, 0.30)))
    white = g.mix(panels, g.mix(grain, lin((0.82, 0.83, 0.84)), lin((0.90, 0.91, 0.92))), lin((0.40, 0.41, 0.43)))
    steel = g.mix(grain, lin((0.20, 0.21, 0.23)), lin((0.27, 0.28, 0.30)))
    gold = g.mix(grain, lin((0.78, 0.60, 0.22)), lin((0.88, 0.70, 0.30)))
    door = g.mix(grain, lin((0.06, 0.06, 0.07)), lin((0.09, 0.09, 0.10)))
    red = g.mix(grain, lin((0.72, 0.10, 0.08)), lin((0.82, 0.14, 0.11)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (brick, steel, gold, door, red, team)):
        albedo = g.mix(m, albedo, col)
    albedo = g.mix(cornice, albedo, lin((0.78, 0.76, 0.72)))
    albedo = g.mix(roof, albedo, white)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = g.math('MULTIPLY_ADD', roof, -0.4, weighted((0.85, 0.85, 0.45, 0.3, 0.8, 0.5, 0.5)))
    metallic = g.math('MULTIPLY_ADD', roof, 0.5, weighted((0.0, 0.0, 0.75, 0.9, 0.2, 0.1, 0.2)))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('ADD', courses, panels), 0.6)), 0.03, 0.005)
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
    _gate_frame(body)
    _tower(body)
    _yard(body)
    body = body.finish(PREFIX + "body")
    gate = _gate()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-2.5, -1.5, 0.0), (2.5, 1.5, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, gate, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the dome, the shells and the tank smooth, the edges sharp
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
