"""City small house: element 01 of the city set (docs/city_elements/01_small_house), its states (appearance.md, 4; the states of the
civilian buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry daylight, no
snow; not the winter house of the map (building_house_small, civil_house.py).

Footprint 2x2 cells, origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the houses of the
map (the game squashes it upright, game/data/visuals/models.yaml): two storeys of brick to the eaves at 1.8, the ridge at 2.55, the
chimney to 2.8. The parts:
    house     two storeys of red brick on a concrete foundation, the back two thirds of the footprint; white framed windows with
              curtains, a wooden door in a white frame
    porch     a wooden deck with a step, two white posts and a sloped canopy over the door (the team zone)
    roof      a gable roof of dark tiles, its ridge along the street, a brick chimney
    garden    a strip of lawn in front, a concrete path to the porch, a low white picket fence with a gate gap, two shrubs
No emblems, no text. The canopy over the door takes the owner's colour.
Nodes: "body" (the walls, windows, door, porch, fence and shrubs, and the barrels, sandbags or rubble of a state), "roof" (the roof and
the chimney), "team_mask" (the canopy), "base" (the foundation, the lawn and the path). The abandoned one also has fire_1..3. One GLB per
state:
    city_house_small                    whole, empty
    city_house_small_occupied           whole, three soldiers inside: a barrel out of each upper front window, sandbags round the foot
    city_house_small_damaged            damaged, empty: glass knocked out, cracks, soot over the windows, pickets and bricks down
    city_house_small_damaged_occupied   damaged, occupied: the same, with the barrels and the sandbags
    city_house_small_abandoned          critically damaged, its soldiers out: the right half of the roof and the chimney gone, the back
                                        right corner fallen in, the floor between the storeys and charred rafters showing, rubble
    city_house_small_ruins              destroyed: stumps of the walls, the chimney standing alone, a broken floor, beams and rubble
Budget 8000 triangles, texture 1024 (docs/city_elements/01_small_house/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_house as chs
    print(chs.run("city_house_small"))   # or chs.run_all()
"""

import math

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_house_small": "intact",
    "city_house_small_occupied": "occupied",
    "city_house_small_damaged": "damaged",
    "city_house_small_damaged_occupied": "damaged_occupied",
    "city_house_small_abandoned": "abandoned",
    "city_house_small_ruins": "ruins",
}


BRICK, ROOF, WHITE, GLASS, CURTAIN, WOOD, CONCRETE, GRASS, SAND, TEAM, DARK, HEDGE, PATH = (cc.part(i) for i in range(13))
PARTS = 13

HX = 0.85  # the side walls stand at -HX and HX
YF, YB = -0.40, 0.90  # the front and the back wall
YM = (YF + YB) / 2  # under the ridge
WALL_T = 0.12
FOUND_H = 0.12
FLOOR2 = 0.95  # the floor between the storeys
EAVE, RIDGE = 1.80, 2.55
SLOPE = (RIDGE - EAVE) / (YM - YF)
ROOF_T = 0.07
GROUND, UPPER = (0.40, 0.78), (1.20, 1.58)  # the windows of the two storeys, bottom and top
WIN_W = 0.28
FENCE_Y, FENCE_X = -0.95, 0.95

# The windows, as (wall, along): along is x on the front and the back, y on the sides. The door is in the middle of the front.
WINDOWS = [("front", u, GROUND) for u in (-0.52, 0.52)] + [("front", u, UPPER) for u in (-0.52, 0.0, 0.52)] + \
          [("back", u, z) for u in (-0.42, 0.42) for z in (GROUND, UPPER)] + \
          [(side, u, z) for side in ("left", "right") for u in (0.0, 0.5) for z in (GROUND, UPPER)]
# The flames of the abandoned one: the upper left window, the top of the broken right wall, the ridge that is left.
FIRE_POINTS = ((-0.52, YF - 0.02, 1.4), (HX - 0.06, 0.0, 0.95), (-0.4, YM, RIDGE))


