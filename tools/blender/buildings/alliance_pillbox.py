"""Alliance pillbox: the Alliance's light defence, a machine gun nest (docs/ra2_buildings/allies/pillbox).

Footprint 1x1 cell (game/data/factions/alliance/buildings.yaml), origin at the centre of the footprint on the ground.
The composition follows the owner's references; the parts are our own:
    bunker    a low round steel bunker, a dark slot running round it under a broad lip
    roof      a flat round roof over the lip with a raised disc in its middle
    bags      two rows of sandbags round its foot
No emblems, no text. The disc on the roof takes the owner's colour, where the references have the blue. Height 0.46 to the top of
the roof. Budget 20000 triangles (a building), texture 512 (one cell).

Nodes (docs/art/model_standards.md, section 4):
    body            the bunker, the slot, the lip, the roof and the bags
    muzzle_0        empty in the slot at the front: where the gun fires from
    team_mask       the disc on the roof: the game paints it in the owner's colour
    base            the round plinth of trodden earth (docs/art/model_standards.md, "Фундамент")

In Blender, after tools/blender is on sys.path:
    import buildings.alliance_pillbox as apb
    print(apb.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

ID = "alliance_pillbox"
PREFIX = ID + "_"
SIZE = 512


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


EARTH, STEEL, DARK, SAND, TEAM = (_part(i) for i in range(5))
PARTS = 5

PLINTH_H = 0.03
SLOT = (0.2, 0.26)
LIP = 0.32
ROOF = 0.42


def _bag(p, centre, turn, length, width, height):
    """A sandbag: three rings, its belly fuller than its foot and top, its long side across the radius."""
    rot = Matrix.Rotation(turn, 3, 'Z')

    def ring(sl, sw, dz):
        return [centre + rot @ Vector((u * sl / 2, v * sw / 2, dz)) for u, v in ((1, 1), (-1, 1), (-1, -1), (1, -1))]

    p.loft([ring(length, width, 0.0), ring(length * 1.05, width * 1.1, height * 0.5), ring(length * 0.9, width * 0.9, height)], SAND)


# --- body -------------------------------------------------------------------------

def _bunker(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(PLINTH_H, 0.3), (SLOT[0], 0.28), (SLOT[0], 0.255), (SLOT[1], 0.255), (SLOT[1], 0.3),
                                     (LIP, 0.36), (LIP + 0.04, 0.36), (LIP + 0.04, 0.3), (ROOF, 0.28), (ROOF, 0.18)], STEEL, sides=24)
    p.turned((0.0, 0.0, 0.0), 'Z', [(SLOT[0] + 0.005, 0.258), (SLOT[1] - 0.005, 0.258)], DARK, sides=24)  # the dark slot
    for row, (z, r, n, phase) in enumerate(((PLINTH_H, 0.42, 14, 0.0), (PLINTH_H + 0.065, 0.4, 13, 0.5))):
        for i in range(n):
            a = 2 * math.pi * (i + phase) / n
            _bag(p, Vector((r * math.cos(a), r * math.sin(a), z)), a + math.pi / 2, 0.19, 0.11, 0.07)


def _team(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(ROOF, 0.19), (ROOF + 0.035, 0.18), (ROOF + 0.04, 0.15), (ROOF + 0.04, 0.01)], TEAM, sides=24)


# --- material -----------------------------------------------------------------------

def _material():
    """Trodden earth, grey steel, the dark slot, sandbags, a light team zone."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    grain = g.noise(40.0, 4.0)

    earth = g.mix(grain, lin((0.40, 0.36, 0.28)), lin((0.48, 0.43, 0.34)))
    steel = g.mix(grain, lin((0.52, 0.54, 0.58)), lin((0.62, 0.64, 0.68)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.09, 0.09, 0.10)))
    sand = g.mix(grain, lin((0.46, 0.44, 0.34)), lin((0.56, 0.53, 0.42)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = earth
    for m, col in zip(mask[1:], (steel, dark, sand, team)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.95, 0.4, 0.7, 0.9, 0.35))
    metallic = weighted((0.0, 0.6, 0.3, 0.0, 0.4))
    normal = g.bump(g.noise(80.0, 3.0), 0.03, 0.004)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _clear():
    for obj in [o for o in bpy.data.objects if o.name.startswith(PREFIX)]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def _empty(name, point):
    node = bpy.data.objects.new(PREFIX + name, None)
    node.empty_display_type = 'PLAIN_AXES'
    node.empty_display_size = 0.05
    node.location = Vector(point)
    bpy.context.scene.collection.objects.link(node)
    return node


def _drop_bottoms(objs):
    """Deletes the faces that look down onto the plinth or the ground: never seen, and black in the bake."""
    for obj in objs:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        low = [f for f in bm.faces if f.normal.z < -0.99 and (obj.matrix_world @ f.calc_center_median()).z < PLINTH_H + 1e-3]
        bmesh.ops.delete(bm, geom=low, context='FACES')
        bm.to_mesh(obj.data)
        bm.free()


def build():
    _clear()
    body = Parts()
    _bunker(body)
    body = body.finish(PREFIX + "body")
    team = Parts()
    _team(team)
    team = team.finish(PREFIX + "team_mask")
    base = Parts()
    base.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.49), (PLINTH_H, 0.47), (PLINTH_H, 0.01)], EARTH, sides=24)
    base = base.finish(PREFIX + "base")
    meshes = [body, team, base]
    _drop_bottoms([body, base])
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, [_empty("muzzle_0", (0.0, -0.27, (SLOT[0] + SLOT[1]) / 2))], mat, graph


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
