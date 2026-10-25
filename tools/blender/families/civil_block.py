"""Civilian apartment block with three states (docs/map_elements/11_civilian_buildings/prompts.md, 3.4).

Footprint 3x3 cells, origin at the centre of the footprint on the ground. Five storeys: the walls 5.6 high, the penthouse to 6.2.
Nodes: "body" (walls, floors, the canopy, the rubble or rifles of a state), "roof" (the cornice, the penthouse, the air
conditioners), "team_mask" (the band under the cornice that the game paints in the owner's colour). The damaged one also has
the empty nodes fire_1..4, where the game stands the flames. One GLB per state:
    building_apartment_block            intact, empty
    building_apartment_block_damaged    almost destroyed: the back gone, the right side and the top storeys collapsed
    building_apartment_block_occupied   intact, with rifle barrels out of the windows: occupied by infantry
    building_apartment_block_ruins      destroyed: the stumps of the walls and the rubble on the footprint, with bars of steel
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

VARIANTS = {
    "building_apartment_block": "intact",
    "building_apartment_block_damaged": "damaged",
    "building_apartment_block_occupied": "occupied",
    "building_apartment_block_ruins": "ruins",
}

# The flames of the damaged block: the broken top of the front wall, the top of the collapsed right side, the cornice on the left.
FIRE_POINTS = ((0.5, -1.55, 3.7), (1.6, -0.4, 3.7), (-1.0, -1.6, 5.6), (-1.6, 0.4, 5.6))

# The rubble on the ground round the damaged block: x, y of its corner, its side, its height.
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


def _body(bm, variant):
    if variant == "damaged":
        rc.add_box(bm, (-1.5, -1.5, 0.0), (0.5, -1.5 + WALL_T, H_WALL))  # front wall, the left part
        rc.add_box(bm, (0.5, -1.5, 0.0), (1.5, -1.5 + WALL_T, 3.6))  # front wall, the right part: its top is gone
        rc.add_box(bm, (-1.5, -1.5, 0.0), (-1.5 + WALL_T, 1.5, H_WALL))  # left wall, the back is gone
        rc.add_box(bm, (1.5 - WALL_T, -1.5, 0.0), (1.5, 0.0, 3.6))  # right wall, the front half: its top is gone
        rc.add_box(bm, (1.5 - WALL_T, 0.0, 0.0), (1.5, 1.5, 1.2))  # right wall, the back half: a stub
        rc.add_box(bm, (1.2, -1.5, 2.2), (1.7, 0.0, 2.3))  # a floor slab, bare at the broken side
        for x, y, side, height in RUBBLE:
            rc.add_box(bm, (x, y, 0.0), (x + side, y + side, height))
    else:
        rc.add_box(bm, (-1.5, -1.5, 0.0), (1.5, 1.5, H_WALL))  # the block, solid: its windows are in the texture
        rc.add_box(bm, (-0.7, -1.85, 2.05), (0.7, -1.5, 2.15))  # the canopy over the entrance
    if variant == "occupied":
        for x in (-0.67, -0.05, 0.57, 1.19):
            rc.add_stick(bm, Vector((x, -1.45, 0.55)), Vector((x, -1.9, 0.62)), 0.03)  # rifle barrels out of the ground windows


def _roof(bm, variant):
    if variant == "damaged":
        rc.add_box(bm, (-1.58, -1.58, H_WALL), (0.3, 1.58, H_WALL + 0.1))  # the cornice over the part that is left
        return
    rc.add_box(bm, (-1.58, -1.58, H_WALL), (1.58, 1.58, H_WALL + 0.1))  # the cornice, which is the roof too
    rc.add_box(bm, (-0.9, -0.9, H_WALL + 0.1), (0.9, 0.9, H_TOP))  # the penthouse, set back from the walls
    rc.add_box(bm, (0.95, -0.85, H_WALL + 0.1), (1.35, -0.35, H_WALL + 0.4))  # an air conditioner
    rc.add_box(bm, (-1.2, 0.35, H_WALL + 0.1), (-0.8, 0.85, H_WALL + 0.35))  # another one


def _team(bm, variant):
    # The band under the cornice, on the front; the damaged block keeps it over the part that stands.
    right = 0.3 if variant == "damaged" else 1.2
    rc.add_box(bm, (-1.2, -1.53, 5.36), (right, -1.5, 5.52))


def _material(mat_name, variant):
    """Concrete walls with rows of windows, a door, a grey roof, the neutral band under the cornice and, per state, cracks or rifles."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(12.0, 4.0)
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
    if variant == "damaged":
        crack = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', grain, 0.5)), -100.0, 1.0))
        concrete = g.mix(crack, concrete, (0.12, 0.11, 0.10))
    base = g.mix(window, concrete, (0.07, 0.09, 0.11))
    base = g.mix(door, base, (0.30, 0.20, 0.12))
    roof_col = g.mix(grain, (0.24, 0.23, 0.22), (0.32, 0.30, 0.28))
    base = g.mix(roof, base, roof_col)
    base = g.mix(band, base, (0.86, 0.86, 0.86))
    metal = 0.0
    rough = g.math('MULTIPLY_ADD', window, -0.65, 0.85)  # glass 0.2, concrete 0.85
    if variant == "occupied":
        rifle = g.clamp01(g.math('MULTIPLY_ADD', y, -40.0, -62.0))  # beyond y -1.55: the barrels and the canopy
        base = g.mix(rifle, base, (0.25, 0.25, 0.27))
        metal = g.math('MULTIPLY', rifle, 0.8)
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
    objs = [body, roof, team]
    for obj, lo, hi in ((body, 0.0, 0.7), (roof, 0.7, 0.9), (team, 0.9, 1.0)):
        rc.uv_smart(obj)
        rc.place_uv(obj, lo, hi)
    mat, graph = _material(variant_id + "_mat", variant)
    for obj in objs:
        obj.data.materials.append(mat)
    return objs, mat, graph


