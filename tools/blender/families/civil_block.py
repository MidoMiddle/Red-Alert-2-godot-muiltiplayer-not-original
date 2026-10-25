"""Civilian apartment block: its states (docs/map_elements/11_civilian_buildings/prompts.md, 3.4) and the soldiers inside.

Footprint 3x3 cells, origin at the centre of the footprint on the ground. Five storeys: the walls 5.6 high, the penthouse to 6.2.
The block stands on a concrete foundation, a plinth a few centimetres wider than the walls (docs/art/model_standards.md, 3.1).
Nodes: "body" (walls, floors, the canopy, the balcony that fell, the rubble, rifles or sandbags of a state), "roof" (the cornice,
the penthouse, the air conditioners, the sandbags along the edge of the flat roof), "team_mask" (the band under the cornice that the
game paints in the owner's colour), "base" (the concrete foundation). The abandoned one also has the empty nodes fire_1..4, where the
game stands the flames. One GLB per state:
    building_apartment_block                    whole, empty
    building_apartment_block_occupied           whole, occupied by infantry: rifle barrels out of the windows, sandbags round the foot and along the roof's edge
    building_apartment_block_damaged            damaged, empty: windows knocked out, cracks, soot over the openings, a balcony fallen
    building_apartment_block_damaged_occupied   damaged, occupied by infantry: the same, with rifles and sandbags
    building_apartment_block_abandoned          critically damaged, its soldiers out: the back gone, the right side and the top storeys collapsed, the floors left inside
    building_apartment_block_ruins              destroyed: the stumps of the walls, the floors left on them, the rubble, bars of steel
Texture 2048: a multi-storey building (prompts.md, section 4); the ruins 1024.

In Blender, after rts_common is on sys.path:
    import families.civil_block as cb
    print(cb.run("building_apartment_block"))
"""

import os

import bmesh
from mathutils import Vector

import rts_common as rc

SIZE = 2048
WALL_T = 0.3
H_WALL = 5.6
H_TOP = 6.2
STOREY = 1.12
# The concrete foundation: its top is FOUNDATION_H high, its edge FOUNDATION_D from the centre (the walls stand at 1.5).
FOUNDATION_H = 0.2
FOUNDATION_D = 1.56
# The centre line of the sandbags round the block: their inner edge touches the walls, which stand at 1.5.
SANDBAG_D = 1.64
QUARTER_TURN = 1.5707963267948966

VARIANTS = {
    "building_apartment_block": "intact",
    "building_apartment_block_occupied": "occupied",
    "building_apartment_block_damaged": "damaged",
    "building_apartment_block_damaged_occupied": "damaged_occupied",
    "building_apartment_block_abandoned": "abandoned",
    "building_apartment_block_ruins": "ruins",
}

# The flames of the abandoned block: the broken top of the front wall, the top of the collapsed right side, the cornice on the left.
FIRE_POINTS = ((0.5, -1.55, 3.7), (1.6, -0.4, 3.7), (-1.0, -1.6, 5.6), (-1.6, 0.4, 5.6))

# The rubble on the ground round the abandoned block: x, y of its corner, its side, its height.
RUBBLE = (
    (1.55, -1.95, 0.4, 0.25),
    (1.85, -1.35, 0.3, 0.2),
    (1.7, -0.5, 0.25, 0.18),
    (1.9, 0.3, 0.35, 0.22),
    (-1.9, -1.7, 0.3, 0.2),
    (0.2, -1.95, 0.25, 0.15),
)


# The rubble of the ruins on the footprint: x, y of its corner, its side, its height.
RUBBLE_BLOCK = (
    (-1.0, -0.9, 0.35, 0.3),
    (-0.5, -0.6, 0.4, 0.45),
    (-0.1, -0.9, 0.3, 0.25),
    (0.3, -0.5, 0.45, 0.4),
    (0.8, -0.8, 0.35, 0.3),
    (-0.9, 0.1, 0.4, 0.35),
    (-0.3, 0.2, 0.5, 0.55),
    (0.2, 0.3, 0.35, 0.3),
    (0.7, 0.2, 0.4, 0.45),
    (-0.8, 0.9, 0.35, 0.3),
    (-0.1, 0.8, 0.3, 0.25),
    (0.5, 0.9, 0.35, 0.35),
    (1.0, -0.2, 0.3, 0.2),
)

