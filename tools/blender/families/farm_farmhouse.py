"""Farmhouse, element 08 of the temperate set (docs/temperate_elements/08_farmhouse): a two-storey wooden house with a veranda.

The map building neutral_farmhouse (game/data/neutral/civilian.yaml): 2x2, 700 hp, three soldiers go in. Footprint 2x2 cells, origin at
the centre of the footprint on the ground, the front (the veranda, the door) to -Y. Made a storey to a cell, as the houses of the map
(the game squashes it upright, game/data/visuals/models.yaml): two storeys of boards to the eaves at 1.85, the ridge at 2.6, the
chimney to 2.9. The parts:
    house     two storeys of pale yellow clapboard, the back three fifths of the footprint, white corner boards; windows in white
              frames with blue shutters on every side, a small attic window in the left gable, a red door in a white frame
    veranda   a deck of grey planks across the whole front, white posts and railings, steps down in the middle; its lean-to roof over
              it is the owner's band (team_mask), seen from above once the game squashes the house
    roof      a gable roof of dark green shingles, its ridge along the front; a fieldstone chimney up the right gable
    base      the concrete foundation under the house and the veranda, a step before it
No emblems, no text.
Nodes: "body" (the walls, windows, door, veranda and chimney, and the barrels, sandbags or rubble of a state), "roof", "team_mask" (the
veranda's roof), "base"; "smoke_anchor" at the chimney's top while the house is whole (smoke from the chimney, appearance.md 5); the
burning one also fire_1..3. One GLB per state (each state one file, so one paint: a whole house that turned another colour as soldiers
went in would show):
    farmhouse                    whole
    farmhouse_occupied           whole, three soldiers inside: a barrel out of each upper front window, sandbags along the veranda
    farmhouse_damaged            damaged: two windows in three knocked out, shutters torn off or hanging, the veranda's right post
                                 snapped and its roof sagging there, a run of railing gone, planks on the ground, soot
    farmhouse_damaged_occupied   the same, with the barrels and the sandbags
    farmhouse_abandoned          burning, its soldiers out: the left part of the roof fallen into the house, the left gable gone,
                                 charred rafters, every window out, the veranda's roof down at one end, charred; fire_1..3
    farmhouse_ruins              burnt down: charred stumps of the walls on the foundation, the stone chimney standing, a bit of the
                                 veranda's deck, roof boards and beams fallen
Budget 8000 triangles, texture 1024 (docs/temperate_elements/08_farmhouse/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.farm_farmhouse as farmhouse
    print(farmhouse.run_all())
"""

from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "farm"
SIZE = 1024
VARIANTS = {
    "farmhouse": "intact",
    "farmhouse_occupied": "occupied",
    "farmhouse_damaged": "damaged",
    "farmhouse_damaged_occupied": "damaged_occupied",
    "farmhouse_abandoned": "abandoned",
    "farmhouse_ruins": "ruins",
}

# The farm's row in the file: rts_common has no row for it yet, so it is added here while the script runs.
ROW = "15. Ферма"
SLOT0 = 0


def _register():
    if ROW not in rc.ROWS:
        rc.ROWS = rc.ROWS + (ROW,)
    row = rc.ROWS.index(ROW)
    for k, variant_id in enumerate(VARIANTS):
        rc.LAYOUT.setdefault(variant_id, (row, SLOT0 + k))


_register()

SIDING, TRIM, ROOF, SHUTTER, GLASS, WOOD, STONE, TEAM, DARK, DOOR, SAND, CONCRETE = (cc.part(i) for i in range(12))
PARTS = 12

FOUND_H = 0.12
HX = 0.80  # the side walls stand at -HX and HX
YF, YB = -0.30, 0.86  # the front and the back wall
YM = (YF + YB) / 2  # under the ridge
WALL_T = 0.08
FLOOR2 = 1.0
EAVE, RIDGE = 1.85, 2.6
SLOPE = (RIDGE - EAVE) / (YM - YF)
ROOF_T = 0.06
OVER = 0.12  # the roof's overhang at the eaves
GABLE_OVER = 0.08  # and at the gables
BROKEN_TOP = 1.72  # the top of the walls of the burning one's left part, the roof fallen there
BROKEN_X = -0.15  # the roof stands right of this on the burning one

