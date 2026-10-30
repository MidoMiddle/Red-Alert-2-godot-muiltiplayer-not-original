"""City apartment block: element 02 of the city set (docs/city_elements/02_apartment_block), its states (appearance.md, 4; the states
of the civilian buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry
daylight, no snow; not the winter block of the map (building_apartment_block, civil_block.py).

Footprint 3x3 cells, origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the buildings of
the map (the game squashes it upright, game/data/visuals/models.yaml): five storeys of 1.1 to the roof at 5.56, the cornice to 5.84, the
hut and the aerials on the roof to about 6.6. The parts:
    block      five storeys of grey-beige panels with seams, a darker plinth; rows of the same windows, dark blue glass in light
               frames; light balconies with metal railings in two columns on the front and on the back
    entrance   glass doors in a metal frame in the middle of the front, a dark canopy over them, steps
    cornice    a band round the top that stands proud of the walls and rises a little over the roof: the team zone, seen from
               far away (appearance.md, 3)
    roof       a flat roof of bitumen inside the cornice, a stair hut, vent pipes and two aerials
    pavement   a concrete pavement round the block
No emblems, no text.
Nodes: "body" (the walls, windows, balconies, entrance, the roof's surface, and the barrels, sandbags or rubble of a state), "roof"
(the hut, the pipes and the aerials; the abandoned one's roof slab too), "team_mask" (the cornice), "base" (the pavement). The
abandoned one also has fire_1..3. One GLB per state:
    city_apartment_block                    whole, empty
    city_apartment_block_occupied           whole, eight soldiers inside: a barrel out of eight front windows, sandbags on the roof and
                                            at the foot
    city_apartment_block_damaged            damaged, empty: half the windows knocked out, soot round them, cracks, three balconies
                                            fallen to the foot
    city_apartment_block_damaged_occupied   damaged, occupied: the same, with the barrels and the sandbags
    city_apartment_block_abandoned          critically damaged, its soldiers out: the front right corner cut off from the third storey
                                            up, the floors showing, bars out of their edges, a slab hanging, rubble
    city_apartment_block_ruins              destroyed: the back left corner standing two storeys, stumps of the walls, heaps of
                                            panels, slabs and bars
Budget 15000 triangles, texture 2048 (docs/city_elements/02_apartment_block/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_apartment as cab
    print(cab.run("city_apartment_block"))   # or cab.run_all()
"""

import math

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 2048
VARIANTS = {
    "city_apartment_block": "intact",
    "city_apartment_block_occupied": "occupied",
    "city_apartment_block_damaged": "damaged",
    "city_apartment_block_damaged_occupied": "damaged_occupied",
    "city_apartment_block_abandoned": "abandoned",
    "city_apartment_block_ruins": "ruins",
}

PANEL, PLINTH, GLASS, FRAME, BALCONY, METAL, DARK, ROOF, CONCRETE, SAND, TEAM = (cc.part(i) for i in range(11))
PARTS = 11

X0, Y0 = 1.25, 1.15  # the walls stand at x = ±X0, y = ±Y0
WALL_T = 0.15
PAVE_H = 0.06
STOREY = 1.1
STOREYS = 5
TOP = PAVE_H + STOREY * STOREYS  # the top of the walls
ROOF_Z = TOP  # the roof: the top of the solid block (of the slab on the walls when abandoned)
CORNICE = (TOP - 0.2, TOP + 0.14, 0.08)  # bottom, top (a low parapet over the roof), how far it stands out of the walls
WIN = (0.13, 0.35, 0.95)  # half the width; bottom and top above the floor of the storey
COLUMNS = {"front": (-1.04, -0.62, -0.2, 0.2, 0.62, 1.04), "back": (-1.04, -0.62, -0.2, 0.2, 0.62, 1.04),
           "left": (-0.84, -0.42, 0.0, 0.42, 0.84), "right": (-0.84, -0.42, 0.0, 0.42, 0.84)}
BALCONY_COLUMNS = (-0.62, 0.62)
DOOR = 0.24  # half the width of the entrance
# Abandoned: the front right corner gone from about the third storey up. Where the walls break, as (from, to, top) along each wall.
CUT_X, CUT_Y = 0.2, -0.1
BROKEN_FRONT = ((CUT_X, 0.7, 2.6), (0.7, X0, 2.2))
BROKEN_RIGHT = ((-Y0, -0.6, 2.3), (-0.6, CUT_Y, 2.8))
FIRE_POINTS = ((0.7, -0.6, 2.32), (-0.62, -Y0 - 0.02, PAVE_H + 3 * STOREY + 0.6), (X0 - 0.1, 0.3, TOP))


