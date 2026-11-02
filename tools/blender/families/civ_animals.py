"""The civilians' animals (docs/map_elements/13_civilians, 4; docs/temperate_elements/13_livestock; docs/desert_elements/11_camels_vultures):
the cow and the sheep of the farms, the camel of the desert, the dog of the towns and villages.

Units of game/data/neutral/civilians.yaml (animal: true): civ_cow, civ_sheep, civ_camel, civ_dog. They graze by the barns and wells, walk
about and bolt from a fight. Made at the size the game draws its people (a civilian is 0.54 tall): the cow 0.4 tall, the sheep 0.26, the
camel 0.62 to its head, the dog 0.2. Origin on the ground under the middle of the body, front to +Y in Blender (-Z in Godot after the Y-up
export).

A four-legged skeleton of its own, not Mixamo's: root (on the ground, along +Y), spine (the body), neck, head, tail, and for each leg
leg_* (the upper leg) and shin_* (the lower), fl, fr, bl, br. Each part of the body follows one bone whole (the low-poly animal is made
of parts that meet at the joints). The clips are NLA tracks of the rig, exported as the GLB's animations (the client's AnimalAnimator
plays them by what the animal does):
    idle   standing, the tail swishing, the head turning a little, 2 s
    graze  the head down to the grass, chewing, 2 s
    walk   a walk, one stride a second (the camel paces: the legs of one side together)
    run    a gallop, a stride in half a second
    die    falling on its side (once)
Nodes: "rig" (the skeleton) and "body" (the skinned mesh). No team colour. Budget 1500 triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.civ_animals as animals
    print(animals.run_all())
"""

import math
import os

import bpy
from mathutils import Euler, Vector

from families import city_common as cc
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "unit"
SIZE = 512
FPS = 24

COAT, COAT2, SKIN, HOOF, HORN, EYE = (cc.part(i) for i in range(6))
PARTS = 6

# torso: half length, radius, width (of the radius), centre height; legs: x, y of the front and the back pair, top, knee, radii;
# neck: base and tip; head: its points from the back of the skull to the nose and their radii; tail: base and tip.
VARIANTS = {
    "civ_cow": dict(
        ry=0.23, r=0.105, wide=0.85, hb=0.27, legx=0.065, legy=(0.15, -0.16), top=0.25, knee=0.13, lr=(0.028, 0.02),
        neck=((0.0, 0.18, 0.3), (0.0, 0.28, 0.33)), head=((0.0, 0.27, 0.34), (0.0, 0.34, 0.31), (0.0, 0.4, 0.25)), hr=(0.04, 0.045, 0.034),
        tail=((0.0, -0.23, 0.33), (0.0, -0.27, 0.15)), horns=True, ears=0.035, coats=((0.92, 0.90, 0.86), (0.10, 0.09, 0.09)),
        patches=True, hump=None, wool=False),
    "civ_sheep": dict(
        ry=0.15, r=0.085, wide=0.95, hb=0.17, legx=0.045, legy=(0.095, -0.1), top=0.15, knee=0.08, lr=(0.016, 0.013),
        neck=((0.0, 0.12, 0.19), (0.0, 0.17, 0.22)), head=((0.0, 0.16, 0.23), (0.0, 0.21, 0.21), (0.0, 0.25, 0.18)), hr=(0.026, 0.028, 0.02),
        tail=((0.0, -0.15, 0.2), (0.0, -0.17, 0.14)), horns=False, ears=0.025, coats=((0.86, 0.82, 0.72), (0.18, 0.16, 0.15)),
        patches=False, hump=None, wool=True),
    "civ_camel": dict(
        ry=0.22, r=0.095, wide=0.8, hb=0.4, legx=0.06, legy=(0.15, -0.16), top=0.38, knee=0.2, lr=(0.024, 0.017),
        neck=((0.0, 0.18, 0.42), (0.0, 0.32, 0.54)), head=((0.0, 0.31, 0.56), (0.0, 0.38, 0.56), (0.0, 0.44, 0.52)), hr=(0.032, 0.034, 0.024),
        tail=((0.0, -0.22, 0.44), (0.0, -0.25, 0.3)), horns=False, ears=0.02, coats=((0.74, 0.58, 0.38), (0.60, 0.45, 0.28)),
        patches=False, hump=((0.0, -0.02, 0.48), 0.085), wool=False, pace=True),
    "civ_dog": dict(
        ry=0.1, r=0.045, wide=0.85, hb=0.12, legx=0.03, legy=(0.07, -0.07), top=0.11, knee=0.055, lr=(0.012, 0.009),
        neck=((0.0, 0.08, 0.14), (0.0, 0.11, 0.18)), head=((0.0, 0.1, 0.19), (0.0, 0.14, 0.19), (0.0, 0.18, 0.17)), hr=(0.024, 0.024, 0.014),
        tail=((0.0, -0.1, 0.15), (0.0, -0.15, 0.2)), horns=False, ears=0.022, coats=((0.62, 0.42, 0.24), (0.86, 0.74, 0.56)),
        patches=False, hump=None, wool=False),
}

