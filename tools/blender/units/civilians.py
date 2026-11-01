"""The civilians of the map with a skeleton (docs/map_elements/13_civilians): people rigged on the Mixamo skeleton (M1, 4.13).

Made at the size of the beachgoers (families/beach_people.py, 0.56 tall) and painted the same way, but standing in a T-pose: the arms
straight out to the sides, palms down, the feet a little apart, the pose of the Mixamo skeleton. Origin at the centre on the ground, the
front to +Y in Blender (-Z in Godot). One GLB per look:
    civ_man            a light casual jacket over a white shirt, jeans, white sneakers
    civ_woman          a knee-length coat over a dress, flat shoes, long hair
    civ_winter_man     a puffer jacket, a knitted hat and a scarf, jeans, winter boots
    civ_winter_woman   a long dark winter coat, a knitted hat and a scarf
    civ_farmer         denim overalls over a checked shirt, rubber boots, a cap
    civ_desert_man     a long light robe and a head scarf, sandals
    civ_desert_woman   a long loose dress in earth tones and a light head scarf
No text, no logos, no team colour (nobody's). Nodes: "rig" (the skeleton) and "body" under it. Budget 1500 triangles, texture 512.

The skeleton is the one Mixamo put into civ_man (docs/art/mixamo.md): its bones come from a clip downloaded on it, and every look is
skinned to the same bones by this script, so every clip fits every look. The clips are one GLB of their own, human_clips.glb, with no
mesh; the game puts them on each person (game/scripts/Units/HumanAnimator.cs). Their table, game/data/visuals/human_clips.yaml, is
written here too: which clips loop, how far a walk or a run carries in a second (the game matches the feet to the ground), and the
clips only some people take.

In Blender, after tools/blender is on sys.path:
    import units.civilians as civilians
    print(civilians.upload("civ_man"))   # the file for mixamo.com, once
    print(civilians.run_all())           # the clips and every look
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector
from mathutils.geometry import intersect_point_line

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin, save_blend

FOLDER = "unit"
SIZE = 512
HUMAN_HEIGHT = 1.75  # metres: the upload file is scaled to a man's height, the size the auto-rigger is made for
CLIPS_DIR = os.path.join(rc.SRC_ROOT, FOLDER, "mixamo_clips")  # the downloads, out of git (docs/art/mixamo.md, 4)
SKELETON_FROM = "idle_0"  # the clip whose skeleton, at rest, is the skeleton of every look
CLIPS_ID = "human_clips"
CLIPS_TABLE = os.path.join(rc.REPO, "game", "data", "visuals", "human_clips.yaml")
HIPS = 'pose.bones["mixamorig:Hips"].location'
FORWARD = 2  # the channel of the hips' location the walks carry forward along
FPS = 30

# The clips of docs/art/mixamo.md: name -> (loops, travels[, frames]). A clip that travels walks or runs on: its forward drift comes off
# (the game moves the unit) and is kept as its stride. frames: the part of the download the clip is, when it is more than one loop (the
# catwalk goes forward, turns and comes back: one step cycle of its straight stretch). The unused_* downloads are left out.
CLIPS = {
    "idle_0": (True, False), "idle_1": (True, False), "idle_2": (True, False), "idle_3": (True, False), "idle_4": (True, False),
    "walk_0": (True, True), "walk_1": (True, True), "walk_2": (True, True), "walk_3": (True, True), "walk_woman_0": (True, True, (64, 100)),
    "run_0": (True, True), "run_1": (True, True), "run_2": (True, True),
    "crouch_down": (False, False), "crouch": (True, False), "crouch_up": (False, False), "crouch_sprint": (False, True),
    "die_0": (False, False), "die_1": (False, False),
    "prone": (True, False),
}

# The clips only some people take: the catwalk is the women's.
ONLY = {
    "walk_woman_0": ["civ_woman", "civ_winter_woman", "civ_desert_woman", "civ_beach_woman"],
}

(SKIN, HAIR, TOP, BOTTOM, SHOE, ACCENT, DARK, WHITE) = (cc.part(i) for i in range(8))
PARTS = 8

# The look of each: skin, hair, top (the jacket, the coat, the robe), bottom (the trousers, the dress), shoe and accent (the shirt, the
# hat, the scarf) colours, and what is worn.
VARIANTS = {
    "civ_man": dict(skin=(0.82, 0.62, 0.48), hair=(0.25, 0.17, 0.11), top=(0.70, 0.62, 0.46), bottom=(0.22, 0.30, 0.48),
                    shoe=(0.86, 0.86, 0.84), accent=(0.93, 0.93, 0.91), wear=("shirt",)),
    "civ_woman": dict(skin=(0.86, 0.68, 0.56), hair=(0.42, 0.26, 0.14), top=(0.62, 0.36, 0.30), bottom=(0.30, 0.34, 0.52),
                      shoe=(0.20, 0.16, 0.14), accent=(0.86, 0.82, 0.70), wear=("coat", "long_hair")),
    "civ_winter_man": dict(skin=(0.84, 0.66, 0.54), hair=(0.22, 0.16, 0.10), top=(0.20, 0.36, 0.30), bottom=(0.20, 0.26, 0.40),
                           shoe=(0.24, 0.18, 0.12), accent=(0.72, 0.16, 0.14), wear=("puffer", "knit_hat", "scarf")),
    "civ_winter_woman": dict(skin=(0.86, 0.70, 0.60), hair=(0.30, 0.20, 0.12), top=(0.18, 0.18, 0.22), bottom=(0.18, 0.18, 0.22),
                             shoe=(0.14, 0.12, 0.11), accent=(0.84, 0.78, 0.62), wear=("long_coat", "knit_hat", "scarf", "long_hair")),
    "civ_farmer": dict(skin=(0.76, 0.56, 0.42), hair=(0.30, 0.22, 0.14), top=(0.62, 0.22, 0.18), bottom=(0.26, 0.36, 0.54),
                       shoe=(0.18, 0.26, 0.16), accent=(0.86, 0.80, 0.56), wear=("bib", "boots", "cap")),
    "civ_desert_man": dict(skin=(0.62, 0.44, 0.30), hair=(0.12, 0.10, 0.08), top=(0.90, 0.88, 0.80), bottom=(0.90, 0.88, 0.80),
                           shoe=(0.46, 0.32, 0.20), accent=(0.84, 0.82, 0.74), wear=("robe", "head_scarf")),
    "civ_desert_woman": dict(skin=(0.60, 0.42, 0.30), hair=(0.12, 0.10, 0.08), top=(0.62, 0.42, 0.28), bottom=(0.62, 0.42, 0.28),
                             shoe=(0.40, 0.28, 0.18), accent=(0.88, 0.80, 0.62), wear=("robe", "head_scarf")),
}

KNEE, HIP, WAIST, CHEST, SHOULDER, NECK = 0.15, 0.27, 0.315, 0.39, 0.435, 0.455
HEAD, HEAD_R = 0.5, 0.042
ARM = SHOULDER - 0.006  # the height of the arms held out


def ring(centre, rx, ry, sides=8):
    c = Vector(centre)
    return [c + Vector((math.cos(2 * math.pi * k / sides) * rx, math.sin(2 * math.pi * k / sides) * ry, 0.0)) for k in range(sides)]


def limb(p, points, radii, parts, sides=6):
    """A tapered limb through the points; each length painted with its part (parts has one fewer than points)."""
    for (a, b), (ra, rb), part in zip(zip(points, points[1:]), zip(radii, radii[1:]), parts):
        a, b = Vector(a), Vector(b)
        d = (b - a).normalized()
        u = d.cross(Vector((0.0, 1.0, 0.0)))
        if u.length < 1e-6:
            u = d.cross(Vector((1.0, 0.0, 0.0)))
        u = u.normalized()
        v = d.cross(u).normalized()
        p.loft([[c + (u * math.cos(2 * math.pi * k / sides) + v * math.sin(2 * math.pi * k / sides)) * r for k in range(sides)]
                for c, r in ((a, ra), (b, rb))], part)


def skirt(p, stations, part):
    """A skirt, a coat's tail or a robe: a loft of rings (height, half-width, half-depth) from the waist down."""
    for (z0, rx0, ry0), (z1, rx1, ry1) in zip(stations, stations[1:]):
        p.loft([ring((0.0, 0.0, z0), rx0, ry0, 10), ring((0.0, 0.0, z1), rx1, ry1, 10)], part)


