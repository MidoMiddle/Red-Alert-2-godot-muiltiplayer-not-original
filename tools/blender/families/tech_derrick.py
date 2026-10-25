"""Oil pumpjack, tech_oil_derrick (docs/map_elements/12_tech_buildings): the silo tank, the engine, the beam on its stand.

Footprint 2x2 cells, origin at the centre of the footprint on the ground. A concrete slab; a silo tank with a cone top, a ladder and
a ring of the owner's colour; the engine with a pipe to the tank; a control shed; the A-frame stand and the beam. The beam is its own
node "beam" with its pivot at its axis of rotation, so the game can nod it. "team_mask" is the ring on the tank.
One GLB per state:
    tech_oil_derrick            intact, working: the beam nods
    tech_oil_derrick_damaged    almost destroyed: the tank tilted, the ladder torn off, the beam dropped, fire on the tank, the
                                engine and the head of the beam (nodes fire_1..3)
    tech_oil_derrick_ruins      destroyed: the tank fallen on the slab, the beam on the ground, the rubble
Texture 1024: a 2x2 structure (docs/map_elements/12_tech_buildings/prompts.md).

In Blender, after rts_common is on sys.path:
    import families.tech_derrick as td
    print(td.run("tech_oil_derrick"))
"""

import math
import os

import bmesh
from mathutils import Matrix, Vector

import rts_common as rc

SIZE = 1024
PIVOT = (0.2, 0.1, 1.35)  # the axis of the beam in the world: the origin of its node
PIVOT_STAND = (0.2, 0.1, 1.3)  # the top of the A-frame, just under the axis of the beam
TANK = (-0.55, 0.25)  # the axis of the silo
TANK_R = 0.36
TANK_TOP = 1.7  # the cylinder of the silo; its cone goes on to 2.05

VARIANTS = {
    "tech_oil_derrick": "intact",
    "tech_oil_derrick_damaged": "damaged",
    "tech_oil_derrick_ruins": "ruins",
}

# The flames of the damaged derrick: the top of the tank, the engine, the head of the beam.
FIRE_POINTS = ((-0.55, 0.25, 2.0), (0.6, -0.4, 0.65), (0.95, 0.1, 1.42))

# The rubble on the slab, round the ruins and the damaged one: x, y of its corner, its side, its height.
RUBBLE = (
    (-0.2, -0.8, 0.3, 0.2),
    (0.3, -0.7, 0.25, 0.18),
    (0.6, 0.5, 0.3, 0.16),
    (-0.5, 0.7, 0.28, 0.22),
    (0.1, 0.2, 0.2, 0.25),
    (-0.85, -0.2, 0.25, 0.15),
)


def _tank(bm, variant):
    cx, cy = TANK
    if variant == "ruins":
        # The silo fallen on the slab, its axis along x.
        matrix = Matrix.Translation((cx, cy, TANK_R)) @ Matrix.Rotation(math.pi / 2, 4, 'Y')
        bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=14, radius1=TANK_R, radius2=TANK_R,
                              depth=1.2, matrix=matrix)
        return
    tilt = Matrix.Rotation(0.09, 4, 'X') if variant == "damaged" else Matrix.Identity(4)
    height = TANK_TOP - 0.12
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=14, radius1=TANK_R, radius2=TANK_R,
                          depth=height, matrix=Matrix.Translation((cx, cy, 0.12 + height / 2)) @ tilt)
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=14, radius1=TANK_R, radius2=0.1,
                          depth=0.35, matrix=Matrix.Translation((cx, cy, TANK_TOP + 0.175)) @ tilt)


def _ladder(bm):
    cx, cy = TANK
    front = cy - TANK_R - 0.03  # just in front of the silo
    for x in (cx - 0.22, cx - 0.1):
        rc.add_stick(bm, Vector((x, front, 0.2)), Vector((x, front, TANK_TOP)), 0.022)
    for i in range(8):
        z = 0.2 + 0.2 * i
        rc.add_box(bm, (cx - 0.22, front - 0.02, z), (cx - 0.1, front + 0.02, z + 0.03))


def _body(bm, variant):
    cx, cy = TANK
    rc.add_box(bm, (-0.95, -0.95, 0.0), (0.95, 0.95, 0.12))  # the concrete slab
    _tank(bm, variant)
    if variant == "ruins":
        rc.add_box(bm, (0.35, -0.65, 0.12), (0.85, -0.15, 0.32))  # the engine block, its lower part
        rc.add_box(bm, (0.1, -0.5, 0.12), (0.9, -0.3, 0.2))  # a piece of the beam, fallen on the slab
        for x, y, side, height in RUBBLE:
            rc.add_box(bm, (x, y, 0.12), (x + side, y + side, 0.12 + height))
        return
    rc.add_box(bm, (0.35, -0.65, 0.12), (0.85, -0.15, 0.6))  # the engine block
    rc.add_box(bm, (0.85, -0.5, 0.2), (0.92, -0.3, 0.5))  # its flywheel
    rc.add_box(bm, (-1.0, -0.72, 0.12), (-0.62, -0.25, 0.62))  # the control shed
    rc.add_box(bm, (-1.05, -0.77, 0.62), (-0.57, -0.2, 0.7))  # its roof
    rc.add_stick(bm, Vector((0.05, -0.22, 0.12)), Vector(PIVOT_STAND), 0.035)  # the A-frame stand, one leg
    rc.add_stick(bm, Vector((0.05, 0.42, 0.12)), Vector(PIVOT_STAND), 0.035)  # and the other
    rc.add_stick(bm, Vector((0.9, 0.1, 0.12)), Vector((0.9, 0.1, 1.28)), 0.03)  # the pump rod down to the engine
    rc.add_stick(bm, Vector((0.35, -0.3, 0.3)), Vector((cx + TANK_R, cy - 0.1, 0.3)), 0.04)  # the pipe to the tank
    if variant == "intact":
        _ladder(bm)
    else:
        rc.add_box(bm, (-0.3, -0.7, 0.12), (-0.1, -0.5, 0.25))  # the rubble of the ladder and the slab
        rc.add_box(bm, (0.5, 0.45, 0.12), (0.7, 0.62, 0.22))


