"""City school: element 03 of the city set (docs/city_elements/03_school), its states (appearance.md, 4; the states of the civilian
buildings, docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside. City climate: dry daylight, no snow.

Footprint 3x2 cells, origin at the centre of the footprint on the ground, the front to -Y. Made a storey to a cell, as the buildings of
the map (the game squashes it upright, game/data/visuals/models.yaml): two storeys to the eaves at 2.38, the hipped roof to 3.0, the
entrance's gable to 2.9. The yard and its fence are not in the model (prompts.md, 1): fence sections come from the fence set. The parts:
    school     a long block of two storeys in light yellow plaster, a white plinth, white corner pilasters and a white cornice;
               rows of tall classroom windows in white frames with glazing bars
    entrance   a block standing out of the middle of the front, rising into a gable with a round clock without numbers; a double door
               of dark green under a wide canopy, two broad steps
    roof       a hipped roof of grey-green sheet metal with standing seams, a small gable roof over the entrance
    apron      a concrete apron round the school
No emblems, no text, no flagpole (prompts.md, 4). The canopy over the entrance takes the owner's colour: a band above the door, as the
prompt has it, is too thin to read once the game squashes the school upright.
Nodes: "body" (the walls, windows, entrance, and the barrels, sandbags or rubble of a state), "roof", "team_mask" (the canopy),
"base" (the apron). The abandoned one also has fire_1..3. One GLB per state:
    city_school                     whole, empty
    city_school_occupied            whole, six soldiers inside: a barrel out of each upper front window, sandbags on the steps
    city_school_damaged             damaged, empty: half the windows knocked out, plaster fallen off to the brick, cracks, soot
    city_school_damaged_occupied    damaged, occupied: the same, with the barrels and the sandbags
    city_school_abandoned           critically damaged, its soldiers out: the roof of the right wing fallen in, its walls broken, the
                                    floor between the storeys and charred beams showing, rubble
    city_school_ruins               destroyed: stumps of the walls, the entrance's door frame standing, heaps of brick, roof sheets
                                    and beams
Budget 10000 triangles, texture 2048 (docs/city_elements/03_school/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_school as csc
    print(csc.run("city_school"))   # or csc.run_all()
"""

import math

import bmesh
from mathutils import Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 2048
VARIANTS = {
    "city_school": "intact",
    "city_school_occupied": "occupied",
    "city_school_damaged": "damaged",
    "city_school_damaged_occupied": "damaged_occupied",
    "city_school_abandoned": "abandoned",
    "city_school_ruins": "ruins",
}

PLASTER, WHITE, GLASS, DOOR, ROOF, CONCRETE, DARK, SAND, TEAM, BRICK, CLOCK = (cc.part(i) for i in range(11))
PARTS = 11

X0, Y0 = 1.40, 0.80
WALLS = cc.Facade(X0, Y0)
APRON_H = 0.05
STOREY = 1.1
EAVE = APRON_H + 2 * STOREY + 0.13  # two storeys and a cornice
OVER = 0.1  # the roof over the walls
RIDGE = 3.0
RIDGE_X = X0 - (Y0 + OVER) * 0.9  # where the hips meet the ridge
WALL_T = 0.14
WIN_HALF = 0.11
GROUND, UPPER = (0.33, 0.95), (APRON_H + STOREY + 0.28, APRON_H + STOREY + 0.9)
COLUMNS = {"front": (-1.22, -0.92, -0.62, 0.62, 0.92, 1.22), "back": tuple(-1.22 + 0.348 * i for i in range(8)),
           "left": (-0.42, 0.0, 0.42), "right": (-0.42, 0.0, 0.42)}
# The entrance: its half width, how far it stands out of the front, the top of its gable.
ENT_HALF, ENT_OUT, ENT_TOP = 0.38, 0.12, 2.9
CLOCK_Z = EAVE + 0.17
# Abandoned: the right wing (x > WING_X) lost its roof and its upper walls broke.
WING_X = 0.45
BROKEN = {"front": ((WING_X, 0.95, 1.7), (0.95, X0, 1.35)), "back": ((WING_X, 1.0, 1.55), (1.0, X0, 1.9)),
          "right": ((-Y0, -0.2, 1.4), (-0.2, Y0, 1.8))}
