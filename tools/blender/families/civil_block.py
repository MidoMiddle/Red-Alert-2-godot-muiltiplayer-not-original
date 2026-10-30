"""Civilian apartment block of the map: the winter block (docs/map_elements/11_civilian_buildings), its states (appearance.md, 5) and
the soldiers inside. Not the city's (families/city_apartment.py, drawn on the urban maps).

Footprint 3x3 cells, origin at the centre of the footprint on the ground, the front to -Y. Made upright as the city set (a storey to a
cell, the game squashes it, game/data/visuals/models.yaml): a raised ground storey over a plinth and five storeys of 1.1 to the roof at
5.96, the parapet to 6.26, the shafts and the aerials on the roof to about 6.9. The parts:
    block      red brick on a grey concrete plinth with cellar vents, a brick cornice; rows of the same windows in white frames, the
               glass dark with the light of rooms in some of it
    entrances  two, at the front left and right: a steel door under a concrete canopy with snow on it, steps; over each a column of
               small windows of the stair, between the storeys
    balconies  a column in the middle of the front and two on the back: concrete slabs, pale painted fronts, snow on the slabs
    roof       a flat roof under snow inside a parapet (the owner's colour: a band round the top, seen from far away), two brick vent
               shafts with snow on them, a hatch, three aerials
    yard       snow round the block, trodden paths from the entrances, drifts at the walls
No emblems, no text.
Nodes: "body" (the walls, windows, entrances, balconies, drifts, the roof's surface, and the barrels, sandbags or rubble of a state),
"roof" (the shafts, the hatch, the aerials, the roof's snow), "team_mask" (the parapet), "base" (the snow round the block). The abandoned
one also has fire_1..4. One GLB per state:
    building_apartment_block                    whole, empty
    building_apartment_block_occupied           whole, eight soldiers inside: a barrel out of eight front windows, sandbags at the
                                                foot and along the front of the roof
    building_apartment_block_damaged            damaged, empty: half the windows knocked out, soot over them, cracks, two balconies
                                                fallen to the foot
    building_apartment_block_damaged_occupied   damaged, occupied: the same, with the barrels and the sandbags
    building_apartment_block_abandoned          critically damaged, its soldiers out: the front right corner gone from the third storey
                                                up, the floors showing with bars out of their edges, a slab hanging, rubble in the snow
    building_apartment_block_ruins              destroyed: the back left corner standing two storeys, stumps of the walls, heaps of
                                                brick, slabs and bars under patches of snow
Budget 15000 triangles, texture 2048, the ruins 1024 (prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.civil_block as cb
    print(cb.run("building_apartment_block"))   # or cb.run_all()
"""

import math

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

SIZE, RUINS_SIZE = 2048, 1024
FOLDER = "map"  # game/assets/models/map/<id>/, assets_src/models/map/<id>/
VARIANTS = {
    "building_apartment_block": "intact",
    "building_apartment_block_occupied": "occupied",
    "building_apartment_block_damaged": "damaged",
    "building_apartment_block_damaged_occupied": "damaged_occupied",
    "building_apartment_block_abandoned": "abandoned",
    "building_apartment_block_ruins": "ruins",
}

(BRICK, PLINTH, GLASS, FRAME, PANEL, CONCRETE, DARK, DOOR_STEEL, METAL, SNOW, PATH, SAND, TEAM, INSIDE) = (cc.part(i) for i in range(14))
PARTS = 14

