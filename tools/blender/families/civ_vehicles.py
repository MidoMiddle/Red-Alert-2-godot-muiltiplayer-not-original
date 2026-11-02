"""The civilians' vehicles (docs/map_elements/13_civilians, 3): the car, the bus and the truck of the towns and villages.

Units of game/data/neutral/civilians.yaml: civ_car (its colours a plain sedan, a taxi, a police car or an ambulance), civ_bus, civ_truck.
They stand parked by the houses and the bus stops; a car blown up burns as a wreck (the client chars it, UnitView.SetWreck) and its
alarm blinks two headlights the client stands itself (Effects/CarAlarms) at the front, 0.14 up. Made at the size the game draws a
vehicle (the Grizzly tank is 0.8 long): the sedan 0.64 long, the truck 0.96, the bus 1.3. Origin at the centre of the body on the
ground, front to +Y in Blender (-Z in Godot after the Y-up export). The parts:
    body  the sedan: a low body of rounded sections, its bonnet and boot lower than the cabin, the glasshouse over it, the roof; bumpers,
          a dark grille, headlights and red tail lights; four wheels with grey hubs. The bus: a long box, a band of windows down both
          sides, the windscreen, a door on the right, a stripe. The truck: a cab with its windscreen, a white box behind it on a dark
          chassis, three axles.
Node: "body". No team colour (nobody owns them). One GLB per kind:
    civ_car_sedan      muted blue-grey
    civ_car_sedan_red  muted maroon
    civ_car_taxi       yellow, a sign on its roof
    civ_car_police     white with a blue band, a light bar of red and blue
    civ_car_ambulance  white with a red band, a red light bar
    civ_bus            cream, a muted red stripe, a light grey roof
    civ_truck          a muted green cab, a white box
No writing, no emblems (docs/15_legal.md). Budget 1500 triangles, texture 512.

In Blender, after tools/blender is on sys.path:
    import families.civ_vehicles as vehicles
    print(vehicles.run_all())
"""

from mathutils import Vector

from families import city_common as cc
from families import industrial_common as ic
from units.parts import Parts, lin
import rts_common as rc

FOLDER = "unit"
SIZE = 512

# kind, the paint, the accent (a band, a sign), the second accent (the light bar's other half).
VARIANTS = {
    "civ_car_sedan": ("sedan", (0.36, 0.42, 0.50), None, None),
    "civ_car_sedan_red": ("sedan", (0.50, 0.20, 0.20), None, None),
    "civ_car_taxi": ("taxi", (0.92, 0.72, 0.16), (0.95, 0.94, 0.88), None),
    "civ_car_police": ("police", (0.90, 0.90, 0.90), (0.16, 0.26, 0.62), (0.85, 0.12, 0.10)),
    "civ_car_ambulance": ("ambulance", (0.92, 0.92, 0.90), (0.80, 0.14, 0.12), (0.85, 0.12, 0.10)),
    "civ_bus": ("bus", (0.86, 0.78, 0.56), (0.62, 0.22, 0.18), None),
    "civ_truck": ("truck", (0.30, 0.44, 0.34), (0.88, 0.88, 0.86), None),
}

PAINT, GLASS, TYRE, TRIM, LIGHT, TAIL, ACCENT, ACCENT2, DARK = (cc.part(i) for i in range(9))
PARTS = 9


def _section(y, w, zb, zt, c):
    """A rounded section across the body at y: half-width w, from zb to zt, its corners cut by c."""
    return [Vector(q) for q in ((-w, y, zb + c), (-w + c, y, zb), (w - c, y, zb), (w, y, zb + c),
                                (w, y, zt - c), (w - c, y, zt), (-w + c, y, zt), (-w, y, zt - c))]


def _wheels(p, axles, track, radius, width):
    for y in axles:
        for x in (-track, track):
            p.cylinder((x, y, radius), 'X', radius, width, TYRE, sides=10)
            out = 1 if x > 0 else -1
            p.cylinder((x + out * width, y, radius), 'X', radius * 0.55, 0.004, TRIM, sides=8)  # the hub