FIRE_POINTS = ((0.9, -0.2, APRON_H + STOREY + 0.05), (1.22, -Y0 - 0.02, 1.2), (-0.5, 0.0, RIDGE - 0.1))


def _wall_top(wall, u, abandoned):
    for a, b, top in BROKEN.get(wall, ()) if abandoned else ():
        if a <= u <= b:
            return top
    return EAVE


# --- the school -----------------------------------------------------------------------

def _block(p, abandoned):
    """The two storeys. Whole: one solid whose top the roof covers (the top is deleted: shut under the roof it would bake black).
    Abandoned: hollow walls broken over the right wing, the floor between the storeys inside."""
    if not abandoned:
        faces = cc.made(p, lambda: p.box((-X0, -Y0, APRON_H), (X0, Y0, EAVE), PLASTER))
        bmesh.ops.delete(p.bm, geom=[f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z > EAVE - 0.01],
                         context='FACES')
    else:
        p.box((-X0, -Y0, APRON_H), (WING_X, -Y0 + WALL_T, EAVE), PLASTER)
        p.box((-X0, Y0 - WALL_T, APRON_H), (WING_X, Y0, EAVE), PLASTER)
        p.box((-X0, -Y0 + WALL_T, APRON_H), (-X0 + WALL_T, Y0 - WALL_T, EAVE), PLASTER)
        for a, b, top in BROKEN["front"]:
            p.box((a, -Y0, APRON_H), (b, -Y0 + WALL_T, top), PLASTER)
        for a, b, top in BROKEN["back"]:
            p.box((a, Y0 - WALL_T, APRON_H), (b, Y0, top), PLASTER)
        for a, b, top in BROKEN["right"]:
            p.box((X0 - WALL_T, max(a, -Y0 + WALL_T), APRON_H), (X0, min(b, Y0 - WALL_T), top), PLASTER)
        z = APRON_H + STOREY
        p.box((-X0 + WALL_T, -Y0 + WALL_T, z - 0.08), (X0 - WALL_T, Y0 - WALL_T, z), CONCRETE)
        p.box((-X0 + WALL_T, -Y0 + WALL_T, EAVE - 0.06), (WING_X, Y0 - WALL_T, EAVE), CONCRETE)  # the ceiling under the roof left
    # The white plinth, the band between the storeys, the cornice and the corner pilasters.
    for wall, (a, b) in (("front", (-X0 - 0.03, X0 + 0.03)), ("back", (-X0 - 0.03, X0 + 0.03)), ("left", (-Y0, Y0)), ("right", (-Y0, Y0))):
        if not abandoned or wall == "left":
            tops = [(a, b, EAVE)]
        elif wall == "right":
            tops = list(BROKEN[wall])
        else:  # the front and the back: whole over the left wing, broken over the right one
            tops = [(a, WING_X, EAVE)] + list(BROKEN[wall])
        WALLS.box(p, wall, a, b, APRON_H, 0.22, -0.01, 0.03, WHITE)
        for u0, u1, top in tops:
            if top > APRON_H + STOREY + 0.06:
                WALLS.box(p, wall, u0, u1, APRON_H + STOREY - 0.04, APRON_H + STOREY + 0.04, -0.01, 0.025, WHITE)
            if top >= EAVE:
                WALLS.box(p, wall, u0, u1, EAVE - 0.13, EAVE, -0.01, 0.05, WHITE)
    for x, y in ((-X0, -Y0), (X0, -Y0), (-X0, Y0), (X0, Y0)):
        if abandoned and x > 0:
            continue
        p.box((x - 0.06, y - 0.06, APRON_H), (x + 0.06, y + 0.06, EAVE - 0.13), WHITE)


