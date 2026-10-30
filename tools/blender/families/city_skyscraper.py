"""City skyscraper: element 19 of the city set (docs/city_elements/19_skyscraper), its states (appearance.md, 4; the states of the
civilian buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry daylight, no
snow.

Footprint 2x2 cells, origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the buildings of
the map (the game squashes it upright, game/data/visuals/models.yaml): a glass lobby and ten storeys to the roof at 8.55, the parapet to
8.75, the antenna to about 9.6: higher than the office (6), as the documents have it, and slimmer. The parts:
    tower     silver steel piers at the corners and between the strips; on every face four vertical strips of dark blue glass from the
              lobby to the roof, a pane to each storey, the sky lighter higher up
    lobby     a glass hall set back under the tower, glass doors and a plain sign board without text over them
    parapet   a band round the edge of the roof: the team zone (prompts.md, 4)
    roof      a light grey roof with a machine room on it
    antenna   a short mast with two crossbars, a node of its own (prompts.md, 4)
    plaza     the paving round it
No emblems, no text. The glass is baked opaque with the rest, as the office's.
Nodes: "body" (the tower, the lobby, the roof's surface, and the barrels, sandbags or wreck of a state), "roof" (the machine room),
"antenna", "team_mask" (the parapet), "base" (the plaza). The abandoned one also has fire_1..3. One GLB per state:
    city_skyscraper                     whole, empty
    city_skyscraper_occupied            whole, ten soldiers inside: a barrel out of the upper front glass, sandbags on the roof and
                                        either side of the doors
    city_skyscraper_damaged             damaged: panes knocked out up and down the strips, the rest cracked, glass on the plaza
    city_skyscraper_damaged_occupied    damaged, occupied: the same, with the barrels and the sandbags
    city_skyscraper_abandoned           critically damaged, its soldiers out: the top storeys of the front right quarter gone, the
                                        steel frame standing in the gap, the antenna down, all the glass out
    city_skyscraper_ruins               destroyed: the steel frame of the lower storeys, floor slabs, heaps, glass on the plaza
Budget 12000 triangles, texture 2048 (docs/city_elements/19_skyscraper/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_skyscraper as csk
    print(csk.run("city_skyscraper"))   # or csk.run_all()
"""

import bmesh
from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 2048
VARIANTS = {
    "city_skyscraper": "intact",
    "city_skyscraper_occupied": "occupied",
    "city_skyscraper_damaged": "damaged",
    "city_skyscraper_damaged_occupied": "damaged_occupied",
    "city_skyscraper_abandoned": "abandoned",
    "city_skyscraper_ruins": "ruins",
}

STEEL, GLASS, LOBBY, DARK, ROOF, PAVE, SIGN, SAND, TEAM, FRAME, CONCRETE = (cc.part(i) for i in range(11))
PARTS = 11

X0 = Y0 = 0.72
WALLS = cc.Facade(X0, Y0)
APRON_H = 0.05
LOBBY_TOP = 1.0
LOBBY_IN = 0.08  # how far the lobby's glass stands back under the tower
STOREY = 0.75
STOREYS = 10
TOP = LOBBY_TOP + STOREY * STOREYS  # 8.5
PARAPET = (TOP - 0.1, TOP + 0.22, 0.04)
STRIPS = (-0.48, -0.16, 0.16, 0.48)
STRIP_HALF = 0.1
CUT_Z = LOBBY_TOP + STOREY * 8  # abandoned: the front right quarter gone above this
FIRE_POINTS = ((0.35, -0.35, CUT_Z + 0.05), (-0.4, -Y0 - 0.02, CUT_Z - 0.4), (-0.3, 0.3, TOP))


def _floor(i):
    """The floor of storey i of the tower, 0 the first over the lobby."""
    return LOBBY_TOP + STOREY * i