def _beam(bm, variant):
    """The beam in its own space: the pivot at the origin, the beam along x from its counterweight to its head."""
    rc.add_box(bm, (-0.25, -0.06, -0.06), (0.75, 0.06, 0.06))  # the beam
    rc.add_box(bm, (-0.42, -0.1, -0.22), (-0.22, 0.1, 0.1))  # the counterweight
    rc.add_box(bm, (0.7, -0.09, -0.12), (0.82, 0.09, 0.12))  # the head, on the pump rod
    if variant == "damaged":
        # The head drops: the beam is broken and hangs down.
        bmesh.ops.transform(bm, matrix=Matrix.Rotation(0.5, 4, 'Y'), verts=list(bm.verts))


def _team(bm, variant):
    cx, cy = TANK
    tilt = Matrix.Rotation(0.09, 4, 'X') if variant == "damaged" else Matrix.Identity(4)
    # A ring round the silo, a little wider than it: the game paints it in the owner's colour.
    bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=14, radius1=TANK_R + 0.012,
                          radius2=TANK_R + 0.012, depth=0.16, matrix=Matrix.Translation((cx, cy, 1.5)) @ tilt)


def _finish(bm):
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))


def _material(mat_name, variant):
    """Painted steel with rust blotches; the damaged one has cracks, the ruins are sooty at the foot."""
    mat = rc.new_material(mat_name)
    g = rc.Graph(mat)
    ao = g.ao(0.25)
    z = g.axis(g.object_coord(), 'Z')
    grain = g.noise(14.0, 4.0)
    steel = g.mix(grain, (0.40, 0.43, 0.46), (0.52, 0.55, 0.58))
    rust = g.clamp01(g.math('MULTIPLY_ADD', g.noise(7.0, 2.0), 5.0, -2.6))
    base = g.mix(rust, steel, (0.45, 0.25, 0.13))
    if variant == "damaged":
        crack = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', grain, 0.5)), -100.0, 1.0))
        base = g.mix(crack, base, (0.10, 0.09, 0.08))
    if variant == "ruins":
        soot = g.clamp01(g.math('MULTIPLY_ADD', z, -2.5, 1.0))
        base = g.mix(soot, base, (0.08, 0.07, 0.07))
    rough = g.math('MULTIPLY_ADD', grain, -0.3, 0.55)
    metal = g.math('MULTIPLY_ADD', rust, -0.8, 0.8)  # rust is not metal
    normal = g.bump(g.noise(20.0, 3.0), 0.06, 0.01)
    g.finish(base, normal, g.combine(ao, rough, metal))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    bm_body = bmesh.new()
    _body(bm_body, variant)
    _finish(bm_body)
    body = rc.make_object("derrick_body", bm_body)
    objs = [body]
    if variant != "ruins":
        bm_beam = bmesh.new()
        _beam(bm_beam, variant)
        _finish(bm_beam)
        beam = rc.make_object("derrick_beam", bm_beam)
        beam.location = Vector(PIVOT)  # its mesh is in its own space, so the node turns about the axis
        bm_team = bmesh.new()
        _team(bm_team, variant)
        _finish(bm_team)
        team = rc.make_object("derrick_team", bm_team)
        objs += [beam, team]
    ranges = [(body, 0.0, 0.85)]
    if variant != "ruins":
        ranges += [(objs[1], 0.85, 0.95), (objs[2], 0.95, 1.0)]
    for obj, lo, hi in ranges:
        rc.uv_smart(obj)
        rc.place_uv(obj, lo, hi)
    mat, graph = _material(variant_id + "_mat", variant)
    for obj in objs:
        obj.data.materials.append(mat)
    return objs, mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, beam and team_mask (fire_1..3 when damaged)."""
    objs, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    nodes = rc.fire_nodes(FIRE_POINTS) if variant == "damaged" else []
    names = ["body"] if variant == "ruins" else ["body", "beam", "team_mask"]
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in objs)}, "
              f"size {tuple(round(v, 3) for v in objs[0].dimensions)}"]
    imgs = rc.bake_material(objs, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    with rc.node_names(objs, names):
        rc.save_blend(objs + nodes, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs + nodes, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
