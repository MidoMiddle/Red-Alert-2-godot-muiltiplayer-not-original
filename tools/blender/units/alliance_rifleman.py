"""Rifleman alliance_rifleman: the Alliance's base soldier (docs/ra2_units/allies/gi), on the Mixamo skeleton of the civilians.

The soldier is skinned to the skeleton of tools/blender/units/civilians.py (docs/art/mixamo.md), at the height of civ_man, so the
clips of that skeleton play on him: the soldiers' (rifle_*), with the gun in both hands; he stands, walks, runs, shoots, kneels behind
his sandbags dug in and shoots from there, and falls. Made standing in the
T-pose of that skeleton, the front to +Y in Blender (-Z in Godot), the origin at his feet. Budget 1500 triangles, texture 512.

Nodes (docs/art/model_standards.md, section 4):
    rig          the skeleton
      body       the soldier: camouflage uniform, webbing and belt, boots, gloves, the helmet with its goggles, two ammunition belts
                 crossed over the chest
      team_mask  the vest and the shoulder pads: the owner's colour (the blue of the reference)
      weapon     the machine gun, all on the right hand, so it goes with the hand as one piece
    sandbags     the ring of sandbags of a soldier dug in, four courses high, not on the skeleton: the game shows it only
                 then

The look follows the owner's references (an RA2 screenshot of the soldiers dug in and on the move, two fan renders of the soldier):
a helmet with goggles, a vest over a camouflage uniform, belts of ammunition across the chest, a machine gun with a long jacketed
barrel. Proportions and details are our own, no emblems, no text (docs/15_legal.md, section 3).

In Blender, after tools/blender is on sys.path:
    import units.alliance_rifleman as rifleman
    print(rifleman.run())
"""

import math
import os

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units import civilians as civ
from units.civilians import ARM, CHEST, HEAD, HEAD_R, HIP, KNEE, NECK, SHOULDER, WAIST, limb, ring
from units.parts import Parts, lin, save_blend

ID = "alliance_rifleman"
SIZE = 512
HAND = "mixamorig:RightHand"
OTHER_HAND = "mixamorig:LeftHand"

(SKIN, UNIFORM, GEAR, BOOT, METAL, BRASS, GLASS, SAND, HELMET, TEAM, DARK) = (cc.part(i) for i in range(11))
PARTS = 11

RING_R = 0.19  # the sandbags' ring: the middle of the bags from the soldier's feet
HOLD_CLIP, HOLD_FRAME = "rifle_idle_1", 1  # the gun is put in the hands as they aim in this clip: from the right palm through the left


# --- geometry -------------------------------------------------------------------

