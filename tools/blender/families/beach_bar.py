"""Beach bar, element 08 of the beach set (docs/beach_elements/08_beach_bar): an open bar on the sand, a counter under a striped awning.

The map building neutral_beach_bar (game/data/neutral/civilian.yaml): 3x2, 600 hp, no soldiers go in. Footprint 3x2 cells, origin at the
centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the buildings of the map (the game squashes it
upright, game/data/visuals/models.yaml), as the city's cafe: the awning's ridge at 1.75, its eaves at 1.25. The parts:
    base     a deck of planks on the sand over most of the footprint
    counter  the bar: a long counter of boards painted blue with a light wooden top, its stools before it; behind it a low back wall of
             shelves with bottles and glasses (dots of colour, no labels)
    body     six dark wooden posts holding the awning, a bright sign board without text on the front of the ridge, three round tables
             with stools out in front on the deck
    roof     the awning: a gable of striped red and white cloth along x, over the posts, a scalloped valance along its eaves
Nodes: "base", "counter", "body", "roof"; the abandoned one also fire_1..3. One GLB per state:
    beach_bar            whole
    beach_bar_damaged    damaged: the awning torn, a hole in it and a flap hanging, the counter splintered at one end, a table
                         knocked over, stools lying about, soot
    beach_bar_abandoned  burning: the awning burnt through to its frame at one end, the rest charred, a post leaning, the sign down, the
                         tables overturned, fire_1..3
    beach_bar_ruins      burnt down: charred stumps of the posts, the counter burnt low, the awning's frame fallen on the deck, one
                         table left on its side, the sign on the sand
Budget 8000 triangles, texture 2048.

In Blender, after tools/blender is on sys.path:
    import families.beach_bar as bar
    print(bar.run_all())
"""

import math

import bmesh
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "beach"
SIZE = 2048
VARIANTS = {
    "beach_bar": "intact",
    "beach_bar_damaged": "damaged",
    "beach_bar_abandoned": "abandoned",
    "beach_bar_ruins": "ruins",
}

DECK, BOARDS, TOP, POST, CLOTH, SIGN, BOTTLES, METAL, DARK = (cc.part(i) for i in range(9))
PARTS = 9

DECK_H = 0.06
EAVE, RIDGE = 1.25, 1.75
AX, AY = 1.5, 1.0  # the awning's half length (x) and half depth (y), its overhang included
POSTS = [(x, y) for x in (-1.3, 0.0, 1.3) for y in (-0.8, 0.8)]
COUNTER = (-1.0, 1.0, 0.18, 0.46)  # x0, x1, y0, y1
COUNTER_H = 0.52
FIRE_POINTS = ((-0.9, 0.0, 1.5), (0.4, 0.35, 0.7), (1.0, -0.4, 1.3))


def _deck(p):
    p.box((-1.42, -0.95, 0.0), (1.42, 0.95, DECK_H), DECK)


def _counter(p, variant):
    x0, x1, y0, y1 = COUNTER
    if variant == "ruins":
        p.box((x0, y0, DECK_H), (x0 + 0.9, y1, DECK_H + 0.18), BOARDS)  # burnt low
        p.box((x0 + 1.1, y0, DECK_H), (x1, y1, DECK_H + 0.1), BOARDS)
        return
    end = x1 - 0.35 if variant in ("damaged", "abandoned") else x1  # splintered off at the right end
    p.box((x0, y0, DECK_H), (end, y1, COUNTER_H), BOARDS)
    p.box((x0 - 0.04, y0 - 0.06, COUNTER_H), (end + 0.04, y1 + 0.02, COUNTER_H + 0.05), TOP)
    if end < x1:
        for k in range(4):  # the splinters of its broken end
            cc.block(p, (end + 0.1 + 0.07 * k, y0 - 0.1 - 0.05 * k, DECK_H + 0.02), (0.22, 0.04, 0.03), (0.0, 0.2, 0.6 * k), BOARDS)
    # The back wall of shelves, bottles on two shelves.
    p.box((x0, 0.78, DECK_H), (x1, 0.86, 1.05), BOARDS)
    for z in (0.62, 0.86):
        p.box((x0 + 0.05, 0.66, z), (x1 - 0.05, 0.78, z + 0.025), TOP)
        if variant != "abandoned":
            for k in range(14):
                x = x0 + 0.12 + 0.13 * k
                p.cylinder((x, 0.72, z + 0.07), 'Z', 0.022, 0.045 + 0.01 * (k % 3), BOTTLES, sides=5)
    # The stools before the counter.
    for k, x in enumerate((-0.75, -0.25, 0.25, 0.75)):
        if variant == "damaged" and k == 3:
            _stool(p, (0.9, -0.05), lying=True)
            continue
        _stool(p, (x, y0 - 0.2))


