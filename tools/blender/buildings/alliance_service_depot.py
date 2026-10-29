"""Alliance service depot: the Alliance's repair bay (docs/ra2_buildings/allies/service_depot).

Footprint 3x3 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground; the camera
sees the south (-Y in Blender, +Z in Godot) and east (+X) sides. The composition follows the owner's references; the parts are
our own:
    pad       a round pad towards the south-east: a grate in the middle, a ring striped orange and black round it, four
              clamps at its sides
    housing   a rounded housing at the north-west corner, its long side towards the pad
    rail      a rail from the housing towards the pad, a carriage on it
    arm       a jointed arm on the carriage: a turning base, an upper arm, a forearm with the tool; the game moves it while the
              depot repairs (PlayerBuildings, RepairSystem.IsRepairing)
No emblems, no text. The cap of the housing and the clamps take the owner's colour, where the references have the blue.
Height 1.1 to the elbow. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the pad, the housing and the rail
    arm             the base of the arm on the carriage: turns about its Y; its origin on the turntable, its +X towards the pad
    arm_upper       under arm: the upper arm, turns about its Z (the shoulder) at its origin
    arm_fore        under arm_upper: the forearm with the tool, turns about its Z (the elbow) at its origin
    team_mask       the cap of the housing and the clamps: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_service_depot as asd
    print(asd.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_service_depot"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, STEEL, ORANGE, GREY, GRATE, HAZARD, LAMP, TEAM = (_part(i) for i in range(8))  # GRATE: painted inside HAZARD
PARTS = 8

PLINTH_H = 0.08
PAD = Vector((0.25, -0.25, 0.0))  # the pad's centre
PAD_R = 1.15  # the striped ring's outer radius
GRATE_R = 0.86
PAD_TOP = 0.12
HOUSING = Vector((-0.85, 0.8, 0.0))  # the housing's centre
TOWARDS = (PAD - HOUSING).normalized()  # from the housing to the pad
SIDE = Vector((-TOWARDS.y, TOWARDS.x, 0.0))
CARRIAGE = 0.85  # how far along the rail the carriage stands from the housing's centre
ARM_PIVOT_Z = 0.2
UPPER = 0.7  # the upper arm's length
FORE = 0.5  # the forearm's length


def _at(u, v, z):
    """A point in the housing's frame: u towards the pad, v across, z up."""
    return HOUSING + TOWARDS * u + SIDE * v + Vector((0.0, 0.0, z))


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _block(p, u0, u1, v0, v1, z0, z1, part):
    """A box in the housing's frame."""
    p.loft([[_at(u, v, z) for u, v in ((u0, v0), (u1, v0), (u1, v1), (u0, v1))] for z in (z0, z1)], part)


# --- body -------------------------------------------------------------------------

def _pad(p):
    # One disc: the material paints the grate inside GRATE_R and the stripes round it.
    p.turned(PAD, 'Z', [(PLINTH_H, PAD_R), (PAD_TOP, PAD_R), (PAD_TOP, 0.01)], HAZARD, sides=48)


def _housing(p):
    # The body: low at the pad's end, a rounded back, a channel along its top for the rail.
    _block(p, -0.35, 0.3, -0.3, 0.3, PLINTH_H, 0.42, GREY)
    back = _at(-0.35, 0.0, 0.3)
    p.loft([_ring(back + SIDE * s, SIDE, 0.22, 16) for s in (-0.3, 0.3)], GREY)  # the rounded back, lying across
    _block(p, -0.3, 0.3, -0.32, -0.3, 0.12, 0.38, STEEL)  # dark side panels
    _block(p, -0.3, 0.3, 0.3, 0.32, 0.12, 0.38, STEEL)
    for v in (-0.2, 0.2):  # a lamp at each side of the front
        _block(p, 0.3, 0.32, v - 0.05, v + 0.05, 0.3, 0.36, LAMP)


def _rail(p):
    for v in (-0.12, 0.12):
        _block(p, 0.3, 1.3, v - 0.025, v + 0.025, PLINTH_H, 0.14, STEEL)
    for u in (0.45, 0.65, 0.85, 1.05, 1.25):  # the sleepers
        _block(p, u - 0.03, u + 0.03, -0.18, 0.18, PLINTH_H, 0.1, STEEL)


def _team(p):
    _block(p, -0.33, 0.28, -0.28, 0.28, 0.42, 0.47, TEAM)  # the cap of the housing
    back = _at(-0.35, 0.0, 0.3)
    p.loft([_ring(back + SIDE * s, SIDE, 0.225, 16) for s in (-0.12, 0.12)], TEAM)  # the band over the rounded back
    for i in range(4):  # the clamps at the sides of the pad
        a = math.pi / 4 + i * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        corners = [PAD + out * r + side * s for r, s in ((GRATE_R - 0.1, -0.1), (PAD_R + 0.05, -0.12), (PAD_R + 0.05, 0.12),
                                                         (GRATE_R - 0.1, 0.1))]
        p.loft([[q + Vector((0.0, 0.0, z)) for q in corners] for z in (PAD_TOP, PAD_TOP + 0.07)], TEAM)


# --- the arm ----------------------------------------------------------------------