GROUND, UPPER = (0.45, 0.85), (1.25, 1.65)
WIN_H = 0.12  # half the window's width

DECK_TOP = FOUND_H + 0.05
VY = -0.88  # the veranda's front edge
POST_Y = -0.84
POSTS = (-0.76, -0.27, 0.27, 0.76)
RAIL_TOP = DECK_TOP + 0.33
VROOF_WALL, VROOF_EDGE = 1.12, 0.99  # the veranda's roof at the wall and at its front edge
VROOF_Y = -0.94

CHIMNEY = (0.80, 0.98, 0.17, 0.39)  # x0, x1, y0, y1
CHIMNEY_TOP = 2.9

# The windows: (wall, along, storey). along is x on the front and the back, y on the sides.
WINDOWS = ([("front", u, GROUND) for u in (-0.52, 0.52)] + [("front", u, UPPER) for u in (-0.5, 0.0, 0.5)] +
           [("back", u, z) for u in (-0.45, 0.45) for z in (GROUND, UPPER)] +
           [(side, u, z) for side in ("left", "right") for u in (-0.07, 0.62) for z in (GROUND, UPPER)])
WALLS = cc.Facade(HX, -YF, YB)
FIRE_POINTS = ((-0.45, YM, 1.6), (0.5, YF - 0.03, 1.45), (-0.35, -0.6, 0.55))
SMOKE = (0.89, 0.28, CHIMNEY_TOP + 0.05)


# --- the house ----------------------------------------------------------------------

def _gable(p, x0, x1):
    p.profile_x([(YF, EAVE), (YB, EAVE), (YM, RIDGE)], x0, x1, SIDING)


def _walls(p, burning):
    """The four walls as slabs of boards, the gables over the sides, corner boards; the floor between the storeys and the ceiling,
    seen only where the burning one is broken open."""
    top_left = BROKEN_TOP if burning else EAVE
    split = -0.35
    for y0, y1 in ((YF, YF + WALL_T), (YB - WALL_T, YB)):  # the front and the back, in two pieces where the left part may fall
        p.box((-HX, y0, FOUND_H), (split, y1, top_left), SIDING)
        p.box((split, y0, FOUND_H), (HX, y1, EAVE), SIDING)
    p.box((-HX, YF + WALL_T, FOUND_H), (-HX + WALL_T, YB - WALL_T, top_left), SIDING)
    p.box((HX - WALL_T, YF + WALL_T, FOUND_H), (HX, YB - WALL_T, EAVE), SIDING)
    if not burning:
        _gable(p, -HX, -HX + WALL_T)
    _gable(p, HX - WALL_T, HX)
    for x, y in ((-HX, YF), (HX, YF), (HX, YB), (-HX, YB)):
        top = top_left if x < 0 else EAVE
        p.box((x - 0.025, y - 0.025, FOUND_H), (x + 0.025, y + 0.025, top), TRIM)
    p.box((-HX + WALL_T, YF + WALL_T, FLOOR2 - 0.04), (HX - WALL_T, YB - WALL_T, FLOOR2), WOOD)
    p.box((BROKEN_X if burning else -HX + WALL_T, YF + WALL_T, EAVE - 0.04), (HX - WALL_T, YB - WALL_T, EAVE), WOOD)


def _shutter(p, wall, a, b, z0, z1, hanging, inner):
    """A shutter of slats beside a window; hanging: torn from its upper hinge, swung askew about its lower one at inner (a or b)."""
    before = set(p.bm.verts)
    WALLS.box(p, wall, a, b, z0, z1, -0.005, 0.02, SHUTTER)
    if hanging:
        pivot = WALLS.point(wall, inner, z0, 0.02)
        axis = WALLS.out(wall)
        m = Matrix.Translation(pivot) @ Matrix.Rotation(0.5 if inner == b else -0.5, 4, axis) @ Matrix.Translation(-pivot)
        for v in p.bm.verts:
            if v not in before:
                v.co = m @ v.co + axis * 0.012