# The bars of steel out of the heap: from one point to another.
BARS_BLOCK = (
    ((-0.2, 0.3, 0.6), (-0.6, -1.25, 1.6)),
    ((0.4, 0.2, 0.7), (1.45, -1.25, 0.6)),
    ((0.0, 0.6, 0.5), (0.5, 1.45, 1.9)),
    ((-0.8, 0.0, 0.4), (-1.3, 0.8, 1.0)),
)

# The ruins are 1024 (prompts.md, section 4): a heap is not a multi-storey building.
RUINS_SIZE = 1024


def _foundation(bm):
    """The concrete foundation: a plinth under the walls, a few centimetres wider than them, so the block stands on it."""
    rc.add_box(bm, (-FOUNDATION_D, -FOUNDATION_D, 0.0), (FOUNDATION_D, FOUNDATION_D, FOUNDATION_H))


def _sandbags(bm):
    """Sandbags round the foot of the block, two rows in running bond with gaps between the bags; the door stays open."""
    for z, offset in ((0.0, 0.0), (0.15, 0.18)):
        for t in (-1.44, -1.08, -0.72, -0.36, 0.0, 0.36, 0.72, 1.08, 1.44):
            along = t + offset
            if abs(along) > 1.5:
                continue
            if abs(along) >= 0.55:
                rc.add_sandbag(bm, along, -SANDBAG_D, z, 0.0, 0.32, 0.22, 0.17)  # the front, without the door
            rc.add_sandbag(bm, along, SANDBAG_D, z, 0.0, 0.32, 0.22, 0.17)  # the back
            rc.add_sandbag(bm, -SANDBAG_D, along, z, QUARTER_TURN, 0.32, 0.22, 0.17)  # the left side
            rc.add_sandbag(bm, SANDBAG_D, along, z, QUARTER_TURN, 0.32, 0.22, 0.17)  # the right side


def _body(bm, variant):
    if variant == "abandoned":
        rc.add_box(bm, (-1.5, -1.5, 0.0), (0.5, -1.5 + WALL_T, H_WALL))  # front wall, the left part
        rc.add_box(bm, (0.5, -1.5, 0.0), (1.5, -1.5 + WALL_T, 3.6))  # front wall, the right part: its top is gone
        rc.add_box(bm, (-1.5, -1.5, 0.0), (-1.5 + WALL_T, 1.5, H_WALL))  # left wall, the back is gone
        rc.add_box(bm, (1.5 - WALL_T, -1.5, 0.0), (1.5, 0.0, 3.6))  # right wall, the front half: its top is gone
        rc.add_box(bm, (1.5 - WALL_T, 0.0, 0.0), (1.5, 1.5, 1.2))  # right wall, the back half: a stub
        # The floors left inside, one storey up each: the front and the left walls hold them, the broken side shows their ends.
        rc.add_box(bm, (-1.35, -1.35, 1.12), (1.35, 1.5, 1.26))  # storey 1, whole
        rc.add_box(bm, (-1.35, -1.35, 2.24), (1.35, 0.0, 2.38))  # storey 2, the front half
        rc.add_box(bm, (-1.35, 0.0, 2.24), (0.4, 1.5, 2.38))  # storey 2, the back half, broken at x 0.4
        rc.add_box(bm, (-1.35, -1.35, 3.36), (1.35, 0.0, 3.5))  # storey 3, the front half
        rc.add_box(bm, (-1.35, 0.0, 3.36), (0.0, 0.9, 3.5))  # storey 3, the back corner, broken
        rc.add_box(bm, (-1.35, -1.35, 4.48), (0.5, 0.9, 4.62))  # storey 4, the left part: the right part fell
        for x, y, side, height in RUBBLE:
            rc.add_box(bm, (x, y, 0.0), (x + side, y + side, height))
    else:
        rc.add_box(bm, (-1.5, -1.5, 0.0), (1.5, 1.5, H_WALL))  # the block, solid: its windows are in the texture
        rc.add_box(bm, (-0.7, -1.85, 2.05), (0.7, -1.5, 2.15))  # the canopy over the entrance
    if variant in ("damaged", "damaged_occupied"):
        rc.add_box(bm, (1.0, -2.35, 0.0), (1.9, -1.95, 0.22))  # a balcony fallen on the ground, from the front, clear of the sandbags
    if variant in ("occupied", "damaged_occupied"):
        for x in (-0.67, -0.05, 0.57, 1.19):
            rc.add_stick(bm, Vector((x, -1.45, 0.55)), Vector((x, -1.9, 0.62)), 0.03)  # rifle barrels out of the ground windows
        _sandbags(bm)
    if variant == "damaged_occupied":
        for x in (-0.05, 0.57):
            rc.add_stick(bm, Vector((x, -1.45, 1.67)), Vector((x, -1.9, 1.74)), 0.03)  # and out of two windows of the second storey


