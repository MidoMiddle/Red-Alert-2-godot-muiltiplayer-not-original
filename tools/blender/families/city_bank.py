"""City bank: element 23 of the city set (docs/city_elements/23_bank), its states (appearance.md, 4; the states of the civilian
buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry daylight, no snow.

Footprint 3x2 cells (x 3, y 2), origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the
buildings of the map (the game squashes it upright, game/data/visuals/models.yaml): two tall storeys of stone to the cornice at 3.9,
the pediment's peak at 4.8 (prompts.md, 1: about 5). The parts:
    body       a block of light grey stone in courses, a dark grey plinth, a band between the storeys and a dark cornice with dark
               streaks under it; tall ground-floor windows in dark frames; a heavy bronze door in the middle of the front
    garrison windows   the tall windows of the upper storey, the ones the soldiers fire from (prompts.md, 4): a node of their own
    columns    a portico of four smooth white columns on bases, with capitals, carrying an entablature
    pediment   the dark grey triangle over the portico, its raking cornice standing out
    team mask  the pediment's tympanum, the flat triangle in it (a band over the pediment, as the prompt has it, is too thin to read
               once the game squashes the bank upright)
    steps      broad stone steps up to the portico
    plaza      the paving round it
No emblems, no text.
Nodes: "body", "garrison_windows", "columns", "pediment", "team_mask", "steps", "base". The abandoned one also has fire_1..3. One GLB
per state:
    city_bank                     whole, empty
    city_bank_occupied            whole, four soldiers inside: a barrel out of the four upper front windows, sandbags on the steps
    city_bank_damaged             damaged: cracks in the stone, windows knocked out, a piece broken off a column
    city_bank_damaged_occupied    damaged, occupied: the same, with the barrels and the sandbags
    city_bank_abandoned           critically damaged, its soldiers out: the pediment and the entablature fallen, two columns down,
                                  the upper front broken open on the right, the floor and the inner walls showing
    city_bank_ruins               destroyed: a heap of stone, the stumps of the columns, drums of them about the plaza
Budget 10000 triangles, texture 2048 (docs/city_elements/23_bank/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_bank as cbk
    print(cbk.run("city_bank"))   # or cbk.run_all()
"""

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 2048
VARIANTS = {
    "city_bank": "intact",
    "city_bank_occupied": "occupied",
    "city_bank_damaged": "damaged",
    "city_bank_damaged_occupied": "damaged_occupied",
    "city_bank_abandoned": "abandoned",
    "city_bank_ruins": "ruins",
}

STONE, DARK_STONE, COLUMN, GLASS, FRAME, BRONZE, DARK, PAVE, SAND, TEAM, FLOOR = (cc.part(i) for i in range(11))
PARTS = 11

X0 = 1.3
YF, YB = -0.23, 0.92  # the front wall and the back wall: the portico and the steps before it, all inside the footprint
WALLS = cc.Facade(X0, -YF, YB)
PAVE_H = 0.05
PLINTH = 0.3
STOREY_2 = 2.05  # the floor of the upper storey
CORNICE = 3.9
PORTICO_Y = -0.68  # the columns' line
COLS_X = (-0.66, -0.22, 0.22, 0.66)
ENTAB = (3.0, 3.3)  # the entablature over the columns
PED_TOP = 4.8
FRONT_WIN = (-1.04, 1.04)
UPPER_FRONT = (-1.04, -0.36, 0.36, 1.04)
WIN_HALF = 0.13
GROUND_WIN = (0.55, 1.75)
UPPER_WIN = (2.35, 3.55)
FIRE_POINTS = ((0.7, YF - 0.02, 2.9), (0.0, PORTICO_Y, 0.5), (-0.6, 0.3, CORNICE))
THROUGH = 0.3  # how far the abandoned one is broken in from the right front corner, in x past the middle


# --- the block -----------------------------------------------------------------------------