LEGS = ("fl", "fr", "bl", "br")


def _tube(p, points, radii, part, sides=8, scale_x=1.0):
    """A body along the points, its rings of the radii, both ends capped."""
    pts = [Vector(q) for q in points]
    rings = []
    for k, c in enumerate(pts):
        d = (pts[min(k + 1, len(pts) - 1)] - pts[max(k - 1, 0)]).normalized()
        u = d.cross(Vector((0.0, 0.0, 1.0)))
        if u.length < 1e-6:
            u = d.cross(Vector((0.0, 1.0, 0.0)))
        u = u.normalized()
        v = d.cross(u).normalized()
        rings.append([c + (u * math.cos(2 * math.pi * i / sides) * scale_x + v * math.sin(2 * math.pi * i / sides)) * radii[k]
                      for i in range(sides)])
    p.loft(rings, part)


def _legs(v):
    """Each leg: its name, its top, its knee, its foot."""
    out = {}
    for name in LEGS:
        x = v["legx"] * (-1 if name[1] == "l" else 1)
        y = v["legy"][0 if name[0] == "f" else 1]
        out[name] = (Vector((x, y, v["top"])), Vector((x, y, v["knee"])), Vector((x, y, 0.0)))
    return out


def _shape(v):
    """The parts of the body, one Parts per bone."""
    parts = {name: Parts() for name in ("spine", "neck", "head", "tail") + tuple("leg_" + n for n in LEGS) + tuple("shin_" + n for n in LEGS)}
    ry, r, hb = v["ry"], v["r"], v["hb"]
    body = [(0.0, -ry, hb + r * 0.15), (0.0, -ry * 0.75, hb + r * 0.05), (0.0, -ry * 0.2, hb), (0.0, ry * 0.4, hb), (0.0, ry * 0.85, hb + r * 0.1),
            (0.0, ry, hb + r * 0.2)]
    radii = [r * 0.35, r * 0.9, r * 1.0, r * 1.0, r * 0.85, r * 0.35]
    _tube(parts["spine"], body, radii, COAT2 if v["wool"] else COAT, sides=10, scale_x=v["wide"])
    if v["hump"]:
        c, hr = v["hump"]
        parts["spine"].turned(c, 'Z', [(-hr * 0.9, hr * 0.9), (0.0, hr), (hr * 0.5, hr * 0.7), (hr * 0.85, hr * 0.25)], COAT, sides=8)
    if v["horns"] or v["patches"]:  # the cow's udder
        parts["spine"].turned((0.0, -ry * 0.55, hb - r * 0.85), 'Z', [(-0.03, 0.012), (0.0, 0.035), (0.02, 0.03)], SKIN, sides=6)
    (n0, n1) = v["neck"]
    _tube(parts["neck"], [n0, n1], [r * 0.55, v["hr"][0] * 1.1], COAT, sides=7)
    _tube(parts["head"], v["head"], list(v["hr"]), COAT, sides=8)
    nose = Vector(v["head"][-1])
    parts["head"].turned(tuple(nose), 'Y', [(-0.01, v["hr"][2] * 0.95), (0.006, v["hr"][2] * 0.8)], SKIN if v["horns"] else HOOF, sides=6)
    back = Vector(v["head"][0])
    for side in (-1, 1):
        ear = back + Vector((side * v["hr"][1] * 0.9, 0.0, v["hr"][0] * 0.6))
        parts["head"].bar(ear, ear + Vector((side * v["ears"], -0.005, v["ears"] * 0.3)), v["ears"] * 0.22, COAT)
        eye = Vector(v["head"][1]) + Vector((side * v["hr"][1] * 0.85, -0.005, v["hr"][1] * 0.35))
        parts["head"].box(tuple(eye - Vector((0.004, 0.006, 0.005))), tuple(eye + Vector((0.004, 0.006, 0.005))), EYE)
        if v["horns"]:
            parts["head"].bar(back + Vector((side * 0.025, 0.0, 0.035)), back + Vector((side * 0.06, 0.01, 0.055)), 0.007, HORN)
    t0, t1 = Vector(v["tail"][0]), Vector(v["tail"][1])
    _tube(parts["tail"], [t0, (t0 + t1) / 2, t1], [r * 0.12, r * 0.09, r * 0.06], COAT, sides=5)
    if v["horns"] or v["wool"] is False and v["hump"]:  # a tuft at the tip
        parts["tail"].turned(tuple(t1), 'Z', [(-0.03, 0.006), (0.0, 0.013), (0.01, 0.004)], HOOF if v["horns"] else COAT2, sides=5)
    for name, (top, knee, foot) in _legs(v).items():
        r0, r1 = v["lr"]
        parts["leg_" + name].turned(tuple(knee), 'Z', [(-r1, r1 * 0.9), (0.0, r1), ((top - knee).z * 0.6, r0), ((top - knee).z + r0, r0 * 0.8)],
                                    COAT, sides=6)
        parts["shin_" + name].turned(tuple(foot), 'Z', [(0.0, r1 * 1.05), (r1 * 1.2, r1 * 1.0), (r1 * 1.3, r1 * 0.9), ((knee - foot).z, r1 * 0.9)],
                                     COAT, sides=6)
        parts["shin_" + name].turned(tuple(foot), 'Z', [(0.0, r1 * 1.12), (r1 * 1.2, r1 * 1.05)], HOOF, sides=6)
    return parts


