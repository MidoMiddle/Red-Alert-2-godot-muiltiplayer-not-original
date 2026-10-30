"""City church: element 05 of the city set (docs/city_elements/05_church), its states (appearance.md, 4; the states of the civilian
buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry daylight, no snow.

Footprint 2x3 cells (x 2, y 3), origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the
buildings of the map (the game squashes it upright, game/data/visuals/models.yaml): the nave's eaves at 1.5 and its ridge at 2.5, the
bell tower to 5.0, no higher (prompts.md, 4). The yard and its fence are not in the model: fence sections come from the fence set.
The parts:
    nave      white plastered walls with a cream plinth and cornice, tall arched windows in cream frames down both sides, a steep
              roof of dark tiles with white gables, a round apse at the back under a cone of the same tiles
    tower     a dark red brick tower in front of the nave: an arched door at its foot with steps, a clock on each of its four sides,
              a cream cornice, and over it an open belfry of four brick piers with a bronze bell inside
    spire     the pyramid roof of the tower: the team zone, seen from far away (a band on the tower, as the prompt has it, is too
              thin to read once the game squashes the church upright)
    paving    a stone paving round it
No emblems, no religious symbols, no text (prompts.md, 4).
Nodes: "body" (the nave, its windows, the apse, and the barrels, sandbags or rubble of a state), "roof" (the nave's roof and the
apse's cone), "tower" (the tower with its clocks, belfry and bell: a node of its own, for its stump in the ruins), "team_mask" (the
spire), "base" (the paving). The abandoned one also has fire_1..3. One GLB per state:
    city_church                     whole, empty
    city_church_occupied            whole, four soldiers inside: a barrel out of the two front windows on each side, sandbags on the
                                    steps
    city_church_damaged             damaged, empty: windows knocked out, cracks, a length of the cornice broken off, the clocks
                                    stopped at another hour, bits at the foot
    city_church_damaged_occupied    damaged, occupied: the same, with the barrels and the sandbags
    city_church_abandoned           critically damaged, its soldiers out: the tower leaning forward, the back half of the nave's
                                    roof fallen in with its walls broken, rafters and rubble
    city_church_ruins               destroyed: the stump of the tower standing out of the ruins (appearance.md, 4), low stumps of
                                    the nave, heaps of plaster and tiles, beams
Budget 10000 triangles, texture 2048 (docs/city_elements/05_church/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_church as cch
    print(cch.run("city_church"))   # or cch.run_all()
"""

import math

import bmesh
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 2048
VARIANTS = {
    "city_church": "intact",
    "city_church_occupied": "occupied",
    "city_church_damaged": "damaged",
    "city_church_damaged_occupied": "damaged_occupied",
    "city_church_abandoned": "abandoned",
    "city_church_ruins": "ruins",
}

PLASTER, CREAM, BRICK, ROOF, GLASS, WOOD, CONCRETE, DARK, BRONZE, CLOCK, SAND, TEAM = (cc.part(i) for i in range(12))
PARTS = 12

PAVE_H = 0.05
NAVE_X = 0.6  # the side walls at ±NAVE_X
NAVE_Y = (-0.85, 1.15)  # the front and the back wall of the nave
EAVE, RIDGE = 1.5, 2.5
SIDES = cc.Facade(NAVE_X, 1.0)  # used for the side walls only: u runs along y there
WINDOWS_Y = (-0.45, 0.05, 0.55)
WIN = (0.45, 1.22, 0.11)  # bottom, top of the arch, half the width
WALL_T = 0.12
APSE = (Vector((0.0, NAVE_Y[1], 0.0)), 0.42)  # centre, radius
TOWER_C = Vector((0.0, -1.04, 0.0))
TOWER_H = 0.41  # half the side of the tower
SHAFT_TOP = 3.75
BELFRY_TOP = 4.45
SPIRE_TOP = 5.0
CLOCK_Z = 3.25
LEAN = math.radians(7.0)  # how far the abandoned one's tower leans forward
FIRE_POINTS = ((0.0, 0.7, 1.0), (NAVE_X + 0.02, 0.05, 1.0), (0.0, -0.2, RIDGE - 0.1))


