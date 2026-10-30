"""City car repair garage: element 26 of the city set (docs/city_elements/26_auto_repair) and its states (appearance.md, 4). City
climate: dry daylight, no snow. No soldiers go in (README.md, 3): no occupied states and no team zone.

Footprint 2x2 cells, origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the buildings of
the map (the game squashes it upright, game/data/visuals/models.yaml): one tall storey of the bays to the roof at 1.6 and the coping at
1.74, on the back half of the footprint; the forecourt in front with two cars. The cars are made taller than they are, by the squash
(CAR_Z), so that they come out right once the game squashes the garage. The parts:
    body      grey concrete blocks with dark streaks under the roof, a concrete plinth and lintels; small dark-framed windows on the sides
              and the back, a side door on the right
    roof      a flat roof of dark bitumen, a light coping round it, a vent box with a fan and a small unit on it
    bay doors left, bay doors right   the two wide blue-grey roll-up doors of the bays, each a node of its own, so that the game can
              open them (prompts.md, 4)
    sign      a plain sign board over the doors in muted colours, no text, in a dark metal frame
    cars      two cars on the forecourt, a red one nose to the left bay with its bonnet up and a cream one by the right bay, in the model
              for now (README.md, open question 1)
    forecourt the concrete in front and the paving round it
No emblems, no text.
Nodes: "body", "roof", "bay_doors_left", "bay_doors_right", "sign", "cars", "base". The abandoned one also has fire_1..2. One GLB per
state:
    city_auto_repair               whole: the doors shut
    city_auto_repair_damaged       damaged: the left door torn off and leaning on the wall, the right one bent out, the windows and the
                                   cream car's glass broken, cracks
    city_auto_repair_abandoned     burnt («горит», appearance.md, 4): the left door gone and the right one half up, the bays dark and
                                   charred inside, soot up the front, the sign hanging by one end, both cars burnt out, an oil slick on
                                   the forecourt, smoke out of the vent
    city_auto_repair_ruins         destroyed: the roof sagged down into the bays between stumps of the walls, the cars wrecked
Budget 6000 triangles, texture 1024 (docs/city_elements/26_auto_repair/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_auto_repair as car
    print(car.run("city_auto_repair"))   # or car.run_all()
"""

import math

from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_auto_repair": "intact",
    "city_auto_repair_damaged": "damaged",
    "city_auto_repair_abandoned": "abandoned",
    "city_auto_repair_ruins": "ruins",
}

(BLOCK, CONCRETE, DOOR, GLASS, FRAME, DARK, PAVE, FORECOURT, SIGN, ROOF, METAL, CAR_RED, CAR_CREAM, TYRE, CHAR,
 OIL) = (cc.part(i) for i in range(16))
PARTS = 16

X0 = 0.92
YF, YB = -0.12, 0.9  # the front wall and the back wall
WALLS = cc.Facade(X0, -YF, YB)
PAVE_H = 0.05
PLINTH = 0.14
ROOF_Z = 1.6
COPING = 1.74
DOORS = ((-0.82, -0.08), (0.08, 0.82))  # the two bays along the front
DOOR_TOP = 1.12
SIGN_Z = (1.28, 1.56)
CAR_Z = 1 / 0.35  # the game squashes the garage upright by 0.35: the cars are made this much taller, to come out right
FIRE_POINTS = ((-0.45, YF - 0.05, 0.5), (0.45, 0.55, ROOF_Z + 0.3))


# --- the block -----------------------------------------------------------------------------

def _block(p, variant):
    abandoned = variant == "abandoned"
    p.box((-X0, YF, PAVE_H), (X0, YB, ROOF_Z), BLOCK)
    for wall, (a, b) in (("front", (-X0 - 0.03, X0 + 0.03)), ("back", (-X0 - 0.03, X0 + 0.03)), ("left", (YF, YB)), ("right", (YF, YB))):
        WALLS.box(p, wall, a, b, PAVE_H, PLINTH, -0.01, 0.03, CONCRETE)
    # The jambs and the lintel over the doors.
    for u0, u1 in ((-X0, DOORS[0][0]), (DOORS[0][1], DOORS[1][0]), (DOORS[1][1], X0)):
        WALLS.box(p, "front", u0, u1, PAVE_H, DOOR_TOP, -0.01, 0.03, CONCRETE)
    WALLS.box(p, "front", -X0, X0, DOOR_TOP, DOOR_TOP + 0.1, -0.01, 0.04, CONCRETE)
    # Windows: two on the back, one on the left; on the right a window and the side door.
    damaged = variant != "intact"
    n = 0
    for wall, u in (("back", -0.45), ("back", 0.45), ("left", 0.4), ("right", 0.65)):
        n += 1
        broken = DARK if abandoned or damaged and n % 2 == 1 else None
        WALLS.window(p, wall, u, 0.7, 1.15, 0.16, GLASS, FRAME, bars=(1, 0), broken=broken)
    WALLS.box(p, "right", 0.08, 0.36, PLINTH, 1.0, -0.01, 0.015, DARK if abandoned else DOOR)
    WALLS.box(p, "right", 0.04, 0.4, 1.0, 1.06, -0.01, 0.03, CONCRETE)


