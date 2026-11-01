"""Lighthouse, element 12 of the beach set (docs/beach_elements/12_lighthouse): a white tower with red bands, a lantern on top.

The map building neutral_lighthouse (game/data/neutral/civilian.yaml): 2x2, 2000 hp, six soldiers go in. Footprint 2x2 cells, origin at
the centre of the footprint on the ground, the front to -Y. Made at the size the game draws it, not squashed (as the city's fountain):
a tower squashed upright would be a barrel, and the tallest thing of the coast should stand tall — the gallery at 2.05, the lantern's
dome to 2.62. The parts:
    body     a stone plinth, the round tower narrowing upward, white with two red bands, small dark windows up it and a door at its
             foot; the keeper's house against it, white plaster under a red tiled roof, its door and windows
    gallery  the round gallery under the lantern, its dark steel railing
    lantern  the glass lantern on its mullions, the light's warm core inside, the red dome with its ball and vane
Nodes: "body", "gallery", "lantern" (the game may light the core); the abandoned one also fire_1..2. One GLB per state:
    beach_lighthouse                   whole
    beach_lighthouse_occupied          whole, its soldiers in: barrels out of the tower's windows and the house's
    beach_lighthouse_damaged           damaged: the lantern's glass broken, the light out, cracks and soot, a chunk out of the plinth
    beach_lighthouse_damaged_occupied  the same, with the barrels
    beach_lighthouse_abandoned         critically damaged: the tower leaning, the lantern's dome knocked askew, the house's roof
                                       burning through, fire_1..2
    beach_lighthouse_ruins             destroyed: the tower broken off above the first band, its top lying on the sand in pieces, the
                                       house's walls low, rubble
Budget 10000 triangles, texture 2048.

In Blender, after tools/blender is on sys.path:
    import families.beach_lighthouse as lighthouse
    print(lighthouse.run_all())
"""

import math

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "beach"
SIZE = 2048
VARIANTS = {
    "beach_lighthouse": "intact",
    "beach_lighthouse_occupied": "occupied",
    "beach_lighthouse_damaged": "damaged",
    "beach_lighthouse_damaged_occupied": "damaged_occupied",
    "beach_lighthouse_abandoned": "abandoned",
    "beach_lighthouse_ruins": "ruins",
}

STONE, TOWER, METAL, GLASS, GLOW, RED, PLASTER, TILE, DARK = (cc.part(i) for i in range(9))
PARTS = 9

AT = Vector((0.22, 0.2, 0.0))  # the tower's axis
PLINTH = 0.12
BASE_R, TOP_R, TOWER_TOP = 0.48, 0.3, 2.02
SIDES = 16
FIRE_POINTS = ((-0.6, -0.5, 0.65), (-0.45, -0.25, 0.5))


def _radius(z):
    return BASE_R + (TOP_R - BASE_R) * (z - PLINTH) / (TOWER_TOP - PLINTH)


def _tower(p, top=TOWER_TOP):
    p.turned(AT, 'Z', [(0.0, 0.68), (PLINTH, 0.66)], STONE, sides=12)  # the plinth
    p.turned(AT, 'Z', [(PLINTH, BASE_R), (top, _radius(top))], TOWER, sides=SIDES)
    # Small windows up the tower, a little out of its face, turning round it; the door at its foot, to the front.
    for k, z in enumerate((0.75, 1.15, 1.55, 1.85)):
        if z > top - 0.08:
            break
        a = math.radians(-90 + 70 * k)
        r = _radius(z)
        out = Vector((math.cos(a), math.sin(a), 0.0))
        c = AT + out * r + Vector((0.0, 0.0, z))
        side = Vector((-out.y, out.x, 0.0))
        p.loft([[c - side * 0.045 + Vector((0, 0, -0.07)), c + side * 0.045 + Vector((0, 0, -0.07)), c + side * 0.045 + Vector((0, 0, 0.07)),
                 c - side * 0.045 + Vector((0, 0, 0.07))],
                [q + out * 0.012 for q in (c - side * 0.045 + Vector((0, 0, -0.07)), c + side * 0.045 + Vector((0, 0, -0.07)),
                                           c + side * 0.045 + Vector((0, 0, 0.07)), c - side * 0.045 + Vector((0, 0, 0.07)))]], DARK)
    door = AT + Vector((0.0, -BASE_R + 0.02, 0.0))
    p.box((door.x - 0.09, door.y - 0.03, PLINTH), (door.x + 0.09, door.y, PLINTH + 0.3), DARK)


def _windows_out():
    """The tower's windows, for the barrels: (point, out)."""
    out = []
    for k, z in enumerate((0.75, 1.15, 1.55)):
        a = math.radians(-90 + 70 * k)
        d = Vector((math.cos(a), math.sin(a), 0.0))
        out.append((tuple(AT + d * (_radius(z) + 0.02) + Vector((0.0, 0.0, z))), tuple(d)))
    return out


