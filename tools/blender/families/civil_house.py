"""Civilian house of the map: the winter house (docs/map_elements/11_civilian_buildings), its states (appearance.md, 5) and the
soldiers inside. Not the city's (families/city_house.py, drawn on the urban maps).

Footprint 2x2 cells, origin at the centre of the footprint on the ground, the front to -Y. Made upright as the city set (a storey to a
cell, the game squashes it, game/data/visuals/models.yaml): the walls to the eaves at 1.5, a steep roof to the ridge at 2.8 (snow does
not lie on a flat one), the chimney to 3.15. The parts:
    house     a timber house of two storeys, the upper one in the roof: ochre boards on a stone plinth, white corner boards, its gable
              to the street; white carved frames round the windows with curtains, dark green shutters on the front ones, an attic
              window in each gable, a wooden door at the front right
    porch     a wooden deck with steps, two posts and a little gable roof over the door (the owner's colour)
    roof      dark green sheet with standing seams under a thick layer of snow, its eaves showing; a brick chimney with snow on it
    yard      snow over the plot, a trodden path to the porch, drifts at the walls, a fence of dark pickets with snow on its rails,
              a woodpile under a lean-to at the left wall
No emblems, no text. The porch roof takes the owner's colour.
Nodes: "body" (the walls, windows, porch, fence, woodpile, drifts, and the barrels, sandbags or rubble of a state), "roof" (the roof, its
snow and the chimney), "team_mask" (the porch roof), "base" (the foundation and the snow of the plot). The abandoned one also has
fire_1..3. One GLB per state:
    building_house_small                    whole, empty
    building_house_small_occupied           whole, three soldiers inside: a barrel out of the two front windows and the attic window,
                                            sandbags round the foot
    building_house_small_damaged            damaged, empty: glass knocked out, a shutter torn off, boards broken in, soot over the
                                            windows, snow slid off part of the roof, pickets down
    building_house_small_damaged_occupied   damaged, occupied: the same, with the barrels and the sandbags
    building_house_small_abandoned          critically damaged, its soldiers out: the back of the right slope fallen in with the back
                                            right corner, charred rafters and the attic floor showing, rubble in the snow
    building_house_ruins                    destroyed: the stone plinth, charred stumps of the walls, the chimney standing alone,
                                            beams, sheets of the roof and rubble under patches of snow
Budget 8000 triangles, texture 1024 (prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.civil_house as ch
    print(ch.run("building_house_small"))   # or ch.run_all()
"""

import math

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

SIZE = 1024
FOLDER = "map"  # game/assets/models/map/<id>/, assets_src/models/map/<id>/
VARIANTS = {
    "building_house_small": "intact",
    "building_house_small_occupied": "occupied",
    "building_house_small_damaged": "damaged",
    "building_house_small_damaged_occupied": "damaged_occupied",
    "building_house_small_abandoned": "abandoned",
    "building_house_ruins": "ruins",
}

(BOARDS, PLINTH, TRIM, GLASS, CURTAIN, SHUTTER, WOOD, ROOF, SNOW, CONCRETE, BRICK, PATH, SAND, TEAM, DARK) = (cc.part(i) for i in range(15))
PARTS = 15

HX = 0.72  # the side walls stand at -HX and HX
YF, YB = -0.5, 0.78  # the front and the back wall
WALL_T = 0.1
FOUND_H = 0.1
PLINTH_H = 0.32
EAVE, RIDGE = 1.5, 2.8
SLOPE = (RIDGE - EAVE) / HX
OVER = 0.12  # the roof over the walls
ROOF_T = 0.05
SNOW_T = 0.09
GROUND = (0.48, 0.98)  # the windows of the ground storey, bottom and top
ATTIC = (1.66, 2.06)
WIN_H = 0.13  # half the width of a window
DOOR = (0.29, 0.55)  # the door at the front right, from x to x
FENCE_Y, FENCE_X = -0.96, 0.95
GATE = (0.22, 0.62)
CHIMNEY = ((-0.34, 0.22), (-0.14, 0.42))  # its corners in the plane, on the left slope
SPLIT_Y = 0.12  # abandoned: the right slope and the right wall are gone behind it

