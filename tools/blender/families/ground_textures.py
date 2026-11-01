"""Textures of the ground drawn with numpy, as the road textures (families/road_textures.py), and laid on squares in the gallery file.
    sand_shore   clean light sand by the water (the owner, 2026-10-30: the photographed sand was too dirty): pale warm beige, a fine
                 even grain, faint wind ripples, now and then a darker grain or a light speck of shell; no pebbles, no dark patches
    shoal_sand   the sandy bottom of a shoal (docs/naval_elements/01_shoal), seen through the shallow water the ground shader lays over
                 it: wet sand a little darker and warmer than the shore's, ripple marks of the current in clear crests and troughs
                 running one way, fine dark detritus settled in the troughs, light specks of shell on the crests, faint green patches of
                 algae; no water colour in it (the shader's)
    quay_concrete the concrete of a quay wall (docs/naval_elements/09_quay_wall), laid on the cliff's walls from the side as the
                 rock is: light grey cast panels, two by two to a tile, thin dark joints, a faint lift line across each, the holes of
                 the formwork's ties two by two to a panel (a few filled) with rusty streaks running down from them, weathered patches,
                 small light chips; no algae (it would come back in a band on
                 every tile up the wall)
    beach_wet_sand the wet sand of a beach by the water (docs/beach_elements/01_sand_dunes): the shore's sand darkened and packed
                 by the waves, grey-brown and cool; the swash lines of spent waves across it — thin wavy lines of fine dark grains and
                 a pale fringe of dried foam — the rills of the backwash running down between them, small pits of air holes, a few
                 light shell fragments; smooth, no wind ripples (the waves flatten them)
Files: game/assets/textures/terrain/<name>.png, 1024x1024 RGB, seamless.

In Blender, after tools/blender is on sys.path:
    import families.ground_textures as gt
    print(gt.run_all())
"""

import numpy as np

from families import road_textures as rt

NAMES = ("sand_shore", "shoal_sand", "quay_concrete", "beach_wet_sand")
SEEDS = {"sand_shore": 61, "shoal_sand": 67, "quay_concrete": 71, "beach_wet_sand": 73}


def draw(name):
    rng = np.random.default_rng(SEEDS[name])
    v, u = np.mgrid[0:rt.SIZE, 0:rt.SIZE] / rt.SIZE
    if name == "shoal_sand":
        return _shoal(rng, u, v)
    if name == "quay_concrete":
        return _quay(rng, u, v)
    if name == "beach_wet_sand":
        return _wet(rng, u, v)
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


def _wet(rng, u, v):
    """The swash lines run along u (along the water), each wavy in its own way and broken; the backwash rills run across them."""
    def wavy(periods, phases, amp):
        # A wave of whole periods along u, so it wraps.
        return sum(a * np.sin(2 * np.pi * (k * u + ph)) for k, ph, a in zip(periods, phases, amp))

    swash = np.zeros_like(u)
    fringe = np.zeros_like(u)
    for at in (0.08, 0.31, 0.47, 0.74):
        bend = wavy((1, 2, 3), rng.random(3), (0.035, 0.03, 0.012)) + 0.05 * (rt._fbm(rng, 6, 3) - 0.5)
        d = np.mod(v - at - bend + 0.5, 1.0) - 0.5  # signed distance across the line, wrapped
        keep = np.clip((rt._fbm(rng, 8, 3) - 0.38) * 4.0, 0.0, 1.0)  # the line fades out in places
        width = 0.0018 + 0.0018 * rt._fbm(rng, 16, 2)
        swash = np.maximum(swash, keep * np.clip(1.0 - np.abs(d) / width, 0.0, 1.0))
        fringe = np.maximum(fringe, keep * np.clip(1.0 - np.abs(d + 0.006) / 0.006, 0.0, 1.0) * (d < -0.001))
    # Rills of the backwash: faint fine grooves down the slope, a little slanted, in patches.
    groove = 0.5 + 0.5 * np.sin(2 * np.pi * (60 * u + 4 * v) + 3.0 * rt._fbm(rng, 6, 2))
    rills = np.clip((rt._fbm(rng, 5, 3) - 0.5) * 3.0, 0.0, 1.0) * groove ** 6
    wet = rt._fbm(rng, 3, 3) - 0.5  # wetter and drier stretches
    tone = 0.07 * wet + 0.035 * (rt._fbm(rng, 32, 3) - 0.5) + 0.05 * (rt._noise(rng, 512) - 0.5)
    tone = tone - 0.06 * rills - 0.22 * swash * (0.7 + 0.3 * rt._noise(rng, 256)) + 0.07 * fringe
    tone = tone - 0.22 * rt._grit(rng, 0.003) + 0.15 * rt._grit(rng, 0.003, 2)  # air holes, shell fragments
    base = np.array([0.62, 0.56, 0.47])
    return np.clip(base[None, None, :] * (1.0 + tone[..., None]), 0, 1)


