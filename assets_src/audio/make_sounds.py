"""The first sound set of the game (docs/implementation/M6_menus_skirmish.md, 4.17, step 14), synthesized.

Stdlib only, fixed seeds: running it again writes the same bytes. Every sound is made here from noise, sines and envelopes, in the
spirit of sfxr: the project's own work, no samples. They are what sounds.yaml asks for — shots, impacts, explosions, deaths and
the two of the interface — and stand until recorded or licensed sounds replace them under the same names. The weapons by kind
(pistol, rifle, tank gun) and the helicopter rotor go further: two-pole filters, soft clipping, struck metal and echo; the rotor
is a seamless loop.

    python3 assets_src/audio/make_sounds.py            # writes game/assets/audio/{sfx,ui}/*.wav
"""
import math
import os
import random
import struct
import wave

RATE = 44100
ROOT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "game", "assets", "audio")


def lowpass(samples, cutoff):
    """One-pole low-pass at cutoff Hz."""
    alpha = 1.0 - math.exp(-2.0 * math.pi * cutoff / RATE)
    out, y = [], 0.0
    for x in samples:
        y += alpha * (x - y)
        out.append(y)
    return out


def highpass(samples, cutoff):
    low = lowpass(samples, cutoff)
    return [x - l for x, l in zip(samples, low)]


def biquad(samples, kind, hz, q=0.707):
    """A two-pole filter ("low", "high" or "band", the band's peak at 0 dB), after the RBJ cookbook: far steeper than the one-pole
    filters above, so a crack is bright without hiss and a boom is low without mud."""
    w0 = 2.0 * math.pi * hz / RATE
    alpha, cos = math.sin(w0) / (2.0 * q), math.cos(w0)
    if kind == "low":
        b = ((1.0 - cos) / 2.0, 1.0 - cos, (1.0 - cos) / 2.0)
    elif kind == "high":
        b = ((1.0 + cos) / 2.0, -(1.0 + cos), (1.0 + cos) / 2.0)
    else:
        b = (alpha, 0.0, -alpha)
    a0, a1, a2 = 1.0 + alpha, -2.0 * cos, 1.0 - alpha
    b0, b1, b2 = (x / a0 for x in b)
    a1, a2 = a1 / a0, a2 / a0
    out, x1, x2, y1, y2 = [], 0.0, 0.0, 0.0, 0.0
    for x in samples:
        y = b0 * x + b1 * x1 + b2 * x2 - a1 * y1 - a2 * y2
        x2, x1, y2, y1 = x1, x, y1, y
        out.append(y)
    return out


def noise(seconds, rnd):
    return [rnd.uniform(-1.0, 1.0) for _ in range(int(seconds * RATE))]


def envelope(samples, attack, decay_rate):
    """A fast rise over `attack` seconds, then exp(-t * decay_rate)."""
    out = []
    for i, x in enumerate(samples):
        t = i / RATE
        rise = min(1.0, t / attack) if attack > 0 else 1.0
        out.append(x * rise * math.exp(-t * decay_rate))
    return out


def tone(seconds, start_hz, end_hz, decay_rate, shape="sine"):
    """A tone gliding from start_hz to end_hz, decaying."""
    out, phase = [], 0.0
    count = int(seconds * RATE)
    for i in range(count):
        t = i / RATE
        hz = start_hz + (end_hz - start_hz) * (i / max(1, count - 1))
        phase += 2.0 * math.pi * hz / RATE
        value = math.sin(phase) if shape == "sine" else (1.0 if math.sin(phase) >= 0 else -1.0)
        out.append(value * math.exp(-t * decay_rate))
    return out


def mix(*layers):
    length = max(len(layer) for layer, _ in layers)
    out = [0.0] * length
    for layer, gain in layers:
        for i, x in enumerate(layer):
            out[i] += x * gain
    return out


def delay(samples, seconds):
    return [0.0] * int(seconds * RATE) + samples


def pad(samples, seconds):
    """Silence after the sound, for its echo to ring into."""
    return samples + [0.0] * int(seconds * RATE)


def saturate(samples, drive):
    """Soft clipping (tanh): the peaks are pressed down and the body comes up, which makes a blow dense and loud."""
    peak = max(abs(x) for x in samples) or 1.0
    return [math.tanh(drive * x / peak) / math.tanh(drive) for x in samples]


def metal(seconds, partials):
    """Struck metal: decaying sines at inharmonic ratios, (Hz, gain, decay rate) each."""
    return mix(*((tone(seconds, hz, hz, decay_rate), gain) for hz, gain, decay_rate in partials))


def echo(samples, taps):
    """The ground and the hills answering: copies of the sound arriving later, each quieter and duller, (delay s, gain, cutoff Hz)
    each."""
    layers = [(samples, 1.0)]
    for seconds, gain, cutoff in taps:
        layers.append((delay(biquad(samples, "low", cutoff), seconds), gain))
    return mix(*layers)


def loopable(samples, count):
    """Folds what is past `count` back over the start with an equal-power crossfade (for noise, which is uncorrelated with itself):
    the end runs on into the start without a seam."""
    fade = len(samples) - count
    out = samples[:count]
    for i in range(fade):
        angle = (i / fade) * math.pi / 2.0
        out[i] = samples[i] * math.sin(angle) + samples[count + i] * math.cos(angle)
    return out