def _figure(p, wear):
    puff = 1.15 if "puffer" in wear else 1.0
    # Legs a little apart, as the skeleton stands: thighs and shins in the trousers (the shins bare under a coat), the shoes on a dark
    # sole; boots come up the shin.
    shin = SKIN if "coat" in wear else BOTTOM
    for side in (-1, 1):
        limb(p, [(0.024 * side, 0.0, HIP), (0.027 * side, 0.004, (HIP + KNEE) / 2), (0.03 * side, 0.006, KNEE), (0.034 * side, 0.0, 0.025)],
             [0.024, 0.02, 0.016, 0.013], [BOTTOM, BOTTOM, shin])
        x = 0.034 * side
        p.box((x - 0.012, -0.01, 0.004), (x + 0.012, 0.036, 0.016), SHOE)
        p.box((x - 0.0125, -0.0105, 0.0), (x + 0.0125, 0.0365, 0.004), DARK)
        if "boots" in wear:  # rubber boots to the mid shin
            limb(p, [(x, 0.002, 0.012), (0.032 * side, 0.004, 0.09)], [0.016, 0.016], [SHOE])
    # The hips, the top from its hem up to the shoulders, the neck.
    p.loft([ring((0.0, 0.0, HIP - 0.01), 0.044, 0.028), ring((0.0, 0.0, HIP + 0.012), 0.042, 0.027)], BOTTOM)
    stations = [(HIP + 0.008, 0.047, 0.030), (WAIST, 0.042, 0.028), (CHEST, 0.048, 0.030), (SHOULDER, 0.055, 0.027)]
    for (z0, rx0, ry0), (z1, rx1, ry1) in zip(stations, stations[1:]):
        p.loft([ring((0.0, 0.0, z0), rx0 * puff, ry0 * puff), ring((0.0, 0.0, z1), rx1 * puff, ry1 * puff)], TOP)
    if "shirt" in wear:  # the shirt between the open lapels
        p.box((-0.012, 0.024, CHEST - 0.035), (0.012, 0.032, SHOULDER - 0.004), ACCENT)
    if "bib" in wear:  # the overalls' bib over the shirt, its straps over the shoulders
        p.box((-0.026, 0.024, WAIST - 0.01), (0.026, 0.033, CHEST + 0.012), BOTTOM)
        for side in (-1, 1):
            p.box((0.018 * side - 0.005, -0.03, CHEST + 0.01), (0.018 * side + 0.005, 0.032, SHOULDER + 0.002), BOTTOM)
    if "coat" in wear:  # the coat's tail to the knee, the dress showing under it
        skirt(p, [(WAIST, 0.045, 0.03), (HIP, 0.052, 0.036), (KNEE + 0.01, 0.06, 0.044)], TOP)
    if "long_coat" in wear:
        skirt(p, [(WAIST, 0.045, 0.03), (HIP, 0.052, 0.036), (KNEE - 0.03, 0.062, 0.046)], TOP)
    if "robe" in wear:  # a long loose robe down to the ankles
        skirt(p, [(WAIST, 0.046, 0.031), (HIP, 0.056, 0.038), (KNEE, 0.064, 0.046), (0.03, 0.07, 0.05)], TOP)
    p.loft([ring((0.0, 0.0, SHOULDER), 0.03, 0.022), ring((0.0, 0.0, NECK + 0.01), 0.014, 0.014)], SKIN)
    if "scarf" in wear:
        p.loft([ring((0.0, 0.0, SHOULDER - 0.004), 0.034, 0.027), ring((0.0, 0.0, NECK + 0.004), 0.024, 0.022)], ACCENT)
    # Arms straight out to the sides, palms down: the sleeves, the hands.
    for side in (-1, 1):
        limb(p, [(0.05 * side, 0.0, ARM), (0.128 * side, 0.0, ARM), (0.198 * side, 0.0, ARM), (0.232 * side, 0.0, ARM)],
             [0.016 * puff, 0.013 * puff, 0.011, 0.009], [TOP, TOP, SKIN])
    # The head, the hair or what covers it, the nose.
    p.sphere((0.0, 0.0, HEAD), 'Z', HEAD_R, SKIN, sides=10, rings=6)
    if "head_scarf" in wear:  # a scarf over the head and down the back of the neck
        p.sphere((0.0, -0.008, HEAD + 0.004), 'Z', HEAD_R * 1.12, ACCENT, sides=10, rings=5)
        p.box((-0.03, -0.05, NECK - 0.012), (0.03, -0.022, HEAD - 0.01), ACCENT)
    else:
        p.sphere((0.0, -0.01, HEAD + 0.006), 'Z', HEAD_R * 1.06, HAIR, sides=10, rings=4)
    if "long_hair" in wear:
        p.box((-0.034, -0.042, HEAD - 0.06), (0.034, -0.014, HEAD + 0.02), HAIR)
    if "knit_hat" in wear:
        p.sphere((0.0, -0.006, HEAD + 0.018), 'Z', HEAD_R * 1.08, ACCENT, sides=10, rings=4)
    if "cap" in wear:
        p.cylinder((0.0, -0.004, HEAD + 0.03), 'Z', 0.044, 0.016, ACCENT, sides=10)
        p.box((-0.03, 0.03, HEAD + 0.018), (0.03, 0.075, HEAD + 0.024), ACCENT)  # its peak, to the front
    p.box((-0.004, HEAD_R - 0.002, HEAD - 0.004), (0.004, HEAD_R + 0.006, HEAD + 0.008), SKIN)  # the nose


