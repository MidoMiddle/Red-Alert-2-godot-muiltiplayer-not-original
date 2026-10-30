"""Tech hospital, tech_hospital (docs/ra2_buildings/tech/hospital, docs/map_elements/12_tech_buildings): the neutral hospital an
engineer takes; a hurt soldier of its owner goes in and comes out whole (game/data/neutral/tech.yaml, EntrySystem). The kinds of parts
follow the owner's pictures of RA2's tech hospital (2026-10-30): a block of storeys stepping up to a tall wing with a helipad on its
roof, rows of windows, the healing sign on the front, porches over the doors, a flag, lawns and shrubs round it; the shapes and the
layout are ours (generation_requirements.md, section 8). The sign is our own: a green cross on white, the colour of healing in the game
(docs/art/style_guide/01_colour.md), not the red cross.

Footprint 6x4 cells, origin at the centre of the footprint on the ground, the front to -Y; made at the size the game draws it. The parts:
    plaza       paving over the footprint (the base node), lawns at the corners with shrubs, a walk to the doors, a bench
    tower       at the back left, seven storeys: the helipad on its roof (a dark square, a white ring, an H, lights at the corners),
                a porch over its side door
    main wing   five storeys in the middle; on its roof a stair hut, air units, vents
    right wing  four storeys, standing forward at the right; a sign frame on its roof with the green cross on both faces, a water tank
    entrance    three storeys before the main wing: the green cross on a white panel on its front, a glass door under a canopy on posts
    each wing   a light concrete box on a darker plinth, a band of glass each storey on every side with piers over it, a cornice and a
                parapet round a dark roof
    yard        an ambulance by the door (white, a green stripe, a light bar)
    team mask   the flag at the front right and a band under the entrance's cornice: the owner's colour
Nodes: "body", "team_mask", "base". One GLB per state:
    tech_hospital            whole
    tech_hospital_abandoned  critically damaged: windows blown out in patches, a corner of the right wing's top storey fallen in,
                             the helipad cracked, the sign hanging, the canopy down at one end, the ambulance burnt out, the shrubs
                             scorched, soot; fire in the main wing, the right wing and the tower (fire_1..3)
    tech_hospital_ruins      destroyed: jagged stumps of the wings, the tower's the tallest, heaps of slabs, the burnt ambulance
Budget 8000 triangles, texture 2048 (docs/map_elements/12_tech_buildings/prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.tech_hospital as th
    print(th.run_all())
"""

import math
import os
import random

import bmesh
import bpy
from mathutils import Matrix, Vector

import rts_common as rc
from families import city_common as cc
from units.parts import Parts, lin, uv_together

SIZE = 2048
VARIANTS = {
    "tech_hospital": "intact",
    "tech_hospital_abandoned": "abandoned",
    "tech_hospital_ruins": "ruins",
}

(WALL, TRIM, PLINTH, GLASS, ROOF, DARK, WHITE, GREEN, RED, YELLOW, GRASS, SHRUB, PAVING, TEAM, METAL, WOOD) = (cc.part(i) for i in range(16))
PARTS = 16

PAD = 0.04
S = 0.24  # a storey
# The wings: x0, x1, y0, y1, storeys, the sides with windows.
TOWER = (-2.75, -1.55, -0.3, 1.6, 7, ("front", "back", "left", "right"))
MAIN = (-1.55, 1.0, -0.5, 1.75, 5, ("front", "back", "right"))
RIGHT = (1.0, 2.75, -1.3, 1.2, 4, ("front", "back", "right"))
ENTRANCE = (-1.0, 0.4, -1.25, -0.5, 3, ("front", "left", "right"))
FIRE_POINTS = ((-0.3, 0.6, PAD + 5 * S + 0.05), (2.3, -0.8, PAD + 3 * S + 0.05), (-2.15, 0.3, PAD + 7 * S + 0.05))


def _top(wing):
    return PAD + wing[4] * S