def _stool(p, at, lying=False):
    if lying:
        before = set(p.bm.verts)
        _stool(p, (0.0, 0.0))
        m = Matrix.Translation((at[0], at[1], 0.1)) @ Matrix.Rotation(1.4, 4, 'X') @ Matrix.Translation((0.0, 0.0, -DECK_H - 0.1))
        for v in p.bm.verts:
            if v not in before:
                v.co = m @ v.co
        return
    x, y = at
    p.cylinder((x, y, DECK_H + 0.17), 'Z', 0.012, 0.17, METAL, sides=5)
    p.cylinder((x, y, DECK_H + 0.355), 'Z', 0.07, 0.02, TOP, sides=8)


def _table(p, at, fallen=False):
    before = set(p.bm.verts)
    p.cylinder((0.0, 0.0, DECK_H + 0.2), 'Z', 0.02, 0.2, METAL, sides=5)
    p.cylinder((0.0, 0.0, DECK_H + 0.41), 'Z', 0.17, 0.015, TOP, sides=10)
    m = Matrix.Translation((at[0], at[1], 0.0))
    if fallen:
        m = Matrix.Translation((at[0], at[1], 0.18)) @ Matrix.Rotation(1.5, 4, 'Y') @ Matrix.Translation((0.0, 0.0, -DECK_H - 0.2))
    for v in p.bm.verts:
        if v not in before:
            v.co = m @ v.co
    if not fallen:
        for a in (0.4, 2.6):
            _stool(p, (at[0] + math.cos(a) * 0.28, at[1] + math.sin(a) * 0.28))


def _posts(p, variant):
    for k, (x, y) in enumerate(POSTS):
        top = EAVE + (RIDGE - EAVE) * (1 - abs(y) / AY) - 0.03
        if variant == "ruins":
            p.box((x - 0.05, y - 0.05, DECK_H), (x + 0.05, y + 0.05, DECK_H + 0.2 + 0.1 * (k % 3)), POST)
            continue
        base, head = Vector((x, y, DECK_H)), Vector((x, y, top))
        if variant == "abandoned" and k == 0:
            head += Vector((0.25, 0.1, -0.1))  # leaning
        p.bar(base, head, 0.045, POST)


def _tables(p, variant):
    spots = ((-0.95, -0.55), (0.0, -0.6), (0.95, -0.55))
    if variant == "ruins":
        _table(p, (-0.6, -0.6), fallen=True)  # one table left, on its side; the rest burnt
        return
    for k, at in enumerate(spots):
        fallen = (variant == "damaged" and k == 2) or variant == "abandoned"
        _table(p, at, fallen)


def _sign(p, variant):
    if variant in ("abandoned", "ruins"):
        cc.block(p, (0.5, -1.05, 0.05), (0.7, 0.04, 0.22), (1.45, 0.0, 0.3), SIGN)  # fallen on the sand
        return
    p.box((-0.35, -AY - 0.02, EAVE + 0.02), (0.35, -AY + 0.02, EAVE + 0.28), SIGN)


def _awning(p, variant):
    """The gable of cloth along x: from the front eave up to the ridge and down to the back eave, a thin sheet; its valance."""
    def sheet(x0, x1, sag=0.0):
        rings = []
        for x in (x0, x1):
            ring = [Vector((x, -AY, EAVE)), Vector((x, 0.0, RIDGE - sag)), Vector((x, AY, EAVE)),
                    Vector((x, AY, EAVE + 0.025)), Vector((x, 0.0, RIDGE + 0.025 - sag)), Vector((x, -AY, EAVE + 0.025))]
            rings.append(ring)
        p.loft(rings, CLOTH)

    def valance(x0, x1):
        n = max(2, int((x1 - x0) / 0.25))
        for y in (-AY, AY):
            for k in range(n):
                a, b = x0 + (x1 - x0) * k / n, x0 + (x1 - x0) * (k + 1) / n
                p.loft([[Vector((a, y - 0.01, EAVE)), Vector((b, y - 0.01, EAVE)), Vector(((a + b) / 2, y - 0.01, EAVE - 0.12))],
                        [Vector((a, y + 0.01, EAVE)), Vector((b, y + 0.01, EAVE)), Vector(((a + b) / 2, y + 0.01, EAVE - 0.12))]], CLOTH)

    if variant == "ruins":
        # The frame of the awning fallen on the deck, charred.
        for k, (a, b) in enumerate((((-1.3, -0.5, 0.12), (1.2, -0.3, 0.08)), ((-1.2, 0.4, 0.1), (1.3, 0.6, 0.14)),
                                    ((-0.6, -0.8, 0.09), (-0.4, 0.8, 0.15)))):
            p.bar(a, b, 0.035, POST)
        cc.block(p, (0.5, 0.1, 0.1), (0.9, 0.6, 0.02), (0.1, -0.08, 0.2), CLOTH)  # a scrap of the cloth
        return
    if variant == "abandoned":
        # Burnt through at the left end down to its frame; the rest charred.
        sheet(-0.4, AX)
        for y in (-AY, 0.0, AY):
            z = RIDGE if y == 0.0 else EAVE
            p.bar((-AX, y, z), (-0.4, y, z), 0.025, POST)
        for x in (-AX, -0.95):
            p.bar((x, -AY, EAVE), (x, 0.0, RIDGE), 0.022, POST)
            p.bar((x, 0.0, RIDGE), (x, AY, EAVE), 0.022, POST)
        valance(-0.4, AX)
        return
    if variant == "damaged":
        sheet(-AX, -0.3)
        sheet(0.15, AX)
        # The torn flap hanging from the ridge into the gap.
        cc.block(p, (-0.07, -0.35, RIDGE - 0.35), (0.42, 0.02, 0.55), (0.35, 0.0, 0.0), CLOTH)
        valance(-AX, AX)
        return
    sheet(-AX, AX)
    valance(-AX, AX)