def _block(p, abandoned):
    """Whole: one solid, its top the roof. Abandoned: solid to the upper floor, then hollow walls with the right front part broken
    open, the floor and an inner wall showing."""
    if not abandoned:
        faces = cc.made(p, lambda: p.box((-X0, YF, PAVE_H), (X0, YB, CORNICE), STONE))
        cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > CORNICE - 0.01], DARK_STONE)
    else:
        faces = cc.made(p, lambda: p.box((-X0, YF, PAVE_H), (X0, YB, STOREY_2), STONE))
        cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > STOREY_2 - 0.01], FLOOR)
        t = 0.14
        p.box((-X0, YB - t, STOREY_2), (X0, YB, CORNICE), STONE)
        p.box((-X0, YF + t, STOREY_2), (-X0 + t, YB - t, CORNICE), STONE)
        p.box((X0 - t, 0.25, STOREY_2), (X0, YB - t, CORNICE - 0.6), STONE)
        p.box((-X0, YF, STOREY_2), (THROUGH, YF + t, CORNICE), STONE)
        p.box((THROUGH, YF, STOREY_2), (0.75, YF + t, 2.9), STONE)
        p.box((0.75, YF, STOREY_2), (X0, YF + t, 2.4), STONE)
        p.box((0.0, YF + t, STOREY_2), (0.12, YB - t, 3.2), STONE)  # an inner wall, showing through the gap
        p.box((-X0 + t, YF + t, CORNICE - 0.1), (THROUGH, YB - t, CORNICE), DARK_STONE)  # the roof that is left
    # The plinth, the band between the storeys, the cornice.
    for wall, (a, b) in (("front", (-X0 - 0.04, X0 + 0.04)), ("back", (-X0 - 0.04, X0 + 0.04)), ("left", (YF, YB)), ("right", (YF, YB))):
        WALLS.box(p, wall, a, b, PAVE_H, PLINTH, -0.01, 0.04, DARK_STONE)
        WALLS.box(p, wall, a, b, STOREY_2 - 0.05, STOREY_2 + 0.05, -0.01, 0.035, DARK_STONE)
        if not abandoned or wall in ("back", "left"):
            WALLS.box(p, wall, a, b, CORNICE - 0.2, CORNICE, -0.01, 0.07, DARK_STONE)
    # The door under the portico.
    WALLS.box(p, "front", -0.22, 0.22, PLINTH, 1.75, -0.01, 0.02, DARK if abandoned else BRONZE)
    WALLS.box(p, "front", -0.27, 0.27, 1.75, 1.85, -0.01, 0.04, DARK_STONE)


def _ground_windows(p, variant):
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall, us in (("front", FRONT_WIN), ("back", (-0.8, -0.27, 0.27, 0.8)), ("left", (0.15, 0.62)), ("right", (0.15, 0.62))):
        for u in us:
            n += 1
            broken = DARK if abandoned or damaged and n % 3 == 0 else None
            WALLS.window(p, wall, u, GROUND_WIN[0], GROUND_WIN[1], WIN_HALF, GLASS, FRAME, bars=(1, 2), broken=broken)


def _upper_windows(p, variant):
    """The upper storey's windows: the garrison's. Damaged: some out; abandoned: out, and none where the front is broken."""
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall, us in (("front", UPPER_FRONT), ("back", (-0.8, -0.27, 0.27, 0.8)), ("left", (0.15, 0.62)), ("right", (0.15, 0.62))):
        for u in us:
            if abandoned and (wall == "front" and u > THROUGH - 0.1 or wall == "right"):
                continue
            n += 1
            broken = DARK if abandoned or damaged and n % 2 == 0 else None
            WALLS.window(p, wall, u, UPPER_WIN[0], UPPER_WIN[1], WIN_HALF, GLASS, FRAME, bars=(1, 2), broken=broken)


# --- the portico ---------------------------------------------------------------------

def _column(p, x, top=ENTAB[0], chipped=False):
    p.box((x - 0.12, PORTICO_Y - 0.12, PLINTH), (x + 0.12, PORTICO_Y + 0.12, PLINTH + 0.1), COLUMN)  # the base
    shaft_top = top - 0.12 if top >= ENTAB[0] else top
    p.turned((x, PORTICO_Y, PLINTH + 0.1), 'Z', [(0.0, 0.095), (shaft_top - PLINTH - 0.1, 0.085)], COLUMN, sides=12)
    if top >= ENTAB[0]:
        p.box((x - 0.13, PORTICO_Y - 0.13, top - 0.12), (x + 0.13, PORTICO_Y + 0.13, top), COLUMN)  # the capital
    if chipped:  # a piece broken off its shaft, on the steps
        cc.block(p, (x + 0.12, PORTICO_Y - 0.2, PLINTH + 0.06), (0.16, 0.1, 0.1), (0.2, 0.4, 0.6), COLUMN)


def _columns(p, variant):
    if variant == "abandoned":
        for k, x in enumerate(COLS_X):
            if k == 2:  # down along the steps
                cc.block(p, (x - 0.25, -0.82, 0.34), (1.3, 0.2, 0.2), (0.0, 0.0, 0.06), COLUMN)
            elif k == 3:  # down on the floor of the portico, behind the ones standing
                cc.block(p, (0.45, YF - 0.2, PLINTH + 0.1), (1.3, 0.2, 0.2), (0.0, 0.0, -0.05), COLUMN)
            else:
                _column(p, x)
        return
    for k, x in enumerate(COLS_X):
        _column(p, x, chipped=variant in ("damaged", "damaged_occupied") and k == 1)


