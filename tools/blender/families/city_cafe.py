"""City cafe: element 22 of the city set (docs/city_elements/22_cafe) and its states (appearance.md, 4). City climate: dry daylight, no
snow. No soldiers go in (README.md, 3): no occupied states and no team zone.

Footprint 2x1 cells (x 2, y 1), origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the
buildings of the map (the game squashes it upright, game/data/visuals/models.yaml): one tall storey to the roof at 1.5 and the parapet
at 1.68, the parasols to 1.05, so after the squash it stands lower than the shop. The parts:
    body      a one-storey cafe of light cream brick on the back half of the footprint, a white cornice and parapet round a flat gravel roof
              with a vent; a door of glass in a dark frame in the middle of the front
    windows   wide shop windows either side of the door, warm glass in dark green frames (a node of its own, prompts.md, 4)
    awning    a striped red and white awning over the door and the windows
    terrace   a wooden deck on the front half with a low rail, three small dark brown tables with chairs, each under a parasol of its
              own colour
No emblems, no text.
Nodes: "body", "windows", "awning", "terrace", "base" (the pavement). The abandoned one also has fire_1..2. One GLB per state:
    city_cafe               whole
    city_cafe_damaged       damaged: a window broken, the awning torn and hanging, two tables knocked over, a parasol down
    city_cafe_abandoned     burnt («горит», appearance.md, 4): the windows out and blackened over, soot up the front, the awning
                            burnt away to its frame, the deck charred, the tables and parasols down
    city_cafe_ruins         destroyed: stumps of the walls, heaps, charred boards and a broken table on the pavement
Budget 6000 triangles, texture 1024 (docs/city_elements/22_cafe/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_cafe as ccf
    print(ccf.run("city_cafe"))   # or ccf.run_all()
"""

import math

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_cafe": "intact",
    "city_cafe_damaged": "damaged",
    "city_cafe_abandoned": "abandoned",
    "city_cafe_ruins": "ruins",
}

BRICK, WHITE, GLASS, FRAME, AWN, DECK, TABLE, PAVE, DARK, VENT, P_RED, P_BLUE, P_YELLOW, CHAR, ROOF = (cc.part(i) for i in range(15))
PARTS = 15

X0 = 0.92
YF, YB = 0.0, 0.46  # the front and the back wall
WALLS = cc.Facade(X0, -YF, YB)
PAVE_H = 0.04
DECK_H = 0.1
ROOF_Z = 1.5
PARAPET = 1.68
WIN = (0.22, 1.05)  # the windows' bottom and top
TABLES = ((-0.6, -0.24, P_RED), (0.0, -0.28, P_BLUE), (0.6, -0.24, P_YELLOW))
FIRE_POINTS = ((-0.5, YF - 0.02, 0.7), (0.2, 0.25, ROOF_Z))


# --- the cafe ----------------------------------------------------------------------------

def _body(p, variant):
    """The walls as one solid, the roof its top; the cornice and parapet; the door."""
    faces = cc.made(p, lambda: p.box((-X0, YF, PAVE_H), (X0, YB, ROOF_Z), BRICK))
    cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > ROOF_Z - 0.01], ROOF)
    z0 = ROOF_Z - 0.08
    p.box((-X0 - 0.03, YF - 0.03, z0), (X0 + 0.03, YF + 0.07, PARAPET), WHITE)
    p.box((-X0 - 0.03, YB - 0.07, z0), (X0 + 0.03, YB + 0.03, PARAPET), WHITE)
    p.box((-X0 - 0.03, YF + 0.07, z0), (-X0 + 0.07, YB - 0.07, PARAPET), WHITE)
    p.box((X0 - 0.07, YF + 0.07, z0), (X0 + 0.03, YB - 0.07, PARAPET), WHITE)
    p.cylinder((0.5, 0.25, ROOF_Z + 0.1), 'Z', 0.06, 0.1, VENT, sides=8)
    WALLS.box(p, "front", -0.16, 0.16, PAVE_H, 1.0, -0.01, 0.012, DARK if variant == "abandoned" else GLASS)  # the door
    for a in (-0.19, 0.16):
        WALLS.box(p, "front", a, a + 0.03, PAVE_H, 1.03, -0.01, 0.025, FRAME)
    WALLS.box(p, "front", -0.19, 0.19, 1.0, 1.03, -0.01, 0.025, FRAME)


