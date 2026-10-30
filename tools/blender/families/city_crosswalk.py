"""City pedestrian crossing: element 18 of the city set (docs/city_elements/18_pedestrian_crossing). No model: a decal over the road
(prompts.md, 1), the cell stays a road. This script draws its texture in its three looks (appearance.md, 4) and lays each on a square
of asphalt in the gallery file.

The texture: 1024x1024 RGBA, transparent where there is no paint, no baked shadow (prompts.md, 4). The road runs up the texture (v);
the zebra's stripes run along it, side by side across the road, and a solid stop line crosses the road below them. White paint, a
little scuffed:
    crosswalk_stripes           whole: clean stripes, sharp edges, a light wear
    crosswalk_stripes_worn      worn: faded, the paint gone in patches and along the wheel tracks
    crosswalk_stripes_damaged   damaged: worn, with soot from a blast and dark potholes on top of it (the potholes are the decal's own
                                dark paint over the asphalt)
Files: game/assets/textures/decals/<name>.png.

In Blender, after tools/blender is on sys.path:
    import families.city_crosswalk as ccw
    print(ccw.run_all())
"""

import os

import bpy
import numpy as np

import rts_common as rc

SIZE = 1024
LOOKS = {"crosswalk_stripes": "intact", "crosswalk_stripes_worn": "worn", "crosswalk_stripes_damaged": "damaged"}
OUT = os.path.join(rc.REPO, "game", "assets", "textures", "decals")
# Gallery keys (rts_common.LAYOUT): the asphalt square with each look on it.
GALLERY = {"crosswalk_stripes": "city_crosswalk", "crosswalk_stripes_worn": "city_crosswalk_worn",
           "crosswalk_stripes_damaged": "city_crosswalk_damaged"}

STRIPES = 6
STRIPE_V = (0.2, 0.92)  # where the stripes run, up the road
STOP_V = (0.07, 0.11)  # the stop line
MARGIN = 0.06  # paint stops short of the road's edges


def _noise(rng, cells, size=SIZE):
    """Smooth value noise in 0..1: random values on a coarse grid, blended up to the texture's size."""
    grid = rng.random((cells + 1, cells + 1))
    t = np.linspace(0.0, cells, size, endpoint=False)
    i = t.astype(int)
    f = t - i
    f = f * f * (3 - 2 * f)
    a = grid[np.ix_(i, i)]
    b = grid[np.ix_(i, i + 1)]
    c = grid[np.ix_(i + 1, i)]
    d = grid[np.ix_(i + 1, i + 1)]
    fu, fv = f[None, :], f[:, None]
    return (a * (1 - fu) + b * fu) * (1 - fv) + (c * (1 - fu) + d * fu) * fv


def _paint_mask():
    """1 where the stripes and the stop line are painted, 0 elsewhere, with soft edges a pixel wide."""
    v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE
    width = (1 - 2 * MARGIN) / (2 * STRIPES - 1)
    k = (u - MARGIN) / width
    stripe = (k >= 0) & (k < 2 * STRIPES - 1) & (np.floor(k) % 2 == 0) & (v >= STRIPE_V[0]) & (v <= STRIPE_V[1])
    stop = (u >= MARGIN) & (u <= 1 - MARGIN) & (v >= STOP_V[0]) & (v <= STOP_V[1])
    return (stripe | stop).astype(float)


