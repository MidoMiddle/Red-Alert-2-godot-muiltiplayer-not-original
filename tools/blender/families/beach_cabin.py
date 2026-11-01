"""Beach cabin, element 06 of the beach set (docs/beach_elements/06_beach_cabin): a painted wooden changing hut on the sand.

The map building neutral_beach_cabin (game/data/neutral/civilian.yaml): 1x1, 300 hp, no soldiers go in. Footprint 1x1 cell, origin at
the centre of the cell on the ground, the front to -Y. Made a storey to a cell, as the buildings of the map (the game squashes it upright,
game/data/visuals/models.yaml), as the city's kiosk: the roof's high edge at 1.3 over the front, 1.08 at the back. The parts:
    hut     walls of vertical painted boards, white corner trims, a lean-to roof of grey boards sloping to the back and overhanging, a small
            window high on the right side, a vent of slats over the door
    door    the door in the front, in a contrasting colour, its handle; torn off and lying on the sand when damaged, hanging open when
            abandoned
    base    a low deck of planks on the sand it stands on, a step before the door
Nodes: "body", "door", "base"; the abandoned one also fire_1..2. One GLB per state; the whole one in three paints (each cell picks one),
the damaged and the burning one with their paint peeled and scorched down to the grey wood, so a cabin that changes paint as it is hurt
does not show:
    beach_cabin            whole, light blue, a white door
    beach_cabin_white      whole, white, a blue door
    beach_cabin_yellow     whole, yellow, a white door
    beach_cabin_damaged    damaged: the door torn off and lying before it, a hole kicked in the left wall, the window's glass out,
                           the paint peeling, soot
    beach_cabin_abandoned  burning: the hut leaning, the door hanging open, the roof's corner fallen in, charred, fire_1..2
    beach_cabin_ruins      burnt down: the charred stumps of the corners and a low bit of the back wall, the roof's boards fallen over
                           the deck, the door on the sand
Budget 2000 triangles, texture 1024.

In Blender, after tools/blender is on sys.path:
    import families.beach_cabin as cabin
    print(cabin.run_all())
"""

import math

import bmesh
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin

FOLDER = "beach"
SIZE = 1024
# State, paint of the walls, paint of the door.
VARIANTS = {
    "beach_cabin": ("intact", (0.48, 0.72, 0.86), (0.94, 0.93, 0.90)),
    "beach_cabin_white": ("intact", (0.92, 0.91, 0.88), (0.22, 0.42, 0.70)),
    "beach_cabin_yellow": ("intact", (0.96, 0.82, 0.36), (0.94, 0.93, 0.90)),
    "beach_cabin_damaged": ("damaged", (0.48, 0.72, 0.86), (0.94, 0.93, 0.90)),
    "beach_cabin_abandoned": ("abandoned", (0.48, 0.72, 0.86), (0.94, 0.93, 0.90)),
    "beach_cabin_ruins": ("ruins", (0.48, 0.72, 0.86), (0.94, 0.93, 0.90)),
}

BOARDS, TRIM, ROOF, DOOR, GLASS, METAL, DECK, DARK = (cc.part(i) for i in range(8))
PARTS = 8

DECK_H = 0.05
HX, HY = 0.28, 0.28
WALLS = cc.Facade(HX, HY)
FRONT_TOP, BACK_TOP = 1.3, 1.08
FIRE_POINTS = ((0.0, -0.05, 0.9), (-0.15, 0.15, 1.15))


def _top(y):
    """The height of the walls' top at y: the roof slopes from the front down to the back."""
    return FRONT_TOP + (BACK_TOP - FRONT_TOP) * (y + HY) / (2 * HY)


