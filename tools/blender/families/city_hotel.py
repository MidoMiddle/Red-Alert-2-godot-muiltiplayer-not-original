"""City hotel: element 24 of the city set (docs/city_elements/24_hotel), its states (appearance.md, 4; the states of the civilian
buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry daylight, no snow.

Footprint 3x2 cells (x 3, y 2), origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the
buildings of the map (the game squashes it upright, game/data/visuals/models.yaml): a tall ground floor and eight storeys to the roof
at 7.9, the parapet to 8.1 (prompts.md, 1: about 8). The parts:
    body       light yellow brick with a white band at each floor, a dark plinth; the ground floor's tall windows and a glass door
    garrison windows   the rows of windows of the upper storeys, dark blue glass in white frames: the ones the soldiers fire from
               (prompts.md, 4), a node of their own
    balconies  a narrow balcony along the front and the back of every upper storey: a grey concrete slab and a metal railing
    awning     a dark red awning over the entrance on two posts
    sign       a plain board over the awning, no text
    parapet    a band round the edge of the roof: the team zone, seen from far away, as the office's and the skyscraper's (a band over the
               entrance, as the prompt has it, is too thin to read once the game squashes the hotel upright)
    plaza      the paving round it
No emblems, no text.
Nodes: "body", "garrison_windows", "balconies", "awning", "sign", "team_mask" (the parapet), "base". The abandoned one also has
fire_1..3. One GLB per state:
    city_hotel                     whole, empty
    city_hotel_occupied            whole, six soldiers inside: a barrel out of six upper front windows, sandbags on the roof and at
                                   the door
    city_hotel_damaged             damaged: glass knocked out, lengths of the railings broken off, cracks and soot
    city_hotel_damaged_occupied    damaged, occupied: the same, with the barrels and the sandbags
    city_hotel_abandoned           critically damaged, its soldiers out: the front right corner gone from the fifth storey up, its
                                   balconies fallen, the floors showing, all the glass out
    city_hotel_ruins               destroyed: a shell of the lower storeys at the back, heaps, balcony slabs in the street
Budget 12000 triangles, texture 2048 (docs/city_elements/24_hotel/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_hotel as cht
    print(cht.run("city_hotel"))   # or cht.run_all()
"""

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 2048
VARIANTS = {
    "city_hotel": "intact",
    "city_hotel_occupied": "occupied",
    "city_hotel_damaged": "damaged",
    "city_hotel_damaged_occupied": "damaged_occupied",
    "city_hotel_abandoned": "abandoned",
    "city_hotel_ruins": "ruins",
}

BRICK, WHITE, GLASS, CONCRETE, METAL, AWN, SIGN, DARK, PAVE, SAND, TEAM, ROOF = (cc.part(i) for i in range(12))
PARTS = 12

X0, Y0 = 1.32, 0.62  # the balconies stand out to 0.8, the awning to the footprint's edge at 0.98
WALLS = cc.Facade(X0, Y0)
PAVE_H = 0.05
GROUND_TOP = 1.15
STOREY = 0.85
STOREYS = 8
TOP = GROUND_TOP + STOREY * STOREYS  # 7.95
PARAPET = (TOP - 0.1, TOP + 0.2, 0.04)
COLUMNS = {"front": (-1.0, -0.5, 0.0, 0.5, 1.0), "back": (-1.0, -0.5, 0.0, 0.5, 1.0), "left": (-0.4, 0.4), "right": (-0.4, 0.4)}
WIN = (0.25, 0.7, 0.14)  # bottom and top over the storey's floor, half the width
BALCONY_OUT = 0.18
CUT_X, CUT_Z = 0.25, GROUND_TOP + STOREY * 4  # abandoned: the front right corner gone over this
FIRE_POINTS = ((0.7, -0.3, CUT_Z + 0.05), (-0.5, -Y0 - 0.02, CUT_Z + 0.4), (-0.6, 0.3, TOP))


def _floor(i):
    """The floor of upper storey i, 0 the first over the ground floor."""
    return GROUND_TOP + STOREY * i


def _gone(wall, u, z):
    """Whether the abandoned one has lost the wall at u, z: its front right corner over CUT_Z."""
    return z > CUT_Z and (wall == "front" and u > CUT_X or wall == "right" and u < 0.0)


