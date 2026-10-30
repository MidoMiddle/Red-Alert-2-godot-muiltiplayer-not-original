"""City fire station: element 25 of the city set (docs/city_elements/25_fire_station), its states (appearance.md, 4; the states of the
civilian buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry daylight, no
snow.

Footprint 3x2 cells (x 3, y 2), origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the
buildings of the map (the game squashes it upright, game/data/visuals/models.yaml): a tall garage storey and the firemen's storey over
it to the roof at 3.5, the hose tower at the right front corner to 6.05 (prompts.md, 1: about 6). The parts:
    body       red brick with light joints, a light concrete plinth, a band between the storeys and a coping round the flat grey roof,
               a gutter along the back with a downpipe, a short antenna; small windows in white frames on the ground floor's back and
               side, a side door; the concrete apron in front of the doors
    doors left, doors right   the two big red roller doors for the engines, each a node of its own, so that the game can open them
    tower      the light grey hose-drying tower: narrow windows up its front and its side, a door at its foot, a cap on its top
    garrison windows   the windows of the firemen's storey, the ones the soldiers fire from (prompts.md, 4): a node of their own
    team mask  a broad band over the doors and the parapet round the tower's top (the narrow band of the prompt is too thin to read
               once the game squashes the station upright; the tower's top is seen from afar)
    plaza      the paving round it
No emblems, no text.
Nodes: "body", "doors_left", "doors_right", "tower", "garrison_windows", "team_mask", "base". The abandoned one also has fire_1..3. One
GLB per state:
    city_fire_station                     whole, empty: the doors shut
    city_fire_station_occupied            whole, four soldiers inside: a barrel out of the four upper front windows, sandbags along
                                          the front of the roof
    city_fire_station_damaged             damaged: the left door torn off and lying on the apron, the right one bent out, the tower's
                                          glass knocked out, cracks and soot
    city_fire_station_damaged_occupied    damaged, occupied: the same, with the barrels and the sandbags
    city_fire_station_abandoned           critically damaged, its soldiers out: both doors down, the tower broken off over the roof,
                                          its pieces on the roof and the apron, the upper storey broken open at the left front corner
    city_fire_station_ruins               destroyed: the tower fallen across the plaza in pieces, stumps of the walls, heaps of brick
Budget 10000 triangles, texture 2048 (docs/city_elements/25_fire_station/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_fire_station as cfs
    print(cfs.run("city_fire_station"))   # or cfs.run_all()
"""

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 2048
VARIANTS = {
    "city_fire_station": "intact",
    "city_fire_station_occupied": "occupied",
    "city_fire_station_damaged": "damaged",
    "city_fire_station_damaged_occupied": "damaged_occupied",
    "city_fire_station_abandoned": "abandoned",
    "city_fire_station_ruins": "ruins",
}

BRICK, CONCRETE, DOOR, GLASS, FRAME, DARK, PAVE, APRON, SAND, TEAM, ROOF, METAL, FLOOR = (cc.part(i) for i in range(13))
PARTS = 13

X0 = 1.38
YF, YB = -0.62, 0.86  # the front wall and the back wall
WALLS = cc.Facade(X0, -YF, YB)
PAVE_H = 0.05
PLINTH = 0.2
STOREY_2 = 2.1  # the floor of the firemen's storey
ROOF_Z = 3.5
COPING = 3.66
DOORS = ((-1.12, -0.46), (-0.34, 0.32))  # the two door openings along the front
DOOR_TOP = 1.62
BAND = (1.7, 2.04)  # the owner's band over the doors
UPPER_FRONT = (-0.98, -0.52, -0.06, 0.4)
UPPER_WIN = (2.42, 3.16)
WIN_HALF = 0.13
TOWER = (0.56, 1.24, -0.9, -0.22)  # x0, x1, y0, y1 of the hose tower
TOWER_TOP = 5.8
TOWER_SLITS = (1.0, 2.0, 3.0, 4.0, 5.0)
TOWER_BREAK = 4.3  # where the abandoned one's tower broke off
FIRE_POINTS = ((-0.8, YF - 0.05, 1.0), (0.9, -0.56, TOWER_BREAK), (-0.9, -0.2, ROOF_Z))
OPEN_X = -0.35  # the abandoned one's upper storey is broken open from the left wall to here, at the front


# --- the block -----------------------------------------------------------------------------

