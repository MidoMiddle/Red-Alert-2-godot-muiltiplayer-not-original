"""Textures of the new kinds of road (docs/map_elements/04_roads): the highway, the highway under snow, the town street of old asphalt and
the street of cobbles (prompts.md, 1 and 3). Drawn here with numpy, no generator, and laid on squares in the gallery file.

Each is 1024x1024 RGB, seamless: every noise is periodic on the texture, so the tile repeats without a seam. No wheel tracks or
markings: a road runs either way across the cells and the texture is not turned with it (prompts.md, 4; the markings are decals,
later). The three asphalts must not merge (appearance.md, 5): the highway the darkest and smoothest, the town street lighter and
patched.
    road_highway         dark, even asphalt, fine light aggregate, faint darker oil marks
    road_highway_snow    the same under a thin, broken dusting of snow, grey slush where it thinned
    road_street          old lighter asphalt, square patches of newer darker asphalt, fine cracks
    road_street_cobble   grey granite setts in staggered rows, each its own tone, dark joints with dirt
Files: game/assets/textures/terrain/<name>.png.

In Blender, after tools/blender is on sys.path:
    import families.road_textures as rt
    print(rt.run_all())
"""

import os

import bpy
import numpy as np

import rts_common as rc

SIZE = 1024
NAMES = ("road_highway", "road_highway_snow", "road_street", "road_street_cobble")
OUT = os.path.join(rc.REPO, "game", "assets", "textures", "terrain")
SETTS = 32  # cobbles along each side of the texture


def _noise(rng, cells):
    """Smooth periodic value noise in 0..1: random values on a cells x cells grid that wraps round, blended up to the texture."""
    grid = rng.random((cells, cells))
    t = np.linspace(0.0, cells, SIZE, endpoint=False)
    i = t.astype(int)
    f = t - i
    f = f * f * (3 - 2 * f)
    j = (i + 1) % cells
    a, b, c, d = grid[np.ix_(i, i)], grid[np.ix_(i, j)], grid[np.ix_(j, i)], grid[np.ix_(j, j)]
    fu, fv = f[None, :], f[:, None]
    return (a * (1 - fu) + b * fu) * (1 - fv) + (c * (1 - fu) + d * fu) * fv


def _fbm(rng, cells, octaves):
    """Several octaves of the noise, each twice as fine and half as strong, in 0..1."""
    out, weight, total = np.zeros((SIZE, SIZE)), 1.0, 0.0
    for k in range(octaves):
        out += weight * _noise(rng, cells * 2 ** k)
        total += weight
        weight *= 0.5
    return out / total


