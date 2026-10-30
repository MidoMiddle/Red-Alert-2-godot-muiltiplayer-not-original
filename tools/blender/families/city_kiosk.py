"""City kiosk: element 07 of the city set (docs/city_elements/07_kiosk) and its states (appearance.md, 4). City climate: dry daylight,
no snow. No soldiers go in (README.md, 3), so it has no occupied states and no team zone.

Footprint 1x1 cell, origin at the centre of the cell on the ground, the front to -Y. Made a storey to a cell, as the buildings of the map
(the game squashes it upright, game/data/visuals/models.yaml): the roof at 1.06, the sign on it to 1.3 (prompts.md, 1). The parts:
    pavilion  a small box of dark green vertical boards on a concrete pad, a flat roof slab over it; in front a serving hatch, light
              inside, behind a metal grille, a shelf under it and a white price board without text beside it; a door on the right
    sign      a small bright yellow board without text on the front of the roof
    canopy    a dark sloped canopy over the hatch with a bright red stripe along its edge
No emblems, no text, no seller in the hatch.
Nodes: "body" (the pavilion, the hatch, the door, the sign, and the shards or the wreck of a state), "canopy" (a node of its own: it
burns first, prompts.md, 4), "base" (the pad). The abandoned one also has fire_1..2. One GLB per state:
    city_kiosk              whole
    city_kiosk_damaged      damaged: the hatch's glass broken, a bar of the grille gone, the canopy torn, its right half hanging
    city_kiosk_abandoned    critically damaged: the pavilion skewed, the door hanging open, the canopy fallen down its front, the sign
                            on the ground
    city_kiosk_ruins        destroyed: low pieces of the walls, boards, the roof slab on the ground, glass, what is left of the sign
Budget 2000 triangles, texture 1024 (docs/city_elements/07_kiosk/prompts.md, 4).

In Blender, after tools/blender is on sys.path:
    import families.city_kiosk as cki
    print(cki.run("city_kiosk"))   # or cki.run_all()
"""

import math

import bmesh
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin

SIZE = 1024
VARIANTS = {
    "city_kiosk": "intact",
    "city_kiosk_damaged": "damaged",
    "city_kiosk_abandoned": "abandoned",
    "city_kiosk_ruins": "ruins",
}

BOARDS, ROOF, HATCH, METAL, WHITE, SIGN, CANOPY, CONCRETE, DARK, DOOR = (cc.part(i) for i in range(10))
PARTS = 10

PAD_H = 0.03
HX, HY = 0.32, 0.3
WALLS = cc.Facade(HX, HY)
TOP = 1.0
HATCH_Z = (0.45, 0.8)
FIRE_POINTS = ((0.0, -HY - 0.15, 0.85), (0.1, 0.0, TOP + 0.05))


# --- the pavilion ---------------------------------------------------------------------

def _pavilion(p, variant):
    """The box of boards and its roof slab, the hatch with its grille, shelf and price board, the door, the sign."""
    damaged = variant in ("damaged", "abandoned")
    top = [f for f in cc.made(p, lambda: p.box((-HX, -HY, PAD_H), (HX, HY, TOP), BOARDS))
           if abs(f.normal.z) > 0.99 and f.calc_center_median().z > TOP - 0.01]
    bmesh.ops.delete(p.bm, geom=top, context='FACES')  # under the roof slab: shut, it would bake black
    p.box((-HX - 0.04, -HY - 0.04, TOP), (HX + 0.04, HY + 0.04, TOP + 0.06), ROOF)
    z0, z1 = HATCH_Z
    WALLS.box(p, "front", -0.18, 0.18, z0, z1, -0.01, 0.004, DARK if damaged else HATCH)
    WALLS.box(p, "front", -0.21, 0.21, z0 - 0.03, z0, -0.01, 0.09, METAL)  # the shelf
    WALLS.box(p, "front", -0.21, 0.21, z1, z1 + 0.03, -0.01, 0.02, METAL)
    for k, u in enumerate(cc.steps(-0.15, 0.15, 0.06)):  # the grille
        if variant == "damaged" and k == 2:
            continue
        if variant != "abandoned":
            WALLS.box(p, "front", u - 0.006, u + 0.006, z0, z1, 0.0, 0.015, METAL)
    if variant != "abandoned":
        WALLS.box(p, "front", -0.18, 0.18, (z0 + z1) / 2 - 0.006, (z0 + z1) / 2 + 0.006, 0.0, 0.015, METAL)
    WALLS.box(p, "front", 0.22, 0.3, 0.5, 0.72, -0.01, 0.012, WHITE)  # the price board, no text
    # The door on the right side; hanging open from its hinge when abandoned.
    if variant == "abandoned":
        hinge = Vector((HX, -0.2, PAD_H))
        p.loft(_turned_box(hinge, (HX, -0.2, PAD_H + 0.02), (HX + 0.025, 0.2, 0.86), math.radians(-70)), DOOR)
        WALLS.box(p, "right", -0.2, 0.2, PAD_H + 0.02, 0.86, -0.01, 0.002, DARK)  # the dark doorway
    else:
        WALLS.box(p, "right", -0.2, 0.2, PAD_H + 0.02, 0.86, -0.01, 0.015, DOOR)
        WALLS.box(p, "right", 0.12, 0.16, 0.42, 0.46, 0.0, 0.03, METAL)  # the handle
    if variant != "abandoned":
        WALLS.box(p, "front", -0.22, 0.22, TOP + 0.06, TOP + 0.26, -0.03, 0.0, SIGN)