def trim(samples, floor_db=-60.0):
    """Cuts the silence at the end: everything after the last sample above floor_db of the peak, plus 50 ms."""
    floor = (max(abs(x) for x in samples) or 1.0) * 10 ** (floor_db / 20.0)
    last = max((i for i, x in enumerate(samples) if abs(x) > floor), default=len(samples) - 1)
    return samples[:last + int(0.05 * RATE)]


def normalize(samples, peak_db=-1.0, fade_out=True):
    peak = max(abs(x) for x in samples) or 1.0
    gain = 10 ** (peak_db / 20.0) / peak
    out = [x * gain for x in samples]
    # A short fade at the end: no click when it stops. A loop has none, its end runs on into its start.
    fade = int(0.01 * RATE) if fade_out else 0
    for i in range(1, min(fade, len(out)) + 1):
        out[-i] *= i / fade
    return out


def write(relative, samples, loop=False):
    path = os.path.join(ROOT, relative)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, x)) * 32767)) for x in normalize(samples, fade_out=not loop))
    with wave.open(path, "wb") as out:
        out.setnchannels(1)
        out.setsampwidth(2)
        out.setframerate(RATE)
        out.writeframes(frames)
    print(f"{relative}: {len(samples) / RATE:.2f} s")


def shot(seed, crack_hz, thump_hz, length):
    """A gunshot: a bright crack of noise over a short low thump."""
    rnd = random.Random(seed)
    crack = envelope(highpass(noise(length, rnd), crack_hz), 0.001, 38.0)
    body = envelope(lowpass(noise(length, rnd), 1800), 0.002, 18.0)
    thump = tone(length, thump_hz, thump_hz * 0.6, 22.0)
    return mix((crack, 0.6), (body, 0.8), (thump, 0.7))


def impact(seed, length):
    """A hit: a dull burst of low noise."""
    rnd = random.Random(seed)
    burst = envelope(lowpass(noise(length, rnd), 900), 0.002, 16.0)
    knock = tone(length, 140, 70, 25.0)
    return mix((burst, 1.0), (knock, 0.5))


def explosion(seed, length, rumble_hz, cutoff):
    """A blast: a loud rush of noise falling into a long low rumble."""
    rnd = random.Random(seed)
    rush = envelope(lowpass(noise(length, rnd), cutoff), 0.004, 5.5)
    rumble = envelope(lowpass(noise(length, rnd), 160), 0.01, 2.6)
    boom = tone(length, rumble_hz, rumble_hz * 0.45, 4.0)
    return mix((rush, 0.9), (rumble, 1.0), (boom, 0.8))


def death(seed, length, metal):
    """A unit lost: a crunch, and for a vehicle a ringing metal tone falling away."""
    rnd = random.Random(seed)
    crunch = envelope(lowpass(noise(length, rnd), 2400), 0.002, 9.0)
    layers = [(crunch, 1.0)]
    if metal:
        layers.append((tone(length, 420, 180, 6.0), 0.35))
        layers.append((tone(length, 610, 260, 7.0), 0.2))
    else:
        layers.append((tone(length, 220, 120, 12.0), 0.3))
    return mix(*layers)


def pistol(seed, crack_hz):
    """A pistol: a short sharp crack with little low end, and the click of the slide going back a moment after."""
    rnd = random.Random(seed)
    length = 0.3
    crack = envelope(biquad(noise(length, rnd), "high", crack_hz, 1.0), 0.0003, 90.0)
    body = envelope(biquad(noise(length, rnd), "band", 1300, 0.9), 0.0008, 45.0)
    thump = tone(length, 170, 95, 40.0)
    shot = saturate(mix((crack, 0.8), (body, 0.9), (thump, 0.6)), 2.0)
    click = envelope(biquad(noise(0.08, rnd), "high", 5000), 0.0002, 200.0)
    slide = delay(mix((metal(0.08, [(2350, 1.0, 90.0), (3480, 0.7, 110.0), (4710, 0.4, 130.0)]), 1.0), (click, 0.5)), 0.045)
    return echo(pad(mix((shot, 1.0), (slide, 0.25)), 0.2), [(0.07, 0.22, 2600), (0.13, 0.12, 1600)])


def rifle_round(rnd, crack_hz, thump_hz):
    """One round of an automatic rifle, dry: shorter than a pistol's, with more body."""
    length = 0.16
    crack = envelope(biquad(noise(length, rnd), "high", crack_hz, 0.9), 0.0004, 75.0)
    body = envelope(biquad(noise(length, rnd), "band", 850, 0.7), 0.001, 38.0)
    thump = tone(length, thump_hz, thump_hz * 0.55, 35.0)
    return saturate(mix((crack, 0.9), (body, 0.9), (thump, 0.5)), 2.5)


def rifle_shot(seed, crack_hz, thump_hz):
    """A single round of the rifle, with a short echo: one sound for each shot fired."""
    rnd = random.Random(seed)
    return echo(pad(rifle_round(rnd, crack_hz, thump_hz), 0.25), [(0.08, 0.25, 2200), (0.15, 0.14, 1400)])