def _rig(variant_id, v):
    """The skeleton, in the rest pose of the model."""
    data = bpy.data.armatures.new(variant_id + "_rig")
    arm = bpy.data.objects.new(variant_id + "_rig", data)
    bpy.context.scene.collection.objects.link(arm)
    rc.select_only(arm)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.mode_set(mode='EDIT')
    bones = data.edit_bones

    def bone(name, head, tail, parent=None):
        b = bones.new(name)
        b.head, b.tail = Vector(head), Vector(tail)
        b.roll = 0.0
        if parent:
            b.parent = bones[parent]
        return b

    ry, hb = v["ry"], v["hb"]
    bone("root", (0, 0, 0), (0, 0.1, 0))
    bone("spine", (0, -ry * 0.6, hb), (0, ry * 0.6, hb), "root")
    bone("neck", v["neck"][0], v["neck"][1], "spine")
    bone("head", v["head"][0], v["head"][-1], "neck")
    bone("tail", v["tail"][0], v["tail"][1], "spine")
    for name, (top, knee, foot) in _legs(v).items():
        bone("leg_" + name, top, knee, "root")
        bone("shin_" + name, knee, foot, "leg_" + name)
    bpy.ops.object.mode_set(mode='OBJECT')
    for pb in arm.pose.bones:
        pb.rotation_mode = 'XYZ'
    return arm


def _key(arm, frame, poses):
    """poses: {bone: (rx, ry, rz) in degrees, or (rx, ry, rz, (x, y, z)) with a move}; every bone not named is keyed at rest."""
    for pb in arm.pose.bones:
        pose = poses.get(pb.name, (0.0, 0.0, 0.0))
        pb.rotation_euler = Euler(tuple(math.radians(a) for a in pose[:3]), 'XYZ')
        pb.location = Vector(pose[3]) if len(pose) > 3 else Vector()
        pb.keyframe_insert("rotation_euler", frame=frame)
        pb.keyframe_insert("location", frame=frame)


