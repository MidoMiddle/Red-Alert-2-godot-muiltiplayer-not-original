"""City office: element 04 of the city set (docs/city_elements/04_office), its states (appearance.md, 4; the states of the civilian
buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry daylight, no snow.

Footprint 2x2 cells, origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the buildings of
the map (the game squashes it upright, game/data/visuals/models.yaml): a tall ground floor and six office storeys to the roof at 6.05,
the parapet to 6.25, the aerials to about 6.9: as high as the apartment block or higher (prompts.md, 4). The parts:
    tower      a block of grey-blue concrete with beige corner columns; on every storey a band of dark blue glass panes between thin
               metal mullions, the sky lighter in the panes higher up; the ground floor faced in darker stone
    shopfront  a wide glass shopfront on the front of the ground floor with glass doors in its middle, a plain sign board over it
               without text; glass on the other ground-floor walls
    parapet    a band round the edge of the roof: the team zone (prompts.md, 4), seen from far away
    roof       a light grey flat roof, a machine room, a hatch and two aerials
    plaza      the paving of the square round it
No emblems, no text. The glass is one baked material like the rest (opaque): the prompt's separate transparent glass is left out.
Nodes: "body" (the tower, the glass, the shopfront, the roof's surface, and the barrels, sandbags or rubble of a state), "roof" (what
stands on the roof; the abandoned one's roof slab too), "team_mask" (the parapet), "base" (the plaza). The abandoned one also has
fire_1..3. One GLB per state:
    city_office                     whole, empty
    city_office_occupied            whole, five soldiers inside: a barrel out of the glass of the upper storeys, sandbags on the roof
                                    and either side of the doors
    city_office_damaged             damaged, empty: panes knocked out in patches, the rest cracked, soot, glass on the plaza
    city_office_damaged_occupied    damaged, occupied: the same, with the barrels and the sandbags
    city_office_abandoned           critically damaged, its soldiers out: the front right corner cut off from the third storey up,
                                    the steel frame standing in the gap, the floors showing, all the glass out
    city_office_ruins               destroyed: the steel frame of the lower storeys, floor slabs, heaps of concrete, glass on the
                                    plaza
Budget 10000 triangles, texture 2048 (docs/city_elements/04_office/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_office as cof
    print(cof.run("city_office"))   # or cof.run_all()
"""

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 2048
VARIANTS = {
    "city_office": "intact",
    "city_office_occupied": "occupied",
    "city_office_damaged": "damaged",
    "city_office_damaged_occupied": "damaged_occupied",
    "city_office_abandoned": "abandoned",
    "city_office_ruins": "ruins",
}

CONCRETE, BEIGE, GLASS, METAL, DARK, ROOF, PAVE, SIGN, SAND, TEAM, STEEL = (cc.part(i) for i in range(11))
PARTS = 11

X0 = Y0 = 0.82
WALLS = cc.Facade(X0, Y0)
APRON_H = 0.05
GROUND_TOP = 1.25
STOREY = 0.8
STOREYS = 6  # above the ground floor
TOP = GROUND_TOP + STOREY * STOREYS
PARAPET = (TOP - 0.1, TOP + 0.2, 0.04)  # bottom, top, how far it stands out
BAND = (0.18, 0.62)  # the glass of a storey, above its floor
PANE = 0.26  # about the width of a pane
WALL_T = 0.12
# Abandoned: the front right corner gone from the third storey up; where the walls break, as (from, to, top) along each wall.
CUT_X, CUT_Y = 0.0, 0.0
BROKEN = {"front": ((CUT_X, 0.45, 3.65), (0.45, X0, 3.0)), "right": ((-Y0, -0.4, 3.2), (-0.4, CUT_Y, 3.9))}
COLUMNS_ABANDONED = (((0.4, -0.4), 4.6), ((0.76, -0.4), 3.9), ((0.4, -0.76), 4.2), ((0.76, -0.76), 3.3))
FIRE_POINTS = ((0.55, -0.55, 2.9), (-0.4, -Y0 - 0.02, 4.5), (0.2, 0.4, TOP))


def _floor(i):
    """The floor of storey i: 0 the ground floor, 1..6 the office storeys."""
    return APRON_H if i == 0 else GROUND_TOP + STOREY * (i - 1)


def _wall_top(wall, u, abandoned):
    for a, b, top in BROKEN.get(wall, ()) if abandoned else ():
        if a <= u <= b:
            return top
    return TOP


