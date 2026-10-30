"""City shop: element 06 of the city set (docs/city_elements/06_shop) and its states (appearance.md, 4). City climate: dry daylight,
no snow. No soldiers go in (README.md, 3), so it has no occupied states and no team zone.

Footprint 2x2 cells, origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the buildings of
the map (the game squashes it upright, game/data/visuals/models.yaml): a tall shop floor and a low storey over it, the roof at 2.3 and
the parapet at 2.5 (prompts.md, 1). The parts:
    shop      cream plastered walls set back from the street, a white cornice and parapet round a flat gravel roof with a vent and an
              air box; on the shop floor a display window across the whole front in dark green frames, the glass warm with the light
              inside, a glass door in its middle; three white-framed windows on the storey over it, two on each side; a back door
    sign      a plain board over the window in warm red, no text
    awning    a striped fabric awning over the pavement, red and cream, on two thin arms
    pavement  the pavement under the awning and round the shop, with a kerb
No emblems, no text, no goods in the window (prompts.md, 4). The glass is baked opaque with the rest.
Nodes: "body" (the shop, its windows, the sign, and the shards or rubble of a state), "awning" (a node of its own, prompts.md, 4),
"base" (the pavement). The abandoned one also has fire_1..3. One GLB per state:
    city_shop               whole
    city_shop_damaged       damaged: the display window broken in places, the sign hanging askew, cracks, glass on the pavement
    city_shop_abandoned     critically damaged: the awning fallen onto the pavement, the window all broken, the front right corner
                            of the storey fallen in, soot, rubble
    city_shop_ruins         destroyed: stumps of the walls, the sign's two posts standing out of the ruins (appearance.md, 4), heaps
                            and glass
Budget 6000 triangles, texture 1024 (docs/city_elements/06_shop/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_shop as csh
    print(csh.run("city_shop"))   # or csh.run_all()
"""

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_shop": "intact",
    "city_shop_damaged": "damaged",
    "city_shop_abandoned": "abandoned",
    "city_shop_ruins": "ruins",
}

PLASTER, WHITE, GLASS, FRAME, SIGN, AWNING, DOOR, ROOF, PAVE, DARK, METAL = (cc.part(i) for i in range(11))
PARTS = 11

X0 = 0.85
YF, YB = -0.5, 0.9  # the front and the back wall: the shop stands back, the pavement under the awning in front
WALLS = cc.Facade(X0, -YF, YB)
PAVE_H = 0.06
SHOP_TOP = 1.35  # the top of the shop floor
ROOF_Z = 2.3
PARAPET = 2.5
WINDOW = (PAVE_H + 0.14, 1.06)  # the display window, bottom and top
UPPER = (1.6, 2.02)
AWNING_Z = (1.12, 0.86)  # where the awning leaves the wall, where its edge hangs
AWNING_OUT = 0.46
FIRE_POINTS = ((0.4, YF - 0.02, 0.7), (0.55, 0.0, 1.5), (-0.4, 0.3, ROOF_Z))


# --- the shop -----------------------------------------------------------------------------

def _block(p, abandoned):
    """Whole: one solid, its top the gravel roof (repainted). Abandoned: hollow walls, the front right corner of the storey fallen
    in, the floor between the storeys inside."""
    if not abandoned:
        top = [f for f in cc.made(p, lambda: p.box((-X0, YF, PAVE_H), (X0, YB, ROOF_Z), PLASTER))
               if abs(f.normal.z) > 0.99 and f.calc_center_median().z > ROOF_Z - 0.01]
        cc.repaint(p, top, ROOF)
        _parapet(p, X0, YF)
    else:
        t = 0.1
        p.box((-X0, YF, PAVE_H), (0.2, YF + t, ROOF_Z), PLASTER)
        p.box((0.2, YF, PAVE_H), (X0, YF + t, SHOP_TOP + 0.15), PLASTER)
        p.box((-X0, YB - t, PAVE_H), (X0, YB, ROOF_Z), PLASTER)
        p.box((-X0, YF + t, PAVE_H), (-X0 + t, YB - t, ROOF_Z), PLASTER)
        p.box((X0 - t, YF + t, PAVE_H), (X0, 0.25, SHOP_TOP + 0.35), PLASTER)
        p.box((X0 - t, 0.25, PAVE_H), (X0, YB - t, ROOF_Z), PLASTER)
        p.box((-X0 + t, YF + t, SHOP_TOP - 0.06), (X0 - t, YB - t, SHOP_TOP), DOOR)  # the floor of the storey, wooden
        p.box((-X0 + t, 0.25, ROOF_Z - 0.08), (X0 - t, YB - t, ROOF_Z), ROOF)  # the roof that stands, at the back
        p.box((-X0 + t, YF + t, ROOF_Z - 0.08), (0.2, 0.25, ROOF_Z), ROOF)
        _parapet(p, X0, YF, broken=True)
    WALLS.box(p, "front", -X0 - 0.02, X0 + 0.02, SHOP_TOP - 0.05, SHOP_TOP + 0.03, -0.01, 0.04, WHITE)  # a band between the floors
    for wall in ("left", "right"):
        WALLS.box(p, wall, YF, YB, SHOP_TOP - 0.05, SHOP_TOP + 0.03, -0.01, 0.04, WHITE)


