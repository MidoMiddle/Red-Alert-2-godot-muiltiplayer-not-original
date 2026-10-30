"""Textures of the ground drawn with numpy, as the road textures (families/road_textures.py), and laid on squares in the gallery file.
    sand_shore   clean light sand by the water (the owner, 2026-10-30: the photographed sand was too dirty): pale warm beige, a fine
                 even grain, faint wind ripples, now and then a darker grain or a light speck of shell; no pebbles, no dark patches
Files: game/assets/textures/terrain/<name>.png, 1024x1024 RGB, seamless.

In Blender, after tools/blender is on sys.path:
    import families.ground_textures as gt
    print(gt.run_all())
"""

import numpy as np

from families import road_textures as rt

NAMES = ("sand_shore",)


def draw(name):
    rng = np.random.default_rng({"sand_shore": 61}[name])
    v, u = np.mgrid[0:rt.SIZE, 0:rt.SIZE] / rt.SIZE
    # Ripples across the texture, bent by a slow noise; whole numbers of them across, so they wrap.
    bend = rt._fbm(rng, 3, 3)
    ripple = 0.5 + 0.5 * np.sin(2 * np.pi * (24 * u + 8 * v + 3 * bend))
    tone = 0.025 * (ripple - 0.5) + 0.06 * (rt._fbm(rng, 4, 4) - 0.5) + 0.05 * (rt._fbm(rng, 32, 3) - 0.5) + 0.08 * (rt._noise(rng, 512) - 0.5)
    tone = tone - 0.14 * rt._grit(rng, 0.02) + 0.08 * rt._grit(rng, 0.01)
    base = np.array([0.88, 0.80, 0.64])
    return np.clip(base[None, None, :] * (1.0 + tone[..., None]), 0, 1)


def run(name):
    img, path = rt.save(name, draw(name))
    rt._gallery(name, img)
    return f"{name}: {path}"


def run_all():
    return "\n".join(run(name) for name in NAMES)