X0, Y0 = 1.25, 1.15  # the walls stand at x = ±X0, y = ±Y0
WALL_T = 0.15
FLOOR0 = 0.46  # the floor of the ground storey, over the plinth
STOREY = 1.1
STOREYS = 5
TOP = FLOOR0 + STOREY * STOREYS  # the top of the walls: the roof
PARAPET = (TOP - 0.02, TOP + 0.3, 0.05)  # bottom, top, how far it stands out of the walls
WIN = (0.13, 0.3, 0.9)  # half the width; bottom and top above the floor of the storey
STAIRS = (-0.7, 0.7)  # the entrances and the stairs over them
COLUMNS = {"front": (-1.05, -0.35, 0.0, 0.35, 1.05), "back": (-1.05, -0.5, 0.0, 0.5, 1.05), "left": (-0.55, 0.55), "right": (-0.55, 0.55)}
BALCONIES = {"front": (0.0,), "back": (-0.5, 0.5)}
CUT_X, CUT_Y = 0.2, -0.1  # abandoned: the corner beyond these is gone above CUT_Z
CUT_Z = FLOOR0 + 2 * STOREY
F = cc.Facade(X0, Y0)
FIRE_POINTS = ((0.7, -0.6, CUT_Z + 0.05), (-0.35, -Y0 - 0.02, FLOOR0 + 3 * STOREY + 0.6), (X0 - 0.1, 0.4, TOP),
               (-1.05, -Y0 - 0.02, FLOOR0 + 0.6))


def _floor(k):
    return FLOOR0 + k * STOREY


def _gone(wall, u, z):
    """Abandoned: whether a point of a wall fell with the front right corner."""
    if z < CUT_Z:
        return False
    return wall == "front" and u > CUT_X - 0.15 or wall == "right" and u < CUT_Y + 0.15


# --- the block ----------------------------------------------------------------------

def _walls(p, abandoned):
    """The four walls, the cornice and the roof's surface; abandoned, the front right corner cut off above CUT_Z, its stumps jagged,
    the floors and the walls of the rooms showing in the gap."""
    if not abandoned:
        p.box((-X0, -Y0, 0.0), (X0, Y0, TOP), BRICK)
    else:
        p.box((-X0, CUT_Y, 0.0), (X0, Y0, TOP), BRICK)
        p.box((-X0, -Y0, 0.0), (CUT_X, CUT_Y, TOP), BRICK)
        p.box((CUT_X, -Y0, 0.0), (X0, CUT_Y, CUT_Z), BRICK)
        rnd = ic.seed("block_walls")
        ic.stumps(p, rnd, [((CUT_X, -Y0 + 0.07), (X0, -Y0 + 0.07)), ((X0 - 0.07, -Y0), (X0 - 0.07, CUT_Y))], CUT_Z, 0.1, 0.7, BRICK,
                  step=0.2, gap=0.2, thick=WALL_T)
        # The rooms opened: their walls on the faces of the cut, the floor of the third storey, the ends of the floors above.
        p.box((CUT_X - 0.005, -Y0 + 0.02, CUT_Z), (CUT_X + 0.002, CUT_Y, TOP - 0.02), INSIDE)
        p.box((CUT_X, CUT_Y - 0.002, CUT_Z), (X0 - 0.02, CUT_Y + 0.005, TOP - 0.02), INSIDE)
        p.box((CUT_X, -Y0 + 0.02, CUT_Z - 0.01), (X0 - 0.02, CUT_Y, CUT_Z + 0.02), CONCRETE)
        for k in (3, 4):
            p.box((CUT_X - 0.002, -Y0 + 0.02, _floor(k) - 0.1), (CUT_X + 0.12, CUT_Y + 0.12, _floor(k)), CONCRETE)
            p.box((CUT_X, CUT_Y - 0.002, _floor(k) - 0.1), (X0 - 0.02, CUT_Y + 0.12, _floor(k)), CONCRETE)
    # The plinth, standing a little out, and the brick cornice under the parapet.
    for wall, lo, hi in (("front", -X0 - 0.02, X0 + 0.02), ("back", -X0 - 0.02, X0 + 0.02), ("left", -Y0, Y0), ("right", -Y0, Y0)):
        F.box(p, wall, lo, hi, 0.0, FLOOR0 - 0.04, -0.01, 0.02, PLINTH)
        F.box(p, wall, lo, hi, FLOOR0 - 0.06, FLOOR0 - 0.02, -0.01, 0.035, CONCRETE)
        cut = abandoned and wall in ("front", "right")
        a, b = (lo, CUT_X) if cut and wall == "front" else (CUT_Y, hi) if cut else (lo, hi)
        F.box(p, wall, a, b, TOP - 0.2, TOP - 0.12, -0.01, 0.03, BRICK)
        for u in cc.steps(lo + 0.25, hi - 0.25, 0.5):  # the cellar vents
            F.box(p, wall, u - 0.07, u + 0.07, 0.12, 0.24, -0.01, 0.004, DARK)