# --- shapes on a wall --------------------------------------------------------------------

def _arch(p, wall, u, z0, z1, half, d0, d1, painted, segs=6):
    """A prism of an arched outline on a side wall of the nave: straight sides up to the spring of a round top."""
    spring = z1 - half
    outline = [(u - half, z0), (u + half, z0)]
    outline += [(u + half * math.cos(math.pi * k / segs), spring + half * math.sin(math.pi * k / segs)) for k in range(segs + 1)]
    p.loft([[SIDES.point(wall, a, b, d) for a, b in outline] for d in (d0, d1)], painted)


def _arched_window(p, wall, u, broken):
    """Dark glass (a hole when broken) on a slightly larger cream frame of the same arch, and a sill."""
    z0, z1, half = WIN
    _arch(p, wall, u, z0 - 0.03, z1 + 0.03, half + 0.03, -0.01, 0.014, CREAM)
    _arch(p, wall, u, z0, z1, half, -0.01, 0.02, DARK if broken else GLASS)
    SIDES.box(p, wall, u - half - 0.05, u + half + 0.05, z0 - 0.07, z0 - 0.03, -0.01, 0.045, CREAM)


# --- the nave ---------------------------------------------------------------------------

def _nave(p, variant):
    """The walls. Whole: a solid, its top deleted (the roof covers it; shut, it would bake black). Abandoned: hollow walls, the back
    half broken where the roof fell, the floor and the apse's broken wall inside."""
    abandoned = variant == "abandoned"
    y0, y1 = NAVE_Y
    if not abandoned:
        faces = cc.made(p, lambda: p.box((-NAVE_X, y0, PAVE_H), (NAVE_X, y1, EAVE), PLASTER))
        bmesh.ops.delete(p.bm, geom=[f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > EAVE - 0.01],
                         context='FACES')
        _gable(p, y0 - 0.012, y0 + 0.04)
        _gable(p, y1 - 0.04, y1 + 0.012)
        p.cylinder((APSE[0].x, APSE[0].y, (PAVE_H + 1.2) / 2), 'Z', APSE[1], (1.2 - PAVE_H) / 2, PLASTER, sides=16)
    else:
        mid = 0.3
        p.box((-NAVE_X, y0, PAVE_H), (-NAVE_X + WALL_T, mid, EAVE), PLASTER)
        p.box((NAVE_X - WALL_T, y0, PAVE_H), (NAVE_X, mid, EAVE), PLASTER)
        p.box((-NAVE_X, mid, PAVE_H), (-NAVE_X + WALL_T, y1, 0.95), PLASTER)
        p.box((NAVE_X - WALL_T, mid, PAVE_H), (NAVE_X, 0.75, 1.15), PLASTER)
        p.box((NAVE_X - WALL_T, 0.75, PAVE_H), (NAVE_X, y1, 0.6), PLASTER)
        p.box((-NAVE_X, y1 - WALL_T, PAVE_H), (-0.1, y1, 0.8), PLASTER)
        p.box((-NAVE_X + WALL_T, y0, PAVE_H), (NAVE_X - WALL_T, y0 + WALL_T, EAVE), PLASTER)
        _gable(p, y0 - 0.012, y0 + 0.04)
        p.box((-NAVE_X + WALL_T, y0 + WALL_T, PAVE_H), (NAVE_X - WALL_T, y1 - WALL_T, PAVE_H + 0.03), CONCRETE)  # the floor
    # The plinth and the cornice down both sides.
    for wall in ("left", "right"):
        SIDES.box(p, wall, y0, y1, PAVE_H, 0.22, -0.01, 0.03, CREAM)
        top = 0.3 if abandoned else y1
        if variant in ("damaged", "damaged_occupied") and wall == "right":
            SIDES.box(p, wall, y0, 0.2, EAVE - 0.1, EAVE, -0.01, 0.05, CREAM)  # a length of it broken off
            SIDES.box(p, wall, 0.65, y1, EAVE - 0.1, EAVE, -0.01, 0.05, CREAM)
        else:
            SIDES.box(p, wall, y0, top, EAVE - 0.1, EAVE, -0.01, 0.05, CREAM)