def _turned_box(hinge, lo, hi, angle):
    """The rings of a box from lo to hi turned by angle about the vertical through hinge: a door swung open."""
    m = Matrix.Rotation(angle, 3, 'Z')
    (x0, y0, z0), (x1, y1, z1) = lo, hi
    return [[hinge + m @ (Vector((x, y, z)) - hinge) for x, y in ((x0, y0), (x1, y0), (x1, y1), (x0, y1))] for z in (z0, z1)]


def _skew(p):
    """Skews the pavilion: its top pushed to the left and back, its foot where it was."""
    shear = Matrix(((1.0, 0.0, -0.09), (0.0, 1.0, 0.05), (0.0, 0.0, 1.0)))
    for v in p.bm.verts:
        v.co = shear @ v.co


# --- the canopy -----------------------------------------------------------------------

def _canopy(p, variant):
    """The dark canopy over the hatch, sloping from the roof to its striped edge. Damaged: torn in two, the right half hanging;
    abandoned: fallen down the front."""
    y_wall, y_edge = -HY - 0.04, -HY - 0.3
    z_wall, z_edge = TOP + 0.02, 0.9

    def sheet(x0, x1):
        p.loft([[Vector((x, y_wall, z_wall)), Vector((x, y_wall, z_wall + 0.025)), Vector((x, y_edge, z_edge + 0.025)),
                 Vector((x, y_edge, z_edge))] for x in (x0, x1)], CANOPY)

    if variant == "abandoned":
        cc.block(p, (0.0, -HY - 0.14, 0.45), (0.7, 0.025, 0.36), (0.25, 0.0, 0.12), CANOPY)
        return
    if variant == "damaged":
        sheet(-0.38, 0.02)
        cc.block(p, (0.2, -HY - 0.1, 0.84), (0.34, 0.2, 0.025), (-0.9, 0.35, 0.1), CANOPY)
    else:
        sheet(-0.38, 0.38)
    for x in (-0.33, 0.33):  # the arms
        if variant == "damaged" and x > 0:
            continue
        p.bar((x, y_wall, z_wall - 0.12), (x, y_edge + 0.02, z_edge), 0.01, METAL)


# --- the wreck ------------------------------------------------------------------------

SHARDS = tuple(((x, y, PAD_H + 0.004), (0.05, 0.035, 0.006), (0.0, 0.0, 1.1 * k))
               for k, (x, y) in enumerate(((-0.15, -0.42), (0.1, -0.45), (0.3, -0.38), (-0.32, -0.36))))
RUINS_PIECES = (((-HX, -HY, PAD_H), (-HX + 0.05, HY, 0.42)), ((-HX, HY - 0.05, PAD_H), (0.1, HY, 0.3)),
                ((HX - 0.05, 0.0, PAD_H), (HX, HY, 0.22)))
RUINS_BLOCKS = (((0.05, 0.0, 0.1), (0.72, 0.68, 0.06), (0.2, -0.15, 0.3)),
                ((0.25, -0.3, 0.07), (0.4, 0.06, 0.03), (0.1, 0.1, 0.6)), ((-0.2, -0.32, 0.06), (0.35, 0.05, 0.03), (0.0, 0.2, -0.4)),
                ((0.3, 0.3, 0.06), (0.4, 0.05, 0.03), (0.1, 0.0, 1.3)), ((-0.1, 0.38, 0.05), (0.3, 0.05, 0.03), (0.2, 0.1, 0.2)))