def _half(wall):
    return X0 if wall in ("front", "back") else Y0


# --- the tower --------------------------------------------------------------------------

def _block(p, abandoned):
    """Whole: one solid, its top the roof (repainted). Abandoned: hollow walls broken at the front right corner, the floors inside
    (above the cut without the corner)."""
    if not abandoned:
        top = [f for f in cc.made(p, lambda: p.box((-X0, -Y0, APRON_H), (X0, Y0, TOP), CONCRETE))
               if abs(f.normal.z) > 0.99 and f.calc_center_median().z > TOP - 0.01]
        cc.repaint(p, top, ROOF)
    else:
        p.box((-X0, Y0 - WALL_T, APRON_H), (X0, Y0, TOP), CONCRETE)
        p.box((-X0, -Y0 + WALL_T, APRON_H), (-X0 + WALL_T, Y0 - WALL_T, TOP), CONCRETE)
        p.box((-X0, -Y0, APRON_H), (CUT_X, -Y0 + WALL_T, TOP), CONCRETE)
        p.box((X0 - WALL_T, CUT_Y, APRON_H), (X0, Y0 - WALL_T, TOP), CONCRETE)
        for a, b, top in BROKEN["front"]:
            p.box((a, -Y0, APRON_H), (b, -Y0 + WALL_T, top), CONCRETE)
        for a, b, top in BROKEN["right"]:
            p.box((X0 - WALL_T, max(a, -Y0 + WALL_T), APRON_H), (X0, b, top), CONCRETE)
        lo, hi = -X0 + WALL_T, X0 - WALL_T
        for i in range(1, STOREYS + 1):
            z = _floor(i)
            if z > 2.8:
                p.box((lo, lo, z - 0.07), (CUT_X, hi, z), CONCRETE)
                p.box((CUT_X, CUT_Y, z - 0.07), (hi, hi, z), CONCRETE)
            else:
                p.box((lo, lo, z - 0.07), (hi, hi, z), CONCRETE)
    for x, y in ((-X0, -Y0), (X0, -Y0), (-X0, Y0), (X0, Y0)):  # the corner columns
        h = 2.8 if abandoned and x > 0 and y < 0 else TOP
        p.box((x - 0.06, y - 0.06, APRON_H), (x + 0.06, y + 0.06, h), BEIGE)


def _glass(p, variant):
    """The bands of glass of the office storeys, a pane between each two mullions, a sill under each band. Damaged: panes out in
    patches; abandoned: all out, and nothing where the walls fell."""
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall in ("front", "back", "left", "right"):
        h = _half(wall) - 0.07
        edges = cc.steps(-h, h, PANE)
        for i in range(1, STOREYS + 1):
            z0, z1 = _floor(i) + BAND[0], _floor(i) + BAND[1]
            for a, b in zip(edges, edges[1:]):
                if z1 > _wall_top(wall, (a + b) / 2, abandoned):
                    continue
                n += 1
                broken = abandoned or damaged and (n * 7 + i) % 5 < 2
                WALLS.box(p, wall, a, b, z0, z1, -0.005, 0.006, DARK if broken else GLASS)
                WALLS.box(p, wall, b - 0.008, b + 0.008, z0, z1, -0.005, 0.016, METAL)
            for a, b, top in _pieces(wall, h, abandoned):  # the sill, on the parts of the wall that stand that high
                if z0 < top:
                    WALLS.box(p, wall, a, b, z0 - 0.03, z0, -0.005, 0.03, METAL)


def _pieces(wall, h, abandoned):
    """The wall from -h to h as (from, to, top): whole, or its standing part and its broken pieces."""
    if not abandoned or wall not in BROKEN:
        return [(-h, h, TOP)]
    cut = CUT_X if wall == "front" else CUT_Y
    return [(-h, cut, TOP)] + [(a, min(b, h), top) for a, b, top in BROKEN[wall]]


