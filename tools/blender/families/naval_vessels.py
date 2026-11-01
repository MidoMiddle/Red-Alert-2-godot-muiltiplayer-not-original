"""Civilian vessels, element 08 of the naval set (docs/naval_elements/08_civilian_vessels): a ferry, a cargo ship and a fishing boat; neutral,
they stand in the way of the fleet. No writing, no flags, nothing military.

Made at the size the game draws it, against the tank (a cell is about five metres), the bow to +x; origin at the centre of the footprint
on the water's surface (what lies below 0 is hidden by the water, the hulls go under it to their keels):
    ferry       footprint 2x1: a white hull 1.9 long and 0.6 wide with a blue stripe and a dark red bottom at the waterline, a raised
                bow ramp; two decks of white cabins with bands of windows, the bridge forward on the upper one, two orange lifeboats, a
                railing round the upper deck; the funnel white with a blue band and a black top, to 0.95
    cargo ship  footprint 4x1: a dark red hull 3.8 long and 0.76 wide, black above the waterline's band, a raised forecastle with a
                mast; grey hatch covers along the deck with containers on two of them, two yellow derrick cranes; the white
                superstructure at the stern in three tiers with the bridge and its wings on top, the funnel grey with a red band and a
                black top, to 1.1
    fishing     footprint 1x1, the beached boat's hull afloat (docs/beach_elements/17_beached_boat): a blue wooden hull 0.9 long with a
    boat        light wood rail, a small white wheelhouse at the stern with a stub of an exhaust, a mast with a boom forward, a pile of
                nets and orange floats
Nodes: "hull" (and all on the deck), "cabin" (the superstructure), "funnel"; "smoke_anchor" at the funnel's top while whole; fire_1, fire_2
when damaged. One GLB per vessel and state:
    civ_ferry, civ_ferry_damaged, civ_cargo_ship, civ_cargo_ship_damaged, civ_fishing_boat, civ_fishing_boat_damaged
    damaged: holes in the hull at the waterline and in the cabins, half the windows out, the railing bent, soot; burning (the boat's
    wood more). Sinking is the game's: it lists and lowers the damaged one (appearance.md, section 4).
Budget 8000 triangles for the ferry and the cargo ship, 1500 for the boat; texture 2048, the boat 1024 (prompts.md).

In Blender, after tools/blender is on sys.path:
    import families.naval_vessels as nv
    print(nv.run_all())
"""

import math

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from families.naval_reef import FOLDER
from units.parts import Parts, lin
import rts_common as rc

VARIANTS = {
    "civ_ferry": ("ferry", False),
    "civ_ferry_damaged": ("ferry", True),
    "civ_cargo_ship": ("cargo", False),
    "civ_cargo_ship_damaged": ("cargo", True),
    "civ_fishing_boat": ("boat", False),
    "civ_fishing_boat_damaged": ("boat", True),
}
SIZES = {"ferry": 2048, "cargo": 2048, "boat": 1024}

(WHITE_HULL, RED_HULL, BLUE_HULL, DECK, WOOD, WHITE, GLASS, DARK, ORANGE, METAL, YELLOW, GREEN, NET, RED, BLUE) = (cc.part(i) for i in range(15))
PARTS = 15


# --- shapes ---------------------------------------------------------------------------

def hull(p, length, beam, freeboard, depth, bow, painted, deck=DECK, transom=0.85, sheer=0.06):
    """A hull along x, the bow at +x: stations of a cross-section narrowing to a point over the last bow of its length, its deck
    rising a little towards the bow, a flat transom at the stern; the deck's faces painted deck."""
    n = 12
    rings = []
    for k in range(n + 1):
        t = k / n
        x = -length / 2 + length * t
        u = max(0.0, (x - (length / 2 - bow)) / bow)
        b = beam / 2 * (transom + (1 - transom) * min(1.0, t * 6)) * (1 - u ** 1.7) + 0.005
        top = freeboard + sheer * u ** 2
        d = depth * (1 - 0.6 * u)
        rings.append([Vector((x, y, z)) for y, z in ((b, top), (b * 0.97, 0.0), (b * 0.7, -d * 0.8), (0.0, -d), (-b * 0.7, -d * 0.8),
                                                    (-b * 0.97, 0.0), (-b, top))])
    faces = cc.made(p, lambda: p.loft(rings, painted))
    cc.repaint(p, [f for f in faces if abs(f.normal.z) > 0.8 and f.calc_center_median().z > 0.0], deck)