def _gable(p, ya, yb):
    """A white gable end of the nave, over the eaves up to the ridge."""
    p.loft([[Vector((-NAVE_X, y, EAVE)), Vector((NAVE_X, y, EAVE)), Vector((0.0, y, RIDGE - 0.06))] for y in (ya, yb)], PLASTER)


def _windows(p, variant):
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall in ("left", "right"):
        for u in WINDOWS_Y:
            if abandoned and u > 0.3:
                continue  # the walls that fell
            n += 1
            _arched_window(p, wall, u, abandoned or damaged and n % 2 == 1)


def _barrels(p):
    """Four rifle barrels, one for each soldier inside, out of the two front windows on each side."""
    z = (WIN[0] + WIN[1]) / 2 - 0.05
    for wall in ("left", "right"):
        for u in WINDOWS_Y[:2]:
            a = SIDES.point(wall, u, z, -0.02)
            p.bar(a, a + SIDES.out(wall) * 0.42 + Vector((0.0, -0.04, -0.04)), 0.017, DARK)


def _sandbags(p):
    """Sandbags on the steps either side of the tower's door, two rows."""
    y = TOWER_C.y - TOWER_H - 0.16
    for z, shift in ((PAVE_H + 0.08, 0.0), (PAVE_H + 0.16, 0.1)):
        for t in cc.steps(0.24 + shift, 0.5, 0.22):
            for x in (-t, t):
                cc.bag(p, (x, y, z), 0.0, SAND, length=0.2, width=0.13, height=0.08)


# --- the tower -------------------------------------------------------------------------

def _tower(p, variant):
    """The brick shaft, its door and steps, its clocks and cornice, the open belfry with its bell. Ruins: a broken stump."""
    c, h = TOWER_C, TOWER_H
    if variant == "ruins":
        p.box((c.x - h, c.y - h, PAVE_H), (c.x + h, c.y + h, 2.2), BRICK)
        p.box((c.x - h, c.y - h, 2.2), (c.x, c.y, 2.65), BRICK)  # a corner left standing higher
        p.box((c.x - h, c.y, 2.2), (c.x - h + 0.14, c.y + h, 2.45), BRICK)
        return
    p.box((c.x - h, c.y - h, PAVE_H), (c.x + h, c.y + h, SHAFT_TOP), BRICK)
    p.box((c.x - h - 0.04, c.y - h - 0.04, SHAFT_TOP - 0.1), (c.x + h + 0.04, c.y + h + 0.04, SHAFT_TOP), CREAM)  # the cornice
    face = c.y - h
    # The arched door at its foot, its frame, and two steps.
    def arch(half, top):
        return [(-half, PAVE_H), (half, PAVE_H)] + [(half * math.cos(math.pi * k / 6), top - half + half * math.sin(math.pi * k / 6))
                                                    for k in range(7)]

    p.loft([[Vector((x, face - d, z)) for x, z in arch(0.21, 0.93)] for d in (-0.01, 0.014)], CREAM)
    p.loft([[Vector((x, face - d, z)) for x, z in arch(0.17, 0.89)] for d in (-0.01, 0.022)], DARK if variant == "abandoned" else WOOD)
    for k, d in enumerate((0.26, 0.14)):
        p.box((-0.34 + 0.05 * k, face - d, PAVE_H), (0.34 - 0.05 * k, face + 0.01, PAVE_H + 0.05 * (k + 1)), CONCRETE)
    # A clock on each side: a cream face in a dark ring, two hands. Damaged ones stopped at another hour.
    stopped = variant in ("damaged", "damaged_occupied", "abandoned")
    for k in range(4):
        out = Vector((math.sin(k * math.pi / 2), -math.cos(k * math.pi / 2), 0.0))
        centre = c + out * h + Vector((0.0, 0.0, CLOCK_Z))
        axis = 'Y' if k % 2 == 0 else 'X'
        p.cylinder(centre + out * 0.012, axis, 0.19, 0.012, DARK, sides=20)
        p.cylinder(centre + out * 0.028, axis, 0.165, 0.006, CLOCK, sides=20)
        hand = centre + out * 0.04
        side = Vector((out.y, -out.x, 0.0))
        long, short = ((-0.1, -0.06), (0.06, 0.07)) if stopped else ((0.0, 0.13), (0.09, -0.03))  # (across, up) of each hand
        p.bar(hand, hand + side * long[0] + Vector((0, 0, long[1])), 0.008, DARK)
        p.bar(hand, hand + side * short[0] + Vector((0, 0, short[1])), 0.01, DARK)
    # The belfry: four brick piers at the corners, a cream band over the openings, the floor is the shaft's top; the bell inside.
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = c.x + sx * (h - 0.07), c.y + sy * (h - 0.07)
            p.box((x - 0.07, y - 0.07, SHAFT_TOP), (x + 0.07, y + 0.07, BELFRY_TOP), BRICK)
    p.box((c.x - h, c.y - h, BELFRY_TOP - 0.12), (c.x + h, c.y + h, BELFRY_TOP), CREAM)
    if variant != "abandoned":
        p.turned((c.x, c.y, SHAFT_TOP + 0.08), 'Z', [(0.0, 0.17), (0.04, 0.16), (0.16, 0.11), (0.3, 0.08), (0.34, 0.03)], BRONZE, sides=12)
        p.bar((c.x, c.y, SHAFT_TOP + 0.42), (c.x, c.y, BELFRY_TOP - 0.12), 0.012, DARK)