def _ground_floor(p, variant):
    """The shopfront with the doors and the sign on the front; glass on the other walls of the ground floor."""
    damaged = variant in ("damaged", "damaged_occupied", "abandoned")
    z0, z1 = APRON_H + 0.08, 0.92
    for k, (a, b) in enumerate(zip(cc.steps(-0.66, 0.66, 0.33), cc.steps(-0.66, 0.66, 0.33)[1:])):
        door = abs((a + b) / 2) < 0.2
        part = DARK if damaged and k % 2 == 0 else (METAL if door else GLASS)
        WALLS.box(p, "front", a, b, APRON_H if door else z0, z1, -0.005, 0.008, part)
        WALLS.box(p, "front", b - 0.012, b + 0.012, APRON_H, z1, -0.005, 0.02, METAL)
    WALLS.box(p, "front", -0.68, 0.68, z1, z1 + 0.03, -0.005, 0.03, METAL)
    WALLS.box(p, "front", -0.68, -0.656, APRON_H, z1, -0.005, 0.02, METAL)
    if not variant == "abandoned":
        WALLS.box(p, "front", -0.6, 0.6, 0.98, 1.16, -0.005, 0.045, SIGN)  # the sign board, no text
    for wall in ("back", "left", "right"):
        edges = cc.steps(-0.62, 0.62, 0.31)
        for k, (a, b) in enumerate(zip(edges, edges[1:])):
            WALLS.box(p, wall, a, b, 0.3, z1, -0.005, 0.006, DARK if damaged and k % 2 else GLASS)
            WALLS.box(p, wall, b - 0.01, b + 0.01, 0.3, z1, -0.005, 0.016, METAL)


def _barrels(p):
    """Five rifle barrels, one for each soldier inside, out of the glass of the upper storeys on the front."""
    for u, i in ((-0.5, 6), (0.5, 6), (0.0, 5), (-0.5, 4), (0.5, 4)):
        a = WALLS.point("front", u, _floor(i) + sum(BAND) / 2, -0.02)
        p.bar(a, a + Vector((u * 0.05, -0.4, -0.04)), 0.017, DARK)


def _sandbags(p):
    """Sandbags along the edge of the roof inside the parapet, and two rows either side of the doors."""
    for t in cc.steps(-X0 + 0.25, X0 - 0.25, 0.27):
        cc.bag(p, (t, -Y0 + 0.22, TOP), 0.0, SAND)
        cc.bag(p, (t, Y0 - 0.22, TOP), 0.0, SAND)
        cc.bag(p, (-X0 + 0.22, t, TOP), 1.5708, SAND)
        cc.bag(p, (X0 - 0.22, t, TOP), 1.5708, SAND)
    for z, shift in ((APRON_H, 0.0), (APRON_H + 0.08, 0.12)):
        for t in cc.steps(0.3 + shift, 0.75, 0.24):
            for x in (-t, t):
                cc.bag(p, (x, -Y0 - 0.12, z), 0.0, SAND, length=0.22, width=0.14, height=0.08)


def _frame(p):
    """The steel frame left standing in the cut corner: columns, beams at the floors between them, broken floor slabs, bars."""
    for (x, y), h in COLUMNS_ABANDONED:
        p.box((x - 0.035, y - 0.035, 2.8), (x + 0.035, y + 0.035, h), STEEL)
    pairs = ((0, 1), (0, 2), (1, 3), (2, 3))
    for i in range(3, STOREYS + 1):
        z = _floor(i)
        for a, b in pairs:
            (pa, ha), (pb, hb) = COLUMNS_ABANDONED[a], COLUMNS_ABANDONED[b]
            if z + 0.05 < min(ha, hb):
                p.bar((pa[0], pa[1], z), (pb[0], pb[1], z), 0.025, STEEL)
        for (x, y), h in COLUMNS_ABANDONED:  # beams back to the floors that stand
            if z + 0.05 < h:
                p.bar((x, y, z), (CUT_X, y, z), 0.022, STEEL)
                p.bar((x, y, z), (x, CUT_Y, z), 0.022, STEEL)
    for centre, size, turn in SLABS_ABANDONED:
        cc.block(p, centre, size, turn, CONCRETE)
    for centre, size, turn in RUBBLE_ABANDONED:
        cc.block(p, centre, size, turn, CONCRETE)


SLABS_ABANDONED = (((0.55, -0.5, 3.0), (0.55, 0.45, 0.06), (0.35, -0.25, 0.2)), ((0.6, -0.45, 2.85), (0.4, 0.5, 0.06), (0.0, 0.0, 0.3)))
RUBBLE_ABANDONED = (((0.6, -Y0 - 0.14, APRON_H + 0.1), (0.4, 0.22, 0.18), (0.2, 0.1, 0.3)), ((X0 + 0.12, -0.5, APRON_H + 0.09), (0.2, 0.4, 0.15), (0.1, 0.3, 1.1)),
                    ((0.9, -0.9, APRON_H + 0.07), (0.16, 0.16, 0.12), (0.3, 0.2, 0.6)))
