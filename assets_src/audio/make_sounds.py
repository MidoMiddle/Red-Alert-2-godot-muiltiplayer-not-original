"""The first sound set of the game (docs/implementation/M6_menus_skirmish.md, 4.17, step 14), synthesized.

Stdlib only, fixed seeds: running it again writes the same bytes. Every sound is made here from noise, sines and envelopes, in the
spirit of sfxr: the project's own work, no samples. They are what sounds.yaml asks for — shots, impacts, explosions, deaths and
the two of the interface — and stand until recorded or licensed sounds replace them under the same names.

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


def normalize(samples, peak_db=-1.0):
    peak = max(abs(x) for x in samples) or 1.0
    gain = 10 ** (peak_db / 20.0) / peak
    # A short fade at the end: no click when it stops.
    fade = int(0.01 * RATE)
    out = [x * gain for x in samples]
    for i in range(1, min(fade, len(out)) + 1):
        out[-i] *= i / fade
    return out


def write(relative, samples):
    path = os.path.join(ROOT, relative)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    frames = b"".join(struct.pack("<h", int(max(-1.0, min(1.0, x)) * 32767)) for x in normalize(samples))
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
    # The interface: a soft high tick over a button, a short firm click on it.
    write("ui/hover_01.wav", mix((tone(0.05, 1500, 1400, 60.0), 1.0)))
    rnd = random.Random(501)
    write("ui/click_01.wav", mix((tone(0.08, 900, 600, 45.0), 1.0), (envelope(highpass(noise(0.08, rnd), 3000), 0.0005, 90.0), 0.4)))


if __name__ == "__main__":
    main()