def _windows(p, variant):
    damaged = variant in ("damaged", "damaged_occupied")
    abandoned = variant == "abandoned"
    n = 0
    for wall, columns in COLUMNS.items():
        for storey in (GROUND, UPPER):
            for u in columns:
                if storey[1] + 0.05 > _wall_top(wall, u, abandoned):
                    continue
                n += 1
                broken = DARK if abandoned or damaged and n % 2 == 1 else None
                WALLS.window(p, wall, u, storey[0], storey[1], WIN_HALF, GLASS, WHITE, bars=(1, 2), broken=broken)


def _entrance(p, variant):
    """The entrance block out of the middle of the front, its gable with the clock, the double door and the steps."""
    y_front = -Y0 - ENT_OUT
    p.box((-ENT_HALF, y_front, APRON_H), (ENT_HALF, -Y0 + 0.01, EAVE), PLASTER)
    p.loft([[Vector((-ENT_HALF, y, EAVE)), Vector((ENT_HALF, y, EAVE)), Vector((0.0, y, ENT_TOP - 0.08))]
            for y in (y_front, -Y0 + 0.2)], PLASTER)
    p.box((-ENT_HALF - 0.03, y_front - 0.03, APRON_H), (ENT_HALF + 0.03, y_front + 0.02, 0.22), WHITE)
    p.box((-ENT_HALF - 0.03, y_front - 0.04, EAVE - 0.13), (ENT_HALF + 0.03, y_front + 0.02, EAVE), WHITE)
    for x in (-ENT_HALF, ENT_HALF):
        p.box((x - 0.05, y_front - 0.04, APRON_H), (x + 0.05, y_front + 0.04, EAVE - 0.13), WHITE)
    # The clock: a white face in a dark ring, two hands, no numbers.
    if variant != "abandoned":
        p.cylinder((0.0, y_front - 0.01, CLOCK_Z), 'Y', 0.13, 0.015, DARK, sides=20)
        p.cylinder((0.0, y_front - 0.03, CLOCK_Z), 'Y', 0.11, 0.01, CLOCK, sides=20)
        p.bar((0.0, y_front - 0.045, CLOCK_Z), (0.0, y_front - 0.045, CLOCK_Z + 0.085), 0.007, DARK)
        p.bar((0.0, y_front - 0.045, CLOCK_Z), (0.055, y_front - 0.045, CLOCK_Z - 0.02), 0.008, DARK)
    # The upper window over the canopy, the double door and its frame, the steps.
    ent = cc.Facade(ENT_HALF, Y0 + ENT_OUT)
    broken = DARK if variant in ("damaged", "damaged_occupied", "abandoned") else None
    ent.window(p, "front", 0.0, UPPER[0], UPPER[1], 0.17, GLASS, WHITE, bars=(2, 2), broken=broken)
    ent.box(p, "front", -0.19, 0.19, APRON_H + 0.06, 0.9, -0.01, 0.015, DARK if variant == "abandoned" else DOOR)
    ent.box(p, "front", -0.006, 0.006, APRON_H + 0.06, 0.9, 0.0, 0.02, WHITE)
    ent.box(p, "front", -0.22, 0.22, 0.9, 0.94, -0.01, 0.03, WHITE)
    for a in (-0.22, 0.19):
        ent.box(p, "front", a, a + 0.03, APRON_H, 0.9, -0.01, 0.03, WHITE)
    for k, d in enumerate((0.2, 0.1)):
        ent.box(p, "front", -0.6 + 0.05 * k, 0.6 - 0.05 * k, APRON_H, APRON_H + 0.04 * (k + 1), -0.01, d, CONCRETE)


def _canopy(p):
    """The wide canopy over the entrance: the team zone."""
    y_front = -Y0 - ENT_OUT
    p.box((-0.55, y_front - 0.3, 1.0), (0.55, y_front + 0.02, 1.07), TEAM)