# --- placing on the walls --------------------------------------------------------------

def _on_wall(p, wall, u0, u1, z0, z1, d0, d1, part):
    """A box on the outer face of a wall: u along the wall, z up, d out of the wall (0 its face, less than 0 into it)."""
    if wall == "front":
        lo, hi = (u0, YF - d1, z0), (u1, YF - d0, z1)
    elif wall == "back":
        lo, hi = (u0, YB + d0, z0), (u1, YB + d1, z1)
    elif wall == "left":
        lo, hi = (-HX - d1, u0, z0), (-HX - d0, u1, z1)
    else:
        lo, hi = (HX + d0, u0, z0), (HX + d1, u1, z1)
    p.box(lo, hi, part)


def _window(p, wall, u, storey, broken, curtains):
    """Glass (or a dark hole when broken), curtains drawn to the sides, a white frame with a sill and a cross of glazing bars."""
    z0, z1 = storey
    h = WIN_W / 2
    _on_wall(p, wall, u - h, u + h, z0, z1, -0.01, 0.005, DARK if broken else GLASS)
    if curtains:
        for a, b in ((u - h + 0.01, u - h + 0.08), (u + h - 0.08, u + h - 0.01)):
            _on_wall(p, wall, a, b, z0 + 0.02, z1 - 0.02, 0.0, 0.009, CURTAIN)
    _on_wall(p, wall, u - h - 0.03, u + h + 0.03, z1, z1 + 0.03, -0.01, 0.025, WHITE)
    _on_wall(p, wall, u - h - 0.04, u + h + 0.04, z0 - 0.03, z0, -0.01, 0.045, WHITE)
    for a in (u - h - 0.03, u + h):
        _on_wall(p, wall, a, a + 0.03, z0, z1, -0.01, 0.025, WHITE)
    if not broken:
        _on_wall(p, wall, u - 0.008, u + 0.008, z0, z1, 0.0, 0.012, WHITE)
        _on_wall(p, wall, u - h, u + h, (z0 + z1) / 2 - 0.008, (z0 + z1) / 2 + 0.008, 0.0, 0.012, WHITE)


# --- the house ----------------------------------------------------------------------

def _gable(p, x0, x1):
    p.profile_x([(YF, EAVE), (YB, EAVE), (YM, RIDGE)], x0, x1, BRICK)


def _walls(p, abandoned):
    p.box((-HX, YF, FOUND_H), (HX, YF + WALL_T, EAVE), BRICK)  # the front
    p.box((-HX, YF + WALL_T, FOUND_H), (-HX + WALL_T, YB - WALL_T, EAVE), BRICK)  # the left
    _gable(p, -HX, -HX + WALL_T)
    if abandoned:
        p.box((-HX, YB - WALL_T, FOUND_H), (0.2, YB, EAVE), BRICK)  # the back, its right part fallen
        p.box((HX - WALL_T, YF + WALL_T, FOUND_H), (HX, YM, EAVE), BRICK)  # the right, its front half
        p.box((HX - WALL_T, YM, FOUND_H), (HX, YB - WALL_T, 0.9), BRICK)  # and its back half broken low
    else:
        p.box((-HX, YB - WALL_T, FOUND_H), (HX, YB, EAVE), BRICK)
        p.box((HX - WALL_T, YF + WALL_T, FOUND_H), (HX, YB - WALL_T, EAVE), BRICK)
        _gable(p, HX - WALL_T, HX)
    # The floor between the storeys and the ceiling under the roof: seen only where the house is broken open.
    p.box((-HX + WALL_T, YF + WALL_T, FLOOR2 - 0.04), (HX - WALL_T, YB - WALL_T, FLOOR2), WOOD)
    p.box((-HX + WALL_T, YF + WALL_T, EAVE - 0.04), (0.1 if abandoned else HX - WALL_T, YB - WALL_T, EAVE), WOOD)


