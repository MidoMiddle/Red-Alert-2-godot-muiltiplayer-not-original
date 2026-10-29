"""Service depot: the Union's repair yard (docs/ra2_buildings/soviets/service_depot).

Footprint 4x3 cells (game/data/factions/union/buildings_advanced.yaml): 4 along x, 3 along the depth. Origin at the centre of
the footprint on the ground. The composition follows the owner's references; the parts are our own:
    tower     on a dark slab with a trimmed edge in the west: a brick block with a stepped top and trims, a yellow tank lying at
              its front, pumps, a ladder; a lattice mast over it
    crane     on the mast a cab with a counter-jib and its weight, a lattice jib towards the pad, a stay from the cab's peak to
              the jib's end; it swings while the depot repairs
    hook      a cable from the jib's end and a red block with a hook on it, over the pad; it goes down and up while the depot
              repairs
    pad       an eight-sided concrete pad in the east, a square striped frame in its middle
No emblems, no text. The trims of the tower and the slab and the cab take the owner's colour, where the references have the red.
Height 2.4 to the cab's peak. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the slab, the tower, the tank, the pumps, the ladder, the mast and the pad
    crane           the cab, the counter-jib and the jib: turns about its Y at its origin, the top of the mast
    cable           under crane: the cable, its origin at the jib's end, hanging down along -Y; the game stretches it down to the hook
    hook            under crane: the block and the hook, its origin at the jib's end too; the game lowers it
    team_mask       the trims and the cab: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.service_depot as sd
    print(sd.run())
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "service_depot"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, BRICK, STEEL, RED, TAN, HAZARD, TEAM = (_part(i) for i in range(7))
PARTS = 7

PLINTH_H = 0.06
SLAB = (-2.0, -0.72, -1.15, 1.15)  # x0, x1, y0, y1 of the tower's slab
SLAB_TOP = 0.14
TOWER = (-1.85, -0.95, -0.55, 0.75)  # x0, x1, y0, y1 of the brick block
TOWER_TOP = 0.95
STEP_TOP = 1.25
MAST = Vector((-1.35, 0.2, 0.0))
MAST_TOP = 2.0
PAD = Vector((0.65, -0.1, 0.0))
PAD_R = 1.25
PAD_TOP = 0.1
JIB = 1.95  # the jib's reach from the mast's axis
JIB_RISE = 0.12
HOOK_Z = 0.75  # where the hook hangs at rest


def _octagon(c, r, z):
    return [Vector((c.x + r * math.cos(a), c.y + r * math.sin(a), z)) for a in (math.pi / 8 + i * math.pi / 4 for i in range(8))]


# --- body -------------------------------------------------------------------------

def _tower(p):
    x0, x1, y0, y1 = SLAB
    p.box((x0, y0, PLINTH_H), (x1, y1, SLAB_TOP), STEEL)  # the dark slab
    tx0, tx1, ty0, ty1 = TOWER
    p.box((tx0, ty0, SLAB_TOP), (tx1, ty1, TOWER_TOP), BRICK)
    p.box((tx0 + 0.12, ty0 + 0.12, TOWER_TOP), (tx1 - 0.12, ty1 - 0.12, STEP_TOP), BRICK)  # the stepped top
    # The tank lying at the front, a pump beside it, a ladder up the east face.
    tank = Vector((-1.45, -0.82, 0.34))
    p.turned(tank, 'X', [(-0.3, 0.15), (-0.27, 0.19), (0.27, 0.19), (0.3, 0.15)], TAN, sides=16)
    p.box((-1.12, -1.0, SLAB_TOP), (-0.85, -0.68, 0.42), RED)
    p.turned((-0.98, -0.84, 0.0), 'Z', [(0.42, 0.08), (0.5, 0.08)], STEEL, sides=10)
    for y in (-0.15, 0.15):  # the ladder's rails
        p.box((tx1, y - 0.015, SLAB_TOP), (tx1 + 0.03, y + 0.015, TOWER_TOP), RED)
    for i in range(7):
        z = SLAB_TOP + 0.1 + i * 0.11
        p.box((tx1, -0.15, z), (tx1 + 0.03, 0.15, z + 0.02), RED)


def _mast(p):
    """A lattice mast from the stepped top to MAST_TOP: four legs, braces on every face."""
    h = 0.11
    z0, z1 = STEP_TOP, MAST_TOP - 0.05
    legs = [Vector((MAST.x + sx * h, MAST.y + sy * h, 0.0)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))]
    for q in legs:
        p.bar(q + Vector((0, 0, z0)), q + Vector((0, 0, z1)), 0.022, STEEL)
    steps = 4
    for i in range(steps):
        za, zb = z0 + (z1 - z0) * i / steps, z0 + (z1 - z0) * (i + 1) / steps
        for k in range(4):
            a, b = legs[k], legs[(k + 1) % 4]
            p.bar(a + Vector((0, 0, za)), b + Vector((0, 0, zb)), 0.012, STEEL)
            p.bar(a + Vector((0, 0, zb)), b + Vector((0, 0, zb)), 0.012, STEEL)
    p.turned(MAST, 'Z', [(MAST_TOP - 0.06, 0.17), (MAST_TOP, 0.17)], STEEL, sides=16)  # the slewing ring


def _pad(p):
    p.loft([_octagon(PAD, PAD_R, z) for z in (PLINTH_H, PAD_TOP)], CONCRETE)
    h, w = 0.52, 0.09  # the striped square frame
    c = PAD
    for lo, hi in (((c.x - h, c.y - h), (c.x + h, c.y - h + w)), ((c.x - h, c.y + h - w), (c.x + h, c.y + h)),
                   ((c.x - h, c.y - h + w), (c.x - h + w, c.y + h - w)), ((c.x + h - w, c.y - h + w), (c.x + h, c.y + h - w))):
        p.box((lo[0], lo[1], PAD_TOP), (hi[0], hi[1], PAD_TOP + 0.01), HAZARD)


# --- the crane --------------------------------------------------------------------

def _crane_nodes():
    """The turning crane and, under it, the cable and the hook. The crane is built along its +X towards the pad from its origin
    on the mast's top, then turned to face the pad."""
    tip = Vector((JIB, 0.0, JIB_RISE))
    crane = Parts()
    crane.box((-0.5, -0.1, 0.0), (0.0, 0.1, 0.08), STEEL)  # the counter-jib
    crane.box((-0.55, -0.14, -0.02), (-0.38, 0.14, 0.2), STEEL)  # its weight
    for y in (-0.08, 0.08):  # the jib: two lower chords, one upper, zigzag braces
        crane.bar((0.1, y, 0.05), tip + Vector((0.0, y * 0.4, -0.02)), 0.018, STEEL)
    crane.bar((0.1, 0.0, 0.2), tip + Vector((0.0, 0.0, 0.03)), 0.018, STEEL)
    n = 9
    for i in range(n):
        t0, t1 = i / n, (i + 1) / n
        for y in (-0.08, 0.08):
            a = Vector((0.1, y, 0.05)).lerp(tip + Vector((0.0, y * 0.4, -0.02)), t0)
            b = Vector((0.1, 0.0, 0.2)).lerp(tip + Vector((0.0, 0.0, 0.03)), t1)
            crane.bar(a, b, 0.01, STEEL)
    crane.bar((-0.05, 0.0, 0.42), tip + Vector((0.0, 0.0, 0.03)), 0.008, STEEL)  # the stay from the peak to the end
    crane.bar((-0.05, 0.0, 0.42), (-0.5, 0.0, 0.08), 0.008, STEEL)  # and to the counter-jib
    crane.box((tip.x - 0.05, -0.05, tip.z - 0.06), (tip.x + 0.05, 0.05, tip.z + 0.04), STEEL)  # the sheave at the end
    crane = crane.finish(PREFIX + "crane")

    drop = MAST_TOP + JIB_RISE - 0.06 - HOOK_Z  # from the jib's end down to the top of the hook's block
    cable = Parts()
    cable.turned((0.0, 0.0, 0.0), 'Z', [(-drop, 0.012), (0.0, 0.012)], STEEL, sides=6)
    cable = cable.finish(PREFIX + "cable")

    hook = Parts()
    hook.box((-0.06, -0.08, -drop - 0.14), (0.06, 0.08, -drop), RED)  # the block
    hook.turned((0.0, 0.0, 0.0), 'Z', [(-drop - 0.2, 0.015), (-drop - 0.14, 0.015)], STEEL, sides=6)
    hook.bar((0.0, 0.0, -drop - 0.2), (0.06, 0.0, -drop - 0.26), 0.015, STEEL)  # the hook's curve
    hook.bar((0.06, 0.0, -drop - 0.26), (0.08, 0.0, -drop - 0.2), 0.015, STEEL)
    hook = hook.finish(PREFIX + "hook")

    crane.location = MAST + Vector((0.0, 0.0, MAST_TOP))
    towards = PAD - MAST
    crane.rotation_euler = (0.0, 0.0, math.atan2(towards.y, towards.x))
    for node in (cable, hook):
        node.parent = crane
        node.location = tip + Vector((0.0, 0.0, -0.06))
    bpy.context.view_layer.update()
    return crane, cable, hook