F = cc.Facade(HX, -YF, YB)
# The windows, as (wall, along, storey): along is x on the front and the back, y on the sides.
WINDOWS = [("front", u, GROUND) for u in (-0.46, -0.06)] + [("front", 0.0, ATTIC), ("back", 0.0, ATTIC)] + \
          [("back", u, GROUND) for u in (-0.36, 0.36)] + [(side, u, GROUND) for side in ("left", "right") for u in (-0.2, 0.42)]
# The flames of the abandoned one: the left front window, the broken top of the right wall, the attic floor in the gap.
FIRE_POINTS = ((-0.46, YF - 0.02, 0.9), (HX - 0.04, -0.1, 1.4), (0.35, 0.45, EAVE + 0.05))


def _profile_y(p, xz, y0, y1, painted):
    """A convex profile in the XZ plane pushed along Y from y0 to y1."""
    p.loft([[Vector((x, y, z)) for x, z in xz] for y in (y0, y1)], painted)


def _slope_z(x):
    """The height of the top of the roof sheet over x."""
    return RIDGE + ROOF_T - abs(x) * SLOPE


def _gone(wall, u, z):
    """Abandoned: whether a point of a wall fell with the back right corner."""
    if wall == "back":
        return u > -0.1
    if wall == "right":
        return u > SPLIT_Y - 0.1 and z > 0.5
    return False


# --- the house ----------------------------------------------------------------------

def _walls(p, abandoned):
    p.box((-HX, YF, FOUND_H), (HX, YF + WALL_T, EAVE), BOARDS)  # the front
    _profile_y(p, [(-HX, EAVE), (HX, EAVE), (0.0, RIDGE)], YF, YF + WALL_T, BOARDS)  # its gable
    p.box((-HX, YF + WALL_T, FOUND_H), (-HX + WALL_T, YB - WALL_T, EAVE), BOARDS)  # the left
    if abandoned:
        p.box((-HX, YB - WALL_T, FOUND_H), (-0.1, YB, EAVE), BOARDS)  # the back, its left part
        _profile_y(p, [(-HX, EAVE), (-0.1, EAVE), (-0.1, RIDGE - 0.1 * SLOPE)], YB - WALL_T, YB, BOARDS)
        p.box((HX - WALL_T, YF + WALL_T, FOUND_H), (HX, SPLIT_Y, EAVE), BOARDS)  # the right, its front part
        rnd = ic.seed("house_walls")
        ic.stumps(p, rnd, [((-0.1, YB - 0.05), (HX - 0.05, YB - 0.05)), ((HX - 0.05, SPLIT_Y), (HX - 0.05, YB))], FOUND_H, 0.3, 0.9, BOARDS,
                  step=0.16, gap=0.15)
        p.box((-HX + WALL_T, YF + WALL_T, EAVE - 0.04), (HX - WALL_T, YB - WALL_T, EAVE), WOOD)  # the attic floor, open to the sky
    else:
        p.box((-HX, YB - WALL_T, FOUND_H), (HX, YB, EAVE), BOARDS)
        _profile_y(p, [(-HX, EAVE), (HX, EAVE), (0.0, RIDGE)], YB - WALL_T, YB, BOARDS)
        p.box((HX - WALL_T, YF + WALL_T, FOUND_H), (HX, YB - WALL_T, EAVE), BOARDS)
    # The stone plinth round the foot and the white boards at the corners.
    for wall, lo, hi in (("front", -HX - 0.015, HX + 0.015), ("left", YF, YB), ("right", YF, SPLIT_Y if abandoned else YB),
                         ("back", -HX - 0.015, -0.1 if abandoned else HX + 0.015)):
        F.box(p, wall, lo, hi, FOUND_H, PLINTH_H, -0.01, 0.015, PLINTH)
    for x, y in ((-HX, YF), (HX, YF), (-HX, YB)) + (() if abandoned else ((HX, YB),)):
        p.box((x - 0.025, y - 0.025, PLINTH_H), (x + 0.025, y + 0.025, EAVE), TRIM)
    # The eaves board under the roof along the sides.
    for x in (-HX, HX):
        y1 = SPLIT_Y if abandoned and x > 0 else YB
        p.box((x - 0.02, YF, EAVE - 0.08), (x + 0.02, y1, EAVE), TRIM)