def _spire(p):
    """The pyramid roof of the tower, over the eaves of the belfry: the team zone."""
    c, h = TOWER_C, TOWER_H + 0.07
    base = [Vector((c.x + sx * h, c.y + sy * h, BELFRY_TOP)) for sx, sy in ((1, -1), (1, 1), (-1, 1), (-1, -1))]
    tip = [Vector((c.x + sx * 0.02, c.y + sy * 0.02, SPIRE_TOP)) for sx, sy in ((1, -1), (1, 1), (-1, 1), (-1, -1))]
    p.loft([base, tip], TEAM)


def _lean(p):
    """Leans what p holds forward, about the front edge of the tower's foot."""
    pivot = Vector((0.0, TOWER_C.y - TOWER_H, PAVE_H))
    bmesh.ops.rotate(p.bm, verts=list(p.bm.verts), cent=pivot, matrix=Matrix.Rotation(LEAN, 3, 'X') @ Matrix.Rotation(0.05, 3, 'Y'))


# --- the roof ---------------------------------------------------------------------------

def _roof(p, abandoned):
    """The nave's roof, its open bottom on the walls, and the apse's cone. Abandoned: the back half gone."""
    y0, y1 = NAVE_Y[0] - 0.06, (0.3 if abandoned else NAVE_Y[1] + 0.06)
    half = NAVE_X + 0.1
    eave = EAVE - 0.1 * (RIDGE - EAVE) / NAVE_X
    faces = cc.made(p, lambda: p.loft([[Vector((-half, y, eave)), Vector((half, y, eave)), Vector((half, y, eave + 0.06)),
                                        Vector((0.0, y, RIDGE + 0.06)), Vector((-half, y, eave + 0.06))] for y in (y0, y1)], ROOF))
    bmesh.ops.delete(p.bm, geom=[f for f in faces if f.normal.z < -0.99], context='FACES')
    if not abandoned:
        p.turned((APSE[0].x, APSE[0].y, 1.2), 'Z', [(0.0, APSE[1] + 0.06), (0.05, APSE[1] + 0.06), (0.55, 0.02)], ROOF, sides=16)


# --- rubble ---------------------------------------------------------------------------

BITS = (((-NAVE_X - 0.12, -0.3, PAVE_H + 0.03), (0.1, 0.08, 0.05), (0.2, 0.1, 0.4)), ((NAVE_X + 0.13, 0.4, PAVE_H + 0.03), (0.14, 0.06, 0.05), (0.1, 0.3, 1.2)),
        ((NAVE_X + 0.1, -0.1, PAVE_H + 0.03), (0.3, 0.07, 0.06), (0.0, 0.1, 0.1)), ((0.3, -1.6, PAVE_H + 0.03), (0.08, 0.06, 0.05), (0.0, 0.2, 2.0)))