def _lamps(p, front, back, w, z):
    for x in (-w, w):
        p.box((x - 0.035, front - 0.004, z - 0.012), (x + 0.035, front + 0.006, z + 0.012), LIGHT)
        p.box((x - 0.03, back - 0.006, z - 0.01), (x + 0.03, back + 0.004, z + 0.012), TAIL)


def _sedan(p, kind):
    half, front, back = 0.15, 0.32, -0.32
    stations = ((front, 0.135, 0.05, 0.11), (front - 0.03, 0.148, 0.04, 0.15), (0.12, half, 0.04, 0.165), (-0.2, half, 0.04, 0.17),
                (back + 0.03, 0.148, 0.04, 0.165), (back, 0.135, 0.05, 0.12))
    p.loft([_section(y, w, zb, zt, 0.025) for y, w, zb, zt in stations], PAINT)
    # The glasshouse: a trapezoid swept from the windscreen's foot over the roof to the back window, the roof on it.
    def trap(y, w0, w1, z0, z1):
        return [Vector((-w0, y, z0)), Vector((w0, y, z0)), Vector((w1, y, z1)), Vector((-w1, y, z1))]
    p.loft([trap(0.13, 0.135, 0.13, 0.164, 0.17), trap(0.03, 0.135, 0.112, 0.164, 0.262), trap(-0.13, 0.135, 0.112, 0.164, 0.262),
            trap(-0.24, 0.135, 0.13, 0.164, 0.172)], GLASS)
    p.box((-0.114, -0.135, 0.258), (0.114, 0.035, 0.272), PAINT)
    for y in (-0.05,):  # the middle pillars, leaning in with the side windows
        for out in (-1, 1):
            p.bar((out * 0.134, y, 0.166), (out * 0.112, y, 0.262), 0.007, PAINT)
    p.box((-0.15, front - 0.012, 0.035), (0.15, front + 0.012, 0.075), TRIM)  # the bumpers
    p.box((-0.15, back - 0.012, 0.035), (0.15, back + 0.012, 0.075), TRIM)
    p.box((-0.06, front - 0.002, 0.085), (0.06, front + 0.004, 0.125), DARK)  # the grille
    _lamps(p, front - 0.004, back, 0.1, 0.13)
    _wheels(p, (0.2, -0.2), 0.128, 0.06, 0.024)
    if kind == "taxi":
        p.box((-0.05, -0.07, 0.272), (0.05, -0.03, 0.31), ACCENT)
    elif kind in ("police", "ambulance"):
        p.box((-0.07, -0.06, 0.272), (0.0, -0.03, 0.296), ACCENT2)
        p.box((0.0, -0.06, 0.272), (0.07, -0.03, 0.296), ACCENT if kind == "police" else ACCENT2)
        for x in (-0.152, 0.152):  # the band down its sides
            p.box((x - 0.004, -0.29, 0.1), (x + 0.004, 0.29, 0.13), ACCENT)


def _bus(p):
    half, front, back = 0.21, 0.65, -0.65
    stations = ((front, 0.2, 0.07, 0.4), (front - 0.04, half, 0.06, 0.44), (back + 0.03, half, 0.06, 0.44), (back, 0.2, 0.07, 0.42))
    p.loft([_section(y, w, zb, zt, 0.03) for y, w, zb, zt in stations], PAINT)
    p.box((-0.19, -0.6, 0.44), (0.19, 0.58, 0.455), TRIM)  # the roof
    for x in (-half, half):
        out = 1 if x > 0 else -1
        p.box((x - 0.004 * (1 - out), -0.56, 0.25), (x + 0.004 * (1 + out), 0.52, 0.39), GLASS)  # the windows
        p.box((x - 0.004 * (1 - out), -0.62, 0.15), (x + 0.004 * (1 + out), 0.62, 0.19), ACCENT)  # the stripe
    p.box((half - 0.002, 0.42, 0.08), (half + 0.008, 0.56, 0.38), DARK)  # the door
    p.box((-0.18, front - 0.002, 0.22), (0.18, front + 0.008, 0.4), GLASS)  # the windscreen
    p.box((-0.2, front - 0.01, 0.05), (0.2, front + 0.016, 0.1), TRIM)
    p.box((-0.2, back - 0.016, 0.05), (0.2, back + 0.01, 0.1), TRIM)
    _lamps(p, front + 0.004, back, 0.14, 0.14)
    _wheels(p, (0.42, -0.4), 0.185, 0.078, 0.03)


