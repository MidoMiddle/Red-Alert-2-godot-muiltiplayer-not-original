"""Beachgoers, element 10 of the beach set (docs/beach_elements/10_beachgoers): the civilians of the coast, and its lifeguard.

The units civ_beach_man, civ_beach_woman and civ_lifeguard (game/data/neutral/civilians.yaml, docs/map_elements/13_civilians): people of
nobody who stroll between the loungers, the bar and the shower and run from danger. Made as the infantry are for now (docs/art/
unit_models_plan.md): a static figure standing, no skeleton yet, at the size of the stand-in it replaces (UnitView.BuildInfantry, grown:
0.56 tall). Origin at the centre on the ground, the front to +Y in Blender (-Z in Godot). The figure: legs, hips, a torso narrowing to
the waist and widening to the shoulders, arms hanging a little out, a head with its hair; the clothes are the parts the faces are
painted with, and what is worn over them (a dress, a hat, glasses, a float). One GLB per look; the game picks one per unit:
    civ_beach_man        a white t-shirt, blue shorts, flip-flops
    civ_beach_man_b      no shirt, red board shorts, dark glasses, tanned
    civ_beach_woman      a yellow sundress, a straw sun hat, sandals
    civ_beach_woman_b    a blue swimsuit, an orange wrap round the hips, long hair
    civ_lifeguard        a red vest and red shorts, a red cap, a whistle on a cord, the red rescue float under his arm
No text, no logos, no team colour (nobody's). Node: "body". Budget 1500 triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.beach_people as people
    print(people.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "beach"
SIZE = 512

(SKIN, HAIR, TOP, BOTTOM, SHOE, ACCENT, DARK, WHITE) = (cc.part(i) for i in range(8))
PARTS = 8

# The look of each: skin, hair, top (None: bare), bottom, shoe, accent colours, and what is worn.
VARIANTS = {
    "civ_beach_man": dict(skin=(0.82, 0.62, 0.48), hair=(0.22, 0.15, 0.10), top=(0.94, 0.94, 0.92), bottom=(0.18, 0.36, 0.72),
                          shoe=(0.15, 0.15, 0.16), accent=(0.0, 0.0, 0.0), wear=()),
    "civ_beach_man_b": dict(skin=(0.64, 0.45, 0.32), hair=(0.12, 0.10, 0.08), top=None, bottom=(0.82, 0.16, 0.12),
                            shoe=(0.20, 0.20, 0.22), accent=(0.0, 0.0, 0.0), wear=("glasses",)),
    "civ_beach_woman": dict(skin=(0.86, 0.68, 0.56), hair=(0.55, 0.38, 0.20), top=(0.96, 0.80, 0.22), bottom=(0.96, 0.80, 0.22),
                            shoe=(0.60, 0.42, 0.26), accent=(0.86, 0.76, 0.52), wear=("dress", "hat")),
    "civ_beach_woman_b": dict(skin=(0.58, 0.40, 0.29), hair=(0.10, 0.08, 0.07), top=(0.16, 0.42, 0.78), bottom=(0.16, 0.42, 0.78),
                              shoe=(0.20, 0.20, 0.22), accent=(0.95, 0.52, 0.16), wear=("swimsuit", "wrap", "long_hair")),
    "civ_lifeguard": dict(skin=(0.70, 0.50, 0.36), hair=(0.20, 0.14, 0.10), top=(0.84, 0.12, 0.10), bottom=(0.84, 0.12, 0.10),
                          shoe=(0.15, 0.15, 0.16), accent=(0.86, 0.12, 0.10), wear=("vest", "cap", "float", "whistle")),
}

KNEE, HIP, WAIST, CHEST, SHOULDER, NECK = 0.15, 0.27, 0.315, 0.39, 0.435, 0.455
HEAD, HEAD_R = 0.5, 0.042


def ring(centre, rx, ry, sides=8):
    c = Vector(centre)
    return [c + Vector((math.cos(2 * math.pi * k / sides) * rx, math.sin(2 * math.pi * k / sides) * ry, 0.0)) for k in range(sides)]


def limb(p, points, radii, parts, sides=6):
    """A tapered limb through the points; each length painted with its part (parts has one fewer than points)."""
    for (a, b), (ra, rb), part in zip(zip(points, points[1:]), zip(radii, radii[1:]), parts):
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        u = d.cross(Vector((0.0, 1.0, 0.0)))
        if u.length < 1e-6:
            u = d.cross(Vector((1.0, 0.0, 0.0)))
        u = u.normalized()
        v = d.cross(u).normalized()
        p.loft([[c + (u * math.cos(2 * math.pi * k / sides) + v * math.sin(2 * math.pi * k / sides)) * r for k in range(sides)]
                for c, r in ((a, ra), (b, rb))], part)


def _figure(p, look):
    bare = look["top"] is None
    wear = look["wear"]
    swimsuit = "swimsuit" in wear
    top = SKIN if (bare or swimsuit) else TOP  # a swimsuit leaves the midriff and shoulders bare; its top is a band
    # Legs: thighs in the shorts (bare under a dress or a swimsuit), shins and feet.
    for side in (-1, 1):
        x = 0.022 * side
        thigh = SKIN if ("dress" in wear or swimsuit) else BOTTOM
        limb(p, [(x, 0.0, HIP), (x, 0.004, (HIP + KNEE) / 2), (x, 0.006, KNEE), (x, 0.0, 0.025)], [0.024, 0.02, 0.016, 0.012],
             [thigh, SKIN if "dress" not in wear and not swimsuit else SKIN, SKIN])
        p.box((x - 0.011, -0.008, 0.0), (x + 0.011, 0.034, 0.012), SHOE)
    # The hips and the torso: a loft of rings from the hips up to the neck.
    stations = [(HIP - 0.01, 0.044, 0.028, BOTTOM), (WAIST, 0.038, 0.025, SKIN if swimsuit else top),
                (CHEST, 0.046, 0.028, top), (SHOULDER, 0.054, 0.026, top)]
    for k, ((z0, rx0, ry0, part0), (z1, rx1, ry1, part1)) in enumerate(zip(stations, stations[1:])):
        # The hips in the shorts; above the waist, the top.
        p.loft([ring((0.0, 0.0, z0), rx0, ry0), ring((0.0, 0.0, z1), rx1, ry1)], part0 if k == 0 else part1)
    if swimsuit:  # the swimsuit's top over the chest, its bottom over the hips
        p.loft([ring((0.0, 0.0, CHEST - 0.03), 0.045, 0.028), ring((0.0, 0.0, CHEST + 0.012), 0.049, 0.029)], TOP)
    p.loft([ring((0.0, 0.0, SHOULDER), 0.03, 0.022), ring((0.0, 0.0, NECK + 0.01), 0.014, 0.014)], SKIN)  # the neck and shoulders' top
    # Arms, hanging a little out: the sleeve or the bare shoulder, the forearm, the hand.
    for side in (-1, 1):
        sleeve = TOP if (not bare and "dress" not in wear and not swimsuit and "vest" not in wear) else SKIN
        limb(p, [(0.058 * side, 0.0, SHOULDER - 0.006), (0.068 * side, 0.002, 0.355), (0.074 * side, 0.01, 0.285), (0.074 * side, 0.012, 0.255)],
             [0.016, 0.013, 0.011, 0.012], [sleeve, SKIN, SKIN])
    # The head and the hair.
    p.sphere((0.0, 0.0, HEAD), 'Z', HEAD_R, SKIN, sides=10, rings=6)
    p.sphere((0.0, -0.01, HEAD + 0.006), 'Z', HEAD_R * 1.06, HAIR, sides=10, rings=4)
    p.box((-0.004, HEAD_R - 0.002, HEAD - 0.004), (0.004, HEAD_R + 0.006, HEAD + 0.008), SKIN)  # the nose
    if "long_hair" in wear:
        p.box((-0.034, -0.042, HEAD - 0.06), (0.034, -0.014, HEAD + 0.02), HAIR)
    if "glasses" in wear:
        p.box((-0.03, HEAD_R - 0.012, HEAD + 0.004), (0.03, HEAD_R + 0.004, HEAD + 0.016), DARK)
    if "dress" in wear:  # a sundress: a flared skirt from the waist to the knees
        p.loft([ring((0.0, 0.0, WAIST), 0.04, 0.026), ring((0.0, 0.0, HIP), 0.05, 0.034), ring((0.0, 0.0, KNEE + 0.02), 0.066, 0.05)], BOTTOM)
    if "wrap" in wear:  # a wrap round the hips, down to the mid thigh
        p.loft([ring((0.0, 0.0, HIP + 0.01), 0.046, 0.03), ring((0.0, 0.0, (HIP + KNEE) / 2 + 0.01), 0.056, 0.04)], ACCENT)
    if "hat" in wear:  # a straw sun hat: a wide brim and a low crown
        p.cylinder((0.0, 0.0, HEAD + 0.03), 'Z', 0.085, 0.004, ACCENT, sides=12)
        p.cylinder((0.0, 0.0, HEAD + 0.05), 'Z', 0.04, 0.02, ACCENT, sides=10)
    if "cap" in wear:
        p.cylinder((0.0, -0.004, HEAD + 0.03), 'Z', 0.044, 0.016, ACCENT, sides=10)
        p.box((-0.03, 0.03, HEAD + 0.018), (0.03, 0.075, HEAD + 0.024), ACCENT)  # its peak, to the front
    if "vest" in wear:  # the vest's white piping down its front, no letters
        p.box((-0.004, 0.026, WAIST + 0.01), (0.004, 0.031, SHOULDER - 0.01), WHITE)
    if "whistle" in wear:
        p.bar((-0.02, 0.022, NECK - 0.005), (0.0, 0.03, CHEST + 0.005), 0.0025, WHITE)
        p.bar((0.02, 0.022, NECK - 0.005), (0.0, 0.03, CHEST + 0.005), 0.0025, WHITE)
        p.box((-0.006, 0.026, CHEST - 0.005), (0.006, 0.04, CHEST + 0.007), DARK)
    if "float" in wear:  # the rescue float: a long red bar under the right arm, its strap over the shoulder
        p.box((0.082, -0.022, 0.29), (0.104, 0.014, 0.42), ACCENT)
        p.bar((0.095, -0.005, 0.43), (-0.04, 0.0, SHOULDER + 0.004), 0.004, DARK)


def _material(name, look):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.03)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(60.0, 2.0)
    shade = lambda c, k=0.12: g.mix(g.math('MULTIPLY', grain, k), lin(c), lin(tuple(v * 0.8 for v in c)))
    top = look["top"] or look["skin"]
    albedo = cc.paint(g, mask, (shade(look["skin"], 0.08), shade(look["hair"]), shade(top), shade(look["bottom"]), lin(look["shoe"]),
                               shade(look["accent"] if any(look["accent"]) else (0.5, 0.5, 0.5)), lin((0.04, 0.04, 0.05)),
                               lin((0.94, 0.94, 0.92))))
    rough = cc.weighted(g, mask, (0.6, 0.7, 0.85, 0.85, 0.7, 0.75, 0.2, 0.8))
    g.finish(albedo, g.bump(grain, 0.03, 0.002), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    look = VARIANTS[variant_id]
    ic.clear(variant_id, ("body",))
    body = Parts()
    _figure(body, look)
    return cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat", look))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
