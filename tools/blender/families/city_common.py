"""Shared steps of the city set (docs/city_elements): surface parts, small shapes, the material's part masks and the build and bake of
one state of an element.

A city module builds its objects from Parts (units/parts.py), each face painted with a surface part; its material reads the part back
and gives each its colour. The steps after the geometry are the same for every element: drop the hidden bottoms, smooth by angle, one
UV layout packed by the islands' real shape, bake, put the model in the row of the city in the file, save the .blend and export the GLB.
"""

import math
import os

import bmesh
import bpy
from mathutils import Euler, Vector

import rts_common as rc
from units.parts import PART, uv_together

FOLDER = "city"  # game/assets/models/city/<id>/, assets_src/models/city/<id>/
PART_SCALE = 20  # a part i is written as i / PART_SCALE in the red channel: up to 20 parts
ISLAND_GAP = 4.0  # pixels between the UV islands at the size of the bake: less, and a colour bleeds over a seam (a red line on a roof)


def part(index):
    """Surface part i, written to PART in the red channel; the material picks it back."""
    return (index / PART_SCALE, 0.0, 0.0, 1.0)


# --- shapes -------------------------------------------------------------------------

def steps(a, b, step):
    """Points from a to b, about step apart, both ends included."""
    n = max(1, int((b - a) / step))
    return [a + (b - a) * i / n for i in range(n + 1)]


def block(p, centre, size, turn, painted):
    """A box of the given size turned about its middle (radians about x, y, z): a brick, a beam, a slab."""
    sx, sy, sz = (v / 2 for v in size)
    c = Vector(centre)
    m = Euler(turn).to_matrix()
    p.loft([[c + m @ Vector((u * sx, v * sy, w * sz)) for u, v in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for w in (-1, 1)], painted)


def bag(p, centre, turn, painted, length=0.24, width=0.15, height=0.09):
    """A sandbag: three rings, its belly fuller than its foot and its top, its long side along turn (radians about the vertical)."""
    m = Euler((0.0, 0.0, turn)).to_matrix()
    c = Vector(centre)

    def ring(sl, sw, dz):
        return [c + m @ Vector((u * sl / 2, v * sw / 2, dz)) for u, v in ((1, 1), (-1, 1), (-1, -1), (1, -1))]

    p.loft([ring(length, width, 0.0), ring(length * 1.05, width * 1.1, height * 0.5), ring(length * 0.9, width * 0.9, height)], painted)


class Facade:
    """The four outer walls of a box-shaped building, x = ±x0, the front at y = -y0 and the back at y = back (y0 when not given):
    boxes and windows placed on them by u along the wall (x on the front and the back, y on the sides), z up and d out of the wall
    (0 its face, less than 0 into it)."""

    def __init__(self, x0, y0, back=None):
        self.x0, self.y0 = x0, y0
        self.back = y0 if back is None else back

    def box(self, p, wall, u0, u1, z0, z1, d0, d1, painted):
        x0, y0 = self.x0, self.y0
        if wall == "front":
            lo, hi = (u0, -y0 - d1, z0), (u1, -y0 - d0, z1)
        elif wall == "back":
            lo, hi = (u0, self.back + d0, z0), (u1, self.back + d1, z1)
        elif wall == "left":
            lo, hi = (-x0 - d1, u0, z0), (-x0 - d0, u1, z1)
        else:
            lo, hi = (x0 + d0, u0, z0), (x0 + d1, u1, z1)
        p.box(lo, hi, painted)

    def out(self, wall):
        """The outward direction of a wall."""
        return {"front": Vector((0, -1, 0)), "back": Vector((0, 1, 0)), "left": Vector((-1, 0, 0)), "right": Vector((1, 0, 0))}[wall]

    def point(self, wall, u, z, d=0.0):
        """The point at u along the wall, at z, d out of it."""
        x0, y0 = self.x0, self.y0
        base = {"front": Vector((u, -y0, z)), "back": Vector((u, self.back, z)), "left": Vector((-x0, u, z)), "right": Vector((x0, u, z))}[wall]
        return base + self.out(wall) * d

    def window(self, p, wall, u, z0, z1, half, glass, frame, bars=(1, 1), broken=None):
        """Glass (or the part broken, a dark hole, with no bars) in a frame of four bars with a sill under it; bars: how many
        vertical and horizontal glazing bars cross it."""
        self.box(p, wall, u - half, u + half, z0, z1, -0.01, 0.006, broken or glass)
        self.box(p, wall, u - half - 0.03, u + half + 0.03, z1, z1 + 0.03, -0.01, 0.022, frame)
        self.box(p, wall, u - half - 0.04, u + half + 0.04, z0 - 0.035, z0, -0.01, 0.045, frame)
        for a in (u - half - 0.03, u + half):
            self.box(p, wall, a, a + 0.03, z0, z1, -0.01, 0.022, frame)
        if broken:
            return
        vertical, horizontal = bars
        for k in range(1, vertical + 1):
            c = u - half + 2 * half * k / (vertical + 1)
            self.box(p, wall, c - 0.008, c + 0.008, z0, z1, 0.0, 0.012, frame)
        for k in range(1, horizontal + 1):
            c = z0 + (z1 - z0) * k / (horizontal + 1)
            self.box(p, wall, u - half, u + half, c - 0.008, c + 0.008, 0.0, 0.012, frame)


def made(p, build):
    """The faces that build adds to p, their normals worked out (a new face has none yet)."""
    before = set(p.bm.faces)
    build()
    faces = [f for f in p.bm.faces if f not in before]
    for f in faces:
        f.normal_update()
    return faces


def repaint(p, faces, painted):
    for f in faces:
        for loop in f.loops:
            loop[p.layer] = painted


# --- material -----------------------------------------------------------------------

def part_masks(g, count):
    """The masks of the parts 0..count-1: each 1 on the faces of its part, 0 elsewhere."""
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), float(PART_SCALE))
    return [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(count)]


