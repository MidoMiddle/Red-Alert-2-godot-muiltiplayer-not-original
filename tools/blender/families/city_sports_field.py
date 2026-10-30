"""City sports field: element 20 of the city set (docs/city_elements/20_sports_field). The field is no model (prompts.md, 4): a grass
texture and a decal of its lines; the one model is the goal, in its states (appearance.md, 4). City climate: dry daylight, no snow.

The goal: origin at the centre of the cell on the ground, its mouth to -Y. Made at the size the game draws it, as the bench (the game
does not squash map objects): 0.9 wide and 0.45 high, so it fits a cell, not the prompt's two high. The parts:
    frame     white painted steel: two posts and a crossbar at the front, lower posts at the back, bars along the ground and up the
              sides, a little worn
    net       the net over its back, top and sides: a node of its own with a material of its own, a fine white mesh see-through
              between its strings (a small grid picture, alpha blended), not baked
One GLB per state:
    city_sports_goal            whole
    city_sports_goal_damaged    damaged: a post bent, the crossbar sagging, the net torn off the back's right half
    city_sports_goal_ruins      destroyed: crushed flat on the ground, the frame skewed, the net slack on it
The textures (game/assets/textures/):
    terrain/city_sport_grass.png    a mown pitch, tiling: stripes from the mower, a few worn patches; 1024², no shadows
    decals/city_sport_lines.png     the white lines of a small pitch on a transparent ground: the edge, the centre line and circle,
                                    a box at each end; 1024², no text
In the gallery a field lies in the row of the city: the grass and the lines on a 3 by 3 plane (the texture is square).
No map object takes the goal yet, and the field is the map's texture (prompts.md, 4): none of it is in models.yaml.
Budget 500 triangles, texture 1024 (docs/city_elements/20_sports_field/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_sports_field as csf
    print(csf.run_all())
"""

import math
import os

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_sports_goal": "intact",
    "city_sports_goal_damaged": "damaged",
    "city_sports_goal_ruins": "ruins",
}
HALF_W = 0.45
FRONT_Y, BACK_Y = -0.2, 0.2
TOP = 0.45
BACK_TOP = 0.3
NET_CELL = 0.035  # the net's mesh size

WHITE, = (cc.part(i) for i in range(1))
PARTS = 1


# --- the goal ---------------------------------------------------------------------------

def _frame(p, variant):
    bent = variant == "damaged"
    left_top = Vector((-HALF_W, FRONT_Y, TOP))
    right_top = Vector((HALF_W + (0.06 if bent else 0.0), FRONT_Y - (0.05 if bent else 0.0), TOP - (0.08 if bent else 0.0)))
    p.bar((-HALF_W, FRONT_Y, 0.0), left_top, 0.014, WHITE)
    p.bar((HALF_W, FRONT_Y, 0.0), right_top, 0.014, WHITE)
    p.bar(left_top, right_top if not bent else (0.0, FRONT_Y, TOP - 0.06), 0.014, WHITE)
    if bent:
        p.bar((0.0, FRONT_Y, TOP - 0.06), right_top, 0.014, WHITE)
    for x in (-HALF_W, HALF_W):
        p.bar((x, BACK_Y, 0.0), (x, BACK_Y, BACK_TOP), 0.01, WHITE)
        p.bar((x, FRONT_Y, 0.008), (x, BACK_Y, 0.008), 0.008, WHITE)
        p.bar((x, BACK_Y, BACK_TOP), (x, FRONT_Y, TOP) if x < 0 or not bent else right_top, 0.008, WHITE)
    p.bar((-HALF_W, BACK_Y, 0.008), (HALF_W, BACK_Y, 0.008), 0.008, WHITE)
    p.bar((-HALF_W, BACK_Y, BACK_TOP), (HALF_W, BACK_Y, BACK_TOP), 0.008, WHITE)


