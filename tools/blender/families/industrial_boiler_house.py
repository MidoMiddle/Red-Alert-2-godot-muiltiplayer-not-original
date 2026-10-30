"""Boiler house, element 06 of the industrial set (docs/industrial_elements/06_boiler_house): a small two-storey brick house with a flat
roof and a short steel stack on it; three soldiers hold it (game/data/neutral/civilian.yaml).

Footprint 2x2 cells, origin at the centre of the footprint on the ground, the front to -Y; made upright as the city set (a storey to a
cell, the game squashes it, families/industrial_common.py): the walls to 2.25, the parapet to 2.4, the stack to 4.4. The parts:
    walls      dark red brick on a darker plinth, brick piers at the corners, a cornice and a parapet with a steel coping; narrow windows
               in two rows behind grilles; a grey steel door under a small canopy (the owner's colour); soot over the windows
    roof       bitumen in a parapet, a hatch, a skylight, the stack at the back right with bands and three guy wires, a steam pipe with
               its safety valve at the front left
    outside    a coal hopper on legs with its chute into the right wall, a pipe out of the left wall down to the ground and away, a
               ladder up the back to the roof, coal in a heap
    yard       a concrete apron
Nodes: "body", "roof", "team_mask" (the canopy), "base" (the apron); "smoke_anchor" at the top of the stack, "steam_anchor" over the
valve; fire_1..3 when abandoned. One GLB per state:
    boiler_house                   whole, empty
    boiler_house_occupied          whole, three soldiers: barrels out of the upper front windows, sandbags at the door
    boiler_house_damaged           damaged: two windows knocked out, a hole by the door, bricks at the foot, the stack's wires
                                   loose, soot
    boiler_house_damaged_occupied  the same, with the barrels and the sandbags
    boiler_house_abandoned         critically damaged, its soldiers out: the front right corner fallen, the roof sagging into the
                                   gap, a boiler showing inside, rubble; fire in three places
    boiler_house_ruins             destroyed: stumps of the walls, the two boilers bare on their floor, the stack fallen across,
                                   heaps of brick
Budget 8000 triangles, texture 1024 (docs/industrial_elements/06_boiler_house/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.industrial_boiler_house as bh
    print(bh.run_all())
"""

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

SIZE = 1024
VARIANTS = {
    "boiler_house": "intact",
    "boiler_house_occupied": "occupied",
    "boiler_house_damaged": "damaged",
    "boiler_house_damaged_occupied": "damaged_occupied",
    "boiler_house_abandoned": "abandoned",
    "boiler_house_ruins": "ruins",
}

(BRICK, PLINTH, GLASS, GRILLE, ROOF, DOOR, METAL, CONCRETE, DARK, SAND, TEAM, COAL, BOILER, FLOOR) = (cc.part(i) for i in range(14))
PARTS = 14

X0, Y0 = 0.85, 0.85
APRON = 0.05
STOREY = 1.1
TOP = APRON + 2 * STOREY  # the walls' top
PARAPET = TOP + 0.15
WALLS = cc.Facade(X0, Y0)
ROWS = ((0.45, 0.95), (1.5, 2.0))  # the windows' sill and head, each storey
COLUMNS = {"front": (-0.55, 0.55), "back": (-0.45, 0.0, 0.45), "left": (-0.45, 0.45), "right": (0.45,)}
STACK = Vector((0.5, 0.5, 0.0))
STACK_TOP = 4.4
VALVE = Vector((-0.5, -0.45, 0.0))
SPLIT = 0.15  # the abandoned one: right of x and in front of y the corner has fallen
FIRE_POINTS = ((0.45, -0.45, 1.2), (-0.3, 0.2, PARAPET), (0.6, -0.9, 0.3))