def _roof(p, variant):
    """The bitumen roof, the coping round it, a vent box with a fan and a small unit on it."""
    p.box((-X0 + 0.02, YF + 0.02, ROOF_Z - 0.02), (X0 - 0.02, YB - 0.02, ROOF_Z + 0.03), ROOF)
    t = 0.07
    for lo, hi in (((-X0 - 0.03, YF - 0.03, ROOF_Z - 0.04), (X0 + 0.03, YF - 0.03 + t, COPING)),
                   ((-X0 - 0.03, YB + 0.03 - t, ROOF_Z - 0.04), (X0 + 0.03, YB + 0.03, COPING)),
                   ((-X0 - 0.03, YF - 0.03 + t, ROOF_Z - 0.04), (-X0 - 0.03 + t, YB + 0.03 - t, COPING)),
                   ((X0 + 0.03 - t, YF - 0.03 + t, ROOF_Z - 0.04), (X0 + 0.03, YB + 0.03 - t, COPING))):
        p.box(lo, hi, CONCRETE)
    p.box((0.25, 0.4, ROOF_Z + 0.03), (0.6, 0.7, ROOF_Z + 0.22), METAL)  # the vent box
    p.cylinder((0.425, 0.55, ROOF_Z + 0.25), 'Z', 0.11, 0.03, DARK if variant == "abandoned" else METAL, sides=10)  # its fan
    p.box((-0.6, 0.45, ROOF_Z + 0.03), (-0.35, 0.65, ROOF_Z + 0.16), METAL)  # a small unit


# --- the doors -------------------------------------------------------------------------

def _door(p, a, b, state):
    """A roll-up door in its bay: 'shut'; 'bent', its foot pushed out; 'leaning', torn off and leaning on the wall before the dark
    bay; 'half', rolled half up over the dark bay; 'gone', the bay dark and only the roll left at the top."""
    y = YF
    if state != "shut":
        p.box((a, y - 0.004, PAVE_H), (b, y + 0.05, DOOR_TOP), DARK)  # the dark bay inside
    if state == "shut":
        p.box((a, y - 0.025, PAVE_H), (b, y + 0.02, DOOR_TOP), DOOR)
    elif state == "bent":
        p.loft([[Vector((a + 0.02, yy - 0.012, zz)), Vector((b - 0.02, yy - 0.012, zz)),
                 Vector((b - 0.02, yy + 0.012, zz)), Vector((a + 0.02, yy + 0.012, zz))]
                for yy, zz in ((y - 0.2, PAVE_H + 0.03), (y - 0.1, 0.55), (y - 0.04, DOOR_TOP))], DOOR)
    elif state == "leaning":
        cc.block(p, ((a + b) / 2, y - 0.1, 0.58), (b - a - 0.04, 0.04, 1.05), (-0.15, 0.0, 0.04), DOOR)
    elif state == "half":
        p.box((a, y - 0.025, 0.62), (b, y + 0.02, DOOR_TOP), DOOR)
    if state in ("leaning", "gone"):
        p.box((a, y - 0.03, DOOR_TOP - 0.1), (b, y + 0.02, DOOR_TOP), DOOR)  # the roll left at the top


def _doors(variant):
    left, right = Parts(), Parts()
    states = {"intact": ("shut", "shut"), "damaged": ("leaning", "bent"), "abandoned": ("gone", "half")}[variant]
    _door(left, *DOORS[0], states[0])
    _door(right, *DOORS[1], states[1])
    return left, right