def _clips(arm, v):
    """The clips as actions, each pushed as an NLA track named after it."""
    arm.animation_data_create()
    sign = {"fl": 1, "fr": -1, "bl": -1, "br": 1}  # the diagonal pairs swing together

    def stride(frames, swing, bend, bob, gallop):
        keys = []
        steps = 8
        for k in range(steps + 1):
            t = k / steps
            poses = {}
            for name in LEGS:
                if gallop:
                    phase = t + (0.0 if name[0] == "f" else 0.5) + (0.08 if name[1] == "r" else 0.0)
                elif v.get("pace"):  # the camel paces: the legs of one side together
                    phase = t + (0.0 if name[1] == "l" else 0.5)
                else:
                    phase = t + (0.0 if sign[name] > 0 else 0.5)
                s = math.sin(2 * math.pi * phase)
                lift = max(0.0, math.cos(2 * math.pi * phase))  # the leg bends as it swings forward
                poses["leg_" + name] = (swing * s, 0.0, 0.0)
                poses["shin_" + name] = ((-bend if name[0] == "f" else bend) * lift, 0.0, 0.0)
            up = bob * abs(math.sin(2 * math.pi * t * (1 if gallop else 2)))
            pitch = (6.0 if gallop else 0.0) * math.sin(2 * math.pi * t)
            poses["spine"] = (pitch, 0.0, 0.0, (0.0, 0.0, up))
            poses["neck"] = (-pitch * 0.8 + 3.0 * math.sin(2 * math.pi * t * 2), 0.0, 0.0)
            poses["tail"] = (0.0, 0.0, 8.0 * math.sin(2 * math.pi * t))
            keys.append((1 + round(frames * t), poses))
        return keys

    def idle(frames):
        keys = []
        for k in range(9):
            t = k / 8
            keys.append((1 + round(frames * t), {
                "tail": (0.0, 0.0, 22.0 * math.sin(2 * math.pi * t * 2)),
                "neck": (2.0 * math.sin(2 * math.pi * t), 0.0, 8.0 * math.sin(2 * math.pi * t)),
                "head": (0.0, 0.0, 6.0 * math.sin(2 * math.pi * t + 1.0)),
                "spine": (0.0, 0.0, 0.0, (0.0, 0.0, v["r"] * 0.03 * math.sin(2 * math.pi * t * 2))),
            }))
        return keys

    def graze(frames):
        keys = []
        for k in range(9):
            t = k / 8
            chew = 5.0 * math.sin(2 * math.pi * t * 4)
            keys.append((1 + round(frames * t), {
                "neck": (-48.0, 0.0, 4.0 * math.sin(2 * math.pi * t)),
                "head": (-25.0 + chew, 0.0, 0.0),
                "tail": (0.0, 0.0, 15.0 * math.sin(2 * math.pi * t)),
                "spine": (-3.0, 0.0, 0.0),
                "leg_fl": (-6.0, 0.0, 0.0), "leg_fr": (-6.0, 0.0, 0.0),
            }))
        return keys

    def die(frames):
        lie = v["r"] * v["wide"]
        keys = [(1, {}),
                (round(frames * 0.5), {"root": (0.0, 50.0, 0.0, (0.0, 0.0, lie * 0.5)), "neck": (-10.0, 0.0, 0.0)}),
                (frames, {"root": (0.0, 90.0, 0.0, (0.0, 0.0, lie)), "neck": (-15.0, 0.0, 20.0), "head": (0.0, 0.0, 10.0),
                          "leg_fl": (25.0, 0.0, 0.0), "leg_bl": (-20.0, 0.0, 0.0), "leg_fr": (10.0, 0.0, 0.0), "leg_br": (-10.0, 0.0, 0.0),
                          "tail": (0.0, 0.0, 10.0)})]
        return keys

    clips = {"idle": idle(2 * FPS), "graze": graze(2 * FPS), "walk": stride(FPS, 22.0, 35.0, v["r"] * 0.06, False),
             "run": stride(FPS // 2, 30.0, 42.0, v["r"] * 0.15, True), "die": die(round(FPS * 0.7))}
    for name, keys in clips.items():
        action = bpy.data.actions.new(f"{arm.name}_{name}")
        arm.animation_data.action = action
        for frame, poses in keys:
            _key(arm, frame, poses)
        arm.animation_data.action = None
        track = arm.animation_data.nla_tracks.new()
        track.name = name
        track.strips.new(name, int(action.frame_range[0]), action)
    for pb in arm.pose.bones:  # the rest pose in the file
        pb.rotation_euler = Euler()
        pb.location = Vector()


def _material(name, coats, patches, wool):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.04)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    up = g.axis(g.normal_world(), 'Z')
    grain = g.noise(60.0, 3.0)
    coat = g.mix(g.noise(8.0, 2.0), lin(coats[0]), lin(tuple(c * 0.88 for c in coats[0])))
    if patches:  # the cow's black and white
        spots = g.clamp01(g.math('MULTIPLY_ADD', g.noise(4.0, 2.0), 14.0, -7.0))
        coat = g.mix(spots, coat, lin(coats[1]))
        coat2 = coat
    else:
        coat2 = g.mix(g.noise(10.0, 2.0), lin(coats[1]), lin(tuple(c * 0.85 for c in coats[1])))
        if not wool:  # darker on the back, lighter under the belly
            coat = g.mix(g.clamp01(g.math('MULTIPLY_ADD', up, -0.6, 0.2)), coat, lin(coats[1]))
    if wool:  # the fleece is the coat of the body and the legs' tops; the face and legs dark
        coat, coat2 = coat2, coat
    skin = lin((0.80, 0.56, 0.52))
    hoof = lin((0.14, 0.12, 0.11))
    horn = lin((0.86, 0.80, 0.66))
    eye = lin((0.04, 0.04, 0.04))
    albedo = cc.paint(g, mask, (coat, coat2, skin, hoof, horn, eye))
    albedo = g.mix(g.math('MULTIPLY', g.clamp01(g.math('MULTIPLY_ADD', z, -14.0, 0.5)), 0.4), albedo, lin((0.30, 0.26, 0.20)))  # dust
    rough = cc.weighted(g, mask, (0.9, 0.95, 0.6, 0.5, 0.5, 0.2))
    bump = g.math('ADD', grain, g.math('MULTIPLY', g.noise(25.0, 3.0), 2.0 if wool else 0.3))
    g.finish(albedo, g.bump(bump, 0.3 if wool else 0.1, 0.01), g.combine(ao, rough, 0.0))
    return mat, g


def build(variant_id):
    """The skinned mesh and the rig with its clips; returns (mesh, rig, material, graph)."""
    v = VARIANTS[variant_id]
    for name in (variant_id + "_body", variant_id + "_rig"):
        if (old := bpy.data.objects.get(name)) is not None:
            bpy.data.objects.remove(old, do_unlink=True)
    for action in [a for a in bpy.data.actions if a.name.startswith(variant_id + "_rig_")]:
        bpy.data.actions.remove(action)
    cc.clear(variant_id, ("body",))
    parts = _shape(v)
    order = list(parts)
    meshes, mat, graph = cc.finish(variant_id, [(parts[n], n) for n in order], -1.0, _material(variant_id + "_mat", v["coats"], v["patches"], v["wool"]))
    for obj, bone in zip(meshes, order):  # each part follows its bone whole
        group = obj.vertex_groups.new(name=bone)
        group.add(list(range(len(obj.data.vertices))), 1.0, 'REPLACE')
    rc.select_only(meshes)
    bpy.context.view_layer.objects.active = meshes[0]
    bpy.ops.object.join()
    mesh = meshes[0]
    mesh.name = variant_id + "_body"
    mesh.data.name = variant_id + "_body"
    arm = _rig(variant_id, v)
    mesh.parent = arm
    mod = mesh.modifiers.new("rig", 'ARMATURE')
    mod.object = arm
    _clips(arm, v)
    return mesh, arm, mat, graph


def _export(objs, path):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with rc.at_origin(objs):
        rc.select_only(objs)
        bpy.context.view_layer.objects.active = objs[0]
        bpy.ops.export_scene.gltf(filepath=path, export_format='GLB', use_selection=True, export_yup=True, export_apply=False,
                                  export_vertex_color='NONE', export_image_format='AUTO', export_skins=True, export_animations=True,
                                  export_animation_mode='NLA_TRACKS', export_force_sampling=True, export_frame_step=1,
                                  export_anim_single_armature=True)


def run(variant_id):
    mesh, arm, mat, graph = build(variant_id)
    report = [f"{variant_id}: triangles {rc.tri_count(mesh)}, size {tuple(round(d, 3) for d in mesh.dimensions)}"]
    base = os.path.join(rc.SRC_ROOT, FOLDER, variant_id)
    cc.repack([mesh], cc.ISLAND_GAP / SIZE)
    arm.data.pose_position = 'REST'
    imgs = rc.bake_material([mesh], graph, SIZE, variant_id, os.path.join(base, "bake"))
    arm.data.pose_position = 'POSE'
    rc.wire_final(mat, imgs)
    objs = [arm, mesh]
    rc.arrange(objs, variant_id)
    mesh.parent = arm
    mesh.matrix_parent_inverse.identity()
    glb = os.path.join(rc.GLB_ROOT, FOLDER, variant_id, variant_id + ".glb")
    with rc.node_names(objs, ["rig", "body"]):
        rc.save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        _export(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