def _window(p, wall, u, z0, z1, half, broken):
    F.window(p, wall, u, z0, z1, half, GLASS, FRAME, bars=(1, 1), broken=DARK if broken else None)


def _openings(p, variant):
    """The windows of the storeys and of the stairs, and the entrances. Damaged: half the windows knocked out; abandoned: nearly all."""
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall, columns in COLUMNS.items():
        for k in range(STOREYS):
            for u in columns:
                z0, z1 = _floor(k) + WIN[1], _floor(k) + WIN[2]
                n += 1
                if abandoned and _gone(wall, u, z0):
                    continue
                broken = abandoned and n % 5 != 0 or damaged and n % 2 == 0
                _window(p, wall, u, z0, z1, WIN[0], broken)
    for u in STAIRS:
        for j in range(1, STOREYS):
            zc = _floor(j) - 0.2
            if abandoned and _gone("front", u, zc):
                continue
            _window(p, "front", u, zc - 0.22, zc + 0.22, 0.1, abandoned or damaged and j % 2 == 1)
        _entrance(p, u, abandoned)


def _entrance(p, u, abandoned):
    """A steel door under a concrete canopy on two brackets, snow on the canopy, two steps up to it."""
    F.box(p, "front", u - 0.16, u + 0.16, 0.14, 0.98, -0.01, 0.012, DARK if abandoned else DOOR_STEEL)
    F.box(p, "front", u - 0.19, u + 0.19, 0.98, 1.02, -0.01, 0.03, CONCRETE)
    for a in (u - 0.19, u + 0.16):
        F.box(p, "front", a, a + 0.03, 0.14, 0.98, -0.01, 0.03, CONCRETE)
    F.box(p, "front", u - 0.3, u + 0.3, 1.1, 1.16, 0.0, 0.42, CONCRETE)  # the canopy
    F.box(p, "front", u - 0.28, u + 0.28, 1.16, 1.21, 0.02, 0.4, SNOW)
    for a in (u - 0.28, u + 0.24):  # the brackets
        F.box(p, "front", a, a + 0.04, 0.9, 1.1, 0.0, 0.06, CONCRETE)
    for h, d in ((0.14, 0.32), (0.07, 0.44)):
        F.box(p, "front", u - 0.24, u + 0.24, 0.0, h, 0.0, d, CONCRETE)


def _balcony(p, wall, u, z):
    """A concrete slab with a pale painted front and sides, snow on the slab inside them, a door with a window to it."""
    out = 0.32
    F.box(p, wall, u - 0.28, u + 0.28, z - 0.05, z + 0.01, 0.0, out, CONCRETE)
    F.box(p, wall, u - 0.28, u + 0.28, z + 0.01, z + 0.4, out - 0.025, out, PANEL)
    for a in (u - 0.28, u + 0.255):
        F.box(p, wall, a, a + 0.025, z + 0.01, z + 0.4, 0.0, out - 0.025, PANEL)
    F.box(p, wall, u - 0.25, u + 0.25, z + 0.01, z + 0.05, 0.0, out - 0.03, SNOW)
    F.box(p, wall, u - 0.26, u + 0.26, z + 0.4, z + 0.43, out - 0.04, out + 0.005, SNOW)  # on the rail


def _balconies(p, variant):
    fallen = {("front", 0.0, 2), ("back", -0.5, 3)} if variant in ("damaged", "damaged_occupied") else set()
    for wall, columns in BALCONIES.items():
        for u in columns:
            for k in range(1, STOREYS):
                if (wall, u, k) in fallen or variant == "abandoned" and _gone(wall, u + 0.28, _floor(k)):
                    continue
                _balcony(p, wall, u, _floor(k))


