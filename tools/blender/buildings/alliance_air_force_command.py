"""Alliance air force command: the Alliance's radar and airfield (docs/ra2_buildings/allies/air_force_command).

Footprint 3x2 cells (game/data/factions/alliance/buildings.yaml): 3 along x, 2 along the depth. The four pads its jets land on
are its two east columns (AmmoSystem.PadCell): a square of 2x2 cells; the buildings stand in the west column. Origin at the
centre of the footprint on the ground. The composition follows the owner's references; the parts are our own:
    tower     the main tower in the middle of the west column: a ringed drum, a neck, a glazed six-sided cabin, and over it a
              curved radar array that turns
    towers    two round towers north and south of it, flared at the foot, with antennas
    entrance  a small domed pavilion in front of the main tower, steps down to the pads
    pads      four square pads in frames, a lit cross between them, bollards at the corners
No emblems, no text. The bands of the towers and the frames of the pads take the owner's colour, where the references have the
blue. Height 1.9 to the top of the array. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the towers, the entrance, the pads and the bollards
    spin_radar      the radar array; its origin is on the main tower's axis, and the game turns it about its Y while the owner
                    has power (PlayerBuildings)
    team_mask       the bands of the towers and the frames of the pads: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_air_force_command as afc
    print(afc.run())
"""

import math
import os

import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_air_force_command"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, WHITE, STEEL, GLASS, PAD, LIGHT, TEAM = (_part(i) for i in range(7))
PARTS = 7

PLINTH_H = 0.08
PAD_Z = 0.11  # the top of the pads' slab
PADS_X = (-0.5, 1.5)  # the pads' square, the two east columns
MAIN = Vector((-1.08, 0.0, 0.0))  # the main tower's axis
CABIN_TOP = 1.4
RADAR_PIVOT = Vector((MAIN.x, MAIN.y, CABIN_TOP + 0.05))
SIDE_TOWERS = (Vector((-1.05, 0.64, 0.0)), Vector((-1.05, -0.64, 0.0)))


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


# --- body -------------------------------------------------------------------------

def _main_tower(p):
    c = MAIN
    p.turned(c, 'Z', [(PLINTH_H, 0.33), (0.16, 0.33), (0.2, 0.3), (0.85, 0.3), (0.9, 0.24)], WHITE, sides=24)  # the drum
    p.turned(c, 'Z', [(0.9, 0.16), (1.12, 0.16)], STEEL, sides=16)  # the neck
    p.turned(c, 'Z', [(1.12, 0.2), (1.16, 0.25)], WHITE, sides=6)
    p.turned(c, 'Z', [(1.16, 0.25), (1.36, 0.27)], GLASS, sides=6)  # the glazed cabin
    p.turned(c, 'Z', [(1.36, 0.28), (CABIN_TOP, 0.28), (CABIN_TOP + 0.03, 0.2)], WHITE, sides=6)  # its roof


def _side_tower(p, c):
    p.turned(c, 'Z', [(PLINTH_H, 0.3), (0.2, 0.26), (0.35, 0.23), (0.78, 0.23), (0.82, 0.25), (0.86, 0.25), (0.86, 0.2)],
             WHITE, sides=24)
    for dx, dy, h in ((-0.07, 0.05, 1.22), (0.05, 0.07, 1.12), (0.02, -0.08, 1.05)):  # antennas
        p.turned(c + Vector((dx, dy, 0.0)), 'Z', [(0.8, 0.025), (h, 0.015)], STEEL, sides=8)
        p.sphere(c + Vector((dx, dy, h + 0.02)), 'Z', 0.03, WHITE, sides=8, rings=4)


def _entrance(p):
    c = Vector((-0.66, 0.0, 0.0))
    p.turned(c, 'Z', [(PLINTH_H, 0.17), (0.62, 0.17), (0.66, 0.19)], WHITE, sides=20)
    dome = [(0.66 + 0.16 * math.sin(math.pi / 2 * i / 5), 0.18 * math.cos(math.pi / 2 * i / 5)) for i in range(5)]
    p.turned(c, 'Z', dome + [(0.83, 0.03)], GLASS, sides=20)  # the glazed dome
    p.box((c.x + 0.14, -0.1, 0.12), (c.x + 0.18, 0.1, 0.48), STEEL)  # the door
    for i in range(4):  # steps down to the pads
        x = c.x + 0.18 + i * 0.05
        p.box((x, -0.12, PLINTH_H), (x + 0.05, 0.12, PAD_Z + 0.08 - i * 0.025), STEEL)


def _pads(p):
    x0, x1 = PADS_X
    p.box((x0, -1.0, PLINTH_H), (x1, 1.0, PAD_Z), PAD)  # the slab of the pads
    mid = (x0 + x1) / 2
    p.box((mid - 0.07, -0.98, PAD_Z), (mid + 0.07, 0.98, PAD_Z + 0.008), LIGHT)  # the lit cross between them
    p.box((x0 + 0.02, -0.07, PAD_Z), (x1 - 0.02, 0.07, PAD_Z + 0.008), LIGHT)
    for x in (x0 + 0.06, x1 - 0.06):  # bollards with lamps at the corners
        for y in (-0.94, 0.94):
            p.turned((x, y, 0.0), 'Z', [(PAD_Z, 0.04), (PAD_Z + 0.09, 0.04)], STEEL, sides=10)
            p.turned((x, y, 0.0), 'Z', [(PAD_Z + 0.09, 0.035), (PAD_Z + 0.12, 0.03)], LIGHT, sides=10)


