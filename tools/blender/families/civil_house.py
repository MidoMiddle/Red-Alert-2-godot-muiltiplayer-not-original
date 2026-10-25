"""Civilian house: its states (docs/map_elements/11_civilian_buildings/appearance.md, 5) and the soldiers inside.

Footprint 2x2 cells, origin at the centre of the footprint on the ground. Walls 1.7 high, roof ridge at 2.5. The house stands on a
concrete foundation, a low plinth a few centimetres wider than the walls (docs/art/model_standards.md, 3.1).
Nodes: "body" (walls, door, windows, and the rubble, rifles or sandbags of a state), "roof", "team_mask" (the band
above the door that the game paints in the owner's colour), "base" (the concrete foundation). The abandoned one also has the empty
nodes fire_1..3, where the game stands the flames. One GLB per state:
    building_house_small                    whole, empty
    building_house_small_occupied           whole, occupied by infantry: rifle barrels out of the windows, sandbags round the foot
    building_house_small_damaged            damaged, empty: cracks, broken windows, soot over the windows
    building_house_small_damaged_occupied   damaged, occupied by infantry: the same, with rifles and sandbags
    building_house_small_abandoned          critically damaged, its soldiers out: back wall gone, right side collapsed, half roof, rubble, the ceiling left inside
    building_house_ruins                    destroyed: the stumps of the walls and the rubble on the footprint, snow between the bricks
Texture 1024: a 2x2 building is not a small prop (generation_requirements.md, section 4).

In Blender, after rts_common is on sys.path:
    import families.civil_house as ch
    print(ch.run("building_house_small"))
"""

import os

import bmesh
from mathutils import Vector

import rts_common as rc

SIZE = 1024
WALL_T = 0.15
H_WALL = 1.7
H_RIDGE = 2.5
# The concrete foundation: its top is FOUNDATION_H high, its edge FOUNDATION_D from the centre (the walls stand at 1.0).
FOUNDATION_H = 0.14
FOUNDATION_D = 1.06
# The centre line of the sandbags round the house: their inner edge touches the walls, which stand at 1.0.
SANDBAG_D = 1.12
QUARTER_TURN = 1.5707963267948966

VARIANTS = {
    "building_house_small": "intact",
    "building_house_small_occupied": "occupied",
    "building_house_small_damaged": "damaged",
    "building_house_small_damaged_occupied": "damaged_occupied",
    "building_house_small_abandoned": "abandoned",
    "building_house_ruins": "ruins",
}

# The flames of the abandoned house: the top of the front windows, the top of the intact right wall, the ridge of the roof that is left.
FIRE_POINTS = ((-0.55, -1.02, 1.5), (0.9, -0.4, 1.72), (-0.5, 0.0, 2.5))

# The rubble of the ruins on the footprint: x, y of its corner, its side, its height.
RUBBLE_HOUSE = (
    (-0.7, -0.5, 0.25, 0.2),
    (-0.35, -0.2, 0.3, 0.25),
    (0.05, -0.55, 0.22, 0.14),
    (0.3, 0.05, 0.28, 0.22),
    (-0.1, 0.35, 0.2, 0.3),
    (0.55, -0.35, 0.18, 0.12),
    (-0.8, 0.45, 0.2, 0.16),
    (0.2, 0.6, 0.25, 0.18),
    (0.6, 0.7, 0.14, 0.1),
    (-0.45, 0.7, 0.16, 0.12),
)


def _walls(bm, variant):
    rc.add_box(bm, (-1.0, -1.0, 0.0), (1.0, -1.0 + WALL_T, H_WALL))  # front, y = -1
    if variant == "abandoned":
        rc.add_box(bm, (-1.0, 1.0 - WALL_T, 0.0), (0.2, 1.0, H_WALL))  # back wall, the right part is gone
        rc.add_box(bm, (1.0 - WALL_T, -1.0 + WALL_T, 0.0), (1.0, 0.0, H_WALL))  # right wall, front half
        rc.add_box(bm, (1.0 - WALL_T, 0.0, 0.0), (1.0, 1.0 - WALL_T, 0.9))      # right wall, collapsed half
    else:
        rc.add_box(bm, (-1.0, 1.0 - WALL_T, 0.0), (1.0, 1.0, H_WALL))  # back, y = 1
        rc.add_box(bm, (1.0 - WALL_T, -1.0 + WALL_T, 0.0), (1.0, 1.0 - WALL_T, H_WALL))  # right
    rc.add_box(bm, (-1.0, -1.0 + WALL_T, 0.0), (-1.0 + WALL_T, 1.0 - WALL_T, H_WALL))  # left, x = -1