def _barrels(p):
    """Six rifle barrels, one for each soldier inside, out of the upper front windows."""
    z = (UPPER[0] + UPPER[1]) / 2
    for u in COLUMNS["front"]:
        a = WALLS.point("front", u, z, -0.02)
        p.bar(a, a + Vector((u * 0.03, -0.42, -0.04)), 0.017, DARK)


def _sandbags(p):
    """Sandbags on the steps either side of the door, two rows."""
    y = -Y0 - ENT_OUT - 0.12
    for z, shift in ((APRON_H + 0.08, 0.0), (APRON_H + 0.16, 0.12)):
        for t in cc.steps(0.26 + shift, 0.58, 0.24):
            for x in (-t, t):
                cc.bag(p, (x, y, z), 0.0, SAND, length=0.22, width=0.14, height=0.08)


# Bits at the foot of the damaged one; the rubble of the abandoned one: middle, size, turn.
BITS = (((-1.0, -Y0 - 0.14, APRON_H + 0.03), (0.12, 0.08, 0.05), (0.2, 0.1, 0.4)), ((0.75, -Y0 - 0.16, APRON_H + 0.03), (0.1, 0.07, 0.05), (0.1, 0.3, 1.2)),
        ((X0 + 0.07, 0.3, APRON_H + 0.03), (0.08, 0.12, 0.05), (0.3, 0.0, 0.2)), ((-0.3, Y0 + 0.12, APRON_H + 0.03), (0.12, 0.08, 0.05), (0.0, 0.2, 2.0)))
RUBBLE_ABANDONED = (((0.9, 0.0, APRON_H + STOREY + 0.08), (0.5, 0.4, 0.14), (0.1, 0.2, 0.3)), ((0.7, 0.4, APRON_H + STOREY + 0.06), (0.3, 0.3, 0.1), (0.3, 0.1, 1.0)),
                    ((1.1, -Y0 - 0.12, APRON_H + 0.1), (0.4, 0.2, 0.16), (0.2, 0.2, 0.2)), ((X0 + 0.1, 0.2, APRON_H + 0.09), (0.2, 0.4, 0.14), (0.1, 0.3, 0.6)),
                    ((0.8, Y0 + 0.1, APRON_H + 0.08), (0.36, 0.18, 0.12), (0.2, 0.1, 1.4)))
SHEETS_ABANDONED = (((0.85, -0.25, 1.6), (0.7, 0.5, 0.03), (0.5, -0.4, 0.2)), ((1.0, 0.35, 1.45), (0.6, 0.45, 0.03), (-0.4, 0.5, -0.3)))
BEAMS_ABANDONED = (((WING_X, -0.3, EAVE + 0.15), (0.95, -0.1, APRON_H + STOREY + 0.1)), ((WING_X, 0.3, EAVE + 0.15), (1.1, 0.4, 1.5)),
                   ((WING_X, 0.0, RIDGE - 0.2), (0.8, 0.1, 1.8)))


def _ruins(p):
    """Stumps of the walls, the door frame of the entrance standing, heaps of brick, roof sheets and beams."""
    for lo, hi in (((-X0, -Y0, APRON_H), (-0.6, -Y0 + WALL_T, 1.0)), ((-X0, -Y0 + WALL_T, APRON_H), (-X0 + WALL_T, Y0, 1.3)),
                   ((-X0, Y0 - WALL_T, APRON_H), (0.2, Y0, 0.85)), ((0.6, Y0 - WALL_T, APRON_H), (X0, Y0, 0.5)),
                   ((X0 - WALL_T, -0.3, APRON_H), (X0, Y0, 0.6)), ((0.5, -Y0, APRON_H), (X0, -Y0 + WALL_T, 0.4))):
        p.box(lo, hi, BRICK)
    y_front = -Y0 - ENT_OUT
    for x in (-ENT_HALF, ENT_HALF - 0.12):
        p.box((x, y_front, APRON_H), (x + 0.12, -Y0, 1.25), PLASTER)
    p.box((-ENT_HALF, y_front, 0.95), (ENT_HALF, -Y0, 1.1), PLASTER)
    for centre, size, turn in RUINS_HEAPS:
        cc.block(p, centre, size, turn, BRICK)
    for centre, size, turn in RUINS_SHEETS:
        cc.block(p, centre, size, turn, ROOF)
    for a, b in RUINS_BEAMS:
        p.bar(a, b, 0.035, DARK)


