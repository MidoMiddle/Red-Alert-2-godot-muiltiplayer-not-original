"""Shared steps for the Blender builds of map elements (Blender 5.x, Cycles).

A family module in tools/blender/families/ builds geometry and a Graph material.
This module bakes albedo, normal and ORM, checks the bakes, writes PNG files,
wires the final material, exports the GLB and saves the .blend.
Rules for sizes, nodes and textures: docs/map_elements/generation_requirements.md.

Colours given to Graph are sRGB values: Blender writes baked pixels to PNG without
a colour conversion, so these numbers are what Godot reads back.
"""

import contextlib
import json
import os
import struct

import bpy

REPO = "/Users/elai/store/Other Projects/red_alert_2_godot"
SRC_ROOT = os.path.join(REPO, "assets_src", "models")
GLB_ROOT = os.path.join(REPO, "game", "assets", "models")


# --- geometry ---------------------------------------------------------------

def add_box(bm, lo, hi):
    """Axis-aligned box between the minimum corner lo and the maximum corner hi."""
    x0, y0, z0 = lo
    x1, y1, z1 = hi
    v = [bm.verts.new(p) for p in (
        (x0, y0, z0), (x1, y0, z0), (x1, y1, z0), (x0, y1, z0),
        (x0, y0, z1), (x1, y0, z1), (x1, y1, z1), (x0, y1, z1))]
    for q in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
              (2, 3, 7, 6), (1, 2, 6, 5), (0, 4, 7, 3)):
        bm.faces.new([v[i] for i in q])


def add_prism(bm, p1, p2, a, b):
    """Box along the segment p1-p2; a and b are half-sizes across it (mathutils.Vector)."""
    v = [bm.verts.new(p + a * sa + b * sb)
         for p in (p1, p2) for sa in (-1, 1) for sb in (-1, 1)]
    for q in ((0, 1, 3, 2), (4, 6, 7, 5), (0, 1, 5, 4),
              (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)):
        bm.faces.new([v[i] for i in q])


def make_object(name, bm, smooth=False):
    """Object with the mesh from bm, linked to the scene at the origin."""
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    if smooth:
        mesh.shade_smooth()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


@contextlib.contextmanager
def node_names(objs, names):
    """Gives objs their GLB node names for a while; a session object with such a name steps aside.

    Afterwards every object gets its own name back, so the scene the user has open keeps its names.
    """
    saved = []
    for obj, name in zip(objs, names):
        taken = bpy.data.objects.get(name)
        if taken is not None and taken not in objs:
            taken.name = name + "__session"
            saved.append((taken, name))
        saved.append((obj, obj.name))
        obj.name = name
    try:
        yield
    finally:
        for item, name in reversed(saved):
            item.name = name


def select_only(obj):
    """Selects one object, or a list of objects, and makes the first one active."""
    objs = list(obj) if isinstance(obj, (list, tuple)) else [obj]
    if bpy.context.object is not None and bpy.context.object.mode != 'OBJECT':
        bpy.ops.object.mode_set(mode='OBJECT')
    bpy.ops.object.select_all(action='DESELECT')
    for item in objs:
        item.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]


def uv_smart(obj):
    """UV islands with margins, for boxes and separate parts."""
    select_only(obj)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.smart_project(angle_limit=1.15, island_margin=0.02)
    bpy.ops.object.mode_set(mode='OBJECT')


def tri_count(obj):
    obj.data.calc_loop_triangles()
    return len(obj.data.loop_triangles)


# --- material graph ----------------------------------------------------------

def new_material(name):
    old = bpy.data.materials.get(name)
    if old is not None:
        bpy.data.materials.remove(old)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    return mat


def sock(node, ident, outputs=False):
    """Socket by identifier: Mix nodes reuse the same names, identifiers do not."""
    pool = node.outputs if outputs else node.inputs
    for s in pool:
        if s.identifier == ident:
            return s
    raise KeyError(f"{node.bl_idname}: no socket {ident!r}, have {[s.identifier for s in pool]}")