# --- the block ------------------------------------------------------------------------

def _block(p, abandoned):
    """Whole: one solid, its top the roof. Abandoned: a solid up to CUT_Z and two solids over it that leave the front right corner
    open, its floors showing."""
    if not abandoned:
        faces = cc.made(p, lambda: p.box((-X0, -Y0, PAVE_H), (X0, Y0, TOP), BRICK))
        cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > TOP - 0.01], ROOF)
    else:
        faces = cc.made(p, lambda: p.box((-X0, -Y0, PAVE_H), (X0, Y0, CUT_Z), BRICK))
        cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > CUT_Z - 0.01], CONCRETE)
        for lo, hi in (((-X0, 0.0, CUT_Z), (X0, Y0, TOP)), ((-X0, -Y0, CUT_Z), (CUT_X, 0.0, TOP))):
            faces = cc.made(p, lambda: p.box(lo, hi, BRICK))
            cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > TOP - 0.01], ROOF)
        for i in range(5, STOREYS):  # the floors of the storeys over the cut, sticking out of the broken walls
            z = _floor(i)
            p.box((CUT_X, -0.6, z - 0.06), (X0 - 0.2, 0.0, z), CONCRETE)
    # The plinth and a white band at each floor.
    for wall, (a, b) in (("front", (-X0 - 0.02, X0 + 0.02)), ("back", (-X0 - 0.02, X0 + 0.02)), ("left", (-Y0, Y0)), ("right", (-Y0, Y0))):
        WALLS.box(p, wall, a, b, PAVE_H, 0.3, -0.01, 0.03, DARK)
        for i in range(STOREYS):
            z = _floor(i)
            if abandoned and wall in ("front", "right") and z > CUT_Z:
                cut = (a, CUT_X) if wall == "front" else (0.0, b)
                WALLS.box(p, wall, cut[0], cut[1], z - 0.03, z + 0.03, -0.01, 0.025, WHITE)
            else:
                WALLS.box(p, wall, a, b, z - 0.03, z + 0.03, -0.01, 0.025, WHITE)
    # The ground floor: tall windows and the glass door.
    for u in (-1.0, -0.55, 0.55, 1.0):
        WALLS.window(p, "front", u, 0.35, 0.95, 0.16, GLASS, WHITE, bars=(1, 1), broken=DARK if abandoned else None)
    WALLS.box(p, "front", -0.2, 0.2, PAVE_H, 0.95, -0.01, 0.012, DARK if abandoned else GLASS)
    for a in (-0.23, 0.2):
        WALLS.box(p, "front", a, a + 0.03, PAVE_H, 0.98, -0.01, 0.025, METAL)


def _windows(p, variant):
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall, us in COLUMNS.items():
        for i in range(STOREYS):
            z = _floor(i)
            for u in us:
                if abandoned and _gone(wall, u, z + WIN[1]):
                    continue
                n += 1
                broken = DARK if abandoned or damaged and (n * 7 + i) % 5 < 2 else None
                WALLS.window(p, wall, u, z + WIN[0], z + WIN[1], WIN[2], GLASS, WHITE, bars=(0, 0), broken=broken)


def _balconies(p, variant):
    """A narrow balcony along the front and the back of every upper storey; damaged, lengths of their railings gone; abandoned,
    none where the corner went, two slabs down in the street."""
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    for wall in ("front", "back"):
        for i in range(STOREYS):
            z = _floor(i)
            a, b = -X0 + 0.1, X0 - 0.1
            if abandoned and wall == "front" and z > CUT_Z:
                b = CUT_X - 0.05
            WALLS.box(p, wall, a, b, z - 0.02, z + 0.04, -0.01, BALCONY_OUT, CONCRETE)
            top = z + 0.32
            posts = cc.steps(a + 0.02, b - 0.02, 0.32)
            for k, t in enumerate(posts):
                if damaged and wall == "front" and (i + k) % 6 == 0:
                    continue
                WALLS.box(p, wall, t - 0.008, t + 0.008, z + 0.04, top, BALCONY_OUT - 0.02, BALCONY_OUT - 0.005, METAL)
            gaps = [(a, b)] if not (damaged and wall == "front" and i % 3 == 1) else [(a, -0.2), (0.4, b)]
            for ga, gb in gaps:
                WALLS.box(p, wall, ga, gb, top - 0.02, top + 0.005, BALCONY_OUT - 0.025, BALCONY_OUT, METAL)
    if abandoned:
        for c, s, t in (((0.8, -Y0 - 0.22, PAVE_H + 0.04), (1.0, 0.2, 0.06), (0.1, 0.2, 0.08)),
                        ((0.55, -Y0 - 0.12, PAVE_H + 0.1), (0.9, 0.2, 0.06), (0.4, -0.3, -0.1))):
            cc.block(p, c, s, t, CONCRETE)