def _windows(p, variant):
    """Wide windows either side of the door, three panes each in dark green frames."""
    z0, z1 = WIN
    for side in (-1, 1):
        edges = [side * x for x in (0.26, 0.47, 0.68, 0.86)]
        lo, hi = min(edges), max(edges)
        for k, (a, b) in enumerate(zip(sorted(edges), sorted(edges)[1:])):
            broken = variant == "abandoned" or variant == "damaged" and side < 0 and k == 1
            WALLS.box(p, "front", a, b, z0, z1, -0.01, 0.006, DARK if broken else GLASS)
            WALLS.box(p, "front", b - 0.012, b + 0.012, z0, z1, -0.01, 0.02, FRAME)
        WALLS.box(p, "front", lo - 0.012, lo + 0.012, z0, z1, -0.01, 0.02, FRAME)
        WALLS.box(p, "front", lo - 0.012, hi + 0.012, z1, z1 + 0.03, -0.01, 0.025, FRAME)
        WALLS.box(p, "front", lo - 0.02, hi + 0.02, z0 - 0.04, z0, -0.01, 0.04, FRAME)


def _awning(p, variant):
    """The striped awning over the door and the windows; torn and hanging when damaged, only its frame left when burnt."""
    hi, lo, out = 1.2, 1.0, 0.24
    if variant == "abandoned":
        p.bar((-0.88, YF - 0.01, hi), (-0.88, YF - out, lo), 0.01, DARK)
        p.bar((0.88, YF - 0.01, hi), (0.88, YF - out, lo), 0.01, DARK)
        p.bar((-0.88, YF - out, lo), (0.88, YF - out, lo), 0.01, DARK)
        return
    x1 = 0.1 if variant == "damaged" else 0.88
    p.loft([[Vector((x, YF - 0.01, hi)), Vector((x, YF - 0.01, hi + 0.02)), Vector((x, YF - out, lo + 0.02)), Vector((x, YF - out, lo))]
            for x in (-0.88, x1)], AWN)
    p.box((-0.88, YF - out - 0.01, lo - 0.09), (x1, YF - out + 0.005, lo + 0.02), AWN)
    if variant == "damaged":  # the torn right part hanging down the front
        cc.block(p, (0.5, YF - 0.06, 0.95), (0.76, 0.02, 0.3), (0.0, 0.0, 0.0), AWN)


def _table(p, x, y, parasol, down=False):
    """A small round table with two chairs, under a parasol of its colour. Down: knocked over on its side, the parasol on the deck."""
    z = DECK_H
    if down:
        cc.block(p, (x, y, z + 0.1), (0.16, 0.16, 0.025), (math.pi / 2 - 0.2, 0.0, 0.5), TABLE)
        p.bar((x - 0.15, y + 0.02, z + 0.03), (x + 0.12, y - 0.1, z + 0.03), 0.012, TABLE)
        cc.block(p, (x + 0.15, y + 0.15, z + 0.02), (0.36, 0.36, 0.02), (0.15, 0.1, 0.3), parasol)
        return
    p.cylinder((x, y, z + 0.4), 'Z', 0.09, 0.012, TABLE, sides=10)
    p.bar((x, y, z), (x, y, z + 0.39), 0.012, TABLE)
    for dx in (-0.15, 0.15):  # the chairs
        p.box((x + dx - 0.04, y - 0.04, z + 0.2), (x + dx + 0.04, y + 0.04, z + 0.23), TABLE)
        p.box((x + dx + (0.035 if dx > 0 else -0.045), y - 0.04, z + 0.23), (x + dx + (0.045 if dx > 0 else -0.035), y + 0.04, z + 0.4), TABLE)
        p.bar((x + dx, y, z), (x + dx, y, z + 0.2), 0.008, TABLE)
    p.bar((x, y, z + 0.41), (x, y, 0.95), 0.008, TABLE)  # the parasol's pole
    p.turned((x, y, 0.88), 'Z', [(0.0, 0.2), (0.02, 0.2), (0.12, 0.02)], parasol, sides=8)