def _roof(bm, variant):
    if variant == "abandoned":
        rc.add_box(bm, (-1.58, -1.58, H_WALL), (0.3, 1.58, H_WALL + 0.1))  # the cornice over the part that is left
        return
    rc.add_box(bm, (-1.58, -1.58, H_WALL), (1.58, 1.58, H_WALL + 0.1))  # the cornice, which is the roof too
    rc.add_box(bm, (-0.9, -0.9, H_WALL + 0.1), (0.9, 0.9, H_TOP))  # the penthouse, set back from the walls
    rc.add_box(bm, (0.95, -0.85, H_WALL + 0.1), (1.35, -0.35, H_WALL + 0.4))  # an air conditioner
    rc.add_box(bm, (-1.2, 0.35, H_WALL + 0.1), (-0.8, 0.85, H_WALL + 0.35))  # another one
    if variant in ("occupied", "damaged_occupied"):
        # Sandbags along the edge of the flat roof, one row on each side; the penthouse and the air conditioners stay clear of them.
        for t in (-1.4, -1.0, -0.6, -0.2, 0.2, 0.6, 1.0, 1.4):
            rc.add_sandbag(bm, -1.46, t, H_WALL + 0.1, QUARTER_TURN, 0.34, 0.22, 0.16)
            rc.add_sandbag(bm, 1.46, t, H_WALL + 0.1, QUARTER_TURN, 0.34, 0.22, 0.16)
            rc.add_sandbag(bm, t, -1.46, H_WALL + 0.1, 0.0, 0.34, 0.22, 0.16)
            rc.add_sandbag(bm, t, 1.46, H_WALL + 0.1, 0.0, 0.34, 0.22, 0.16)


def _team(bm, variant):
    # The band under the cornice, on the front; the abandoned block keeps it over the part that stands.
    right = 0.3 if variant == "abandoned" else 1.2
    rc.add_box(bm, (-1.2, -1.53, 5.36), (right, -1.5, 5.52))