RUINS_HEAPS = (((-0.5, 0.1, 0.2), (0.9, 0.7, 0.3), (0.1, 0.15, 0.2)), ((0.4, -0.1, 0.22), (0.9, 0.8, 0.34), (0.15, 0.1, 0.8)),
               ((1.05, 0.3, 0.16), (0.5, 0.6, 0.22), (0.2, 0.2, 0.3)), ((-1.0, -0.4, 0.15), (0.4, 0.4, 0.2), (0.1, 0.2, 1.1)),
               ((0.9, -0.6, 0.12), (0.4, 0.3, 0.16), (0.2, 0.1, 0.4)))
RUINS_SHEETS = (((-0.2, 0.3, 0.5), (1.1, 0.7, 0.03), (0.3, -0.2, 0.2)), ((0.7, 0.0, 0.48), (0.9, 0.6, 0.03), (-0.35, 0.25, 0.7)),
                ((-0.9, 0.2, 0.42), (0.6, 0.5, 0.03), (0.2, 0.4, 1.4)))
RUINS_BEAMS = (((-0.8, 0.5, 0.95), (-0.2, 0.2, 0.3)), ((0.2, 0.5, 0.7), (0.8, -0.2, 0.35)), ((0.6, 0.6, 0.6), (1.2, 0.2, 0.25)))


# --- the roof -------------------------------------------------------------------------

