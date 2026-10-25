"""War miner harvester: the Union's armed ore harvester (docs/ra2_units/soviets/war_miner).

Size of the placeholder it replaces (UnitView.VehicleSize): 0.75 wide, 1 long, 0.42 high; the load strip of
HarvesterMarks floats at 0.46, so nothing rises above 0.44. Origin at the centre on the ground, front to +Y in
Blender (−Z in Godot). Tracked, a machine-gun turret (game/data/factions/union/units.yaml). Budget 8000
triangles, texture 1024.

Nodes (docs/art/model_standards.md, section 4):
    hull                 tracks, hull, the ore bin, the intake with its grille and upper roller
      team_mask          stripes on the bin's sides, the plate on the glacis: the owner's colour
      spin_drum          the toothed roller at the bottom of the intake; its origin on the axle
      turret             turned by the game around its own origin, on the turret block
        team_mask_turret roof and side plates of the turret
        barrel           the twin machine gun, its origin at the mount
          muzzle_0, muzzle_1  empties: where the shots leave
The game does not turn spin_drum yet (M1, 4.9: working animations).

The turret stands far enough ahead of the bin for the guns to sweep round over the deck. The parts follow the
reference picture given by the owner: a dark tracked chassis, a tall meshed bin behind, a small turret with two
guns, an intake with cheeks, a grille and rollers in front. Proportions and details are our own, in the Union's
look (docs/art/style_guide/02_units_buildings.md, section 2): heavy boxes, dark steel. No emblems or text.

In Blender, after rts_common is on sys.path:
    import units.harvester as miner
    print(miner.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, attach, empty, lin, save_blend, uv_together

ID = "harvester"
SIZE = 1024


def _part(index):
    """Surface part i, written to PART as i / 10 in the red channel; the material picks it back."""
    return (index / 10, 0.0, 0.0, 1.0)


ARMOR, METAL, RUBBER, BIN = (_part(i) for i in range(4))

TURRET_AT = Vector((0.0, 0.17, 0.29))   # turret origin in hull space: on the turret block
BARREL_AT = Vector((0.0, 0.09, 0.05))   # gun mount in turret space
GUN = 0.15                              # length of the guns: their reach stays short of the bin
GUN_X = 0.028                           # half the spacing of the two guns
DRUM_AT = Vector((0.0, 0.46, 0.06))     # axle of the toothed roller


def _glacis_z(y):
    """Height of the sloped front of the deck."""
    return 0.22 - (y - 0.28) * (0.07 / 0.08)


# --- geometry -------------------------------------------------------------------

def _hull():
    p = Parts()
    track = [(-0.40, 0.0), (0.32, 0.0), (0.40, 0.05), (0.40, 0.09), (0.36, 0.14),
             (-0.44, 0.14), (-0.49, 0.09), (-0.49, 0.05)]
    for s in (-1, 1):
        lo, hi = sorted((s * 0.25, s * 0.37))
        p.profile_x(track, lo, hi, RUBBER)                                     # track belt
        for y in (-0.33, -0.19, -0.05, 0.09, 0.23):
            p.cylinder((s * 0.375, y, 0.05), 'X', 0.045, 0.008, METAL)         # road wheel
        p.cylinder((s * 0.375, -0.44, 0.07), 'X', 0.05, 0.008, METAL)          # drive sprocket
        p.cylinder((s * 0.375, 0.36, 0.07), 'X', 0.045, 0.008, METAL)          # idler
        lo, hi = sorted((s * 0.19, s * 0.235))
        p.profile_x([(0.32, 0.03), (0.50, 0.03), (0.50, 0.07), (0.38, 0.22), (0.32, 0.22)], lo, hi, ARMOR)  # intake cheek
        p.box((s * 0.25 - 0.02, 0.2, 0.22), (s * 0.25 + 0.02, 0.235, 0.245), METAL)  # headlight
    p.box((-0.25, -0.46, 0.05), (0.25, 0.36, 0.16), ARMOR)                     # lower hull between the tracks
    deck = [(-0.49, 0.15), (0.36, 0.15), (0.36, 0.16), (0.28, 0.22), (-0.47, 0.22), (-0.49, 0.20)]
    p.profile_x(deck, -0.31, 0.31, ARMOR)                                      # deck
    p.box((-0.12, 0.07, 0.22), (0.12, 0.27, 0.29), ARMOR)                      # turret block

    # Ore bin, its top edges chamfered, a frame post at each corner.
    rings = []
    for z, inset in ((0.22, 0.0), (0.38, 0.0), (0.4, 0.02)):
        rings.append([Vector((-0.36 + inset, -0.5 + inset, z)), Vector((0.36 - inset, -0.5 + inset, z)),
                      Vector((0.36 - inset, -0.08 - inset, z)), Vector((-0.36 + inset, -0.08 - inset, z))])
    p.loft(rings, BIN)
    for x in (-0.365, 0.365):
        for y in (-0.505, -0.075):
            p.box((x - 0.012, y - 0.012, 0.21), (x + 0.012, y + 0.012, 0.385), METAL)

    # Intake: a grille from the deck down to the front and an upper roller behind it.
    for x in (-0.135, -0.045, 0.045, 0.135):
        p.bar((x, 0.36, 0.205), (x, 0.48, 0.11), 0.009, METAL)
    p.cylinder((0.0, 0.43, 0.15), 'X', 0.035, 0.19, METAL, sides=12)
    return p.finish("hull")


def _drum():
    p = Parts()
    p.cylinder((0.0, 0.0, 0.0), 'X', 0.04, 0.19, METAL, sides=12)
    for i in range(6):
        a = 2 * math.pi * (i + 0.5 * (i % 2)) / 6
        out = Vector((0.0, math.cos(a), math.sin(a)))
        for x in (-0.13, -0.045, 0.045, 0.13):
            p.bar(Vector((x, 0, 0)) + out * 0.035, Vector((x, 0, 0)) + out * 0.058, 0.01, METAL)  # tooth
    return p.finish("spin_drum")


def _turret():
    p = Parts()
    p.cylinder((0.0, 0.0, 0.0075), 'Z', 0.09, 0.0075, METAL, sides=16)        # ring
    rings = []
    for z, inset in ((0.015, 0.0), (0.075, 0.0), (0.09, 0.02)):
        rings.append([Vector((-0.11 + inset, -0.09 + inset, z)), Vector((0.11 - inset, -0.09 + inset, z)),
                      Vector((0.11 - inset, 0.09 - inset, z)), Vector((-0.11 + inset, 0.09 - inset, z))])
    p.loft(rings, ARMOR)
    p.box((-0.05, 0.08, 0.025), (0.05, 0.105, 0.08), METAL)                    # gun mantlet
    return p.finish("turret")


def _barrel():
    p = Parts()
    for x in (-GUN_X, GUN_X):
        p.cylinder((x, GUN / 2, 0.0), 'Y', 0.008, GUN / 2, METAL, sides=8)    # gun
        p.cylinder((x, 0.03, 0.0), 'Y', 0.014, 0.03, METAL, sides=8)          # cooling jacket
    return p.finish("barrel")


def _team_hull():
    p = Parts()
    for s in (-1, 1):
        lo, hi = sorted((s * 0.36, s * 0.364))
        p.box((lo, -0.17, 0.235), (hi, -0.12, 0.37), ARMOR)                   # band down the bin's side
    plate = [(y, _glacis_z(y) + dz) for y, dz in ((0.29, 0.001), (0.35, 0.001), (0.35, 0.005), (0.29, 0.005))]
    p.profile_x(plate, -0.09, 0.09, ARMOR)                                     # plate on the glacis
    return p.finish("team_mask")


def _team_turret():
    p = Parts()
    p.box((-0.085, -0.065, 0.09), (0.085, 0.065, 0.094), ARMOR)               # roof plate
    for s in (-1, 1):
        lo, hi = sorted((s * 0.11, s * 0.114))
        p.box((lo, -0.065, 0.025), (hi, 0.065, 0.07), ARMOR)                  # side plate
    return p.finish("team_mask_turret")


# --- material -------------------------------------------------------------------

def _material():
    """Dark steel with panel lines, grey metal, black rubber, an ochre bin covered with mesh."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    index = g.math('MULTIPLY', rc.sock(split, 'Red', outputs=True), 10.0)
    mask = [g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.math('SUBTRACT', index, float(i))), -2.0, 1.0))
            for i in range(4)]

    grain = g.noise(40.0, 4.0)
    coord = g.object_coord()

    def lines(axis_name, per_cell, width):
        t = g.math('FRACT', g.math('MULTIPLY', g.axis(coord, axis_name), per_cell))
        return g.clamp01(g.math('MULTIPLY_ADD', t, -1.0 / width, 1.0))

    # Panel lines every 0.1 cells along the hull; a fine mesh on the bin in all three directions.
    upright = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.axis(g.normal_world(), 'Z')), -4.0, 3.4))
    panels = g.math('MULTIPLY', g.math('MULTIPLY', lines('Y', 10.0, 0.05), upright), 0.6)
    mesh = g.math('MAXIMUM', g.math('MAXIMUM', lines('X', 40.0, 0.18), lines('Y', 40.0, 0.18)), lines('Z', 40.0, 0.18))
    mesh = g.math('MULTIPLY', mesh, 0.55)

    armor_col = g.mix(panels, g.mix(grain, lin((0.30, 0.31, 0.32)), lin((0.38, 0.39, 0.40))), lin((0.16, 0.16, 0.17)))
    metal_col = g.mix(grain, lin((0.40, 0.41, 0.43)), lin((0.50, 0.51, 0.53)))
    rubber_col = g.mix(grain, lin((0.10, 0.10, 0.11)), lin((0.17, 0.17, 0.18)))
    bin_col = g.mix(mesh, g.mix(grain, lin((0.62, 0.52, 0.30)), lin((0.72, 0.61, 0.37))), lin((0.30, 0.24, 0.13)))

    albedo = armor_col
    for m, col in zip(mask[1:], (metal_col, rubber_col, bin_col)):
        albedo = g.mix(m, albedo, col)

    def weighted(values):
        out = g.math('MULTIPLY', mask[0], values[0])
        for m, v in zip(mask[1:], values[1:]):
            out = g.math('MULTIPLY_ADD', m, v, out)
        return out

    rough = weighted((0.55, 0.4, 0.88, 0.6))
    metallic = weighted((0.6, 0.85, 0.0, 0.5))
    normal = g.bump(g.math('MAXIMUM', g.math('MULTIPLY', mesh, mask[3]), g.noise(80.0, 3.0)), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------

def build():
    hull = _hull()
    drum = _drum()
    turret = _turret()
    barrel = _barrel()
    team = _team_hull()
    team_turret = _team_turret()
    attach(drum, hull, DRUM_AT)
    attach(turret, hull, TURRET_AT)
    attach(barrel, turret, BARREL_AT)
    attach(team, hull, (0.0, 0.0, 0.0))
    attach(team_turret, turret, (0.0, 0.0, 0.0))
    muzzles = [empty(f"muzzle_{i}", barrel, (x, GUN, 0.0)) for i, x in enumerate((-GUN_X, GUN_X))]
    bpy.context.view_layer.update()

    meshes = [hull, drum, turret, barrel, team, team_turret]
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, muzzles, mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, muzzles, mat, graph = build()
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"
              for obj in meshes]
    report.append(f"total triangles {sum(rc.tri_count(obj) for obj in meshes)}")
    base = os.path.join(rc.SRC_ROOT, "unit", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = [*meshes, *muzzles]
    glb = os.path.join(rc.GLB_ROOT, "unit", ID, ID + ".glb")
    save_blend(objs, mat, imgs, os.path.join(base, ID + ".blend"))
    rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
