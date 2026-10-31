"""Textures of the ground drawn with numpy, as the road textures (families/road_textures.py), and laid on squares in the gallery file.
    sand_shore   clean light sand by the water (the owner, 2026-10-30: the photographed sand was too dirty): pale warm beige, a fine
                 even grain, faint wind ripples, now and then a darker grain or a light speck of shell; no pebbles, no dark patches
    shoal_sand   the sandy bottom of a shoal (docs/naval_elements/01_shoal), seen through the shallow water the ground shader lays over
                 it: wet sand a little darker and warmer than the shore's, ripple marks of the current in clear crests and troughs
                 running one way, fine dark detritus settled in the troughs, light specks of shell on the crests, faint green patches of
                 algae; no water colour in it (the shader's)
Files: game/assets/textures/terrain/<name>.png, 1024x1024 RGB, seamless.

In Blender, after tools/blender is on sys.path:
    import families.ground_textures as gt
    print(gt.run_all())
"""

import numpy as np

from families import road_textures as rt

NAMES = ("sand_shore", "shoal_sand")
SEEDS = {"sand_shore": 61, "shoal_sand": 67}


def draw(name):
    rng = np.random.default_rng(SEEDS[name])
    v, u = np.mgrid[0:rt.SIZE, 0:rt.SIZE] / rt.SIZE
    if name == "shoal_sand":
        return _shoal(rng, u, v)
    # Ripples across the texture, bent by a slow noise; whole numbers of them across, so they wrap.
    bend = rt._fbm(rng, 3, 3)
    ripple = 0.5 + 0.5 * np.sin(2 * np.pi * (24 * u + 8 * v + 3 * bend))
    tone = 0.025 * (ripple - 0.5) + 0.06 * (rt._fbm(rng, 4, 4) - 0.5) + 0.05 * (rt._fbm(rng, 32, 3) - 0.5) + 0.08 * (rt._noise(rng, 512) - 0.5)
    tone = tone - 0.14 * rt._grit(rng, 0.02) + 0.08 * rt._grit(rng, 0.01)
    base = np.array([0.88, 0.80, 0.64])
    return np.clip(base[None, None, :] * (1.0 + tone[..., None]), 0, 1)


def _shoal(rng, u, v):
    # Ripple marks: 12 crests across, bent by a slow noise; a crest sharp on its lee side, as the current leaves them.
    bend = rt._fbm(rng, 3, 3)
    phase = np.mod(12 * u + 2 * v + 1.6 * bend, 1.0)
    crest = np.where(phase < 0.7, phase / 0.7, (1.0 - phase) / 0.3)  # a slow rise, a short steep fall
    crest = crest ** 1.5
    trough = np.clip(1.0 - crest * 2.5, 0.0, 1.0)
    tone = 0.16 * (crest - 0.4) + 0.06 * (rt._fbm(rng, 4, 4) - 0.5) + 0.05 * (rt._fbm(rng, 32, 3) - 0.5) + 0.07 * (rt._noise(rng, 512) - 0.5)
    tone = tone - 0.22 * rt._grit(rng, 0.05) * trough  # detritus settled in the troughs
    tone = tone + 0.12 * rt._grit(rng, 0.008, 2) * (crest > 0.6)  # shell on the crests
    base = np.array([0.80, 0.71, 0.53])
    rgb = base[None, None, :] * (1.0 + tone[..., None])
    algae = np.clip((rt._fbm(rng, 6, 3) - 0.64) * 5.0, 0.0, 1.0)[..., None] * 0.2
    return np.clip(rgb * (1 - algae) + np.array([0.46, 0.52, 0.32])[None, None, :] * algae, 0, 1)


def run(name):
    img, path = rt.save(name, draw(name))
    rt._gallery(name, img)
    return f"{name}: {path}"


def run_all():
    return "\n".join(run(name) for name in NAMES)
