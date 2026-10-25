"""Grizzly battle tank alliance_light_tank: the Alliance's main tank (docs/ra2_units/allies/grizzly_tank).

Size of the placeholder it replaces (UnitView.VehicleSize): 0.58 wide, 0.8 long. Origin at the centre of the hull
on the ground, front to +Y in Blender (−Z in Godot after the Y-up export). Budget 8000 triangles, texture 1024.

Nodes (docs/art/model_standards.md, section 4):
    hull                 tracks, wheels, hull, rear drums
      team_mask          stripes on the side skirts and the fenders, the glacis plate: the owner's colour
      turret             turned by the game around its own origin, the centre of the turret ring
        team_mask_turret band over the turret
        barrel           the gun, its origin at the trunnion
          muzzle_0       empty: the point the shot leaves from

The look follows the silhouette class of the appearance card: tracks under wide fenders that stand out of the hull,
a narrow flat turret, a long gun. Light enamel with darker panel lines, grey metal, black tracks. The owner's blue
stripes of the reference became the team_mask parts. No emblems or markings (docs/15_legal.md, section 3).

In Blender, after rts_common is on sys.path:
    import units.alliance_light_tank as tank
    print(tank.run())
"""

import math
import os

import bpy
from mathutils import Vector

import rts_common as rc
from units.parts import PART, Parts, attach, empty, lin, save_blend, uv_together

ID = "alliance_light_tank"
SIZE = 1024

# Surface parts, written to the colour attribute PART; the material turns them into enamel, metal and rubber.
ENAMEL = (1.0, 0.0, 0.0, 1.0)
METAL = (0.0, 1.0, 0.0, 1.0)
RUBBER = (0.0, 0.0, 1.0, 1.0)

TURRET_AT = Vector((0.0, -0.03, 0.225))   # turret origin in hull space: centre of the ring, on the deck
BARREL_AT = Vector((0.0, 0.17, 0.05))     # barrel origin in turret space: the trunnion
MUZZLE_Y = 0.40                           # muzzle along the barrel
TURRET_K = 1.12                           # turret outline scale: wide enough to fill most of the hull


# --- geometry -------------------------------------------------------------------

def _hull():
    p = Parts()
    track = [(-0.30, 0.0), (0.30, 0.0), (0.37, 0.05), (0.37, 0.09), (0.33, 0.13),
             (-0.34, 0.13), (-0.38, 0.09), (-0.38, 0.05)]
    for s in (-1, 1):
        p.profile_x(track, s * 0.175, s * 0.285, RUBBER)                       # track belt
        for y in (-0.24, -0.12, 0.0, 0.12, 0.24):
            p.cylinder((s * 0.29, y, 0.045), 'X', 0.042, 0.008, METAL)         # road wheel
            p.cylinder((s * 0.3, y, 0.045), 'X', 0.016, 0.006, METAL, sides=8)  # hub
        p.cylinder((s * 0.29, -0.335, 0.07), 'X', 0.05, 0.008, METAL)          # drive sprocket
        p.cylinder((s * 0.29, 0.325, 0.07), 'X', 0.045, 0.008, METAL)          # idler
        p.box((min(s * 0.285, s * 0.3), -0.36, 0.085), (max(s * 0.285, s * 0.3), 0.36, 0.13), ENAMEL)  # side skirt
        for y in (-0.25, -0.33):
            p.cylinder((s * 0.22, y, 0.23), 'X', 0.03, 0.06, METAL, sides=12)    # drum on the rear deck
        p.box((s * 0.2 - 0.016, 0.3, 0.185), (s * 0.2 + 0.016, 0.335, 0.215), METAL)  # headlight
    p.box((-0.175, -0.35, 0.05), (0.175, 0.34, 0.13), ENAMEL)                  # lower hull between the tracks
    upper = [(-0.39, 0.13), (0.39, 0.13), (0.42, 0.16), (0.30, 0.20), (-0.36, 0.20), (-0.39, 0.18)]
    p.profile_x(upper, -0.295, 0.295, ENAMEL)                                  # upper hull with the fenders
    p.box((-0.16, -0.34, 0.20), (0.16, 0.12, 0.215), ENAMEL)                   # engine deck
    p.cylinder((TURRET_AT.x, TURRET_AT.y, 0.22), 'Z', 0.12, 0.005, METAL, sides=24)  # turret ring
    return p.finish("hull")


def _turret_outline():
    """The turret seen from above: round at the back, straight sides, a narrow chamfered front."""
    arc = [(0.165 * math.cos(-math.pi * i / 10), -0.05 + 0.165 * math.sin(-math.pi * i / 10)) for i in range(1, 10)]
    pts = [(0.11, 0.16), (0.165, 0.10), (0.165, -0.05), *arc, (-0.165, -0.05), (-0.165, 0.10), (-0.11, 0.16)]
    return [(x * TURRET_K, y * TURRET_K) for x, y in pts]


def _turret():
    p = Parts()
    outline = _turret_outline()
    cy = -0.03
    rings = []
    for z, k in ((0.0, 0.94), (0.025, 1.0), (0.065, 1.0), (0.095, 0.8)):      # undercut, wall, sloped roof edge
        rings.append([Vector((x * k, cy + (y - cy) * k, z)) for x, y in outline])
    p.loft(rings, ENAMEL)
    p.box((-0.055, 0.14, 0.015), (0.055, 0.21, 0.085), METAL)                  # gun mantlet
    p.cylinder((-0.06, -0.09, 0.105), 'Z', 0.038, 0.012, ENAMEL)               # commander's cupola
    p.cylinder((-0.06, -0.09, 0.119), 'Z', 0.032, 0.003, METAL)                # its hatch
    p.box((0.03, -0.11, 0.09), (0.1, -0.05, 0.112), METAL)                     # sight block
    return p.finish("turret")