def _house(p, variant):
    """The keeper's house against the tower, front left: white plaster, a red tiled roof."""
    x0, x1, y0, y1, eave = -0.95, -0.3, -0.92, -0.05, 0.62
    low = variant == "ruins"
    p.box((x0, y0, 0.0), (x1, y1, 0.22 if low else eave), PLASTER)
    if low:
        return
    walls = cc.Facade((x1 - x0) / 2, (y1 - y0) / 2)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2

    def at(wall, u0, u1, z0, z1, part):
        lo, hi = {"front": ((cx + u0, y0 - 0.012, z0), (cx + u1, y0, z1)), "left": ((x0 - 0.012, cy + u0, z0), (x0, cy + u1, z1))}[wall]
        p.box(lo, hi, part)

    at("front", -0.22, -0.06, 0.0, 0.42, DARK)  # the door
    at("front", 0.06, 0.24, 0.26, 0.44, DARK if variant != "intact" and variant != "occupied" else GLASS)
    at("left", -0.2, 0.0, 0.26, 0.44, DARK if variant != "intact" and variant != "occupied" else GLASS)
    # The roof: a gable along x, red tiles; burnt through at its far end when abandoned.
    end = x1 + 0.06 if variant != "abandoned" else -0.5
    rings = [[Vector((x, y0 - 0.06, eave)), Vector((x, cy, eave + 0.3)), Vector((x, y1 + 0.06, eave)), Vector((x, y1 + 0.06, eave + 0.03)),
              Vector((x, cy, eave + 0.33)), Vector((x, y0 - 0.06, eave + 0.03))] for x in (x0 - 0.06, end)]
    p.loft(rings, TILE)


def _gallery(p):
    z = TOWER_TOP
    p.turned(AT, 'Z', [(z, 0.42), (z + 0.04, 0.42)], METAL, sides=SIDES)
    p.turned(AT, 'Z', [(z + 0.2, 0.41), (z + 0.22, 0.41)], METAL, sides=SIDES)  # the rail
    for k in range(8):
        a = 2 * math.pi * k / 8
        q = AT + Vector((math.cos(a) * 0.41, math.sin(a) * 0.41, 0.0))
        p.bar((q.x, q.y, z + 0.04), (q.x, q.y, z + 0.2), 0.008, METAL)


def _lantern(p, variant):
    z = TOWER_TOP + 0.04
    broken = variant in ("damaged", "damaged_occupied", "abandoned")
    p.turned(AT, 'Z', [(z, 0.25), (z + 0.32, 0.25)], DARK if broken else GLASS, sides=8)
    if not broken:
        p.turned(AT, 'Z', [(z + 0.06, 0.1), (z + 0.26, 0.1)], GLOW, sides=8)  # the light's core, seen through the glass
    for k in range(8):  # the mullions
        a = 2 * math.pi * (k + 0.5) / 8
        q = AT + Vector((math.cos(a) * 0.255, math.sin(a) * 0.255, 0.0))
        p.bar((q.x, q.y, z), (q.x, q.y, z + 0.32), 0.008, METAL)
    dome = [(z + 0.32, 0.29), (z + 0.36, 0.27), (z + 0.46, 0.16), (z + 0.53, 0.04)]
    if variant == "abandoned":
        before = set(p.bm.verts)
        p.turned(AT, 'Z', dome, RED, sides=SIDES)
        m = Matrix.Translation(AT + Vector((0.0, 0.0, z + 0.32))) @ Matrix.Rotation(0.35, 4, 'X') @ Matrix.Translation(-(AT + Vector((0.0, 0.0, z + 0.32))))
        for v in p.bm.verts:
            if v not in before:
                v.co = m @ v.co
        return
    p.turned(AT, 'Z', dome, RED, sides=SIDES)
    p.sphere(AT + Vector((0.0, 0.0, z + 0.56)), 'Z', 0.028, METAL, sides=8, rings=4)
    p.bar(AT + Vector((0.0, 0.0, z + 0.58)), AT + Vector((0.0, 0.0, z + 0.72)), 0.006, METAL)  # the vane's spike


def _lean(parts, angle, which):
    """Leans what stands on the plinth, about the tower's foot, of the vertices which picks: its top pushed back and to the right."""
    m = Matrix.Translation(AT) @ Matrix.Rotation(angle, 4, Vector((1.0, -0.6, 0.0)).normalized()) @ Matrix.Translation(-AT)
    for p in parts:
        for v in p.bm.verts:
            if v.co.z > PLINTH + 0.01 and which(v.co):
                v.co = m @ v.co