def _team(p):
    x0, x1, y0, y1 = SLAB
    w = 0.06
    for lo, hi in (((x0, y0), (x1, y0 + w)), ((x0, y1 - w), (x1, y1)), ((x0, y0 + w), (x0 + w, y1 - w)), ((x1 - w, y0 + w), (x1, y1 - w))):
        p.box((lo[0], lo[1], SLAB_TOP), (hi[0], hi[1], SLAB_TOP + 0.04), TEAM)  # the trimmed edge of the slab
    tx0, tx1, ty0, ty1 = TOWER
    p.box((tx0 - 0.02, ty0 - 0.02, TOWER_TOP - 0.06), (tx1 + 0.02, ty1 + 0.02, TOWER_TOP), TEAM)  # the trims of the tower
    p.box((tx0 + 0.1, ty0 + 0.1, STEP_TOP - 0.05), (tx1 - 0.1, ty1 - 0.1, STEP_TOP), TEAM)


def _cab():
    """The cab on the crane, in the owner's colour: a separate mesh under the crane, so it turns with it."""
    p = Parts()
    p.box((-0.18, -0.15, 0.0), (0.12, 0.15, 0.28), TEAM)
    p.loft([[Vector((x, y, 0.28)) for x, y in ((-0.18, -0.15), (0.12, -0.15), (0.12, 0.15), (-0.18, 0.15))],
            [Vector((x, y, 0.42)) for x, y in ((-0.08, -0.04), (0.0, -0.04), (0.0, 0.04), (-0.08, 0.04))]], TEAM)  # its peak
    return p.finish(PREFIX + "team_mask_cab")