def _barrel():
    p = Parts()
    p.cylinder((0, MUZZLE_Y / 2, 0), 'Y', 0.016, MUZZLE_Y / 2, METAL, sides=12)
    p.cylinder((0, 0.225, 0), 'Y', 0.025, 0.035, METAL, sides=12)             # fume extractor
    p.cylinder((0, MUZZLE_Y - 0.02, 0), 'Y', 0.021, 0.02, METAL, sides=12)    # muzzle ring
    return p.finish("barrel")


def _glacis_z(y):
    """Height of the sloped front of the upper hull."""
    return 0.20 - (y - 0.30) * (0.04 / 0.12)


def _team_hull():
    p = Parts()
    for s in (-1, 1):
        lo, hi = sorted((s * 0.3, s * 0.304))
        p.box((lo, -0.07, 0.088), (hi, 0.05, 0.128), ENAMEL)                  # stripe on the side skirt
        lo, hi = sorted((s * 0.17, s * 0.297))
        p.box((lo, -0.07, 0.2), (hi, 0.05, 0.204), ENAMEL)                    # the same stripe over the fender
    plate = [(y, _glacis_z(y) + dz) for y, dz in ((0.31, 0.001), (0.39, 0.001), (0.39, 0.005), (0.31, 0.005))]
    p.profile_x(plate, -0.07, 0.07, ENAMEL)                                    # plate on the glacis
    return p.finish("team_mask")


def _team_turret():
    p = Parts()
    p.box((-0.14, 0.0, 0.095), (0.14, 0.05, 0.099), ENAMEL)                   # band over the roof
    for s in (-1, 1):
        lo, hi = sorted((s * 0.165 * TURRET_K, s * (0.165 * TURRET_K + 0.004)))
        p.box((lo, 0.0, 0.025), (hi, 0.05, 0.065), ENAMEL)                    # and down the straight sides
    return p.finish("team_mask_turret")


# --- material -------------------------------------------------------------------

def _material():
    """Enamel with panel lines on its upright faces, grey metal, black rubber; the parts come from PART."""
    mat = rc.new_material(ID + "_mat")
    g = rc.Graph(mat)
    ao = g.ao(0.1)
    attr = g.add('ShaderNodeAttribute', attribute_name=PART)
    split = g.add('ShaderNodeSeparateColor')
    g.link(rc.sock(attr, 'Color', outputs=True), split, 'Color')
    enamel = rc.sock(split, 'Red', outputs=True)
    metal = rc.sock(split, 'Green', outputs=True)
    rubber = rc.sock(split, 'Blue', outputs=True)

    grain = g.noise(40.0, 4.0)
    # Panel lines every 0.08 cells along the hull, on the upright faces only.
    t = g.math('FRACT', g.math('MULTIPLY', g.axis(g.object_coord(), 'Y'), 12.5))
    line = g.clamp01(g.math('MULTIPLY_ADD', t, -20.0, 1.0))
    upright = g.clamp01(g.math('MULTIPLY_ADD', g.math('ABSOLUTE', g.axis(g.normal_world(), 'Z')), -4.0, 3.4))
    line = g.math('MULTIPLY', g.math('MULTIPLY', line, upright), 0.7)

    enamel_col = g.mix(line, g.mix(grain, lin((0.74, 0.76, 0.80)), lin((0.83, 0.85, 0.89))), lin((0.34, 0.35, 0.38)))
    metal_col = g.mix(grain, lin((0.36, 0.37, 0.40)), lin((0.47, 0.48, 0.51)))
    rubber_col = g.mix(grain, lin((0.10, 0.10, 0.11)), lin((0.17, 0.17, 0.18)))
    albedo = g.mix(rubber, g.mix(metal, enamel_col, metal_col), rubber_col)

    rough = g.math('MULTIPLY_ADD', rubber, 0.88, g.math('MULTIPLY_ADD', metal, 0.4, g.math('MULTIPLY', enamel, 0.45)))
    metallic = g.math('MULTIPLY_ADD', metal, 0.85, g.math('MULTIPLY', enamel, 0.1))
    normal = g.bump(g.noise(80.0, 3.0), 0.03, 0.005)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------

def build():
    hull = _hull()
    turret = _turret()
    barrel = _barrel()
    team = _team_hull()
    team_turret = _team_turret()
    attach(turret, hull, TURRET_AT)
    attach(barrel, turret, BARREL_AT)
    attach(team, hull, (0.0, 0.0, 0.0))
    attach(team_turret, turret, (0.0, 0.0, 0.0))
    muzzle = empty("muzzle_0", barrel, (0.0, MUZZLE_Y, 0.0))
    bpy.context.view_layer.update()

    meshes = [hull, turret, barrel, team, team_turret]
    uv_together(meshes)
    mat, graph = _material()
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, muzzle, mat, graph


def run():
    """Builds, bakes, saves the .blend, exports the GLB and reports what was written."""
    meshes, muzzle, mat, graph = build()
    report = [f"{obj.name}: triangles {rc.tri_count(obj)}, size {tuple(round(v, 3) for v in obj.dimensions)}"
              for obj in meshes]
    report.append(f"total triangles {sum(rc.tri_count(obj) for obj in meshes)}")
    base = os.path.join(rc.SRC_ROOT, "unit", ID)
    imgs = rc.bake_material(meshes, graph, SIZE, ID, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = [*meshes, muzzle]
    glb = os.path.join(rc.GLB_ROOT, "unit", ID, ID + ".glb")
    save_blend(objs, mat, imgs, os.path.join(base, ID + ".blend"))
    rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)