def _ruins(p, rnd):
    stump = 0.85
    _tower(p, stump)
    for k in range(6):  # its top in pieces on the sand behind it, white and red
        a = rnd.uniform(-0.4, 1.4)
        c = AT + Vector((math.cos(a) * (0.7 + 0.08 * k), math.sin(a) * (0.6 + 0.05 * k), 0.08))
        cc.block(p, tuple(c), (0.32, 0.28, 0.18), (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), a), TOWER if k % 3 else RED)
    cc.block(p, (0.75, 0.82, 0.12), (0.36, 0.36, 0.22), (1.3, 0.2, 0.4), RED)  # the dome
    ic.rubble(p, rnd, -0.9, 0.9, -0.9, 0.9, 0.0, 14, 0.09, STONE)


def _occupied(p):
    for point, out in _windows_out():
        ic.barrels(p, [point], out, DARK, length=0.32)
    ic.barrels(p, [(-0.62, -0.93, 0.36)], (0.0, -1.0, 0.0), DARK, length=0.3)


def _material(name, variant):
    """Grey stone, the tower white with two red bands, dark steel, light glass, the light's warm core, red dome and tiles, white
    plaster; cracks and soot per state."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    stone = g.mix(g.noise(8.0, 4.0), lin((0.50, 0.49, 0.46)), lin((0.62, 0.60, 0.56)))
    bands = g.math('ADD', cc.band(g, z, 0.62, 0.86), cc.band(g, z, 1.38, 1.62))
    white = g.mix(grain, lin((0.90, 0.90, 0.88)), lin((0.96, 0.96, 0.94)))
    tower = g.mix(g.clamp01(bands), white, lin((0.78, 0.14, 0.12)))
    tower = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -3.0, 0.9)), 0.4), tower, lin((0.55, 0.52, 0.46)))  # salt-grimed low
    metal = g.mix(grain, lin((0.16, 0.17, 0.18)), lin((0.24, 0.25, 0.27)))
    glass = g.mix(grain, lin((0.62, 0.78, 0.84)), lin((0.78, 0.88, 0.92)))
    glow = lin((1.0, 0.92, 0.62))
    red = g.mix(grain, lin((0.70, 0.12, 0.10)), lin((0.80, 0.18, 0.14)))
    plaster = g.mix(g.noise(6.0, 3.0), lin((0.88, 0.86, 0.82)), lin((0.94, 0.92, 0.88)))
    tile = g.mix(grain, lin((0.62, 0.22, 0.14)), lin((0.72, 0.30, 0.18)))
    dark = lin((0.05, 0.05, 0.06))
    albedo = cc.paint(g, mask, (stone, tower, metal, glass, glow, red, plaster, tile, dark))
    if variant not in ("intact", "occupied"):
        albedo = g.mix(g.math('MULTIPLY', mask[1], cc.cracks(g, 5.0)), albedo, lin((0.20, 0.19, 0.18)))
        amount = {"damaged": -3.0, "damaged_occupied": -3.0, "abandoned": -2.0, "ruins": -1.2}[variant]
        scorch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, amount))
        albedo = g.mix(g.math('MULTIPLY', scorch, 0.8), albedo, lin((0.10, 0.09, 0.08)))
    rough = cc.weighted(g, mask, (0.9, 0.7, 0.45, 0.1, 0.3, 0.6, 0.85, 0.8, 0.9))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.8, 0.0, 0.0, 0.2, 0.0, 0.0, 0.0))
    g.finish(albedo, g.bump(g.math('ADD', grain, g.noise(8.0, 4.0)), 0.08, 0.01), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "gallery", "lantern"))
    rnd = ic.seed(variant_id)
    body = Parts()
    if variant == "ruins":
        _ruins(body, rnd)
        _house(body, variant)
        return cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat", variant))
    _tower(body)
    _house(body, variant)
    if variant in ("occupied", "damaged_occupied"):
        _occupied(body)
    if variant in ("damaged", "damaged_occupied"):
        cc.block(body, tuple(AT + Vector((-0.5, -0.45, 0.06))), (0.22, 0.18, 0.12), (0.2, 0.1, 0.5), STONE)  # a chunk off the plinth
    gallery, lantern = Parts(), Parts()
    _gallery(gallery)
    _lantern(lantern, variant)
    if variant == "abandoned":
        # The tower leans, and all it carries; the house stands where it is.
        _lean([gallery, lantern], 0.12, lambda co: True)
        _lean([body], 0.12, lambda co: (Vector((co.x, co.y)) - AT.xy).length < BASE_R + 0.05)
    return cc.finish(variant_id, [(body, "body"), (gallery, "gallery"), (lantern, "lantern")], 0.0, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