def _floor(i):
    """The floor of storey i (0 the ground one)."""
    return PAVE_H + STOREY * i


# --- placing on the walls --------------------------------------------------------------

def _on_wall(p, wall, u0, u1, z0, z1, d0, d1, painted):
    """A box on the outer face of a wall: u along the wall (x on the front and the back, y on the sides), z up, d out of the wall."""
    if wall == "front":
        lo, hi = (u0, -Y0 - d1, z0), (u1, -Y0 - d0, z1)
    elif wall == "back":
        lo, hi = (u0, Y0 + d0, z0), (u1, Y0 + d1, z1)
    elif wall == "left":
        lo, hi = (-X0 - d1, u0, z0), (-X0 - d0, u1, z1)
    else:
        lo, hi = (X0 + d0, u0, z0), (X0 + d1, u1, z1)
    p.box(lo, hi, painted)


def _wall_top(wall, u, abandoned):
    """How high the wall stands at u: the full height, or lower where the abandoned one broke."""
    if abandoned and wall == "front":
        for a, b, top in BROKEN_FRONT:
            if a <= u <= b:
                return top
    if abandoned and wall == "right":
        for a, b, top in BROKEN_RIGHT:
            if a <= u <= b:
                return top
    return TOP


def _window(p, wall, u, z, broken):
    """Glass (a dark hole when broken) in a light frame of four bars, a sill under it."""
    h, b, t = WIN
    z0, z1 = z + b, z + t
    _on_wall(p, wall, u - h, u + h, z0, z1, -0.01, 0.006, DARK if broken else GLASS)
    _on_wall(p, wall, u - h - 0.03, u + h + 0.03, z1, z1 + 0.03, -0.01, 0.02, FRAME)
    _on_wall(p, wall, u - h - 0.04, u + h + 0.04, z0 - 0.04, z0, -0.01, 0.045, FRAME)
    for a in (u - h - 0.03, u + h):
        _on_wall(p, wall, a, a + 0.03, z0, z1, -0.01, 0.02, FRAME)


def _balcony(p, wall, u, z):
    """A light slab on the wall and a metal railing round it: a top rail on three sides and bars under it."""
    w, d = 0.3, 0.3
    _on_wall(p, wall, u - w, u + w, z, z + 0.06, -0.01, d, BALCONY)
    _on_wall(p, wall, u - w, u + w, z + 0.42, z + 0.45, d - 0.03, d, METAL)
    for a in (u - w, u + w - 0.03):
        _on_wall(p, wall, a, a + 0.03, z + 0.42, z + 0.45, 0.0, d, METAL)
    for k in cc.steps(u - w + 0.015, u + w - 0.015, 0.1):
        _on_wall(p, wall, k - 0.008, k + 0.008, z + 0.06, z + 0.42, d - 0.025, d - 0.01, METAL)


# --- the block --------------------------------------------------------------------

def _walls(p, abandoned):
    """Four walls with a darker plinth; the abandoned one broken at its front right corner. Only the abandoned one is hollow, with
    floors inside: a whole block is one solid, its top the roof, since faces shut inside bake black and take the texture's room."""
    if not abandoned:
        top = [f for f in cc.made(p, lambda: p.box((-X0, -Y0, PAVE_H), (X0, Y0, TOP), PANEL)) if f.normal.z > 0.99
               or f.normal.z < -0.99 and f.calc_center_median().z > PAVE_H + 0.01]
        cc.repaint(p, top, ROOF)
        _plinth(p)
        return
        p.box((-X0, -Y0, PAVE_H), (CUT_X, -Y0 + WALL_T, TOP), PANEL)
        for a, b, top in BROKEN_FRONT:
            p.box((a, -Y0, PAVE_H), (b, -Y0 + WALL_T, top), PANEL)
        p.box((X0 - WALL_T, CUT_Y, PAVE_H), (X0, Y0 - WALL_T, TOP), PANEL)
        for a, b, top in BROKEN_RIGHT:
            p.box((X0 - WALL_T, max(a, -Y0 + WALL_T), PAVE_H), (X0, b, top), PANEL)
    p.box((-X0, Y0 - WALL_T, PAVE_H), (X0, Y0, TOP), PANEL)
    p.box((-X0, -Y0 + WALL_T, PAVE_H), (-X0 + WALL_T, Y0 - WALL_T, TOP), PANEL)
    _plinth(p)
    # The floors inside, seen where the block is broken open; above the cut the corner is gone.
    lo_x, lo_y, hi_x, hi_y = -X0 + WALL_T, -Y0 + WALL_T, X0 - WALL_T, Y0 - WALL_T
    for i in range(1, STOREYS):
        z = _floor(i)
        if z > 2.0:
            p.box((lo_x, lo_y, z - 0.08), (CUT_X, hi_y, z), CONCRETE)
            p.box((CUT_X, CUT_Y, z - 0.08), (hi_x, hi_y, z), CONCRETE)
        else:
            p.box((lo_x, lo_y, z - 0.08), (hi_x, hi_y, z), CONCRETE)


