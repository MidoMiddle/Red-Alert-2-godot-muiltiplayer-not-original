"""Alliance power plant: the Alliance's power plant, +200 (docs/ra2_buildings/allies/power_plant).

Footprint 2x2 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    plinth    a round plinth with a bronze ring on it
    housings  three blocks round the middle, a third of a turn apart, the gap between two of them at the front (south, the side
              the camera sees): flared at the foot with vents, a chrome plate on the outer face, a chrome cap
    stacks    a tall chrome stack over each housing, out of a cone, hollow at the top, a clamp near the top; the back one taller
    core      a dark column in the middle wound with copper coils, a copper bowl on top
    terminal  a chrome post with two cross arms at the front, in the gap
No emblems, no text. The housings and the clamps take the owner's colour, where the references have the blue. Height 1.95 to the
top of the back stack. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the bronze ring, the stacks, the plates, the core, the bowl and the terminal
    team_mask       the three housings and the clamps of the stacks: the game paints them in the owner's colour
    base            the round plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_power_plant as app
    print(app.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_power_plant"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


BRONZE, STEEL, CHROME, COPPER, DARK, TEAM = (_part(i) for i in range(6))
PARTS = 6

PLINTH_H = 0.06
RING_Z = 0.1  # the top of the bronze ring: the housings stand on it
# The housings: the angle of each (radians; the gap is at -90°, the front), how far its stack stands out, its stack's top.
HOUSINGS = ((math.radians(90), 2.0), (math.radians(210), 1.78), (math.radians(330), 1.78))
STACK_R = 0.4  # the stacks' distance from the middle
HOUSING_Z = 0.82
SPAN = math.radians(30)  # half the angle a housing takes


def _sector(angle, r0, r1, half, z, steps=6):
    """An annular sector round the middle at height z: the outer arc from r1, then the inner one back at r0."""
    outer = [Vector((r1 * math.cos(a), r1 * math.sin(a), z))
             for a in (angle - half + 2 * half * i / steps for i in range(steps + 1))]
    inner = [Vector((r0 * math.cos(a), r0 * math.sin(a), z))
             for a in (angle + half - 2 * half * i / steps for i in range(steps + 1))]
    return outer + inner


def _block(p, angle, r0, half, levels, part):
    """A block over the sector, its outer radius at each height from levels: (z, r1)."""
    p.loft([_sector(angle, r0, r1, half, z) for z, r1 in levels], part)


# --- body -------------------------------------------------------------------------

def _stack(p, angle, top):
    x, y = STACK_R * math.cos(angle), STACK_R * math.sin(angle)
    # Out of a cone on the housing, a straight stack, a lip at the top and the dark mouth inside it.
    p.turned((x, y, 0.0), 'Z', [(HOUSING_Z - 0.05, 0.2), (HOUSING_Z + 0.03, 0.2), (HOUSING_Z + 0.25, 0.13), (top - 0.08, 0.13),
                                (top - 0.06, 0.15), (top, 0.15), (top, 0.11)], CHROME, sides=20)
    p.turned((x, y, 0.0), 'Z', [(top - 0.12, 0.11), (top - 0.002, 0.11)], DARK, sides=20)
    p.turned((x, y, 0.0), 'Z', [(HOUSING_Z + 0.25, 0.145), (HOUSING_Z + 0.3, 0.145)], COPPER, sides=20)  # a copper band
    # Copper cables down the stack's side towards the middle.
    inward = Vector((-math.cos(angle), -math.sin(angle), 0.0))
    side = Vector((-math.sin(angle), math.cos(angle), 0.0))
    for s in (-1, 1):
        foot = Vector((x, y, RING_Z)) + inward * 0.2 + side * s * 0.13
        p.bar(foot, foot + Vector((0.0, 0.0, HOUSING_Z + 0.35)), 0.018, COPPER)


def _housing_trim(p, angle):
    """The chrome plate on the housing's outer face and its chrome cap."""
    _block(p, angle, 0.72, SPAN * 0.6, [(0.38, 0.79), (0.72, 0.79)], CHROME)
    _block(p, angle, 0.24, SPAN + 0.02, [(HOUSING_Z, 0.79), (HOUSING_Z + 0.04, 0.79)], CHROME)
    for s in (-1, 1):  # vents in the flared foot
        a = angle + s * SPAN * 0.45
        p.turned((0.86 * math.cos(a), 0.86 * math.sin(a), 0.0), 'Z', [(0.12, 0.035), (0.2, 0.035)], DARK, sides=10)