RUBBLE_ABANDONED = (((0.0, 0.8, PAVE_H + 0.12), (0.7, 0.5, 0.2), (0.1, 0.15, 0.3)), ((0.3, 0.5, PAVE_H + 0.08), (0.3, 0.3, 0.14), (0.3, 0.1, 1.0)),
                    ((NAVE_X + 0.12, 0.9, PAVE_H + 0.1), (0.2, 0.4, 0.16), (0.1, 0.3, 0.6)), ((0.0, 1.45, PAVE_H + 0.1), (0.5, 0.3, 0.16), (0.2, 0.1, 0.2)))
TILES_ABANDONED = (((-0.15, 0.7, 0.5), (0.7, 0.5, 0.04), (0.5, -0.3, 0.2)), ((0.3, 0.9, 0.42), (0.5, 0.45, 0.04), (-0.4, 0.3, 0.9)))
RAFTERS_ABANDONED = (((0.0, 0.3, RIDGE), (0.25, 0.75, 1.6)), ((-0.5, 0.3, EAVE + 0.1), (-0.3, 0.8, 0.9)), ((0.5, 0.3, EAVE + 0.1), (0.35, 0.9, 1.0)),
                     ((0.0, 0.3, RIDGE), (-0.3, 0.7, 1.4)))
RUINS_STUMPS = (((-NAVE_X, -0.85, PAVE_H), (-NAVE_X + WALL_T, 0.2, 0.9)), ((NAVE_X - WALL_T, -0.85, PAVE_H), (NAVE_X, -0.2, 0.6)),
                ((NAVE_X - WALL_T, 0.3, PAVE_H), (NAVE_X, 1.15, 0.4)), ((-NAVE_X, 0.5, PAVE_H), (-NAVE_X + WALL_T, 1.15, 0.5)),
                ((-0.4, 1.03, PAVE_H), (0.3, 1.15, 0.7)))
RUINS_HEAPS = (((0.0, 0.1, 0.2), (0.9, 0.9, 0.3), (0.1, 0.15, 0.3)), ((0.1, 0.8, 0.18), (0.7, 0.6, 0.26), (0.2, 0.1, 1.0)),
               ((-0.2, -0.5, 0.16), (0.6, 0.5, 0.22), (0.15, 0.2, 0.6)), ((0.0, -1.5, 0.12), (0.5, 0.3, 0.16), (0.2, 0.1, 0.4)))