def _net_mesh(name, variant):
    """The net as flat panels with their UVs in metres over the mesh size, so the grid repeats at the same size everywhere."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UVMap")
    panels = [
        [(-HALF_W, BACK_Y, 0.0), (HALF_W, BACK_Y, 0.0), (HALF_W, BACK_Y, BACK_TOP), (-HALF_W, BACK_Y, BACK_TOP)],  # the back
        [(-HALF_W, BACK_Y, BACK_TOP), (HALF_W, BACK_Y, BACK_TOP), (HALF_W, FRONT_Y, TOP), (-HALF_W, FRONT_Y, TOP)],  # the top
        [(-HALF_W, FRONT_Y, 0.0), (-HALF_W, BACK_Y, 0.0), (-HALF_W, BACK_Y, BACK_TOP), (-HALF_W, FRONT_Y, TOP)],  # the left side
        [(HALF_W, BACK_Y, 0.0), (HALF_W, FRONT_Y, 0.0), (HALF_W, FRONT_Y, TOP), (HALF_W, BACK_Y, BACK_TOP)],  # the right side
    ]
    if variant == "damaged":  # the back's right half torn off
        panels[0] = [(-HALF_W, BACK_Y, 0.0), (0.05, BACK_Y, 0.0), (0.0, BACK_Y, BACK_TOP), (-HALF_W, BACK_Y, BACK_TOP)]
    for quad in panels:
        verts = [bm.verts.new(v) for v in quad]
        face = bm.faces.new(verts)
        a, b = Vector(quad[0]), Vector(quad[1])
        across = (b - a).normalized()
        up = Vector(quad[3]) - a
        up = (up - across * up.dot(across)).normalized()
        for loop in face.loops:
            d = loop.vert.co - a
            loop[uv].uv = (d.dot(across) / NET_CELL, d.dot(up) / NET_CELL)
    mesh = bpy.data.meshes.new(name)
    bm.to_mesh(mesh)
    bm.free()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _net_image():
    """A small white grid on a transparent ground: one cell of the net, repeated over the panels."""
    name = "city_sports_goal_net"
    size = 64
    img = bpy.data.images.get(name) or bpy.data.images.new(name, size, size, alpha=True)
    px = np.zeros((size, size, 4), dtype=np.float32)
    px[..., :3] = 0.92
    line = np.zeros(size, dtype=bool)
    line[:5] = True
    px[..., 3] = (line[None, :] | line[:, None]).astype(np.float32)
    img.pixels.foreach_set(px.ravel())
    img.pack()
    return img


def _net_material(name):
    old = bpy.data.materials.get(name)
    if old is not None:
        bpy.data.materials.remove(old)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = _net_image()
    tex.interpolation = 'Closest'
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
    bsdf.inputs["Roughness"].default_value = 0.8
    try:
        mat.surface_render_method = 'BLENDED'
    except (AttributeError, TypeError):
        mat.blend_method = 'BLEND'
    mat.use_backface_culling = False
    return mat


def _material(name):
    """White painted steel, worn to grey in places."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    white = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(14.0, 4.0), 6.0, -3.6)), lin((0.92, 0.92, 0.90)), lin((0.58, 0.58, 0.57)))
    g.finish(white, g.bump(g.noise(40.0, 3.0), 0.02, 0.002), g.combine(ao, 0.5, 0.15))
    return mat, g


def build(variant_id):
    cc.clear(variant_id, ("frame", "net"))
    variant = VARIANTS[variant_id]
    frame = Parts()
    _frame(frame, variant)
    meshes, mat, graph = cc.finish(variant_id, [(frame, "frame")], 0.0, _material(variant_id + "_mat"))
    net = _net_mesh(f"{variant_id}_net", variant)
    net.data.materials.append(_net_material(variant_id + "_net_mat"))
    if variant == "ruins":
        # Down on the ground: the goal crushed flat by a tread, its frame skewed, the net slack over it.
        m = Matrix(((1.0, 0.0, 0.5, 0.0), (0.0, 1.0, 0.35, 0.0), (0.0, 0.0, 0.12, 0.012), (0.0, 0.0, 0.0, 1.0)))
        for obj in meshes + [net]:
            obj.data.transform(m)
    return meshes, mat, graph, [net]


def run(variant_id):
    """Builds, bakes the frame, saves the .blend, exports the GLB with the nodes frame and net (the net with its own material)."""
    meshes, mat, graph, extra = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE, extra=extra)


# --- the field's textures -----------------------------------------------------------------

TEX = os.path.join(rc.REPO, "game", "assets", "textures")


def _tile_noise(rng, cells, size=SIZE):
    """Smooth value noise that tiles: its grid wraps round."""
    grid = rng.random((cells, cells))
    t = np.linspace(0.0, cells, size, endpoint=False)
    i = t.astype(int)
    f = t - i
    f = f * f * (3 - 2 * f)
    j = (i + 1) % cells
    a, b, c, d = grid[np.ix_(i, i)], grid[np.ix_(i, j)], grid[np.ix_(j, i)], grid[np.ix_(j, j)]
    fu, fv = f[None, :], f[:, None]
    return (a * (1 - fu) + b * fu) * (1 - fv) + (c * (1 - fu) + d * fu) * fv