def _windows(p, variant, burning):
    damaged = variant in ("damaged", "damaged_occupied")
    h = WIN_H
    for i, (wall, u, storey) in enumerate(WINDOWS):
        broken = burning or damaged and i % 3 != 1
        z0, z1 = storey
        WALLS.window(p, wall, u, z0, z1, h, GLASS, TRIM, bars=(1, 1), broken=DARK if broken else None)
        for k, (a, b) in enumerate(((u - h - 0.1, u - h - 0.035), (u + h + 0.035, u + h + 0.1))):
            if burning and (i + k) % 2 == 0 or damaged and (i + k) % 4 == 0:
                continue  # torn off
            _shutter(p, wall, a, b, z0, z1, damaged and (i + k) % 4 == 2, b if k == 0 else a)
    if not burning:  # the attic window in the left gable
        WALLS.window(p, "left", YM, 1.98, 2.2, 0.08, GLASS, TRIM, bars=(0, 0), broken=DARK if damaged else None)
    # The door in a white frame.
    WALLS.box(p, "front", -0.13, 0.13, DECK_TOP, 0.92, -0.01, 0.015, DARK if burning else DOOR)
    WALLS.box(p, "front", -0.16, 0.16, 0.92, 0.96, -0.01, 0.03, TRIM)
    for a in (-0.16, 0.13):
        WALLS.box(p, "front", a, a + 0.03, DECK_TOP, 0.92, -0.01, 0.03, TRIM)
    if not burning:
        WALLS.box(p, "front", 0.07, 0.1, 0.5, 0.56, 0.0, 0.035, TRIM)  # the handle


def _chimney(p, top=CHIMNEY_TOP):
    """Fieldstone up the right gable: a wide foot, the stack narrowing over the eaves, a cap."""
    x0, x1, y0, y1 = CHIMNEY
    p.box((x0, y0 - 0.02, 0.0), (x1 + 0.01, y1 + 0.02, 1.2), STONE)
    p.loft([[Vector((x0, y0 - 0.02, 1.2)), Vector((x1 + 0.01, y0 - 0.02, 1.2)), Vector((x1 + 0.01, y1 + 0.02, 1.2)), Vector((x0, y1 + 0.02, 1.2))],
            [Vector((x0, y0, 1.42)), Vector((x1 - 0.02, y0, 1.42)), Vector((x1 - 0.02, y1, 1.42)), Vector((x0, y1, 1.42))]], STONE)
    p.box((x0, y0, 1.42), (x1 - 0.02, y1, top), STONE)
    if top >= CHIMNEY_TOP:
        p.box((x0 - 0.015, y0 - 0.015, top), (x1 - 0.005, y1 + 0.015, top + 0.05), CONCRETE)


# --- the veranda ----------------------------------------------------------------------

def _vroof_z(y):
    return VROOF_WALL + (VROOF_EDGE - VROOF_WALL) * (y - YF) / (VROOF_Y - YF)


def _veranda(p, variant):
    """The deck, the steps, the posts and the railing; damaged, the right post snapped and a run of railing gone; burning, more."""
    damaged = variant in ("damaged", "damaged_occupied")
    burning = variant == "abandoned"
    p.box((-HX - 0.04, VY, FOUND_H), (HX + 0.04, YF, DECK_TOP), WOOD)
    p.box((-0.24, -0.98, 0.0), (0.24, VY, 0.09), WOOD)  # the steps
    for x in POSTS:
        top = _vroof_z(POST_Y) - 0.005
        if (damaged or burning) and x > 0.7:
            top = 0.55  # snapped
        if burning and x < -0.7:
            continue  # burnt through, gone
        p.box((x - 0.028, POST_Y - 0.028, DECK_TOP), (x + 0.028, POST_Y + 0.028, top), TRIM)
    runs = [((-0.76, POST_Y), (-0.27, POST_Y)), ((0.27, POST_Y), (0.76, POST_Y)), ((-0.76, POST_Y), (-0.76, YF)), ((0.76, POST_Y), (0.76, YF))]
    for k, (a, b) in enumerate(runs):
        if (damaged or burning) and k == 1 or burning and k == 2:
            continue
        a, b = Vector((a[0], a[1], 0.0)), Vector((b[0], b[1], 0.0))
        for z in (DECK_TOP + 0.08, RAIL_TOP):
            p.bar(a + Vector((0, 0, z)), b + Vector((0, 0, z)), 0.016, TRIM)
        n = max(2, int((b - a).length / 0.1))
        for i in range(1, n):
            c = a.lerp(b, i / n)
            p.box((c.x - 0.01, c.y - 0.01, DECK_TOP + 0.08), (c.x + 0.01, c.y + 0.01, RAIL_TOP), TRIM)
    if damaged or burning:
        # The railing knocked off and the planks torn up, lying on the deck and hanging over its edge.
        for c, size, turn in (((0.55, -0.68, DECK_TOP + 0.02), (0.45, 0.04, 0.03), (0.0, 0.0, 0.25)),
                              ((0.4, -0.52, DECK_TOP + 0.012), (0.3, 0.06, 0.02), (0.0, 0.05, -0.4)),
                              ((0.62, -0.9, 0.1), (0.4, 0.04, 0.03), (0.35, 0.0, 0.1))):
            cc.block(p, c, size, turn, TRIM if size[1] < 0.05 else WOOD)