class Graph:
    """Builds a material graph. Colour arguments are sRGB tuples; numbers are factors."""

    def __init__(self, mat):
        self.nt = mat.node_tree
        self.nodes = self.nt.nodes
        self.nodes.clear()
        self.x = 0

    def add(self, kind, **props):
        node = self.nodes.new(kind)
        for key, value in props.items():
            setattr(node, key, value)
        node.location = (self.x, 0)
        self.x += 240
        return node

    def put(self, node, ident, value):
        sock(node, ident).default_value = value

    def link(self, out, node, ident):
        self.nt.links.new(out, sock(node, ident))

    def math(self, op, a, b=0.0, c=0.0, clamp=False):
        node = self.add('ShaderNodeMath', operation=op, use_clamp=clamp)
        for i, arg in enumerate((a, b, c)):
            if isinstance(arg, (int, float)):
                node.inputs[i].default_value = float(arg)
            else:
                self.nt.links.new(arg, node.inputs[i])
        return node.outputs[0]

    def clamp01(self, a):
        return self.math('ADD', a, 0.0, clamp=True)

    def mix(self, fac, a, b):
        node = self.add('ShaderNodeMix', data_type='RGBA', clamp_factor=True)
        if isinstance(fac, (int, float)):
            self.put(node, 'Factor_Float', float(fac))
        else:
            self.link(fac, node, 'Factor_Float')
        for ident, val in (('A_Color', a), ('B_Color', b)):
            if isinstance(val, tuple):
                self.put(node, ident, (*val, 1.0))
            else:
                self.link(val, node, ident)
        return sock(node, 'Result_Color', outputs=True)

    def noise(self, scale, detail):
        node = self.add('ShaderNodeTexNoise')
        self.put(node, 'Scale', scale)
        self.put(node, 'Detail', detail)
        return sock(node, 'Fac', outputs=True)

    def object_coord(self):
        return sock(self.add('ShaderNodeTexCoord'), 'Object', outputs=True)

    def normal_world(self):
        return sock(self.add('ShaderNodeNewGeometry'), 'Normal', outputs=True)

    def axis(self, vec, name):
        node = self.add('ShaderNodeSeparateXYZ')
        self.link(vec, node, 'Vector')
        return sock(node, name, outputs=True)

    def join(self, x, y, z):
        node = self.add('ShaderNodeCombineXYZ')
        for ident, val in (('X', x), ('Y', y), ('Z', z)):
            if isinstance(val, (int, float)):
                self.put(node, ident, float(val))
            else:
                self.link(val, node, ident)
        return sock(node, 'Vector', outputs=True)

    def ao(self, distance):
        node = self.add('ShaderNodeAmbientOcclusion', samples=24)
        self.put(node, 'Distance', distance)
        return sock(node, 'AO', outputs=True)

    def bump(self, height, strength, distance):
        node = self.add('ShaderNodeBump')
        self.put(node, 'Strength', strength)
        self.put(node, 'Distance', distance)
        self.link(height, node, 'Height')
        return sock(node, 'Normal', outputs=True)

    def combine(self, r, g, b):
        node = self.add('ShaderNodeCombineColor')
        for ident, val in (('Red', r), ('Green', g), ('Blue', b)):
            if isinstance(val, (int, float)):
                self.put(node, ident, float(val))
            else:
                self.link(val, node, ident)
        return sock(node, 'Color', outputs=True)

    def snow_mask(self, z_min, up_min):
        """1 on upward faces above z_min, 0 elsewhere, with soft edges."""
        up = self.clamp01(self.math('MULTIPLY_ADD', self.axis(self.normal_world(), 'Z'), 6.0, -6.0 * up_min))
        high = self.clamp01(self.math('MULTIPLY_ADD', self.axis(self.object_coord(), 'Z'), 40.0, -40.0 * z_min))
        return self.math('MULTIPLY', up, high)

    def finish(self, albedo, normal, orm):
        """BSDF and output used for baking; albedo and orm are the sources of the EMIT passes."""
        self.bsdf = self.add('ShaderNodeBsdfPrincipled')
        self.out = self.add('ShaderNodeOutputMaterial')
        self.nt.links.new(self.bsdf.outputs[0], self.out.inputs['Surface'])
        self.nt.links.new(albedo, self.bsdf.inputs['Base Color'])
        self.nt.links.new(normal, self.bsdf.inputs['Normal'])
        self.albedo_src = albedo
        self.orm_src = orm


# --- baking -------------------------------------------------------------------

def new_image(name, size, srgb):
    old = bpy.data.images.get(name)
    if old is not None:
        bpy.data.images.remove(old)
    img = bpy.data.images.new(name, size, size, alpha=False)
    img.colorspace_settings.name = 'sRGB' if srgb else 'Non-Color'
    return img


@contextlib.contextmanager
def only_for_bake(obj):
    """Hides the other meshes from rendering while obj (or a list) bakes, so AO sees nothing else."""
    keep = list(obj) if isinstance(obj, (list, tuple)) else [obj]
    hidden = []
    for other in bpy.data.objects:
        if other not in keep and other.type == 'MESH' and not other.hide_render:
            other.hide_render = True
            hidden.append(other)
    try:
        yield
    finally:
        for other in hidden:
            other.hide_render = False