def _openings(p, variant):
    """The windows and the door. Damaged: two windows in three knocked out; abandoned: all of them."""
    damaged = variant in ("damaged", "damaged_occupied")
    for i, (wall, u, storey) in enumerate(WINDOWS):
        if variant == "abandoned" and (wall == "back" and u > 0.2 or wall == "right" and (u > 0.25 or storey is UPPER and u > 0.2)):
            continue  # on the walls that fell
        broken = variant == "abandoned" or damaged and i % 3 != 1
        _window(p, wall, u, storey, broken, not broken)
    _on_wall(p, "front", -0.15, 0.15, FOUND_H, 0.82, -0.01, 0.015, DARK if variant == "abandoned" else WOOD)
    _on_wall(p, "front", -0.18, 0.18, 0.82, 0.86, -0.01, 0.03, WHITE)
    for a in (-0.18, 0.15):
        _on_wall(p, "front", a, a + 0.03, FOUND_H, 0.82, -0.01, 0.03, WHITE)


def _porch(p):
    p.box((-0.32, YF - 0.24, FOUND_H - 0.04), (0.32, YF, FOUND_H + 0.02), WOOD)  # the deck
    p.box((-0.2, YF - 0.34, 0.02), (0.2, YF - 0.24, 0.08), WOOD)  # the step
    for x in (-0.28, 0.28):
        p.bar((x, YF - 0.2, FOUND_H + 0.02), (x, YF - 0.2, 0.97), 0.018, WHITE)


def _canopy(p):
    """The sloped canopy over the door: the team zone. A band on the wall, as the prompt has it, is too thin to read once the game
    squashes the house upright; the canopy is seen from above."""
    p.profile_x([(YF, 1.02), (YF - 0.28, 0.94), (YF - 0.28, 0.98), (YF, 1.07)], -0.36, 0.36, TEAM)


def _fence(p, variant):
    """A low white picket fence along the front of the garden and its sides, with a gap for the gate. Damaged, some pickets are down;
    abandoned and ruined, most."""
    keep = {"damaged": 4, "damaged_occupied": 4, "abandoned": 2, "ruins": 3}.get(variant)
    runs = [((x, FENCE_Y) for x in cc.steps(-FENCE_X, -0.16, 0.07)), ((x, FENCE_Y) for x in cc.steps(0.16, FENCE_X, 0.07)),
            ((-FENCE_X, y) for y in cc.steps(FENCE_Y + 0.07, YF - 0.06, 0.07)), ((FENCE_X, y) for y in cc.steps(FENCE_Y + 0.07, YF - 0.06, 0.07))]
    n = 0
    for run in runs:
        for x, y in run:
            n += 1
            if keep is not None and n % keep == 0:
                continue
            p.box((x - 0.013, y - 0.008, 0.02), (x + 0.013, y + 0.008, 0.2), WHITE)
    if keep is None or variant in ("damaged", "damaged_occupied"):
        for z in (0.07, 0.15):  # the rails
            p.box((-FENCE_X, FENCE_Y + 0.008, z), (-0.16, FENCE_Y + 0.02, z + 0.02), WHITE)
            p.box((0.16, FENCE_Y + 0.008, z), (FENCE_X, FENCE_Y + 0.02, z + 0.02), WHITE)
            for x in (-FENCE_X, FENCE_X):
                p.box((x - 0.006 if x < 0 else x - 0.006, FENCE_Y, z), (x + 0.006, YF - 0.06, z + 0.02), WHITE)


def _shrubs(p):
    for x in (-0.68, 0.68):
        p.sphere((x, YF - 0.32, 0.13), 'Z', 0.13, HEDGE, sides=10, rings=6)