def _cut(wall, u, z):
    """Whether the abandoned one has lost the wall at u, z: the front right quarter, over CUT_Z."""
    if z <= CUT_Z:
        return False
    return wall == "front" and u > 0 or wall == "right" and u < 0


# --- the tower ------------------------------------------------------------------------

def _tower(p, abandoned):
    """The tower over the lobby: whole, one solid with its top the roof (repainted); abandoned, a solid up to CUT_Z and an L of
    two solids over it, the front right quarter open, the cut's floor showing."""
    if not abandoned:
        faces = cc.made(p, lambda: p.box((-X0, -Y0, LOBBY_TOP), (X0, Y0, TOP), STEEL))
        cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > TOP - 0.01], ROOF)
        return
    faces = cc.made(p, lambda: p.box((-X0, -Y0, LOBBY_TOP), (X0, Y0, CUT_Z), STEEL))
    cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > CUT_Z - 0.01], CONCRETE)
    for lo, hi in (((-X0, 0.0, CUT_Z), (X0, Y0, TOP)), ((-X0, -Y0, CUT_Z), (0.0, 0.0, TOP))):
        faces = cc.made(p, lambda: p.box(lo, hi, STEEL))
        cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > TOP - 0.01], ROOF)
        cc.repaint(p, [f for f in faces if abs(f.normal.x) > 0.99 or abs(f.normal.y) > 0.99
                       if abs(f.calc_center_median().x) < 0.01 or abs(f.calc_center_median().y) < 0.01], FRAME)  # the cut's sides


def _strips(p, variant):
    """The four strips of glass on each face, a pane to each storey between thin steel lines. Damaged: panes out here and there;
    abandoned: all out, and none where the quarter went."""
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall in ("front", "back", "left", "right"):
        for u in STRIPS:
            for i in range(STOREYS):
                z0, z1 = _floor(i) + 0.03, _floor(i + 1) - 0.03
                if abandoned and _cut(wall, u, z1):
                    continue
                n += 1
                broken = abandoned or damaged and (n * 7 + i * 3) % 5 < 2
                WALLS.box(p, wall, u - STRIP_HALF, u + STRIP_HALF, z0, z1, -0.005, 0.006, DARK if broken else GLASS)
    for x in (-X0, X0):  # the corner piers stand a little proud of the faces
        for y in (-Y0, Y0):
            top = CUT_Z if abandoned and x > 0 and y < 0 else TOP
            p.box((x - 0.05, y - 0.05, LOBBY_TOP), (x + 0.05, y + 0.05, top), STEEL)


def _lobby(p, variant):
    """The glass hall under the tower: glass walls with steel mullions, the doors and the sign on the front."""
    broken = variant in ("damaged", "damaged_occupied", "abandoned")
    hall = cc.Facade(X0 - LOBBY_IN, Y0 - LOBBY_IN)
    faces = cc.made(p, lambda: p.box((-X0 + LOBBY_IN, -Y0 + LOBBY_IN, APRON_H), (X0 - LOBBY_IN, Y0 - LOBBY_IN, LOBBY_TOP), LOBBY))
    bmesh.ops.delete(p.bm, geom=[f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > LOBBY_TOP - 0.01],
                     context='FACES')  # under the tower: shut, it would bake black
    for wall in ("front", "back", "left", "right"):
        for k, u in enumerate(cc.steps(-X0 + LOBBY_IN, X0 - LOBBY_IN, 0.21)):
            hall.box(p, wall, u - 0.012, u + 0.012, APRON_H, LOBBY_TOP, -0.005, 0.02, STEEL)
            if broken and k % 2 == 1 and wall in ("front", "right"):
                hall.box(p, wall, u + 0.015, u + 0.19, 0.15, 0.85, -0.005, 0.004, DARK)
    hall.box(p, "front", -0.2, 0.2, APRON_H, 0.7, -0.005, 0.01, DARK if variant == "abandoned" else LOBBY)  # the doors
    hall.box(p, "front", -0.22, 0.22, 0.7, 0.73, -0.005, 0.025, STEEL)
    if variant != "abandoned":
        hall.box(p, "front", -0.3, 0.3, 0.78, 0.93, -0.005, 0.035, SIGN)  # the sign, no text


