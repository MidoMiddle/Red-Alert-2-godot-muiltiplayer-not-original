"""Alliance chronosphere: the Alliance's teleport superweapon (docs/ra2_buildings/allies/chronosphere).

Footprint 4x3 cells (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground; the camera
sees the south (-Y in Blender, +Z in Godot) and east (+X) sides. The composition follows the owner's references; the parts are
our own:
    deck      a dark deck with its corners cut
    dome      a long glazed dome in the east, lying along x, in two halves: the north one fixed, the south one (towards the camera)
              turning over the top onto it when the chronosphere opens
    drum      a drum inside the dome along its length, ribbed, a glowing band round it; it turns while the dome is open
    frame     two tall arched frames in the west with a coil between them
    tanks     two capsules lying at the west corners
No emblems, no text. The arched frames, the panels on the fixed half of the dome and the channel round the dome take the owner's
colour, where the references have the blue. Height 1.3 to the top of the frames. Budget 20000 triangles, texture 2048.

When its charge is full the game opens it (PlayerBuildings): the south half of the dome turns 90° about its long axis onto the
north half, and the drum turns; when it has been used, the drum stops and the dome shuts.

Nodes (docs/art/model_standards.md, section 4):
    body            the deck, the fixed half of the dome, the frames, the coil and the tanks
    dome            the south half; its origin on the dome's long axis at its foot: the game turns it about its X by -90°
    drum            the drum; its origin on its axis: the game turns it about its X while open
    team_mask       the frames, the panels and the channel: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_chronosphere as ach
    print(ach.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_chronosphere"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


DECK, STEEL, GLASS, WHITE, DARK, LIGHT, COPPER, TEAM = (_part(i) for i in range(8))
PARTS = 8

PLINTH_H = 0.06
DECK_TOP = 0.14
DOME = Vector((0.95, -0.1, DECK_TOP))  # the dome's centre at its foot
DOME_RX = 0.95  # its half-length along x
DOME_R = 0.76  # its radius across
DRUM_R = 0.34
DRUM_HALF = 0.55


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _shell(p, radius, thick, phi0, phi1, part, steps_x=16, steps_a=10):
    """A part of the dome's shell between the angles phi0 and phi1 from its top (negative to the south), radius across, thick."""
    rings = []
    for i in range(steps_x + 1):
        t = -0.96 + 1.92 * i / steps_x
        k = math.sqrt(1.0 - t * t)
        x = DOME.x + DOME_RX * t
        arc = []
        for r, order in ((radius + thick, range(steps_a + 1)), (radius, reversed(range(steps_a + 1)))):
            for j in order:
                phi = phi0 + (phi1 - phi0) * j / steps_a
                arc.append(Vector((x, DOME.y + r * k * math.sin(phi), DOME.z + r * k * math.cos(phi))))
        rings.append(arc)
    p.loft(rings, part)


# --- body -------------------------------------------------------------------------

def _deck(p):
    cut = 0.35
    outline = [(1.98, -1.48 + cut), (1.98, 1.48 - cut), (1.98 - cut, 1.48), (-1.98 + cut, 1.48), (-1.98, 1.48 - cut), (-1.98, -1.48 + cut),
               (-1.98 + cut, -1.48), (1.98 - cut, -1.48)]
    p.loft([[Vector((x, y, z)) for x, y in outline] for z in (PLINTH_H, DECK_TOP)], DECK)


def _frames(p):
    c = Vector((-0.5, -0.06, 0.72))  # the coil between them, along y
    p.turned(c, 'Y', [(-0.38, 0.12), (0.38, 0.12)], DARK, sides=16)
    for i in range(6):
        y = -0.3 + i * 0.12
        p.turned(c + Vector((0.0, y, 0.0)), 'Y', [(-0.03, 0.17), (0.03, 0.17)], COPPER, sides=16)
    p.box((-0.75, -0.48, DECK_TOP), (-0.25, 0.38, 0.3), STEEL)  # the block the frames stand on


def _tanks(p):
    for y in (-0.98, 0.98):
        c = Vector((-1.4, y, 0.36))
        p.turned(c, 'X', [(-0.52, 0.0), (-0.5, 0.12), (-0.44, 0.2), (0.44, 0.2), (0.5, 0.12), (0.52, 0.0)], WHITE, sides=18)
        for x in (-0.18, 0.18):
            p.turned(c + Vector((x, 0.0, 0.0)), 'X', [(-0.07, 0.205), (0.07, 0.205)], DARK, sides=18)
        for x in (-0.3, 0.3):  # its cradle
            p.box((c.x + x - 0.05, c.y - 0.18, DECK_TOP), (c.x + x + 0.05, c.y + 0.18, c.z - 0.12), DARK)


def _team(p):
    for y in (-0.5, 0.38):  # the arched frames
        prof = [(-0.92, 0.3), (-0.1, 0.3), (-0.1, 0.5), (-0.32, 1.05), (-0.52, 1.3), (-0.75, 1.3), (-0.92, 1.12)]
        p.loft([[Vector((x, yy, z)) for x, z in prof] for yy in (y, y + 0.12)], TEAM)
    for i in range(5):  # the panels on the fixed half of the dome, along its ridge-side
        t = -0.7 + i * 0.35
        x = DOME.x + DOME_RX * t
        k = math.sqrt(1.0 - t * t)
        phi = math.radians(55)
        q = Vector((x, DOME.y + (DOME_R + 0.025) * k * math.sin(phi), DOME.z + (DOME_R + 0.025) * k * math.cos(phi)))
        n = Vector((0.0, math.sin(phi), math.cos(phi)))
        p.loft([_ring(q + n * dz, n, 0.09, 8) for dz in (0.0, 0.015)], TEAM)
    # The channel round the dome's foot.
    rings = []
    for r in (1.08, 0.98):
        rings.append([Vector((DOME.x + DOME_RX * r * math.cos(a), DOME.y + DOME_R * r * math.sin(a), 0.0)) for a in (2 * math.pi * i / 40 for i in range(40))])
    for i in range(40):
        j = (i + 1) % 40
        quad = [rings[0][i], rings[0][j], rings[1][j], rings[1][i]]
        p.loft([[q + Vector((0.0, 0.0, z)) for q in quad] for z in (DECK_TOP, DECK_TOP + 0.012)], TEAM)