def _body(p):
    # Legs a little apart, as the skeleton stands: loose trousers, boots to the mid shin on a dark sole.
    for side in (-1, 1):
        limb(p, [(0.024 * side, 0.0, HIP), (0.027 * side, 0.004, (HIP + KNEE) / 2), (0.03 * side, 0.006, KNEE), (0.034 * side, 0.0, 0.07)],
             [0.027, 0.023, 0.019, 0.017], [UNIFORM, UNIFORM, UNIFORM])
        x = 0.034 * side
        limb(p, [(x, 0.002, 0.012), (x, 0.003, 0.085)], [0.018, 0.018], [BOOT])
        p.box((x - 0.014, -0.014, 0.004), (x + 0.014, 0.04, 0.022), BOOT)
        p.box((x - 0.0145, -0.0145, 0.0), (x + 0.0145, 0.0405, 0.004), DARK)
        p.box((x - 0.006 + 0.016 * side, -0.012, (HIP + KNEE) / 2 - 0.02), (x + 0.006 + 0.016 * side, 0.016, (HIP + KNEE) / 2 + 0.012),
              UNIFORM)  # a cargo pocket on the thigh
    # The hips, the jacket under the vest, the belt with its pouches.
    p.loft([ring((0.0, 0.0, HIP - 0.01), 0.046, 0.03), ring((0.0, 0.0, HIP + 0.012), 0.044, 0.029)], UNIFORM)
    stations = [(HIP + 0.008, 0.047, 0.030), (WAIST, 0.043, 0.028), (CHEST, 0.049, 0.030), (SHOULDER, 0.055, 0.027)]
    for (z0, rx0, ry0), (z1, rx1, ry1) in zip(stations, stations[1:]):
        p.loft([ring((0.0, 0.0, z0), rx0, ry0), ring((0.0, 0.0, z1), rx1, ry1)], UNIFORM)
    p.loft([ring((0.0, 0.0, HIP + 0.004), 0.05, 0.033), ring((0.0, 0.0, HIP + 0.02), 0.05, 0.033)], GEAR)
    for x in (-0.03, 0.0, 0.03):
        p.box((x - 0.011, 0.028, HIP - 0.002), (x + 0.011, 0.042, HIP + 0.024), GEAR)
    p.box((-0.02, -0.045, HIP - 0.004), (0.02, -0.028, HIP + 0.026), GEAR)  # a pouch on the back
    # Two belts of rounds across the chest, over the vest, front and back, from each shoulder to the other side's hip.
    for side in (-1, 1):
        for y in (0.042, -0.04):
            p.bar((0.038 * side, y, SHOULDER - 0.004), (-0.036 * side, y, WAIST - 0.004), 0.0055, BRASS)
    # The neck; the arms straight out, palms down: the sleeves rolled to the forearm, gloves.
    p.loft([ring((0.0, 0.0, SHOULDER), 0.03, 0.022), ring((0.0, 0.0, NECK + 0.01), 0.015, 0.015)], SKIN)
    for side in (-1, 1):
        limb(p, [(0.05 * side, 0.0, ARM), (0.128 * side, 0.0, ARM), (0.198 * side, 0.0, ARM), (0.234 * side, 0.0, ARM)],
             [0.017, 0.014, 0.0115, 0.0105], [UNIFORM, UNIFORM, GEAR])
    # The head, the helmet down to the brows, the goggles on its front, the nose.
    p.sphere((0.0, 0.0, HEAD), 'Z', HEAD_R, SKIN, sides=10, rings=6)
    p.turned((0.0, -0.004, HEAD), 'Z', [(-0.004, 0.054), (0.012, 0.054), (0.028, 0.047), (0.041, 0.032), (0.049, 0.012)], HELMET,
             sides=12)
    p.box((-0.032, 0.040, HEAD + 0.012), (0.032, 0.054, HEAD + 0.03), GLASS)
    p.box((-0.004, HEAD_R - 0.002, HEAD - 0.012), (0.004, HEAD_R + 0.006, HEAD + 0.0), SKIN)


def _team(p):
    """The vest over the jacket and a pad on each shoulder."""
    stations = [(WAIST - 0.01, 0.05, 0.035), (CHEST, 0.056, 0.037), (SHOULDER - 0.006, 0.058, 0.033)]
    for (z0, rx0, ry0), (z1, rx1, ry1) in zip(stations, stations[1:]):
        p.loft([ring((0.0, 0.0, z0), rx0, ry0), ring((0.0, 0.0, z1), rx1, ry1)], TEAM)
    for side in (-1, 1):
        p.turned((0.068 * side, 0.0, ARM + 0.004), 'X', [(-0.018, 0.017), (-0.006, 0.024), (0.01, 0.024), (0.02, 0.018)], TEAM, sides=8)