def _sign(p, variant):
    """A plain board over the doors in a dark frame; abandoned: hanging down by its right end."""
    lo, hi = SIGN_Z
    if variant == "abandoned":
        cc.block(p, (0.05, YF - 0.07, 0.95), (1.32, 0.03, hi - lo), (0.0, -0.55, 0.0), SIGN)
        cc.block(p, (0.05, YF - 0.05, 0.95), (1.38, 0.02, hi - lo + 0.06), (0.0, -0.55, 0.0), METAL)
        return
    WALLS.box(p, "front", -0.72, 0.72, lo - 0.03, hi + 0.03, -0.01, 0.05, METAL)
    WALLS.box(p, "front", -0.68, 0.68, lo, hi, 0.04, 0.07, SIGN)


# --- the cars --------------------------------------------------------------------------

def _car(p, centre, heading, paint, hood_up=False, glass=GLASS, burnt=False, crushed=False):
    """A small car: heading the direction of its nose (radians from +y towards -x); its heights in the game's units, made CAR_Z
    taller. Burnt: charred, no glass and no tyres, down on its rims; crushed: its roof pushed down and the body tilted."""
    length, width = 0.62, 0.3
    cx, cy = centre
    ca, sa = math.cos(heading), math.sin(heading)
    sink = 0.025 if burnt else 0.0
    tilt = 0.04 if crushed else 0.0

    def at(u, v, h):  # u across the car (to its right), v along it (to its nose), h up
        h = h - sink + tilt * u / width
        return Vector((cx + u * ca - v * sa, cy + u * sa + v * ca, PAVE_H + h * CAR_Z))

    body = CHAR if burnt else paint
    hw = width / 2
    p.loft([[at(-hw, v, lo), at(hw, v, lo), at(hw, v, hi), at(-hw, v, hi)]
            for v, lo, hi in ((-length / 2, 0.04, 0.08), (-length / 2 + 0.04, 0.03, 0.09), (length / 2 - 0.05, 0.03, 0.088),
                              (length / 2, 0.04, 0.072))], body)
    top = 0.12 if crushed else 0.15
    windows = DARK if burnt else glass
    p.loft([[at(-hw + 0.01, -0.21, 0.088), at(hw - 0.01, -0.21, 0.088), at(hw - 0.01, 0.12, 0.088), at(-hw + 0.01, 0.12, 0.088)],
            [at(-hw + 0.035, -0.15, top), at(hw - 0.035, -0.15, top), at(hw - 0.035, 0.05, top), at(-hw + 0.035, 0.05, top)]], windows)
    p.loft([[at(-hw + 0.035, -0.15, top - 0.002), at(hw - 0.035, -0.15, top - 0.002), at(hw - 0.035, 0.05, top - 0.002),
             at(-hw + 0.035, 0.05, top - 0.002)],
            [at(-hw + 0.04, -0.145, top + 0.006), at(hw - 0.04, -0.145, top + 0.006), at(hw - 0.04, 0.045, top + 0.006),
             at(-hw + 0.04, 0.045, top + 0.006)]], body)  # the roof
    if not burnt:
        for v in (-length / 2 + 0.12, length / 2 - 0.12):
            for side in (-1, 1):
                u0, u1 = side * (hw - 0.05), side * (hw + 0.006)
                ring = [(v + 0.05 * math.cos(2 * math.pi * i / 8), 0.05 + 0.05 * math.sin(2 * math.pi * i / 8)) for i in range(8)]
                p.loft([[at(u, vv, hh) for vv, hh in ring] for u in (u0, u1)], TYRE)
    if hood_up:
        p.loft([[at(-hw + 0.03, 0.13, h), at(hw - 0.03, 0.13, h), at(hw - 0.03, length / 2 - 0.03, h), at(-hw + 0.03, length / 2 - 0.03, h)]
                for h in (0.086, 0.094)], DARK)  # the engine under it
        angle = math.radians(60)
        hinge, reach = 0.12, length / 2 - 0.12
        tip_v, tip_h = hinge + reach * math.cos(angle), 0.09 + reach * math.sin(angle)
        n_v, n_h = -math.sin(angle) * 0.008, math.cos(angle) * 0.008
        p.loft([[at(-hw + 0.02, hinge + dv, 0.09 + dh), at(hw - 0.02, hinge + dv, 0.09 + dh),
                 at(hw - 0.02, tip_v + dv, tip_h + dh), at(-hw + 0.02, tip_v + dv, tip_h + dh)]
                for dv, dh in ((0.0, 0.0), (n_v, n_h))], paint)


def _cars(p, variant):
    if variant == "ruins":
        _car(p, (-0.5, -0.55), 0.35, CAR_RED, burnt=True, crushed=True)
        _car(p, (0.55, -0.6), -0.5, CAR_CREAM, burnt=True, crushed=True)
        return
    burnt = variant == "abandoned"
    _car(p, (-0.45, -0.66), 0.0, CAR_RED, hood_up=not burnt, burnt=burnt)
    _car(p, (0.5, -0.64), -0.18, CAR_CREAM, glass=DARK if variant == "damaged" else GLASS, burnt=burnt)