def _window(p, wall, u, storey, broken, shutters):
    """Glass (a dark hole when broken) with curtains, a white frame with a carved board over it, shutters on the front ones."""
    z0, z1 = storey
    F.window(p, wall, u, z0, z1, WIN_H, GLASS, TRIM, bars=(1, 1), broken=DARK if broken else None)
    if not broken:
        for a, b in ((u - WIN_H + 0.01, u - WIN_H + 0.07), (u + WIN_H - 0.07, u + WIN_H - 0.01)):
            F.box(p, wall, a, b, z0 + 0.02, z1 - 0.02, -0.004, 0.003, CURTAIN)
    # The carved board: a low gable over the frame.
    c = F.point(wall, u, z1 + 0.03, 0.0)
    out, along = F.out(wall), Vector((1, 0, 0)) if wall in ("front", "back") else Vector((0, 1, 0))
    tri = [c - along * (WIN_H + 0.06), c + along * (WIN_H + 0.06), c + Vector((0, 0, 0.09))]
    p.loft([[q + out * d for q in tri] for d in (-0.005, 0.03)], TRIM)
    if shutters:
        for a in (u - WIN_H - 0.15, u + WIN_H + 0.04):
            F.box(p, wall, a, a + 0.11, z0, z1, 0.0, 0.02, SHUTTER)


def _openings(p, variant):
    """The windows and the door. Damaged: two windows in three knocked out and a shutter off; abandoned: all of them."""
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    for i, (wall, u, storey) in enumerate(WINDOWS):
        if abandoned and _gone(wall, u, storey[1]):
            continue
        broken = abandoned or damaged and i % 3 != 1
        _window(p, wall, u, storey, broken, wall == "front" and storey is GROUND and not (damaged and u > -0.2))
    F.box(p, "front", DOOR[0], DOOR[1], FOUND_H + 0.06, 0.98, -0.01, 0.012, DARK if abandoned else WOOD)
    F.box(p, "front", DOOR[0] - 0.035, DOOR[1] + 0.035, 0.98, 1.02, -0.01, 0.03, TRIM)
    for a in (DOOR[0] - 0.035, DOOR[1]):
        F.box(p, "front", a, a + 0.035, FOUND_H + 0.06, 0.98, -0.01, 0.03, TRIM)
    if damaged:
        # Boards broken in by a shell, left of the door.
        F.box(p, "front", 0.06, 0.2, 0.42, 0.66, -0.02, 0.004, DARK)


def _porch(p, abandoned):
    x0, x1 = DOOR[0] - 0.12, DOOR[1] + 0.12
    p.box((x0, YF - 0.3, FOUND_H), (x1, YF, FOUND_H + 0.06), WOOD)  # the deck
    for h, d in ((FOUND_H, 0.42), (FOUND_H * 0.5, 0.52)):  # the steps
        p.box((x0 + 0.05, YF - d, 0.0), (x1 - 0.05, YF - d + 0.1, h), WOOD)
    if not abandoned:
        for x in (x0 + 0.03, x1 - 0.03):
            p.bar((x, YF - 0.27, FOUND_H + 0.06), (x, YF - 0.27, 1.12), 0.018, WOOD)


def _porch_roof(p):
    """The little gable roof over the porch: the team zone, seen from above once the game squashes the house."""
    x0, x1 = DOOR[0] - 0.17, DOOR[1] + 0.17
    xm = (x0 + x1) / 2
    for xe in (x0, x1):
        _profile_y(p, [(xe, 1.1), (xm, 1.32), (xm, 1.36), (xe, 1.14)], YF - 0.36, YF, TEAM)