def _weapon(p):
    """A belt-fed machine gun along +Y, its grip in the right fist: stock, receiver, jacketed barrel, carry handle, folded bipod and
    the box of rounds on its left with the belt going in. Made at the origin; _hold puts it in the hand."""
    x, y, z = 0.0, 0.0, 0.0

    def at(dx, dy, dz):
        return (x + dx, y + dy, z + dz)

    p.profile_x([(y - 0.10, z - 0.004), (y - 0.03, z + 0.008), (y - 0.03, z + 0.03), (y - 0.10, z + 0.024)], x - 0.0065, x + 0.0065,
                METAL)  # stock
    p.box(at(-0.009, -0.03, 0.006), at(0.009, 0.05, 0.032), METAL)  # receiver
    p.cylinder(at(0.0, 0.1, 0.022), 'Y', 0.0085, 0.05, METAL, sides=8)  # barrel jacket
    p.cylinder(at(0.0, 0.175, 0.022), 'Y', 0.0042, 0.026, METAL, sides=6)  # barrel
    p.cylinder(at(0.0, 0.2, 0.022), 'Y', 0.006, 0.006, METAL, sides=6)  # flash hider
    p.box(at(-0.0015, 0.186, 0.027), at(0.0015, 0.192, 0.036), METAL)  # front sight
    p.bar(at(0.0, 0.0, 0.032), at(0.0, 0.012, 0.044), 0.0025, METAL)  # carry handle
    p.bar(at(0.0, 0.012, 0.044), at(0.0, 0.05, 0.044), 0.0025, METAL)
    p.bar(at(0.0, 0.05, 0.044), at(0.0, 0.056, 0.032), 0.0025, METAL)
    p.bar(at(0.0, 0.005, 0.01), at(0.0, -0.004, -0.016), 0.0055, METAL)  # pistol grip, in the fist
    for side in (-1, 1):
        p.bar(at(0.004 * side, 0.14, 0.014), at(0.006 * side, 0.07, 0.008), 0.0022, METAL)  # bipod legs, folded
    p.box(at(-0.026, -0.006, -0.004), at(-0.009, 0.03, 0.026), GEAR)  # the box of rounds
    p.bar(at(-0.018, 0.012, 0.026), at(-0.009, 0.012, 0.03), 0.004, BRASS)  # the belt into the receiver


def _sandbags(p):
    """Four courses of bags round the soldier, each over the joints of the one below, each a little further in."""
    for course in range(4):
        n, z, r = 9, 0.044 * course, RING_R - 0.006 * course
        for i in range(n):
            a = 2 * math.pi * (i + 0.5 * course) / n
            cc.bag(p, (math.cos(a) * r, math.sin(a) * r, z), a + math.pi / 2, SAND, length=0.135, width=0.075, height=0.046)


# --- material -------------------------------------------------------------------