def _terrace(p, variant):
    p.box((-X0, -0.48, PAVE_H), (X0, YF, DECK_H), CHAR if variant == "abandoned" else DECK)
    if variant != "ruins":
        for x in cc.steps(-X0 + 0.02, X0 - 0.02, 0.3):  # the low rail
            if abs(x) > 0.2:
                p.bar((x, -0.47, DECK_H), (x, -0.47, DECK_H + 0.14), 0.01, CHAR if variant == "abandoned" else DECK)
        for a, b in ((-X0 + 0.02, -0.2), (0.2, X0 - 0.02)):
            p.box((a, -0.48, DECK_H + 0.12), (b, -0.46, DECK_H + 0.15), CHAR if variant == "abandoned" else DECK)
    for k, (x, y, parasol) in enumerate(TABLES):
        down = variant in ("abandoned", "ruins") or variant == "damaged" and k != 1
        if variant == "ruins" and k != 0:
            continue
        _table(p, x, y, CHAR if variant == "abandoned" else parasol, down=down)


# --- ruins ------------------------------------------------------------------------------

def _ruins(p):
    for lo, hi in (((-X0, YF, PAVE_H), (-0.3, YF + 0.1, 0.6)), ((-X0, YF + 0.1, PAVE_H), (-X0 + 0.1, YB, 0.9)),
                   ((-0.4, YB - 0.1, PAVE_H), (X0, YB, 0.5)), ((X0 - 0.1, 0.15, PAVE_H), (X0, YB - 0.1, 0.35)),
                   ((0.4, YF, PAVE_H), (X0, YF + 0.1, 0.25))):
        p.box(lo, hi, BRICK)
    for c, s, t in (((0.0, 0.25, 0.15), (0.9, 0.35, 0.22), (0.1, 0.15, 0.3)), ((0.5, 0.2, 0.12), (0.5, 0.3, 0.16), (0.2, 0.1, 1.0)),
                    ((-0.5, 0.3, 0.12), (0.4, 0.25, 0.15), (0.15, 0.2, 0.6))):
        cc.block(p, c, s, t, BRICK)
    for c, s, t in (((-0.3, -0.25, PAVE_H + 0.02), (0.6, 0.06, 0.03), (0.0, 0.1, 0.4)), ((0.3, -0.3, PAVE_H + 0.02), (0.5, 0.06, 0.03), (0.1, 0.0, -0.7)),
                    ((0.6, -0.15, PAVE_H + 0.02), (0.4, 0.06, 0.03), (0.0, 0.2, 0.2))):
        cc.block(p, c, s, t, CHAR)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Light cream brick, white trim, warm glass, dark green frames, red and white stripes, warm wood, dark brown tables, the
    parasols' colours, paving; soot and char when burnt."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    course = g.math('MULTIPLY', z, 30.0)
    row = g.math('FRACT', course)
    col = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 8.0), g.math('MULTIPLY', g.math('FLOOR', course), 0.5)))
    mortar = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', row, -12.0, 2.0)), g.clamp01(g.math('MULTIPLY_ADD', col, -25.0, 2.0)))
    brick = g.mix(mortar, g.mix(g.noise(5.0, 2.0), lin((0.84, 0.76, 0.62)), lin((0.90, 0.82, 0.68))), lin((0.74, 0.72, 0.68)))
    white = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, -0.8, 1.0)), lin((0.30, 0.30, 0.32)), lin((0.66, 0.50, 0.30)))
    frame = g.mix(grain, lin((0.12, 0.24, 0.18)), lin((0.16, 0.30, 0.22)))
    stripe = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 6.0)), 40.0, -20.0))
    awn = g.mix(stripe, lin((0.72, 0.16, 0.14)), lin((0.92, 0.90, 0.84)))
    plank = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 14.0)), -20.0, 1.5))
    deck = g.mix(plank, g.mix(g.noise(3.0, 6.0), lin((0.52, 0.36, 0.22)), lin((0.62, 0.44, 0.27))), lin((0.30, 0.20, 0.12)))
    table = g.mix(grain, lin((0.24, 0.16, 0.10)), lin((0.30, 0.20, 0.13)))
    slabs = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 4.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 4.0)), -30.0, 1.5)))
    pave = g.mix(slabs, g.mix(grain, lin((0.60, 0.59, 0.56)), lin((0.68, 0.67, 0.64))), lin((0.44, 0.43, 0.41)))
    dark = g.mix(grain, lin((0.08, 0.08, 0.09)), lin((0.13, 0.13, 0.14)))
    vent = g.mix(grain, lin((0.46, 0.47, 0.49)), lin((0.54, 0.55, 0.57)))
    red, blue, yellow = lin((0.80, 0.18, 0.15)), lin((0.18, 0.38, 0.72)), lin((0.94, 0.78, 0.18))
    char = g.mix(g.noise(10.0, 4.0), lin((0.06, 0.05, 0.05)), lin((0.16, 0.12, 0.09)))
    roof = g.mix(g.noise(40.0, 4.0), lin((0.42, 0.41, 0.39)), lin((0.54, 0.53, 0.51)))  # gravel
    albedo = cc.paint(g, mask, (brick, white, glass, frame, awn, deck, table, pave, dark, vent, red, blue, yellow, char, roof))
    if variant in ("damaged", "abandoned"):
        albedo = g.mix(g.math('MULTIPLY', mask[0], cc.cracks(g, 7.0)), albedo, lin((0.30, 0.26, 0.20)))
    if variant == "abandoned":  # soot up the front over the windows and the door, and in patches
        front = g.clamp01(g.math('MULTIPLY_ADD', y, -30.0, 1.5))
        up = g.math('MULTIPLY', front, g.clamp01(g.math('MULTIPLY_ADD', z, 2.0, -0.4)))
        soot = g.math('MAXIMUM', up, g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.0, 2.0), 6.0, -3.0)))
        albedo = g.mix(g.math('MULTIPLY', g.math('ADD', g.math('ADD', mask[0], mask[1]), mask[3]), g.math('MULTIPLY', soot, 0.85)),
                       albedo, lin((0.08, 0.07, 0.07)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.5, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    rough = cc.weighted(g, mask, (0.85, 0.6, 0.12, 0.45, 0.9, 0.8, 0.6, 0.85, 0.9, 0.5, 0.85, 0.85, 0.85, 0.95, 0.95))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.2, 0.0, 0.0, 0.0, 0.0, 0.0, 0.3, 0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', mortar, -1.0), g.noise(60.0, 3.0)), 0.04, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "windows", "awning", "terrace", "base"))
    variant = VARIANTS[variant_id]
    body, terrace = Parts(), Parts()
    _terrace(terrace, variant)
    parts = [(body, "body")]
    if variant == "ruins":
        _ruins(body)
    else:
        _body(body, variant)
        windows, awning = Parts(), Parts()
        _windows(windows, variant)
        _awning(awning, variant)
        parts += [(windows, "windows"), (awning, "awning")]
    parts.append((terrace, "terrace"))
    base = Parts()
    base.box((-0.98, -0.49, 0.0), (0.98, 0.49, PAVE_H), PAVE)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, windows, awning, terrace and base (fire_1..2 when
    burnt)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