def _barrels(p):
    """Three rifle barrels, one for each soldier inside, out of the upper front windows."""
    for u in (-0.52, 0.0, 0.52):
        p.bar((u, YF + 0.02, 1.35), (u + 0.04, YF - 0.36, 1.31), 0.016, DARK)


def _sandbags(p):
    """Sandbags round the foot of the house, two rows; the door stays open."""
    for z, shift in ((FOUND_H, 0.0), (FOUND_H + 0.08, 0.13)):
        for t in cc.steps(-HX + 0.1 + shift, HX - 0.1, 0.26):
            if abs(t) > 0.36:
                cc.bag(p, (t, YF - 0.11, z), 0.0, SAND)
            cc.bag(p, (t, YB + 0.11, z), 0.0, SAND)
        for t in cc.steps(YF + 0.05 + shift, YB - 0.05, 0.26):
            cc.bag(p, (-HX - 0.11, t, z), math.pi / 2, SAND)
            cc.bag(p, (HX + 0.11, t, z), math.pi / 2, SAND)


# Bricks down at the foot of the damaged one; the rubble of the abandoned one and of the ruins: middle, size, turn.
BRICKS = (((-0.7, YF - 0.12, 0.13), (0.07, 0.04, 0.03), (0.0, 0.2, 0.5)), ((0.6, YF - 0.1, 0.13), (0.07, 0.04, 0.03), (0.3, 0.0, 1.2)),
          ((HX + 0.08, 0.3, 0.13), (0.07, 0.04, 0.03), (0.0, 0.0, 0.3)), ((-HX - 0.08, 0.6, 0.13), (0.07, 0.04, 0.03), (0.2, 0.1, 2.0)),
          ((0.3, YB + 0.08, 0.13), (0.07, 0.04, 0.03), (0.0, 0.3, 0.8)))
RUBBLE_ABANDONED = (((0.55, 0.65, FLOOR2 + 0.06), (0.3, 0.24, 0.12), (0.1, 0.2, 0.4)), ((0.45, 0.3, FLOOR2 + 0.05), (0.2, 0.2, 0.1), (0.3, 0.1, 1.0)),
                    ((0.7, YB + 0.05, 0.2), (0.3, 0.2, 0.16), (0.2, 0.3, 0.2)), ((HX + 0.05, 0.6, 0.18), (0.2, 0.3, 0.14), (0.1, 0.4, 0.7)),
                    ((0.35, YB + 0.06, 0.15), (0.2, 0.16, 0.1), (0.3, 0.2, 1.3)),
                    ((0.5, 0.58, FLOOR2 + 0.16), (0.2, 0.2, 0.45), (0.0, 1.1, 0.3)))  # the chimney, fallen on the floor
RAFTERS_ABANDONED = (((0.1, YM, RIDGE - 0.02), (0.4, YM + 0.2, RIDGE - 0.35)), ((0.1, YF + 0.2, EAVE + 0.2), (0.45, YF + 0.3, EAVE - 0.1)),
                     ((0.1, YB - 0.2, EAVE + 0.2), (0.5, YB - 0.3, EAVE - 0.05)), ((0.1, YM - 0.3, RIDGE - 0.35), (0.55, YM - 0.35, EAVE + 0.05)))
RUBBLE_RUINS = (((-0.45, 0.1, 0.22), (0.4, 0.34, 0.2), (0.1, 0.2, 0.4)), ((0.05, 0.4, 0.2), (0.34, 0.3, 0.16), (0.2, 0.1, 1.1)),
                ((0.45, -0.05, 0.18), (0.3, 0.26, 0.14), (0.1, 0.3, 0.2)), ((-0.1, -0.2, 0.17), (0.26, 0.22, 0.12), (0.3, 0.1, 0.9)),
                ((0.6, 0.6, 0.16), (0.22, 0.2, 0.1), (0.2, 0.2, 0.3)), ((-0.6, 0.6, 0.16), (0.2, 0.24, 0.1), (0.1, 0.2, 1.6)),
                ((0.2, YF - 0.12, 0.08), (0.2, 0.14, 0.08), (0.3, 0.2, 0.4)))