def _foundation(bm):
    """The concrete foundation: a low plinth under the walls, a few centimetres wider than them, so the house stands on it."""
    rc.add_box(bm, (-FOUNDATION_D, -FOUNDATION_D, 0.0), (FOUNDATION_D, FOUNDATION_D, FOUNDATION_H))


def _sandbags(bm):
    """Sandbags round the foot of the house, two rows in running bond with gaps between the bags; the door stays open."""
    for z, offset in ((0.0, 0.0), (0.15, 0.18)):
        for t in (-1.08, -0.72, -0.36, 0.0, 0.36, 0.72, 1.08):
            along = t + offset
            if abs(along) > 1.15:
                continue
            if abs(along) >= 0.5:
                rc.add_sandbag(bm, along, -SANDBAG_D, z, 0.0, 0.3, 0.2, 0.17)  # the front, without the door
            rc.add_sandbag(bm, along, SANDBAG_D, z, 0.0, 0.3, 0.2, 0.17)  # the back
            rc.add_sandbag(bm, -SANDBAG_D, along, z, QUARTER_TURN, 0.3, 0.2, 0.17)  # the left side
            rc.add_sandbag(bm, SANDBAG_D, along, z, QUARTER_TURN, 0.3, 0.2, 0.17)  # the right side


def _extras(bm, variant):
    if variant in ("occupied", "damaged_occupied"):
        for x in (-0.75, -0.25, 0.25, 0.75):
            rc.add_stick(bm, Vector((x, -0.98, 1.2)), Vector((x, -1.38, 1.26)), 0.03)  # rifle barrels out of the windows
        _sandbags(bm)
    if variant == "abandoned":
        for x, y, s in ((0.6, 0.4, 0.2), (0.8, 0.7, 0.14), (0.45, 0.9, 0.12), (0.9, -0.2, 0.16), (0.2, 0.9, 0.1)):
            rc.add_box(bm, (x, y, 0.0), (x + s, y + s, s * 0.8))  # rubble on the ground
        rc.add_box(bm, (-0.85, -0.85, 1.62), (0.1, 0.85, H_WALL))  # the ceiling that is left, under the roof that stands


def _roof(bm, variant):
    x0, x1 = -1.05, 1.05
    if variant == "abandoned":
        x1 = 0.1  # half of the roof is gone
    a0 = bm.verts.new((x0, -1.05, H_WALL))
    b0 = bm.verts.new((x0, 1.05, H_WALL))
    c0 = bm.verts.new((x0, 0.0, H_RIDGE))
    a1 = bm.verts.new((x1, -1.05, H_WALL))
    b1 = bm.verts.new((x1, 1.05, H_WALL))
    c1 = bm.verts.new((x1, 0.0, H_RIDGE))
    for face in ((a0, c0, c1, a1), (c0, b0, b1, c1), (a0, a1, b1, b0), (a0, b0, c0), (a1, c1, b1)):
        bm.faces.new(face)


def _team(bm):
    rc.add_box(bm, (-0.35, -1.02, 1.0), (0.35, -0.98, 1.12))


