"""Battle bunker: the Union's garrisoned bunker, empty and with infantry inside (docs/ra2_buildings/soviets/battle_bunker), battle_bunker
in the data drafts (docs/ra2_buildings/yaml_drafts/union/buildings.yaml).

Footprint 2x2 cells, origin at the centre of the footprint on the ground; the front looks along -Y. The composition follows the owner's
references; the parts are our own:
    pad       a low six-sided concrete pad (the base node)
    lower     a wide six-sided tier of brick on a dark steel skirt, dark steel posts at its corners, a steel hatch in front, a dark
              ledge round its top
    collar    a sloped band between the tiers and six blocks on the skirt, in the owner's colour
    upper     a narrower six-sided tier of brick on a dark ring, a firing slit under a hood in each face, a dark rim round an open top
              with a floor sunk inside
No emblems, no text. Height 0.86 to the rim. Budget 20000 triangles, texture 1024 (four cells).
The occupied one adds five rifle barrels out of the slits, one for each soldier inside (the back face has none), and coils of barbed
wire on posts round the pad.

Nodes (docs/art/model_standards.md, section 4):
    body            the tiers, the posts, the slits, the hatch (and the barrels and the wire when occupied)
    team_mask       the collar and the blocks: the game paints them in the owner's colour
    muzzle_0..4     occupied only: empties at the barrels' muzzles, where the soldiers' shots come from
    base            the pad (docs/art/model_standards.md, "Фундамент")
One GLB per state:
    battle_bunker               empty
    battle_bunker_occupied      with infantry inside

In Blender, after tools/blender is on sys.path:
    import buildings.battle_bunker as bb
    print(bb.run("battle_bunker"))   # or bb.run_all()
"""

import math
import os

import bmesh
import bpy
from mathutils import Euler, Vector

import rts_common as rc
from units.parts import PART, Parts, lin, uv_together

SIZE = 1024
VARIANTS = ("battle_bunker", "battle_bunker_occupied")


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


CONCRETE, BRICK, DARK, STEEL, FLOOR, SLIT, TEAM = (_part(i) for i in range(7))
PARTS = 7

COS30 = math.cos(math.pi / 6)
PAD_H = 0.04
LOWER = (0.14, 0.38, 0.84)  # the lower tier: foot, top, the radius of its corners
UPPER = (0.54, 0.80, 0.60)  # the upper tier
SLIT_Z = (0.64, 0.71)
RIM_TOP = 0.86
# The faces of a tier, by the direction they look: face k between corners k and k + 1 looks at 30 + 60k degrees; 4 is the front.
FACES = range(6)
FIRING = (4, 3, 5, 2, 0)  # the faces with a barrel when occupied: all but the back one
WIRE_R = 1.06  # the corners of the barbed wire round the pad


def _face_dir(k):
    a = math.radians(30 + 60 * k)
    return Vector((math.cos(a), math.sin(a), 0.0))


def _corner(k, r, z):
    a = math.radians(60 * k)
    return Vector((r * math.cos(a), r * math.sin(a), z))


def _block(p, centre, size, turn, part):
    """A box of the given size turned about the vertical through its middle."""
    sx, sy, sz = (v / 2 for v in size)
    c = Vector(centre)
    m = Euler((0.0, 0.0, turn)).to_matrix()
    p.loft([[c + m @ Vector((u * sx, v * sy, w * sz)) for u, v in ((1, 1), (-1, 1), (-1, -1), (1, -1))] for w in (-1, 1)], part)


def _on_face(p, k, apothem, z, size, part):
    """A box flat on face k of a tier: its middle at the apothem and the height z, its width along the face."""
    d = _face_dir(k)
    _block(p, (d.x * apothem, d.y * apothem, z), size, math.atan2(d.y, d.x) + math.pi / 2, part)


def _pipe(p, points, radius, part, sides=4):
    pts = [Vector(q) for q in points]
    rings = []
    for i, q in enumerate(pts):
        d = (pts[min(i + 1, len(pts) - 1)] - pts[max(i - 1, 0)]).normalized()
        u = d.cross(Vector((0.0, 0.0, 1.0)) if abs(d.z) < 0.9 else Vector((1.0, 0.0, 0.0))).normalized()
        v = d.cross(u)
        rings.append([q + (u * math.cos(2 * math.pi * s / sides) + v * math.sin(2 * math.pi * s / sides)) * radius for s in range(sides)])
    p.loft(rings, part)


# --- body -------------------------------------------------------------------------

def _made(p, build):
    """The faces that build adds to p, their normals worked out (a new face has none yet)."""
    before = set(p.bm.faces)
    build()
    made = [f for f in p.bm.faces if f not in before]
    for f in made:
        f.normal_update()
    return made


def _paint(face, layer, part):
    for loop in face.loops:
        loop[layer] = part