def windows(p, x0, x1, y, z0, z1, count, broken=False, rnd=None, side=1):
    """A band of windows on a cabin's side at y (side: which way it faces), some out when broken."""
    w = (x1 - x0) / count
    for k in range(count):
        a, b = x0 + w * k + w * 0.15, x0 + w * (k + 1) - w * 0.15
        out = broken and rnd is not None and rnd.random() < 0.5
        p.box((a, y - 0.006 if side > 0 else y - 0.002, z0), (b, y + 0.002 if side > 0 else y + 0.006, z1), DARK if out else GLASS)


def cabin(p, x0, x1, half, z0, z1, rows, painted=WHITE, broken=False, rnd=None):
    """A cabin box with bands of windows on both sides and the front (+x), a roof a little wider."""
    p.box((x0, -half, z0), (x1, half, z1), painted)
    for y, side in ((half, 1), (-half, -1)):
        for r in range(rows):
            h = (z1 - z0) / rows
            windows(p, x0 + 0.03, x1 - 0.03, y, z0 + h * r + h * 0.35, z0 + h * r + h * 0.75, max(2, int((x1 - x0) / 0.07)), broken, rnd, side)
    p.box((x1, -half + 0.03, z1 - (z1 - z0) * 0.55), (x1 + 0.006, half - 0.03, z1 - (z1 - z0) * 0.15), GLASS)
    p.box((x0 - 0.01, -half - 0.01, z1), (x1 + 0.01, half + 0.01, z1 + 0.015), painted)


def railing(p, x0, x1, half, z, bent=False):
    for y in (-half, half):
        for x in cc.steps(x0, x1, 0.09):
            p.bar((x, y, z), (x, y, z + 0.05), 0.003, WHITE)
        end = Vector((x1, y, z + 0.05))
        mid = Vector(((x0 + x1) / 2, y + (0.04 if bent and y > 0 else 0.0), z + (0.02 if bent and y > 0 else 0.05)))
        p.bar((x0, y, z + 0.05), mid, 0.003, WHITE)
        p.bar(mid, end, 0.003, WHITE)


def funnel(f, x, y, z0, z1, rx, ry, band, painted=WHITE):
    """An oval funnel from z0 to z1, a band of colour near its top and a black top."""
    for a, b, part in ((z0, z1 - 0.1, painted), (z1 - 0.1, z1 - 0.05, band), (z1 - 0.05, z1, DARK)):
        f.loft([[Vector((x + rx * math.cos(2 * math.pi * k / 12), y + ry * math.sin(2 * math.pi * k / 12), z)) for k in range(12)]
                for z in (a, b)], part)


def holes(p, rnd, spots, half):
    """Holes in a hull's sides at the waterline: dark, their edges torn."""
    for x, side in spots:
        y = side * (half + 0.004)
        p.box((x - 0.06, y - 0.004, 0.0), (x + 0.06, y + 0.004, 0.07), DARK)
        for k in range(3):
            cc.block(p, (x + rnd.uniform(-0.06, 0.06), y + side * 0.01, rnd.uniform(0.0, 0.08)), (0.03, 0.01, 0.03),
                     (rnd.uniform(-0.5, 0.5), 0.0, rnd.uniform(0, math.pi)), DARK)


# --- the vessels ----------------------------------------------------------------------