def _plinth(p):
    for wall in ("front", "back"):
        _on_wall(p, wall, -X0 - 0.03, X0 + 0.03, PAVE_H, 0.4, -0.01, 0.03, PLINTH)
    for wall in ("left", "right"):
        _on_wall(p, wall, -Y0, Y0, PAVE_H, 0.4, -0.01, 0.03, PLINTH)


def _openings(p, variant):
    """The windows, the balconies and the entrance. Damaged: half the windows out and three balconies fallen; abandoned: all the
    windows out, nothing where the corner went."""
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall, columns in COLUMNS.items():
        for i in range(STOREYS):
            z = _floor(i)
            for u in columns:
                if wall == "front" and i == 0 and abs(u) < DOOR + 0.1:
                    continue  # the entrance
                if z + WIN[2] + 0.05 > _wall_top(wall, u, abandoned):
                    continue  # the wall is gone here
                n += 1
                _window(p, wall, u, z, abandoned or damaged and n % 2 == 0)
    fallen = {("front", -0.62, 2), ("back", 0.62, 3), ("front", 0.62, 4)} if damaged or abandoned else set()
    for wall in ("front", "back"):
        for i in range(1, STOREYS):
            for u in BALCONY_COLUMNS:
                if (wall, u, i) in fallen or _floor(i) + 0.5 > _wall_top(wall, u, abandoned):
                    continue
                _balcony(p, wall, u, _floor(i))
    _on_wall(p, "front", -DOOR, DOOR, PAVE_H, 0.95, -0.01, 0.01, DARK if abandoned else GLASS)
    for a, b, z0, z1 in ((-DOOR - 0.04, -DOOR, PAVE_H, 0.99), (DOOR, DOOR + 0.04, PAVE_H, 0.99), (-DOOR, DOOR, 0.95, 0.99),
                         (-0.02, 0.02, PAVE_H, 0.95)):
        _on_wall(p, "front", a, b, z0, z1, -0.01, 0.025, METAL)
    _on_wall(p, "front", -DOOR - 0.2, DOOR + 0.2, 1.05, 1.12, -0.01, 0.42, DARK)  # the canopy
    for k, d in enumerate((0.36, 0.24)):
        _on_wall(p, "front", -DOOR - 0.12 + k * 0.05, DOOR + 0.12 - k * 0.05, PAVE_H, PAVE_H + 0.05 * (k + 1), -0.01, d, CONCRETE)


def _barrels(p):
    """Eight rifle barrels, one for each soldier inside, out of the outer front windows of the upper storeys."""
    for i in range(1, STOREYS):
        for u in (-1.04, 1.04):
            z = _floor(i) + (WIN[1] + WIN[2]) / 2
            p.bar((u, -Y0 + 0.02, z), (u * 1.04, -Y0 - 0.42, z - 0.04), 0.018, DARK)


def _sandbags(p):
    """Sandbags along the edge of the roof inside the cornice, and two rows at the foot either side of the entrance."""
    for t in cc.steps(-X0 + 0.3, X0 - 0.3, 0.27):
        cc.bag(p, (t, -Y0 + 0.28, ROOF_Z), 0.0, SAND)
        cc.bag(p, (t, Y0 - 0.28, ROOF_Z), 0.0, SAND)
    for t in cc.steps(-Y0 + 0.5, Y0 - 0.5, 0.27):
        cc.bag(p, (-X0 + 0.28, t, ROOF_Z), math.pi / 2, SAND)
        cc.bag(p, (X0 - 0.28, t, ROOF_Z), math.pi / 2, SAND)
    for z, shift in ((PAVE_H, 0.0), (PAVE_H + 0.08, 0.13)):
        for t in cc.steps(DOOR + 0.35 + shift, X0 - 0.05, 0.26):
            for x in (-t, t):
                cc.bag(p, (x, -Y0 - 0.12, z), 0.0, SAND)