def _barrels(p):
    """Ten rifle barrels, one for each soldier inside, out of the two middle front strips of the five upper storeys."""
    for i in range(STOREYS - 5, STOREYS):
        for u in (-0.16, 0.16):
            a = WALLS.point("front", u, _floor(i) + STOREY / 2, -0.02)
            p.bar(a, a + Vector((u * 0.2, -0.4, -0.04)), 0.017, DARK)


def _sandbags(p):
    for t in cc.steps(-X0 + 0.22, X0 - 0.22, 0.27):
        cc.bag(p, (t, -Y0 + 0.2, TOP), 0.0, SAND)
        cc.bag(p, (t, Y0 - 0.2, TOP), 0.0, SAND)
    for t in cc.steps(-Y0 + 0.45, Y0 - 0.45, 0.27):
        cc.bag(p, (-X0 + 0.2, t, TOP), 1.5708, SAND)
        cc.bag(p, (X0 - 0.2, t, TOP), 1.5708, SAND)
    for z, shift in ((APRON_H, 0.0), (APRON_H + 0.08, 0.12)):
        for t in cc.steps(0.3 + shift, 0.7, 0.24):
            for x in (-t, t):
                cc.bag(p, (x, -Y0 - 0.12, z), 0.0, SAND, length=0.22, width=0.14, height=0.08)


# The steel frame left in the cut quarter of the abandoned one: columns (x, y, top), and the slabs and rubble.
FRAME_COLUMNS = ((0.36, -0.36, CUT_Z + 0.65), (0.68, -0.36, CUT_Z + 0.4), (0.36, -0.68, CUT_Z + 0.5), (0.68, -0.68, CUT_Z + 0.2))
SLABS_ABANDONED = (((0.4, -0.4, CUT_Z + 0.06), (0.5, 0.45, 0.05), (0.3, -0.2, 0.2)),)
RUBBLE_ABANDONED = (((0.5, -Y0 - 0.15, APRON_H + 0.1), (0.4, 0.2, 0.18), (0.2, 0.1, 0.3)), ((X0 + 0.14, -0.5, APRON_H + 0.09), (0.2, 0.4, 0.15), (0.1, 0.3, 1.1)),
                    ((0.85, -0.85, APRON_H + 0.07), (0.18, 0.16, 0.12), (0.3, 0.2, 0.6)), ((0.6, -0.95, APRON_H + 0.04), (0.5, 0.06, 0.04), (0.0, 0.1, 0.2)))
SHARDS = tuple(((x, y, APRON_H + 0.005), (0.06 + 0.02 * (k % 3), 0.04 + 0.02 * (k % 2), 0.008), (0.0, 0.0, 0.7 * k))
               for k, (x, y) in enumerate(((-0.6, -0.92), (-0.2, -0.95), (0.3, -0.9), (0.7, -0.93), (0.92, -0.4), (0.93, 0.2),
                                           (-0.93, 0.5), (-0.92, -0.3), (0.1, 0.94), (-0.5, 0.92))))


def _frame(p):
    for x, y, top in FRAME_COLUMNS:
        p.box((x - 0.035, y - 0.035, CUT_Z), (x + 0.035, y + 0.035, top), FRAME)
    for x0, y0, t0 in FRAME_COLUMNS:
        for x1, y1, t1 in FRAME_COLUMNS:
            if (x0, y0) < (x1, y1) and (abs(x0 - x1) < 0.01 or abs(y0 - y1) < 0.01):
                z = CUT_Z + 0.4 - 0.05
                if min(t0, t1) > z:
                    p.bar((x0, y0, z), (x1, y1, z), 0.025, FRAME)
    for x, y, top in FRAME_COLUMNS[:2]:
        p.bar((x, y, CUT_Z + 0.35), (0.0 if x < 0.5 else x, 0.0 if x < 0.5 else y, CUT_Z + 0.35), 0.022, FRAME)


