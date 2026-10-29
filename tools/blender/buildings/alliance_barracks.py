"""Alliance barracks: the Alliance's infantry building (docs/ra2_buildings/allies/barracks).

Footprint 2x3 cells (game/data/factions/alliance/buildings.yaml): 2 along x, 3 along the depth; the front, with the doors,
looks south (-Y in Blender, +Z in Godot), where the exit cell is. Origin at the centre of the footprint on the ground.
The composition follows the owner's reference; the parts are our own:
    halls     two arched halls side by side along the depth, on a low wall: light metal skin, dark steel ribs, an arched
              frame on the front with a door in it, a row of green lit windows along each outer side
    tower     a round watchtower behind the left hall: a ringed shaft, a cabin with a dark window band, a dome, a mast with
              a flag
    yard      striped pads in front of the doors, three barrels by the right hall
No emblems, no text. The flag, the front frames and the rails along the halls take the owner's colour, where the reference
has the blue. Height 2.5 to the top of the mast. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the halls, the doors, the windows, the tower, the pads and the barrels
    team_mask       the flag, the front frames of the halls and the rails along them: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")
    exit_infantry   empty on the ground in front of the right hall's door: where the infantry comes out

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_barracks as ab
    print(ab.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_barracks"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, STEEL, SKIN, GLASS, HAZARD, DOOR, TEAM = (_part(i) for i in range(7))
PARTS = 7

PLINTH_H = 0.08
WALL_Z = 0.30  # the top of the halls' low walls, where the arches spring
ARCH_RX = 0.44  # the arch's half-width
ARCH_RZ = 0.62  # its height over the wall
HALL_X = (-0.48, 0.48)  # the halls' centre lines
HALL_Y = (-1.2, 0.75)  # the halls' front and back
RIB_Y = (-0.6, 0.0, 0.6)
ARCH_STEPS = 16
TOWER = (-0.45, 1.12)  # the watchtower's centre in plan
SHAFT_TOP = 1.30  # the top of the tower's shaft, the cabin's floor
MAST_TOP = 2.5
EXIT_POINT = (0.48, -1.7, 0.0)  # in front of the right hall's door


def _arch(cx, rx, rz, z0, inner=None):
    """The outline of an arch over the line z0 at x = cx, in the XZ plane: a half ellipse closed by its foot; with inner, a band
    between the half ellipse and a smaller one inside it (rx and rz less inner)."""
    outer = [(cx + rx * math.cos(a), z0 + rz * math.sin(a)) for a in (math.pi * i / ARCH_STEPS for i in range(ARCH_STEPS + 1))]
    if inner is None:
        return outer
    back = [(cx + (rx - inner) * math.cos(a), z0 + (rz - inner) * math.sin(a))
            for a in (math.pi * i / ARCH_STEPS for i in reversed(range(ARCH_STEPS + 1)))]
    return outer + back


def _along_y(p, outline, y0, y1, part):
    """An outline in the XZ plane pushed along y from y0 to y1."""
    p.loft([[Vector((x, y, z)) for x, z in outline] for y in (y0, y1)], part)


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


# --- body -------------------------------------------------------------------------

def _hall(p, cx):
    y0, y1 = HALL_Y
    # The low wall and the arched skin in one: the material paints the band under WALL_Z as concrete. Its ends are the front
    # and back walls.
    outline = [(cx + ARCH_RX, PLINTH_H)] + _arch(cx, ARCH_RX, ARCH_RZ, WALL_Z) + [(cx - ARCH_RX, PLINTH_H)]
    _along_y(p, outline, y0, y1, SKIN)
    for y in RIB_Y:
        _along_y(p, _arch(cx, ARCH_RX + 0.03, ARCH_RZ + 0.03, WALL_Z, inner=0.05), y - 0.03, y + 0.03, STEEL)
    _along_y(p, _arch(cx, ARCH_RX + 0.03, ARCH_RZ + 0.03, WALL_Z, inner=0.07), y1 - 0.06, y1 + 0.02, STEEL)  # back frame
    # The door in the front wall, under its steel lintel, and a lamp at each side of it.
    p.box((cx - 0.15, y0 - 0.04, PLINTH_H), (cx + 0.15, y0 + 0.02, 0.56), DOOR)
    p.box((cx - 0.19, y0 - 0.06, 0.56), (cx + 0.19, y0 + 0.02, 0.62), STEEL)
    for s in (-1, 1):
        p.box((cx + s * 0.19 - 0.025, y0 - 0.05, PLINTH_H), (cx + s * 0.19 + 0.025, y0 + 0.02, 0.56), STEEL)  # the door posts
        p.box((cx + s * 0.27 - 0.035, y0 - 0.04, 0.44), (cx + s * 0.27 + 0.035, y0 + 0.01, 0.52), GLASS)
    # The lit windows along the outer side.
    side = 1 if cx > 0 else -1
    x = cx + side * ARCH_RX
    for y in (-0.9, -0.5, -0.1, 0.3):
        p.box((min(x, x + side * 0.02), y - 0.12, 0.14), (max(x, x + side * 0.02), y + 0.12, 0.24), GLASS)


def _tower(p):
    tx, ty = TOWER
    top = SHAFT_TOP
    p.turned((tx, ty, 0.0), 'Z', [(PLINTH_H, 0.29), (0.16, 0.29), (0.18, 0.25), (top - 0.04, 0.23), (top, 0.30)], SKIN, sides=16)
    for z in (0.5, 0.9):
        p.turned((tx, ty, 0.0), 'Z', [(z - 0.025, 0.25), (z + 0.025, 0.25)], STEEL, sides=16)  # rings round the shaft
    p.turned((tx, ty, 0.0), 'Z', [(top, 0.32), (top + 0.08, 0.32)], STEEL, sides=16)  # the cabin's floor
    p.turned((tx, ty, 0.0), 'Z', [(top + 0.08, 0.29), (top + 0.27, 0.29)], DOOR, sides=16)  # the window band
    p.turned((tx, ty, 0.0), 'Z', [(top + 0.27, 0.31), (top + 0.32, 0.31)], STEEL, sides=16)
    dome = [(top + 0.32 + 0.27 * math.sin(math.pi / 2 * i / 6), 0.30 * math.cos(math.pi / 2 * i / 6)) for i in range(6)]
    p.turned((tx, ty, 0.0), 'Z', dome + [(top + 0.6, 0.04)], SKIN, sides=16)  # the dome
    p.turned((tx, ty, 0.0), 'Z', [(top + 0.58, 0.024), (MAST_TOP, 0.018)], STEEL, sides=6)  # the mast


def _yard(p):
    for cx in HALL_X:  # a striped pad in front of each door
        p.box((cx - 0.26, -1.48, PLINTH_H), (cx + 0.26, HALL_Y[0] - 0.04, PLINTH_H + 0.015), HAZARD)
    for x, y in ((0.97 - 0.09, -0.3), (0.97 - 0.09, -0.08), (0.97 - 0.27, 1.25)):  # barrels by the right side and at the back
        p.turned((x, y + 0.0, 0.0), 'Z', [(PLINTH_H, 0.075), (PLINTH_H + 0.2, 0.075), (PLINTH_H + 0.21, 0.065)], STEEL, sides=10)


def _team(p):
    y0 = HALL_Y[0]
    for cx in HALL_X:
        _along_y(p, _arch(cx, ARCH_RX + 0.04, ARCH_RZ + 0.04, WALL_Z, inner=0.08), y0 - 0.05, y0 + 0.03, TEAM)  # front frame
        for s in (-1, 1):  # the rails along both sides, on the top of the low wall
            x = cx + s * ARCH_RX
            p.box((min(x, x + s * 0.035), y0, WALL_Z - 0.05), (max(x, x + s * 0.035), HALL_Y[1], WALL_Z + 0.01), TEAM)
    tx, ty = TOWER
    p.box((tx + 0.02, ty - 0.008, MAST_TOP - 0.24), (tx + 0.42, ty + 0.008, MAST_TOP - 0.02), TEAM)  # the flag


# --- material -----------------------------------------------------------------------

def _material():
    """Grey concrete, dark steel, a light metal skin in panels, green lit glass, yellow and black stripes, a dark door."""
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
    wall = g.math('MULTIPLY', mask[2], g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, WALL_Z * 60.0)))  # the skin's foot: the wall
    # The skin's panels: a thin dark seam every 0.2 along the halls.
    seams = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 5.0)), -25.0, 1.0))
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 6.0)), 40.0, -20.0))

    concrete = g.mix(grain, lin((0.40, 0.40, 0.38)), lin((0.48, 0.48, 0.46)))
    steel = g.mix(grain, lin((0.24, 0.26, 0.29)), lin((0.31, 0.33, 0.36)))
    skin = g.mix(seams, g.mix(grain, lin((0.76, 0.78, 0.80)), lin((0.84, 0.86, 0.88))), lin((0.50, 0.52, 0.55)))
    glass = g.mix(grain, lin((0.20, 0.72, 0.36)), lin((0.30, 0.85, 0.45)))
    hazard = g.mix(stripes, lin((0.86, 0.68, 0.10)), lin((0.10, 0.10, 0.10)))
    door = g.mix(grain, lin((0.14, 0.15, 0.17)), lin((0.19, 0.20, 0.22)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (steel, skin, glass, hazard, door, team)):
        albedo = g.mix(m, albedo, col)
    albedo = g.mix(wall, albedo, concrete)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.45, 0.4, 0.15, 0.7, 0.5, 0.5))
    metallic = weighted((0.0, 0.8, 0.7, 0.0, 0.0, 0.6, 0.3))
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
    for cx in HALL_X:
        _hall(body, cx)
    _tower(body)
    _yard(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-1.0, -1.5, 0.0), (1.0, 1.5, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the arches and the tower smooth, the edges sharp
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