def _pediment(p, variant):
    """The entablature over the columns and the triangle on it; abandoned: fallen, in pieces on the steps."""
    if variant == "abandoned":
        for c, s, t in (((-0.5, PORTICO_Y - 0.1, PAVE_H + 0.3), (0.9, 0.3, 0.3), (0.2, 0.1, 0.1)),
                        ((0.45, PORTICO_Y + 0.02, PLINTH + 0.15), (0.7, 0.28, 0.25), (0.1, 0.3, -0.15)),
                        ((-0.05, PORTICO_Y - 0.15, PAVE_H + 0.2), (0.6, 0.25, 0.2), (0.3, 0.2, 0.2))):
            cc.block(p, c, s, t, DARK_STONE)
        p.box((-0.92, PORTICO_Y - 0.16, ENTAB[0]), (-0.4, YF + 0.02, ENTAB[1]), DARK_STONE)  # a stump of the entablature, left
        return
    p.box((-0.92, PORTICO_Y - 0.16, ENTAB[0]), (0.92, YF + 0.02, ENTAB[1]), DARK_STONE)
    # The pediment: a triangular prism, its raking cornice standing out over its face.
    y0, y1 = PORTICO_Y - 0.12, YF + 0.02
    p.loft([[Vector((-0.92, y, ENTAB[1])), Vector((0.92, y, ENTAB[1])), Vector((0.0, y, PED_TOP))] for y in (y0, y1)], DARK_STONE)
    for sign in (-1, 1):
        a, b = Vector((sign * 0.98, y0 - 0.05, ENTAB[1] - 0.02)), Vector((0.0, y0 - 0.05, PED_TOP + 0.06))
        p.bar(a, b, 0.05, DARK_STONE)


def _tympanum(p):
    """The flat triangle inside the pediment: the team zone."""
    y = PORTICO_Y - 0.125
    p.loft([[Vector((-0.74, yy, ENTAB[1] + 0.07)), Vector((0.74, yy, ENTAB[1] + 0.07)), Vector((0.0, yy, PED_TOP - 0.16))]
            for yy in (y - 0.012, y)], TEAM)


def _steps(p):
    for k, (y0, z) in enumerate(((-0.98, 0.1), (-0.91, 0.17), (-0.84, 0.24), (-0.77, PLINTH))):
        p.box((-0.95 + 0.04 * k, y0, PAVE_H), (0.95 - 0.04 * k, YF, z), STONE)


def _barrels(p):
    """Four rifle barrels, one for each soldier inside, out of the four upper front windows."""
    z = (UPPER_WIN[0] + UPPER_WIN[1]) / 2
    for u in UPPER_FRONT:
        a = WALLS.point("front", u, z, -0.02)
        p.bar(a, a + Vector((u * 0.08, -0.45, -0.05)), 0.017, DARK)


def _sandbags(p):
    for z, shift in ((0.24, 0.0), (0.32, 0.12)):
        for t in cc.steps(0.35 + shift, 0.85, 0.24):
            for x in (-t, t):
                cc.bag(p, (x, -0.86, z), 0.0, SAND, length=0.22, width=0.14, height=0.08)