def _vroof(p, variant):
    """The veranda's lean-to roof: the owner's band. Damaged, it sags over the snapped post; burning, its left end is down on the deck."""
    before = set(p.bm.verts)
    p.profile_x([(YF, VROOF_WALL), (VROOF_Y, VROOF_EDGE), (VROOF_Y, VROOF_EDGE + 0.04), (YF, VROOF_WALL + 0.04)], -HX - 0.04, HX + 0.04, TEAM)
    new = [v for v in p.bm.verts if v not in before]
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        for v in new:
            if v.co.x > 0.2:
                t = (v.co.x - 0.2) / (HX - 0.16)
                v.co.z -= 0.42 * t * (v.co.y - YF) / (VROOF_Y - YF)
    if variant == "abandoned":
        for v in new:
            if v.co.x < -0.2:
                t = (-0.2 - v.co.x) / (HX - 0.16)
                v.co.z -= 0.75 * t * (v.co.y - YF) / (VROOF_Y - YF)


# --- the roof -------------------------------------------------------------------------

def _roof(p, burning):
    """Two slopes of shingles on the gables, overhanging the walls; on the burning one only the part right of BROKEN_X stands."""
    x0, x1 = (BROKEN_X if burning else -HX - GABLE_OVER), HX + GABLE_OVER
    for sign in (-1, 1):
        eave_y = YM + sign * (YM - YF + OVER)
        eave_z = EAVE - OVER * SLOPE
        p.profile_x([(eave_y, eave_z), (YM, RIDGE), (YM, RIDGE + ROOF_T), (eave_y, eave_z + ROOF_T)], x0, x1, ROOF)
    p.box((x0, YM - 0.03, RIDGE + ROOF_T - 0.02), (x1, YM + 0.03, RIDGE + ROOF_T + 0.03), ROOF)  # the ridge cap


def _fallen(p):
    """The burning one's left part: the roof slid down into the house, charred rafters over the gap, a beam across."""
    cc.block(p, (-0.47, YM - 0.18, 1.48), (0.62, 0.7, 0.05), (0.15, -0.55, 0.12), ROOF)
    cc.block(p, (-0.5, YM + 0.32, 1.42), (0.55, 0.5, 0.05), (-0.25, 0.5, -0.1), ROOF)
    for k, x in enumerate((-0.25, -0.48, -0.7)):
        drop = 0.18 * k
        p.bar((x, YF + 0.04, BROKEN_TOP - 0.02), (x + 0.03, YM, RIDGE - 0.25 - drop), 0.024, DARK)
        if k != 1:
            p.bar((x, YB - 0.04, BROKEN_TOP - 0.02), (x - 0.03, YM, RIDGE - 0.3 - drop), 0.024, DARK)
    p.bar((BROKEN_X + 0.02, YM, RIDGE - 0.02), (-0.62, YM + 0.05, 1.55), 0.03, DARK)  # the ridge beam, broken and down


# --- the soldiers ----------------------------------------------------------------------