def _core(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(RING_Z, 0.16), (0.3, 0.14), (1.0, 0.11)], STEEL, sides=16)  # the column
    for z in (0.5, 0.58, 0.66, 0.74, 0.82):  # the coils
        p.turned((0.0, 0.0, 0.0), 'Z', [(z - 0.03, 0.13), (z, 0.17), (z + 0.03, 0.13)], COPPER, sides=16)
    # The bowl: up the outside, over the lip, down the inside.
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.98, 0.07), (1.06, 0.13), (1.16, 0.24), (1.24, 0.31), (1.27, 0.31), (1.25, 0.27),
                                     (1.18, 0.2), (1.12, 0.06)], COPPER, sides=24)


def _terminal(p):
    """A chrome post with two cross arms at the front, in the gap between the housings."""
    x, y = 0.0, -0.62
    p.turned((x, y, 0.0), 'Z', [(RING_Z, 0.07), (RING_Z + 0.04, 0.05), (0.72, 0.035)], CHROME, sides=12)
    p.sphere((x, y, 0.76), 'Z', 0.055, CHROME, sides=12, rings=6)
    for z in (0.38, 0.58):
        p.bar((x - 0.2, y, z), (x + 0.2, y, z), 0.016, CHROME)
        for s in (-1, 1):
            p.bar((x + s * 0.2, y, z), (x + s * 0.23, y, z + 0.06), 0.016, CHROME)
        p.turned((x, y, 0.0), 'Z', [(z - 0.025, 0.05), (z + 0.025, 0.05)], CHROME, sides=12)


def _body(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLINTH_H, 0.95), (RING_Z, 0.93), (RING_Z, 0.3)], BRONZE, sides=36)  # the bronze ring
    for angle, top in HOUSINGS:
        _stack(p, angle, top)
        _housing_trim(p, angle)
    _core(p)
    _terminal(p)


def _team(p):
    for angle, top in HOUSINGS:
        # The housing: flared at the foot, straight above.
        _block(p, angle, 0.24, SPAN, [(RING_Z, 0.92), (0.3, 0.8), (HOUSING_Z, 0.77)], TEAM)
        x, y = STACK_R * math.cos(angle), STACK_R * math.sin(angle)
        p.turned((x, y, 0.0), 'Z', [(top - 0.42, 0.155), (top - 0.28, 0.155)], TEAM, sides=20)  # the clamp near the top


# --- material -----------------------------------------------------------------------

def _material():
    """Bronze, dark steel, polished chrome, copper, the dark mouths and vents, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.15)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    grain = g.noise(30.0, 4.0)
    streaks = g.noise(8.0, 6.0)

    bronze = g.mix(streaks, lin((0.36, 0.25, 0.14)), lin((0.50, 0.36, 0.20)))
    steel = g.mix(grain, lin((0.22, 0.23, 0.26)), lin((0.30, 0.31, 0.34)))
    chrome = g.mix(grain, lin((0.72, 0.73, 0.76)), lin((0.84, 0.85, 0.87)))
    copper = g.mix(grain, lin((0.78, 0.48, 0.22)), lin((0.88, 0.58, 0.28)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.08, 0.08, 0.09)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = bronze
    for m, col in zip(mask[1:], (steel, chrome, copper, dark, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.55, 0.45, 0.3, 0.35, 0.8, 0.45))
    metallic = weighted((0.6, 0.7, 0.6, 0.7, 0.2, 0.4))  # not 1: a full metal mirrors the dark sky
    normal = g.bump(g.noise(60.0, 3.0), 0.02, 0.004)
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
    _clear()
    body = Parts()
    _body(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.99), (PLINTH_H, 0.99)], BRONZE, sides=36)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the stacks, the bowl and the coils smooth, edges sharp
    uv_together(meshes)
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