def _drifts(p, abandoned):
    """Snow drifted against the plinth, none in front of the entrances."""
    d = 0.16
    runs = [("front", -X0, -0.95), ("front", -0.45, 0.45), ("front", 0.95, X0), ("back", -X0, X0), ("left", -Y0, Y0), ("right", -Y0, Y0)]
    for wall, a, b in runs:
        out = F.out(wall)
        along = Vector((1, 0, 0)) if wall in ("front", "back") else Vector((0, 1, 0))
        foot = [F.point(wall, a, 0.05, 0.02), F.point(wall, a, 0.05, d + 0.02), F.point(wall, a, 0.24, 0.02)]
        p.loft([[q + along * t for q in foot] for t in (0.0, b - a)], SNOW)


def _barrels(p):
    """Eight rifle barrels, one for each soldier inside: out of the front windows of the second and third storeys."""
    ic.barrels(p, [F.point("front", u, _floor(k) + 0.62) for k in (1, 2) for u in (-1.05, -0.35, 0.35, 1.05)], (0, -1, 0), DARK)


def _sandbags(p):
    """Sandbags at the foot of the front, the entrances open, and along the front of the roof inside the parapet."""
    xs = [x for x in cc.steps(-X0 + 0.1, X0 - 0.1, 0.24) if all(abs(x - s) > 0.3 for s in STAIRS)]
    ic.sandbags(p, xs, -Y0 - 0.22, 0.05, SAND)
    ic.sandbags(p, list(cc.steps(-X0 + 0.15, X0 - 0.3, 0.24)), -Y0 + 0.14, TOP + 0.08, SAND, rows=1)


# --- the roof -------------------------------------------------------------------------

def _roof_snow(p, abandoned):
    """Snow over the roof inside the parapet, heaped against it; abandoned, not over the corner that is gone."""
    t = 0.08
    if abandoned:
        p.box((-X0 + 0.05, CUT_Y, TOP), (X0 - 0.05, Y0 - 0.05, TOP + t), SNOW)
        p.box((-X0 + 0.05, -Y0 + 0.05, TOP), (CUT_X, CUT_Y, TOP + t), SNOW)
    else:
        p.box((-X0 + 0.05, -Y0 + 0.05, TOP), (X0 - 0.05, Y0 - 0.05, TOP + t), SNOW)
        for sx in (-1, 1):  # heaped against the parapet on the sides
            p.profile_x([(-Y0 + 0.05, TOP + t), (Y0 - 0.05, TOP + t), (Y0 - 0.05, TOP + t + 0.08), (-Y0 + 0.05, TOP + t + 0.08)],
                        sx * (X0 - 0.05), sx * (X0 - 0.2), SNOW)


def _roof_kit(p, abandoned):
    """Two brick vent shafts with caps and snow, a hatch, three aerials; the right shaft is gone with the corner when abandoned."""
    shafts = [(-0.7, 0.4)] + ([] if abandoned else [(0.7, 0.4)])
    for x, y in shafts:
        p.box((x - 0.16, y - 0.12, TOP), (x + 0.16, y + 0.12, TOP + 0.55), BRICK)
        p.box((x - 0.19, y - 0.15, TOP + 0.55), (x + 0.19, y + 0.15, TOP + 0.6), CONCRETE)
        p.box((x - 0.17, y - 0.13, TOP + 0.6), (x + 0.17, y + 0.13, TOP + 0.64), SNOW)
        for d in (-1, 1):  # the vents, dark, in its long sides
            p.box((x - 0.1, y + d * 0.12 - 0.005, TOP + 0.3), (x + 0.1, y + d * 0.12 + 0.005, TOP + 0.45), DARK)
    p.box((-0.15, -0.35, TOP), (0.15, -0.05, TOP + 0.2), METAL)  # the hatch
    p.box((-0.16, -0.36, TOP + 0.2), (0.16, -0.04, TOP + 0.24), SNOW)
    for x, y in ((-1.0, 0.85), (0.2, 0.9), (-0.35, 0.2)):
        p.bar((x, y, TOP + 0.05), (x, y, TOP + 0.8), 0.012, METAL)
        p.bar((x - 0.18, y, TOP + 0.7), (x + 0.18, y, TOP + 0.7), 0.008, METAL)
        p.bar((x - 0.12, y, TOP + 0.58), (x + 0.12, y, TOP + 0.58), 0.008, METAL)