def rifle_burst(seed, rounds, rate):
    """A burst: `rounds` rounds at `rate` a second, each a little different (crack, thump, loudness, a jitter of 2 ms) so that it
    does not sound like a machine gun of a toy; the echo of all of them rings on after the last."""
    rnd = random.Random(seed)
    layers = []
    for i in range(rounds):
        at = max(0.0, i / rate + rnd.uniform(-0.002, 0.002))
        round_ = rifle_round(rnd, 2600 * rnd.uniform(0.92, 1.08), 120 * rnd.uniform(0.95, 1.05))
        layers.append((delay(round_, at), rnd.uniform(0.85, 1.0)))
    return echo(pad(mix(*layers), 0.5), [(0.09, 0.3, 2200), (0.17, 0.2, 1500), (0.3, 0.12, 900), (0.45, 0.07, 600)])


def tank_cannon(seed, boom_hz):
    """A tank gun: a deep saturated boom with a sub-bass under it, the crack of the muzzle blast, the ring of the breech, and a
    long echo off the land."""
    rnd = random.Random(seed)
    length = 1.8
    blast = envelope(biquad(noise(length, rnd), "low", 380, 0.8), 0.002, 6.0)
    crack = envelope(biquad(noise(length, rnd), "high", 1400, 0.8), 0.0005, 40.0)
    # The middle of the blast: without it small speakers, which give nothing under 150 Hz, would hardly play the shot.
    roar = envelope(biquad(noise(length, rnd), "band", 900, 0.7), 0.001, 9.0)
    boom = tone(length, boom_hz, boom_hz * 0.42, 3.2)
    sub = tone(length, boom_hz * 0.5, boom_hz * 0.3, 2.2)
    low = saturate(mix((blast, 1.0), (boom, 1.0), (sub, 0.6)), 3.0)
    ring = metal(length, [(311, 1.0, 4.5), (452, 0.8, 5.5), (697, 0.5, 6.5), (938, 0.3, 8.0)])
    # The crack and the roar are laid over the clipped low end, not clipped with it, or the boom would press them down.
    high = saturate(mix((crack, 0.8), (roar, 1.0)), 2.0)
    land = [(0.14, 0.35, 1400), (0.27, 0.25, 900), (0.43, 0.16, 600), (0.62, 0.1, 400)]
    return echo(mix((low, 1.0), (high, 0.6), (ring, 0.12)), land)