def _block(p, abandoned):
    """Whole: one solid, its top the roof. Abandoned: solid to the upper floor, then hollow walls with the left front part broken
    open, the floor and an inner wall showing."""
    if not abandoned:
        faces = cc.made(p, lambda: p.box((-X0, YF, PAVE_H), (X0, YB, ROOF_Z), BRICK))
        cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > ROOF_Z - 0.01], ROOF)
    else:
        faces = cc.made(p, lambda: p.box((-X0, YF, PAVE_H), (X0, YB, STOREY_2), BRICK))
        cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > STOREY_2 - 0.01], FLOOR)
        t = 0.14
        p.box((-X0, YB - t, STOREY_2), (X0, YB, ROOF_Z), BRICK)
        p.box((X0 - t, YF + t, STOREY_2), (X0, YB - t, ROOF_Z), BRICK)
        p.box((-X0, 0.2, STOREY_2), (-X0 + t, YB - t, ROOF_Z - 0.5), BRICK)
        p.box((OPEN_X, YF, STOREY_2), (X0, YF + t, ROOF_Z), BRICK)
        p.box((-0.8, YF, STOREY_2), (OPEN_X, YF + t, 2.7), BRICK)
        p.box((-0.3, YF + t, STOREY_2), (-0.18, YB - t, 3.1), BRICK)  # an inner wall, showing through the gap
        p.box((OPEN_X, YF + t, ROOF_Z - 0.1), (X0 - t, YB - t, ROOF_Z), ROOF)  # the roof that is left
    for wall, (a, b) in (("front", (-X0 - 0.03, X0 + 0.03)), ("back", (-X0 - 0.03, X0 + 0.03)), ("left", (YF, YB)), ("right", (YF, YB))):
        WALLS.box(p, wall, a, b, PAVE_H, PLINTH, -0.01, 0.03, CONCRETE)
        if wall != "front":
            WALLS.box(p, wall, a, b, STOREY_2 - 0.04, STOREY_2 + 0.04, -0.01, 0.03, CONCRETE)
        if not abandoned or wall in ("back", "right"):
            WALLS.box(p, wall, a, b, ROOF_Z - 0.06, COPING, -0.01, 0.05, CONCRETE)
    if abandoned:
        WALLS.box(p, "front", OPEN_X, X0 + 0.03, ROOF_Z - 0.06, COPING, -0.01, 0.05, CONCRETE)
        WALLS.box(p, "left", 0.2, YB, ROOF_Z - 0.56, ROOF_Z - 0.44, -0.01, 0.05, CONCRETE)
    # The jambs and the lintels of the door openings.
    for a, b in DOORS:
        for u in (a - 0.06, b):
            WALLS.box(p, "front", u, u + 0.06, PAVE_H, DOOR_TOP + 0.05, -0.01, 0.04, CONCRETE)
        WALLS.box(p, "front", a - 0.06, b + 0.06, DOOR_TOP, DOOR_TOP + 0.07, -0.01, 0.04, CONCRETE)


def _ground_windows(p, variant):
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall, us in (("back", (-1.0, -0.5, 0.0, 0.5, 1.0)), ("left", (0.45,)), ("right", (0.1, 0.55))):
        for u in us:
            n += 1
            broken = DARK if abandoned or damaged and n % 3 == 0 else None
            WALLS.window(p, wall, u, 0.75, 1.45, 0.12, GLASS, FRAME, bars=(1, 1), broken=broken)
    # The side door of the firemen.
    WALLS.box(p, "left", -0.35, 0.0, PLINTH, 1.4, -0.01, 0.015, DARK if abandoned else DOOR)
    WALLS.box(p, "left", -0.4, 0.05, 1.4, 1.47, -0.01, 0.03, CONCRETE)


def _upper_windows(p, variant):
    """The firemen's storey: the garrison's windows. Damaged: some out; abandoned: out, and none where the front is broken open."""
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall, us in (("front", UPPER_FRONT), ("back", (-1.0, -0.5, 0.0, 0.5, 1.0)), ("left", (-0.2, 0.45)), ("right", (0.1, 0.55))):
        for u in us:
            if abandoned and (wall == "front" and u < OPEN_X + 0.1 or wall == "left" and u < 0.3):
                continue
            n += 1
            broken = DARK if abandoned or damaged and n % 2 == 0 else None
            WALLS.window(p, wall, u, UPPER_WIN[0], UPPER_WIN[1], WIN_HALF, GLASS, FRAME, bars=(1, 1), broken=broken)


