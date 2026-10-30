"""City traffic light: element 17 of the city set (docs/city_elements/17_traffic_light) and its states (appearance.md, 4). City
climate: dry daylight, no snow. A map object (traffic_light, game/data/map/objects.yaml): no soldiers, no team zone.

Origin at the centre of the cell on the ground, the lamps to -Y. Made at the size the game draws it, as the road signs (the game does
not squash map objects): the housing's top at 0.94, a little over the road signs, not the prompt's 2. The parts:
    post      a grey steel pole on a round foot, a bracket at the top
    housing   a black housing with a hood over each lamp and a black back plate round it
    lamps     red on top, yellow in the middle, green at the bottom, each a node of its own and unlit: the client lights them in
              turn (prompts.md, 4)
No emblems, no text, no symbols.
Nodes: "post", "housing", "lamp_red", "lamp_yellow", "lamp_green" in every state. One GLB per state:
    city_traffic_light              whole
    city_traffic_light_damaged      damaged: the red lamp smashed (a dark hole), the housing knocked askew on its bracket; the yellow
                                    one is left to blink (appearance.md, 4)
    city_traffic_light_ruins        destroyed: the pole bent over near its foot, the housing down on the ground, all its lamps dark
The game draws a map object's main file only (game/scripts/Terrain/MapObjects.cs): the damaged and the destroyed one wait for the
client to show them.
Budget 1500 triangles, texture 1024 (docs/city_elements/17_traffic_light/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_traffic_light as ctl
    print(ctl.run("city_traffic_light"))   # or ctl.run_all()
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_traffic_light": "intact",
    "city_traffic_light_damaged": "damaged",
    "city_traffic_light_ruins": "ruins",
}

STEEL, BLACK, RED, YELLOW, GREEN, DARK = (cc.part(i) for i in range(6))
PARTS = 6

POLE_TOP = 0.66
HOUSING = (0.055, 0.04, 0.66, 0.94)  # half width, half depth, bottom, top
LAMP_R = 0.032
LAMPS = (("lamp_red", RED, 0.893), ("lamp_yellow", YELLOW, 0.8), ("lamp_green", GREEN, 0.707))


def _post(p):
    p.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 0.07), (0.03, 0.06), (0.05, 0.03), (0.06, 0.02), (POLE_TOP, 0.02), (POLE_TOP + 0.01, 0.005)],
             STEEL, sides=12)


def _housing(p):
    """The black housing, its back plate and a hood over each lamp."""
    hw, hd, z0, z1 = HOUSING
    p.box((-hw, -hd, z0), (hw, hd, z1), BLACK)
    p.box((-hw - 0.02, hd - 0.004, z0 - 0.01), (hw + 0.02, hd + 0.006, z1 + 0.01), BLACK)  # the back plate
    p.box((-0.015, -0.015, POLE_TOP - 0.03), (0.015, 0.015, z0), STEEL)  # the bracket on the pole
    for _, _, z in LAMPS:
        p.box((-0.042, -hd - 0.045, z + LAMP_R - 0.002), (0.042, -hd, z + LAMP_R + 0.006), BLACK)  # the hood's top
        for x in (-0.042, 0.036):
            p.box((x, -hd - 0.04, z - 0.005), (x + 0.006, -hd, z + LAMP_R + 0.006), BLACK)  # its sides


def _lamp(p, painted, z):
    p.cylinder((0.0, -HOUSING[1] - 0.004, z), 'Y', LAMP_R, 0.004, painted, sides=14)


def _transform(parts, m, pivot, shift=Vector(), above=None):
    for p in parts:
        for v in p.bm.verts:
            if above is None or v.co.z > above:
                v.co = m @ (v.co - pivot) + pivot + shift


def _material(name, variant):
    """Grey steel, black housing, the lamps unlit: dull red, yellow and green glass; dark where a lamp is smashed."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(40.0, 4.0)
    steel = g.mix(grain, lin((0.50, 0.51, 0.53)), lin((0.58, 0.59, 0.61)))
    if variant != "intact":
        steel = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(8.0, 4.0), 6.0, -3.4)), steel, lin((0.36, 0.25, 0.17)))
    black = g.mix(grain, lin((0.07, 0.07, 0.08)), lin((0.11, 0.11, 0.12)))
    red = lin((0.42, 0.08, 0.06))
    yellow = lin((0.46, 0.34, 0.07))
    green = lin((0.07, 0.34, 0.15))
    dark = lin((0.03, 0.03, 0.03))
    albedo = cc.paint(g, mask, (steel, black, red, yellow, green, dark))
    rough = cc.weighted(g, mask, (0.5, 0.6, 0.15, 0.15, 0.15, 0.9))
    metallic = cc.weighted(g, mask, (0.3, 0.1, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.noise(40.0, 3.0), 0.01, 0.002)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    names = ["post", "housing"] + [n for n, _, _ in LAMPS]
    cc.clear(variant_id, names)
    variant = VARIANTS[variant_id]
    post, housing = Parts(), Parts()
    _post(post)
    _housing(housing)
    lamps = []
    for name, painted, z in LAMPS:
        lamp = Parts()
        smashed = variant == "ruins" or variant == "damaged" and name == "lamp_red"
        _lamp(lamp, DARK if smashed else painted, z)
        lamps.append((lamp, name))
    head = [housing] + [p for p, _ in lamps]
    if variant == "damaged":
        _transform(head, Matrix.Rotation(math.radians(20), 3, 'Y') @ Matrix.Rotation(math.radians(12), 3, 'Z'), Vector((0.0, 0.0, HOUSING[2])))
    elif variant == "ruins":
        # The pole bent over near its foot; the housing torn off and lying on the ground on its back, lamps up.
        _transform([post], Matrix.Rotation(math.radians(70), 3, 'X'), Vector((0.0, 0.0, 0.08)), above=0.08)
        _transform(head, Matrix.Rotation(math.radians(-90), 3, 'X'), Vector((0.0, 0.0, HOUSING[2])),
                   Vector((0.25, -0.3, -HOUSING[2] + HOUSING[1] + 0.01)))
    parts = [(post, "post"), (housing, "housing")] + lamps
    return cc.finish(variant_id, parts, 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes post, housing, lamp_red, lamp_yellow and lamp_green."""
    meshes, mat, graph = build(variant_id)
    return cc.run(variant_id, meshes, mat, graph, SIZE)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