def _truck(p):
    front, back = 0.48, -0.48
    cab = ((front, 0.155, 0.08, 0.3), (front - 0.03, 0.165, 0.07, 0.36), (0.22, 0.165, 0.07, 0.37))
    p.loft([_section(y, w, zb, zt, 0.025) for y, w, zb, zt in cab], PAINT)
    p.box((-0.14, front - 0.022, 0.22), (0.14, front - 0.012, 0.34), GLASS)  # the windscreen, on the sloped face
    for x in (-0.166, 0.166):
        out = 1 if x > 0 else -1
        p.box((x - 0.003 * (1 - out), 0.28, 0.23), (x + 0.003 * (1 + out), 0.42, 0.33), GLASS)  # the side windows
    p.box((-0.12, -0.46, 0.06), (0.12, 0.3, 0.1), DARK)  # the chassis
    p.box((-0.175, back, 0.1), (0.175, 0.2, 0.46), ACCENT)  # the box
    p.box((-0.168, front - 0.002, 0.08), (0.168, front + 0.016, 0.12), TRIM)
    p.box((-0.07, front - 0.003, 0.13), (0.07, front + 0.004, 0.2), DARK)  # the grille
    _lamps(p, front + 0.006, back, 0.12, 0.13)
    _wheels(p, (0.34, -0.2, -0.34), 0.15, 0.07, 0.028)


def _material(name, paint, accent, accent2):
    mat = rc.new_material(name)
    g = rc.Graph(mat)
    ao = g.ao(0.05)
    mask = cc.part_masks(g, PARTS)
    up = g.axis(g.normal_world(), 'Z')
    grain = g.noise(40.0, 3.0)
    body = g.mix(g.math('MULTIPLY', g.noise(6.0, 2.0), 0.3), lin(paint), lin(tuple(c * 0.85 for c in paint)))
    sky = g.clamp01(g.math('MULTIPLY_ADD', up, 0.6, 0.4))  # the glass catches the sky on its faces that look up
    glass = g.mix(sky, lin((0.16, 0.20, 0.24)), lin((0.42, 0.52, 0.60)))
    tyre = lin((0.08, 0.08, 0.08))
    trim = g.mix(grain, lin((0.46, 0.47, 0.48)), lin((0.56, 0.57, 0.58)))
    light = lin((0.96, 0.94, 0.80))
    tail = lin((0.70, 0.08, 0.06))
    acc = lin(accent or paint)
    acc2 = lin(accent2 or accent or paint)
    dark = lin((0.10, 0.10, 0.11))
    albedo = cc.paint(g, mask, (body, glass, tyre, trim, light, tail, acc, acc2, dark))
    rough = cc.weighted(g, mask, (0.35, 0.1, 0.9, 0.4, 0.2, 0.3, 0.4, 0.3, 0.7))
    metallic = cc.weighted(g, mask, (0.2, 0.0, 0.0, 0.6, 0.0, 0.0, 0.1, 0.0, 0.0))
    g.finish(albedo, g.bump(grain, 0.05, 0.003), g.combine(ao, rough, metallic))
    return mat, g


def build(variant_id):
    kind, paint, accent, accent2 = VARIANTS[variant_id]
    ic.clear(variant_id, ("body",))
    body = Parts()
    {"bus": _bus, "truck": _truck}.get(kind, lambda p: _sedan(p, kind))(body)
    return cc.finish(variant_id, [(body, "body")], -1.0, _material(variant_id + "_mat", paint, accent, accent2))


def run(variant_id):
    meshes, mat, graph = build(variant_id)
    return ic.run(variant_id, meshes, mat, graph, SIZE, folder=FOLDER)


def run_all():
    return "\n".join(run(variant_id) for variant_id in VARIANTS)