def _material(name, look):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.03)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(60.0, 2.0)
    shade = lambda c, k=0.12: g.mix(g.math('MULTIPLY', grain, k), lin(c), lin(tuple(v * 0.8 for v in c)))
    albedo = cc.paint(g, mask, (shade(look["skin"], 0.08), shade(look["hair"]), shade(look["top"]), shade(look["bottom"]),
                               lin(look["shoe"]), shade(look["accent"]), lin((0.04, 0.04, 0.05)), lin((0.94, 0.94, 0.92))))
    rough = cc.weighted(g, mask, (0.6, 0.7, 0.85, 0.85, 0.7, 0.75, 0.6, 0.8))
    g.finish(albedo, g.bump(grain, 0.03, 0.002), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    look = VARIANTS[variant_id]
    cc.clear(variant_id, ("body",))
    body = Parts()
    _figure(body, look["wear"])
    return cc.finish(variant_id, [(body, "body")], 0.0, _material(variant_id + "_mat", look))


# --- the file for mixamo.com --------------------------------------------------------

def export_upload(variant_id, meshes, path):
    """The file for mixamo.com: a copy of the model joined into one mesh, scaled to a man's height and turned to face -Y (Mixamo
    reads the front of a Blender model there), its texture embedded. The copy is removed afterwards."""
    copies = []
    for obj in meshes:
        copy = obj.copy()
        copy.data = obj.data.copy()
        bpy.context.scene.collection.objects.link(copy)
        copies.append(copy)
    rc.select_only(copies)
    if len(copies) > 1:
        bpy.ops.object.join()
    upload_obj = bpy.context.view_layer.objects.active
    upload_obj.name = variant_id + "_upload"
    height = max(v.co.z for v in upload_obj.data.vertices)
    upload_obj.data.transform(Matrix.Rotation(math.pi, 4, 'Z') @ Matrix.Scale(HUMAN_HEIGHT / height, 4))
    upload_obj.matrix_world = Matrix.Identity(4)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    rc.select_only(upload_obj)
    bpy.ops.export_scene.fbx(filepath=path, use_selection=True, object_types={'MESH'}, mesh_smooth_type='FACE',
                             add_leaf_bones=False, path_mode='COPY', embed_textures=True)
    data = upload_obj.data
    bpy.data.objects.remove(upload_obj)
    bpy.data.meshes.remove(data)
    return f"  upload {path}: {os.path.getsize(path)} bytes, {round(HUMAN_HEIGHT, 2)} m tall"


def upload(variant_id):
    """Builds and bakes the T-pose model and writes the upload file for Mixamo; returns a report."""
    meshes, mat, graph = build(variant_id)
    base = os.path.join(rc.SRC_ROOT, FOLDER, variant_id)
    cc.repack(meshes, cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material(meshes, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    return export_upload(variant_id, meshes, os.path.join(base, "mixamo", variant_id + "_upload.fbx"))


# --- the skeleton -------------------------------------------------------------------

def _import_clip(name):
    """The armature and the action of a downloaded clip; the armature brought from a man's height facing -Y to the size of the model
    facing +Y (the upload's turn and scale undone), so its bones stand in the figure."""
    before, actions = set(bpy.data.objects), set(bpy.data.actions)
    bpy.ops.import_scene.fbx(filepath=os.path.join(CLIPS_DIR, name + ".fbx"))
    new = [o for o in bpy.data.objects if o not in before]
    arm = next(o for o in new if o.type == 'ARMATURE')
    for obj in new:
        if obj is not arm:
            bpy.data.objects.remove(obj, do_unlink=True)
    action = next(a for a in bpy.data.actions if a not in actions)
    return arm, action


def skeleton(variant_id, height):
    """The rig of a look: the armature of SKELETON_FROM at rest, at the model's height, named rig."""
    old = bpy.data.objects.get(variant_id + "_rig")
    if old is not None:
        data = old.data
        bpy.data.objects.remove(old, do_unlink=True)
        bpy.data.armatures.remove(data)
    arm, action = _import_clip(SKELETON_FROM)
    arm.animation_data_clear()
    bpy.data.actions.remove(action)
    arm.matrix_world = Matrix.Scale(height / HUMAN_HEIGHT, 4) @ Matrix.Rotation(math.pi, 4, 'Z') @ arm.matrix_world
    arm.name = variant_id + "_rig"
    arm.data.name = variant_id + "_rig"
    for bone in arm.data.bones:  # the leaves Mixamo adds (HeadTop_End, the toes' ends) move nothing
        bone.use_deform = not bone.name.endswith("_End")
    return arm


def _segments(arm):
    """The deforming bones as world segments: name -> (head, tail, parent name, child names)."""
    segs = {}
    for bone in arm.data.bones:
        if bone.use_deform:
            segs[bone.name] = (arm.matrix_world @ bone.head_local, arm.matrix_world @ bone.tail_local,
                               bone.parent.name if bone.parent else None, [c.name for c in bone.children if c.use_deform])
    return segs


def _distance(point, head, tail):
    closest, t = intersect_point_line(point, head, tail)
    if t < 0.0:
        closest = head
    elif t > 1.0:
        closest = tail
    return (point - closest).length


def skin(mesh, arm):
    """Weights by the parts the figure is made of: each separate piece (a length of a limb, a ring of the torso, the head, a skirt)
    goes with the bone nearest its middle and that bone's neighbours (parent, children, siblings); each vertex takes the two of them
    nearest it, the nearer one most. So a sleeve bends with its arm and never with the chest, and a skirt follows both legs."""
    segs = _segments(arm)
    bm = bmesh.new()
    bm.from_mesh(mesh.data)
    bm.verts.ensure_lookup_table()
    world = mesh.matrix_world
    seen, pieces = set(), []
    for start in bm.verts:
        if start.index in seen:
            continue
        piece, stack = [], [start]
        seen.add(start.index)
        while stack:
            v = stack.pop()
            piece.append(v.index)
            for e in v.link_edges:
                o = e.other_vert(v)
                if o.index not in seen:
                    seen.add(o.index)
                    stack.append(o)
        pieces.append(piece)
    points = [world @ v.co for v in bm.verts]
    bm.free()
    mesh.vertex_groups.clear()
    groups = {name: mesh.vertex_groups.new(name=name) for name in segs}
    for piece in pieces:
        middle = sum((points[i] for i in piece), Vector()) / len(piece)
        near = min(segs, key=lambda n: _distance(middle, segs[n][0], segs[n][1]))
        parent = segs[near][2]
        kin = {near, *segs[near][3]}
        if parent is not None:
            kin |= {parent, *segs[parent][3]}
        for i in piece:
            ranked = sorted((_distance(points[i], segs[n][0], segs[n][1]), n) for n in kin)[:2]
            weights = [(1.0 / max(d, 1e-4) ** 4, n) for d, n in ranked]
            total = sum(w for w, _ in weights)
            for w, n in weights:
                if w / total > 0.02:
                    groups[n].add([i], w / total, 'REPLACE')
    mesh.parent = arm
    mesh.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = mesh.modifiers.new("rig", 'ARMATURE')
    mod.object = arm
    unweighted = sum(1 for v in mesh.data.vertices if not v.groups)
    return f"  skinned to {len(segs)} bones, {len(pieces)} pieces, unweighted vertices {unweighted}"


def export_rigged(objs, path, animations=False, root=None):
    """The GLB of a skeleton: the rig and its mesh, or the rig alone with its NLA tracks as the clips."""
    os.makedirs(os.path.dirname(path), exist_ok=True)
    bpy.ops.object.select_all(action='DESELECT')
    for obj in objs:
        obj.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_yup=True, export_apply=False,
                              export_vertex_color='NONE', export_image_format='AUTO', export_skins=True,
                              export_animations=animations, export_animation_mode='NLA_TRACKS', export_force_sampling=True,
                              export_frame_step=1, export_anim_single_armature=True)


def run(variant_id):
    """Builds and bakes a look, skins it to the Mixamo skeleton, saves its .blend and exports its GLB; returns a report."""
    meshes, mat, graph = build(variant_id)
    body = meshes[0]
    height = max(v.co.z for v in body.data.vertices)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes)}, size {tuple(round(v, 3) for v in body.dimensions)}"]
    base = os.path.join(rc.SRC_ROOT, FOLDER, variant_id)
    cc.repack(meshes, cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material(meshes, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    arm = skeleton(variant_id, height)
    report.append(skin(body, arm))
    objs = [arm, body]
    glb = os.path.join(rc.GLB_ROOT, FOLDER, variant_id, variant_id + ".glb")
    with rc.node_names(objs, ["rig", "body"]):
        save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        export_rigged(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


# --- the clips ----------------------------------------------------------------------

def _fcurves(action):
    return [fc for layer in action.layers for strip in layer.strips for bag in strip.channelbags for fc in bag.fcurves]


def _in_place(action, f0, f1):
    """Takes the forward drift between the frames off the hips, so the clip walks on the spot; returns the drift in the armature's
    units."""
    fc = next(f for f in _fcurves(action) if f.data_path == HIPS and f.array_index == FORWARD)
    keys = fc.keyframe_points
    v0, v1 = fc.evaluate(f0), fc.evaluate(f1)
    for k in keys:
        shift = (k.co[0] - f0) / (f1 - f0) * (v1 - v0)
        k.co[1] -= shift
        k.handle_left[1] -= shift
        k.handle_right[1] -= shift
    fc.update()
    return v1 - v0


def run_clips(height=None):
    """The clips GLB (the rig of civ_man with one NLA track per clip, no mesh) and the table of the clips; returns a report."""
    if height is None:
        meshes, _, _ = build("civ_man")
        height = max(v.co.z for v in meshes[0].data.vertices)
    for old in [a for a in bpy.data.actions if a.name.startswith(CLIPS_ID + "_")]:  # the clips of an earlier run
        bpy.data.actions.remove(old)
    arm = skeleton(CLIPS_ID, height)
    scale = arm.matrix_world.to_scale().x  # armature units to the model's: the hips' drift is in these
    arm.animation_data_create()
    rows, report = [], []
    for name, (loops, travels, *cut) in CLIPS.items():
        clip_arm, action = _import_clip(name)
        bpy.data.objects.remove(clip_arm, do_unlink=True)
        action.name = CLIPS_ID + "_" + name
        f0, f1 = cut[0] if cut else (action.frame_range[0], action.frame_range[1])
        length = (f1 - f0) / FPS
        row = f"  {name}: {{ loop: {'true' if loops else 'false'}, length: {length:.3f}"
        if travels:
            stride = abs(_in_place(action, f0, f1)) * scale / length
            row += f", stride: {stride:.3f}"
        rows.append(row + " }")
        track = arm.animation_data.nla_tracks.new()
        track.name = name
        strip = track.strips.new(name, int(f0), action)
        if action.slots:
            strip.action_slot = action.slots[0]
        strip.action_frame_start, strip.action_frame_end = f0, f1
        report.append(rows[-1].strip())
    glb = os.path.join(rc.GLB_ROOT, FOLDER, CLIPS_ID, CLIPS_ID + ".glb")
    with rc.node_names([arm], ["rig"]):
        export_rigged([arm], glb, animations=True)
    only = [f"  {name}: [{', '.join(ids)}]" for name, ids in ONLY.items()]
    with open(CLIPS_TABLE, "w", encoding="utf-8") as f:
        f.write("# The clips of the people (docs/art/mixamo.md), written by tools/blender/units/civilians.py (run_clips): whether a clip loops,\n"
                "# its length in seconds, and for a walk or a run its stride, how far it carries in a second at its own speed (game units; the\n"
                "# game plays it faster or slower to match the feet to the ground). The clips themselves: assets/models/unit/human_clips.\n"
                "clips:\n" + "\n".join(rows) + "\n"
                "# The clips only these people take (unit ids); the others are everyone's.\n"
                "only:\n" + "\n".join(only) + "\n")
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    reports = [run(variant_id) for variant_id in VARIANTS]
    height = max(v.co.z for v in bpy.data.objects["civ_man_body"].data.vertices)
    return "\n".join([run_clips(height)] + reports)