def _quay(rng, u, v):
    """Cast panels, two by two; v runs up the wall (the image's rows from the bottom)."""
    def near(t, count, width):
        f = np.mod(t * count, 1.0)
        return np.minimum(f, 1.0 - f) < width * count  # within width of a line, count lines across

    joints = near(u, 2, 0.0025) | near(v, 2, 0.0025)
    tone = 0.05 * (rt._fbm(rng, 4, 4) - 0.5) + 0.05 * (rt._fbm(rng, 32, 3) - 0.5) + 0.06 * (rt._noise(rng, 512) - 0.5)
    tone = tone - 0.1 * np.clip((rt._fbm(rng, 6, 3) - 0.6) * 4.0, 0.0, 1.0)  # weathered patches
    # The panels' edges a little darker, the joints dark.
    fu, fv = np.mod(u * 2, 1.0), np.mod(v * 2, 1.0)
    edge = np.minimum(np.minimum(fu, 1 - fu), np.minimum(fv, 1 - fv))
    tone = tone - 0.08 * np.clip(1.0 - edge / 0.03, 0.0, 1.0)
    # The lift line of each pour across the middle of a panel, faint.
    tone = tone - 0.05 * (near(v + 0.25, 2, 0.0015) & ~joints)
    # The ties' holes, two by two to a panel, a few filled; the rust streaks down from them.
    cell = (np.floor(u * 4) + 4 * np.floor(v * 4)).astype(int) % 16
    kept = (rng.random(16) > 0.3)[cell]
    hu, hv = np.mod(u * 4, 1.0) - 0.5, np.mod(v * 4, 1.0) - 0.5
    hole = kept & ((hu ** 2 + hv ** 2) < 0.025 ** 2)
    width = 0.03 + 0.02 * rt._noise(rng, 64)
    below = kept & (np.abs(hu) < width) & (hv < -0.02) & (hv > -0.45)
    streak = below * np.clip(1.0 + (hv + 0.02) / 0.43, 0.0, 1.0) * (0.5 + 0.5 * rt._noise(rng, 16))
    tone = tone + 0.06 * rt._grit(rng, 0.004, 2) - 0.03 * rt._grit(rng, 0.02)
    base = np.array([0.66, 0.66, 0.63])
    rgb = base[None, None, :] * (1.0 + tone[..., None])
    rust = np.array([0.48, 0.32, 0.20])
    rgb = rgb * (1 - 0.5 * streak[..., None]) + rust[None, None, :] * 0.5 * streak[..., None]
    rgb = np.where(joints[..., None], rgb * 0.45, rgb)
    rgb = np.where(hole[..., None], rgb * 0.3, rgb)
    return np.clip(rgb, 0, 1)


def run(name):
    img, path = rt.save(name, draw(name))
    rt._gallery(name, img)
    return f"{name}: {path}"


def run_all():
    return "\n".join(run(name) for name in NAMES)
