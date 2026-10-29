"""Iron curtain: the Union's protection superweapon (docs/ra2_buildings/soviets/iron_curtain).

Footprint 3x3 cells (game/data/factions/union/buildings_advanced.yaml), origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    pad       an eight-sided steel pad on a brick skirt, stripes along its south edge, legs at its corners
    blocks    two brick blocks on the west and east with stone tops, a pump on each: a stack of flanged drums, bent pipes up
              and over towards the middle
    ring      a large red pipe ring round it all on clamps
    core      a stepped red pedestal in the middle, a dark neck, a large dark polished sphere on it
No emblems, no text. The ring and the pedestal take the owner's colour, where the references have the red. Height 1.75 to the top
of the sphere. Budget 20000 triangles, texture 2048.

When its charge is full the game heats the sphere (PlayerBuildings): it glows red, brighter and brighter, until it is used.

Nodes (docs/art/model_standards.md, section 4):
    body            the pad, the skirt, the legs, the blocks, the pumps, the pipes, the clamps and the neck
    glow_sphere     the sphere: the game lights it red while the superweapon is ready
    team_mask       the ring and the pedestal: the game paints them in the owner's colour
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.iron_curtain as ic
    print(ic.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "iron_curtain"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


STEEL, BRICK, STONE, DARK, ORANGE, HAZARD, BLACK, TEAM = (_part(i) for i in range(8))
PARTS = 8

PLINTH_H = 0.06
PAD_R = 1.42
PAD_TOP = 0.3
SKIRT_TOP = PAD_TOP - 0.06
RING_R = 1.25
RING_Z = 0.55
SPHERE = Vector((0.0, 0.08, 1.28))
SPHERE_R = 0.45
PUMPS = (Vector((-0.78, -0.25, 0.0)), Vector((0.8, 0.3, 0.0)))


def _ring(centre, d, radius, sides):
    u = d.cross(Vector((1.0, 0.0, 0.0)) if abs(d.x) < 0.9 else Vector((0.0, 1.0, 0.0))).normalized()
    v = d.cross(u)
    return [centre + (u * math.cos(a) + v * math.sin(a)) * radius for a in (2 * math.pi * i / sides for i in range(sides))]


def _pipe(p, points, radius, part, sides=12):
    pts = [Vector(q) for q in points]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        rings.append(_ring(q, d, radius, sides))
    p.loft(rings, part)


def _octagon(r, z):
    return [Vector((r * math.cos(a), r * math.sin(a), z)) for a in (math.pi / 8 + i * math.pi / 4 for i in range(8))]


# --- body -------------------------------------------------------------------------

def _pad(p):
    # The deck in one piece: the material paints its skirt under SKIRT_TOP as brick.
    p.loft([_octagon(PAD_R, PLINTH_H), _octagon(PAD_R, PAD_TOP)], STEEL)
    for q in _octagon(PAD_R - 0.02, 0.0):  # legs at the corners
        p.box((q.x - 0.05, q.y - 0.05, PLINTH_H), (q.x + 0.05, q.y + 0.05, PAD_TOP), BLACK)
    p.box((-0.75, -PAD_R * 0.92 - 0.02, PAD_TOP - 0.05), (0.75, -PAD_R * 0.92 + 0.06, PAD_TOP + 0.005), HAZARD)  # stripes


def _blocks(p):
    for c in PUMPS:
        p.box((c.x - 0.32, c.y - 0.36, PAD_TOP), (c.x + 0.32, c.y + 0.36, 0.62), BRICK)
        p.box((c.x - 0.35, c.y - 0.39, 0.62), (c.x + 0.35, c.y + 0.39, 0.68), STONE)
        # The pump: a stack of flanged drums.
        p.turned(c, 'Z', [(0.68, 0.17), (0.74, 0.17), (0.74, 0.13), (0.82, 0.13), (0.82, 0.17), (0.88, 0.17), (0.88, 0.13),
                          (0.96, 0.13), (0.96, 0.16), (1.0, 0.16), (1.0, 0.1)], DARK, sides=16)
        # A bent pipe up out of it and over towards the middle, and a small one down its side.
        to = Vector((-c.x, -c.y, 0.0)).normalized()
        top = c + Vector((0.0, 0.0, 1.0))
        _pipe(p, [top, top + Vector((0, 0, 0.12)), top + Vector((0, 0, 0.2)) + to * 0.06, top + Vector((0, 0, 0.2)) + to * 0.22,
                  top + Vector((0, 0, 0.12)) + to * 0.3], 0.055, ORANGE)
        side = Vector((-to.y, to.x, 0.0))
        q = c + side * 0.2 + to * 0.1
        _pipe(p, [q + Vector((0, 0, 0.92)), q + Vector((0, 0, 0.98)) + side * 0.06, q + Vector((0, 0, 0.75)) + side * 0.1,
                  q + Vector((0, 0, 0.68)) + side * 0.1], 0.025, ORANGE)


def _core(p):
    c = Vector((SPHERE.x, SPHERE.y, 0.0))
    p.turned(c, 'Z', [(0.74, 0.13), (SPHERE.z - SPHERE_R + 0.06, 0.1)], DARK, sides=16)  # the neck
    for k in range(10):  # clamps round the ring
        a = 2 * math.pi * (k + 0.5) / 10
        q = Vector((RING_R * math.cos(a), RING_R * math.sin(a), RING_Z))
        t = Vector((-math.sin(a), math.cos(a), 0.0))
        p.loft([_ring(q - t * 0.05, t, 0.085, 12), _ring(q + t * 0.05, t, 0.085, 12)], STEEL)
        if k % 2 == 0:  # posts down to the deck
            p.box((q.x - 0.03, q.y - 0.03, PAD_TOP), (q.x + 0.03, q.y + 0.03, RING_Z - 0.06), BLACK)


def _team(p):
    rings = []
    steps = 48
    for i in range(steps + 1):
        a = 2 * math.pi * i / steps
        rings.append(_ring(Vector((RING_R * math.cos(a), RING_R * math.sin(a), RING_Z)), Vector((-math.sin(a), math.cos(a), 0.0)), 0.06, 12))
    p.loft(rings, TEAM)  # the big ring
    c = Vector((SPHERE.x, SPHERE.y, 0.0))
    p.turned(c, 'Z', [(PAD_TOP, 0.55), (0.42, 0.55), (0.42, 0.44), (0.56, 0.44), (0.56, 0.32), (0.74, 0.3), (0.74, 0.13)], TEAM, sides=32)


def _sphere():
    p = Parts()
    p.sphere(SPHERE, 'Z', SPHERE_R, BLACK, sides=32, rings=16)
    obj = p.finish(PREFIX + "glow_sphere")
    return obj


# --- material -----------------------------------------------------------------------

def _material():
    """Rusty steel plates, brick in courses, stone, dark steel, orange pipes, stripes, a polished black sphere, a light team zone."""
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
    rust = g.noise(5.0, 5.0)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    z = g.axis(coord, 'Z')
    courses = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 18.0)), -10.0, 1.0))
    stripes = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 6.0)), 40.0, -20.0))

    steel = g.mix(rust, lin((0.58, 0.50, 0.46)), lin((0.66, 0.56, 0.50)))
    brick = g.mix(courses, g.mix(grain, lin((0.50, 0.34, 0.26)), lin((0.58, 0.40, 0.30))), lin((0.36, 0.30, 0.26)))
    stone = g.mix(grain, lin((0.62, 0.60, 0.56)), lin((0.70, 0.68, 0.64)))
    dark = g.mix(grain, lin((0.22, 0.22, 0.23)), lin((0.30, 0.30, 0.31)))
    orange = g.mix(grain, lin((0.78, 0.48, 0.14)), lin((0.86, 0.56, 0.20)))
    hazard = g.mix(stripes, lin((0.90, 0.72, 0.12)), lin((0.10, 0.10, 0.10)))
    black = g.mix(grain, lin((0.10, 0.10, 0.11)), lin((0.16, 0.16, 0.17)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = steel
    for m, col in zip(mask[1:], (brick, stone, dark, orange, hazard, black, team)):
        albedo = g.mix(m, albedo, col)
    skirt = g.math('MULTIPLY', mask[0], g.clamp01(g.math('MULTIPLY_ADD', z, -100.0, SKIRT_TOP * 100.0 + 0.5)))
    albedo = g.mix(skirt, albedo, brick)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.6, 0.85, 0.8, 0.45, 0.4, 0.55, 0.12, 0.4))
    metallic = weighted((0.5, 0.0, 0.0, 0.7, 0.6, 0.1, 0.7, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', courses, mask[1]), 0.7)), 0.03, 0.005)
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
    _pad(body)
    _blocks(body)
    _core(body)
    body = body.finish(PREFIX + "body")
    sphere = _sphere()
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.loft([_octagon(PAD_R + 0.05, z) for z in (0.0, PLINTH_H)], STEEL)
    base = base.finish(PREFIX + "base")
    meshes = [body, sphere, team, base]
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