def bake_pass(graph, obj, bake_type, src, img):
    """One bake into img. For EMIT, src is wired to an emission shader, then the BSDF is restored."""
    nt = graph.nt
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    nt.nodes.active = tex
    select_only(obj)
    if bake_type == 'EMIT':
        emit = nt.nodes.new('ShaderNodeEmission')
        nt.links.new(src, emit.inputs['Color'])
        nt.links.new(emit.outputs[0], graph.out.inputs['Surface'])
        bpy.ops.object.bake(type='EMIT', margin=8, use_clear=True)
        nt.links.new(graph.bsdf.outputs[0], graph.out.inputs['Surface'])
        nt.nodes.remove(emit)
    else:
        bpy.ops.object.bake(type=bake_type, margin=8, use_clear=True, normal_space='TANGENT')
    nt.nodes.remove(tex)


def _mean(img, channel):
    px = img.pixels[:]
    return sum(px[channel::4]) / (len(px) // 4)


def check_bakes(albedo, normal, orm):
    """Raises before anything is written if a bake came out black or flat."""
    lum = sum(_mean(albedo, c) for c in range(3)) / 3
    if lum < 0.15:
        raise RuntimeError(f"albedo is too dark: mean {lum:.3f}")
    if _mean(normal, 2) < 0.9:
        raise RuntimeError(f"normal blue mean {_mean(normal, 2):.3f}")
    if _mean(orm, 0) < 0.25:
        raise RuntimeError(f"AO mean {_mean(orm, 0):.3f}")


def bake_material(obj, graph, size, base, out_dir):
    """Bakes albedo, ORM and normal of the graph, checks them, saves PNG files and packs them."""
    scene = bpy.context.scene
    scene.render.engine = 'CYCLES'
    scene.cycles.samples = 64
    albedo = new_image(base + "_albedo", size, srgb=True)
    orm = new_image(base + "_orm", size, srgb=False)
    normal = new_image(base + "_normal", size, srgb=False)
    with only_for_bake(obj):
        bake_pass(graph, obj, 'EMIT', graph.albedo_src, albedo)
        bake_pass(graph, obj, 'EMIT', graph.orm_src, orm)
        bake_pass(graph, obj, 'NORMAL', None, normal)
    check_bakes(albedo, normal, orm)
    imgs = {"albedo": albedo, "normal": normal, "orm": orm}
    os.makedirs(out_dir, exist_ok=True)
    for key, img in imgs.items():
        img.filepath_raw = os.path.join(out_dir, f"{base}_{key}.png")
        img.file_format = 'PNG'
        img.save()
        img.pack()
    return imgs


def wire_final(mat, imgs):
    """Final material for export: textures on the BSDF, ORM split into G (roughness) and B (metal)."""
    nt = mat.node_tree
    nt.nodes.clear()
    out = nt.nodes.new('ShaderNodeOutputMaterial')
    bsdf = nt.nodes.new('ShaderNodeBsdfPrincipled')
    nmap = nt.nodes.new('ShaderNodeNormalMap')
    split = nt.nodes.new('ShaderNodeSeparateColor')
    tex = {}
    for key in ('albedo', 'normal', 'orm'):
        tex[key] = nt.nodes.new('ShaderNodeTexImage')
        tex[key].image = imgs[key]
    nt.links.new(bsdf.outputs[0], out.inputs['Surface'])
    nt.links.new(tex['albedo'].outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(tex['normal'].outputs['Color'], nmap.inputs['Color'])
    nt.links.new(nmap.outputs['Normal'], bsdf.inputs['Normal'])
    nt.links.new(tex['orm'].outputs['Color'], split.inputs['Color'])
    nt.links.new(split.outputs['Green'], bsdf.inputs['Roughness'])
    nt.links.new(split.outputs['Blue'], bsdf.inputs['Metallic'])


# --- export and checks ----------------------------------------------------------

def export_glb(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True,
                              export_yup=True, export_apply=True,
                              export_vertex_color='NONE', export_image_format='AUTO')


def glb_report(path):
    """Node names, translations and counts from the JSON chunk of a GLB."""
    with open(path, 'rb') as f:
        magic, _, _ = struct.unpack('<4sII', f.read(12))
        if magic != b'glTF':
            raise RuntimeError(f"{path} is not a GLB file")
        length, _ = struct.unpack('<II', f.read(8))
        data = json.loads(f.read(length))
    nodes = [(n.get('name'), n.get('translation', [0.0, 0.0, 0.0])) for n in data.get('nodes', [])]
    return (f"  {os.path.getsize(path)} bytes, nodes {nodes}, "
            f"materials {len(data.get('materials', []))}, images {len(data.get('images', []))}")


def save_blend(objs, mat, imgs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    blocks = set(objs) | {o.data for o in objs} | {mat} | set(imgs.values())
    bpy.data.libraries.write(path, blocks, fake_user=True)