def _hut(p, variant):
    """The walls as one box whose top follows the slope, the trims, the roof, the window and the vent."""
    damaged = variant != "intact"
    rings = []
    for z in (DECK_H, None):
        rings.append([Vector((x, y, _top(y) if z is None else z)) for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY))])
    top = [f for f in cc.made(p, lambda: p.loft(rings, BOARDS)) if f.normal.z > 0.5]
    bmesh.ops.delete(p.bm, geom=top, context='FACES')  # under the roof: shut, it would bake black
    for x, y in ((-HX, -HY), (HX, -HY), (HX, HY), (-HX, HY)):  # the corner trims
        p.box((x - 0.022, y - 0.022, DECK_H), (x + 0.022, y + 0.022, _top(y) - 0.01), TRIM)
    # The roof: grey boards, overhanging in front and at the sides, sloping to the back as the walls' top does.
    over = 0.07
    roof = [[Vector((x, y, _top(y) + dz)) for x, y in ((-HX - over, -HY - over * 1.6), (HX + over, -HY - over * 1.6), (HX + over, HY + over),
                                                       (-HX - over, HY + over))] for dz in (0.0, 0.035)]
    p.loft(roof, ROOF)
    # The small window high on the right side, its glass out when damaged; the vent of slats over the door.
    WALLS.box(p, "right", -0.08, 0.08, 0.86, 1.0, -0.01, 0.006, DARK if damaged else GLASS)
    WALLS.box(p, "right", -0.1, 0.1, 0.84, 0.86, -0.01, 0.02, TRIM)
    WALLS.box(p, "right", -0.1, 0.1, 1.0, 1.02, -0.01, 0.02, TRIM)
    for k in range(3):
        WALLS.box(p, "front", -0.1, 0.1, 1.08 + 0.04 * k, 1.1 + 0.04 * k, -0.01, 0.015, TRIM)
    if variant == "damaged":
        WALLS.box(p, "left", -0.12, 0.06, 0.2, 0.5, -0.01, 0.003, DARK)  # the hole kicked in
        for u, z in ((-0.1, 0.55), (0.09, 0.2)):  # split boards about it
            WALLS.box(p, "left", u - 0.02, u + 0.02, z - 0.08, z + 0.08, 0.0, 0.012, BOARDS)


DOOR_U, DOOR_TOP = (-0.13, 0.13), 0.98


def _door(p, variant):
    u0, u1 = DOOR_U
    if variant == "intact":
        WALLS.box(p, "front", u0, u1, DECK_H + 0.01, DOOR_TOP, -0.01, 0.015, DOOR)
        WALLS.box(p, "front", u1 - 0.05, u1 - 0.03, 0.5, 0.58, 0.0, 0.035, METAL)
        return
    if variant == "damaged":
        # Torn off, lying on the sand before the step, a little askew.
        cc.block(p, (0.05, -HY - 0.32, 0.012), (u1 - u0, DOOR_TOP - DECK_H, 0.022), (0.0, 0.0, 0.25), DOOR)
        return
    if variant == "abandoned":
        hinge = Vector((u0, -HY - 0.015, 0.0))
        m = Matrix.Rotation(math.radians(-75), 3, 'Z')  # swung out to the left
        rings = [[hinge + m @ (Vector((x, y, z)) - hinge) for x, y in ((u0, -HY - 0.03), (u1, -HY - 0.03), (u1, -HY), (u0, -HY))]
                 for z in (DECK_H + 0.04, DOOR_TOP - 0.03)]
        p.loft(rings, DOOR)
        return
    cc.block(p, (0.32, -HY - 0.25, 0.012), (u1 - u0, DOOR_TOP - DECK_H - 0.2, 0.022), (0.0, 0.05, -0.6), DOOR)  # ruins: on the sand


def _doorway(p):
    """The dark doorway of a cabin whose door is gone or open."""
    u0, u1 = DOOR_U
    WALLS.box(p, "front", u0, u1, DECK_H + 0.01, DOOR_TOP, -0.01, 0.002, DARK)


def _lean(p):
    """The burning hut leans: its top pushed to the right and back; one corner of the roof fallen in."""
    shear = Matrix(((1.0, 0.0, 0.07), (0.0, 1.0, 0.05), (0.0, 0.0, 1.0)))
    for v in p.bm.verts:
        v.co = shear @ v.co
        if v.co.x < -0.1 and v.co.y > 0.1 and v.co.z > 0.95:
            v.co.z -= 0.22 * min(1.0, (-0.1 - v.co.x) / 0.25)


def _ruins(p):
    for x, y, h in ((-HX, -HY, 0.42), (HX, -HY, 0.3), (HX, HY, 0.5), (-HX, HY, 0.36)):  # the corners' stumps
        p.box((x - 0.025, y - 0.025, DECK_H), (x + 0.025, y + 0.025, DECK_H + h), BOARDS)
    p.box((-HX, HY - 0.03, DECK_H), (0.12, HY, 0.26), BOARDS)  # a low bit of the back wall
    for k, (c, size, turn) in enumerate((((0.0, 0.02, 0.14), (0.62, 0.5, 0.03), (0.25, -0.18, 0.2)),
                                         ((-0.18, -0.12, 0.08), (0.4, 0.06, 0.03), (0.1, 0.05, 0.9)),
                                         ((0.15, 0.2, 0.08), (0.35, 0.06, 0.03), (0.05, 0.1, -0.5)),
                                         ((0.2, -0.2, 0.07), (0.3, 0.05, 0.03), (0.0, 0.15, 1.4)))):
        cc.block(p, c, size, turn, ROOF if k == 0 else BOARDS)