def _ruins(p):
    for x in COLS_X:
        _column(p, x, top=PLINTH + 0.3 + 0.25 * (abs(x) < 0.3))
    for lo, hi in (((-X0, 0.4, PAVE_H), (-0.2, YB, 1.1)), ((-X0, YF, PAVE_H), (-X0 + 0.15, 0.4, 0.8)), ((0.6, YB - 0.15, PAVE_H), (X0, YB, 0.6))):
        p.box(lo, hi, STONE)
    for c, s, t in (((0.0, 0.2, 0.3), (2.0, 1.1, 0.55), (0.05, 0.1, 0.1)), ((0.7, -0.1, 0.25), (0.8, 0.7, 0.4), (0.2, 0.1, 0.7)),
                    ((-0.6, -0.2, 0.22), (0.7, 0.6, 0.35), (0.1, 0.2, 1.2)), ((0.0, -0.4, 0.4), (1.0, 0.5, 0.08), (0.3, -0.2, 0.3))):
        cc.block(p, c, s, t, STONE)
    for c, t in (((1.1, -0.8, 0.15), 0.5), ((-1.15, -0.75, 0.15), 1.3), ((1.25, -0.45, 0.12), 2.2)):  # drums of the columns
        p.turned((c[0], c[1], PAVE_H), 'Z', [(0.0, 0.09), (0.18, 0.09)], COLUMN, sides=10)
    cc.block(p, (-0.4, -0.55, PLINTH + 0.1), (0.8, 0.3, 0.2), (0.1, 0.1, 0.25), DARK_STONE)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Light grey stone in courses, dark grey stone with streaks under the cornice, smooth whitish columns, dark-framed blue glass, a
    bronze door, paving, sandbags, a light team zone, a floor of boards; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    course = g.math('MULTIPLY', z, 7.0)
    row = g.math('FRACT', course)
    col = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 2.2), g.math('MULTIPLY', g.math('FLOOR', course), 0.5)))
    joint = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', row, -40.0, 2.0)), g.clamp01(g.math('MULTIPLY_ADD', col, -60.0, 2.0)))
    stone = g.mix(joint, g.mix(g.noise(4.0, 3.0), lin((0.70, 0.69, 0.66)), lin((0.78, 0.77, 0.74))), lin((0.56, 0.55, 0.52)))
    streak = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 3.0, -3.0 * (CORNICE - 0.6))),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 9.0)), 3.0, -1.6)))
    stone = g.mix(g.math('MULTIPLY', streak, 0.5), stone, lin((0.40, 0.39, 0.37)))  # dark streaks under the cornice
    dark_stone = g.mix(grain, lin((0.36, 0.36, 0.37)), lin((0.44, 0.44, 0.45)))
    column = g.mix(grain, lin((0.86, 0.85, 0.82)), lin((0.92, 0.91, 0.88)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.25)), lin((0.12, 0.18, 0.28)), lin((0.30, 0.40, 0.52)))
    frame = g.mix(grain, lin((0.12, 0.12, 0.13)), lin((0.18, 0.18, 0.19)))
    bronze = g.mix(grain, lin((0.40, 0.28, 0.14)), lin((0.50, 0.36, 0.18)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.10, 0.10, 0.11)))
    slabs = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 3.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 3.0)), -30.0, 1.5)))
    pave = g.mix(slabs, g.mix(grain, lin((0.62, 0.60, 0.56)), lin((0.70, 0.68, 0.64))), lin((0.45, 0.44, 0.42)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    floor = g.mix(g.noise(3.0, 6.0), lin((0.36, 0.26, 0.16)), lin((0.44, 0.32, 0.20)))
    albedo = cc.paint(g, mask, (stone, dark_stone, column, glass, frame, bronze, dark, pave, sand, team, floor))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        walls = g.math('ADD', g.math('ADD', mask[0], mask[1]), mask[2])
        albedo = g.mix(g.math('MULTIPLY', walls, cc.cracks(g, 6.0)), albedo, lin((0.22, 0.21, 0.20)))
        soot = g.math('MULTIPLY', walls, g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.8, 2.0), 6.0, -3.3 if variant != "abandoned" else -2.8)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.0, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.7), albedo, lin((0.12, 0.11, 0.10)))
    rough = cc.weighted(g, mask, (0.8, 0.8, 0.5, 0.1, 0.5, 0.4, 0.9, 0.85, 0.95, 0.45, 0.8))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.2, 0.7, 0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', joint, -1.0), g.noise(60.0, 3.0)), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    names = ("body", "garrison_windows", "columns", "pediment", "team_mask", "steps", "base")
    cc.clear(variant_id, names)
    variant = VARIANTS[variant_id]
    body, steps = Parts(), Parts()
    _steps(steps)
    parts = [(body, "body")]
    if variant == "ruins":
        _ruins(body)
    else:
        _block(body, variant == "abandoned")
        _ground_windows(body, variant)
        windows, columns, pediment = Parts(), Parts(), Parts()
        _upper_windows(windows, variant)
        _columns(columns, variant)
        _pediment(pediment, variant)
        parts += [(windows, "garrison_windows"), (columns, "columns"), (pediment, "pediment")]
        if variant != "abandoned":
            team = Parts()
            _tympanum(team)
            parts.append((team, "team_mask"))
        if variant in ("occupied", "damaged_occupied"):
            _barrels(body)
            _sandbags(steps)
    parts.append((steps, "steps"))
    base = Parts()
    base.box((-1.48, -0.98, 0.0), (1.48, 0.98, PAVE_H), PAVE)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, garrison_windows, columns, pediment, team_mask, steps and
    base (fire_1..3 when abandoned; no pediment's tympanum then, it fell)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