def _walls(p, variant):
    abandoned = variant == "abandoned"
    if abandoned:
        p.box((-X0, -Y0, APRON), (SPLIT, Y0, TOP), BRICK)
        p.box((SPLIT, -SPLIT, APRON), (X0, Y0, TOP), BRICK)
    else:
        p.box((-X0, -Y0, APRON), (X0, Y0, TOP), BRICK)
    p.box((-X0 - 0.025, -Y0 - 0.025, APRON), (X0 + 0.025, Y0 + 0.025, APRON + 0.3), PLINTH)
    # The cornice and the parapet with its coping: round the standing part only, when abandoned.
    rings = [((-X0, -Y0), (X0, Y0))] if not abandoned else [((-X0, -Y0), (SPLIT, Y0)), ((SPLIT, -SPLIT), (X0, Y0))]
    for (x0, y0), (x1, y1) in rings:
        p.box((x0 - 0.04, y0 - 0.04, TOP - 0.1), (x1 + 0.04, y1 + 0.04, TOP), PLINTH)
        for a, b in (((x0, y0), (x1, y0 + 0.08)), ((x0, y1 - 0.08), (x1, y1)), ((x0, y0), (x0 + 0.08, y1)), ((x1 - 0.08, y0), (x1, y1))):
            p.box((a[0], a[1], TOP), (b[0], b[1], PARAPET), BRICK)
        if not abandoned:
            p.box((x0 - 0.02, y0 - 0.02, PARAPET), (x1 + 0.02, y1 + 0.02, PARAPET + 0.03), METAL)
    # Corner piers.
    for x, y in ((-X0, -Y0), (-X0, Y0), (X0, Y0)) + (() if abandoned else ((X0, -Y0),)):
        p.box((x - 0.06, y - 0.06, APRON), (x + 0.06, y + 0.06, TOP - 0.1), BRICK)


def _windows(p, variant):
    k = 0
    for wall, us in COLUMNS.items():
        for z0, z1 in ROWS:
            for u in us:
                if variant == "abandoned" and ((wall == "front" and u > SPLIT) or (wall == "right" and u < -SPLIT)):
                    continue
                k += 1
                out = variant in ("damaged", "damaged_occupied") and k in (2, 7) or variant == "abandoned" and k % 2
                WALLS.window(p, wall, u, z0, z1, 0.1, GLASS, GRILLE, bars=(2, 1), broken=DARK if out else None)
                if not out:
                    for a in (u - 0.05, u, u + 0.05):  # the grille's bars
                        WALLS.box(p, wall, a - 0.007, a + 0.007, z0, z1, 0.01, 0.025, GRILLE)


def _door(p, team, variant):
    """The steel door at the front left, its canopy in the owner's colour, a step."""
    u = -0.2
    torn = variant in ("damaged", "damaged_occupied", "abandoned")
    WALLS.box(p, "front", u - 0.18, u + 0.18, APRON, 0.95, -0.02, 0.0, DARK)
    if torn:
        cc.block(p, (u + 0.1, -Y0 - 0.3, APRON + 0.02), (0.34, 0.8, 0.03), (0.0, 0.0, 0.4), DOOR)
    else:
        WALLS.box(p, "front", u - 0.16, u + 0.16, APRON + 0.04, 0.93, 0.0, 0.025, DOOR)
        for z in (0.3, 0.65):
            WALLS.box(p, "front", u - 0.14, u + 0.14, z - 0.02, z + 0.02, 0.025, 0.035, DOOR)
        WALLS.box(p, "front", u + 0.09, u + 0.12, 0.45, 0.55, 0.025, 0.05, METAL)
    WALLS.box(p, "front", u - 0.24, u + 0.24, APRON, APRON + 0.07, 0.0, 0.2, CONCRETE)
    WALLS.box(team, "front", u - 0.28, u + 0.28, 1.05, 1.11, 0.0, 0.3, TEAM)
    for d in (0.05, 0.27):
        p.bar(WALLS.point("front", u - 0.25, 1.05, d), WALLS.point("front", u - 0.25, 1.25, 0.0), 0.008, METAL)
        p.bar(WALLS.point("front", u + 0.25, 1.05, d), WALLS.point("front", u + 0.25, 1.25, 0.0), 0.008, METAL)