def _awning(p):
    out = 0.36
    p.profile_x([(-Y0 - 0.01, 1.08), (-Y0 - out, 1.0), (-Y0 - out, 1.04), (-Y0 - 0.01, 1.12)], -0.4, 0.4, AWN)
    p.box((-0.4, -Y0 - out - 0.01, 0.92), (0.4, -Y0 - out + 0.005, 1.04), AWN)
    for x in (-0.36, 0.36):
        p.bar((x, -Y0 - out + 0.02, PAVE_H), (x, -Y0 - out + 0.02, 1.0), 0.012, METAL)


def _sign(p):
    WALLS.box(p, "front", -0.38, 0.38, 1.14, 1.32, -0.01, 0.04, SIGN)  # no text


def _parapet(p, abandoned):
    z0, z1, out = PARAPET
    x_end = CUT_X if abandoned else X0 + out
    y_start = 0.0 if abandoned else -Y0 - out
    p.box((-X0 - out, -Y0 - out, z0), (x_end, -Y0 + 0.08, z1), TEAM)
    p.box((-X0 - out, Y0 - 0.08, z0), (X0 + out, Y0 + out, z1), TEAM)
    p.box((-X0 - out, -Y0 + 0.08, z0), (-X0 + 0.08, Y0 - 0.08, z1), TEAM)
    p.box((X0 - 0.08, max(y_start, -Y0 + 0.08), z0), (X0 + out, Y0 - 0.08, z1), TEAM)


def _barrels(p):
    """Six rifle barrels, one for each soldier inside, out of the upper front windows."""
    for u, i in ((-1.0, 7), (0.0, 7), (1.0, 7), (-0.5, 6), (0.5, 6), (0.0, 5)):
        a = WALLS.point("front", u, _floor(i) + (WIN[0] + WIN[1]) / 2, -0.02)
        p.bar(a, a + Vector((u * 0.08, -0.45, -0.05)), 0.017, DARK)


def _sandbags(p):
    for t in cc.steps(-X0 + 0.25, X0 - 0.25, 0.27):
        cc.bag(p, (t, -Y0 + 0.22, TOP), 0.0, SAND)
        cc.bag(p, (t, Y0 - 0.22, TOP), 0.0, SAND)
    for z, shift in ((PAVE_H, 0.0), (PAVE_H + 0.08, 0.12)):
        for t in cc.steps(0.5 + shift, 1.1, 0.24):
            for x in (-t, t):
                cc.bag(p, (x, -Y0 - 0.12, z), 0.0, SAND, length=0.22, width=0.14, height=0.08)