def _faces(wing):
    """The sides of a wing as (side, the point at its start, its direction along, its outward normal, its length)."""
    x0, x1, y0, y1 = wing[:4]
    return {
        "front": (Vector((x0, y0, 0)), Vector((1, 0, 0)), Vector((0, -1, 0)), x1 - x0),
        "back": (Vector((x0, y1, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), x1 - x0),
        "left": (Vector((x0, y0, 0)), Vector((0, 1, 0)), Vector((-1, 0, 0)), y1 - y0),
        "right": (Vector((x1, y0, 0)), Vector((0, 1, 0)), Vector((1, 0, 0)), y1 - y0),
    }


def _slab(p, origin, along, out, a0, a1, z0, z1, d0, d1, painted):
    """A box on a side: from a0 to a1 along it, z0 to z1 up, d0 to d1 out from its face."""
    pts = [origin + along * a + out * d for a, d in ((a0, d0), (a1, d0), (a1, d1), (a0, d1))]
    p.loft([[Vector((q.x, q.y, z)) for q in pts] for z in (z0, z1)], painted)


def _wing(p, wing, variant, storeys=None):
    """A wing: the box on its plinth, glass bands and piers on its sides, the cornice, the parapet and the roof. Returns the roof's z."""
    x0, x1, y0, y1, n, sides = wing
    n = storeys or n
    top = PAD + n * S
    p.box((x0, y0, PAD), (x1, y1, top), WALL)
    p.box((x0 - 0.012, y0 - 0.012, PAD), (x1 + 0.012, y1 + 0.012, PAD + 0.07), PLINTH)
    rnd = random.Random(int((x0 + 5) * 100))
    for side in sides:
        origin, along, out, length = _faces(wing)[side]
        for k in range(n):
            z = PAD + k * S + 0.08
            _slab(p, origin, along, out, 0.06, length - 0.06, z, z + 0.11, -0.01, 0.006, GLASS)
            if variant == "abandoned" and rnd.random() < 0.3:
                a = rnd.uniform(0.1, length - 0.4)
                _slab(p, origin, along, out, a, a + rnd.uniform(0.15, 0.3), z - 0.02, z + 0.14, -0.01, 0.009, DARK)  # blown out
            _slab(p, origin, along, out, 0.04, length - 0.04, z + 0.13, z + 0.15, -0.01, 0.012, TRIM)  # a sill band
        for a in cc.steps(0.06, length - 0.06, 0.16):
            _slab(p, origin, along, out, a - 0.016, a + 0.016, PAD + 0.07, top - 0.03, -0.01, 0.014, WALL)
    p.box((x0 - 0.03, y0 - 0.03, top), (x1 + 0.03, y1 + 0.03, top + 0.05), TRIM)
    p.box((x0 + 0.04, y0 + 0.04, top + 0.05), (x1 - 0.04, y1 - 0.04, top + 0.055), ROOF)
    return top + 0.055


def _cross(p, centre, along, out, size, depth):
    """The healing sign: a white square, a green cross on it, standing out of a face along out."""
    c = Vector(centre)
    h = size / 2
    _slab(p, c, along, out, -h, h, c.z - h, c.z + h, 0.0, depth, WHITE)
    _slab(p, c, along, out, -h * 0.22, h * 0.22, c.z - h * 0.75, c.z + h * 0.75, depth, depth + 0.006, GREEN)
    _slab(p, c, along, out, -h * 0.75, h * 0.75, c.z - h * 0.22, c.z + h * 0.22, depth, depth + 0.006, GREEN)


def _helipad(p, roof, variant):
    x0, x1, y0, y1 = TOWER[:4]
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    p.box((x0 + 0.08, y0 + 0.1, roof), (x1 - 0.08, y1 - 0.1, roof + 0.012), DARK)
    p.turned((cx, cy, roof), 'Z', [(0.0, 0.42), (0.018, 0.42), (0.018, 0.37), (0.0, 0.37)], WHITE, sides=28)
    for x in (-0.14, 0.14):
        p.box((cx + x - 0.03, cy - 0.17, roof), (cx + x + 0.03, cy + 0.17, roof + 0.02), WHITE)
    p.box((cx - 0.12, cy - 0.03, roof), (cx + 0.12, cy + 0.03, roof + 0.02), WHITE)
    for sx in (-1, 1):
        for sy in (-1, 1):
            x, y = cx + sx * (x1 - x0 - 0.24) / 2, cy + sy * (y1 - y0 - 0.28) / 2
            p.box((x - 0.025, y - 0.025, roof), (x + 0.025, y + 0.025, roof + 0.03), YELLOW)
    if variant == "abandoned":
        cc.block(p, (cx + 0.2, cy - 0.3, roof + 0.015), (0.3, 0.05, 0.01), (0.0, 0.0, 0.5), ROOF)  # cracks
        cc.block(p, (cx - 0.25, cy + 0.35, roof + 0.015), (0.25, 0.04, 0.01), (0.0, 0.0, -0.8), ROOF)
    # The stair hut up to it, at the back.
    p.box((x0 + 0.1, y1 - 0.3, roof - 0.005), (x0 + 0.4, y1 - 0.08, roof + 0.2), WALL)
    p.box((x0 + 0.08, y1 - 0.32, roof + 0.2), (x0 + 0.42, y1 - 0.06, roof + 0.23), TRIM)


def _roof_kit(p, roof, rnd, x0, x1, y0, y1, units):
    """Air units, vents and a stair hut on a flat roof."""
    for k in range(units):
        x, y = rnd.uniform(x0 + 0.2, x1 - 0.3), rnd.uniform(y0 + 0.25, y1 - 0.3)
        p.box((x, y, roof), (x + 0.2, y + 0.14, roof + 0.1), METAL)
        p.turned((x + 0.1, y + 0.07, roof + 0.1), 'Z', [(0.0, 0.05), (0.01, 0.05)], DARK, sides=10)
    for k in range(units + 1):
        x, y = rnd.uniform(x0 + 0.15, x1 - 0.15), rnd.uniform(y0 + 0.15, y1 - 0.15)
        p.turned((x, y, roof), 'Z', [(0.0, 0.025), (0.08, 0.025), (0.08, 0.04), (0.1, 0.04), (0.1, 0.0)], METAL, sides=8)


def _entrance(p, team, variant):
    x0, x1, y0, y1 = ENTRANCE[:4]
    roof = _wing(p, ENTRANCE, variant)
    top = _top(ENTRANCE)
    team.box((x0 - 0.035, y0 - 0.035, top - 0.05), (x1 + 0.035, y1 + 0.01, top - 0.005), TEAM)  # a band under the cornice
    _cross(p, ((x0 + x1) / 2, y0, PAD + 2 * S + 0.02), Vector((1, 0, 0)), Vector((0, -1, 0)), 0.36, 0.02)
    # The glass door in a frame, the canopy on two posts over it.
    cx = (x0 + x1) / 2
    p.box((cx - 0.22, y0 - 0.02, PAD), (cx + 0.22, y0 + 0.005, PAD + 0.17), DARK if variant != "intact" else GLASS)
    p.box((cx - 0.25, y0 - 0.025, PAD + 0.17), (cx + 0.25, y0, PAD + 0.2), TRIM)
    fall = 0.0 if variant == "intact" else 0.12
    p.loft([[Vector((cx + sx * 0.36, y, PAD + 0.24 - (fall if sx > 0 and y < y0 - 0.1 else 0.0))) for sx, y in ((-1, y0), (1, y0), (1, y0 - 0.34), (-1, y0 - 0.34))],
            [Vector((cx + sx * 0.36, y, PAD + 0.27 - (fall if sx > 0 and y < y0 - 0.1 else 0.0))) for sx, y in ((-1, y0), (1, y0), (1, y0 - 0.34), (-1, y0 - 0.34))]], WHITE)
    for sx in (-1, 1):
        if variant != "intact" and sx > 0:
            continue
        p.cylinder((cx + sx * 0.32, y0 - 0.3, PAD + 0.12), 'Z', 0.015, 0.12, METAL, sides=6)
    # Steps before the door.
    for k in range(2):
        p.box((cx - 0.3, y0 - 0.06 - 0.06 * k, PAD), (cx + 0.3, y0 - 0.0, PAD + 0.03 * (2 - k)), PLINTH)
    return roof


def _porch(p, variant):
    """A small porch over the tower's front door."""
    x0, x1, y0 = TOWER[0], TOWER[1], TOWER[2]
    cx = (x0 + x1) / 2
    p.box((cx - 0.15, y0 - 0.015, PAD), (cx + 0.15, y0 + 0.005, PAD + 0.16), GLASS if variant == "intact" else DARK)
    p.box((cx - 0.26, y0 - 0.28, PAD + 0.2), (cx + 0.26, y0, PAD + 0.23), TRIM)
    for sx in (-1, 1):
        p.cylinder((cx + sx * 0.22, y0 - 0.24, PAD + 0.1), 'Z', 0.012, 0.1, METAL, sides=6)


def _sign(p, roof, variant):
    """The frame on the right wing's roof with the green cross on both faces."""
    x, y = 1.9, -1.15
    lean = 0.0 if variant == "intact" else 0.35
    for sx in (-0.2, 0.2):
        p.bar((x + sx, y + 0.05, roof), (x + sx, y + 0.05, roof + 0.18), 0.01, METAL)
    base = Vector((x, y, roof + 0.36))
    if variant == "intact":
        _cross(p, base, Vector((1, 0, 0)), Vector((0, -1, 0)), 0.36, 0.02)
        _cross(p, base + Vector((0, 0.02, 0)), Vector((1, 0, 0)), Vector((0, 1, 0)), 0.36, 0.0)
    else:
        part = Parts()
        _cross(part, Vector((0, 0, 0)), Vector((1, 0, 0)), Vector((0, -1, 0)), 0.36, 0.02)
        bmesh.ops.transform(part.bm, matrix=Matrix.Translation(base + Vector((0.05, 0, -0.08))) @ Matrix.Rotation(lean, 4, 'Y'),
                            verts=list(part.bm.verts))
        _copy(part, p)


def _copy(src, dst):
    """Copies the faces of one Parts into another, part by part."""
    for face in src.bm.faces:
        new = dst.bm.faces.new([dst.bm.verts.new(v.co) for v in face.verts])
        for loop, old in zip(new.loops, face.loops):
            loop[dst.layer] = old[src.layer]


def _plaza(base, body, variant):
    base.box((-2.98, -1.98, 0.0), (2.98, 1.98, PAD), PAVING)
    # Lawns at the front left and the back, the walk to the doors between them.
    lawns = ((-2.9, -1.9, -1.2, -0.45), (-2.9, 1.65, 2.9, 1.9), (-1.55, -1.9, -1.15, -0.55))
    for x0, y0, x1, y1 in lawns:
        body.box((x0, y0, PAD), (x1, y1, PAD + 0.008), GRASS)
    for x in (-0.95, 0.35):
        body.box((x - 0.01, -1.95, PAD), (x + 0.01, -1.6, PAD + 0.004), TRIM)  # the walk's kerbs
    if variant == "ruins":
        return
    shrubs = ((-2.7, -1.7), (-2.3, -1.75), (-1.9, -1.7), (-1.4, -1.7), (-2.75, -0.7), (-2.5, 1.78), (-0.5, 1.8), (1.5, 1.8), (2.6, 1.78))
    for x, y in shrubs:
        body.sphere((x, y, PAD + 0.07), 'Z', 0.1, SHRUB if variant == "intact" else DARK, sides=7, rings=4)
    # A bench by the lawn.
    body.box((-2.2, -1.3, PAD + 0.07), (-1.85, -1.22, PAD + 0.09), WOOD)
    body.box((-2.2, -1.22, PAD + 0.09), (-1.85, -1.2, PAD + 0.16), WOOD)
    for x in (-2.17, -1.88):
        body.box((x - 0.01, -1.3, PAD), (x + 0.01, -1.22, PAD + 0.07), METAL)


def _ambulance(p, variant):
    """A small van by the door, its back to the walk: white, a green stripe, a light bar on its cab; burnt out when abandoned."""
    x, y = 0.75, -1.6
    burnt = variant != "intact"
    body = DARK if burnt else WHITE
    p.box((x - 0.2, y - 0.09, PAD + 0.04), (x + 0.12, y + 0.09, PAD + 0.24), body)  # the box
    p.box((x + 0.12, y - 0.085, PAD + 0.04), (x + 0.24, y + 0.085, PAD + 0.17), body)  # the cab
    p.box((x + 0.18, y - 0.08, PAD + 0.12), (x + 0.245, y + 0.08, PAD + 0.165), DARK if burnt else GLASS)
    if not burnt:
        for sy in (-1, 1):
            p.box((x - 0.2, y + sy * 0.09 - 0.002, PAD + 0.12), (x + 0.24, y + sy * 0.09 + 0.002 * sy * 2, PAD + 0.15), GREEN)
        p.box((x + 0.15, y - 0.06, PAD + 0.17), (x + 0.21, y + 0.06, PAD + 0.19), RED)
    for wx in (x - 0.12, x + 0.15):
        for sy in (-1, 1):
            p.cylinder((wx, y + sy * 0.085, PAD + 0.04), 'Y', 0.04, 0.015, DARK, sides=8)


def _stump(p, rnd, wing, height):
    """What is left of a wing: its floor, jagged stumps of its walls and a heap inside."""
    x0, x1, y0, y1 = wing[:4]
    p.box((x0, y0, PAD), (x1, y1, PAD + 0.08), PLINTH)
    sides = [((x, y0 + 0.04), 0.0) for x in cc.steps(x0 + 0.1, x1 - 0.1, 0.24)]
    sides += [((x, y1 - 0.04), 0.0) for x in cc.steps(x0 + 0.1, x1 - 0.1, 0.24)]
    sides += [((x0 + 0.04, y), math.pi / 2) for y in cc.steps(y0 + 0.2, y1 - 0.2, 0.24)]
    sides += [((x1 - 0.04, y), math.pi / 2) for y in cc.steps(y0 + 0.2, y1 - 0.2, 0.24)]
    for (x, y), turn in sides:
        if rnd.random() < 0.3:
            continue
        h = rnd.uniform(0.05, height)
        cc.block(p, (x, y, PAD + 0.08 + h / 2), (0.24, 0.08, h), (rnd.uniform(-0.1, 0.1), rnd.uniform(-0.08, 0.08), turn), WALL)
        if h > 0.25:
            cc.block(p, (x, y, PAD + 0.08 + h * 0.6), (0.2, 0.09, 0.08), (0.0, 0.0, turn), GLASS)  # a window band left in it
    for k in range(int((x1 - x0) * (y1 - y0) * 2) + 1):
        c = (rnd.uniform(x0 + 0.2, x1 - 0.2), rnd.uniform(y0 + 0.2, y1 - 0.2), PAD + 0.12 + rnd.uniform(0.0, height * 0.4))
        cc.block(p, c, (rnd.uniform(0.25, 0.5), rnd.uniform(0.2, 0.4), 0.06), (rnd.uniform(-0.4, 0.4), rnd.uniform(-0.4, 0.4), rnd.uniform(0, 3)), WALL)


def _flag(team, variant):
    if variant == "ruins":
        return
    x, y, top = 2.82, -1.82, 1.6
    team.cylinder((x, y, (PAD + top) / 2), 'Z', 0.015, (top - PAD) / 2, WHITE, sides=6)
    if variant == "abandoned":
        cc.block(team, (x - 0.12, y, top - 0.12), (0.22, 0.01, 0.16), (0.0, 0.25, 0.0), TEAM)
    else:
        team.loft([[Vector((x - 0.02, y + d, top - 0.02)), Vector((x - 0.42, y + d + 0.03, top - 0.04)), Vector((x - 0.42, y + d + 0.03, top - 0.28)),
                    Vector((x - 0.02, y + d, top - 0.26))] for d in (-0.006, 0.006)], TEAM)


def _material(name, variant):
    """Light warm concrete walls and trim, a darker plinth, blue-grey glass, a dark roof, white, the green of the sign, red, yellow
    lights, grass, shrubs, paving in squares, the light team zone, grey metal, wood; soot when abandoned, in the ruins."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)  # short: a longer reach darkens the corners of the small panes and rounds them
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    x = g.axis(coord, 'X')
    y = g.axis(coord, 'Y')
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    stain = g.clamp01(g.math('MULTIPLY_ADD', g.noise(3.0, 3.0), 3.0, -1.7))
    streaks = g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.5, 2.0), 2.5, -1.2))
    wall = g.mix(grain, lin((0.66, 0.64, 0.58)), lin((0.72, 0.70, 0.64)))
    wall = g.mix(g.math('MULTIPLY', streaks, 0.35), wall, lin((0.48, 0.46, 0.42)))
    trim = g.mix(grain, lin((0.78, 0.77, 0.72)), lin((0.84, 0.83, 0.78)))
    plinth = g.mix(grain, lin((0.42, 0.40, 0.37)), lin((0.48, 0.46, 0.42)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY', g.math('FRACT', g.math('MULTIPLY', z, 4.17)), 1.2)), lin((0.10, 0.14, 0.18)),
                  lin((0.28, 0.38, 0.46)))
    roof = g.mix(grain, lin((0.16, 0.16, 0.16)), lin((0.22, 0.22, 0.21)))
    dark = g.mix(grain, lin((0.07, 0.07, 0.08)), lin((0.12, 0.12, 0.13)))
    white = g.mix(grain, lin((0.88, 0.88, 0.86)), lin((0.94, 0.94, 0.92)))
    green = g.mix(grain, lin((0.18, 0.66, 0.30)), lin((0.24, 0.74, 0.36)))
    red = lin((0.80, 0.12, 0.08))
    yellow = lin((0.95, 0.80, 0.25))
    grass = g.mix(g.noise(8.0, 4.0), lin((0.22, 0.36, 0.14)), lin((0.32, 0.46, 0.18)))
    shrub = g.mix(g.noise(20.0, 4.0), lin((0.12, 0.26, 0.10)), lin((0.22, 0.38, 0.14)))
    tiles = g.math('MAXIMUM', g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', x, 4.0)), -30.0, 1.5)),
                   g.clamp01(g.math('MULTIPLY_ADD', g.math('FRACT', g.math('MULTIPLY', y, 4.0)), -30.0, 1.5)))
    paving = g.mix(tiles, g.mix(grain, lin((0.56, 0.52, 0.46)), lin((0.62, 0.58, 0.52))), lin((0.40, 0.37, 0.33)))
    paving = g.mix(g.math('MULTIPLY', stain, 0.4), paving, lin((0.36, 0.33, 0.29)))
    team = g.mix(grain, lin((0.82, 0.82, 0.82)), lin((0.90, 0.90, 0.90)))
    metal = g.mix(grain, lin((0.50, 0.52, 0.54)), lin((0.60, 0.62, 0.64)))
    wood = g.mix(g.noise(5.0, 5.0), lin((0.42, 0.28, 0.16)), lin((0.52, 0.36, 0.22)))
    albedo = cc.paint(g, mask, (wall, trim, plinth, glass, roof, dark, white, green, red, yellow, grass, shrub, paving, team, metal, wood))
    if variant != "intact":
        soot = g.clamp01(g.math('MULTIPLY_ADD', g.noise(1.8, 2.0), 6.0, -2.9 if variant == "abandoned" else -2.4))
        albedo = g.mix(g.math('MULTIPLY', soot, 0.8), albedo, lin((0.07, 0.06, 0.06)))
    rough = cc.weighted(g, mask, (0.85, 0.8, 0.9, 0.1, 0.9, 0.6, 0.5, 0.5, 0.4, 0.3, 0.95, 0.95, 0.9, 0.45, 0.45, 0.8))
    metallic = cc.weighted(g, mask, (0.0, 0.0, 0.0, 0.0, 0.0, 0.2, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.0, 0.6, 0.0))
    normal = g.bump(g.math('MULTIPLY_ADD', mask[12], g.math('MULTIPLY', tiles, -1.0), g.noise(60.0, 3.0)), 0.04, 0.006)
    g.finish(albedo, normal, g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    variant = VARIANTS[variant_id]
    cc.clear(variant_id, ("body", "team_mask", "base"))
    body, team, base = Parts(), Parts(), Parts()
    rnd = random.Random(11)
    _plaza(base, body, variant)
    if variant == "ruins":
        for wing, height in ((TOWER, 0.7), (MAIN, 0.45), (RIGHT, 0.35), (ENTRANCE, 0.25)):
            _stump(body, rnd, wing, height)
        _ambulance(body, "abandoned")
    else:
        roof = _wing(body, TOWER, variant)
        _helipad(body, roof, variant)
        _porch(body, variant)
        roof = _wing(body, MAIN, variant)
        _roof_kit(body, roof, rnd, *MAIN[:4], 3)
        body.box((0.3, 1.2, roof - 0.005), (0.7, 1.55, roof + 0.22), WALL)  # its stair hut
        body.box((0.28, 1.18, roof + 0.22), (0.72, 1.57, roof + 0.25), TRIM)
        if variant == "abandoned":
            # The right wing's top storey fallen in at its front corner: three storeys there, slabs hanging over the gap.
            roof = _wing(body, RIGHT, variant)
            cc.block(body, (2.35, -1.05, roof - 0.12), (0.8, 0.5, 0.26), (0.0, 0.0, 0.0), DARK)
            cc.block(body, (2.3, -1.1, roof - 0.22), (0.7, 0.5, 0.05), (0.3, -0.25, 0.1), WALL)
            cc.block(body, (2.5, -1.45, PAD + 0.05), (0.4, 0.3, 0.08), (0.2, 0.1, 0.6), WALL)
        else:
            roof = _wing(body, RIGHT, variant)
        _roof_kit(body, roof, rnd, 1.0, 2.75, -0.6, 1.2, 2)
        body.turned((1.35, 0.85, roof), 'Z', [(0.0, 0.14), (0.2, 0.14), (0.24, 0.1), (0.25, 0.0)], METAL, sides=12)  # a water tank
        _sign(body, roof, variant)
        _entrance(body, team, variant)
        _ambulance(body, variant)
        _flag(team, variant)
    parts = [(body, "body"), (base, "base")] if variant == "ruins" else [(body, "body"), (team, "team_mask"), (base, "base")]
    meshes = [p.finish(f"{variant_id}_{name}") for p, name in parts]
    cc.drop_bottoms(meshes, PAD - 0.001)
    rc.select_only(meshes)
    bpy.ops.object.shade_smooth_by_angle(angle=math.radians(40))
    uv_together(meshes)
    cc.repack(meshes)
    mat, graph = _material(variant_id + "_mat", variant)
    for obj in meshes:
        obj.data.materials.append(mat)
    return meshes, mat, graph


def run(variant_id):
    """Builds, bakes, saves the .blend, exports the GLB with the nodes body, team_mask and base (fire_1..3 when abandoned)."""
    meshes, mat, graph = build(variant_id)
    variant = VARIANTS[variant_id]
    nodes = rc.fire_nodes(FIRE_POINTS) if variant == "abandoned" else []
    names = [o.name.split(".")[0][len(variant_id) + 1:] for o in meshes]
    base = os.path.join(rc.SRC_ROOT, "map", variant_id)
    report = [f"{variant_id}: triangles {sum(rc.tri_count(o) for o in meshes)}"]
    cc.repack(meshes, cc.ISLAND_GAP / SIZE)
    imgs = rc.bake_material(meshes, graph, SIZE, variant_id, os.path.join(base, "bake"))
    rc.wire_final(mat, imgs)
    objs = meshes + nodes
    rc.arrange(objs, variant_id)
    glb = os.path.join(rc.GLB_ROOT, "map", variant_id, variant_id + ".glb")
    with rc.node_names(objs, names + rc.fire_names(nodes)):
        rc.save_blend(objs, mat, imgs, os.path.join(base, variant_id + ".blend"))
        rc.export_glb(objs, glb)
    report.append(rc.glb_report(glb))
    return "\n".join(report)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
