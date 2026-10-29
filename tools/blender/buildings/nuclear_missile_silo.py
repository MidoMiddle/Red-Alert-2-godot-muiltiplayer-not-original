"""Nuclear missile silo: the Union's superweapon (docs/ra2_buildings/soviets/nuclear_missile_silo).

Footprint 3x3 cells (game/data/factions/union/buildings_advanced.yaml), origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    pad       an eight-sided concrete pad with a railing, crates at a corner, an orange cross of rails under the rocket
    shroud    a tall dark shroud with a pitched top over the middle of the pad, ribbed, red panels on its faces; it is two
              halves split along its length
    rocket    a rocket inside it: a white body with red bands, four boosters round its foot, a nose cone
No emblems, no text. The red panels of the shroud take the owner's colour, where the references have the red. Height 2.3 to the
ridge of the shroud. Budget 20000 triangles, texture 2048.

When the missile is ready (its charge is full) the game opens the silo (PlayerBuildings): the shroud turns an eighth round, so its
halves slide apart across the camera's view, the rocket rises out. When the missile is fired the rocket is gone and the silo shuts; the next rocket waits
inside, down.

Nodes (docs/art/model_standards.md, section 4):
    body            the pad, its railing, the crates and the rails
    shroud          an empty on the pad's middle: turns 45° about its Y as the silo opens
    shroud_a, shroud_b      under shroud: the two halves, their origins on the middle too; shroud_open (empty, under shroud)
                    is where shroud_a stands open, and shroud_b goes the other way as far
    team_mask_a, team_mask_b under the halves: their red panels, in the owner's colour
    rocket          the rocket, its origin at its foot, down inside; rocket_up (empty) is where its foot goes when it is up
    base            the concrete plinth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.nuclear_missile_silo as nms
    print(nms.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "nuclear_missile_silo"
PREFIX = ID + "_"
SIZE = 2048


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, DARK, STEEL, ORANGE, WHITE, RED, CRATE, TEAM = (_part(i) for i in range(8))
PARTS = 8

PLINTH_H = 0.06
PAD_R = 1.45  # the pad's eight-sided outline, to its corners
PAD_TOP = 0.16
HALF_X = 0.62  # the shroud's half-width across its split
HALF_Y = 0.52  # its half-depth along the split
SHROUD_TOP = 2.0
RIDGE = 2.3
SLIDE = 0.55  # how far each half slides away when it opens
ROCKET_FOOT = PAD_TOP + 0.02
ROCKET_LEN = 1.75
RISE = 0.9


def _octagon(r, z):
    return [Vector((r * math.cos(a), r * math.sin(a), z)) for a in (math.pi / 8 + i * math.pi / 4 for i in range(8))]


# --- body -------------------------------------------------------------------------

def _pad(p):
    p.loft([_octagon(PAD_R, z) for z in (PLINTH_H, PAD_TOP)], CONCRETE)
    corners = _octagon(PAD_R - 0.06, PAD_TOP)
    for i in range(8):  # the railing round its edge: posts and a top rail on each side
        a, b = corners[i], corners[(i + 1) % 8]
        for t in (0.0, 0.33, 0.66):
            q = a.lerp(b, t)
            p.box((q.x - 0.012, q.y - 0.012, PAD_TOP), (q.x + 0.012, q.y + 0.012, PAD_TOP + 0.14), STEEL)
        p.bar(a + Vector((0, 0, 0.14)), b + Vector((0, 0, 0.14)), 0.01, STEEL)
    for k in range(4):  # the orange cross of rails under the rocket, out from under the shroud
        a = k * math.pi / 2
        out = Vector((math.cos(a), math.sin(a), 0.0))
        side = Vector((-out.y, out.x, 0.0))
        for s in (-0.08, 0.08):
            p.bar(out * 0.2 + side * s + Vector((0, 0, PAD_TOP + 0.02)), out * 1.12 + side * s + Vector((0, 0, PAD_TOP + 0.02)), 0.02, ORANGE)
        for i in range(5):
            q = out * (0.3 + i * 0.2) + Vector((0, 0, PAD_TOP + 0.02))
            p.bar(q - side * 0.08, q + side * 0.08, 0.012, ORANGE)
    for x, y, s in ((-0.95, -0.7, 0.18), (-0.75, -0.95, 0.16), (-1.0, -0.45, 0.14)):  # crates
        p.box((x - s / 2, y - s / 2, PAD_TOP), (x + s / 2, y + s / 2, PAD_TOP + s), CRATE)


# --- the moving parts ----------------------------------------------------------------

def _half(sign):
    """Half of the shroud on the side of sign along X: walls, ribs and its half of the pitched top; built in place."""
    p = Parts()
    x0, x1 = (0.005, HALF_X) if sign > 0 else (-HALF_X, -0.005)
    xo = x1 if sign > 0 else x0  # its outer face
    p.box((x0, -HALF_Y, PAD_TOP), (x1, HALF_Y, SHROUD_TOP), DARK)
    # Its half of the top, pitched along X: up from the outer edge to the ridge at the split.
    p.loft([[Vector((x, y, z)) for x, z in ((xo, SHROUD_TOP), (sign * 0.005, SHROUD_TOP), (sign * 0.005, RIDGE))] for y in (-HALF_Y, HALF_Y)],
           DARK)
    for y in (-HALF_Y - 0.03, HALF_Y):  # ribs up the end faces
        for x in (0.15, 0.4):
            xx = sign * x
            p.box((xx - 0.035, y, PAD_TOP), (xx + 0.035, y + 0.03, SHROUD_TOP), STEEL)
    for z in (0.6, 1.3):  # steel bands round it
        p.box((x0 - (0.02 if sign < 0 else 0), -HALF_Y - 0.02, z), (x1 + (0.02 if sign > 0 else 0), HALF_Y + 0.02, z + 0.06), STEEL)
    half = p.finish(PREFIX + ("shroud_b" if sign > 0 else "shroud_a"))

    t = Parts()  # its red panels: on the outer face and on its half of each end face
    xs = xo + sign * 0.012
    t.box((min(xo, xs), -0.32, 0.75), (max(xo, xs), 0.32, 1.85), TEAM)
    for y in (-HALF_Y - 0.012, HALF_Y):
        lo, hi = (0.08, 0.5) if sign > 0 else (-0.5, -0.08)
        t.box((lo, y, 1.42), (hi, y + 0.012, 1.85), TEAM)
    team = t.finish(PREFIX + ("team_mask_b" if sign > 0 else "team_mask_a"))
    return half, team


def _rocket():
    p = Parts()
    f = ROCKET_FOOT
    p.turned((0.0, 0.0, 0.0), 'Z', [(f, 0.1), (f + 0.1, 0.13), (f + 1.25, 0.13), (f + 1.45, 0.11), (f + 1.62, 0.06), (f + ROCKET_LEN, 0.01)],
             WHITE, sides=20)
    for z0, z1 in ((f + 0.55, f + 0.68), (f + 1.1, f + 1.2), (f + 1.45, f + 1.52)):  # red bands
        p.turned((0.0, 0.0, 0.0), 'Z', [(z0, 0.135), (z1, 0.135)], RED, sides=20)
    for k in range(4):  # the boosters round its foot
        a = math.pi / 4 + k * math.pi / 2
        c = Vector((0.19 * math.cos(a), 0.19 * math.sin(a), 0.0))
        p.turned(c, 'Z', [(f, 0.06), (f + 0.08, 0.08), (f + 0.55, 0.08), (f + 0.85, 0.02)], WHITE, sides=12)
        p.turned(c, 'Z', [(f + 0.3, 0.083), (f + 0.4, 0.083)], RED, sides=12)
    rocket = p.finish(PREFIX + "rocket")
    rocket.data.transform(Matrix.Translation(Vector((0.0, 0.0, -f))))
    rocket.location = (0.0, 0.0, f)
    return rocket


def _moving():
    shroud = bpy.data.objects.new(PREFIX + "shroud", None)
    shroud.empty_display_type = 'PLAIN_AXES'
    shroud.location = (0.0, 0.0, PAD_TOP)
    bpy.context.scene.collection.objects.link(shroud)
    a, ta = _half(-1)
    b, tb = _half(1)
    for half in (a, b):
        half.data.transform(Matrix.Translation(Vector((0.0, 0.0, -PAD_TOP))))
        half.parent = shroud
        half.location = (0.0, 0.0, 0.0)
    for team, half in ((ta, a), (tb, b)):
        team.data.transform(Matrix.Translation(Vector((0.0, 0.0, -PAD_TOP))))
        team.parent = half
        team.location = (0.0, 0.0, 0.0)
    mark = bpy.data.objects.new(PREFIX + "shroud_open", None)
    mark.empty_display_type = 'PLAIN_AXES'
    mark.empty_display_size = 0.08
    mark.parent = shroud
    mark.location = (-SLIDE, 0.0, 0.0)
    bpy.context.scene.collection.objects.link(mark)
    up = bpy.data.objects.new(PREFIX + "rocket_up", None)
    up.empty_display_type = 'PLAIN_AXES'
    up.empty_display_size = 0.08
    up.location = (0.0, 0.0, ROCKET_FOOT + RISE)
    bpy.context.scene.collection.objects.link(up)
    bpy.context.view_layer.update()
    return shroud, [a, b, ta, tb], _rocket(), [mark, up]


# --- material -----------------------------------------------------------------------

def _material():
    """Light concrete, dark ribbed steel, steel, orange, white, red, crates, a light team zone."""
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
    z = g.axis(coord, 'Z')
    panels = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 6.0)), -30.0, 1.0))

    concrete = g.mix(grain, lin((0.66, 0.66, 0.64)), lin((0.74, 0.74, 0.72)))
    dark = g.mix(panels, g.mix(grain, lin((0.14, 0.14, 0.16)), lin((0.20, 0.20, 0.22))), lin((0.30, 0.30, 0.33)))
    steel = g.mix(grain, lin((0.36, 0.37, 0.39)), lin((0.44, 0.45, 0.47)))
    orange = g.mix(grain, lin((0.88, 0.50, 0.12)), lin((0.94, 0.58, 0.18)))
    white = g.mix(grain, lin((0.82, 0.82, 0.80)), lin((0.90, 0.90, 0.88)))
    red = g.mix(grain, lin((0.72, 0.10, 0.08)), lin((0.82, 0.14, 0.11)))
    crate = g.mix(grain, lin((0.52, 0.36, 0.24)), lin((0.60, 0.44, 0.30)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col in zip(mask[1:], (dark, steel, orange, white, red, crate, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.85, 0.5, 0.45, 0.5, 0.4, 0.45, 0.8, 0.45))
    metallic = weighted((0.0, 0.6, 0.7, 0.2, 0.3, 0.2, 0.0, 0.3))
    normal = g.bump(g.math('MAXIMUM', g.noise(60.0, 3.0), g.math('MULTIPLY', g.math('MULTIPLY', panels, mask[1]), 0.6)), 0.03, 0.005)
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
    """The meshes, the other nodes (each parent before its children) and the material."""
    _clear()
    body = Parts()
    _pad(body)
    body = body.finish(PREFIX + "body")
    shroud, halves, rocket, marks = _moving()
    base = Parts()
    base.loft([_octagon(PAD_R + 0.05, z) for z in (0.0, PLINTH_H)], CONCRETE)
    base = base.finish(PREFIX + "base")
    meshes = [body, *halves, rocket, base]
    _drop_bottoms([body, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    _pack(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [shroud, *marks], mat, graph


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
    top = [obj for obj in objs if obj.parent is None]
    rc.arrange(top, ID)  # its place in the file: the row of the players' buildings
    coll = bpy.data.collections[ID]
    for obj in objs:
        if obj not in top and coll not in obj.users_collection:
            for owner in list(obj.users_collection):
                owner.objects.unlink(obj)
            coll.objects.link(obj)
    glb = os.path.join(rc.GLB_ROOT, "building", ID, ID + ".glb")
    with rc.node_names(objs, [o.name[len(PREFIX):] for o in objs]):
        rc.save_blend(objs, mat, imgs, os.path.join(base, ID + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