def draw(look):
    """The RGBA pixels of a look, rows from the bottom as Blender stores them."""
    rng = np.random.default_rng({"intact": 7, "worn": 11, "damaged": 13}[look])
    paint = _paint_mask()
    fine = _noise(rng, 64)
    coarse = _noise(rng, 8)
    rgb = np.ones((SIZE, SIZE, 3)) * 0.93
    rgb *= (0.94 + 0.06 * fine)[..., None]  # the paint's grain
    if look == "intact":
        alpha = paint * np.clip(0.9 + 0.2 * fine, 0, 1)  # a light scuff, the paint whole
    else:
        v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE
        tracks = np.exp(-((u - 0.3) / 0.06) ** 2) + np.exp(-((u - 0.7) / 0.06) ** 2)  # where the wheels run
        wear = np.clip(0.55 + 0.7 * fine - 0.6 * (coarse > 0.62) - 0.45 * tracks, 0, 1)
        alpha = paint * wear
        rgb *= 0.92
    if look == "damaged":
        v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE
        # Soot: a dark blot round a blast near the stripes' right side, ragged at its edge.
        r = np.hypot(u - 0.68, v - 0.55)
        soot = np.clip(1.0 - r / 0.3 + 0.35 * (fine - 0.5), 0, 1) ** 1.5
        # Potholes: a few dark round holes with ragged rims.
        holes = np.zeros_like(u)
        for (cu, cv, rad) in ((0.62, 0.5, 0.06), (0.75, 0.62, 0.035), (0.3, 0.35, 0.04), (0.52, 0.8, 0.03)):
            holes = np.maximum(holes, np.clip((rad - np.hypot(u - cu, v - cv) + 0.012 * (fine - 0.5)) * 120, 0, 1))
        dark = np.maximum(soot * 0.85, holes)
        dark_col = np.where(holes[..., None] > 0.5, 0.05, 0.09)
        rgb = rgb * (1 - dark[..., None]) + dark_col * dark[..., None]
        alpha = np.maximum(alpha * (1 - holes), np.maximum(soot * 0.8, holes))
    rgba = np.concatenate([rgb, alpha[..., None]], axis=2)
    return rgba.astype(np.float32)


def save(name, rgba):
    path = os.path.join(OUT, name + ".png")
    os.makedirs(OUT, exist_ok=True)
    img = bpy.data.images.get(name) or bpy.data.images.new(name, SIZE, SIZE, alpha=True)
    img.colorspace_settings.name = 'sRGB'
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    img.pack()
    return img, path


def _gallery(name, img):
    """A square of asphalt with the decal on it, in its place in the gallery file."""
    key = GALLERY[name]
    for obj in [o for o in bpy.data.objects if o.name.split(".")[0] in (f"{key}_asphalt", f"{key}_decal")]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    asphalt = _plane(f"{key}_asphalt", 0.0, _asphalt_material())
    decal = _plane(f"{key}_decal", 0.002, _decal_material(name + "_mat", img))
    rc.arrange([asphalt, decal], key)


def _plane(name, z, mat):
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([(-0.5, -0.5, z), (0.5, -0.5, z), (0.5, 0.5, z), (-0.5, 0.5, z)], [], [(0, 1, 2, 3)])
    mesh.uv_layers.new(name="UVMap")
    for loop, uv in zip(mesh.uv_layers[0].data, ((0, 0), (1, 0), (1, 1), (0, 1))):
        loop.uv = uv
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.scene.collection.objects.link(obj)
    return obj


def _asphalt_material():
    mat = bpy.data.materials.get("city_crosswalk_asphalt_mat") or bpy.data.materials.new("city_crosswalk_asphalt_mat")
    mat.use_nodes = True
    bsdf = next(n for n in mat.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (0.06, 0.06, 0.065, 1.0)
    bsdf.inputs["Roughness"].default_value = 0.9
    return mat


def _decal_material(name, img):
    old = bpy.data.materials.get(name)
    if old is not None:
        bpy.data.materials.remove(old)
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    nt.links.new(tex.outputs['Alpha'], bsdf.inputs['Alpha'])
    bsdf.inputs["Roughness"].default_value = 0.8
    try:
        mat.surface_render_method = 'BLENDED'
    except (AttributeError, TypeError):
        mat.blend_method = 'BLEND'
    return mat


def run(name):
    img, path = save(name, draw(LOOKS[name]))
    _gallery(name, img)
    return f"{name}: {path}"


def run_all():
    return "\n".join(run(name) for name in LOOKS)