def _ruins(p):
    for lo, hi in RUINS_PIECES:
        p.box(lo, hi, BOARDS)
    for k, (centre, size, turn) in enumerate(RUINS_BLOCKS):
        cc.block(p, centre, size, turn, ROOF if k == 0 else BOARDS)
    cc.block(p, (-0.3, -0.42, 0.12), (0.3, 0.03, 0.18), (0.6, 0.0, 0.4), SIGN)  # what is left of the sign, leaning
    cc.block(p, (0.32, -0.42, 0.05), (0.3, 0.2, 0.02), (0.1, 0.2, 0.6), CANOPY)


# --- material -----------------------------------------------------------------------

def _material(name, variant):
    """Dark green boards, a dark roof, a light hatch, metal, a white board, a bright yellow sign, a dark canopy with a red stripe along
    its edge, concrete; per state, scorching."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.15)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    gap = g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 16.0)), -14.0, 1.6))  # board joints
    boards = g.mix(gap, g.mix(g.noise(4.0, 6.0), lin((0.12, 0.30, 0.20)), lin((0.16, 0.36, 0.24))), lin((0.06, 0.14, 0.10)))
    roof = g.mix(grain, lin((0.20, 0.21, 0.22)), lin((0.26, 0.27, 0.28)))
    hatch = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, 2.0, -0.9)), lin((0.80, 0.74, 0.58)), lin((0.92, 0.88, 0.76)))
    metal = g.mix(grain, lin((0.30, 0.32, 0.34)), lin((0.40, 0.42, 0.44)))
    white = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    sign = g.mix(grain, lin((0.90, 0.72, 0.16)), lin((0.96, 0.80, 0.22)))
    stripe = g.clamp01(g.math('MULTIPLY_ADD', y, -40.0, -20.8))  # the last eight centimetres of the canopy's edge
    canopy = g.mix(stripe, g.mix(grain, lin((0.14, 0.15, 0.17)), lin((0.19, 0.20, 0.22))), lin((0.78, 0.16, 0.14)))
    concrete = g.mix(grain, lin((0.56, 0.55, 0.52)), lin((0.64, 0.63, 0.60)))
    dark = g.mix(grain, lin((0.05, 0.05, 0.05)), lin((0.10, 0.10, 0.10)))
    door = g.mix(gap, g.mix(g.noise(4.0, 6.0), lin((0.10, 0.24, 0.16)), lin((0.13, 0.29, 0.20))), lin((0.05, 0.12, 0.08)))

    albedo = cc.paint(g, mask, (boards, roof, hatch, metal, white, sign, canopy, concrete, dark, door))
    if variant in ("damaged", "abandoned", "ruins"):
        scorch = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 5.0, -2.8 if variant == "damaged" else -2.2))
        albedo = g.mix(g.math('MULTIPLY', scorch, 0.8), albedo, lin((0.08, 0.07, 0.07)))

    rough = cc.weighted(g, mask, (0.8, 0.8, 0.4, 0.45, 0.6, 0.5, 0.85, 0.9, 0.9, 0.8))
    metallic = cc.weighted(g, mask, (0.0, 0.3, 0.0, 0.7, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', g.math('ADD', mask[0], mask[9]), g.math('MULTIPLY', gap, -1.0), g.noise(60.0, 3.0)), 0.05, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    cc.clear(variant_id, ("body", "canopy", "base"))
    variant = VARIANTS[variant_id]
    body = Parts()
    if variant == "ruins":
        _ruins(body)
    else:
        _pavilion(body, variant)
        if variant == "abandoned":
            _skew(body)
            cc.block(body, (0.3, -0.4, PAD_H + 0.04), (0.44, 0.04, 0.2), (1.35, 0.0, 0.5), SIGN)  # the sign, fallen
    if variant != "intact":
        for centre, size, turn in SHARDS:
            cc.block(body, centre, size, turn, HATCH)
    parts = [(body, "body")]
    if variant != "ruins":
        canopy = Parts()
        _canopy(canopy, variant)
        parts.append((canopy, "canopy"))
    base = Parts()
    base.box((-0.48, -0.48, 0.0), (0.48, 0.48, PAD_H), CONCRETE)
    parts.append((base, "base"))
    return cc.finish(variant_id, parts, PAD_H, _material(variant_id + "_mat", variant))


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, canopy and base (fire_1..2 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    fire = FIRE_POINTS if VARIANTS[variant_id] == "abandoned" else ()
    return cc.run(variant_id, meshes, mat, graph, SIZE, fire)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