def _occupied(p):
    """Three soldiers: a barrel out of each upper front window; sandbags along the veranda, the steps left open."""
    for u in (-0.5, 0.0, 0.5):
        p.bar((u, YF + 0.02, 1.42), (u + 0.04, YF - 0.36, 1.38), 0.016, DARK)
    for z, shift in ((DECK_TOP, 0.0), (DECK_TOP + 0.08, 0.1)):
        for x in cc.steps(-0.66 + shift, -0.36, 0.2) + cc.steps(0.36, 0.66 - shift, 0.2):
            cc.bag(p, (x, POST_Y + 0.1, z), 0.0, SAND, length=0.2, width=0.13, height=0.08)


# --- the ruins -------------------------------------------------------------------------

def _ruins(p):
    rnd = ic.seed("farmhouse_ruins")
    walls = [((-HX, YF), (HX, YF)), ((HX, YF), (HX, YB)), ((HX, YB), (-HX, YB)), ((-HX, YB), (-HX, YF))]
    ic.stumps(p, rnd, walls, FOUND_H, 0.12, 0.6, SIDING, step=0.24, gap=0.2, thick=WALL_T)
    for x, y, h in ((-HX, YF, 0.7), (HX, YF, 0.45), (HX, YB, 0.85), (-HX, YB, 0.55)):  # the corner posts, charred
        p.box((x - 0.03, y - 0.03, FOUND_H), (x + 0.03, y + 0.03, FOUND_H + h), DARK)
    _chimney(p, 2.35)  # the stone stands, broken off at the top
    p.box((-HX - 0.04, VY, FOUND_H), (0.1, YF, DECK_TOP), WOOD)  # what is left of the veranda's deck
    p.box((-0.24, -0.98, 0.0), (0.24, VY, 0.09), WOOD)
    for x, h in ((-0.76, 0.42), (-0.27, 0.25)):
        p.box((x - 0.028, POST_Y - 0.028, DECK_TOP), (x + 0.028, POST_Y + 0.028, DECK_TOP + h), DARK)
    for c, size, turn, part in ((((-0.25, 0.3, 0.3), (0.9, 0.6, 0.05), (0.28, -0.12, 0.15), ROOF)),
                                (((0.35, 0.45, 0.25), (0.7, 0.5, 0.05), (-0.2, 0.2, -0.3), ROOF)),
                                (((0.3, -0.62, 0.22), (0.7, 0.3, 0.04), (0.15, 0.25, 0.1), TEAM)),
                                (((-0.5, -0.05, 0.2), (0.5, 0.06, 0.05), (0.0, 0.2, 0.5), DARK)),
                                (((0.1, 0.0, 0.3), (0.06, 0.9, 0.06), (0.3, 0.0, 0.3), DARK)),
                                (((0.5, 0.15, 0.18), (0.6, 0.06, 0.06), (0.0, -0.15, 1.1), DARK))):
        cc.block(p, c, size, turn, part)
    ic.rubble(p, rnd, -0.6, 0.6, YF + 0.1, YB - 0.1, FOUND_H, 10, 0.12, DARK)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Pale yellow clapboard, white trim, dark green shingles, blue shutters with slats, glass, grey planks, fieldstone, a light team
    zone, a red door, sandbags, concrete; soot per state, charred when burnt down."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 4.0)
    # Clapboard: boards across the wall, each shadowed at its lower edge where it laps over the one below.
    lap = g.math('FRACT', g.math('MULTIPLY', z, 13.0))
    lap_shade = g.clamp01(g.math('MULTIPLY_ADD', lap, 3.0, 0.15))
    paint = g.mix(g.noise(6.0, 3.0), lin((0.90, 0.82, 0.55)), lin((0.95, 0.88, 0.62)))
    siding = g.mix(lap_shade, lin((0.52, 0.46, 0.32)), paint)
    trim = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.95, 0.95, 0.93)))
    # Shingles: rows down the slope, staggered, a dark line under each row, a few darker shingles.
    rows = g.math('MULTIPLY', z, 24.0)
    row_line = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', rows), -9.0, 1.6))
    along = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', x, 9.0), g.math('MULTIPLY', g.math('FLOOR', rows), 0.5)))
    col_line = g.clamp01(g.math('MULTIPLY_ADD', along, -30.0, 1.6))
    shingle = g.mix(g.noise(9.0, 2.0), lin((0.17, 0.30, 0.22)), lin((0.26, 0.40, 0.29)))
    roof = g.mix(g.math('MAXIMUM', row_line, col_line), shingle, lin((0.08, 0.14, 0.10)))
    slats = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 40.0)), -6.0, 1.4))
    shutter = g.mix(slats, g.mix(grain, lin((0.22, 0.40, 0.66)), lin((0.28, 0.47, 0.72))), lin((0.12, 0.22, 0.38)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, 2.5, -1.4)), lin((0.18, 0.25, 0.30)), lin((0.42, 0.54, 0.62)))
    planks = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 14.0)), -14.0, 1.6))
    wood = g.mix(planks, g.mix(g.noise(4.0, 5.0), lin((0.48, 0.44, 0.38)), lin((0.58, 0.53, 0.46))), lin((0.22, 0.20, 0.18)))
    stone, mortar = ic.brick(g, coord, (0.50, 0.47, 0.42), (0.66, 0.62, 0.55), (0.32, 0.30, 0.28), course=9.0, length=6.0)
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.05)), lin((0.10, 0.09, 0.08)))
    door = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 16.0)), -14.0, 1.6)),
                 g.mix(grain, lin((0.52, 0.16, 0.12)), lin((0.60, 0.20, 0.15))), lin((0.28, 0.08, 0.06)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.50, 0.45, 0.32)), lin((0.62, 0.56, 0.40)))
    concrete = g.mix(grain, lin((0.56, 0.55, 0.52)), lin((0.64, 0.63, 0.60)))
    albedo = cc.paint(g, mask, (siding, trim, roof, shutter, glass, wood, stone, team, dark, door, sand, concrete))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        # Soot over the windows: the bands just above their tops, and patches; the burning one sooted all over.
        soot = g.math('MAXIMUM', cc.band(g, z, GROUND[1] + 0.03, GROUND[1] + 0.2), cc.band(g, z, UPPER[1] + 0.03, UPPER[1] + 0.2))
        soot = g.math('MULTIPLY', soot, g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 2.0), 3.0, -0.8)))
        soot = g.math('MAXIMUM', soot, ic.soot(g, 0.85 if variant == "abandoned" else -0.4))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.08, 0.07, 0.06)))
    if variant == "ruins":
        char = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.2, 1.0)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 2.0), 3.0, -0.4)))
        albedo = g.mix(g.math('MULTIPLY', char, 0.85), g.mix(0.45, albedo, lin((0.14, 0.12, 0.10))), lin((0.07, 0.06, 0.05)))
        albedo = g.mix(g.math('MULTIPLY', g.math('ADD', mask[0], mask[1]), 0.55), albedo, lin((0.12, 0.10, 0.09)))  # boards burnt black
    rough = cc.weighted(g, mask, (0.75, 0.55, 0.85, 0.6, 0.12, 0.85, 0.9, 0.5, 0.9, 0.55, 0.95, 0.9))
    normal = g.bump(g.math('ADD', g.math('MULTIPLY', g.math('ADD', g.math('MULTIPLY', lap, mask[0]), mortar), -0.6),
                           g.math('MULTIPLY', g.noise(60.0, 3.0), 0.3)), 0.05, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, 0.0))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _base(p):
    p.box((-HX - 0.06, VY - 0.04, 0.0), (HX + 0.06, YB + 0.05, FOUND_H), CONCRETE)


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "roof", "team_mask", "base"))
    burning = variant == "abandoned"
    body, base = Parts(), Parts()
    parts = [(body, "body")]
    if variant == "ruins":
        _ruins(body)
    else:
        _walls(body, burning)
        _windows(body, variant, burning)
        _chimney(body)
        _veranda(body, variant)
        if variant in ("occupied", "damaged_occupied"):
            _occupied(body)
        if burning:
            _fallen(body)
        roof, team = Parts(), Parts()
        _roof(roof, burning)
        _vroof(team, variant)
        parts += [(roof, "roof"), (team, "team_mask")]
    _base(base)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, FOUND_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (smoke_anchor while whole,
    fire_1..3 when burning)."""
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    fire = FIRE_POINTS if variant == "abandoned" else ()
    anchors = (("smoke_anchor", SMOKE),) if variant in ("intact", "occupied") else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, anchors, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