# Balconies fallen to the foot of the damaged one, and the rubble of the abandoned one: middle, size, turn.
FALLEN = (((-0.6, -Y0 - 0.4, PAVE_H + 0.05), (0.6, 0.3, 0.06), (0.15, 0.25, 0.3)), ((0.7, -Y0 - 0.35, PAVE_H + 0.06), (0.6, 0.3, 0.06), (-0.3, 0.1, -0.5)),
          ((0.5, Y0 + 0.35, PAVE_H + 0.05), (0.6, 0.3, 0.06), (0.2, -0.2, 0.2)))
RUBBLE_ABANDONED = (((0.75, -Y0 - 0.15, PAVE_H + 0.12), (0.5, 0.3, 0.22), (0.2, 0.1, 0.3)), ((X0 + 0.12, -0.6, PAVE_H + 0.1), (0.3, 0.5, 0.18), (0.1, 0.3, 1.2)),
                    ((0.95, -0.9, PAVE_H + 0.15), (0.4, 0.4, 0.28), (0.3, 0.2, 0.6)), ((0.6, -0.5, 2.3), (0.4, 0.34, 0.14), (0.2, 0.3, 0.4)),
                    ((0.3, -0.7, 2.28), (0.26, 0.22, 0.1), (0.1, 0.1, 1.1)), ((1.25, -1.3, PAVE_H + 0.08), (0.24, 0.2, 0.14), (0.3, 0.0, 0.2)))
SLABS_ABANDONED = (((0.75, -0.55, 2.9), (0.9, 0.7, 0.08), (0.5, -0.35, 0.3)),)  # a floor hanging into the gap
BARS_ABANDONED = (((CUT_X, -0.4, _floor(3) - 0.04), (CUT_X + 0.2, -0.5, _floor(3) - 0.2)), ((CUT_X, 0.2, _floor(4) - 0.04), (CUT_X + 0.25, 0.1, _floor(4) - 0.15)),
                  ((0.6, CUT_Y, _floor(3) - 0.04), (0.7, CUT_Y - 0.25, _floor(3) - 0.2)), ((1.0, CUT_Y, _floor(4) - 0.04), (1.05, CUT_Y - 0.3, _floor(4) - 0.1)),
                  ((0.5, -Y0, 2.55), (0.55, -Y0 - 0.2, 2.75)), ((X0, -0.4, 2.75), (X0 + 0.2, -0.45, 2.95)))


def _ruins(p):
    """The back left corner standing two storeys with its floors, stumps of the other walls, heaps of panels and slabs, bars."""
    two = _floor(2)
    p.box((-X0, Y0 - WALL_T, PAVE_H), (-0.2, Y0, two + 0.2), PANEL)
    p.box((-X0, 0.1, PAVE_H), (-X0 + WALL_T, Y0 - WALL_T, two + 0.4), PANEL)
    p.box((-X0 + WALL_T, 0.1, _floor(1) - 0.08), (-0.2, Y0 - WALL_T, _floor(1)), CONCRETE)
    p.box((-X0 + WALL_T, 0.4, two - 0.08), (-0.5, Y0 - WALL_T, two), CONCRETE)
    for lo, hi in (((-X0, -Y0, PAVE_H), (-0.3, -Y0 + WALL_T, 0.9)), ((0.4, -Y0, PAVE_H), (X0, -Y0 + WALL_T, 0.5)),
                   ((X0 - WALL_T, -0.3, PAVE_H), (X0, Y0, 0.7)), ((-0.2, Y0 - WALL_T, PAVE_H), (0.8, Y0, 0.55)),
                   ((-X0, -Y0 + WALL_T, PAVE_H), (-X0 + WALL_T, 0.1, 0.6))):
        p.box(lo, hi, PANEL)
    for centre, size, turn in RUINS_HEAPS:
        cc.block(p, centre, size, turn, PANEL)
    for centre, size, turn in RUINS_SLABS:
        cc.block(p, centre, size, turn, CONCRETE)
    for a, b in RUINS_BARS:
        p.bar(a, b, 0.012, METAL)