# Glass on the plaza: middle, size, turn.
SHARDS = tuple(((x, y, APRON_H + 0.005), (0.06 + 0.02 * (k % 3), 0.04 + 0.02 * (k % 2), 0.008), (0.0, 0.0, 0.7 * k))
               for k, (x, y) in enumerate(((-0.6, -0.92), (-0.2, -0.95), (0.3, -0.9), (0.7, -0.93), (0.92, -0.4), (0.93, 0.2),
                                           (-0.93, 0.5), (-0.92, -0.3), (0.1, 0.94), (-0.5, 0.92))))


def _ruins(p):
    """The steel frame of the two lower storeys with floor slabs, heaps of concrete, glass on the plaza."""
    heights = {(-0.75, 0.75): 2.6, (-0.25, 0.75): 2.1, (-0.75, 0.25): 2.3, (0.75, 0.75): 1.6, (-0.75, -0.25): 1.4, (0.25, 0.75): 1.2,
               (-0.75, -0.75): 0.9, (0.75, -0.75): 0.5}
    for (x, y), h in heights.items():
        p.box((x - 0.035, y - 0.035, APRON_H), (x + 0.035, y + 0.035, h), STEEL)
    for z in (_floor(1), _floor(2)):
        for (x, y), h in heights.items():
            for nx, ny in ((x + 0.5, y), (x, y + 0.5)):
                if (nx, ny) in heights and min(h, heights[(nx, ny)]) > z + 0.05:
                    p.bar((x, y, z), (nx, ny, z), 0.025, STEEL)
    p.box((-0.8, 0.2, _floor(1) - 0.07), (-0.2, 0.8, _floor(1)), CONCRETE)
    p.box((-0.8, 0.45, _floor(2) - 0.07), (-0.45, 0.8, _floor(2)), CONCRETE)
    for centre, size, turn in RUINS_HEAPS:
        cc.block(p, centre, size, turn, CONCRETE)
    for a, b in RUINS_BARS:
        p.bar(a, b, 0.02, STEEL)


RUINS_HEAPS = (((0.2, -0.2, 0.25), (0.9, 0.8, 0.4), (0.1, 0.15, 0.3)), ((-0.3, -0.4, 0.2), (0.6, 0.5, 0.3), (0.2, 0.1, 1.0)),
               ((0.5, 0.3, 0.2), (0.6, 0.6, 0.3), (0.15, 0.2, 0.5)), ((0.3, -0.1, 0.55), (0.8, 0.6, 0.06), (0.35, -0.2, 0.4)),
               ((-0.1, 0.5, 0.45), (0.7, 0.5, 0.06), (-0.3, 0.25, 1.2)))
RUINS_BARS = (((0.75, 0.75, 1.6), (0.55, 0.5, 1.2)), ((-0.25, 0.75, 2.1), (0.0, 0.6, 1.8)), ((0.25, 0.75, 1.2), (0.45, 0.4, 0.8)))


# --- the roof and the parapet -----------------------------------------------------------

def _roof(p, abandoned):
    lo, hi = -X0 + WALL_T, X0 - WALL_T
    if abandoned:  # the roof slab on the hollow walls; a whole tower's roof is the top of its solid
        p.box((lo, lo, TOP - 0.1), (CUT_X, hi, TOP), ROOF)
        p.box((CUT_X, CUT_Y, TOP - 0.1), (hi, hi, TOP), ROOF)
    p.box((-0.55, 0.15, TOP), (-0.05, 0.6, TOP + 0.4), CONCRETE)  # the machine room
    p.box((-0.4, 0.14, TOP), (-0.2, 0.15, TOP + 0.3), DARK)
    p.box((0.2, 0.3, TOP), (0.42, 0.52, TOP + 0.06), METAL)  # the hatch
    for x, y, h in ((-0.45, -0.4, 0.75), (0.5, 0.5, 0.6)) if not abandoned else ((-0.45, 0.45, 0.75),):
        p.bar((x, y, TOP), (x, y, TOP + h), 0.012, METAL)
        p.bar((x - 0.12, y, TOP + h * 0.8), (x + 0.12, y, TOP + h * 0.8), 0.008, METAL)