def _parapet(p, x0, yf, broken=False):
    """The white cornice and parapet round the roof; broken over the fallen corner."""
    z0, z1 = ROOF_Z - 0.08, PARAPET
    if broken:
        p.box((-x0 - 0.04, yf - 0.04, z0), (0.2, yf + 0.08, z1), WHITE)
        p.box((x0 - 0.08, 0.25, z0), (x0 + 0.04, YB - 0.08, z1), WHITE)
    else:
        p.box((-x0 - 0.04, yf - 0.04, z0), (x0 + 0.04, yf + 0.08, z1), WHITE)
        p.box((x0 - 0.08, yf + 0.08, z0), (x0 + 0.04, YB - 0.08, z1), WHITE)
    p.box((-x0 - 0.04, YB - 0.08, z0), (x0 + 0.04, YB + 0.04, z1), WHITE)
    p.box((-x0 - 0.04, yf + 0.08, z0), (-x0 + 0.08, YB - 0.08, z1), WHITE)


def _shopfront(p, variant):
    """The display window across the front in dark green frames, the glass door in its middle. Damaged: panes broken; abandoned:
    all of them."""
    z0, z1 = WINDOW
    edges = cc.steps(-0.75, 0.75, 0.25)
    for k, (a, b) in enumerate(zip(edges, edges[1:])):
        door = abs((a + b) / 2) < 0.15
        broken = variant == "abandoned" or variant == "damaged" and k in (0, 3, 4)
        WALLS.box(p, "front", a, b, PAVE_H if door else z0, z1, -0.01, 0.008, DARK if broken else GLASS)
        WALLS.box(p, "front", b - 0.015, b + 0.015, PAVE_H, z1, -0.01, 0.025, FRAME)
    WALLS.box(p, "front", -0.78, -0.75, PAVE_H, z1, -0.01, 0.025, FRAME)
    WALLS.box(p, "front", -0.78, 0.78, z1, z1 + 0.04, -0.01, 0.03, FRAME)
    WALLS.box(p, "front", -0.78, 0.78, PAVE_H, z0, -0.01, 0.035, FRAME)  # the low wall under the window
    WALLS.box(p, "back", 0.35, 0.65, PAVE_H, 0.95, -0.01, 0.015, DOOR)  # the back door


def _upper_windows(p, variant):
    broken_all = variant == "abandoned"
    n = 0
    for wall, us in (("front", (-0.5, 0.0, 0.5)), ("left", (0.1, 0.55)), ("right", (0.1, 0.55)), ("back", (-0.4, 0.4))):
        for u in us:
            if broken_all and (wall == "front" and u > 0.2 or wall == "right" and u < 0.25):
                continue  # the fallen corner
            n += 1
            broken = DARK if broken_all or variant == "damaged" and n % 3 == 0 else None
            WALLS.window(p, wall, u, UPPER[0], UPPER[1], 0.12, GLASS, WHITE, bars=(1, 1), broken=broken)


def _sign(p, variant):
    """The board over the window. Damaged: hanging askew from one end."""
    if variant == "damaged":
        cc.block(p, (0.0, YF - 0.035, 1.18), (1.44, 0.04, 0.18), (0.0, 0.12, 0.0), SIGN)
    elif variant != "abandoned":
        WALLS.box(p, "front", -0.72, 0.72, 1.17, 1.33, -0.005, 0.05, SIGN)