def _parapet(p, abandoned):
    """The band round the top: the team zone, wide enough to read when the game squashes the block, seen from above too."""
    z0, z1, d = PARAPET
    t = 0.1
    if not abandoned:
        p.box((-X0 - d, -Y0 - d, z0), (X0 + d, -Y0 + t, z1), TEAM)
        p.box((-X0 - d, Y0 - t, z0), (X0 + d, Y0 + d, z1), TEAM)
        p.box((X0 - t, -Y0 + t, z0), (X0 + d, Y0 - t, z1), TEAM)
    else:
        p.box((-X0 - d, -Y0 - d, z0), (CUT_X, -Y0 + t, z1), TEAM)
        p.box((-X0 - d, Y0 - t, z0), (X0 + d, Y0 + d, z1), TEAM)
        p.box((X0 - t, CUT_Y, z0), (X0 + d, Y0 - t, z1), TEAM)
    p.box((-X0 - d, -Y0 + t, z0), (-X0 + t, Y0 - t, z1), TEAM)


SLAB_ABANDONED = (((0.7, -0.55, CUT_Z + 0.35), (0.9, 0.75, 0.1), (0.55, -0.3, 0.3)),)  # a floor hanging into the gap
BARS_ABANDONED = (((CUT_X + 0.12, -0.4, _floor(3) - 0.05), (CUT_X + 0.35, -0.5, _floor(3) - 0.3)),
                  ((CUT_X + 0.12, -0.9, _floor(4) - 0.05), (CUT_X + 0.4, -1.0, _floor(4) - 0.2)),
                  ((0.6, CUT_Y + 0.12, _floor(3) - 0.05), (0.7, CUT_Y - 0.15, _floor(3) - 0.35)),
                  ((1.0, CUT_Y + 0.12, _floor(4) - 0.05), (1.05, CUT_Y - 0.2, _floor(4) - 0.25)))
FALLEN_BALCONIES = (((-0.1, -Y0 - 0.55, 0.08), (0.56, 0.32, 0.06), (0.2, 0.1, 0.4)), ((-0.4, Y0 + 0.5, 0.08), (0.56, 0.32, 0.06), (-0.15, 0.2, -0.3)))


def _abandoned_rubble(p):
    rnd = ic.seed("block_abandoned")
    ic.rubble(p, rnd, CUT_X, X0 + 0.25, -Y0 - 0.35, CUT_Y, CUT_Z + 0.02, 10, 0.18, BRICK, height=0.12)
    ic.rubble(p, rnd, 0.1, X0 + 0.3, -Y0 - 0.38, -Y0 - 0.05, 0.05, 16, 0.2, BRICK, height=0.15)
    ic.rubble(p, rnd, X0 + 0.04, X0 + 0.3, -Y0, 0.0, 0.05, 8, 0.18, BRICK, height=0.12)
    for centre, size, turn in SLAB_ABANDONED + (((0.9, -Y0 - 0.25, 0.25), (0.8, 0.5, 0.08), (0.4, 0.2, 0.3)),):
        cc.block(p, centre, size, turn, CONCRETE)
    for a, b in BARS_ABANDONED:
        p.bar(a, b, 0.012, METAL)


# --- the ruins ----------------------------------------------------------------------