def _ruins(p):
    heights = {(-0.55, 0.55): 2.8, (-0.18, 0.55): 2.2, (0.18, 0.55): 1.8, (0.55, 0.55): 1.3, (-0.55, 0.18): 2.4, (-0.55, -0.18): 1.6,
               (-0.55, -0.55): 1.0, (0.55, -0.55): 0.6, (0.18, -0.55): 0.8}
    for (x, y), h in heights.items():
        p.box((x - 0.035, y - 0.035, APRON_H), (x + 0.035, y + 0.035, h), FRAME)
    for z in (_floor(0), _floor(1), _floor(2)):
        for (x, y), h in heights.items():
            for nx, ny in ((round(x + 0.37, 2), y), (x, round(y + 0.37, 2)), (round(x + 0.36, 2), y)):
                if (nx, ny) in heights and min(h, heights[(nx, ny)]) > z + 0.05:
                    p.bar((x, y, z), (nx, ny, z), 0.025, FRAME)
    p.box((-0.6, 0.15, _floor(0) - 0.07), (-0.1, 0.6, _floor(0)), CONCRETE)
    p.box((-0.6, 0.3, _floor(1) - 0.07), (-0.3, 0.6, _floor(1)), CONCRETE)
    for centre, size, turn in (((0.2, -0.2, 0.25), (0.9, 0.8, 0.4), (0.1, 0.15, 0.3)), ((-0.3, -0.4, 0.2), (0.6, 0.5, 0.3), (0.2, 0.1, 1.0)),
                               ((0.5, 0.3, 0.2), (0.6, 0.6, 0.3), (0.15, 0.2, 0.5)), ((0.3, -0.1, 0.55), (0.8, 0.6, 0.06), (0.35, -0.2, 0.4)),
                               ((-0.1, 0.4, 0.5), (0.7, 0.5, 0.06), (-0.3, 0.25, 1.2))):
        cc.block(p, centre, size, turn, CONCRETE)


# --- roof, antenna and parapet --------------------------------------------------------------

def _roof(p, abandoned):
    if abandoned:
        p.box((-0.5, 0.1, TOP), (-0.05, 0.55, TOP + 0.35), STEEL)
    else:
        p.box((-0.45, -0.25, TOP), (0.3, 0.45, TOP + 0.4), STEEL)  # the machine room
        p.box((-0.2, -0.26, TOP), (0.0, -0.25, TOP + 0.3), DARK)


def _antenna(p, abandoned):
    if abandoned:  # down across the roof that is left
        p.bar((-0.55, 0.45, TOP + 0.02), (0.15, 0.2, TOP + 0.02), 0.015, STEEL)
        return
    base = Vector((0.45, 0.45, TOP))
    p.bar(base, base + Vector((0.0, 0.0, 1.05)), 0.016, STEEL)
    for k, w in ((0.55, 0.16), (0.8, 0.1)):
        p.bar(base + Vector((-w, 0.0, k)), base + Vector((w, 0.0, k)), 0.009, STEEL)