# --- the awning -------------------------------------------------------------------------

def _awning(p, variant):
    """The striped awning: a sloped sheet from the wall over the pavement with a valance along its edge, on two thin arms. Abandoned:
    torn off its arms and lying on the pavement."""
    hi, lo = AWNING_Z
    half = 0.8
    y_wall, y_edge = YF - 0.01, YF - AWNING_OUT
    if variant == "abandoned":
        cc.block(p, (0.1, YF - 0.42, PAVE_H + 0.06), (1.5, 0.5, 0.025), (0.15, 0.25, 0.1), AWNING)
        cc.block(p, (-0.55, YF - 0.25, 0.5), (0.5, 0.3, 0.02), (1.2, 0.2, 0.15), AWNING)  # a flap still hanging from the wall
        p.bar((0.55, YF - 0.02, hi), (0.75, YF - 0.5, PAVE_H + 0.02), 0.01, METAL)
        return
    p.loft([[Vector((x, y_wall, hi)), Vector((x, y_wall, hi + 0.02)), Vector((x, y_edge, lo + 0.02)), Vector((x, y_edge, lo))]
            for x in (-half, half)], AWNING)
    p.box((-half, y_edge - 0.01, lo - 0.1), (half, y_edge + 0.005, lo + 0.02), AWNING)  # the valance
    for x in (-half + 0.05, half - 0.05):
        p.bar((x, y_wall, lo - 0.08), (x, y_edge, lo), 0.008, METAL)


# --- rubble -----------------------------------------------------------------------------

SHARDS = tuple(((x, y, PAVE_H + 0.005), (0.07, 0.05, 0.008), (0.0, 0.0, 0.9 * k))
               for k, (x, y) in enumerate(((-0.6, -0.65), (-0.3, -0.8), (0.4, -0.7), (0.65, -0.85), (0.1, -0.9))))
RUBBLE_ABANDONED = (((0.55, -0.15, SHOP_TOP + 0.08), (0.4, 0.4, 0.12), (0.1, 0.2, 0.3)), ((0.6, YF - 0.15, PAVE_H + 0.08), (0.3, 0.2, 0.14), (0.2, 0.1, 0.4)),
                    ((X0 + 0.08, -0.2, PAVE_H + 0.07), (0.14, 0.3, 0.12), (0.1, 0.3, 1.0)))
RUINS_STUMPS = (((-X0, YF, PAVE_H), (-0.3, YF + 0.1, 0.7)), ((-X0, YF + 0.1, PAVE_H), (-X0 + 0.1, YB, 1.0)), ((-0.4, YB - 0.1, PAVE_H), (X0, YB, 0.6)),
                ((X0 - 0.1, 0.2, PAVE_H), (X0, YB - 0.1, 0.45)), ((0.3, YF, PAVE_H), (X0, YF + 0.1, 0.35)))
RUINS_HEAPS = (((0.0, 0.2, 0.2), (1.1, 0.9, 0.3), (0.1, 0.15, 0.3)), ((0.4, -0.2, 0.16), (0.6, 0.5, 0.22), (0.2, 0.1, 1.0)),
               ((-0.4, 0.5, 0.15), (0.5, 0.4, 0.2), (0.15, 0.2, 0.6)))