def _ferry(hl, cb, fn, damaged, rnd):
    hull(hl, 1.9, 0.6, 0.18, 0.14, 0.45, WHITE_HULL)
    # The bow ramp, raised, and the car deck's rails.
    hl.box((0.62, -0.16, 0.18), (0.66, 0.16, 0.36), DECK)
    hl.bar((0.62, -0.17, 0.36), (0.55, -0.17, 0.18), 0.008, METAL)
    hl.bar((0.62, 0.17, 0.36), (0.55, 0.17, 0.18), 0.008, METAL)
    # Two decks of cabins; the bridge forward on the upper one.
    cabin(cb, -0.78, 0.45, 0.27, 0.18, 0.42, 1, broken=damaged, rnd=rnd)
    cabin(cb, -0.55, 0.22, 0.23, 0.435, 0.6, 1, broken=damaged, rnd=rnd)
    cb.box((0.06, -0.26, 0.6), (0.24, 0.26, 0.7), WHITE)  # the bridge with its wings
    cb.box((0.24, -0.24, 0.62), (0.246, 0.24, 0.68), DARK if damaged else GLASS)
    cb.box((0.05, -0.27, 0.7), (0.25, 0.27, 0.715), WHITE)
    railing(cb, -0.78, 0.45, 0.27, 0.435, bent=damaged)
    # The lifeboats on the upper deck's sides.
    for y in (-0.25, 0.25):
        if damaged and y > 0:
            continue
        cb.turned((-0.35, y, 0.475), 'X', [(-0.11, 0.0), (-0.09, 0.03), (0.09, 0.032), (0.11, 0.0)], ORANGE, sides=8)
    cb.bar((0.15, 0.0, 0.715), (0.15, 0.0, 0.88), 0.006, METAL)  # the mast over the bridge
    cb.sphere((0.15, 0.0, 0.89), 'Z', 0.012, DARK if damaged else WHITE, sides=6, rings=3)
    funnel(fn, -0.4, 0.0, 0.6, 0.95, 0.07, 0.05, BLUE)
    if damaged:
        holes(hl, rnd, ((0.2, 1), (-0.5, -1)), 0.3)


def _cargo(hl, cb, fn, damaged, rnd):
    hull(hl, 3.8, 0.76, 0.24, 0.2, 0.7, RED_HULL)
    # The forecastle, raised at the bow, a mast on it.
    hl.box((1.35, -0.3, 0.24), (1.62, 0.3, 0.32), RED_HULL)
    hl.bar((1.5, 0.0, 0.32), (1.5, 0.0, 0.8), 0.01, METAL)
    hl.bar((1.5, -0.1, 0.65), (1.5, 0.1, 0.65), 0.006, METAL)
    # Hatch covers along the deck, containers on two of them.
    for k, x in enumerate((-0.95, -0.35, 0.25, 0.85)):
        hl.box((x - 0.24, -0.28, 0.24), (x + 0.24, 0.28, 0.33), METAL)
        if k in (1, 2):
            colours = ((RED, BLUE), (GREEN, ORANGE))[k - 1]
            for row, y in enumerate((-0.13, 0.13)):
                hl.box((x - 0.23, y - 0.12, 0.33), (x + 0.23, y + 0.12, 0.45), colours[row])
                if (k + row) % 2 == 0:
                    hl.box((x - 0.23, y - 0.12, 0.45), (x + 0.23, y + 0.12, 0.57), colours[1 - row])
    # Two derrick cranes between the hatches.
    for x in (-0.65, 0.55):
        hl.box((x - 0.04, -0.04, 0.24), (x + 0.04, 0.04, 0.82), YELLOW)
        hl.bar((x, 0.0, 0.35), (x + 0.42, 0.18 if x < 0 else -0.18, 0.62), 0.012, YELLOW)
        hl.bar((x, 0.0, 0.82), (x + 0.42, 0.18 if x < 0 else -0.18, 0.62), 0.003, DARK)
    # The superstructure at the stern: three tiers, the bridge and its wings on top.
    cabin(cb, -1.8, -1.22, 0.33, 0.24, 0.48, 1, broken=damaged, rnd=rnd)
    cabin(cb, -1.76, -1.26, 0.3, 0.495, 0.68, 1, broken=damaged, rnd=rnd)
    cabin(cb, -1.7, -1.3, 0.27, 0.695, 0.84, 1, broken=damaged, rnd=rnd)
    cb.box((-1.42, -0.38, 0.78), (-1.3, 0.38, 0.82), WHITE)  # the bridge wings
    cb.bar((-1.5, 0.0, 0.855), (-1.5, 0.0, 1.05), 0.007, METAL)
    funnel(fn, -1.6, 0.0, 0.855, 1.1, 0.08, 0.06, RED, painted=METAL)
    if damaged:
        holes(hl, rnd, ((0.4, 1), (-0.6, -1), (1.1, 1)), 0.38)


