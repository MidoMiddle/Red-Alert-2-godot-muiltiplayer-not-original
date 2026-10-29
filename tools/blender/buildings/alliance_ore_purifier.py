"""Alliance ore purifier: the ore brings 25% more (docs/ra2_buildings/allies/ore_purifier).

Footprint 3x3 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    plinth    a plate with its corners cut, paved in tiles
    core      a bell in the middle: an olive body with a seam, a band of lit windows round it, a grey riveted cone, a collar,
              a stack with a lip
    ring      a large pipe ring round the core on posts, struts from the core to it
    clamps    four blocks on the diagonals holding the ring, a grey strip down each
No emblems, no text. The clamps and the collar take the owner's colour, where the references have the blue. Height 1.7 to the
lip of the stack. Budget 20000 triangles, texture 2048.

Nodes (docs/art/model_standards.md, section 4):
    body            the core, the band, the ring, the posts, the struts and the strips
    team_mask       the four clamps and the collar: the game paints them in the owner's colour
    base            the paved plate (docs/art/model_standards.md, "Фундамент")
    smoke_1         empty at the mouth of the stack: where smoke will rise (the game does not draw it yet)

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_ore_purifier as aop
    print(aop.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_ore_purifier"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


PAVING, BELL, STEEL, LAMP, DARK, TEAM = (_part(i) for i in range(6))
PARTS = 6

PLINTH_H = 0.08
BODY_TOP = 0.95  # the olive body ends here; the material paints the bell grey above it
SEAM_Z = 0.46
BAND = (0.62, 0.78)  # the band of lit windows
RING_R = 1.0
RING_Z = 0.55
RING_THICK = 0.1
STACK_TOP = 1.7


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _radial_slab(p, angle, profile, half, part, out_by=0.0):
    """A plate along the radius at angle: profile is (distance from the axis, height), half its thickness across."""
    out = Vector((math.cos(angle), math.sin(angle), 0.0))
    side = Vector((-out.y, out.x, 0.0))
    p.loft([[out * (r + out_by) + Vector((0.0, 0.0, z)) + side * s for r, z in profile] for s in (-half, half)], part)


CLAMP = [(0.86, PLINTH_H), (1.28, PLINTH_H), (1.28, 0.46), (1.1, 0.84), (0.86, 0.84)]


# --- body -------------------------------------------------------------------------

def _core(p):
    # The bell in one body of revolution: the olive body bulging twice with a seam between, the cone, the neck, the stack.
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLINTH_H, 0.6), (0.14, 0.64), (0.3, 0.66), (SEAM_Z - 0.02, 0.62), (SEAM_Z, 0.6),
                                     (SEAM_Z + 0.02, 0.62), (0.7, 0.6), (0.86, 0.58), (BODY_TOP, 0.56), (BODY_TOP, 0.6),
                                     (1.02, 0.6), (1.24, 0.24), (1.24, 0.17), (1.48, 0.15), (1.5, 0.2), (STACK_TOP, 0.2),
                                     (STACK_TOP, 0.15), (STACK_TOP - 0.1, 0.15)], BELL, sides=32)
    p.turned((0.0, 0.0, 0.0), 'Z', [(STACK_TOP - 0.1, 0.15), (STACK_TOP - 0.099, 0.01)], DARK, sides=16)  # the mouth
    p.turned((0.0, 0.0, 0.0), 'Z', [(BAND[0], 0.615), (BAND[1], 0.6)], LAMP, sides=32)  # the band of lit windows
    for i in range(24):  # rivets round the rim of the cone
        a = 2 * math.pi * i / 24
        p.sphere((0.6 * math.cos(a), 0.6 * math.sin(a), 1.01), 'Z', 0.018, STEEL, sides=6, rings=3)


def _pipe_ring(p):
    rings = []
    steps = 48
    for i in range(steps + 1):
        a = 2 * math.pi * i / steps
        centre = Vector((RING_R * math.cos(a), RING_R * math.sin(a), RING_Z))
        tangent = Vector((-math.sin(a), math.cos(a), 0.0))
        rings.append(_ring(centre, tangent, RING_THICK, 12))
    p.loft(rings, STEEL)
    for i in range(4):  # struts from the core and posts under the ring, on the axes
        a = i * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        p.bar(out * 0.58 + Vector((0.0, 0.0, RING_Z)), out * (RING_R - RING_THICK) + Vector((0.0, 0.0, RING_Z)), 0.035, STEEL)
        p.turned(out * RING_R, 'Z', [(PLINTH_H, 0.05), (RING_Z - RING_THICK + 0.01, 0.04)], STEEL, sides=8)
    for i in range(4):  # the grey strips down the clamps, a little proud of them
        _radial_slab(p, math.pi / 4 + i * math.pi / 2, CLAMP, 0.05, STEEL, out_by=0.012)


def _team(p):
    for i in range(4):
        _radial_slab(p, math.pi / 4 + i * math.pi / 2, CLAMP, 0.17, TEAM)
    p.turned((0.0, 0.0, 0.0), 'Z', [(1.24, 0.21), (1.33, 0.2), (1.33, 0.14)], TEAM, sides=24)  # the collar


# --- material -----------------------------------------------------------------------

def _material():
    """Paving in tiles, an olive bell with a grey top, steel, the lit band in panes, dark, a light team zone."""
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
    # The tiles of the paving: the edges of Voronoi cells.
    cells = g.add('ShaderNodeTexVoronoi', feature='DISTANCE_TO_EDGE')
    g.link(coord, cells, 'Vector')
    g.put(cells, 'Scale', 5.0)
    joints = g.clamp01(g.math('MULTIPLY_ADD', rc.sock(cells, 'Distance', outputs=True), -25.0, 1.0))
    # The bell: grey above BODY_TOP, a dark seam at SEAM_Z.
    top = g.math('MULTIPLY', mask[1], g.clamp01(g.math('MULTIPLY_ADD', z, 80.0, -BODY_TOP * 80.0 + 0.5)))
    seam = g.math('MULTIPLY', mask[1], g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', z, SEAM_Z)), -60.0, 1.6)))
    # The panes of the band: 24 round it, dark mullions between.
    turn = g.math('MULTIPLY', g.math('ARCTAN2', y, x), 12.0 / math.pi)
    mullions = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', turn), 0.5)), 20.0, -8.0))

    paving = g.mix(joints, g.mix(grain, lin((0.42, 0.36, 0.28)), lin((0.50, 0.44, 0.34))), lin((0.18, 0.16, 0.14)))
    olive = g.mix(grain, lin((0.40, 0.44, 0.32)), lin((0.48, 0.52, 0.38)))
    steel = g.mix(grain, lin((0.56, 0.58, 0.62)), lin((0.64, 0.66, 0.70)))
    lamp = g.mix(mullions, g.mix(grain, lin((1.0, 0.62, 0.16)), lin((1.0, 0.74, 0.26))), lin((0.20, 0.16, 0.12)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.08, 0.08, 0.09)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = paving
    for m, col in zip(mask[1:], (olive, steel, lamp, dark, team)):
        albedo = g.mix(m, albedo, col)
    albedo = g.mix(top, albedo, steel)
    albedo = g.mix(seam, albedo, lin((0.55, 0.57, 0.60)))

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.6, 0.35, 0.25, 0.8, 0.45))
    metallic = g.math('MULTIPLY_ADD', top, 0.4, weighted((0.0, 0.3, 0.7, 0.0, 0.2, 0.3)))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', joints, mask[0]), 0.8)), 0.03, 0.005)
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
    _core(body)
    _pipe_ring(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    cut = 0.45
    outline = [(1.45, -1.45 + cut), (1.45, 1.45 - cut), (1.45 - cut, 1.45), (-1.45 + cut, 1.45), (-1.45, 1.45 - cut),
               (-1.45, -1.45 + cut), (-1.45 + cut, -1.45), (1.45 - cut, -1.45)]
    base.loft([[Vector((x, y, z)) for x, y in outline] for z in (0.0, PLINTH_H)], PAVING)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))  # the bell and the ring smooth, the edges sharp
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [_empty("smoke_1", (0.0, 0.0, STACK_TOP + 0.02))], mat, graph


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