def _fence(p, variant):
    """Dark pickets along the front of the plot and its sides with a gap for the gate, snow on the top rail; pickets down when it
    is damaged, most of them when abandoned or in ruins."""
    keep = {"damaged": 4, "damaged_occupied": 4, "abandoned": 2, "ruins": 2}.get(variant)
    runs = [[(x, FENCE_Y) for x in cc.steps(-FENCE_X, GATE[0], 0.075)], [(x, FENCE_Y) for x in cc.steps(GATE[1], FENCE_X, 0.075)],
            [(-FENCE_X, y) for y in cc.steps(FENCE_Y + 0.075, 0.35, 0.075)], [(FENCE_X, y) for y in cc.steps(FENCE_Y + 0.075, 0.35, 0.075)]]
    n = 0
    for run in runs:
        for x, y in run:
            n += 1
            if keep is not None and n % keep == 0:
                continue
            p.box((x - 0.014, y - 0.008, 0.0), (x + 0.014, y + 0.008, 0.24), WOOD)
    if keep is None or variant in ("damaged", "damaged_occupied"):
        for run in runs:  # two rails along each run, snow on the top one
            (x0, y0), (x1, y1) = run[0], run[-1]
            if y0 == y1:
                lo, hi = (x0 - 0.01, y0 + 0.008), (x1 + 0.01, y0 + 0.02)
            else:
                lo, hi = (x0 - 0.006, y0 - 0.01), (x0 + 0.006, y1 + 0.01)
            for z in (0.08, 0.18):
                p.box((lo[0], lo[1], z), (hi[0], hi[1], z + 0.025), WOOD)
            p.box((lo[0] - 0.004, lo[1] - 0.004, 0.205), (hi[0] + 0.004, hi[1] + 0.004, 0.225), SNOW)


def _woodpile(p, abandoned):
    """Logs stacked at the left wall under a lean-to with snow on it."""
    x0, x1 = -HX - 0.24, -HX - 0.02
    for row in range(3):
        for k in range(5 - row):
            y = -0.16 + 0.11 * k + 0.055 * row
            p.cylinder((-HX - 0.13, y, 0.06 + 0.1 * row), 'X', 0.05, 0.1, WOOD, sides=6)
    if not abandoned:
        _profile_y(p, [(x0 - 0.06, 0.5), (-HX, 0.68), (-HX, 0.71), (x0 - 0.06, 0.53)], -0.26, 0.42, WOOD)
        _profile_y(p, [(x0 - 0.04, 0.53), (-HX - 0.02, 0.71), (-HX - 0.02, 0.76), (x0 - 0.02, 0.58)], -0.24, 0.4, SNOW)
        for y in (-0.24, 0.4):
            p.bar((x0 - 0.03, y, 0.0), (x0 - 0.03, y, 0.52), 0.015, WOOD)


def _drifts(p, abandoned):
    """Snow drifted against the walls: low wedges along their foot, none in front of the porch."""
    d = 0.14
    runs = [("front", -HX - 0.02, DOOR[0] - 0.15), ("left", YF + 0.05, -0.28), ("left", 0.5, YB), ("back", -HX, -0.1 if abandoned else HX)]
    if not abandoned:
        runs.append(("right", YF, YB))
    for wall, a, b in runs:
        out = F.out(wall)
        along = Vector((1, 0, 0)) if wall in ("front", "back") else Vector((0, 1, 0))
        foot = [F.point(wall, a, FOUND_H), F.point(wall, a, FOUND_H) + out * d, F.point(wall, a, FOUND_H + 0.16)]
        p.loft([[q + along * t for q in foot] for t in (0.0, b - a)], SNOW)


def _barrels(p):
    """Three rifle barrels, one for each soldier inside: out of the two front windows and the attic window."""
    ic.barrels(p, [F.point("front", u, z) for u, z in ((-0.46, 0.75), (-0.06, 0.75), (0.0, 1.85))], (0, -1, 0), DARK, length=0.38)


def _sandbags(p):
    """Sandbags round the foot of the house, two rows; the porch stays open."""
    for z, shift in ((FOUND_H, 0.0), (FOUND_H + 0.08, 0.12)):
        for t in cc.steps(-HX + 0.1 + shift, HX - 0.1, 0.25):
            if t < DOOR[0] - 0.2:
                cc.bag(p, (t, YF - 0.11, z), 0.0, SAND)
            cc.bag(p, (t, YB + 0.11, z), 0.0, SAND)
        for t in cc.steps(YF + 0.05 + shift, YB - 0.05, 0.25):
            if t > 0.45 or t < -0.3:  # not over the woodpile
                cc.bag(p, (-HX - 0.11, t, z), math.pi / 2, SAND)
            cc.bag(p, (HX + 0.11, t, z), math.pi / 2, SAND)


# --- the roof -------------------------------------------------------------------------