SLABS_RUINS = (((-0.1, 0.25, 0.4), (0.9, 0.7, 0.05), (0.3, -0.15, 0.2)),)
BEAMS_RUINS = (((-0.5, 0.5, 0.75), (0.2, 0.0, 0.2)), ((0.5, 0.7, 0.55), (-0.1, 0.3, 0.15)), ((-0.3, -0.2, 0.45), (0.3, 0.1, 0.2)))


def _ruins(p):
    p.box((-HX, YF, FOUND_H), (-0.2, YF + WALL_T, 0.75), BRICK)  # the front, the left stump
    p.box((0.35, YF, FOUND_H), (HX, YF + WALL_T, 0.45), BRICK)  # the front, the right one, lower
    p.box((-HX, YF + WALL_T, FOUND_H), (-HX + WALL_T, 0.4, 0.6), BRICK)  # the left
    p.box((-0.6, YB - WALL_T, FOUND_H), (0.1, YB, 0.95), BRICK)  # the back, the highest piece
    p.box((HX - WALL_T, 0.2, FOUND_H), (HX, YB, 0.35), BRICK)  # the right, low
    p.box((0.42, 0.45, FOUND_H), (0.62, 0.65, 1.15), BRICK)  # the chimney, standing alone
    for centre, size, turn in RUBBLE_RUINS:
        cc.block(p, centre, size, turn, BRICK)
    for centre, size, turn in SLABS_RUINS:
        cc.block(p, centre, size, turn, WOOD)
    for a, b in BEAMS_RUINS:
        p.bar(a, b, 0.035, DARK)


# --- the roof -------------------------------------------------------------------------