def _roof(p, abandoned):
    """The hipped roof (its open bottom on the walls), and the gable roof of the entrance running back into it. Abandoned: only the
    left wing's part, cut square at WING_X."""
    x1 = WING_X if abandoned else X0 + OVER
    eave = [Vector(v) for v in ((-X0 - OVER, -Y0 - OVER, EAVE), (x1, -Y0 - OVER, EAVE), (x1, Y0 + OVER, EAVE), (-X0 - OVER, Y0 + OVER, EAVE))]
    ridge_x1 = WING_X if abandoned else RIDGE_X
    ridge = [Vector(v) for v in ((-RIDGE_X, -0.002, RIDGE), (ridge_x1, -0.002, RIDGE), (ridge_x1, 0.002, RIDGE), (-RIDGE_X, 0.002, RIDGE))]
    faces = cc.made(p, lambda: p.loft([eave, ridge], ROOF))
    bmesh.ops.delete(p.bm, geom=[f for f in faces if abs(f.normal.z) > 0.99 and f.calc_center_median().z < EAVE + 0.01], context='FACES')
    y_front = -Y0 - ENT_OUT + 0.03  # just behind the face of the gable, so the gable and its clock show
    half = ENT_HALF + 0.08
    p.loft([[Vector((-half, y, EAVE - 0.03)), Vector((half, y, EAVE - 0.03)), Vector((half, y, EAVE + 0.04)), Vector((0.0, y, ENT_TOP)),
             Vector((-half, y, EAVE + 0.04))] for y in (y_front, -0.1)], ROOF)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Light yellow plaster, white trim, glass, a dark green door, grey-green sheet metal with seams, concrete, a clock face, brick
    under fallen plaster, sandbags, a light team zone; per state, cracks, fallen plaster and soot."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.3)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    plaster = g.mix(g.noise(5.0, 3.0), lin((0.84, 0.75, 0.50)), lin((0.90, 0.82, 0.57)))
    white = g.mix(grain, lin((0.86, 0.86, 0.84)), lin((0.93, 0.93, 0.91)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', z, 0.3)), lin((0.12, 0.18, 0.25)), lin((0.30, 0.40, 0.50)))
    door = g.mix(grain, lin((0.13, 0.26, 0.18)), lin((0.18, 0.32, 0.22)))
    # Sheet metal: standing seams down the slopes, along x on the long ones (y on the hips reads the same from above).
    seam = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 10.0)), 0.5)),
                            40.0, -18.0))
    roof = g.mix(seam, g.mix(g.noise(6.0, 3.0), lin((0.34, 0.40, 0.36)), lin((0.42, 0.48, 0.43))), lin((0.24, 0.28, 0.26)))
    concrete = g.mix(grain, lin((0.58, 0.57, 0.54)), lin((0.66, 0.65, 0.62)))
    dark = g.mix(grain, lin((0.06, 0.06, 0.06)), lin((0.11, 0.10, 0.10)))
    sand = g.mix(g.noise(25.0, 4.0), lin((0.48, 0.43, 0.31)), lin((0.60, 0.54, 0.38)))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))
    brick = g.mix(g.noise(8.0, 2.0), lin((0.50, 0.26, 0.19)), lin((0.60, 0.32, 0.23)))
    clock = lin((0.94, 0.93, 0.88))

    albedo = cc.paint(g, mask, (plaster, white, glass, door, roof, concrete, dark, sand, team, brick, clock))
    if variant in ("damaged", "damaged_occupied", "abandoned"):
        # Plaster fallen off in patches down to the brick, cracks, and soot over the windows of both storeys.
        fallen = g.math('MULTIPLY', mask[0], g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.2, 3.0), 12.0, -7.0)))
        albedo = g.mix(fallen, albedo, brick)
        albedo = g.mix(g.math('MULTIPLY', mask[0], cc.cracks(g, 7.0)), albedo, lin((0.22, 0.19, 0.14)))
        soot = g.math('MAXIMUM', cc.band(g, z, GROUND[1] + 0.04, GROUND[1] + 0.2), cc.band(g, z, UPPER[1] + 0.04, UPPER[1] + 0.2))
        soot = g.math('MULTIPLY', soot, g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.5, 2.0), 5.0, -2.0)))
        if variant == "abandoned":
            soot = g.math('MAXIMUM', soot, g.clamp01(g.math('MULTIPLY_ADD', g.noise(2.0, 2.0), 6.0, -2.8)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.10, 0.09, 0.09)))
    if variant == "ruins":
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -1.5, 1.2)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 3.0, -0.6)))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), g.mix(0.2, albedo, lin((0.58, 0.55, 0.50))), lin((0.10, 0.09, 0.09)))

    rough = cc.weighted(g, mask, (0.85, 0.6, 0.12, 0.5, 0.45, 0.9, 0.9, 0.95, 0.45, 0.85, 0.3))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.2, 0.5, 0.0, 0.2, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[4], g.math('MULTIPLY', seam, -1.0), g.noise(60.0, 3.0)), 0.04, 0.006)
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
        _windows(body, variant)
        _entrance(body, variant)
    if variant in ("occupied", "damaged_occupied"):
        _barrels(body)
        _sandbags(body)
    if variant in ("damaged", "damaged_occupied"):
        for centre, size, turn in BITS:
            cc.block(body, centre, size, turn, BRICK)
    if abandoned:
        for centre, size, turn in RUBBLE_ABANDONED:
            cc.block(body, centre, size, turn, BRICK)
        for centre, size, turn in SHEETS_ABANDONED:
            cc.block(body, centre, size, turn, ROOF)
        for a, b in BEAMS_ABANDONED:
            body.bar(a, b, 0.035, DARK)
    parts = [(body, "body")]
    if variant != "ruins":
        roof = Parts()
        _roof(roof, abandoned)
        team = Parts()
        _canopy(team)
        parts += [(roof, "roof"), (team, "team_mask")]
    base = Parts()
    base.box((-1.48, -0.98, 0.0), (1.48, 0.98, APRON_H), CONCRETE)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, APRON_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