RUINS_HEAPS = (((0.4, -0.3, 0.3), (1.0, 0.8, 0.5), (0.1, 0.15, 0.3)), ((0.2, 0.5, 0.25), (0.9, 0.6, 0.4), (0.2, 0.1, 1.0)),
               ((-0.5, -0.5, 0.25), (0.7, 0.6, 0.4), (0.15, 0.2, 0.6)), ((0.9, 0.6, 0.2), (0.5, 0.6, 0.3), (0.2, 0.1, 0.2)),
               ((0.9, -0.8, 0.18), (0.5, 0.4, 0.26), (0.1, 0.3, 0.9)), ((-0.9, -1.0, 0.12), (0.4, 0.3, 0.18), (0.2, 0.0, 0.4)))
RUINS_SLABS = (((0.3, -0.1, 0.75), (1.6, 0.9, 0.08), (0.25, -0.2, 0.2)), ((-0.2, 0.3, 1.05), (1.2, 0.8, 0.08), (-0.35, 0.15, 0.6)),
               ((0.8, 0.2, 0.6), (1.0, 0.7, 0.08), (0.1, 0.45, -0.3)), ((-0.6, 0.0, 1.5), (0.9, 0.6, 0.08), (0.5, 0.1, 0.4)))
RUINS_BARS = (((-0.2, 0.4, _floor(2)), (0.05, 0.3, _floor(2) + 0.25)), ((-0.2, 0.8, _floor(1)), (0.1, 0.7, _floor(1) + 0.2)),
              ((0.6, -0.2, 0.9), (0.75, -0.4, 1.2)), ((-0.3, -0.2, 1.1), (-0.1, -0.4, 1.4)), ((0.2, 0.6, 0.6), (0.3, 0.8, 0.95)))


# --- the roof and the cornice ------------------------------------------------------------

def _roof(p, abandoned):
    lo_x, lo_y, hi_x, hi_y = -X0 + WALL_T, -Y0 + WALL_T, X0 - WALL_T, Y0 - WALL_T
    if abandoned:  # the roof slab on the hollow walls; a whole block's roof is the top of its solid
        p.box((lo_x, lo_y, TOP - 0.1), (CUT_X, hi_y, ROOF_Z), ROOF)
        p.box((CUT_X, CUT_Y, TOP - 0.1), (hi_x, hi_y, ROOF_Z), ROOF)
    p.box((-0.75, 0.2, ROOF_Z), (-0.15, 0.8, ROOF_Z + 0.6), PANEL)  # the stair hut
    p.box((-0.55, 0.19, ROOF_Z), (-0.33, 0.2, ROOF_Z + 0.45), DARK)  # its door
    p.box((-0.8, 0.15, ROOF_Z + 0.6), (-0.1, 0.85, ROOF_Z + 0.65), CONCRETE)
    for x, y in ((0.55, 0.6), (0.85, 0.6)) if not abandoned else ((-0.9, -0.6),):
        p.cylinder((x, y, ROOF_Z + 0.15), 'Z', 0.05, 0.15, METAL, sides=10)
    for x, y, h in ((-0.95, 0.75, 0.9), (0.6, 0.85, 0.7)):  # the aerials: a mast and two crossbars
        p.bar((x, y, ROOF_Z), (x, y, ROOF_Z + h), 0.012, METAL)
        for k, w in ((0.75, 0.22), (0.9, 0.15)):
            p.bar((x - w, y, ROOF_Z + h * k), (x + w, y, ROOF_Z + h * k), 0.008, METAL)


def _cornice(p, abandoned):
    """The band round the top of the walls, standing out of them: the team zone."""
    z0, z1, out = CORNICE
    x_end = CUT_X if abandoned else X0 + out
    y_start = CUT_Y if abandoned else -Y0 - out
    p.box((-X0 - out, -Y0 - out, z0), (x_end, -Y0 + WALL_T, z1), TEAM)
    p.box((-X0 - out, Y0 - WALL_T, z0), (X0 + out, Y0 + out, z1), TEAM)
    p.box((-X0 - out, -Y0 + WALL_T, z0), (-X0 + WALL_T, Y0 - WALL_T, z1), TEAM)
    p.box((X0 - WALL_T, max(y_start, -Y0 + WALL_T), z0), (X0 + out, Y0 - WALL_T, z1), TEAM)