def _outside(p, variant):
    """The coal hopper and chute at the right, the pipe out of the left wall, the ladder at the back, a heap of coal."""
    if variant != "abandoned":
        x = X0 + 0.28
        for dx in (-0.14, 0.14):
            for y in (-0.2, 0.2):
                p.bar((x + dx, y, APRON), (x + dx, y, 0.75), 0.015, METAL)
        p.loft([[Vector((x + sx * s, sy * s, z)) for sx, sy in ((-1, -1), (1, -1), (1, 1), (-1, 1))] for s, z in ((0.07, 0.75), (0.22, 1.1), (0.22, 1.4))], METAL)
        p.bar((x - 0.1, 0.0, 0.8), (X0, 0.0, 1.0), 0.04, METAL)  # the chute
        ic.rubble(p, ic.seed("coal"), x - 0.15, x + 0.15, -0.35, -0.2, APRON, 6, 0.1, COAL)
    # The pipe from the left wall down and away along the ground.
    a = WALLS.point("left", 0.3, 1.3, 0.0)
    b = WALLS.point("left", 0.3, 1.3, 0.25)
    c = WALLS.point("left", 0.3, APRON + 0.15, 0.25)
    p.bar(a, b, 0.05, METAL)
    p.bar(b, c, 0.05, METAL)
    p.bar(c, c + Vector((-0.0, 0.6, 0.0)), 0.05, METAL)
    for q in (b, c):
        p.turned(q, 'Z', [(-0.06, 0.07), (0.06, 0.07)], DARK, sides=10)
    # The ladder up the back.
    for u in (-0.75, -0.6):
        p.bar(WALLS.point("back", u, APRON, 0.1), WALLS.point("back", u, PARAPET + 0.3, 0.1), 0.01, METAL)
    for z in cc.steps(APRON + 0.2, PARAPET + 0.2, 0.2):
        p.bar(WALLS.point("back", -0.75, z, 0.1), WALLS.point("back", -0.6, z, 0.1), 0.007, METAL)


def _roof(r, variant):
    abandoned = variant == "abandoned"
    if abandoned:
        r.box((-X0 + 0.08, -Y0 + 0.08, TOP - 0.02), (SPLIT, Y0 - 0.08, TOP + 0.02), ROOF)
        r.box((SPLIT, -SPLIT, TOP - 0.02), (X0 - 0.08, Y0 - 0.08, TOP + 0.02), ROOF)
        cc.block(r, (0.5, -0.5, TOP - 0.45), (0.75, 0.7, 0.04), (0.5, -0.6, 0.1), ROOF)  # the slab of the fallen corner, sagging
        r.bar((SPLIT, -0.8, TOP - 0.1), (0.8, -0.2, APRON + 0.6), 0.04, DARK)
    else:
        r.box((-X0 + 0.08, -Y0 + 0.08, TOP - 0.02), (X0 - 0.08, Y0 - 0.08, TOP + 0.02), ROOF)
        r.box((-0.3, 0.1, TOP), (0.0, 0.4, TOP + 0.12), METAL)  # the hatch
        r.box((-0.25, -0.35, TOP), (0.15, -0.05, TOP + 0.06), METAL)  # the skylight's frame
        r.box((-0.22, -0.32, TOP + 0.06), (0.12, -0.08, TOP + 0.08), GLASS)
    # The stack with bands; its guy wires down to the roof's corners (loose ones when damaged).
    r.turned((STACK.x, STACK.y, 0.0), 'Z', [(TOP - 0.02, 0.14), (TOP + 0.15, 0.14), (TOP + 0.15, 0.09), (STACK_TOP, 0.08), (STACK_TOP, 0.065),
                                            (STACK_TOP - 0.3, 0.065)], METAL, sides=14)
    for z in cc.steps(TOP + 0.4, STACK_TOP - 0.1, 0.4):
        r.turned((STACK.x, STACK.y, z), 'Z', [(-0.02, 0.093), (0.02, 0.093)], DARK, sides=14)
    loose = variant != "intact" and variant != "occupied"
    for k, (x, y) in enumerate(((-0.7, 0.7), (0.7, -0.7), (0.72, 0.72))):
        if abandoned and k == 1:
            continue
        top = Vector((STACK.x, STACK.y, STACK_TOP - 0.4))
        end = Vector((x, y, TOP + 0.02))
        if loose and k == 0:
            r.bar(top, top + Vector((-0.25, 0.05, -0.9)), 0.005, DARK)
        else:
            r.bar(top, end, 0.005, DARK)
    # The steam pipe and its valve.
    r.cylinder((VALVE.x, VALVE.y, TOP + 0.3), 'Z', 0.035, 0.3, METAL, sides=8)
    r.cylinder((VALVE.x, VALVE.y, TOP + 0.62), 'X', 0.03, 0.08, METAL, sides=8)
    r.turned((VALVE.x, VALVE.y, TOP + 0.55), 'Z', [(0.0, 0.06), (0.03, 0.06)], DARK, sides=10)