def _roof(p, variant):
    """Two steep slopes of sheet with snow on them, over the gables; the chimney on the left slope. Damaged: the snow slid off the
    lower half of the right slope; abandoned: the back of the right slope is gone."""
    abandoned = variant == "abandoned"
    y0, y1 = YF - OVER, YB + OVER
    ex, ez = HX + OVER, EAVE - OVER * SLOPE
    for side in (-1, 1):
        right = side > 0
        ye = SPLIT_Y if abandoned and right else y1
        _profile_y(p, [(side * ex, ez), (0.0, RIDGE), (0.0, RIDGE + ROOF_T), (side * ex, ez + ROOF_T)], y0, ye, ROOF)
        # The snow: thick on the slope, short of the eaves and the gable ends, so the dark sheet shows round it.
        x_low = ex - (0.38 if variant in ("damaged", "damaged_occupied") and right else 0.05)
        _profile_y(p, [(side * x_low, _slope_z(x_low)), (0.0, RIDGE + ROOF_T), (0.0, RIDGE + ROOF_T + SNOW_T),
                       (side * (x_low - 0.03), _slope_z(x_low - 0.03) + SNOW_T)], y0 + 0.05, ye - (0.0 if abandoned and right else 0.05), SNOW)
    # The chimney: brick up through the left slope, a dark top, snow on it.
    (cx0, cy0), (cx1, cy1) = CHIMNEY
    top = RIDGE + 0.32
    p.box((cx0, cy0, EAVE), (cx1, cy1, top), BRICK)
    p.box((cx0 - 0.025, cy0 - 0.025, top), (cx1 + 0.025, cy1 + 0.025, top + 0.04), CONCRETE)
    p.box((cx0 + 0.03, cy0 + 0.03, top + 0.04), (cx1 - 0.03, cy1 - 0.03, top + 0.045), DARK)
    p.box((cx0 - 0.02, cy0 - 0.02, top + 0.04), (cx0 + 0.04, cy1 + 0.02, top + 0.07), SNOW)


RAFTERS_ABANDONED = (((0.0, SPLIT_Y + 0.08, RIDGE), (0.5, 0.35, EAVE + 0.05)), ((0.0, 0.42, RIDGE - 0.05), (0.62, 0.5, EAVE + 0.15)),
                     ((0.0, 0.62, RIDGE - 0.4), (0.45, YB - 0.05, EAVE + 0.02)), ((0.0, SPLIT_Y, RIDGE), (0.0, YB - 0.1, RIDGE - 0.6)))
RUBBLE_ABANDONED = (((0.5, YB + 0.15, 0.08), (0.36, 0.12, 0.08), (0.2, 0.1, 0.3)), ((HX + 0.16, 0.5, 0.1), (0.12, 0.4, 0.1), (0.1, 0.3, 0.2)),
                    ((0.3, YB + 0.28, 0.06), (0.3, 0.2, 0.05), (0.4, 0.2, 1.2)), ((0.45, 0.45, EAVE + 0.06), (0.35, 0.25, 0.08), (0.2, 0.3, 0.6)))
SHEETS_ABANDONED = (((0.55, YB + 0.32, 0.12), (0.5, 0.4, 0.02), (0.35, 0.1, 0.4)), ((HX + 0.3, 0.3, 0.1), (0.45, 0.35, 0.02), (0.1, -0.4, 1.3)))
BRICKS_DAMAGED = (((-0.55, YF - 0.16, 0.03), (0.22, 0.04, 0.025), (0.0, 0.0, 0.3)), ((0.75, -0.2, 0.03), (0.26, 0.04, 0.025), (0.0, 0.0, 1.2)),
                  ((-0.2, YF - 0.3, 0.02), (0.11, 0.06, 0.04), (0.0, 0.0, 0.7)))  # a shutter and boards on the snow


# --- the ruins ----------------------------------------------------------------------