def _pavement(p):
    p.box((-1.48, -1.48, 0.0), (1.48, 1.48, PAVE_H), CONCRETE)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Grey-beige panels with seams, a darker plinth, dark blue glass with the sky in it, light frames and balconies, metal, bitumen,
    a pavement of slabs, sandbags, a light team zone; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    # Panels: a seam at every storey and every 0.42 along the wall; x + y runs along every wall.
    level = g.math('FRACT', g.math('MULTIPLY', g.math('SUBTRACT', z, PAVE_H), 1.0 / STOREY))
    across = g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 1.0 / 0.42))
    seam = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', level, 0.5)), 60.0, -29.0)),
                  g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', across, 0.5)), 60.0, -29.0)))
    panel = g.mix(seam, g.mix(g.noise(6.0, 3.0), lin((0.64, 0.61, 0.55)), lin((0.72, 0.69, 0.63))), lin((0.46, 0.44, 0.40)))
    plinth = g.mix(grain, lin((0.42, 0.40, 0.37)), lin((0.50, 0.48, 0.45)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.15)), lin((0.10, 0.15, 0.24)), lin((0.30, 0.42, 0.56)))
    frame = g.mix(grain, lin((0.84, 0.84, 0.82)), lin((0.92, 0.92, 0.90)))
    balcony = g.mix(grain, lin((0.76, 0.75, 0.72)), lin((0.84, 0.83, 0.80)))
    metal = g.mix(grain, lin((0.32, 0.34, 0.37)), lin((0.42, 0.44, 0.47)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.10, 0.10, 0.11)))
    roof = g.mix(g.noise(12.0, 4.0), lin((0.20, 0.20, 0.21)), lin((0.28, 0.28, 0.29)))
    slab = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 3.0)), -30.0, 1.5)),
                  g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 3.0)), -30.0, 1.5)))
    concrete = g.mix(slab, g.mix(grain, lin((0.58, 0.57, 0.54)), lin((0.66, 0.65, 0.62))), lin((0.42, 0.41, 0.39)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = cc.paint(g, mask, (panel, plinth, glass, frame, balcony, metal, dark, roof, concrete, sand, team))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        albedo = g.mix(g.math('MULTIPLY', mask[0], cc.cracks(g, 6.0)), albedo, lin((0.20, 0.19, 0.17)))
        # Soot over the windows of every storey: the band just above their tops, where the smoke came out.
        lift = g.math('SUBTRACT', level, (WIN[2] + 0.02) / STOREY)
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY', lift, 12.0)), g.clamp01(g.math('MULTIPLY_ADD', lift, -12.0, 1.6)))
        soot = g.math('MULTIPLY', soot, g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.5, 2.0), 5.0, -2.0)))
        if variant == "abandoned":
            soot = g.math('MAXIMUM', soot, g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.0, 2.0), 6.0, -2.8)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.0, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), g.mix(0.25, albedo, lin((0.58, 0.56, 0.52))), lin((0.10, 0.09, 0.09)))

    rough = cc.weighted(g, mask, (0.85, 0.9, 0.12, 0.5, 0.8, 0.45, 0.9, 0.9, 0.9, 0.95, 0.45))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.6, 0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', seam, -1.0), g.noise(60.0, 3.0)), 0.05, 0.006)
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
        _walls(body, abandoned)
        _openings(body, variant)
    if variant in ("occupied", "damaged_occupied"):
        _barrels(body)
        _sandbags(body)
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        for centre, size, turn in FALLEN:
            cc.block(body, centre, size, turn, BALCONY)
    if abandoned:
        for centre, size, turn in RUBBLE_ABANDONED:
            cc.block(body, centre, size, turn, PANEL)
        for centre, size, turn in SLABS_ABANDONED:
            cc.block(body, centre, size, turn, CONCRETE)
        for a, b in BARS_ABANDONED:
            body.bar(a, b, 0.012, METAL)
    parts = [(body, "body")]
    if variant != "ruins":
        roof = Parts()
        _roof(roof, abandoned)
        team = Parts()
        _cornice(team, abandoned)
        parts += [(roof, "roof"), (team, "team_mask")]
    base = Parts()
    _pavement(base)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