def _boilers(p, xs, y, z):
    """Boilers lying along y: drums with domed ends, bands, a manhole."""
    for x in xs:
        p.turned((x, y, z), 'Y', [(-0.55, 0.0), (-0.52, 0.12), (-0.46, 0.2), (0.46, 0.2), (0.52, 0.12), (0.55, 0.0)], BOILER, sides=14)
        for t in (-0.3, 0.0, 0.3):
            p.turned((x, y + t, z), 'Y', [(-0.02, 0.208), (0.02, 0.208)], DARK, sides=14)
        p.cylinder((x, y - 0.15, z + 0.22), 'Z', 0.06, 0.03, DARK, sides=10)
        for t in (-0.35, 0.35):
            p.box((x - 0.15, y + t - 0.04, APRON), (x + 0.15, y + t + 0.04, z - 0.15), CONCRETE)  # its saddles


def _occupied(p):
    z = (ROWS[1][0] + ROWS[1][1]) / 2
    ic.barrels(p, [WALLS.point("front", u, z, -0.02) for u in COLUMNS["front"]] + [WALLS.point("right", 0.45, z, -0.02)], (0.0, -1.0, 0.0), DARK)
    ic.sandbags(p, (-0.62, -0.42, 0.06, 0.26), -Y0 - 0.25, APRON, SAND)


def _damage(p, rnd):
    WALLS.box(p, "front", 0.15, 0.4, 1.15, 1.4, -0.01, 0.012, DARK)
    WALLS.box(p, "left", -0.2, 0.05, 0.6, 0.85, -0.01, 0.012, DARK)
    for x, y in ((0.3, -Y0 - 0.12), (-X0 - 0.12, -0.1), (0.6, Y0 + 0.1)):
        ic.rubble(p, rnd, x - 0.1, x + 0.1, y - 0.05, y + 0.05, APRON, 3, 0.09, BRICK)


def _abandoned(p, rnd):
    p.box((SPLIT, -Y0, APRON), (X0, -SPLIT, APRON + 0.02), FLOOR)
    ic.stumps(p, rnd, [((SPLIT, -Y0 + 0.05), (X0 - 0.05, -Y0 + 0.05)), ((X0 - 0.05, -Y0 + 0.05), (X0 - 0.05, -SPLIT))], APRON, 0.2, 1.1,
              BRICK, step=0.2, gap=0.1)
    _boilers(p, (0.5,), -0.45, APRON + 0.35)
    ic.rubble(p, rnd, SPLIT, X0 + 0.1, -Y0 - 0.15, -SPLIT, APRON, 14, 0.15, BRICK, height=0.25)


def _ruins(p, rnd):
    p.box((-X0, -Y0, APRON), (X0, Y0, APRON + 0.02), FLOOR)
    ic.stumps(p, rnd, [((-X0 + 0.05, -Y0 + 0.05), (X0 - 0.05, -Y0 + 0.05)), ((X0 - 0.05, -Y0 + 0.05), (X0 - 0.05, Y0 - 0.05)),
                       ((X0 - 0.05, Y0 - 0.05), (-X0 + 0.05, Y0 - 0.05)), ((-X0 + 0.05, Y0 - 0.05), (-X0 + 0.05, -Y0 + 0.05))],
              APRON, 0.15, 1.0, BRICK, step=0.2, gap=0.2)
    _boilers(p, (-0.35, 0.35), 0.05, APRON + 0.35)
    p.turned((0.3, -0.2, APRON + 0.12), 'X', [(-0.65, 0.1), (0.65, 0.09)], METAL, sides=12)  # the stack, fallen across
    ic.rubble(p, rnd, -X0 + 0.1, X0 - 0.1, -Y0 + 0.1, Y0 - 0.1, APRON, 16, 0.18, BRICK, height=0.2)
    ic.rubble(p, rnd, -X0 - 0.1, X0 + 0.1, -Y0 - 0.12, -Y0, APRON, 6, 0.13, BRICK)