RUINS_TILES = (((0.1, 0.3, 0.42), (0.9, 0.6, 0.04), (0.3, -0.2, 0.3)), ((-0.2, 0.9, 0.36), (0.7, 0.5, 0.04), (-0.35, 0.25, 1.1)))
RUINS_BEAMS = (((-0.5, 0.6, 0.8), (0.2, 0.2, 0.25)), ((0.4, -0.3, 0.55), (-0.1, 0.4, 0.2)))


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """White plaster, cream trim, dark red brick, dark tiles, dark glass, wood, concrete, a bronze bell, clock faces, sandbags, a
    light team zone; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    plaster = g.mix(g.noise(5.0, 3.0), lin((0.88, 0.87, 0.83)), lin((0.94, 0.93, 0.89)))
    cream = g.mix(grain, lin((0.84, 0.78, 0.64)), lin((0.90, 0.84, 0.70)))
    course = g.math('MULTIPLY', z, 26.0)
    row = g.math('FRACT', course)
    col = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 7.0), g.math('MULTIPLY', g.math('FLOOR', course), 0.5)))
    mortar = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', row, -12.0, 2.0)), g.clamp01(g.math('MULTIPLY_ADD', col, -25.0, 2.0)))
    brick = g.mix(mortar, g.mix(g.noise(5.0, 2.0), lin((0.42, 0.18, 0.14)), lin((0.52, 0.24, 0.18))), lin((0.62, 0.58, 0.52)))
    tile_row = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 20.0)), -8.0, 1.5))
    roof = g.mix(tile_row, g.mix(g.noise(8.0, 2.0), lin((0.24, 0.22, 0.22)), lin((0.32, 0.29, 0.28))), lin((0.12, 0.11, 0.11)))
    glass = g.mix(g.noise(9.0, 2.0), lin((0.16, 0.22, 0.30)), lin((0.26, 0.32, 0.40)))
    wood = g.mix(g.noise(3.0, 6.0), lin((0.30, 0.19, 0.11)), lin((0.38, 0.25, 0.15)))
    concrete = g.mix(grain, lin((0.56, 0.55, 0.52)), lin((0.64, 0.63, 0.60)))
    dark = g.mix(grain, lin((0.06, 0.06, 0.06)), lin((0.11, 0.10, 0.10)))
    bronze = g.mix(grain, lin((0.46, 0.34, 0.18)), lin((0.56, 0.42, 0.22)))
    clock = lin((0.94, 0.92, 0.86))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    # The paving: square stones.
    stones = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 5.0)), -30.0, 1.5)),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 5.0)), -30.0, 1.5)))
    concrete = g.mix(g.math('MULTIPLY', stones, g.clamp01(g.math('MULTIPLY_ADD', z, -100.0, 6.0))), concrete, lin((0.42, 0.41, 0.39)))

    albedo = cc.paint(g, mask, (plaster, cream, brick, roof, glass, wood, concrete, dark, bronze, clock, sand, team))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        walls = g.math('ADD', mask[0], mask[2])
        albedo = g.mix(g.math('MULTIPLY', walls, cc.cracks(g, 7.0)), albedo, lin((0.22, 0.20, 0.18)))
        soot = g.math('MULTIPLY', walls, g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.0, 2.0), 6.0, -3.2 if variant != "abandoned" else -2.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.75), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.0, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), g.mix(0.2, albedo, lin((0.60, 0.58, 0.54))), lin((0.10, 0.09, 0.09)))

    rough = cc.weighted(g, mask, (0.85, 0.7, 0.85, 0.7, 0.15, 0.7, 0.9, 0.9, 0.35, 0.3, 0.95, 0.45))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.2, 0.8, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[2], g.math('MULTIPLY', mortar, -1.0), g.noise(60.0, 3.0)), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "roof", "tower", "team_mask", "base"))
    variant = VARIANTS[variant_id]
    abandoned = variant == "abandoned"
    body = Parts()
    if variant == "ruins":
        for lo, hi in RUINS_STUMPS:
            body.box(lo, hi, PLASTER)
        for centre, size, turn in RUINS_HEAPS:
            cc.block(body, centre, size, turn, PLASTER)
        for centre, size, turn in RUINS_TILES:
            cc.block(body, centre, size, turn, ROOF)
        for a, b in RUINS_BEAMS:
            body.bar(a, b, 0.035, DARK)
    else:
        _nave(body, variant)
        _windows(body, variant)
    if variant in ("occupied", "damaged_occupied"):
        _barrels(body)
        _sandbags(body)
    if variant in ("damaged", "damaged_occupied"):
        for centre, size, turn in BITS:
            cc.block(body, centre, size, turn, CREAM)
    if abandoned:
        for centre, size, turn in RUBBLE_ABANDONED:
            cc.block(body, centre, size, turn, PLASTER)
        for centre, size, turn in TILES_ABANDONED:
            cc.block(body, centre, size, turn, ROOF)
        for a, b in RAFTERS_ABANDONED:
            body.bar(a, b, 0.03, DARK)
    tower = Parts()
    _tower(tower, variant)
    parts = [(body, "body")]
    if variant != "ruins":
        roof = Parts()
        _roof(roof, abandoned)
        spire = Parts()
        _spire(spire)
        if abandoned:
            _lean(tower)
            _lean(spire)
        parts += [(roof, "roof"), (tower, "tower"), (spire, "team_mask")]
    else:
        parts.append((tower, "tower"))
    base = Parts()
    base.box((-0.98, -1.48, 0.0), (0.98, 1.48, PAVE_H), CONCRETE)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, tower, team_mask and base (fire_1..3 when
    abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