def _boat(hl, cb, fn, damaged, rnd):
    hull(hl, 0.9, 0.34, 0.1, 0.08, 0.35, BLUE_HULL, deck=WOOD, transom=0.8, sheer=0.05)
    for y in (-0.165, 0.165):  # the light wood rail
        hl.bar((-0.43, y, 0.105), (0.25, y * 0.75, 0.12), 0.008, WOOD)
    # The wheelhouse at the stern, its exhaust; the mast and boom forward; nets and floats.
    cb.box((-0.36, -0.11, 0.1), (-0.12, 0.11, 0.3), WHITE)
    cb.box((-0.12, -0.09, 0.2), (-0.114, 0.09, 0.27), DARK if damaged else GLASS)
    for y in (-0.11, 0.11):
        cb.box((-0.3, y - 0.004 if y > 0 else y - 0.002, 0.2), (-0.18, y + 0.002 if y > 0 else y + 0.004, 0.27), DARK if damaged else GLASS)
    cb.box((-0.38, -0.13, 0.3), (-0.1, 0.13, 0.32), BLUE_HULL)
    fn.cylinder((-0.3, 0.06, 0.38), 'Z', 0.015, 0.06, DARK, sides=8)
    lean = 0.15 if damaged else 0.0
    hl.bar((0.1, 0.0, 0.1), (0.1 + lean, 0.0, 0.62), 0.01, WOOD)
    hl.bar((0.1, 0.0, 0.28), (-0.06, 0.0, 0.42), 0.006, WOOD)  # the boom
    hl.bar((0.1 + lean, 0.0, 0.62), (0.35, 0.0, 0.11), 0.002, DARK)  # the stay
    hl.loft([[Vector((0.02 + dx * 0.07, dy * 0.08, 0.1)) for dx, dy in ((1, 1), (-1, 1), (-1, -1), (1, -1))],
             [Vector((0.02 + dx * 0.05, dy * 0.06, 0.15)) for dx, dy in ((1, 1), (-1, 1), (-1, -1), (1, -1))]], NET)
    for x, y in ((0.05, 0.08), (-0.02, -0.07), (0.1, -0.05)):
        hl.sphere((x, y, 0.155), 'Z', 0.02, ORANGE, sides=6, rings=3)
    if damaged:
        holes(hl, rnd, ((0.15, 1),), 0.16)


BUILDERS = {"ferry": _ferry, "cargo": _cargo, "boat": _boat}
FUNNEL_TOPS = {"ferry": (-0.4, 0.0, 0.95), "cargo": (-1.6, 0.0, 1.1), "boat": (-0.3, 0.06, 0.44)}
FIRE_POINTS = {"ferry": ((-0.1, 0.0, 0.45), (0.3, -0.15, 0.2)), "cargo": ((-1.5, 0.0, 0.7), (0.4, 0.1, 0.35)),
               "boat": ((-0.2, 0.0, 0.32), (0.1, 0.0, 0.12))}


# --- material -------------------------------------------------------------------------