def _material(name, variant):
    """Dark red brick in courses with soot over the windows and at the top, a darker plinth, dark glass, black grilles, bitumen, a grey
    steel door, dark metal, concrete, near-black iron, sandbags, the light team zone, coal, the boilers' rusty steel, a dusty floor."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    brick, joints = ic.brick(g, coord, (0.40, 0.16, 0.11), (0.48, 0.21, 0.14), (0.54, 0.50, 0.45))
    top_soot = g.clamp01(g.math('MULTIPLY_ADD', z, 1.2, -2.1))  # darker towards the top
    brick = g.mix(g.math('MULTIPLY', top_soot, 0.6), brick, lin((0.12, 0.08, 0.07)))
    plinth, _ = ic.brick(g, coord, (0.26, 0.12, 0.09), (0.32, 0.16, 0.11), (0.38, 0.35, 0.31))
    glass = g.mix(g.noise(4.0, 2.0), lin((0.09, 0.11, 0.12)), lin((0.20, 0.24, 0.25)))
    grille = g.mix(grain, lin((0.06, 0.06, 0.06)), lin((0.11, 0.11, 0.11)))
    roof = g.mix(g.noise(10.0, 3.0), lin((0.12, 0.12, 0.12)), lin((0.18, 0.18, 0.17)))
    door = g.mix(grain, lin((0.38, 0.40, 0.42)), lin((0.46, 0.48, 0.50)))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(5.0, 4.0), 5.0, -2.9))
    door = g.mix(g.math('MULTIPLY', rust, 0.6), door, lin((0.40, 0.22, 0.10)))
    metal = g.mix(grain, lin((0.18, 0.19, 0.20)), lin((0.26, 0.27, 0.28)))
    metal = g.mix(g.math('MULTIPLY', rust, 0.5), metal, lin((0.36, 0.20, 0.10)))
    concrete = g.mix(grain, lin((0.50, 0.49, 0.46)), lin((0.58, 0.57, 0.54)))
    dark = g.mix(grain, lin((0.04, 0.04, 0.04)), lin((0.08, 0.07, 0.07)))
    sand = g.mix(g.noise(20.0, 3.0), lin((0.56, 0.50, 0.36)), lin((0.64, 0.58, 0.42)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    coal = g.mix(grain, lin((0.03, 0.03, 0.03)), lin((0.08, 0.08, 0.09)))
    boiler = g.mix(rust, lin((0.30, 0.28, 0.26)), lin((0.42, 0.22, 0.10)))
    floor = g.mix(g.noise(8.0, 3.0), lin((0.24, 0.22, 0.20)), lin((0.32, 0.29, 0.26)))
    albedo = cc.paint(g, mask, (brick, plinth, glass, grille, roof, door, metal, concrete, dark, sand, team, coal, boiler, floor))
    amount = {"intact": 0.0, "occupied": 0.0, "damaged": 0.35, "damaged_occupied": 0.35, "abandoned": 0.5, "ruins": 0.6}[variant]
    if amount:
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, amount), 0.85), albedo, lin((0.06, 0.05, 0.05)))
    rough = cc.weighted(g, mask, (0.9, 0.9, 0.15, 0.5, 0.85, 0.5, 0.5, 0.9, 0.9, 0.95, 0.5, 0.7, 0.6, 0.95))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.6, 0.0, 0.5, 0.6, 0.0, 0.0, 0.0, 0.0, 0.0, 0.5, 0.0))
    height = g.math('MULTIPLY_ADD', g.math('ADD', mask[0], mask[1]), g.math('MULTIPLY', joints, -1.0), g.noise(60.0, 3.0))
    g.finish(albedo, g.bump(height, 0.05, 0.006), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "roof", "team_mask", "base"))
    rnd = ic.seed(variant_id)
    body, roof, team, base = Parts(), Parts(), Parts(), Parts()
    base.box((-0.98, -0.98, 0.0), (0.98, 0.98, APRON), CONCRETE)
    if variant == "ruins":
        _ruins(body, rnd)
        parts = [(body, "body"), (base, "base")]
    else:
        _walls(body, variant)
        _windows(body, variant)
        _door(body, team, variant)
        _outside(body, variant)
        _roof(roof, variant)
        if variant in ("occupied", "damaged_occupied"):
            _occupied(body)
        if variant in ("damaged", "damaged_occupied"):
            _damage(body, rnd)
        if variant == "abandoned":
            _abandoned(body, rnd)
        parts = [(body, "body"), (roof, "roof"), (team, "team_mask"), (base, "base")]
    return cc.finish(variant_id, parts, APRON - 0.001, _material(variant_id + "_mat", variant))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    fire = FIRE_POINTS if variant == "abandoned" else ()
    anchors = () if variant in ("abandoned", "ruins") else (("smoke_anchor", (STACK.x, STACK.y, STACK_TOP)),
                                                            ("steam_anchor", (VALVE.x, VALVE.y, TOP + 0.7)))
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, anchors)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