def _material(mat_name, variant):
    """Plaster walls with windows and a door, a roof, a neutral team band and, per state, cracks, soot, rifles or sandbags."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(12.0, 4.0)
    outside = g.math('MAXIMUM', g.math('ABSOLUTE', x), g.math('ABSOLUTE', y))  # the distance from the centre, the square way
    # Windows: a grid on the walls, 1.0..1.5 high. The wall length runs along x + y, which works on all four walls.
    fu = g.math('FRACT', g.math('MULTIPLY', g.math('ADD', x, y), 2.0))
    win_u = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', fu, 20.0, -4.0)),
                   g.clamp01(g.math('MULTIPLY_ADD', fu, -20.0, 16.0)))
    win_z = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 20.0, -20.0)),
                   g.clamp01(g.math('MULTIPLY_ADD', z, -20.0, 30.0)))
    window = g.math('MULTIPLY', win_u, win_z)
    # Door: centred on the front wall, 0.9 high.
    door = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', x), -40.0, 10.0)),
                  g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 36.0)),
                         g.clamp01(g.math('MULTIPLY_ADD', y, -40.0, -32.0))))
    # Team band above the door: the game paints it in the owner's colour.
    band = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', x), -40.0, 14.0)),
                  g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 40.0, -39.2)),
                         g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 45.6)),
                                g.clamp01(g.math('MULTIPLY_ADD', y, -100.0, -97.0)))))
    roof = g.clamp01(g.math('MULTIPLY_ADD', z, 40.0, -68.8))  # above 1.72
    plaster = g.mix(grain, (0.60, 0.54, 0.44), (0.70, 0.64, 0.52))
    if variant in ("abandoned", "damaged", "damaged_occupied"):
        crack = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', grain, 0.5)), -100.0, 1.0))
        plaster = g.mix(crack, plaster, (0.12, 0.11, 0.10))
    base = g.mix(window, plaster, (0.07, 0.09, 0.11))
    if variant in ("damaged", "damaged_occupied"):
        # Windows knocked out in clusters, and soot over the openings: the band just above each window.
        broken = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 2.0), 10.0, -5.5))
        base = g.mix(g.math('MULTIPLY', window, broken), base, (0.03, 0.03, 0.03))
        soot = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, 40.0, -60.0)),
                      g.clamp01(g.math('MULTIPLY_ADD', z, -40.0, 68.0)))
        base = g.mix(soot, base, (0.14, 0.12, 0.11))
    # The foundation (the base node): the concrete, a little darker than the plaster, in the band it stands in.
    foundation = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 8.4)),
                        g.clamp01(g.math('MULTIPLY_ADD', outside, -60.0, 64.2)))
    base = g.mix(foundation, base, g.mix(grain, (0.42, 0.41, 0.39), (0.50, 0.49, 0.46)))
    base = g.mix(door, base, (0.30, 0.20, 0.12))
    roof_col = g.mix(grain, (0.22, 0.20, 0.19), (0.30, 0.27, 0.25))
    base = g.mix(roof, base, roof_col)
    base = g.mix(band, base, (0.86, 0.86, 0.86))
    metal = 0.0
    rough = g.math('MULTIPLY_ADD', window, -0.65, 0.85)  # glass 0.2, plaster 0.85
    if variant in ("occupied", "damaged_occupied"):
        # The barrels: beyond the wall and above the sandbags (z from 0.45 up).
        rifle = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', y, -100.0, -105.0)),
                       g.clamp01(g.math('MULTIPLY_ADD', z, 20.0, -9.0)))
        base = g.mix(rifle, base, (0.25, 0.25, 0.27))
        metal = g.math('MULTIPLY', rifle, 0.8)
        # Sandbags round the foot: the sand colour, outside the walls (at 1.0) and low (up to 0.34).
        bags = g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', outside, 60.0, -60.6)),
                      g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 20.4)))
        sand = g.mix(g.noise(25.0, 4.0), (0.42, 0.36, 0.25), (0.60, 0.52, 0.36))  # grains of the sand, darker than the plaster
        base = g.mix(bags, base, sand)
    normal = g.bump(g.noise(18.0, 3.0), 0.06, 0.01)
    orm = g.combine(ao, rough, metal)
    g.finish(base, normal, orm)
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    bm_body = bmesh.new()
    _walls(bm_body, variant)
    _extras(bm_body, variant)
    bmesh.ops.recalc_face_normals(bm_body, faces=list(bm_body.faces))
    body = rc.make_object("house_body", bm_body)
    bm_roof = bmesh.new()
    _roof(bm_roof, variant)
    bmesh.ops.recalc_face_normals(bm_roof, faces=list(bm_roof.faces))
    roof = rc.make_object("house_roof", bm_roof)
    bm_team = bmesh.new()
    _team(bm_team)
    bmesh.ops.recalc_face_normals(bm_team, faces=list(bm_team.faces))
    team = rc.make_object("house_team", bm_team)
    bm_base = bmesh.new()
    _foundation(bm_base)
    bmesh.ops.recalc_face_normals(bm_base, faces=list(bm_base.faces))
    base = rc.make_object("house_base", bm_base)
    objs = [body, roof, team, base]
    for obj, lo, hi in ((body, 0.0, 0.5), (roof, 0.5, 0.8), (team, 0.8, 0.9), (base, 0.9, 1.0)):
        rc.uv_smart(obj)
        rc.place_uv(obj, lo, hi)
    mat, graph = _material(variant_id + "_mat", variant)
    for obj in objs:
        obj.data.materials.append(mat)
    return objs, mat, graph


def _ruins(bm):
    """The ruins: low stumps of the walls, a heap of rubble, a broken slab, two charred beams, all on the footprint."""
    rc.add_box(bm, (-1.0, -1.0, 0.0), (-0.2, -0.85, 0.75))  # front wall, the left stump
    rc.add_box(bm, (0.35, -1.0, 0.0), (1.0, -0.85, 0.45))  # front wall, the right stump, lower
    rc.add_box(bm, (-1.0, -1.0, 0.0), (-0.85, 0.3, 0.6))  # left wall, a stub
    rc.add_box(bm, (0.85, 0.2, 0.0), (1.0, 1.0, 0.35))  # right wall, a low piece
    rc.add_box(bm, (-0.6, 0.85, 0.0), (0.1, 1.0, 0.9))  # back wall, the highest piece
    rc.add_box(bm, (-0.5, -0.4, 0.0), (0.4, 0.5, 0.12))  # a floor slab on the ground, broken
    for x, y, side, height in RUBBLE_HOUSE:
        rc.add_box(bm, (x, y, 0.0), (x + side, y + side, height))
    rc.add_stick(bm, Vector((-0.2, 0.1, 0.55)), Vector((0.3, -0.1, 0.05)), 0.04)  # a charred beam, leaning on the heap
    rc.add_stick(bm, Vector((0.6, 0.5, 0.4)), Vector((0.1, 0.5, 0.0)), 0.04)  # another one


def build_ruins(variant_id):
    """The ruins: the stumps and the rubble on the foundation, no roof and no band; the game draws them where the house fell."""
    bm = bmesh.new()
    _ruins(bm)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    body = rc.make_object("house_body", bm)
    rc.uv_smart(body)
    rc.place_uv(body, 0.0, 0.6)
    bm_base = bmesh.new()
    _foundation(bm_base)
    bmesh.ops.recalc_face_normals(bm_base, faces=list(bm_base.faces))
    base = rc.make_object("house_base", bm_base)
    rc.uv_smart(base)
    rc.place_uv(base, 0.6, 1.0)
    mat, graph = rc.ruins_material(variant_id + "_mat")
    body.data.materials.append(mat)
    base.data.materials.append(mat)
    return [body, base], mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, roof, team_mask and base (fire_1..3 when abandoned)."""
    objs, mat, graph = build_ruins(variant_id) if VARIANTS[variant_id] == "ruins" else build(variant_id)
    nodes = rc.fire_nodes(FIRE_POINTS) if VARIANTS[variant_id] == "abandoned" else []
    rc.arrange(objs + nodes, variant_id)
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in objs)}, "
              f"size {tuple(round(v, 3) for v in objs[0].dimensions)}"]
    imgs = rc.bake_material(objs, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    names = ["body", "base"] if VARIANTS[variant_id] == "ruins" else ["body", "roof", "team_mask", "base"]
    with rc.node_names(objs + nodes, names + rc.fire_names(nodes)):
        rc.save_blend(objs + nodes, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs + nodes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
