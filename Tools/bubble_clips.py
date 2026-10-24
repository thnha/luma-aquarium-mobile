#!/usr/bin/env python3
"""Generate the aquarium's bubble pop clips (ton_*, bloop_*) from a physical bubble model.

A bubble rings at its Minnaert frequency (about 3.26 / radius in metres), the ring dies away with the damping
of van den Doel's model, and the pitch rises while the bubble nears the surface and shrinks, which is what makes
a real bubble go "plip" instead of beeping. Small bubbles give the high "ton", big ones the low "bloop".

Usage: python3 Tools/bubble_clips.py [out_dir]   (default: Assets/_Project/Audio/Placeholder)
Stdlib only. The same seed gives the same files, so rerunning it doesn't churn the assets.
"""
import math
import os
import random
import struct
import sys
import wave

RATE = 44100


def bubble(f0, rise, decay_scale, length, rng):
    """One bubble: a rising, decaying sine with a faint wet click at the start."""
    damping = (0.13 * f0 + 0.0072 * f0 ** 1.5) * decay_scale
    n = int(length * RATE)
    attack = int(0.0015 * RATE)
    phase, out, noise, last = 0.0, [], 0.0, 0.0
    for i in range(n):
        t = i / RATE
        f = f0 * (1 + rise * damping * t)
        phase += 2 * math.pi * f / RATE
        env = math.exp(-damping * t) * (0.5 - 0.5 * math.cos(math.pi * min(1, i / attack)))
        tone = math.sin(phase) + 0.12 * math.sin(2 * phase + 0.6)
        # A short burst of high-passed noise: the film breaking at the surface.
        white = rng.uniform(-1, 1)
        noise, last = white - last, white
        click = noise * 0.06 * math.exp(-t / 0.003)
        tail = min(1, (n - i) / (0.01 * RATE))   # no step at the end of the file
        out.append((tone * env + click) * tail)
    peak = max(abs(s) for s in out)
    return [s / peak * 0.7 for s in out]


def write(path, samples):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, s)) * 32767)) for s in samples))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "Assets/_Project/Audio/Placeholder"
    rng = random.Random(7)
    # name prefix, base frequencies (Hz), pitch rise, decay scale, length (s)
    kinds = [
        ("ton", [520, 590, 660, 730, 810, 900, 1000, 1120], (0.06, 0.12), 0.5, 0.12),
        ("bloop", [230, 270, 320, 380], (0.12, 0.25), 0.6, 0.26),
    ]
    for prefix, freqs, rise, decay, length in kinds:
        for i, f0 in enumerate(freqs, 1):
            f = f0 * rng.uniform(0.97, 1.03)
            path = os.path.join(out, f"{prefix}_{i:02d}.wav")
            write(path, bubble(f, rng.uniform(*rise), decay, length, rng))
            print(path, round(f), "Hz")


if __name__ == "__main__":
    main()