def _radar():
    """The radar array: a curved panel on two arms over a short post; its object's origin is the pivot on the tower's axis."""
    p = Parts()
    c = RADAR_PIVOT
    p.turned(c, 'Z', [(0.0, 0.05), (0.12, 0.05)], STEEL, sides=10)  # the post
    p.box((c.x - 0.04, c.y - 0.2, c.z + 0.1), (c.x + 0.04, c.y + 0.2, c.z + 0.16), STEEL)  # the yoke
    for s in (-1, 1):
        p.box((c.x - 0.03, c.y + s * 0.2 - 0.025, c.z + 0.12), (c.x + 0.1, c.y + s * 0.2 + 0.025, c.z + 0.3), STEEL)
    # The array: a strip of a cylinder whose axis stands behind the pivot, so it bows out towards +x, with ribs behind it.
    r, span, steps = 0.42, math.radians(48), 10
    axis = Vector((c.x + 0.14 - r, c.y, 0.0))
    angles = [-span + 2 * span * i / steps for i in range(steps + 1)]
    outer = [axis + Vector((r * math.cos(a), r * math.sin(a), 0.0)) for a in angles]
    inner = [axis + Vector(((r - 0.03) * math.cos(a), (r - 0.03) * math.sin(a), 0.0)) for a in reversed(angles)]
    p.loft([[q + Vector((0.0, 0.0, c.z + z)) for q in outer + inner] for z in (0.18, 0.46)], WHITE)
    for a in (-span * 0.6, 0.0, span * 0.6):  # ribs down the back
        q = axis + Vector(((r - 0.05) * math.cos(a), (r - 0.05) * math.sin(a), 0.0))
        p.box((q.x - 0.02, q.y - 0.02, c.z + 0.16), (q.x + 0.02, q.y + 0.02, c.z + 0.48), STEEL)
    radar = p.finish(PREFIX + "spin_radar")
    radar.data.transform(Matrix.Translation(-c))
    radar.location = c
    return radar


def _team(p):
    p.turned(MAIN, 'Z', [(0.5, 0.305), (0.62, 0.305)], TEAM, sides=24)  # the band round the main tower
    for c in SIDE_TOWERS:
        p.turned(c, 'Z', [(0.52, 0.235), (0.64, 0.235)], TEAM, sides=24)
    x0, x1 = PADS_X
    mid = (x0 + x1) / 2
    for cx in ((x0 + mid) / 2, (mid + x1) / 2):  # a frame round each pad
        for cy in (-0.5, 0.5):
            h, w = 0.38, 0.035
            for lo, hi in (((cx - h, cy - h), (cx + h, cy - h + w)), ((cx - h, cy + h - w), (cx + h, cy + h)),
                           ((cx - h, cy - h + w), (cx - h + w, cy + h - w)), ((cx + h - w, cy - h + w), (cx + h, cy + h - w))):
                p.box((lo[0], lo[1], PAD_Z), (hi[0], hi[1], PAD_Z + 0.01), TEAM)


# --- material -----------------------------------------------------------------------

def _material():
    """Grey concrete, off-white panels, dark steel, blue glass, olive pads with dark chevrons, yellow lights, a light team zone."""
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
    # Chevrons on the pads: diamonds round the middle of each pad, every 0.12, dark on every other one.
    px = g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('SUBTRACT', x, PADS_X[0]), 1.0)), 0.5))
    py = g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('ADD', y, 1.0)), 0.5))
    diamond = g.math('ADD', px, py)
    chevrons = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', diamond, 6.0)), 30.0, -15.0)),
                      g.clamp01(g.math('MULTIPLY_ADD', diamond, -20.0, 7.0)))

    concrete = g.mix(grain, lin((0.40, 0.40, 0.39)), lin((0.47, 0.47, 0.45)))
    white = g.mix(grain, lin((0.74, 0.74, 0.72)), lin((0.82, 0.82, 0.80)))
    steel = g.mix(grain, lin((0.24, 0.25, 0.28)), lin((0.31, 0.32, 0.35)))
    glass = g.mix(grain, lin((0.50, 0.68, 0.86)), lin((0.60, 0.78, 0.94)))
    pad = g.mix(chevrons, g.mix(grain, lin((0.50, 0.50, 0.40)), lin((0.56, 0.56, 0.45))), lin((0.16, 0.17, 0.30)))
    light = g.mix(grain, lin((0.96, 0.92, 0.50)), lin((1.0, 0.98, 0.62)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (white, steel, glass, pad, light, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.55, 0.45, 0.1, 0.8, 0.3, 0.5))
    metallic = weighted((0.0, 0.3, 0.7, 0.2, 0.0, 0.0, 0.3))
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
    _main_tower(body)
    for c in SIDE_TOWERS:
        _side_tower(body, c)
    _entrance(body)
    _pads(body)
    body = body.finish(PREFIX + "body")
    radar = _radar()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-1.5, -1.0, 0.0), (1.5, 1.0, PLINTH_H), CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, radar, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the towers and the domes smooth, the edges sharp
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