def _roof_things(p, abandoned):
    """A gutter along the back with a downpipe at the corner, and a short antenna on the roof."""
    p.box((-X0 - 0.02, YB + 0.05, ROOF_Z - 0.16), (X0 + 0.02, YB + 0.13, ROOF_Z - 0.06), METAL)
    p.box((-X0 + 0.06, YB + 0.05, PLINTH), (-X0 + 0.14, YB + 0.13, ROOF_Z - 0.16), METAL)
    if abandoned:
        return
    p.cylinder((-0.9, 0.5, ROOF_Z + 0.5), 'Z', 0.02, 0.5, METAL, sides=6)
    p.bar(Vector((-1.05, 0.5, ROOF_Z + 0.8)), Vector((-0.75, 0.5, ROOF_Z + 0.8)), 0.012, METAL)
    p.box((0.0, 0.2, ROOF_Z), (0.3, 0.5, ROOF_Z + 0.18), METAL)  # a vent box


def _apron(p):
    p.box((-1.24, -0.98, 0.0), (0.44, YF, PAVE_H + 0.012), APRON)


# --- the doors ------------------------------------------------------------------------

def _door(p, a, b, state):
    """A roller door in its opening: shut; 'bent', its foot pushed out; 'off', the panel down on the apron, the opening dark; 'gone',
    the opening dark and nothing left of it but a strip of the roll at the top."""
    y = YF
    if state != "shut":
        p.box((a, y - 0.004, PAVE_H), (b, y + 0.05, DOOR_TOP), DARK)  # the dark garage inside
    if state == "shut":
        p.box((a, y - 0.025, PAVE_H), (b, y + 0.02, DOOR_TOP), DOOR)
    elif state == "bent":
        rings = []
        for yy, zz in ((y - 0.3, PAVE_H + 0.03), (y - 0.14, 0.75), (y - 0.04, DOOR_TOP)):
            rings.append([Vector((a + 0.02, yy - 0.012, zz)), Vector((b - 0.02, yy - 0.012, zz)),
                          Vector((b - 0.02, yy + 0.012, zz)), Vector((a + 0.02, yy + 0.012, zz))])
        p.loft(rings, DOOR)
    elif state == "off":
        cc.block(p, ((a + b) / 2, y - 0.17, 0.6), (b - a - 0.04, 0.04, 1.1), (-0.3, 0.0, 0.05), DOOR)  # leaning against the wall
    if state in ("off", "gone"):
        p.box((a, y - 0.03, DOOR_TOP - 0.12), (b, y + 0.02, DOOR_TOP), DOOR)  # the roll left at the top


def _doors(variant):
    left, right = Parts(), Parts()
    states = {"intact": ("shut", "shut"), "occupied": ("shut", "shut"), "damaged": ("off", "bent"),
              "damaged_occupied": ("off", "bent"), "abandoned": ("gone", "off")}[variant]
    _door(left, *DOORS[0], states[0])
    _door(right, *DOORS[1], states[1])
    return left, right


# --- the tower ------------------------------------------------------------------------

def _slit(p, face, u, z, broken):
    """A narrow window on the tower's front ('front') or right side ('right'), u along it."""
    x0, x1, y0, y1 = TOWER
    h, top = 0.05, 0.42
    glass = DARK if broken else GLASS
    if face == "front":
        p.box((u - h, y0 - 0.006, z), (u + h, y0 + 0.01, z + top), glass)
        p.box((u - h - 0.03, y0 - 0.03, z - 0.03), (u + h + 0.03, y0 + 0.01, z), FRAME)
        p.box((u - h - 0.02, y0 - 0.02, z + top), (u + h + 0.02, y0 + 0.01, z + top + 0.025), FRAME)
    else:
        p.box((x1 - 0.01, u - h, z), (x1 + 0.006, u + h, z + top), glass)
        p.box((x1 - 0.01, u - h - 0.03, z - 0.03), (x1 + 0.03, u + h + 0.03, z), FRAME)
        p.box((x1 - 0.01, u - h - 0.02, z + top), (x1 + 0.02, u + h + 0.02, z + top + 0.025), FRAME)