def _grit(rng, share, size=1):
    """Specks: 1 on a share of the pixels (in clumps of size), 0 elsewhere; periodic, as the texture wraps."""
    specks = (rng.random((SIZE // size, SIZE // size)) < share).astype(float)
    return np.kron(specks, np.ones((size, size)))


def _asphalt(rng, base, spread):
    """Asphalt: a grey of base, varied by spread, with light and dark aggregate."""
    tone = base + spread * (_fbm(rng, 4, 4) - 0.5)
    tone = tone + 0.10 * _grit(rng, 0.05) - 0.06 * _grit(rng, 0.05) + 0.05 * (_noise(rng, 256) - 0.5)
    return tone


def draw(name):
    """The RGB pixels of a texture, 0..1."""
    rng = np.random.default_rng({"road_highway": 21, "road_highway_snow": 21, "road_street": 33, "road_street_cobble": 47}[name])
    if name in ("road_highway", "road_highway_snow"):
        tone = _asphalt(rng, 0.20, 0.05)
        oil = np.clip((_fbm(rng, 6, 3) - 0.66) * 6, 0, 1)
        tone = tone - 0.05 * oil
        rgb = np.stack([tone, tone, tone * 1.03], axis=2)
        if name == "road_highway_snow":
            cover = np.clip((_fbm(rng, 5, 5) - 0.40) * 3, 0, 1) * 0.85  # where the snow lies, never quite all over; thin in the rest
            slush = np.clip(1 - np.abs(_fbm(rng, 5, 5) - 0.5) * 6, 0, 1) * (1 - cover)
            snow = 0.86 + 0.06 * (_noise(rng, 128) - 0.5)
            snow_rgb = np.stack([snow * 0.96, snow * 0.98, snow], axis=2)
            slush_rgb = np.stack([np.full_like(tone, 0.42), np.full_like(tone, 0.44), np.full_like(tone, 0.47)], axis=2)
            rgb = rgb * (1 - slush[..., None] * 0.7) + slush_rgb * slush[..., None] * 0.7
            rgb = rgb * (1 - cover[..., None]) + snow_rgb * cover[..., None]
    elif name == "road_street":
        tone = _asphalt(rng, 0.38, 0.06)
        # Square patches of newer, darker asphalt, on the grid of the texture so they wrap.
        patches = np.zeros((SIZE, SIZE))
        v, u = np.mgrid[0:SIZE, 0:SIZE]
        for _ in range(7):
            x0, y0 = rng.integers(0, SIZE, 2)
            w, h = rng.integers(50, 140, 2)
            inside = (((u - x0) % SIZE) < w) & (((v - y0) % SIZE) < h)
            patches = np.maximum(patches, inside.astype(float))
        tone = tone * (1 - patches) + (_asphalt(rng, 0.31, 0.03)) * patches
        cracks = np.clip(1 - np.abs(_fbm(rng, 8, 3) - 0.5) * 90, 0, 1) * (1 - patches)
        tone = tone - 0.07 * cracks
        rgb = np.stack([tone * 1.01, tone, tone * 0.98], axis=2)
    else:
        rgb = _cobbles(rng)
    return np.clip(rgb, 0, 1)


def _cobbles(rng):
    """Grey granite setts in staggered rows, each its own tone, rounded at the edges; dark joints with a little dirt."""
    v, u = np.mgrid[0:SIZE, 0:SIZE] / SIZE * SETTS
    row = np.floor(v).astype(int)
    shifted = u + 0.5 * (row % 2)
    col = np.floor(shifted).astype(int) % SETTS
    fu, fv = shifted - np.floor(shifted), v - np.floor(v)
    edge = np.minimum(np.minimum(fu, 1 - fu), np.minimum(fv, 1 - fv))  # 0 at a joint, 0.5 in the middle of a sett
    tones = 0.42 + 0.14 * rng.random((SETTS, SETTS))
    tint = rng.random((SETTS, SETTS)) * 0.04
    stone = tones[row % SETTS, col]
    stone = stone + 0.05 * (_noise(rng, 128) - 0.5) + 0.06 * _grit(rng, 0.04) - 0.04 * _grit(rng, 0.04)
    stone = stone * (0.72 + 0.28 * np.clip(edge / 0.16, 0, 1))  # rounded: darker towards the joint
    joint = np.clip(1 - edge / 0.05, 0, 1)
    dirt = 0.16 + 0.06 * _noise(rng, 64)
    tone = stone * (1 - joint) + dirt * joint
    warm = tint[row % SETTS, col] * (1 - joint)
    return np.stack([tone + warm, tone + warm * 0.5, tone], axis=2)


def save(name, rgb):
    path = os.path.join(OUT, name + ".png")
    img = bpy.data.images.get(name) or bpy.data.images.new(name, SIZE, SIZE, alpha=False)
    img.colorspace_settings.name = 'sRGB'
    rgba = np.concatenate([rgb, np.ones((SIZE, SIZE, 1))], axis=2).astype(np.float32)
    img.pixels.foreach_set(rgba.ravel())
    img.filepath_raw = path
    img.file_format = 'PNG'
    img.save()
    img.pack()
    return img, path


def _gallery(name, img):
    """A square of two cells by two in the gallery, the texture twice along each side, so a seam would show."""
    for obj in [o for o in bpy.data.objects if o.name.split(".")[0] == f"{name}_square"]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)
    mesh = bpy.data.meshes.new(f"{name}_square")
    mesh.from_pydata([(-1, -1, 0), (1, -1, 0), (1, 1, 0), (-1, 1, 0)], [], [(0, 1, 2, 3)])
    mesh.uv_layers.new(name="UVMap")
    for loop, uv in zip(mesh.uv_layers[0].data, ((0, 0), (2, 0), (2, 2), (0, 2))):
        loop.uv = uv
    mat = bpy.data.materials.get(name + "_mat") or bpy.data.materials.new(name + "_mat")
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
    tex = next((n for n in nt.nodes if n.type == "TEX_IMAGE"), None) or nt.nodes.new('ShaderNodeTexImage')
    tex.image = img
    nt.links.new(tex.outputs['Color'], bsdf.inputs['Base Color'])
    bsdf.inputs["Roughness"].default_value = 0.85
    mesh.materials.append(mat)
    obj = bpy.data.objects.new(f"{name}_square", mesh)
    bpy.context.scene.collection.objects.link(obj)
    rc.arrange([obj], name)


def run(name):
    img, path = save(name, draw(name))
    _gallery(name, img)
    return f"{name}: {path}"


def run_all():
    return "\n".join(run(name) for name in NAMES)