def _material(mat_name, variant):
    """Concrete walls with rows of windows, a door, a grey roof, the neutral band under the cornice and, per state, cracks, soot, rifles or sandbags."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(12.0, 4.0)
    outside = g.math('MAXIMUM', g.math('ABSOLUTE', x), g.math('ABSOLUTE', y))  # the distance from the centre, the square way
    # Windows: a column every 0.62 along the walls (x + y works on all four), a row every storey; the glass fills the middle of each.
    fu = g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 1.0 / 0.62))
    win_u = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', fu, 30.0, -6.0)),
                   g.clamp01(g.math('MULTIPLY_ADD', fu, -30.0, 24.0)))
    fz = g.math('FRACT', g.math('MULTIPLY', z, 1.0 / STOREY))
    win_z = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', fz, 40.0, -8.8)),
                   g.clamp01(g.math('MULTIPLY_ADD', fz, -40.0, 30.4)))
    window = g.math('MULTIPLY', win_u, win_z)
    # Door: centred on the front, 0.95 high, in the ground storey.
    door = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', x), -40.0, 12.0)),
                  g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 38.0)),
                         g.clamp01(g.math('MULTIPLY_ADD', y, -40.0, -56.0))))
    # Band under the cornice: the game paints it in the owner's colour.
    band = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', x), -40.0, 48.0)),
                  g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 40.0, -214.4)),
                         g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 220.8)),
                                g.clamp01(g.math('MULTIPLY_ADD', y, -40.0, -58.0)))))
    roof = g.clamp01(g.math('MULTIPLY_ADD', z, 40.0, -224.0))  # above the walls
    concrete = g.mix(grain, (0.56, 0.54, 0.50), (0.68, 0.66, 0.62))
    if variant in ("abandoned", "damaged", "damaged_occupied"):
        crack = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', grain, 0.5)), -100.0, 1.0))
        concrete = g.mix(crack, concrete, (0.12, 0.11, 0.10))
    base = g.mix(window, concrete, (0.07, 0.09, 0.11))
    if variant in ("damaged", "damaged_occupied"):
        # Windows knocked out in clusters, and soot over the openings: the band just above each window.
        broken = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 10.0, -5.5))
        base = g.mix(g.math('MULTIPLY', window, broken), base, (0.03, 0.03, 0.03))
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', fz, 40.0, -28.8)),
                      g.clamp01(g.math('MULTIPLY_ADD', fz, -40.0, 38.0)))
        base = g.mix(soot, base, (0.14, 0.12, 0.11))
    # The foundation (the base node): the concrete, a little darker than the walls, in the band it stands in.
    foundation = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 12.0)),
                        g.clamp01(g.math('MULTIPLY_ADD', outside, -60.0, 94.2)))
    base = g.mix(foundation, base, g.mix(grain, (0.42, 0.41, 0.39), (0.50, 0.49, 0.46)))
    base = g.mix(door, base, (0.30, 0.20, 0.12))
    roof_col = g.mix(grain, (0.24, 0.23, 0.22), (0.32, 0.30, 0.28))
    base = g.mix(roof, base, roof_col)
    base = g.mix(band, base, (0.86, 0.86, 0.86))
    metal = 0.0
    rough = g.math('MULTIPLY_ADD', window, -0.65, 0.85)  # glass 0.2, concrete 0.85
    if variant in ("occupied", "damaged_occupied"):
        # The barrels and the canopy: beyond y -1.55, between the sandbags (above 0.45) and the roof's edge (below 2.2).
        rifle = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', y, -40.0, -62.0)),
                       g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 20.0, -9.0)),
                              g.clamp01(g.math('MULTIPLY_ADD', z, -20.0, 44.0))))
        base = g.mix(rifle, base, (0.25, 0.25, 0.27))
        metal = g.math('MULTIPLY', rifle, 0.8)
        # Sandbags round the foot and along the roof's edge: the sand colour, outside the walls (at 1.5) and low, or on the roof.
        bags_low = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', outside, 60.0, -90.6)),
                          g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 20.4)))
        bags_roof = g.math('MULTIPLY', g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 60.0, -343.2)),
                                              g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 355.2))),
                           g.clamp01(g.math('MULTIPLY_ADD', outside, 60.0, -78.0)))
        sand = g.mix(g.noise(25.0, 4.0), (0.42, 0.36, 0.25), (0.60, 0.52, 0.36))  # grains of the sand, darker than the concrete
        base = g.mix(bags_low, base, sand)
        base = g.mix(bags_roof, base, sand)
    normal = g.bump(g.noise(18.0, 3.0), 0.06, 0.01)
    orm = g.combine(ao, rough, metal)
    g.finish(base, normal, orm)
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    bm_body = bmesh.new()
    _body(bm_body, variant)
    bmesh.ops.recalc_face_normals(bm_body, faces=list(bm_body.faces))
    body = rc.make_object("block_body", bm_body)
    bm_roof = bmesh.new()
    _roof(bm_roof, variant)
    bmesh.ops.recalc_face_normals(bm_roof, faces=list(bm_roof.faces))
    roof = rc.make_object("block_roof", bm_roof)
    bm_team = bmesh.new()
    _team(bm_team, variant)
    bmesh.ops.recalc_face_normals(bm_team, faces=list(bm_team.faces))
    team = rc.make_object("block_team", bm_team)
    bm_base = bmesh.new()
    _foundation(bm_base)
    bmesh.ops.recalc_face_normals(bm_base, faces=list(bm_base.faces))
    base = rc.make_object("block_base", bm_base)
    objs = [body, roof, team, base]
    for obj, lo, hi in ((body, 0.0, 0.6), (roof, 0.6, 0.8), (team, 0.8, 0.9), (base, 0.9, 1.0)):
        rc.uv_smart(obj)
        rc.place_uv(obj, lo, hi)
    mat, graph = _material(variant_id + "_mat", variant)
    for obj in objs:
        obj.data.materials.append(mat)
    return objs, mat, graph


def _ruins(bm):
    """The ruins of the block: the stumps of its walls with the floors left on them, a heap of rubble, bars of steel out of the heap."""
    rc.add_box(bm, (-1.5, -1.5, 0.0), (-0.6, -1.2, 1.8))  # front wall, the left stump, the highest piece
    rc.add_box(bm, (0.2, -1.5, 0.0), (1.5, -1.2, 0.7))  # front wall, the right part, low
    rc.add_box(bm, (-1.5, -1.5, 0.0), (-1.2, -0.2, 1.2))  # left wall, a stub
    rc.add_box(bm, (1.2, 0.2, 0.0), (1.5, 1.5, 0.5))  # right wall, low
    rc.add_box(bm, (-0.4, 1.2, 0.0), (0.6, 1.5, 3.6))  # back wall, the tallest jag: it holds the floors of the second and third storeys
    rc.add_box(bm, (-0.9, -0.7, 0.0), (0.4, 0.6, 0.15))  # a floor slab on the ground, broken
    rc.add_box(bm, (-1.5, -0.2, 0.8), (-0.3, 0.4, 0.9))  # a slab hanging off the left wall
    rc.add_box(bm, (-1.35, -1.35, 1.12), (-0.2, -0.45, 1.26))  # a floor of the first storey, on the left stump
    rc.add_box(bm, (-0.4, 0.5, 2.24), (0.6, 1.35, 2.38))  # a floor of the second storey, on the jag
    rc.add_box(bm, (-0.4, 0.9, 3.36), (0.6, 1.35, 3.5))  # a floor of the third storey, on the jag
    for x, y, side, height in RUBBLE_BLOCK:
        rc.add_box(bm, (x, y, 0.0), (x + side, y + side, height))
    for p1, p2 in BARS_BLOCK:
        rc.add_stick(bm, Vector(p1), Vector(p2), 0.035)  # steel out of the heap


def build_ruins(variant_id):
    """The ruins: the stumps and the rubble on the foundation, no roof and no band; the game draws them where the block fell."""
    bm = bmesh.new()
    _ruins(bm)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    body = rc.make_object("block_body", bm)
    rc.uv_smart(body)
    rc.place_uv(body, 0.0, 0.6)
    bm_base = bmesh.new()
    _foundation(bm_base)
    bmesh.ops.recalc_face_normals(bm_base, faces=list(bm_base.faces))
    base = rc.make_object("block_base", bm_base)
    rc.uv_smart(base)
    rc.place_uv(base, 0.6, 1.0)
    mat, graph = rc.ruins_material(variant_id + "_mat")
    body.data.materials.append(mat)
    base.data.materials.append(mat)
    return [body, base], mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..4 when abandoned)."""
    objs, mat, graph = build_ruins(variant_id) if VARIANTS[variant_id] == "ruins" else build(variant_id)
    nodes = rc.fire_nodes(FIRE_POINTS) if VARIANTS[variant_id] == "abandoned" else []
    rc.arrange(objs + nodes, variant_id)
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in objs)}, "
              f"size {tuple(round(v, 3) for v in objs[0].dimensions)}"]
    size = RUINS_SIZE if VARIANTS[variant_id] == "ruins" else SIZE
    imgs = rc.bake_material(objs, graph, size, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    names = ["body", "base"] if VARIANTS[variant_id] == "ruins" else ["body", "roof", "team_mask", "base"]
    with rc.node_names(objs + nodes, names + rc.fire_names(nodes)):
        rc.save_blend(objs + nodes, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs + nodes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