def _material(name, variant):
    """Weathered deck planks, the counter's boards painted blue under a light wooden top, dark posts, the awning in red and white
    stripes across the gable (along y), a bright yellow sign, bottles in dots of green, brown and clear, metal; per state, soot and
    char."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 4.0)
    planks = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 8.0)), -14.0, 1.6))
    deck = g.mix(planks, g.mix(grain, lin((0.58, 0.50, 0.40)), lin((0.66, 0.58, 0.47))), lin((0.30, 0.25, 0.20)))
    boards_j = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 9.0)), -14.0, 1.6))
    boards = g.mix(boards_j, g.mix(g.noise(6.0, 3.0), lin((0.20, 0.42, 0.66)), lin((0.30, 0.52, 0.74))), lin((0.10, 0.16, 0.24)))
    top = g.mix(grain, lin((0.70, 0.58, 0.42)), lin((0.78, 0.66, 0.50)))
    post = g.mix(grain, lin((0.26, 0.19, 0.13)), lin((0.34, 0.25, 0.17)))
    stripe = g.clamp01(g.math('MULTIPLY_ADD', g.math('SINE', g.math('MULTIPLY', x, 10.5)), 40.0, 0.5))
    cloth = g.mix(stripe, lin((0.80, 0.14, 0.12)), lin((0.94, 0.92, 0.88)))
    cloth = g.mix(g.math('MULTIPLY', g.noise(8.0, 3.0), 0.08), cloth, lin((0.9, 0.88, 0.84)))
    down = g.clamp01(g.math('MULTIPLY_ADD', g.axis(g.normal_world(), 'Z'), -4.0, -0.6))
    cloth = g.mix(g.math('MULTIPLY', down, 0.5), cloth, lin((0.30, 0.10, 0.09)))  # the shade under it
    sign = g.mix(grain, lin((0.96, 0.78, 0.18)), lin((0.98, 0.84, 0.28)))
    hue = g.noise(40.0, 1.0)
    bottles = g.mix(g.clamp01(g.math('MULTIPLY_ADD', hue, 6.0, -3.0)), lin((0.12, 0.40, 0.18)), lin((0.50, 0.30, 0.12)))
    metal = lin((0.36, 0.37, 0.39))
    dark = lin((0.05, 0.05, 0.05))
    albedo = cc.paint(g, mask, (deck, boards, top, post, cloth, sign, bottles, metal, dark))
    if variant != "intact":
        amount = {"damaged": -3.0, "abandoned": -1.4, "ruins": 0.2}[variant]
        scorch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.5, 3.0), 5.0, amount))
        albedo = g.mix(g.math('MULTIPLY', scorch, 0.85), albedo, lin((0.08, 0.07, 0.06)))
        if variant == "ruins":
            albedo = g.mix(0.55, albedo, lin((0.15, 0.13, 0.11)))
    sand = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -30.0, 1.4)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(8.0, 2.0), 3.0, -1.2)))
    albedo = g.mix(g.math('MULTIPLY', sand, 0.3 if variant == "ruins" else 0.8), albedo, lin((0.86, 0.78, 0.62)))
    rough = cc.weighted(g, mask, (0.9, 0.6, 0.5, 0.85, 0.85, 0.5, 0.2, 0.4, 0.9))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.7, 0.0))
    normal = g.bump(g.math('ADD', g.math('MULTIPLY', g.math('ADD', planks, boards_j), -0.5), g.math('MULTIPLY', grain, 0.3)), 0.06, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("base", "counter", "body", "roof"))
    base, counter, body, roof = Parts(), Parts(), Parts(), Parts()
    _deck(base)
    _counter(counter, variant)
    _posts(body, variant)
    _tables(body, variant)
    _sign(body, variant)
    _awning(roof, variant)
    return cc.finish(variant_id, [(base, "base"), (counter, "counter"), (body, "body"), (roof, "roof")], DECK_H,
                     _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