def _oil(p):
    """A dark slick on the forecourt, by the left bay."""
    radii = (0.22, 0.17, 0.2, 0.25, 0.18, 0.21, 0.16, 0.23, 0.19, 0.2)
    ring = [(-0.15 + r * 1.4 * math.cos(2 * math.pi * i / 10), -0.3 + r * 0.7 * math.sin(2 * math.pi * i / 10)) for i, r in enumerate(radii)]
    p.loft([[Vector((u, v, PAVE_H + 0.012 + dz)) for u, v in ring] for dz in (0.0, 0.003)], OIL)


# --- the ruins ------------------------------------------------------------------------

def _ruins(body, roof):
    """Stumps of the walls, the roof sagged down into the bays, heaps of blocks, the sign on the ground."""
    for lo, hi in (((-X0, YB - 0.14, PAVE_H), (X0, YB, 1.0)), ((-X0, YF, PAVE_H), (-X0 + 0.14, YB - 0.14, 0.85)),
                   ((X0 - 0.14, 0.3, PAVE_H), (X0, YB - 0.14, 0.7)), ((-0.08, YF, PAVE_H), (0.08, YF + 0.14, 0.6))):
        body.box(lo, hi, BLOCK)
    for c, s, t in (((0.6, 0.0, 0.18), (0.6, 0.5, 0.3), (0.1, 0.2, 0.6)), ((-0.5, 0.1, 0.15), (0.5, 0.4, 0.25), (0.2, 0.1, 1.1)),
                    ((0.75, 0.6, 0.15), (0.35, 0.4, 0.22), (0.3, 0.0, 0.4))):
        cc.block(body, c, s, t, BLOCK)
    cc.block(body, (0.1, -0.9, PAVE_H + 0.03), (1.2, 0.12, 0.04), (0.0, 0.0, 0.05), SIGN)  # the sign, flat on the forecourt
    # The roof: down from the back wall onto the floor of the bays, in two pieces.
    roof.loft([[Vector((-0.86, YB - 0.1, 1.0 + dz)), Vector((-0.02, YB - 0.1, 1.0 + dz)), Vector((-0.02, YF + 0.05, 0.2 + dz)),
                Vector((-0.86, YF + 0.05, 0.12 + dz))] for dz in (0.0, 0.06)], ROOF)
    roof.loft([[Vector((0.02, YB - 0.1, 0.95 + dz)), Vector((0.86, YB - 0.1, 0.7 + dz)), Vector((0.86, 0.2, 0.15 + dz)),
                Vector((0.02, YF + 0.1, 0.35 + dz))] for dz in (0.0, 0.06)], ROOF)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Grey concrete blocks with dark streaks, light concrete, blue-grey roll-up doors in slats, glass, dark frames and metal, paving
    and a stained concrete forecourt, a muted sign, dark bitumen, a red and a cream car, tyres, char, oil; per state, cracks and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    course = g.math('MULTIPLY', z, 6.0)
    row = g.math('FRACT', course)
    col = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 2.8), g.math('MULTIPLY', g.math('FLOOR', course), 0.5)))
    joint = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', row, -30.0, 1.6)), g.clamp01(g.math('MULTIPLY_ADD', col, -40.0, 1.6)))
    block = g.mix(joint, g.mix(g.noise(4.0, 3.0), lin((0.50, 0.50, 0.49)), lin((0.58, 0.58, 0.57))), lin((0.40, 0.40, 0.39)))
    streak = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 2.5, -2.5 * (ROOF_Z - 0.7))),
                    g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 7.0)), 3.0, -1.7)))
    block = g.mix(g.math('MULTIPLY', streak, 0.5), block, lin((0.24, 0.24, 0.24)))  # dark streaks down from the roof
    concrete = g.mix(grain, lin((0.66, 0.66, 0.64)), lin((0.74, 0.74, 0.72)))
    slat = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', z, 16.0)), -20.0, 1.6))
    door = g.mix(slat, g.mix(grain, lin((0.24, 0.34, 0.48)), lin((0.30, 0.40, 0.54))), lin((0.14, 0.20, 0.30)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.4)), lin((0.10, 0.15, 0.22)), lin((0.30, 0.40, 0.50)))
    frame = g.mix(grain, lin((0.14, 0.14, 0.15)), lin((0.20, 0.20, 0.21)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.06)), lin((0.10, 0.10, 0.11)))
    slabs = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 3.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 3.0)), -30.0, 1.5)))
    pave = g.mix(slabs, g.mix(grain, lin((0.62, 0.60, 0.56)), lin((0.70, 0.68, 0.64))), lin((0.45, 0.44, 0.42)))
    stains = g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.5, 3.0), 4.0, -2.2))
    forecourt = g.mix(stains, g.mix(g.noise(8.0, 4.0), lin((0.52, 0.52, 0.50)), lin((0.60, 0.60, 0.58))), lin((0.36, 0.35, 0.33)))
    sign = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', z, (SIGN_Z[0] + SIGN_Z[1]) / 2)), -40.0, 2.0)),
                 lin((0.22, 0.36, 0.36)), lin((0.72, 0.68, 0.56)))  # a muted teal board with a pale stripe along it
    roof = g.mix(g.noise(14.0, 4.0), lin((0.12, 0.12, 0.12)), lin((0.18, 0.18, 0.17)))
    metal = g.mix(grain, lin((0.40, 0.41, 0.43)), lin((0.48, 0.49, 0.51)))
    red = g.mix(grain, lin((0.50, 0.06, 0.05)), lin((0.58, 0.09, 0.07)))
    cream = g.mix(grain, lin((0.80, 0.74, 0.56)), lin((0.86, 0.80, 0.62)))
    tyre = g.mix(grain, lin((0.04, 0.04, 0.04)), lin((0.07, 0.07, 0.07)))
    char = g.mix(g.noise(10.0, 4.0), lin((0.06, 0.05, 0.05)), lin((0.24, 0.14, 0.08)))
    oil = g.mix(g.noise(6.0, 3.0), lin((0.02, 0.02, 0.02)), lin((0.05, 0.04, 0.05)))
    albedo = cc.paint(g, mask, (block, concrete, door, glass, frame, dark, pave, forecourt, sign, roof, metal, red, cream, tyre, char, oil))
    if variant in ("damaged", "abandoned"):
        walls = g.math('ADD', mask[0], mask[1])
        albedo = g.mix(g.math('MULTIPLY', walls, cc.cracks(g, 6.0)), albedo, lin((0.18, 0.18, 0.17)))
    if variant == "abandoned":
        front = g.clamp01(g.math('MULTIPLY_ADD', y, -6.0, 6.0 * YF + 1.0))  # the front wall and what is before it
        soot = g.math('MULTIPLY', g.math('ADD', g.math('ADD', mask[0], mask[1]), g.math('ADD', mask[2], mask[8])),
                      g.math('MAXIMUM', g.math('MULTIPLY', front, 0.9), g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.8, 2.0), 6.0, -3.0))))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.08, 0.07, 0.07)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -0.8, 1.0)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    rough = cc.weighted(g, mask, (0.9, 0.85, 0.5, 0.1, 0.5, 0.9, 0.9, 0.85, 0.6, 0.9, 0.5, 0.35, 0.35, 0.9, 0.95, 0.15))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.3, 0.0, 0.2, 0.0, 0.0, 0.0, 0.0, 0.0, 0.4, 0.2, 0.2, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[0], g.math('MULTIPLY', joint, -1.0),
                           g.math('MULTIPLY_ADD', mask[2], g.math('MULTIPLY', slat, -1.0), g.noise(60.0, 3.0))), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "roof", "bay_doors_left", "bay_doors_right", "sign", "cars", "base"))
    variant = VARIANTS[variant_id]
    body, roof, cars = Parts(), Parts(), Parts()
    parts = [(body, "body"), (roof, "roof")]
    if variant == "ruins":
        _ruins(body, roof)
    else:
        _block(body, variant)
        _roof(roof, variant)
        left, right = _doors(variant)
        sign = Parts()
        _sign(sign, variant)
        parts += [(left, "bay_doors_left"), (right, "bay_doors_right"), (sign, "sign")]
    _cars(cars, variant)
    parts.append((cars, "cars"))
    base = Parts()
    base.box((-0.98, -0.98, 0.0), (0.98, 0.98, PAVE_H), PAVE)
    base.box((-0.94, -0.98, 0.0), (0.94, YF, PAVE_H + 0.01), FORECOURT)
    if variant == "abandoned":
        _oil(base)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAVE_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, bay_doors_left, bay_doors_right, sign, cars and base
    (body, roof, cars and base in the ruins; fire_1..2 when abandoned, at the left bay and over the vent)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