def _tower(p, variant):
    """The hose tower; broken: its glass out; abandoned: broken off at TOWER_BREAK, a jagged top and its pieces about."""
    x0, x1, y0, y1 = TOWER
    xm, ym = (x0 + x1) / 2, (y0 + y1) / 2
    abandoned = variant == "abandoned"
    top = TOWER_BREAK if abandoned else TOWER_TOP
    p.box((x0, y0, PAVE_H), (x1, y1, top), CONCRETE)
    p.box((x0 - 0.03, y0 - 0.03, PAVE_H), (x1 + 0.03, y1 + 0.03, PLINTH + 0.05), CONCRETE)
    broken = variant != "intact" and variant != "occupied"
    for z in TOWER_SLITS:
        if z + 0.45 < top:
            _slit(p, "front", xm, z, broken)
            _slit(p, "right", ym, z, broken)
    # The door at its foot, on its right side.
    p.box((x1 - 0.01, ym - 0.14, PLINTH), (x1 + 0.012, ym + 0.14, 0.95), DARK if abandoned else DOOR)
    if abandoned:
        for c, s, t in (((x0 + 0.15, ym, top + 0.12), (0.25, 0.6, 0.3), (0.2, 0.0, 0.3)),
                        ((x1 - 0.12, y0 + 0.15, top + 0.08), (0.22, 0.25, 0.22), (0.0, 0.3, 0.6))):
            cc.block(p, c, s, t, CONCRETE)  # the jagged top
        cc.block(p, (0.2, 0.25, ROOF_Z + 0.16), (0.62, 0.62, 0.9), (1.3, 0.2, 0.5), CONCRETE)  # a piece on the roof
        cc.block(p, (-0.05, -0.76, PAVE_H + 0.15), (0.6, 0.28, 0.3), (0.1, 0.0, 0.3), CONCRETE)  # one on the apron
    else:
        p.box((x0 - 0.05, y0 - 0.05, top), (x1 + 0.05, y1 + 0.05, top + 0.06), CONCRETE)  # the cap


def _tower_parapet(p):
    """The parapet round the tower's top: part of the team zone."""
    x0, x1, y0, y1 = TOWER
    z0, z1, t = TOWER_TOP + 0.06, TOWER_TOP + 0.25, 0.06
    a, b = x0 - 0.05, x1 + 0.05
    c, d = y0 - 0.05, y1 + 0.05
    p.box((a, c, z0), (b, c + t, z1), TEAM)
    p.box((a, d - t, z0), (b, d, z1), TEAM)
    p.box((a, c + t, z0), (a + t, d - t, z1), TEAM)
    p.box((b - t, c + t, z0), (b, d - t, z1), TEAM)


def _band(p):
    """The owner's band over the doors."""
    WALLS.box(p, "front", -1.22, 0.46, BAND[0], BAND[1], -0.01, 0.07, TEAM)


# --- the soldiers ---------------------------------------------------------------------

def _barrels(p):
    """Four rifle barrels, one for each soldier inside, out of the four upper front windows."""
    z = (UPPER_WIN[0] + UPPER_WIN[1]) / 2
    for u in UPPER_FRONT:
        a = WALLS.point("front", u, z, -0.02)
        p.bar(a, a + Vector(((u + 0.3) * 0.08, -0.45, -0.05)), 0.017, DARK)


def _sandbags(p):
    """A row of sandbags along the front of the roof, behind the coping."""
    for k, x in enumerate(cc.steps(-1.2, 0.4, 0.24)):
        cc.bag(p, (x, YF + 0.16, ROOF_Z), 0.0, SAND, length=0.22, width=0.14, height=0.08)
        if k % 2 == 0 and x < 0.3:
            cc.bag(p, (x + 0.12, YF + 0.16, ROOF_Z + 0.075), 0.0, SAND, length=0.22, width=0.14, height=0.08)


# --- the ruins ------------------------------------------------------------------------