def _material():
    """Woodland camouflage on the uniform and the helmet, olive webbing, brown boots, a dark gun, brass rounds, burlap bags."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.03)
    mask = cc.part_masks(g, PARTS)
    grain = g.noise(60.0, 2.0)

    def blotch(scale, cut):
        return g.clamp01(g.math('MULTIPLY_ADD', g.noise(scale, 2.0), 14.0, -14.0 * cut))

    camo = g.mix(blotch(9.0, 0.55), lin((0.44, 0.45, 0.33)), lin((0.30, 0.36, 0.22)))
    camo = g.mix(blotch(13.0, 0.6), camo, lin((0.40, 0.31, 0.22)))
    camo = g.mix(blotch(17.0, 0.64), camo, lin((0.17, 0.17, 0.14)))
    shade = lambda c, k=0.12: g.mix(g.math('MULTIPLY', grain, k), lin(c), lin(tuple(v * 0.8 for v in c)))
    weave = g.math('MULTIPLY', g.noise(140.0, 1.0), 0.3)
    albedo = cc.paint(g, mask, (shade((0.80, 0.60, 0.46), 0.08), camo, shade((0.30, 0.31, 0.22)), shade((0.30, 0.22, 0.15)),
                               shade((0.13, 0.13, 0.14), 0.2), shade((0.78, 0.60, 0.30), 0.2), lin((0.06, 0.08, 0.10)),
                               g.mix(weave, lin((0.68, 0.60, 0.42)), lin((0.50, 0.43, 0.29))), camo, lin((0.16, 0.28, 0.66)),
                               lin((0.05, 0.05, 0.05))))
    rough = cc.weighted(g, mask, (0.6, 0.85, 0.8, 0.7, 0.45, 0.35, 0.15, 0.95, 0.6, 0.7, 0.8))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.7, 0.9, 0.2, 0.0, 0.0, 0.0, 0.0))
    g.finish(albedo, g.bump(grain, 0.03, 0.002), g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------

def _man_height():
    """The height of civ_man: the skeleton is scaled to it for every person, so the joints of the soldier stand where its clips put
    them (the helmet rises over the hair)."""
    meshes, _, _ = civ.build("civ_man")
    height = max(v.co.z for v in meshes[0].data.vertices)
    cc.clear("civ_man", ("body",))
    return height


def _hold(p, arm):
    """Puts the gun made at the origin into the right hand of the T-pose, so that where the hands are in HOLD_CLIP its grip is in the
    right palm and its barrel lies over the left.

    The gun goes with the right hand: placed by the hands of the clip and taken back to the T-pose by the right hand's own move, it is
    held in that clip as it was placed. The soldiers' clips of Mixamo all hold a rifle the same way in the right hand, so it lies in
    the hands in the others too."""
    clip_arm, action = civ._import_clip(HOLD_CLIP)
    bpy.data.objects.remove(clip_arm, do_unlink=True)
    arm.animation_data_create()
    arm.animation_data.action = action
    if action.slots:
        arm.animation_data.action_slot = action.slots[0]
    bpy.context.scene.frame_set(HOLD_FRAME)
    bpy.context.view_layer.update()

    def palm(bone):
        return arm.matrix_world @ arm.pose.bones[bone].matrix @ Vector((0.0, arm.data.bones[bone].length / 2, 0.0))

    posed = arm.matrix_world @ arm.pose.bones[HAND].matrix
    rest = arm.matrix_world @ arm.data.bones[HAND].matrix_local
    grip, left = palm(HAND), palm(OTHER_HAND)
    arm.animation_data.action = None
    bpy.data.actions.remove(action)
    for pb in arm.pose.bones:
        pb.matrix_basis = Matrix.Identity(4)
    bpy.context.view_layer.update()
    ahead = (left - grip).normalized()
    up = Vector((0.0, 0.0, 1.0))
    up = (up - ahead * up.dot(ahead)).normalized()
    held = Matrix.Translation(grip) @ Matrix((ahead.cross(up), ahead, up)).transposed().to_4x4()
    bmesh.ops.transform(p.bm, matrix=rest @ posed.inverted() @ held, verts=p.bm.verts)
    return f"  gun held as in {HOLD_CLIP}: grip at {tuple(round(v, 3) for v in grip)}, the left hand {round((left - grip).length, 3)} ahead"


def _rigid(mesh, arm, bone):
    """Every vertex of mesh on one bone, fully: a gun does not bend."""
    mesh.vertex_groups.clear()
    group = mesh.vertex_groups.new(name=bone)
    group.add([v.index for v in mesh.data.vertices], 1.0, 'REPLACE')
    mesh.parent = arm
    mesh.matrix_parent_inverse = arm.matrix_world.inverted()
    mod = mesh.modifiers.new("rig", 'ARMATURE')
    mod.object = arm
    return f"  {mesh.name} on {bone}"


def run():
    """Builds the soldier and his sandbags, bakes them, skins him to the skeleton, saves the .blend and exports the GLB."""
    arm = civ.skeleton(ID, _man_height())
    cc.clear(ID, ("body", "team_mask", "weapon", "sandbags"))
    body, team, weapon, bags = Parts(), Parts(), Parts(), Parts()
    _body(body)
    _team(team)
    _weapon(weapon)
    held = _hold(weapon, arm)
    _sandbags(bags)
    meshes, mat, graph = cc.finish(ID, [(body, "body"), (team, "team_mask"), (weapon, "weapon"), (bags, "sandbags")], 0.0, _material())
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}" for obj in meshes]
    report.append(f"soldier triangles {sum(rc.tri_count(o) for o in meshes[:3])}")
    report.append(held)
    base = os.path.join(rc.SRC_ROOT, "unit", ID)
    cc.repack(meshes, cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    report.append(civ.skin(meshes[0], arm))
    report.append(civ.skin(meshes[1], arm))
    report.append(_rigid(meshes[2], arm, HAND))
    objs = [arm, *meshes]
    glb = os.path.join(rc.GLB_ROOT, "unit", ID, ID + ".glb")
    with rc.node_names(objs, ["rig", "body", "team_mask", "weapon", "sandbags"]):
        save_blend(objs, mat, imgs, os.path.join(base, ID + ".blend"))
        civ.export_rigged(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