def _material(name, damaged):
    """White, dark red and blue hulls, dark red-brown under the waterline's band, black above it on the red hull; grey decks, light wood,
    white cabins, dark blue glass, dark iron, orange, grey steel, safety yellow, the containers' green, red and blue, brown nets; a blue
    stripe along the white hull; soot when damaged, the wood charred."""
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.06)
    mask = cc.part_masks(g, PARTS)
    coord = g.object_coord()
    z = g.axis(coord, 'Z')
    grain = g.noise(30.0, 4.0)
    bottom = g.clamp01(g.math('MULTIPLY_ADD', z, -60.0, 1.6))  # 1 under 0.01, 0 over 0.027
    under = lin((0.36, 0.12, 0.08))
    white_hull = g.mix(grain, lin((0.86, 0.87, 0.86)), lin((0.93, 0.93, 0.92)))
    white_hull = g.mix(cc.band(g, z, 0.075, 0.15), white_hull, lin((0.12, 0.28, 0.58)))
    red_hull = g.mix(grain, lin((0.46, 0.12, 0.09)), lin((0.54, 0.16, 0.11)))
    red_hull = g.mix(cc.band(g, z, 0.13, 0.4), red_hull, lin((0.10, 0.10, 0.11)))
    blue_hull = g.mix(grain, lin((0.14, 0.34, 0.58)), lin((0.18, 0.40, 0.64)))
    hulls = [g.mix(bottom, h, under) for h in (white_hull, red_hull, blue_hull)]
    deck = g.mix(grain, lin((0.40, 0.42, 0.42)), lin((0.48, 0.50, 0.50)))
    wood = g.mix(g.noise(6.0, 5.0), lin((0.60, 0.46, 0.28)), lin((0.70, 0.56, 0.36)))
    white = g.mix(grain, lin((0.88, 0.88, 0.87)), lin((0.94, 0.94, 0.93)))
    glass = g.mix(g.clamp01(g.math('MULTIPLY_ADD', z, 4.0, -1.0)), lin((0.10, 0.16, 0.22)), lin((0.30, 0.42, 0.50)))
    dark = g.mix(grain, lin((0.06, 0.06, 0.07)), lin((0.12, 0.12, 0.13)))
    orange = lin((0.96, 0.46, 0.08))
    metal = g.mix(grain, lin((0.44, 0.46, 0.48)), lin((0.54, 0.56, 0.58)))
    yellow = g.mix(grain, lin((0.86, 0.66, 0.10)), lin((0.92, 0.74, 0.16)))
    green = g.mix(grain, lin((0.20, 0.42, 0.26)), lin((0.24, 0.48, 0.30)))
    net = g.mix(g.noise(40.0, 2.0), lin((0.30, 0.24, 0.16)), lin((0.42, 0.34, 0.22)))
    red = g.mix(grain, lin((0.62, 0.14, 0.10)), lin((0.70, 0.18, 0.12)))
    blue = g.mix(grain, lin((0.14, 0.30, 0.56)), lin((0.18, 0.36, 0.62)))
    albedo = cc.paint(g, mask, hulls + [deck, wood, white, glass, dark, orange, metal, yellow, green, net, red, blue])
    if damaged:
        albedo = g.mix(g.math('MULTIPLY', ic.soot(g, 0.5), 0.85), albedo, lin((0.06, 0.05, 0.05)))
        albedo = g.mix(g.math('MULTIPLY', mask[4], 0.6), albedo, lin((0.12, 0.09, 0.07)))  # the wood charred
    rough = cc.weighted(g, mask, (0.45, 0.5, 0.55, 0.8, 0.85, 0.5, 0.1, 0.6, 0.6, 0.5, 0.5, 0.6, 0.9, 0.6, 0.6))
    metallic = cc.weighted(g, mask, (0.2, 0.3, 0.1, 0.3, 0.0, 0.1, 0.0, 0.5, 0.0, 0.6, 0.3, 0.3, 0.0, 0.3, 0.3))
    g.finish(albedo, g.bump(g.noise(60.0, 3.0), 0.03, 0.004), g.combine(ao, rough, metallic))
    return mat, g


# --- build ----------------------------------------------------------------------------

def build(variant_id):
    kind, damaged = VARIANTS[variant_id]
    ic.clear(variant_id, ("hull", "cabin", "funnel"))
    rnd = ic.seed(variant_id)
    hl, cb, fn = Parts(), Parts(), Parts()
    BUILDERS[kind](hl, cb, fn, damaged, rnd)
    return cc.finish(variant_id, [(hl, "hull"), (cb, "cabin"), (fn, "funnel")], -1.0, _material(variant_id + "_mat", damaged))


def run(variant_id):
    """Builds, bakes, saves the .blend and exports the GLB: the smoke's anchor at the funnel's top while whole, the fire nodes when
    damaged."""
    kind, damaged = VARIANTS[variant_id]
    meshes, mat, graph = build(variant_id)
    anchors = () if damaged else (("smoke_anchor", FUNNEL_TOPS[kind]),)
    return ic.run(variant_id, meshes, mat, graph, SIZES[kind], FIRE_POINTS[kind] if damaged else (), anchors, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