def _roof(p, abandoned):
    """Two slopes of tiles, their undersides on the gables, overhanging the walls; the chimney on the back slope."""
    x0, x1 = -HX - 0.07, (0.1 if abandoned else HX + 0.07)
    over = 0.12
    for sign in (-1, 1):  # the front slope, then the back one
        eave_y = YM + sign * (YM - YF + over)
        eave_z = EAVE - over * SLOPE
        p.profile_x([(eave_y, eave_z), (YM, RIDGE), (YM, RIDGE + ROOF_T), (eave_y, eave_z + ROOF_T)], x0, x1, ROOF)
    if not abandoned:
        p.box((0.42, 0.45, 1.9), (0.62, 0.65, 2.75), BRICK)
        p.box((0.40, 0.43, 2.75), (0.64, 0.67, 2.8), CONCRETE)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Red brick with mortar, dark tiles, white trim, glass with warm curtains, wood, concrete, a lawn and a path, sandbags, a light
    team zone; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    # Bricks: courses up the wall, every other one shifted by half a brick; x + y runs along every wall.
    course = g.math('MULTIPLY', z, 26.0)
    row = g.math('FRACT', course)
    col = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 7.0), g.math('MULTIPLY', g.math('FLOOR', course), 0.5)))
    mortar = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', row, -12.0, 2.0)), g.clamp01(g.math('MULTIPLY_ADD', col, -25.0, 2.0)))
    brick = g.mix(mortar, g.mix(g.noise(5.0, 2.0), lin((0.52, 0.25, 0.18)), lin((0.63, 0.32, 0.23))), lin((0.70, 0.66, 0.60)))
    # Tiles: rows across the slope, a dark line under each.
    tile_row = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 22.0)), -8.0, 1.5))
    roof = g.mix(tile_row, g.mix(g.noise(8.0, 2.0), lin((0.27, 0.25, 0.25)), lin((0.36, 0.32, 0.30))), lin((0.14, 0.13, 0.13)))
    white = g.mix(grain, lin((0.84, 0.84, 0.82)), lin((0.92, 0.92, 0.90)))
    glass = g.mix(g.math('MULTIPLY', z, 0.5), lin((0.16, 0.22, 0.27)), lin((0.26, 0.34, 0.40)))
    curtain = g.mix(grain, lin((0.78, 0.58, 0.36)), lin((0.86, 0.66, 0.42)))
    wood = g.mix(g.noise(3.0, 6.0), lin((0.38, 0.25, 0.15)), lin((0.48, 0.33, 0.20)))
    concrete = g.mix(grain, lin((0.55, 0.54, 0.51)), lin((0.63, 0.62, 0.59)))
    grass = g.mix(g.noise(20.0, 5.0), lin((0.30, 0.44, 0.19)), lin((0.40, 0.54, 0.25)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.05)), lin((0.10, 0.09, 0.09)))
    hedge = g.mix(g.noise(15.0, 4.0), lin((0.17, 0.32, 0.13)), lin((0.26, 0.42, 0.18)))
    path = g.mix(grain, lin((0.62, 0.60, 0.56)), lin((0.70, 0.68, 0.64)))

    albedo = cc.paint(g, mask, (brick, roof, white, glass, curtain, wood, concrete, grass, sand, team, dark, hedge, path))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        # Cracks in the brick, and soot over the windows: the bands just above their tops.
        crack = g.math('MULTIPLY', mask[0], cc.cracks(g))
        albedo = g.mix(crack, albedo, lin((0.16, 0.12, 0.10)))
        soot = g.math('MAXIMUM', cc.band(g, z, GROUND[1] + 0.02, GROUND[1] + 0.16), cc.band(g, z, UPPER[1] + 0.02, UPPER[1] + 0.16))
        if variant == "abandoned":
            soot = g.math('MAXIMUM', soot, g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 6.0, -2.8)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.75), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        # Soot up from the foot, and the dust of the fall over everything.
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -2.0, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), g.mix(0.25, albedo, lin((0.55, 0.52, 0.48))), lin((0.10, 0.09, 0.09)))

    rough = cc.weighted(g, mask, (0.85, 0.7, 0.5, 0.15, 0.9, 0.75, 0.9, 0.95, 0.95, 0.45, 0.9, 0.9, 0.85))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.3, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', mortar, -1.0), g.noise(60.0, 3.0)), 0.05, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _base(p):
    p.box((-HX - 0.05, YF - 0.05, 0.0), (HX + 0.05, YB + 0.05, FOUND_H), CONCRETE)  # the foundation
    p.box((-0.98, -0.98, 0.0), (0.98, YF - 0.05, 0.02), GRASS)  # the lawn
    p.box((-0.12, -0.98, 0.02), (0.12, YF - 0.34, 0.03), PATH)


def build(variant_id):
    cc.clear(variant_id, ("body", "roof", "team_mask", "base"))
    variant = VARIANTS[variant_id]
    abandoned = variant == "abandoned"
    body = Parts()
    if variant == "ruins":
        _ruins(body)
    else:
        _walls(body, abandoned)
        _openings(body, variant)
        _porch(body)
        if not abandoned:
            _shrubs(body)
    _fence(body, variant)
    if variant in ("occupied", "damaged_occupied"):
        _barrels(body)
        _sandbags(body)
    if variant in ("damaged", "damaged_occupied"):
        for centre, size, turn in BRICKS:
            cc.block(body, centre, size, turn, BRICK)
    if abandoned:
        for centre, size, turn in RUBBLE_ABANDONED:
            cc.block(body, centre, size, turn, BRICK)
        for a, b in RAFTERS_ABANDONED:
            body.bar(a, b, 0.03, DARK)
    parts = [(body, "body")]
    if variant != "ruins":
        roof = Parts()
        _roof(roof, abandoned)
        team = Parts()
        _canopy(team)
        parts += [(roof, "roof"), (team, "team_mask")]
    base = Parts()
    _base(base)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, FOUND_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