def _ruins(bm):
    """The ruins of the block: the stumps of its walls, a heap of rubble, broken slabs, bars of steel out of the heap."""
    rc.add_box(bm, (-1.5, -1.5, 0.0), (-0.6, -1.2, 1.8))  # front wall, the left stump, the highest piece
    rc.add_box(bm, (0.2, -1.5, 0.0), (1.5, -1.2, 0.7))  # front wall, the right part, low
    rc.add_box(bm, (-1.5, -1.5, 0.0), (-1.2, -0.2, 1.2))  # left wall, a stub
    rc.add_box(bm, (1.2, 0.2, 0.0), (1.5, 1.5, 0.5))  # right wall, low
    rc.add_box(bm, (-0.4, 1.2, 0.0), (0.6, 1.5, 2.2))  # back wall, the tallest jag
    rc.add_box(bm, (-0.9, -0.7, 0.0), (0.4, 0.6, 0.15))  # a floor slab on the ground, broken
    rc.add_box(bm, (-1.5, -0.2, 0.8), (-0.3, 0.4, 0.9))  # a slab hanging off the left wall
    for x, y, side, height in RUBBLE_BLOCK:
        rc.add_box(bm, (x, y, 0.0), (x + side, y + side, height))
    for p1, p2 in BARS_BLOCK:
        rc.add_stick(bm, Vector(p1), Vector(p2), 0.035)  # steel out of the heap


def build_ruins(variant_id):
    """The ruins: the stumps and the rubble only, no roof and no band; the game draws them where the block fell."""
    bm = bmesh.new()
    _ruins(bm)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    body = rc.make_object("block_body", bm)
    rc.uv_smart(body)
    mat, graph = rc.ruins_material(variant_id + "_mat")
    body.data.materials.append(mat)
    return [body], mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof and team_mask (fire_1..4 when damaged)."""
    objs, mat, graph = build_ruins(variant_id) if VARIANTS[variant_id] == "ruins" else build(variant_id)
    nodes = rc.fire_nodes(FIRE_POINTS) if VARIANTS[variant_id] == "damaged" else []
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in objs)}, "
              f"size {tuple(round(v, 3) for v in objs[0].dimensions)}"]
    size = RUINS_SIZE if VARIANTS[variant_id] == "ruins" else SIZE
    imgs = rc.bake_material(objs, graph, size, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    names = ["body"] if VARIANTS[variant_id] == "ruins" else ["body", "roof", "team_mask"]
    with rc.node_names(objs, names):
        rc.save_blend(objs + nodes, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs + nodes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