def _tiers(p):
    """Both tiers as one closed body of revolution, so no cap of one tier hides inside the next (hidden faces bake black): the dark
    skirt, the brick of the lower tier, the dark ledge, the dark ring under the upper tier, its brick, the dark rim round the open
    top and the floor sunk inside. Painted afterwards by where each face is."""
    (l_foot, l_top, l_r), (u_foot, u_top, u_r) = LOWER, UPPER
    floor = u_top - 0.02
    profile = [(PAD_H, 0.92), (l_foot, 0.86), (l_foot, l_r), (l_top, l_r), (l_top, 0.88), (l_top + 0.05, 0.88), (l_top + 0.05, 0.64),
               (u_foot, 0.64), (u_foot, u_r), (u_top, u_r), (u_top, 0.65), (RIM_TOP, 0.65), (RIM_TOP, 0.52), (floor, 0.5),
               (floor, 0.01)]
    for f in _made(p, lambda: p.turned((0.0, 0.0, 0.0), 'Z', profile, DARK, sides=6)):
        c = f.calc_center_median()
        upright = abs(f.normal.z) < 0.3
        if upright and (l_foot < c.z < l_top or (u_foot < c.z < u_top and c.xy.length > u_r * COS30 - 0.01)):
            _paint(f, p.layer, BRICK)
        elif abs(f.normal.z) > 0.9 and abs(c.z - floor) < 1e-3:  # the cap: its normal is set the right way by finish
            _paint(f, p.layer, FLOOR)
    for k in range(6):  # dark posts at the corners of both tiers
        p.bar(_corner(k, LOWER[2] + 0.005, LOWER[0]), _corner(k, LOWER[2] + 0.005, LOWER[1]), 0.03, DARK)
        p.bar(_corner(k, UPPER[2] + 0.005, UPPER[0]), _corner(k, UPPER[2] + 0.005, UPPER[1]), 0.025, DARK)
    for k in FACES:  # a slit under a hood in each face of the upper tier
        a = UPPER[2] * COS30
        _on_face(p, k, a, sum(SLIT_Z) / 2, (0.26, 0.03, SLIT_Z[1] - SLIT_Z[0]), SLIT)
        _on_face(p, k, a + 0.02, SLIT_Z[1] + 0.015, (0.32, 0.05, 0.025), DARK)
    _on_face(p, 4, LOWER[2] * COS30, LOWER[0] + 0.1, (0.26, 0.03, 0.2), STEEL)  # the hatch in front


def _team(p):
    """The sloped collar on the ledge, open at both ends (its caps would hide inside the body), and the blocks on the skirt."""
    collar = _made(p, lambda: p.turned((0.0, 0.0, 0.0), 'Z', [(LOWER[1] + 0.05, 0.86), (0.49, 0.68)], TEAM, sides=6))
    bmesh.ops.delete(p.bm, geom=[f for f in collar if abs(f.normal.z) > 0.99], context='FACES_ONLY')
    for k in FACES:
        _on_face(p, k, 0.8, PAD_H + 0.07, (0.2, 0.14, 0.14), TEAM)


def _barrels(p):
    """Five rifle barrels out of the slits, and the muzzles at their ends."""
    muzzles = []
    for k in FIRING:
        d = _face_dir(k)
        z = sum(SLIT_Z) / 2
        start = d * (UPPER[2] * COS30 - 0.04) + Vector((0.0, 0.0, z))
        end = d * (UPPER[2] * COS30 + 0.42) + Vector((0.0, 0.0, z - 0.03))
        p.bar(start, end, 0.018, DARK)
        muzzles.append(end + d * 0.02)
    return muzzles


def _wire(p):
    """Coils of barbed wire round the pad, from post to post at the corners of a hexagon a little wider than the pad."""
    for k in range(6):
        a, b = _corner(k, WIRE_R, 0.0), _corner(k + 1, WIRE_R, 0.0)
        p.bar(a + Vector((0.0, 0.0, PAD_H)), a + Vector((0.0, 0.0, 0.2)), 0.012, STEEL)  # the post
        along = (b - a).normalized()
        side = along.cross(Vector((0.0, 0.0, 1.0)))
        turns, n = 8, 96
        pts = []
        for s in range(n + 1):
            t = s / n
            ang = 2 * math.pi * turns * t
            pts.append(a.lerp(b, t) + Vector((0.0, 0.0, 0.11)) + (Vector((0.0, 0.0, 1.0)) * math.cos(ang) + side * math.sin(ang)) * 0.065)
        _pipe(p, pts, 0.005, STEEL)


# --- material -----------------------------------------------------------------------