def rotor_loop(seed, seconds, passes_per_second, tail_hz):
    """A helicopter, as a seamless loop: a slap of noise each time a blade of the main rotor passes, a thinner faster buzz of the
    tail rotor, the whine of the turbine and a hiss of air. The loop holds a whole number of blade passes; the noise is made a
    blade pass longer and folded back over its start, and the tones are at whole multiples of 1 / `seconds`."""
    rnd = random.Random(seed)
    period = RATE // passes_per_second
    count = int(seconds * RATE)
    assert count % period == 0 and RATE % passes_per_second == 0, "the loop must hold a whole number of blade passes"
    total = count + period
    gains = [rnd.uniform(0.85, 1.0) for _ in range(count // period)]

    def looped_noise(kind, hz, q):
        return loopable(biquad(noise(total / RATE, rnd), kind, hz, q), count)

    def blade(i):
        """The loudness of the main rotor at sample i: zero, a quick rise, then a fall until the next blade."""
        phase = (i % period) / period
        gain = gains[(i // period) % len(gains)]
        if phase < 0.05:
            return 0.0
        if phase < 0.15:
            return gain * (phase - 0.05) / 0.1
        return gain * math.exp(-(phase - 0.15) * 9.0)

    slap = [x * blade(i) for i, x in enumerate(looped_noise("band", 420, 1.1))]
    whomp = [x * blade(i) for i, x in enumerate(looped_noise("low", 140, 0.8))]
    tail = [x * (0.5 + 0.5 * math.sin(2.0 * math.pi * tail_hz * i / RATE)) ** 2
            for i, x in enumerate(looped_noise("band", 1700, 1.5))]
    air = looped_noise("low", 500, 0.7)
    whine = [math.sin(2.0 * math.pi * 4100 * i / RATE) + 0.5 * math.sin(2.0 * math.pi * 6150 * i / RATE) for i in range(count)]
    return mix((slap, 1.0), (whomp, 1.3), (tail, 0.25), (air, 0.15), (whine, 0.02))


def shimmer(seconds, partials, detune, decay_rate):
    """Ringing glass: each partial a pair of sines a hair apart (`detune` of its frequency) that beat slowly against each other,
    the shimmer of light in a crystal; (Hz, gain) each."""
    layers = []
    for hz, gain in partials:
        layers.append((tone(seconds, hz, hz, decay_rate), gain))
        layers.append((tone(seconds, hz * (1.0 + detune), hz * (1.0 + detune), decay_rate), gain))
    return mix(*layers)


def prism_charge(seed, seconds, start_hz, end_hz, spin_hz):
    """A prism gathering light: a glassy tone (partials at 1, 2.01 and 3.03) rising from start_hz to end_hz and swelling, with a
    tremolo that speeds up as the mirrors spin faster, and a hiss growing under it."""
    rnd = random.Random(seed)
    count = int(seconds * RATE)
    out, phase, spin = [], 0.0, 0.0
    for i in range(count):
        t = i / count
        phase += 2.0 * math.pi * start_hz * (end_hz / start_hz) ** t / RATE
        spin += 2.0 * math.pi * spin_hz * (0.3 + 0.7 * t) / RATE
        glass = math.sin(phase) + 0.4 * math.sin(2.01 * phase) + 0.25 * math.sin(3.03 * phase)
        out.append(glass * t * t * (0.6 + 0.4 * math.sin(spin)))
    hiss = [x * (i / count) ** 3 for i, x in enumerate(biquad(noise(seconds, rnd), "band", end_hz * 2.0, 2.0))]
    return mix((out, 1.0), (hiss, 0.3))


def prism_beam(seed, top_hz, length, ring_decay, thump_hz, fragments):
    """A prism shot: a fast falling zap, glass ringing at inharmonic partials of top_hz, a high sizzle of the beam, a thump where it
    lands, and for the tank `fragments` small zaps a moment later, the beam splitting onto the targets around (CometFragment)."""
    rnd = random.Random(seed)
    zap = tone(0.14, top_hz, top_hz * 0.3, 18.0)
    base = top_hz * 0.5
    glass = shimmer(length, [(base, 1.0), (base * 1.47, 0.7), (base * 2.13, 0.5), (base * 3.41, 0.3)], 0.004, ring_decay)
    sizzle = envelope(biquad(noise(length, rnd), "band", 7000, 0.8), 0.001, ring_decay * 1.5)
    thump = mix((tone(0.4, thump_hz, thump_hz * 0.5, 14.0), 1.0),
                (envelope(biquad(noise(0.4, rnd), "low", 400, 0.8), 0.001, 16.0), 0.8))
    layers = [(zap, 0.8), (glass, 0.35), (sizzle, 0.5), (thump, 0.9)]
    for k in range(fragments):
        hz = rnd.uniform(2500, 4000)
        layers.append((delay(tone(0.07, hz, hz * 0.4, 40.0), 0.12 + 0.04 * k + rnd.uniform(0.0, 0.015)), 0.22))
    return echo(pad(saturate(mix(*layers), 1.5), 0.3), [(0.08, 0.25, 4000), (0.16, 0.14, 2500), (0.27, 0.08, 1500)])


def prism_support(seed):
    """A support beam of a prism tower: softer and with no thump, a glassy tone rising into the tower that fires, under a hiss."""
    rnd = random.Random(seed)
    length = 0.55
    rise = envelope(tone(0.3, 1500, 3000, 6.0), 0.04, 0.0)
    glass = envelope(shimmer(length, [(1750, 1.0), (2570, 0.6), (3730, 0.4)], 0.005, 7.0), 0.05, 0.0)
    hiss = envelope(biquad(noise(length, rnd), "band", 6000, 1.2), 0.05, 9.0)
    return echo(pad(mix((rise, 0.6), (glass, 0.4), (hiss, 0.4)), 0.2), [(0.09, 0.2, 3500), (0.18, 0.1, 2000)])


def lowpass_sweep(samples, cutoffs):
    """A low-pass whose cutoff moves, one cutoff in Hz for each sample: two one-pole stages, for a steeper slope."""
    out, y1, y2 = [], 0.0, 0.0
    for x, hz in zip(samples, cutoffs):
        alpha = 1.0 - math.exp(-2.0 * math.pi * hz / RATE)
        y1 += alpha * (x - y1)
        y2 += alpha * (y1 - y2)
        out.append(y2)
    return out


def varying_delay(samples, delays):
    """Each sample read `delays[i]` seconds back, between two samples by linear interpolation."""
    out = []
    for i, seconds in enumerate(delays):
        position = i - seconds * RATE
        j = math.floor(position)
        a = samples[j] if 0 <= j < len(samples) else 0.0
        b = samples[j + 1] if 0 <= j + 1 < len(samples) else 0.0
        out.append(a + (b - a) * (position - j))
    return out


def crackle(seconds, per_second, rnd, wrap=False):
    """The crackle of jet exhaust: a crowd of tiny clicks of noise, a few ms each, at random times and strengths. With `wrap` a
    click that runs past the end goes on at the start, for a loop."""
    count = int(seconds * RATE)
    out = [0.0] * count
    for _ in range(int(seconds * per_second)):
        at, strength = rnd.randrange(count), rnd.uniform(0.2, 1.0) ** 2
        length = int(rnd.uniform(0.001, 0.004) * RATE)
        for k in range(length):
            if at + k >= count and not wrap:
                break
            out[(at + k) % count] += strength * rnd.uniform(-1.0, 1.0) * math.exp(-4.0 * k / length)
    return biquad(out, "high", 1500)


def jet_loop(seed, seconds, whine_hz):
    """A jet engine at a steady distance, as a seamless loop: the roar of the exhaust (noise in three bands over a low rumble),
    the whine of the turbine and its double, and the crackle. The noise is made 0.1 s longer and folded back over its start;
    whine_hz and the slow sway of the whine are whole multiples of 1 / `seconds`."""
    rnd = random.Random(seed)
    count = int(seconds * RATE)
    total = count + int(0.1 * RATE)

    def looped_noise(kind, hz, q):
        return loopable(biquad(noise(total / RATE, rnd), kind, hz, q), count)

    rumble = looped_noise("low", 110, 0.8)
    roar = looped_noise("low", 700, 0.6)
    body = looped_noise("band", 1600, 0.7)
    hiss = looped_noise("high", 5000, 0.7)
    whine = [(math.sin(2.0 * math.pi * whine_hz * i / RATE) + 0.35 * math.sin(4.0 * math.pi * whine_hz * i / RATE))
             * (0.8 + 0.2 * math.sin(2.0 * math.pi * i / count)) for i in range(count)]
    crack = crackle(seconds, 90, rnd, wrap=True)
    return mix((rumble, 0.9), (roar, 1.0), (body, 0.5), (hiss, 0.15), (whine, 0.06), (crack, 0.15))


def jet_flyby(seed, seconds, speed, height, whine_hz):
    """A fighter passing over, `height` m up at `speed` m/s, overhead at 45% of the sound. From where the plane is at each moment:
    the loudness ((height / distance)^2), the air dulling the far sound (a moving low-pass), the Doppler shift of the whine
    (343 / (343 + the speed away)), the crackle of the exhaust heard mostly behind it, and the reflection off the ground, whose
    delay shrinks with the distance and sweeps a comb over the roar (the hollow "flanging" of a jet going over)."""
    rnd = random.Random(seed)
    count = int(seconds * RATE)
    overhead, sound_speed, ear = seconds * 0.45, 343.0, 1.7
    near, factor, behind = [], [], []
    for i in range(count):
        x = speed * (i / RATE - overhead)
        distance = math.hypot(height, x)
        near.append(height / distance)
        factor.append(sound_speed / (sound_speed + speed * x / distance))
        behind.append(0.5 + 0.5 * x / distance)
    source = mix((biquad(noise(seconds, rnd), "low", 110, 0.8), 0.9), (biquad(noise(seconds, rnd), "low", 3000, 0.6), 1.0),
                 (biquad(noise(seconds, rnd), "high", 5000, 0.7), 0.15))
    crack = [x * b * b for x, b in zip(crackle(seconds, 120, rnd), behind)]
    air = lowpass_sweep(mix((source, 1.0), (crack, 0.5)), [600 + 9000 * n ** 1.5 for n in near])
    whine, phase = [], 0.0
    for i in range(count):
        phase += 2.0 * math.pi * whine_hz * factor[i] / RATE
        whine.append((math.sin(phase) + 0.35 * math.sin(2.0 * phase)) * near[i] ** 2)
    # The plane comes out of and goes into silence: a soft rise over the first 0.5 s and a fall over the last 0.8 s.
    rise, fall = 0.5 * RATE, 0.8 * RATE
    edges = [min(1.0, i / rise, (count - i) / fall) for i in range(count)]
    dry = [e * n * n * x for e, n, x in zip(edges, near, mix((air, 1.0), (whine, 0.08)))]
    wet = mix((dry, 1.0), (varying_delay(dry, [2.0 * ear * n / sound_speed for n in near]), 0.8))
    return echo(pad(wet, 0.1), [(0.12, 0.2, 1500), (0.25, 0.12, 900)])


def bubble(hz, seconds, decay_rate):
    """One bubble in water: a sine whose pitch rises as the bubble shrinks to the surface, dying fast."""
    out, phase = [], 0.0
    count = int(seconds * RATE)
    for i in range(count):
        t = i / count
        phase += 2.0 * math.pi * hz * (1.0 + 1.5 * t) / RATE
        out.append(math.sin(phase) * math.exp(-i / RATE * decay_rate))
    return out


def splash(seed, size):
    """Something falling into water, `size` from 0 (a plank) to 1 (a span of concrete): a low whump where it goes in, a rush of
    water and spray, then bubbles rising, as many and as long as the splash is big."""
    rnd = random.Random(seed)
    length = 0.6 + 1.6 * size
    whump = mix((tone(0.6, 90 - 40 * size, 40 - 15 * size, 6.0), 1.0),
                (envelope(biquad(noise(0.6, rnd), "low", 300, 0.8), 0.003, 9.0), 1.0))
    rush = envelope(biquad(noise(length, rnd), "band", 900, 0.6), 0.01, 5.0 - 2.5 * size)
    spray = envelope(biquad(noise(length, rnd), "high", 3500, 0.7), 0.02, 6.0 - 3.0 * size)
    layers = [(whump, 0.4 + 0.6 * size), (rush, 1.0), (spray, 0.5)]
    for _ in range(int(12 + 40 * size)):
        at = 0.05 + rnd.expovariate(1.0 / (0.15 + 0.4 * size))
        if at < length - 0.1:
            hz = rnd.uniform(350, 1400) / (1.0 + size)
            layers.append((delay(bubble(hz, 0.08, rnd.uniform(40.0, 80.0)), at), rnd.uniform(0.05, 0.2) * math.exp(-at)))
    return mix(*layers)


def wood_crack(rnd, low_hz):
    """Wood giving way: a sharp burst of bright noise over a few hollow modes of the beam (low-Q, dying fast)."""
    snap = envelope(biquad(noise(0.12, rnd), "high", 2000, 0.8), 0.0003, 60.0)
    body = metal(0.3, [(low_hz, 1.0, 22.0), (low_hz * 1.73, 0.6, 30.0), (low_hz * 2.61, 0.35, 40.0)])
    return mix((snap, 1.0), (body, 0.6))


def groan(seed, seconds, start_hz, end_hz):
    """Steel bending: a sawtooth that sticks and slips (its pitch jitters), sinking from start_hz to end_hz, heard through the
    resonances of the girder, swelling in and out."""
    rnd = random.Random(seed)
    count = int(seconds * RATE)
    out, phase, jitter = [], 0.0, 0.0
    for i in range(count):
        t = i / count
        jitter += (rnd.uniform(-1.0, 1.0) - jitter) * 0.002
        phase = (phase + (start_hz + (end_hz - start_hz) * t) * (1.0 + 0.15 * jitter) / RATE) % 1.0
        out.append((2.0 * phase - 1.0) * math.sin(math.pi * t))
    return mix((biquad(out, "band", 420, 3.0), 1.0), (biquad(out, "band", 130, 1.5), 0.8), (biquad(out, "band", 1100, 4.0), 0.3))


def crumble(seed, seconds, per_second, cutoff):
    """Broken concrete and stones knocking together: a crowd of small clicks, low-passed so they are lumps and not sparks."""
    rnd = random.Random(seed)
    return biquad(crackle(seconds, per_second, rnd), "low", cutoff, 0.7)


def wood_span(seed, at=0.0):
    """A span of a wooden bridge breaking: a crack, splinters, two beams snapping with a hollow knock, the span hitting the
    water, and planks dropping after."""
    rnd = random.Random(seed)
    splinters = [x * math.exp(-i / RATE * 5.0) for i, x in enumerate(biquad(crackle(0.6, 160, rnd), "band", 2500, 0.7))]
    layers = [(wood_crack(rnd, rnd.uniform(220, 280)), 1.0), (delay(splinters, 0.03), 0.8),
              (delay(wood_crack(rnd, rnd.uniform(160, 200)), 0.25), 0.7), (delay(wood_crack(rnd, rnd.uniform(240, 300)), 0.45), 0.5),
              (delay(splash(rnd.randrange(1 << 30), 0.45), 0.7), 0.9)]
    for k in range(3):
        layers.append((delay(splash(rnd.randrange(1 << 30), 0.0), 0.95 + 0.2 * k + rnd.uniform(0.0, 0.1)), 0.3))
    return delay(mix(*layers), at)


def wooden_bridge(seed, spans):
    """Spans breaking one after another, as under C4: the whole bridge from shore to shore."""
    rnd = random.Random(seed)
    starts = [0.0]
    for _ in range(spans - 1):
        starts.append(starts[-1] + rnd.uniform(0.3, 0.45))
    return echo(pad(mix(*((wood_span(seed * 10 + k, at), 1.0) for k, at in enumerate(starts))), 0.3),
                [(0.1, 0.25, 2000), (0.21, 0.14, 1200)])


def concrete_bridge(seed):
    """A high concrete bridge coming down whole: the snap of the steel, the girders groaning as they bend, the deck cracking and
    breaking with heavy crunches, a rumble under it all, the deck hitting the water far below with a big splash and the pieces
    after it, and stones still knocking as it settles; a long echo off the walls of the valley."""
    rnd = random.Random(seed)
    snap = mix((envelope(biquad(noise(0.3, rnd), "high", 1500, 0.8), 0.0003, 30.0), 1.0),
               (metal(1.2, [(523, 1.0, 4.0), (811, 0.7, 5.0), (1187, 0.4, 6.0)]), 0.4))
    rumble = [x * math.sin(math.pi * min(1.0, i / (4.5 * RATE))) for i, x in enumerate(biquad(noise(4.5, rnd), "low", 90, 0.8))]
    layers = [(snap, 0.8), (delay(groan(seed + 1, 1.6, 95, 55), 0.1), 0.6), (delay(rumble, 0.4), 1.0)]
    # The deck cracking: the crack of a beam, lower than wood's.
    for at in [0.7, 0.95, 1.3]:
        layers.append((delay(wood_crack(rnd, rnd.uniform(110, 150)), at), 0.5))
    for k, at in enumerate([1.1, 1.55, 2.0]):
        hit = mix((tone(0.9, 70 - 8 * k, 35, 5.0), 1.0), (envelope(biquad(noise(0.9, rnd), "low", 500, 0.8), 0.002, 6.0), 1.0),
                  (crumble(seed + 10 + k, 0.9, 140, 2500), 0.6))
        layers.append((delay(hit, at), 0.9))
    layers.append((delay(splash(seed + 20, 1.0), 2.7), 1.1))
    layers.append((delay(splash(seed + 21, 0.6), 3.05), 0.6))
    layers.append((delay(splash(seed + 22, 0.4), 3.4), 0.4))
    settle = [x * math.exp(-i / RATE * 1.2) for i, x in enumerate(crumble(seed + 30, 2.5, 50, 1800))]
    layers.append((delay(settle, 3.2), 0.4))
    dry = saturate(mix(*layers), 1.8)
    return echo(pad(dry, 0.3), [(0.18, 0.35, 1200), (0.37, 0.25, 800), (0.6, 0.16, 500), (0.9, 0.1, 350)])


def glass(seed, seconds, shards):
    """Window glass breaking: a bright burst, then shards, short pings at high inharmonic pitches, thick at first, thinning out."""
    rnd = random.Random(seed)
    layers = [(envelope(biquad(noise(0.25, rnd), "high", 4000, 0.8), 0.0005, 25.0), 0.8)]
    for _ in range(shards):
        at = rnd.expovariate(1.0 / 0.12)
        if at < seconds - 0.1:
            hz = rnd.uniform(2800, 8000)
            ping = metal(0.12, [(hz, 1.0, rnd.uniform(35.0, 70.0)), (hz * 1.58, 0.5, 60.0), (hz * 2.31, 0.25, 80.0)])
            layers.append((delay(ping, at), rnd.uniform(0.05, 0.25)))
    return mix(*layers)


def swell(samples, peak_at, fall_rate):
    """Shapes a sound to rise until peak_at seconds, then fall away as exp(-t * fall_rate)."""
    peak = peak_at * RATE
    return [x * (i / peak if i < peak else math.exp(-(i - peak) / RATE * fall_rate)) for i, x in enumerate(samples)]


def heavy_hit(rnd, hz, cutoff):
    """A wall or a slab landing: a low falling thump and a burst of low noise."""
    return mix((tone(0.9, hz, hz * 0.5, 5.5), 1.0), (envelope(biquad(noise(0.9, rnd), "low", cutoff, 0.8), 0.002, 7.0), 1.0))


def brick_building(seed):
    """A brick building coming down: the crack of the masonry, the windows shattering, the walls landing in four heavy thumps
    over a rumble, bricks clacking against each other (hard and bright, unlike concrete), the dust settling with a hiss and a few
    bricks and shards still falling after."""
    rnd = random.Random(seed)
    layers = [(wood_crack(rnd, rnd.uniform(170, 210)), 0.9), (delay(glass(seed + 1, 0.8, 40), 0.05), 1.0)]
    at = 0.2
    for k in range(4):
        layers.append((delay(heavy_hit(rnd, 85 - 6 * k, 600), at), rnd.uniform(0.5, 0.65)))
        at += rnd.uniform(0.3, 0.45)
    bricks = swell(biquad(crackle(2.6, 260, rnd), "band", 2200, 1.2), 0.5, 1.6)
    rumble = swell(biquad(noise(2.6, rnd), "low", 100, 0.8), 0.6, 1.4)
    dust = swell(biquad(noise(3.2, rnd), "band", 5000, 0.7), 0.8, 1.2)
    later = biquad(crackle(1.5, 25, rnd), "band", 2000, 1.2)
    layers += [(delay(bricks, 0.15), 2.0), (delay(rumble, 0.1), 0.8), (delay(dust, 0.3), 0.15), (delay(later, 2.2), 0.9),
               (delay(glass(seed + 2, 0.5, 8), 2.4), 0.3)]
    return echo(pad(saturate(mix(*layers), 1.6), 0.3), [(0.11, 0.25, 1800), (0.23, 0.15, 1100), (0.38, 0.08, 700)])


def concrete_building(seed):
    """A concrete building coming down: heavier and lower than brick, and no windows to break: a deep snap with the ring of the
    rebar, the rebar groaning as it bends, three slabs landing like blows over a long deep rumble, the concrete breaking into dull
    lumps, the rebar clanging as it hits, the dust, and lumps rolling after."""
    rnd = random.Random(seed)
    snap = mix((envelope(biquad(noise(0.3, rnd), "high", 1200, 0.8), 0.0003, 28.0), 1.0),
               (metal(0.9, [(640, 1.0, 5.0), (987, 0.6, 6.0), (1433, 0.35, 7.0)]), 0.35))
    layers = [(snap, 0.9), (delay(groan(seed + 1, 0.9, 120, 70), 0.1), 0.35)]
    at = 0.35
    for k in range(3):
        layers.append((delay(heavy_hit(rnd, 65 - 6 * k, 450), at), 1.0))
        at += rnd.uniform(0.4, 0.55)
    for clang_at in (0.6, 1.3):
        hz = rnd.uniform(700, 1100)
        layers.append((delay(metal(0.6, [(hz, 1.0, 9.0), (hz * 1.41, 0.6, 11.0), (hz * 2.17, 0.3, 14.0)]), clang_at), 0.12))
    lumps = swell(crumble(seed + 2, 3.0, 200, 1500), 0.7, 1.3)
    rumble = swell(biquad(noise(3.4, rnd), "low", 75, 0.8), 0.8, 1.0)
    dust = swell(biquad(noise(3.5, rnd), "band", 4500, 0.7), 1.0, 1.0)
    later = crumble(seed + 3, 1.6, 20, 1300)
    layers += [(delay(lumps, 0.3), 1.0), (delay(rumble, 0.1), 1.1), (delay(dust, 0.4), 0.1), (delay(later, 2.8), 0.6)]
    return echo(pad(saturate(mix(*layers), 1.8), 0.3), [(0.13, 0.3, 1400), (0.27, 0.18, 900), (0.44, 0.1, 550)])


def main():
    write("sfx/shot_01.wav", shot(101, 2500, 120, 0.35))
    write("sfx/shot_02.wav", shot(102, 3200, 100, 0.40))
    write("sfx/shot_03.wav", shot(103, 2000, 140, 0.30))
    write("sfx/impact_01.wav", impact(201, 0.30))
    write("sfx/impact_02.wav", impact(202, 0.35))
    write("sfx/explosion_01.wav", explosion(301, 0.9, 70, 1400))
    write("sfx/explosion_02.wav", explosion(302, 1.2, 55, 1100))
    write("sfx/explosion_03.wav", explosion(303, 1.6, 45, 900))
    write("sfx/death_01.wav", death(401, 0.7, metal=True))
    write("sfx/death_02.wav", death(402, 0.5, metal=False))
    # The weapons by kind, with two-pole filters, saturation, metal and echo.
    write("sfx/pistol_01.wav", pistol(601, 3200))
    write("sfx/pistol_02.wav", pistol(602, 3800))
    write("sfx/rifle_01.wav", rifle_shot(701, 2600, 120))
    write("sfx/rifle_02.wav", rifle_shot(702, 2900, 115))
    write("sfx/rifle_03.wav", rifle_shot(703, 2400, 130))
    write("sfx/rifle_burst_01.wav", rifle_burst(711, rounds=8, rate=11.0))
    write("sfx/tank_cannon_01.wav", tank_cannon(801, 62))
    write("sfx/tank_cannon_02.wav", tank_cannon(802, 55))
    # 4.5 turns a second of a rotor of four blades: 18 blade passes a second, 36 in the loop of 2 s.
    write("sfx/rotor_loop_01.wav", rotor_loop(901, 2.0, 18, 80.0), loop=True)
    # The prisms: the tank's beam is brighter and splits onto five targets; the tower's is lower, heavier and rings longer.
    write("sfx/prism_tank_01.wav", prism_beam(1001, 3000, 0.9, 6.0, 110, fragments=5))
    write("sfx/prism_tank_02.wav", prism_beam(1002, 3300, 0.9, 6.5, 120, fragments=5))
    write("sfx/prism_tower_01.wav", prism_beam(1101, 2200, 1.4, 3.5, 70, fragments=0))
    write("sfx/prism_tower_02.wav", prism_beam(1102, 2000, 1.4, 3.2, 65, fragments=0))
    write("sfx/prism_support_01.wav", prism_support(1201))
    write("sfx/prism_tank_charge_01.wav", prism_charge(1301, 0.5, 600, 2400, 14.0))
    write("sfx/prism_tower_charge_01.wav", prism_charge(1401, 0.8, 300, 1600, 9.0))
    # The fighter jet: the engine as a loop, and two passes overhead, one high and one low and fast.
    write("sfx/jet_loop_01.wav", jet_loop(1501, 2.0, 3100.0), loop=True)
    write("sfx/jet_flyby_01.wav", jet_flyby(1601, 4.0, speed=120.0, height=120.0, whine_hz=3100.0))
    write("sfx/jet_flyby_02.wav", jet_flyby(1602, 3.5, speed=140.0, height=80.0, whine_hz=2700.0))
    # The bridges (docs/map_elements/05_bridges): one wooden span under fire, the whole wooden bridge under C4, a high concrete
    # bridge coming down whole.
    write("sfx/bridge_wood_span_01.wav", trim(echo(pad(wood_span(1701), 0.3), [(0.1, 0.25, 2000), (0.21, 0.14, 1200)])))
    write("sfx/bridge_wood_span_02.wav", trim(echo(pad(wood_span(1702), 0.3), [(0.1, 0.25, 2000), (0.21, 0.14, 1200)])))
    write("sfx/bridge_wood_all_01.wav", trim(wooden_bridge(1801, spans=4)))
    write("sfx/bridge_concrete_01.wav", trim(concrete_bridge(1901)))
    # The buildings, by material (docs/map_elements/11_civilian_buildings): brick, with windows; concrete, heavier, without.
    write("sfx/building_brick_01.wav", trim(brick_building(2001)))
    write("sfx/building_brick_02.wav", trim(brick_building(2002)))
    write("sfx/building_concrete_01.wav", trim(concrete_building(2101)))
    write("sfx/building_concrete_02.wav", trim(concrete_building(2102)))
    # The interface: a soft high tick over a button, a short firm click on it.
    write("ui/hover_01.wav", mix((tone(0.05, 1500, 1400, 60.0), 1.0)))
    rnd = random.Random(501)
    write("ui/click_01.wav", mix((tone(0.08, 900, 600, 45.0), 1.0), (envelope(highpass(noise(0.08, rnd), 3000), 0.0005, 90.0), 0.4)))


if __name__ == "__main__":
    main()