def _parapet(p, abandoned):
    """The band round the edge of the roof, standing a little out of the walls and over the roof: the team zone."""
    z0, z1, out = PARAPET
    x_end = CUT_X if abandoned else X0 + out
    y_start = CUT_Y if abandoned else -Y0 - out
    p.box((-X0 - out, -Y0 - out, z0), (x_end, -Y0 + 0.08, z1), TEAM)
    p.box((-X0 - out, Y0 - 0.08, z0), (X0 + out, Y0 + out, z1), TEAM)
    p.box((-X0 - out, -Y0 + 0.08, z0), (-X0 + 0.08, Y0 - 0.08, z1), TEAM)
    p.box((X0 - 0.08, max(y_start, -Y0 + 0.08), z0), (X0 + out, Y0 - 0.08, z1), TEAM)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Grey-blue concrete over darker stone on the ground floor, beige columns, dark blue glass lighter higher up, metal, light grey
    roof, paving, a muted sign, sandbags, steel, a light team zone; per state, cracked glass and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    stone = g.mix(g.noise(10.0, 3.0), lin((0.36, 0.36, 0.38)), lin((0.44, 0.44, 0.46)))
    grey_blue = g.mix(g.noise(6.0, 3.0), lin((0.56, 0.60, 0.64)), lin((0.63, 0.67, 0.71)))
    concrete = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 40.0 * GROUND_TOP)), grey_blue, stone)  # stone below the first floor
    beige = g.mix(grain, lin((0.76, 0.71, 0.60)), lin((0.83, 0.78, 0.67)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.14)), lin((0.10, 0.16, 0.26)), lin((0.36, 0.48, 0.62)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.5, 2.0), 3.0, -1.6)), glass, lin((0.55, 0.64, 0.74)))  # clouds in it
    metal = g.mix(grain, lin((0.30, 0.32, 0.35)), lin((0.38, 0.40, 0.43)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.10, 0.10, 0.11)))
    roof = g.mix(g.noise(12.0, 4.0), lin((0.58, 0.58, 0.57)), lin((0.66, 0.66, 0.65)))
    tiles = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 4.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 4.0)), -30.0, 1.5)))
    pave = g.mix(tiles, g.mix(grain, lin((0.62, 0.60, 0.56)), lin((0.70, 0.68, 0.64))), lin((0.45, 0.44, 0.42)))
    sign = g.mix(grain, lin((0.22, 0.32, 0.34)), lin((0.27, 0.37, 0.39)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    steel = g.mix(g.noise(8.0, 3.0), lin((0.24, 0.25, 0.28)), lin((0.36, 0.30, 0.26)))  # dark steel, rusting

    if variant in ("damaged", "damaged_occupied"):
        glass = g.mix(cc.cracks(g, 14.0), glass, lin((0.75, 0.80, 0.85)))  # cracks across the panes
    albedo = cc.paint(g, mask, (concrete, beige, glass, metal, dark, roof, pave, sign, sand, team, steel))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        albedo = g.mix(g.math('MULTIPLY', g.math('ADD', mask[0], mask[1]), cc.cracks(g, 6.0)), albedo, lin((0.20, 0.20, 0.21)))
        soot = g.math('MULTIPLY', g.math('ADD', g.math('ADD', mask[0], mask[1]), mask[2]),
                      g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.0, 2.0), 6.0, -3.2 if variant != "abandoned" else -2.8)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.0, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), g.mix(0.2, albedo, lin((0.58, 0.56, 0.52))), lin((0.10, 0.09, 0.09)))

    rough = cc.weighted(g, mask, (0.85, 0.8, 0.08, 0.4, 0.9, 0.85, 0.85, 0.6, 0.95, 0.45, 0.6))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.7, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.7))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[6], g.math('MULTIPLY', tiles, -1.0), g.noise(60.0, 3.0)), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "roof", "team_mask", "base"))
    variant = VARIANTS[variant_id]
    abandoned = variant == "abandoned"
    body = Parts()
    if variant == "ruins":
        _ruins(body)
    else:
        _block(body, abandoned)
        _glass(body, variant)
        _ground_floor(body, variant)
    if variant in ("occupied", "damaged_occupied"):
        _barrels(body)
        _sandbags(body)
    if abandoned:
        _frame(body)
    if variant in ("damaged", "damaged_occupied", "abandoned", "ruins"):
        for centre, size, turn in SHARDS:
            cc.block(body, centre, size, turn, GLASS)
    parts = [(body, "body")]
    if variant != "ruins":
        roof = Parts()
        _roof(roof, abandoned)
        team = Parts()
        _parapet(team, abandoned)
        parts += [(roof, "roof"), (team, "team_mask")]
    base = Parts()
    base.box((-0.98, -0.98, 0.0), (0.98, 0.98, APRON_H), PAVE)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, APRON_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