def grass():
    """The pitch's mown grass, tiling: green, two shades in stripes from the mower, grain, a few worn yellowish patches."""
    rng = np.random.default_rng(21)
    v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    stripes = (np.floor(u * 8) % 2)[..., None]
    fine = _tile_noise(rng, 128)[..., None]
    mid = _tile_noise(rng, 16)[..., None]
    worn = np.clip((_tile_noise(rng, 6) - 0.62) * 6, 0, 1)[..., None]
    light, dark = np.array([0.36, 0.55, 0.22]), np.array([0.30, 0.48, 0.18])
    rgb = light * (1 - stripes) + dark * stripes
    rgb = rgb * (0.85 + 0.25 * fine) * (0.92 + 0.12 * mid)
    rgb = rgb * (1 - worn * 0.7) + np.array([0.56, 0.52, 0.32]) * worn * 0.7
    return np.concatenate([np.clip(rgb, 0, 1), np.ones((SIZE, SIZE, 1))], axis=2).astype(np.float32)


def lines():
    """The white lines of a small pitch on a transparent ground, the pitch along the texture's length (v)."""
    v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    w = 5.0 / SIZE  # the line's half width
    def band(a, lo, hi, at):
        return (np.abs(a - at) < w) & (lo <= (u if a is v else v)) & ((u if a is v else v) <= hi)
    m = 0.05
    paint = band(u, m, 1 - m, m) | band(u, m, 1 - m, 1 - m) | band(v, m, 1 - m, m) | band(v, m, 1 - m, 1 - m)
    paint |= band(v, m, 1 - m, 0.5)  # the centre line
    paint |= np.abs(np.hypot(u - 0.5, v - 0.5) - 0.12) < w  # the centre circle
    paint |= np.hypot(u - 0.5, v - 0.5) < 2.2 * w  # the centre spot
    for end in (m, 1 - m):  # a box at each end
        depth = 0.16 if end == m else 1 - m - 0.16
        lo, hi = (min(end, depth), max(end, depth))
        paint |= band(v, 0.3, 0.7, depth) | band(u, lo, hi, 0.3) | band(u, lo, hi, 0.7)
    rng = np.random.default_rng(5)
    wear = np.clip(0.85 + 0.3 * _tile_noise(rng, 64), 0, 1)
    rgba = np.zeros((SIZE, SIZE, 4))
    rgba[..., :3] = 0.95
    rgba[..., 3] = paint * wear
    return rgba.astype(np.float32)


def _save(path, name, rgba):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    img = bpy.data.images.get(name) or bpy.data.images.new(name, SIZE, SIZE, alpha=True)
    img.colorspace_settings.name = 'sRGB'
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    img.pack()
    return img


def textures():
    g = _save(os.path.join(TEX, "terrain", "city_sport_grass.png"), "city_sport_grass", grass())
    l = _save(os.path.join(TEX, "decals", "city_sport_lines.png"), "city_sport_lines", lines())
    return g, l


def _field_gallery(grass_img, lines_img):
    """A field in the gallery: a 3 by 3 plane (the texture is square) of grass with the lines over it."""
    key = "city_sports_field"
    for obj in [o for o in bpy.data.objects if o.name.split(".")[0] in (f"{key}_grass", f"{key}_lines")]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    objs = [_plane(f"{key}_grass", 0.0, (1.5, 1.5), _image_material("city_sport_grass_mat", grass_img, False, repeat=2.0)),
            _plane(f"{key}_lines", 0.003, (1.5, 1.5), _image_material("city_sport_lines_mat", lines_img, True))]
    rc.arrange(objs, key)


def _plane(name, z, half, mat, repeat=1.0):
    hx, hy = half
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(-hx, -hy, z), (hx, -hy, z), (hx, hy, z), (-hx, hy, z)], [], [(0, 1, 2, 3)])
    mesh.uv_layers.new(name="UVMap")
    for loop, uv in zip(mesh.uv_layers[0].data, ((0, 0), (1, 0), (1, 1), (0, 1))):
        loop.uv = uv
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _image_material(name, img, alpha, repeat=1.0):
    old = bpy.data.materials.get(name)
    if old is not None:
        bpy.data.materials.remove(old)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    if repeat != 1.0:
        mapping = nt.nodes.new('ShaderNodeMapping')
        coord = nt.nodes.new('ShaderNodeTexCoord')
        mapping.inputs["Scale"].default_value = (repeat, repeat, 1.0)
        nt.links.new(coord.outputs['UV'], mapping.inputs['Vector'])
        nt.links.new(mapping.outputs['Vector'], tex.inputs['Vector'])
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs["Roughness"].default_value = 0.9
    if alpha:
        nt.links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
        try:
            mat.surface_render_method = 'BLENDED'
        except (AttributeError, TypeError):
            mat.blend_method = 'BLEND'
    return mat


def run_all():
    report = [run(variant_id).splitlines()[0] for variant_id in VARIANTS]
    grass_img, lines_img = textures()
    _field_gallery(grass_img, lines_img)
    report.append("textures: city_sport_grass, city_sport_lines")
    return "\n".join(report)
