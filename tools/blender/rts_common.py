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
from mathutils import Matrix, Vector

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


def add_stick(bm, p1, p2, half):
    """A thin square bar from p1 to p2, its sides perpendicular to its length (half is the half-size)."""
    d = (p2 - p1).normalized()
    a = d.cross(Vector((0.0, 0.0, 1.0)))
    if a.length < 1e-6:
        a = d.cross(Vector((1.0, 0.0, 0.0)))
    a = a.normalized() * half
    b = d.cross(a).normalized() * half
    add_prism(bm, p1, p2, a, b)


def add_sandbag(bm, x, y, z, turn, length, width, height):
    """A sandbag: a pillow of ten quads, its foot on z at (x, y), its long side along turn (radians about the vertical).

    The middle ring is a little larger than the foot and the top, so the bag bulges as a filled one does.
    """
    turned = Matrix.Rotation(turn, 3, 'Z')
    centre = Vector((x, y, z))

    def ring(size_l, size_w, dz):
        corners = []
        for u, v in ((1, 1), (-1, 1), (-1, -1), (1, -1)):
            corners.append(bm.verts.new(centre + turned @ Vector((u * size_l / 2, v * size_w / 2, dz))))
        return corners

    foot = ring(length, width, 0.0)
    belly = ring(length * 1.04, width * 1.08, height * 0.5)
    top = ring(length * 0.9, width * 0.9, height)
    for lower, upper in ((foot, belly), (belly, top)):
        for i in range(4):
            j = (i + 1) % 4
            bm.faces.new((lower[i], lower[j], upper[j], upper[i]))
    bm.faces.new(foot)
    bm.faces.new(top)


def fire_nodes(points):
    """Empty nodes fire_1, fire_2… at the points: the game stands the flames of a damaged building on them."""
    nodes = []
    for i, point in enumerate(points, start=1):
        node = bpy.data.objects.new(f"fire_{i}", None)
        node.empty_display_type = 'PLAIN_AXES'
        node.empty_display_size = 0.12
        node.location = Vector(point)
        bpy.context.scene.collection.objects.link(node)
        nodes.append(node)
    return nodes


def fire_names(nodes):
    """The GLB names of the fire nodes, in the order fire_nodes made them."""
    return [f"fire_{i}" for i in range(1, len(nodes) + 1)]


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


def place_uv(obj, lo, hi):
    """Moves the object's UV layout into [lo, hi] of the texture, so the objects of one model do not overlap there."""
    for item in obj.data.uv_layers.active.data:
        item.uv = (lo + (hi - lo) * item.uv.x, item.uv.y)


def ruins_material(mat_name):
    """The material of ruins: plaster stumps, sooty at the foot, snow on the upward faces, rough all over."""
    mat = new_material(mat_name)
    g = Graph(mat)
    ao = g.ao(0.25)
    z = g.axis(g.object_coord(), 'Z')
    grain = g.noise(12.0, 4.0)
    plaster = g.mix(grain, (0.42, 0.38, 0.34), (0.52, 0.48, 0.44))
    soot = g.clamp01(g.math('MULTIPLY_ADD', z, -3.0, 1.0))  # black at the foot, clean above about a third of a metre
    base = g.mix(soot, plaster, (0.10, 0.09, 0.08))
    base = g.mix(g.snow_mask(0.2, 0.5), base, (0.92, 0.93, 0.95))
    rough = g.math('MULTIPLY_ADD', grain, 0.0, 0.9)
    metal = g.math('MULTIPLY', grain, 0.0)
    normal = g.bump(g.noise(18.0, 3.0), 0.08, 0.01)
    g.finish(base, normal, g.combine(ao, rough, metal))
    return mat, g


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


# --- layout: rows and groups in the file ----------------------------------------
#
# The rule of the file: the models lie in rows, one row per group, the models of a row one after another along x. A model is a
# collection named after the model, in its row; an empty root named after the model stands at its place, and every part of the model
# is a child of the root. So the root moves the whole model, and the outliner shows which roof belongs to which house. The export
# takes the root to the origin for a while (at_origin), so the GLB keeps the positions the model was built with.

LAYOUT_STEP = 6.5  # along x, from one model of a row to the next
ROW_STEP = 10.0  # along y, from one row to the next

ROWS = (
    "1. Жильё",
    "2. Техздания",
    "3. Промышленность",
    "4. Препятствия",
    "5. Мосты",
    "6. Заборы и стены",
    "7. Снег",
    "8. Руда",
    "9. Здания игроков",
)