def _ruins(p):
    for lo, hi in RUINS_STUMPS:
        p.box(lo, hi, PLASTER)
    for centre, size, turn in RUINS_HEAPS:
        cc.block(p, centre, size, turn, PLASTER)
    for x in (-0.55, 0.5):  # the sign's posts, a piece of the board on one
        p.bar((x, YF - 0.06, PAVE_H), (x + 0.04, YF - 0.08, 1.25), 0.02, METAL)
    cc.block(p, (-0.45, YF - 0.1, 1.15), (0.5, 0.04, 0.17), (0.0, 0.3, 0.1), SIGN)
    cc.block(p, (0.2, YF - 0.3, PAVE_H + 0.03), (0.7, 0.3, 0.03), (0.1, 0.05, 0.3), AWNING)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Cream plaster, white trim, glass warm with the light inside, dark green frames, a warm red sign, red and cream stripes on the
    awning, wood, gravel, paving, metal; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    plaster = g.mix(g.noise(5.0, 3.0), lin((0.86, 0.80, 0.66)), lin((0.92, 0.86, 0.72)))
    white = g.mix(grain, lin((0.86, 0.86, 0.84)), lin((0.93, 0.93, 0.91)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, -0.8, 1.1)), lin((0.30, 0.30, 0.32)), lin((0.62, 0.48, 0.28)))  # warm low down
    frame = g.mix(grain, lin((0.12, 0.24, 0.18)), lin((0.16, 0.30, 0.22)))
    sign = g.mix(grain, lin((0.62, 0.20, 0.16)), lin((0.70, 0.26, 0.20)))
    stripe = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 5.0)), 40.0, -20.0))
    awning = g.mix(stripe, g.mix(grain, lin((0.66, 0.18, 0.16)), lin((0.74, 0.22, 0.19))), g.mix(grain, lin((0.88, 0.84, 0.74)), lin((0.94, 0.90, 0.80))))
    door = g.mix(g.noise(3.0, 6.0), lin((0.34, 0.22, 0.13)), lin((0.42, 0.28, 0.17)))
    roof = g.mix(g.noise(40.0, 4.0), lin((0.42, 0.41, 0.39)), lin((0.54, 0.53, 0.51)))
    slabs = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 4.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 4.0)), -30.0, 1.5)))
    pave = g.mix(slabs, g.mix(grain, lin((0.60, 0.59, 0.56)), lin((0.68, 0.67, 0.64))), lin((0.44, 0.43, 0.41)))
    dark = g.mix(grain, lin((0.06, 0.06, 0.06)), lin((0.11, 0.10, 0.10)))
    metal = g.mix(grain, lin((0.30, 0.32, 0.34)), lin((0.38, 0.40, 0.42)))

    albedo = cc.paint(g, mask, (plaster, white, glass, frame, sign, awning, door, roof, pave, dark, metal))
    if variant in ("damaged", "abandoned"):
        albedo = g.mix(g.math('MULTIPLY', mask[0], cc.cracks(g, 7.0)), albedo, lin((0.24, 0.21, 0.17)))
        soot = g.math('MULTIPLY', g.math('ADD', mask[0], mask[1]),
                      g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.0, 2.0), 6.0, -3.4 if variant == "damaged" else -2.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.75), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.5, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), g.mix(0.2, albedo, lin((0.60, 0.57, 0.52))), lin((0.10, 0.09, 0.09)))

    rough = cc.weighted(g, mask, (0.85, 0.6, 0.12, 0.45, 0.6, 0.9, 0.7, 0.95, 0.85, 0.9, 0.45))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.4, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.7))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[8], g.math('MULTIPLY', slabs, -1.0), g.noise(60.0, 3.0)), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _pavement(p):
    p.box((-0.98, -0.98, 0.0), (0.98, 0.98, PAVE_H), PAVE)
    p.box((-0.98, -0.99, 0.0), (0.98, -0.94, PAVE_H + 0.02), PAVE)  # the kerb


def build(variant_id):
    cc.clear(variant_id, ("body", "awning", "base"))
    variant = VARIANTS[variant_id]
    body = Parts()
    if variant == "ruins":
        _ruins(body)
    else:
        _block(body, variant == "abandoned")
        _shopfront(body, variant)
        _upper_windows(body, variant)
        _sign(body, variant)
        if variant != "abandoned":
            body.box((-0.4, 0.2, ROOF_Z), (-0.1, 0.5, ROOF_Z + 0.2), METAL)  # the air box
        body.cylinder((0.45, 0.55, ROOF_Z + 0.12), 'Z', 0.06, 0.12, METAL, sides=10)  # the vent
    if variant in ("damaged", "abandoned", "ruins"):
        for centre, size, turn in SHARDS:
            cc.block(body, centre, size, turn, GLASS)
    if variant == "abandoned":
        for centre, size, turn in RUBBLE_ABANDONED:
            cc.block(body, centre, size, turn, PLASTER)
    parts = [(body, "body")]
    if variant != "ruins":
        awning = Parts()
        _awning(awning, variant)
        parts.append((awning, "awning"))
    base = Parts()
    _pavement(base)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, awning and base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