def paint(g, masks, colours):
    """The albedo: colours[i] on part i."""
    albedo = colours[0]
    for m, col in zip(masks[1:], colours[1:]):
        albedo = g.mix(m, albedo, col)
    return albedo


def weighted(g, masks, values):
    """A number per part, as one value: roughness or metalness."""
    out = g.math('MULTIPLY', masks[0], values[0])
    for m, v in zip(masks[1:], values[1:]):
        out = g.math('MULTIPLY_ADD', m, v, out)
    return out


def band(g, z, lo, hi):
    """1 between the heights lo and hi, soft at its edges."""
    return g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 30.0, -30.0 * lo)), g.clamp01(g.math('MULTIPLY_ADD', z, -30.0, 30.0 * hi)))


def cracks(g, scale=9.0):
    """1 along thin random lines: cracks."""
    return g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.noise(scale, 4.0), 0.5)), -80.0, 1.0))


def glass_material(name, cracked=False):
    """A clear, slightly blue glass, see-through in the GLB (alpha blended), for the meshes a city element exports unbaked (run's
    extra); darker and cloudier when cracked."""
    old = bpy.data.materials.get(name)
    if old is not None:
        bpy.data.materials.remove(old)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (0.36, 0.42, 0.44, 1.0) if cracked else (0.45, 0.58, 0.62, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.3 if cracked else 0.05
    bsdf.inputs["Alpha"].default_value = 0.5 if cracked else 0.3
    try:
        mat.surface_render_method = 'BLENDED'
    except (AttributeError, TypeError):
        mat.blend_method = 'BLEND'
    return mat


# --- build and run -------------------------------------------------------------------

def clear(variant_id, nodes):
    """Removes the parts of an earlier build of this state (and the copies Blender named .001…), so the new ones get their names."""
    names = {f"{variant_id}_{n}" for n in nodes}
    for obj in [o for o in bpy.data.objects if o.name.split(".")[0] in names]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def drop_bottoms(objs, floor):
    """Deletes the faces that look down onto the foundation or the ground (below floor): never seen, and black in the bake."""
    for obj in objs:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        low = [f for f in bm.faces if f.normal.z < -0.99 and f.calc_center_median().z < floor + 1e-3]
        bmesh.ops.delete(bm, geom=low, context='FACES')
        bm.to_mesh(obj.data)
        bm.free()


def repack(meshes, margin=0.001):
    """Packs the islands again by their real shape, so the small ones fill the gaps the smart projection leaves. The margin is a
    share of the texture (0.001: two pixels at 2048): a building of many small boxes has thousands of islands, and a margin scaled
    to the islands left most of the texture empty."""
    rc.select_only(meshes)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(shape_method='CONCAVE', rotate=True, margin_method='FRACTION', margin=margin)
    bpy.ops.object.mode_set(mode='OBJECT')


def finish(variant_id, parts, floor, material):
    """Objects from the Parts, named <variant_id>_<node>; hidden bottoms dropped, smoothed, one packed UV layout, the material.

    parts: [(Parts, node name)]; material: (material, graph)."""
    meshes = [p.finish(f"{variant_id}_{name}") for p, name in parts]
    drop_bottoms(meshes, floor)
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    repack(meshes)
    mat, graph = material
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run(variant_id, meshes, mat, graph, size, fire_points=(), extra=()):
    """Bakes, puts the model in its place in the file, saves the .blend and exports the GLB; returns a report.

    The GLB nodes are the meshes' names without the state's id, and fire_1, fire_2… at fire_points. extra: meshes with a material of
    their own (glass), not baked, exported and saved with the rest."""
    nodes = rc.fire_nodes(fire_points) if fire_points else []
    extra = list(extra)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes + extra)}, "
              f"size {tuple(round(v, 3) for v in meshes[0].dimensions)}"]
    base = os.path.join(rc.SRC_ROOT, FOLDER, variant_id)
    repack(meshes, ISLAND_GAP / size)  # the gap between islands in pixels of this texture, or neighbours bleed into each other
    imgs = rc.bake_material(meshes, graph, size, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + extra + nodes
    rc.arrange(objs, variant_id)  # its place in the file: the row of the city
    glb = os.path.join(rc.GLB_ROOT, FOLDER, variant_id, variant_id + ".glb")
    names = [o.name.split(".")[0][len(variant_id) + 1:] for o in meshes + extra] + rc.fire_names(nodes)
    with rc.node_names(objs, names):
        rc.save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