# Where each model stands: (row, place in the row). The key is the name of the model: its collection, its root and its file.
LAYOUT = {
    "building_house_small": (0, 0),
    "building_house_small_occupied": (0, 1),
    "building_house_small_damaged": (0, 2),
    "building_house_small_damaged_occupied": (0, 3),
    "building_house_small_abandoned": (0, 4),
    "building_house_ruins": (0, 5),
    "building_apartment_block": (0, 6),
    "building_apartment_block_occupied": (0, 7),
    "building_apartment_block_damaged": (0, 8),
    "building_apartment_block_damaged_occupied": (0, 9),
    "building_apartment_block_abandoned": (0, 10),
    "building_apartment_block_ruins": (0, 11),
    "tech_oil_derrick": (1, 0),
    "tech_oil_derrick_abandoned": (1, 1),
    "tech_oil_derrick_ruins": (1, 2),
    "smokestack": (2, 0),
    "cooling_tower": (2, 1),
    "boiler_house": (2, 2),
    "factory_hall": (2, 3),
    "fuel_tank": (2, 4),
    "anti_tank_hedgehog": (3, 0),
    "prop_supply_crate": (3, 1),
    "pipeline_straight": (3, 2),
    "pipeline_corner": (3, 3),
    "bridge_span": (4, 0),
    "bridge_span_damaged": (4, 1),
    "fence_wood_section": (5, 0),
    "fence_mesh_section": (5, 1),
    "wall_stone_section": (5, 2),
    "snowdrift_01": (6, 0),
    "snowdrift_02": (6, 1),
    "ore_source": (7, 0),
    "ore_cluster": (7, 1),
    "gem_cluster": (7, 2),
    "tesla_coil": (8, 0),
    "barracks": (8, 1),
    "alliance_barracks": (8, 2),
    "war_factory": (8, 3),
    "alliance_war_factory": (8, 4),
    "alliance_power_plant": (8, 5),
    "reactor": (8, 6),
    "alliance_air_force_command": (8, 7),
    "radar": (8, 8),
    "tech_center": (8, 9),
    "alliance_battle_lab": (8, 10),
    "alliance_service_depot": (8, 11),
    "alliance_ore_purifier": (8, 12),
    "service_depot": (8, 13),
    "nuclear_reactor": (8, 14),
    "alliance_construction_yard": (8, 15),
    "construction_yard": (8, 16),
    "alliance_naval_shipyard": (8, 17),
    "naval_shipyard": (8, 18),
    "nuclear_missile_silo": (8, 19),
    "alliance_weather_control_device": (8, 20),
    "alliance_chronosphere": (8, 21),
    "iron_curtain": (8, 22),
    "alliance_pillbox": (8, 23),
    "sentry_bunker": (8, 24),
    "wall": (8, 25),
    "alliance_wall": (8, 26),
    "alliance_prism_tower": (8, 27),
    "alliance_patriot_missile_system": (8, 28),
    "alliance_spy_satellite_uplink": (8, 29),
    "flak_cannon": (8, 30),
    "alliance_gap_generator": (8, 31),
    "alliance_grand_cannon": (8, 32),
}


def _in(parent, coll):
    return any(child == coll for child in parent.children)


def _put_collection(coll, parent):
    """Links the collection under parent, and takes it out of the collection it was in before."""
    for owner in [bpy.context.scene.collection, *bpy.data.collections]:
        if owner != parent and _in(owner, coll):
            owner.children.unlink(coll)
    if not _in(parent, coll):
        parent.children.link(coll)
    return coll


def _put_object(obj, coll):
    """Links the object into the collection alone: it leaves the collections it was in."""
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    coll.objects.link(obj)


def _row_collection(row):
    coll = bpy.data.collections.get(ROWS[row]) or bpy.data.collections.new(ROWS[row])
    return _put_collection(coll, bpy.context.scene.collection)


def place(key, root):
    """Moves the root of a model to its place in the file: the y of its row, the x of its place in the row."""
    row, slot = LAYOUT[key]
    root.location = (slot * LAYOUT_STEP, row * ROW_STEP, 0.0)


def arrange(objs, key):
    """Puts the parts of one model in their place: the collection key under its row, and an empty root named key at the place of the
    model, with the parts as its children at their positions relative to it. Parts of an older build of the same model are removed.
    Returns the root."""
    row, _ = LAYOUT[key]
    coll = _put_collection(bpy.data.collections.get(key) or bpy.data.collections.new(key), _row_collection(row))
    root = bpy.data.objects.get(key)
    if root is None:
        root = bpy.data.objects.new(key, None)
        root.empty_display_type = 'PLAIN_AXES'
        root.empty_display_size = 0.5
    _put_object(root, coll)
    for old in {*coll.objects, *root.children}:
        if old != root and old not in objs:
            bpy.data.objects.remove(old, do_unlink=True)
    for obj in objs:
        obj.parent = root
        obj.matrix_parent_inverse = Matrix.Identity(4)
        _put_object(obj, coll)
    place(key, root)
    return root


def park(key):
    """Puts a model made by another script in its place, its parts unchanged: an empty root named key stands at the place of the model
    and the parts become its children, at the positions they have."""
    row, _ = LAYOUT[key]
    coll = _put_collection(bpy.data.collections[key], _row_collection(row))
    parts = list(coll.objects)
    clash = bpy.data.objects.get(key)
    if clash is not None:  # a part named like the model: it keeps its place, the root takes the name
        clash.name = key + "_part"
    root = bpy.data.objects.new(key, None)
    root.empty_display_type = 'PLAIN_AXES'
    root.empty_display_size = 0.5
    root.location = (parts[0].location.x, parts[0].location.y, 0.0) if parts else (0.0, 0.0, 0.0)
    coll.objects.link(root)
    bpy.context.view_layer.update()
    for obj in parts:
        obj.parent = root
        obj.matrix_parent_inverse = root.matrix_world.inverted()
    place(key, root)
    return root


def _root_of(objs):
    """The root of a model: the empty its parts are children of; None for parts that are not arranged."""
    for obj in objs:
        if obj.parent is not None:
            return obj.parent
    return None


@contextlib.contextmanager
def at_origin(objs):
    """The root of the model at the origin for a while, so the parts are exported where the model was built."""
    root = _root_of(objs)
    saved = tuple(root.location) if root is not None else None
    if root is not None:
        root.location = (0.0, 0.0, 0.0)
    try:
        yield
    finally:
        if root is not None:
            root.location = saved


# --- export and checks ----------------------------------------------------------

def export_glb(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with at_origin(objs):
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
    blocks = set(objs) | {o.data for o in objs if o.data is not None} | {mat} | set(imgs.values())
    root = _root_of(objs)
    if root is not None:  # the root and the collection of the model: the file keeps its groups
        blocks |= {root} | set(root.users_collection)
    bpy.data.libraries.write(path, blocks, fake_user=True)