def _ruins(p):
    """Stumps of the walls, heaps of brick, the tower fallen across the plaza in three pieces."""
    for lo, hi in (((-X0, 0.45, PAVE_H), (-0.3, YB, 1.2)), ((-X0, YF, PAVE_H), (-X0 + 0.15, 0.45, 0.9)),
                   ((0.4, YB - 0.15, PAVE_H), (X0, YB, 0.7)), ((-1.12 - 0.06, YF, PAVE_H), (-1.12, YF + 0.1, 1.3))):
        p.box(lo, hi, BRICK)
    for c, s, t in (((-0.3, 0.2, 0.28), (2.0, 1.0, 0.5), (0.05, 0.1, 0.1)), ((0.5, 0.1, 0.22), (0.9, 0.8, 0.38), (0.2, 0.1, 0.7)),
                    ((-0.9, -0.25, 0.2), (0.7, 0.6, 0.32), (0.1, 0.2, 1.2)), ((-0.1, -0.3, 0.38), (1.1, 0.5, 0.08), (0.3, -0.2, 0.3))):
        cc.block(p, c, s, t, BRICK)
    x0, x1, y0, y1 = TOWER
    p.box((x0, y0, PAVE_H), (x1, y1, 0.85), CONCRETE)  # the stump of the tower
    for c, s, t in (((0.85, 0.6, 0.3), (1.05, 0.55, 0.55), (0.05, 0.1, 0.25)),
                    ((1.05, -0.02, 0.28), (0.5, 0.5, 0.5), (0.3, 0.2, 0.6))):
        cc.block(p, c, s, t, CONCRETE)
    cc.block(p, (-0.75, -0.78, 0.09), (0.66, 0.36, 0.04), (0.0, 0.1, 0.1), DOOR)  # a piece of a door on the apron


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Red brick with light joints, light grey concrete, red roller doors in slats, dark blue glass in white frames, a dark garage,
    paving, a concrete apron, sandbags, a light team zone, a grey roof, dark metal, a floor of boards; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    course = g.math('MULTIPLY', z, 24.0)
    row = g.math('FRACT', course)
    col = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 7.0), g.math('MULTIPLY', g.math('FLOOR', course), 0.5)))
    mortar = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', row, -12.0, 2.0)), g.clamp01(g.math('MULTIPLY_ADD', col, -25.0, 2.0)))
    brick = g.mix(mortar, g.mix(g.noise(5.0, 2.0), lin((0.50, 0.17, 0.12)), lin((0.60, 0.23, 0.16))), lin((0.82, 0.80, 0.76)))
    concrete = g.mix(grain, lin((0.70, 0.70, 0.69)), lin((0.78, 0.78, 0.77)))
    slat = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 14.0)), -20.0, 1.6))
    door = g.mix(slat, g.mix(grain, lin((0.62, 0.07, 0.06)), lin((0.72, 0.10, 0.08))), lin((0.40, 0.04, 0.04)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.25)), lin((0.10, 0.15, 0.26)), lin((0.30, 0.40, 0.54)))
    frame = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.10, 0.10, 0.11)))
    slabs = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 3.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 3.0)), -30.0, 1.5)))
    pave = g.mix(slabs, g.mix(grain, lin((0.62, 0.60, 0.56)), lin((0.70, 0.68, 0.64))), lin((0.45, 0.44, 0.42)))
    apron = g.mix(g.noise(8.0, 4.0), lin((0.56, 0.56, 0.54)), lin((0.66, 0.66, 0.64)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    roof = g.mix(g.noise(12.0, 4.0), lin((0.42, 0.42, 0.42)), lin((0.50, 0.50, 0.50)))
    metal = g.mix(grain, lin((0.30, 0.31, 0.33)), lin((0.38, 0.39, 0.41)))
    floor = g.mix(g.noise(3.0, 6.0), lin((0.36, 0.26, 0.16)), lin((0.44, 0.32, 0.20)))
    albedo = cc.paint(g, mask, (brick, concrete, door, glass, frame, dark, pave, apron, sand, team, roof, metal, floor))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        walls = g.math('ADD', mask[0], mask[1])
        albedo = g.mix(g.math('MULTIPLY', walls, cc.cracks(g, 6.0)), albedo, lin((0.20, 0.17, 0.15)))
        soot = g.math('MULTIPLY', g.math('ADD', walls, mask[2]),
                      g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.8, 2.0), 6.0, -3.3 if variant != "abandoned" else -2.8)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -0.8, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    rough = cc.weighted(g, mask, (0.85, 0.85, 0.5, 0.1, 0.6, 0.9, 0.9, 0.85, 0.95, 0.45, 0.85, 0.5, 0.8))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.2, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.3, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', mortar, -1.0),
                           g.math('MULTIPLY_ADD', mask[2], g.math('MULTIPLY', slat, -1.0), g.noise(60.0, 3.0))), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "doors_left", "doors_right", "tower", "garrison_windows", "team_mask", "base"))
    variant = VARIANTS[variant_id]
    abandoned = variant == "abandoned"
    body = Parts()
    parts = [(body, "body")]
    if variant == "ruins":
        _ruins(body)
    else:
        _block(body, abandoned)
        _ground_windows(body, variant)
        _roof_things(body, abandoned)
        left, right = _doors(variant)
        tower, windows, team = Parts(), Parts(), Parts()
        _tower(tower, variant)
        _upper_windows(windows, variant)
        _band(team)
        if not abandoned:
            _tower_parapet(team)
        parts += [(left, "doors_left"), (right, "doors_right"), (tower, "tower"), (windows, "garrison_windows"), (team, "team_mask")]
        if variant in ("occupied", "damaged_occupied"):
            _barrels(body)
            _sandbags(body)
    base = Parts()
    base.box((-1.48, -0.98, 0.0), (1.48, 0.98, PAVE_H), PAVE)
    _apron(base)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, doors_left, doors_right, tower, garrison_windows,
    team_mask and base (fire_1..3 when abandoned; the tower's parapet fell with its top then, the band over the doors stays)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