def _material(name):
    """Grey concrete, red-brown brick with mortar, dark steel, grey steel, a sandy floor, black slits, a light team zone."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.2)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(PARTS)]
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)

    concrete = g.mix(grain, lin((0.44, 0.43, 0.41)), lin((0.52, 0.51, 0.48)))
    # Bricks: courses up the wall, every other course shifted by half a brick; x + y runs along every face.
    course = g.math('MULTIPLY', z, 30.0)
    row = g.math('FRACT', course)
    col = g.math('FRACT', g.math('ADD', g.math('MULTIPLY', g.math('ADD', x, y), 8.0),
                                 g.math('MULTIPLY', g.math('FLOOR', course), 0.5)))
    mortar = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', row, -12.0, 2.0)), g.clamp01(g.math('MULTIPLY_ADD', col, -25.0, 2.0)))
    brick_col = g.mix(g.noise(6.0, 2.0), lin((0.40, 0.25, 0.20)), lin((0.52, 0.34, 0.26)))
    brick = g.mix(mortar, brick_col, lin((0.50, 0.47, 0.43)))
    dark = g.mix(grain, lin((0.25, 0.25, 0.27)), lin((0.32, 0.32, 0.34)))
    steel = g.mix(grain, lin((0.36, 0.37, 0.39)), lin((0.46, 0.47, 0.49)))
    floor = g.mix(grain, lin((0.50, 0.42, 0.34)), lin((0.58, 0.49, 0.40)))
    slit = lin((0.03, 0.03, 0.03))
    team = g.mix(grain, lin((0.80, 0.80, 0.80)), lin((0.88, 0.88, 0.88)))

    albedo = concrete
    for m, col_ in zip(mask[1:], (brick, dark, steel, floor, slit, team)):
        albedo = g.mix(m, albedo, col_)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.9, 0.85, 0.5, 0.45, 0.95, 0.9, 0.45))
    metallic = weighted((0.0, 0.0, 0.7, 0.7, 0.0, 0.0, 0.3))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[1], g.math('MULTIPLY', mortar, -1.0), g.noise(60.0, 3.0)), 0.05, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def _drop_bottoms(objs):
    """Deletes the faces that look down onto the pad or the ground: never seen, and black in the bake."""
    for obj in objs:
        bm = bmesh.new()
        bm.from_mesh(obj.data)
        low = [f for f in bm.faces if f.normal.z < -0.99 and f.calc_center_median().z < PAD_H + 1e-3]
        bmesh.ops.delete(bm, geom=low, context='FACES')
        bm.to_mesh(obj.data)
        bm.free()


def _repack(meshes):
    """Packs the islands again by their real shape: the six-sided rings of the tiers leave holes that the smart projection keeps
    empty (half the texture, too dark for the bake check), and the small islands fit into them."""
    rc.select_only(meshes)
    bpy.ops.object.mode_set(mode='EDIT')
    bpy.ops.mesh.select_all(action='SELECT')
    bpy.ops.uv.select_all(action='SELECT')
    bpy.ops.uv.pack_islands(shape_method='CONCAVE', rotate=True, margin=0.005)
    bpy.ops.object.mode_set(mode='OBJECT')


def _empty(name, point):
    node = bpy.data.objects.new(name, None)
    node.empty_display_type = 'PLAIN_AXES'
    node.empty_display_size = 0.05
    node.location = Vector(point)
    bpy.context.scene.collection.objects.link(node)
    return node


def _clear(variant_id):
    """Removes the parts of an earlier build of this variant (and their copies Blender named .001…), so the new parts get their
    names and the GLB its node names."""
    names = {f"{variant_id}_{n}" for n in ("body", "team_mask", "base", *(f"muzzle_{i}" for i in range(len(FIRING))))}
    for obj in [o for o in bpy.data.objects if o.name.split(".")[0] in names]:
        data = obj.data
        bpy.data.objects.remove(obj)
        if data is not None and data.users == 0:
            bpy.data.meshes.remove(data)


def build(variant_id):
    _clear(variant_id)
    occupied = variant_id.endswith("_occupied")
    body = Parts()
    _tiers(body)
    muzzles = []
    if occupied:
        muzzles = _barrels(body)
        _wire(body)
    team = Parts()
    _team(team)
    base = Parts()
    base.turned((0.0, 0.0, 0.0), 'Z', [(0.0, 1.0), (PAD_H, 0.98), (PAD_H, 0.01)], CONCRETE, sides=6)
    meshes = [body.finish(variant_id + "_body"), team.finish(variant_id + "_team_mask"), base.finish(variant_id + "_base")]
    _drop_bottoms(meshes)
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    _repack(meshes)
    mat, graph = _material(variant_id + "_mat")
    for obj in meshes:
        obj.data.materials.append(mat)
    empties = [_empty(f"{variant_id}_muzzle_{i}", point) for i, point in enumerate(muzzles)]
    return meshes, empties, mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, empties, mat, graph = build(variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes)}, "
              f"size {tuple(round(v, 3) for v in meshes[0].dimensions)}"]
    base = os.path.join(rc.SRC_ROOT, "building", variant_id)
    imgs = rc.bake_material(meshes, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + empties
    rc.arrange(objs, variant_id)  # its place in the file: the row of the players' buildings
    glb = os.path.join(rc.GLB_ROOT, "building", variant_id, variant_id + ".glb")
    with rc.node_names(objs, [o.name[len(variant_id) + 1:] for o in objs]):
        rc.save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