def _ruins(p):
    for lo, hi in (((-X0, 0.2, PAVE_H), (0.2, Y0, 2.4)), ((-X0, -0.3, PAVE_H), (-X0 + 0.15, 0.2, 1.6)), ((0.2, Y0 - 0.15, PAVE_H), (X0, Y0, 1.1)),
                   ((X0 - 0.15, -0.2, PAVE_H), (X0, Y0 - 0.15, 0.6)), ((-X0 + 0.15, 0.2, GROUND_TOP - 0.06), (0.2, Y0 - 0.15, GROUND_TOP))):
        p.box(lo, hi, BRICK)
    for c, s, t in (((0.2, -0.2, 0.3), (1.8, 1.0, 0.5), (0.05, 0.1, 0.1)), ((0.8, 0.2, 0.25), (0.8, 0.7, 0.4), (0.2, 0.1, 0.7)),
                    ((-0.7, -0.4, 0.2), (0.6, 0.5, 0.3), (0.1, 0.2, 1.2))):
        cc.block(p, c, s, t, BRICK)
    for c, s, t in (((0.3, -Y0 - 0.2, PAVE_H + 0.05), (1.1, 0.2, 0.06), (0.1, 0.1, 0.08)), ((-0.6, -Y0 - 0.2, PAVE_H + 0.05), (0.9, 0.2, 0.06), (0.2, -0.1, -0.1)),
                    ((-0.2, -0.1, 0.62), (1.4, 0.8, 0.07), (0.3, -0.2, 0.3))):
        cc.block(p, c, s, t, CONCRETE)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Light yellow brick, white trim, dark blue glass, grey concrete, metal, a dark red awning, a muted sign, a light grey roof, paving,
    sandbags, a light team zone; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    course = g.math('MULTIPLY', z, 28.0)
    row = g.math('FRACT', course)
    col = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 8.0), g.math('MULTIPLY', g.math('FLOOR', course), 0.5)))
    mortar = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', row, -12.0, 2.0)), g.clamp01(g.math('MULTIPLY_ADD', col, -25.0, 2.0)))
    brick = g.mix(mortar, g.mix(g.noise(5.0, 2.0), lin((0.84, 0.76, 0.52)), lin((0.90, 0.82, 0.58))), lin((0.80, 0.78, 0.72)))
    white = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.12)), lin((0.10, 0.15, 0.26)), lin((0.30, 0.40, 0.54)))
    concrete = g.mix(grain, lin((0.54, 0.54, 0.53)), lin((0.62, 0.62, 0.61)))
    metal = g.mix(grain, lin((0.32, 0.33, 0.35)), lin((0.40, 0.41, 0.43)))
    awn = g.mix(grain, lin((0.46, 0.10, 0.10)), lin((0.54, 0.14, 0.13)))
    sign = g.mix(grain, lin((0.20, 0.24, 0.32)), lin((0.25, 0.29, 0.37)))
    dark = g.mix(grain, lin((0.07, 0.07, 0.08)), lin((0.12, 0.12, 0.13)))
    slabs = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 3.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 3.0)), -30.0, 1.5)))
    pave = g.mix(slabs, g.mix(grain, lin((0.62, 0.60, 0.56)), lin((0.70, 0.68, 0.64))), lin((0.45, 0.44, 0.42)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    roof = g.mix(g.noise(12.0, 4.0), lin((0.56, 0.56, 0.55)), lin((0.64, 0.64, 0.63)))
    albedo = cc.paint(g, mask, (brick, white, glass, concrete, metal, awn, sign, dark, pave, sand, team, roof))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        walls = g.math('ADD', mask[0], mask[1])
        albedo = g.mix(g.math('MULTIPLY', walls, cc.cracks(g, 6.0)), albedo, lin((0.24, 0.21, 0.16)))
        soot = g.math('MULTIPLY', g.math('ADD', walls, mask[3]),
                      g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.8, 2.0), 6.0, -3.3 if variant != "abandoned" else -2.8)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -0.8, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    rough = cc.weighted(g, mask, (0.85, 0.6, 0.1, 0.85, 0.45, 0.9, 0.6, 0.9, 0.85, 0.95, 0.45, 0.85))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.3, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', mortar, -1.0), g.noise(60.0, 3.0)), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "garrison_windows", "balconies", "awning", "sign", "team_mask", "base"))
    variant = VARIANTS[variant_id]
    abandoned = variant == "abandoned"
    body = Parts()
    parts = [(body, "body")]
    if variant == "ruins":
        _ruins(body)
    else:
        _block(body, abandoned)
        windows, balconies, awning, sign, team = Parts(), Parts(), Parts(), Parts(), Parts()
        _windows(windows, variant)
        _balconies(balconies, variant)
        _awning(awning)
        _sign(sign)
        _parapet(team, abandoned)
        parts += [(windows, "garrison_windows"), (balconies, "balconies"), (awning, "awning"), (sign, "sign"), (team, "team_mask")]
        if variant in ("occupied", "damaged_occupied"):
            _barrels(body)
            _sandbags(body)
    base = Parts()
    base.box((-1.48, -0.98, 0.0), (1.48, 0.98, PAVE_H), PAVE)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, garrison_windows, balconies, awning, sign, team_mask and
    base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