def _parapet(p, abandoned):
    z0, z1, out = PARAPET
    x_end = 0.0 if abandoned else X0 + out
    y_start = 0.0 if abandoned else -Y0 - out
    p.box((-X0 - out, -Y0 - out, z0), (x_end, -Y0 + 0.08, z1), TEAM)
    p.box((-X0 - out, Y0 - 0.08, z0), (X0 + out, Y0 + out, z1), TEAM)
    p.box((-X0 - out, -Y0 + 0.08, z0), (-X0 + 0.08, Y0 - 0.08, z1), TEAM)
    p.box((X0 - 0.08, max(y_start, -Y0 + 0.08), z0), (X0 + out, Y0 - 0.08, z1), TEAM)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Silver steel, dark blue glass with a line at each storey and the sky lighter higher up, light lobby glass, a light grey roof,
    paving, a muted sign, sandbags, a light team zone, rusting steel frame, concrete; per state, cracked glass and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    steel = g.mix(grain, lin((0.66, 0.68, 0.71)), lin((0.74, 0.76, 0.79)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.1)), lin((0.08, 0.14, 0.26)), lin((0.36, 0.48, 0.64)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.2, 2.0), 3.0, -1.6)), glass, lin((0.55, 0.64, 0.74)))  # clouds in it
    lobby = g.mix(grain, lin((0.50, 0.60, 0.66)), lin((0.58, 0.68, 0.74)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.10, 0.10, 0.11)))
    roof = g.mix(g.noise(12.0, 4.0), lin((0.58, 0.58, 0.57)), lin((0.66, 0.66, 0.65)))
    tiles = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 4.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 4.0)), -30.0, 1.5)))
    pave = g.mix(tiles, g.mix(grain, lin((0.62, 0.60, 0.56)), lin((0.70, 0.68, 0.64))), lin((0.45, 0.44, 0.42)))
    sign = g.mix(grain, lin((0.30, 0.26, 0.34)), lin((0.35, 0.31, 0.39)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    frame = g.mix(g.noise(8.0, 3.0), lin((0.24, 0.25, 0.28)), lin((0.36, 0.30, 0.26)))
    concrete = g.mix(grain, lin((0.52, 0.51, 0.49)), lin((0.60, 0.59, 0.57)))
    if variant in ("damaged", "damaged_occupied"):
        glass = g.mix(cc.cracks(g, 14.0), glass, lin((0.75, 0.80, 0.85)))
    albedo = cc.paint(g, mask, (steel, glass, lobby, dark, roof, pave, sign, sand, team, frame, concrete))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        soot = g.math('MULTIPLY', g.math('ADD', mask[0], mask[1]),
                      g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.6, 2.0), 6.0, -3.3 if variant != "abandoned" else -2.8)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.0, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), g.mix(0.2, albedo, lin((0.58, 0.56, 0.52))), lin((0.10, 0.09, 0.09)))
    rough = cc.weighted(g, mask, (0.45, 0.08, 0.12, 0.9, 0.85, 0.85, 0.6, 0.95, 0.45, 0.6, 0.9))
    metallic = cc.weighted(g, mask, (0.35, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.5, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[5], g.math('MULTIPLY', tiles, -1.0), g.noise(60.0, 3.0)), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "roof", "antenna", "team_mask", "base"))
    variant = VARIANTS[variant_id]
    abandoned = variant == "abandoned"
    body = Parts()
    if variant == "ruins":
        _ruins(body)
    else:
        _tower(body, abandoned)
        _strips(body, variant)
        _lobby(body, variant)
    if variant in ("occupied", "damaged_occupied"):
        _barrels(body)
        _sandbags(body)
    if abandoned:
        _frame(body)
        for centre, size, turn in SLABS_ABANDONED + RUBBLE_ABANDONED:
            cc.block(body, centre, size, turn, CONCRETE)
    if variant in ("damaged", "damaged_occupied", "abandoned", "ruins"):
        for centre, size, turn in SHARDS:
            cc.block(body, centre, size, turn, GLASS)
    parts = [(body, "body")]
    if variant != "ruins":
        roof, antenna, team = Parts(), Parts(), Parts()
        _roof(roof, abandoned)
        _antenna(antenna, abandoned)
        _parapet(team, abandoned)
        parts += [(roof, "roof"), (antenna, "antenna"), (team, "team_mask")]
    base = Parts()
    base.box((-0.98, -0.98, 0.0), (0.98, 0.98, APRON_H), PAVE)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, APRON_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, antenna, team_mask and base (fire_1..3 when
    abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