def _arm_nodes():
    """The arm in three nodes, each built along its own +X from its pivot, then posed: the base on the carriage turned towards the
    pad, the upper arm raised, the forearm bent down towards the middle of the pad."""
    base = Parts()
    base.box((-0.16, -0.17, -0.1), (0.16, 0.17, 0.0), STEEL)  # the carriage
    base.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.14), (0.08, 0.14), (0.1, 0.11)], GREY, sides=20)  # the turntable
    for y in (-0.1, 0.1):  # the cheeks of the shoulder
        base.box((-0.09, y - 0.025, 0.08), (0.09, y + 0.025, 0.24), ORANGE)
    base = base.finish(PREFIX + "arm")

    upper = Parts()
    upper.turned((0.0, 0.0, 0.0), 'Y', [(-0.075, 0.075), (0.075, 0.075)], GREY, sides=16)  # the shoulder joint
    upper.box((0.0, -0.075, -0.065), (UPPER, 0.075, 0.065), ORANGE)
    upper.box((0.08, -0.085, -0.04), (UPPER - 0.08, 0.085, 0.04), GREY)  # grey ribs along its sides
    upper.turned((UPPER, 0.0, 0.0), 'Y', [(-0.07, 0.07), (0.07, 0.07)], GREY, sides=16)  # the elbow joint
    upper = upper.finish(PREFIX + "arm_upper")

    fore = Parts()
    fore.box((0.0, -0.06, -0.055), (FORE, 0.06, 0.055), ORANGE)
    fore.box((0.06, -0.07, -0.03), (FORE - 0.06, 0.07, 0.03), GREY)
    fore.turned((0.0, 0.0, 0.0), 'X', [(FORE, 0.06), (FORE + 0.05, 0.07), (FORE + 0.1, 0.05), (FORE + 0.14, 0.05), (FORE + 0.2, 0.03),
                                       (FORE + 0.26, 0.01)], GREY, sides=6)  # the tool, stepped
    fore.sphere((FORE + 0.27, 0.0, 0.0), 'X', 0.025, LAMP, sides=8, rings=4)
    fore = fore.finish(PREFIX + "arm_fore")

    base.location = _at(CARRIAGE, 0.0, ARM_PIVOT_Z)
    base.rotation_euler = (0.0, 0.0, math.atan2(TOWARDS.y, TOWARDS.x))
    upper.parent = base
    upper.location = (0.0, 0.0, 0.16)
    upper.rotation_euler = (0.0, math.radians(-50), 0.0)  # raised 50°
    fore.parent = upper
    fore.location = (UPPER, 0.0, 0.0)
    fore.rotation_euler = (0.0, math.radians(110), 0.0)  # bent down to 60° under the level
    bpy.context.view_layer.update()
    return base, upper, fore


# --- material -----------------------------------------------------------------------

def _material():
    """Concrete, dark steel, orange, light grey, a dark grate in a grid, orange and black stripes, a lamp, a light team zone."""
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
    # The grate: bars every 0.1 both ways.
    bars = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 10.0)), -12.0, 1.0)),
                  g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 10.0)), -12.0, 1.0)))
    # The ring's stripes: 32 round the pad's centre.
    turn = g.math('MULTIPLY', g.math('ARCTAN2', g.math('SUBTRACT', y, PAD.y), g.math('SUBTRACT', x, PAD.x)), 16.0 / math.pi)
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', turn), 40.0, -20.0))
    dx, dy = g.math('SUBTRACT', x, PAD.x), g.math('SUBTRACT', y, PAD.y)
    radius = g.math('SQRT', g.math('ADD', g.math('MULTIPLY', dx, dx), g.math('MULTIPLY', dy, dy)))
    inside = g.math('MULTIPLY', mask[5], g.clamp01(g.math('MULTIPLY_ADD', radius, -80.0, GRATE_R * 80.0)))  # the grate of the pad

    concrete = g.mix(grain, lin((0.44, 0.44, 0.42)), lin((0.52, 0.52, 0.50)))
    steel = g.mix(grain, lin((0.18, 0.19, 0.21)), lin((0.25, 0.26, 0.28)))
    orange = g.mix(grain, lin((0.90, 0.48, 0.10)), lin((0.96, 0.56, 0.16)))
    grey = g.mix(grain, lin((0.66, 0.67, 0.69)), lin((0.74, 0.75, 0.77)))
    grate = g.mix(bars, g.mix(grain, lin((0.10, 0.11, 0.12)), lin((0.14, 0.15, 0.16))), lin((0.50, 0.52, 0.55)))
    hazard = g.mix(stripes, lin((0.92, 0.50, 0.12)), lin((0.08, 0.08, 0.08)))
    lamp = g.mix(grain, lin((0.55, 0.80, 1.0)), lin((0.65, 0.88, 1.0)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (steel, orange, grey, grate, hazard, lamp, team)):
        albedo = g.mix(m, albedo, col)
    albedo = g.mix(inside, albedo, grate)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.45, 0.4, 0.45, 0.5, 0.6, 0.2, 0.45))
    metallic = weighted((0.0, 0.7, 0.2, 0.5, 0.7, 0.1, 0.0, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', bars, inside), 0.8)), 0.03, 0.005)
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
    """The meshes (the arm's parent before its children) and the material."""
    _clear()
    body = Parts()
    _pad(body)
    _housing(body)
    _rail(body)
    body = body.finish(PREFIX + "body")
    arm, upper, fore = _arm_nodes()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-1.5, -1.5, 0.0), (1.5, 1.5, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, arm, upper, fore, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the joints and the pad smooth, the edges sharp
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
    # The root takes the parts with no parent of their own; the upper arm and the forearm stay under the arm.
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