def _ruins(p):
    rnd = ic.seed("building_apartment_block_ruins")
    p.box((-X0, -Y0, 0.0), (X0, Y0, FLOOR0 - 0.04), PLINTH)
    p.box((-X0 + WALL_T, -Y0 + WALL_T, FLOOR0 - 0.06), (X0 - WALL_T, Y0 - WALL_T, FLOOR0 - 0.02), CONCRETE)
    # The back left corner, two storeys, its top jagged; a floor left on it.
    top = _floor(2)
    p.box((-X0, Y0 - WALL_T, FLOOR0 - 0.04), (-0.15, Y0, top), BRICK)
    p.box((-X0, 0.1, FLOOR0 - 0.04), (-X0 + WALL_T, Y0 - WALL_T, top), BRICK)
    ic.stumps(p, rnd, [((-X0, Y0 - 0.07), (-0.15, Y0 - 0.07)), ((-X0 + 0.07, 0.1), (-X0 + 0.07, Y0))], top, 0.1, 0.6, BRICK, thick=WALL_T)
    p.box((-X0 + WALL_T, 0.2, _floor(1) - 0.1), (-0.3, Y0 - WALL_T, _floor(1)), CONCRETE)
    for u in (-1.05, -0.5):
        for k in (0, 1):
            _window(p, "back", u, _floor(k) + WIN[1], _floor(k) + WIN[2], WIN[0], True)
    _window(p, "left", 0.55, _floor(1) + WIN[1], _floor(1) + WIN[2], WIN[0], True)
    ic.stumps(p, rnd, [((-X0, -Y0 + 0.07), (X0, -Y0 + 0.07)), ((X0 - 0.07, -Y0), (X0 - 0.07, Y0)), ((-0.15, Y0 - 0.07), (X0, Y0 - 0.07)),
                       ((-X0 + 0.07, -Y0), (-X0 + 0.07, 0.1))], FLOOR0 - 0.04, 0.1, 0.9, BRICK, step=0.25, gap=0.3, thick=WALL_T)
    ic.rubble(p, rnd, -0.9, 1.0, -0.9, 0.7, FLOOR0, 26, 0.3, BRICK, height=0.45)
    ic.rubble(p, rnd, -X0 - 0.2, X0 + 0.2, -Y0 - 0.35, -Y0 - 0.05, 0.05, 12, 0.2, BRICK, height=0.1)
    for centre, size, turn in (((0.3, -0.1, 0.95), (1.6, 0.9, 0.1), (0.25, -0.2, 0.2)), ((-0.2, 0.3, 1.2), (1.2, 0.8, 0.1), (-0.35, 0.15, 0.6)),
                               ((0.6, 0.55, 0.8), (1.0, 0.7, 0.1), (0.1, 0.4, 1.4))):
        cc.block(p, centre, size, turn, CONCRETE)
    for a, b in (((-0.2, 0.4, _floor(1)), (0.1, 0.3, _floor(1) + 0.3)), ((0.3, -0.5, 1.0), (0.6, -0.4, 1.4)), ((0.8, 0.3, 0.9), (1.0, 0.1, 1.3)),
                 ((-0.6, -0.4, 0.9), (-0.3, -0.6, 1.25))):
        p.bar(a, b, 0.012, METAL)
    for centre, size, turn in (((0.3, -0.1, 1.02), (0.8, 0.5, 0.04), (0.25, -0.2, 0.2)), ((-0.25, 0.3, 1.28), (0.6, 0.4, 0.04), (-0.35, 0.15, 0.6)),
                               ((0.6, 0.6, 0.9), (0.5, 0.35, 0.04), (0.1, 0.4, 1.4)), ((-0.7, -0.5, 0.75), (0.5, 0.4, 0.05), (0.1, 0.1, 0.3))):
        cc.block(p, centre, size, turn, SNOW)  # snow fallen on the heaps


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Red brick in courses, a grey concrete plinth, dark glass with the light of rooms in some of it, white frames, pale balcony
    fronts, concrete, a brown steel door, metal, snow, trodden paths, sandbags, a light team zone, the plaster of the rooms; per state,
    cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    brick, joints = ic.brick(g, coord, (0.50, 0.23, 0.16), (0.60, 0.29, 0.20), (0.62, 0.58, 0.52), course=14.0, length=9.0)
    plinth = g.mix(grain, lin((0.40, 0.40, 0.40)), lin((0.48, 0.48, 0.47)))
    lit = g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.5, 1.0), 8.0, -4.6))  # some rooms have their light on
    glass = g.mix(lit, g.mix(g.math('MULTIPLY', z, 0.12), lin((0.08, 0.10, 0.14)), lin((0.18, 0.23, 0.30))), lin((0.75, 0.55, 0.28)))
    frame = g.mix(grain, lin((0.84, 0.84, 0.82)), lin((0.92, 0.92, 0.90)))
    panel = g.mix(grain, lin((0.62, 0.68, 0.66)), lin((0.70, 0.76, 0.74)))
    concrete = g.mix(grain, lin((0.52, 0.51, 0.49)), lin((0.60, 0.59, 0.57)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.05)), lin((0.10, 0.09, 0.09)))
    door = g.mix(grain, lin((0.25, 0.15, 0.10)), lin((0.32, 0.20, 0.13)))
    metal = g.mix(grain, lin((0.28, 0.29, 0.31)), lin((0.38, 0.39, 0.41)))
    snow = g.mix(g.noise(9.0, 3.0), lin((0.80, 0.84, 0.90)), lin((0.93, 0.95, 0.97)))
    path = g.mix(g.noise(14.0, 4.0), lin((0.52, 0.52, 0.53)), lin((0.66, 0.66, 0.68)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    inside = g.mix(grain, lin((0.58, 0.55, 0.48)), lin((0.66, 0.62, 0.54)))

    albedo = cc.paint(g, mask, (brick, plinth, glass, frame, panel, concrete, dark, door, metal, snow, path, sand, team, inside))
    not_snow = g.math('SUBTRACT', 1.0, mask[9])
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        crack = g.math('MULTIPLY', mask[0], cc.cracks(g, 5.0))
        albedo = g.mix(crack, albedo, lin((0.14, 0.10, 0.08)))
        # Soot over the windows: the band over each storey's windows.
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('SUBTRACT', z, FLOOR0), 1.0 / STOREY)),
                                                                -12.0, 11.6)), ic.soot(g, 0.6, 3.0))
        if variant == "abandoned":
            soot = g.math('MAXIMUM', soot, ic.soot(g, 0.4))
        albedo = g.mix(g.math('MULTIPLY', g.math('MULTIPLY', soot, not_snow), 0.75), albedo, lin((0.08, 0.07, 0.07)))
    if variant == "ruins":
        burnt = g.math('MULTIPLY', ic.soot(g, 0.4), not_snow)
        albedo = g.mix(g.math('MULTIPLY', burnt, 0.7), albedo, lin((0.07, 0.06, 0.06)))

    rough = cc.weighted(g, mask, (0.9, 0.9, 0.15, 0.5, 0.7, 0.9, 0.9, 0.5, 0.4, 0.75, 0.9, 0.95, 0.5, 0.9))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.2, 0.5, 0.6, 0.0, 0.0, 0.0, 0.0, 0.0))
    height = g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', joints, -1.0), g.noise(60.0, 3.0))
    g.finish(albedo, g.bump(height, 0.05, 0.006), g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _base(p):
    p.box((-1.48, -1.48, 0.0), (1.48, 1.48, 0.04), SNOW)  # the snow round the block
    for u in STAIRS:  # trodden from the entrances to the edge
        p.box((u - 0.2, -1.48, 0.04), (u + 0.2, -Y0 - 0.44, 0.045), PATH)


def build(variant_id):
    ic.clear(variant_id, ("body", "roof", "team_mask", "base"))
    variant = VARIANTS[variant_id]
    abandoned = variant == "abandoned"
    body = Parts()
    parts = [(body, "body")]
    if variant == "ruins":
        _ruins(body)
    else:
        _walls(body, abandoned)
        _openings(body, variant)
        _balconies(body, variant)
        _drifts(body, abandoned)
        if variant in ("occupied", "damaged_occupied"):
            _barrels(body)
            _sandbags(body)
        if variant in ("damaged", "damaged_occupied"):
            for centre, size, turn in FALLEN_BALCONIES:
                cc.block(body, centre, size, turn, CONCRETE)
            ic.rubble(body, ic.seed(variant_id), -X0, X0, -Y0 - 0.3, -Y0 - 0.05, 0.05, 8, 0.1, BRICK)
        if abandoned:
            _abandoned_rubble(body)
        roof, team = Parts(), Parts()
        _roof_snow(roof, abandoned)
        _roof_kit(roof, abandoned)
        _parapet(team, abandoned)
        parts += [(roof, "roof"), (team, "team_mask")]
    base = Parts()
    _base(base)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, 0.03, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..4 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    fire = FIRE_POINTS if variant == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, RUINS_SIZE if variant == "ruins" else SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