# --- material -----------------------------------------------------------------------

def _material():
    """Weathered concrete, brick in courses, dark steel, red, a yellow tank, yellow and black stripes, a light team zone."""
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
    stains = g.noise(4.0, 5.0)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 14.0)), -10.0, 1.0))
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 5.0)), 40.0, -20.0))

    concrete = g.mix(stains, lin((0.50, 0.50, 0.48)), lin((0.62, 0.62, 0.60)))
    brick = g.mix(courses, g.mix(grain, lin((0.66, 0.52, 0.40)), lin((0.74, 0.60, 0.46))), lin((0.46, 0.38, 0.32)))
    steel = g.mix(grain, lin((0.15, 0.15, 0.16)), lin((0.22, 0.22, 0.23)))
    red = g.mix(grain, lin((0.72, 0.10, 0.08)), lin((0.82, 0.14, 0.11)))
    tan = g.mix(grain, lin((0.78, 0.66, 0.30)), lin((0.86, 0.74, 0.36)))
    hazard = g.mix(stripes, lin((0.90, 0.74, 0.12)), lin((0.08, 0.08, 0.08)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (brick, steel, red, tan, hazard, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.9, 0.85, 0.5, 0.5, 0.45, 0.6, 0.5))
    metallic = weighted((0.0, 0.0, 0.7, 0.2, 0.5, 0.0, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', courses, mask[1]), 0.7)), 0.03, 0.005)
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
    """The meshes (each parent before its children) and the material."""
    _clear()
    body = Parts()
    _tower(body)
    _mast(body)
    _pad(body)
    body = body.finish(PREFIX + "body")
    crane, cable, hook = _crane_nodes()
    cab = _cab()
    cab.parent = crane
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-2.0, -1.5, 0.0), (2.0, 1.5, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, crane, cab, cable, hook, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the tank and the ring smooth, the edges sharp
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
    # The root takes the parts with no parent of their own; the cab, the cable and the hook stay under the crane.
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