# --- the moving parts ----------------------------------------------------------------

def _moving():
    dome = Parts()
    _shell(dome, DOME_R + 0.03, 0.03, -math.pi / 2, 0.0, GLASS)
    for k in range(3):  # ribs over it
        t = -0.6 + k * 0.6
        x = DOME.x + DOME_RX * t
        kk = math.sqrt(1.0 - t * t)
        arc = [Vector((x, DOME.y + (DOME_R + 0.07) * kk * math.sin(f), DOME.z + (DOME_R + 0.07) * kk * math.cos(f)))
               for f in (-math.pi / 2 + math.pi / 2 * i / 8 for i in range(9))]
        for a, b in zip(arc, arc[1:]):
            dome.bar(a, b, 0.025, STEEL)
    dome = dome.finish(PREFIX + "dome")
    dome.data.transform(Matrix.Translation(-DOME))
    dome.location = DOME

    drum = Parts()
    c = Vector((DOME.x, DOME.y, DOME.z + DRUM_R + 0.04))
    drum.turned(c, 'X', [(-DRUM_HALF, 0.0), (-DRUM_HALF, DRUM_R - 0.05), (-DRUM_HALF + 0.05, DRUM_R), (DRUM_HALF - 0.05, DRUM_R),
                         (DRUM_HALF, DRUM_R - 0.05), (DRUM_HALF, 0.0)], WHITE, sides=24)
    drum.turned(c, 'X', [(-0.14, DRUM_R + 0.02), (0.14, DRUM_R + 0.02)], LIGHT, sides=24)  # the glowing band
    for k in range(8):  # ribs along it
        a = 2 * math.pi * k / 8
        d = Vector((0.0, math.cos(a), math.sin(a)))
        drum.bar(c + Vector((-DRUM_HALF + 0.04, 0, 0)) + d * (DRUM_R + 0.01), c + Vector((DRUM_HALF - 0.04, 0, 0)) + d * (DRUM_R + 0.01), 0.022, DARK)
    drum = drum.finish(PREFIX + "drum")
    drum.data.transform(Matrix.Translation(-c))
    drum.location = c
    return dome, drum


def _fixed_half(p):
    _shell(p, DOME_R, 0.03, 0.0, math.pi / 2, GLASS)


# --- material -----------------------------------------------------------------------

def _material():
    """A dark deck, grey steel, light glazing in panes, white, dark, a blue glow, copper, a light team zone."""
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
    panes = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 5.0)), -25.0, 1.0)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 5.0)), -25.0, 1.0)))

    deck = g.mix(grain, lin((0.24, 0.25, 0.26)), lin((0.30, 0.31, 0.32)))
    steel = g.mix(grain, lin((0.42, 0.44, 0.48)), lin((0.50, 0.52, 0.56)))
    glass = g.mix(panes, g.mix(grain, lin((0.62, 0.66, 0.72)), lin((0.70, 0.74, 0.80))), lin((0.30, 0.32, 0.36)))
    white = g.mix(grain, lin((0.80, 0.81, 0.83)), lin((0.88, 0.89, 0.91)))
    dark = g.mix(grain, lin((0.08, 0.09, 0.10)), lin((0.13, 0.14, 0.15)))
    light = g.mix(grain, lin((0.45, 0.80, 1.0)), lin((0.60, 0.90, 1.0)))
    copper = g.mix(grain, lin((0.76, 0.46, 0.24)), lin((0.86, 0.56, 0.30)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = deck
    for m, col in zip(mask[1:], (steel, glass, white, dark, light, copper, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.8, 0.45, 0.15, 0.4, 0.6, 0.2, 0.35, 0.45))
    metallic = weighted((0.2, 0.6, 0.3, 0.3, 0.5, 0.0, 0.8, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', panes, mask[2]), 0.6)), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _drop_bottoms(objs):
    """Deletes the faces that look down onto the plinth or the ground: never seen, and black in the bake."""
    for obj in objs:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        low = [f for f in bm.faces if f.normal.z < -0.99 and (obj.matrix_world @ f.calc_center_median()).z < PLINTH_H + 1e-3]
        bmesh.ops.delete(bm, geom=low, context='FACES')
        bm.to_mesh(obj.data)
        bm.free()


def _pack(objs):
    rc.select_only(objs)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(rotate=True, margin=0.003)
    bpy.ops.object.mode_set(mode='OBJECT')


def build():
    _clear()
    body = Parts()
    _deck(body)
    _fixed_half(body)
    _frames(body)
    _tanks(body)
    body = body.finish(PREFIX + "body")
    dome, drum = _moving()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.box((-2.0, -1.5, 0.0), (2.0, 1.5, PLINTH_H), DECK)
    base = base.finish(PREFIX + "base")
    meshes = [body, dome, drum, team, base]
    bpy.context.view_layer.update()
    _drop_bottoms([body, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    _pack(meshes)
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