def _ruins(p):
    rnd = ic.seed("building_house_ruins")
    for wall, lo, hi in (("front", -HX, HX), ("left", YF, YB), ("right", YF, YB), ("back", -HX, HX)):
        F.box(p, wall, lo, hi, FOUND_H, PLINTH_H - 0.06, -WALL_T, 0.015, PLINTH)
    ic.stumps(p, rnd, [((-HX, YF + 0.05), (HX, YF + 0.05)), ((-HX + 0.05, YF), (-HX + 0.05, YB)), ((HX - 0.05, YF), (HX - 0.05, YB)),
                       ((-HX, YB - 0.05), (HX, YB - 0.05))], PLINTH_H - 0.06, 0.05, 0.55, BOARDS, step=0.14, gap=0.35)
    (cx0, cy0), (cx1, cy1) = CHIMNEY
    p.box((cx0, cy0, FOUND_H), (cx1, cy1, 1.75), BRICK)  # the chimney, standing alone
    p.box((cx0 - 0.02, cy0 - 0.02, 1.72), (cx1 + 0.02, cy1 + 0.02, 1.76), SNOW)
    ic.rubble(p, rnd, -0.55, 0.6, -0.35, 0.6, FOUND_H, 14, 0.13, PLINTH, height=0.12)
    ic.rubble(p, rnd, -0.55, 0.6, -0.35, 0.6, FOUND_H, 8, 0.12, DARK, height=0.1)
    ic.rubble(p, rnd, -0.5, 0.55, -0.3, 0.55, FOUND_H + 0.1, 8, 0.1, BOARDS, height=0.1)
    for centre, size, turn in (((0.1, 0.1, 0.32), (0.8, 0.55, 0.02), (0.35, -0.2, 0.3)), ((-0.35, 0.45, 0.28), (0.6, 0.4, 0.02), (-0.4, 0.2, 1.1)),
                               ((0.45, -0.25, 0.2), (0.5, 0.35, 0.02), (0.2, 0.45, 2.0))):
        cc.block(p, centre, size, turn, ROOF)  # sheets of the roof
    for a, b in (((-0.5, -0.2, 0.55), (0.4, 0.3, 0.12)), ((0.5, 0.6, 0.45), (-0.1, 0.1, 0.15)), ((-0.2, 0.6, 0.6), (0.3, -0.3, 0.1))):
        p.bar(a, b, 0.03, DARK)  # charred beams
    for centre, size, turn in (((0.2, -0.1, 0.4), (0.4, 0.3, 0.04), (0.35, -0.2, 0.3)), ((-0.35, 0.4, 0.35), (0.3, 0.25, 0.04), (-0.4, 0.2, 1.1)),
                               ((0.0, 0.35, 0.22), (0.3, 0.3, 0.04), (0.1, 0.1, 0.4))):
        cc.block(p, centre, size, turn, SNOW)  # snow fallen on the heap


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Ochre boards in courses, a grey stone plinth, white trim, dark glass with warm curtains, dark green shutters and roof sheet,
    brown wood, snow, concrete, red brick, a trodden path, sandbags, a light team zone; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.12)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = g.axis(coord, 'X'), g.axis(coord, 'Y'), g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    # Boards: a dark line under each, every 0.07 up the wall, the wood's grain along them.
    board = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 14.0)), -10.0, 1.6))
    boards = g.mix(board, g.mix(g.noise(4.0, 6.0), lin((0.60, 0.44, 0.22)), lin((0.70, 0.53, 0.28))), lin((0.32, 0.22, 0.12)))
    stone, joints = ic.brick(g, coord, (0.36, 0.36, 0.36), (0.48, 0.47, 0.45), (0.24, 0.24, 0.24), course=12.0, length=5.0)
    trim = g.mix(grain, lin((0.84, 0.84, 0.82)), lin((0.93, 0.93, 0.91)))
    glass = g.mix(g.math('MULTIPLY', z, 0.4), lin((0.10, 0.13, 0.16)), lin((0.20, 0.25, 0.30)))
    curtain = g.mix(grain, lin((0.80, 0.55, 0.30)), lin((0.88, 0.64, 0.36)))
    shutter = g.mix(grain, lin((0.12, 0.26, 0.17)), lin((0.16, 0.32, 0.21)))
    wood = g.mix(g.noise(3.0, 6.0), lin((0.24, 0.16, 0.10)), lin((0.32, 0.22, 0.14)))
    seam = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 9.0)), -12.0, 1.4))  # the standing seams down the slope
    roof = g.mix(seam, g.mix(g.noise(8.0, 2.0), lin((0.14, 0.24, 0.18)), lin((0.18, 0.30, 0.22))), lin((0.07, 0.11, 0.08)))
    snow = g.mix(g.noise(9.0, 3.0), lin((0.80, 0.84, 0.90)), lin((0.93, 0.95, 0.97)))
    concrete = g.mix(grain, lin((0.52, 0.51, 0.49)), lin((0.60, 0.59, 0.57)))
    brick, _ = ic.brick(g, coord, (0.48, 0.20, 0.14), (0.58, 0.27, 0.18), (0.62, 0.58, 0.52), course=24.0, length=12.0)
    path = g.mix(g.noise(14.0, 4.0), lin((0.52, 0.52, 0.53)), lin((0.66, 0.66, 0.68)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.05)), lin((0.10, 0.09, 0.08)))

    albedo = cc.paint(g, mask, (boards, stone, trim, glass, curtain, shutter, wood, roof, snow, concrete, brick, path, sand, team, dark))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        crack = g.math('MULTIPLY', g.math('ADD', mask[0], mask[1]), cc.cracks(g))
        albedo = g.mix(crack, albedo, lin((0.14, 0.10, 0.07)))
        soot = g.math('MAXIMUM', cc.band(g, z, GROUND[1] + 0.06, GROUND[1] + 0.3), cc.band(g, z, ATTIC[1] + 0.08, ATTIC[1] + 0.3))
        soot = g.math('MULTIPLY', soot, g.math('SUBTRACT', 1.0, mask[8]))  # not on the snow
        if variant == "abandoned":
            soot = g.math('MAXIMUM', soot, g.math('MULTIPLY', ic.soot(g, 0.5), g.math('SUBTRACT', 1.0, mask[8])))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.75), albedo, lin((0.08, 0.07, 0.06)))
    if variant == "ruins":
        burnt = g.math('MULTIPLY', ic.soot(g, 0.5), g.math('SUBTRACT', 1.0, mask[8]))
        albedo = g.mix(g.math('MULTIPLY', burnt, 0.7), albedo, lin((0.06, 0.05, 0.05)))

    rough = cc.weighted(g, mask, (0.8, 0.9, 0.6, 0.15, 0.9, 0.6, 0.85, 0.45, 0.75, 0.9, 0.9, 0.9, 0.95, 0.5, 0.9))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.4, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.2))
    height = g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', board, -1.0),
                    g.math('MULTIPLY_ADD', mask[1], g.math('MULTIPLY', joints, -1.0), g.noise(60.0, 3.0)))
    g.finish(albedo, g.bump(height, 0.05, 0.006), g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _base(p):
    p.box((-HX - 0.05, YF - 0.05, 0.0), (HX + 0.05, YB + 0.05, FOUND_H), CONCRETE)  # the foundation
    p.box((-0.99, -0.99, 0.0), (0.99, 0.99, 0.025), SNOW)  # the snow over the plot
    p.box((DOOR[0] - 0.02, -0.99, 0.025), (DOOR[1] + 0.02, YF - 0.5, 0.03), PATH)  # trodden from the gate to the steps


def build(variant_id):
    ic.clear(variant_id, ("body", "roof", "team_mask", "base"))
    variant = VARIANTS[variant_id]
    abandoned = variant == "abandoned"
    body = Parts()
    if variant == "ruins":
        _ruins(body)
    else:
        _walls(body, abandoned)
        _openings(body, variant)
        _porch(body, abandoned)
        _woodpile(body, abandoned)
        _drifts(body, abandoned)
    _fence(body, variant)
    if variant in ("occupied", "damaged_occupied"):
        _barrels(body)
        _sandbags(body)
    if variant in ("damaged", "damaged_occupied"):
        for centre, size, turn in BRICKS_DAMAGED:
            cc.block(body, centre, size, turn, SHUTTER if size[0] > 0.2 else BOARDS)
    if abandoned:
        for centre, size, turn in RUBBLE_ABANDONED:
            cc.block(body, centre, size, turn, DARK)
        for centre, size, turn in SHEETS_ABANDONED:
            cc.block(body, centre, size, turn, ROOF)
        for a, b in RAFTERS_ABANDONED:
            body.bar(a, b, 0.025, DARK)
    parts = [(body, "body")]
    if variant != "ruins":
        roof, team = Parts(), Parts()
        _roof(roof, variant)
        _porch_roof(team)  # the porch stands when abandoned too: the house fell at the back
        parts += [(roof, "roof"), (team, "team_mask")]
    base = Parts()
    _base(base)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, 0.02, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