def _base(p):
    p.box((-0.4, -0.4, 0.0), (0.4, 0.4, DECK_H), DECK)
    p.box((-0.18, -0.52, 0.0), (0.18, -0.4, DECK_H * 0.6), DECK)  # the step


def _material(name, variant, wall_paint, door_paint):
    """Painted vertical boards with dark joints, white trims, grey roof boards, the door's paint, light glass, metal, the deck's planks
    weathered; damaged and burning: the paint peeled to grey wood and soot; ruins: charred."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.12)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x, y, z = (g.axis(coord, a) for a in ('X', 'Y', 'Z'))
    grain = g.noise(30.0, 4.0)
    joints = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 14.0)), -14.0, 1.6))
    wood = g.mix(g.noise(5.0, 4.0), lin((0.46, 0.43, 0.38)), lin((0.56, 0.52, 0.46)))  # bare weathered wood
    paint = g.mix(g.math('MULTIPLY', g.noise(8.0, 3.0), 0.25), lin(wall_paint), lin((0.9, 0.9, 0.88)))
    if variant != "intact":
        peeled = g.clamp01(g.math('MULTIPLY_ADD', g.noise(6.0, 4.0), 6.0, -2.4))
        paint = g.mix(peeled, paint, wood)
    boards = g.mix(joints, paint, lin((0.12, 0.12, 0.12)))
    trim = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    roof = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 12.0)), -14.0, 1.6)),
                 g.mix(grain, lin((0.48, 0.49, 0.50)), lin((0.58, 0.58, 0.58))), lin((0.25, 0.25, 0.26)))
    door = g.mix(joints, g.mix(g.math('MULTIPLY', g.noise(8.0, 3.0), 0.2), lin(door_paint), lin((0.9, 0.9, 0.88))), lin((0.14, 0.14, 0.14)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, 4.0, -3.5)), lin((0.48, 0.60, 0.66)), lin((0.80, 0.88, 0.92)))
    metal = lin((0.32, 0.33, 0.35))
    deck = g.mix(g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 10.0)), -14.0, 1.6)),
                 g.mix(grain, lin((0.56, 0.48, 0.38)), lin((0.64, 0.57, 0.46))), lin((0.30, 0.25, 0.20)))
    dark = lin((0.05, 0.05, 0.05))
    albedo = cc.paint(g, mask, (boards, trim, roof, door, glass, metal, deck, dark))
    if variant != "intact":
        amount = {"damaged": -3.0, "abandoned": -1.8, "ruins": 0.2}[variant]
        scorch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, amount))
        albedo = g.mix(g.math('MULTIPLY', scorch, 0.85), albedo, lin((0.07, 0.06, 0.06)))
        if variant == "ruins":
            albedo = g.mix(0.6, albedo, lin((0.14, 0.12, 0.10)))  # burnt down: charred all over
    # Sand blown against the foot of everything.
    sand = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -30.0, 1.4)), g.clamp01(g.math('MULTIPLY_ADD', g.noise(10.0, 2.0), 3.0, -1.0)))
    sand = g.math('MULTIPLY', sand, 0.3 if variant == "ruins" else 1.0)
    albedo = g.mix(sand, albedo, lin((0.86, 0.78, 0.62)))
    rough = cc.weighted(g, mask, (0.75, 0.6, 0.85, 0.7, 0.15, 0.45, 0.9, 0.9))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.7, 0.0, 0.0))
    normal = g.bump(g.math('ADD', g.math('MULTIPLY', joints, -1.0), g.math('MULTIPLY', g.noise(60.0, 3.0), 0.3)), 0.06, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant, wall_paint, door_paint = VARIANTS[variant_id]
    ic.clear(variant_id, ("body", "door", "base"))
    body, door, base = Parts(), Parts(), Parts()
    if variant == "ruins":
        _ruins(body)
    else:
        _hut(body, variant)
        if variant != "intact":
            _doorway(body)
        if variant == "abandoned":
            _lean(body)
    _door(door, variant)
    _base(base)
    return cc.finish(variant_id, [(body, "body"), (door, "door"), (base, "base")], DECK_H,
                     _material(variant_id + "_mat", variant, wall_paint, door_paint))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id][0] == "abandoned" else ()
    return ic.run(variant_id, meshes, mat, graph, SIZE, fire, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
