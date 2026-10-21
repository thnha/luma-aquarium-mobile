"""Draws the mossy stone each dock icon stands on (2.5D: lit top, shaded sides,
a dark rim and a contact shadow so it stands out from the gravel).

The stone is a heightmap of a flat, lumpy slab seen at an oblique angle and lit
from the upper left. Run from the mobile folder:

    python3 Tools/UI/make_dock_rock.py

Writes Assets/_Project/UI/Surfaces/ui3d_rock.png and ui3d_glow.png, the sage halo
under the stone of an open sheet (neither is sliced).
"""
import math
import os
import sys

import numpy as np
from PIL import Image, ImageFilter

OUT = "Assets/_Project/UI/Surfaces/ui3d_rock.png"
GLOW = "Assets/_Project/UI/Surfaces/ui3d_glow.png"
W, H, SS = 640, 300, 3
TW, TD = W * SS, int(W * SS * .62)  # top-view grid: width x depth
OW, OH = W * SS, H * SS             # supersampled output
TILT = .36                          # depth foreshortening
SEED = 4


def noise(cells_x, seed, shape=(TD, TW)):
    cells_y = max(2, int(cells_x * shape[0] / shape[1]))
    r = np.random.default_rng(seed).normal(0, 1, (cells_y, cells_x)).astype(np.float32)
    return np.array(Image.fromarray(r).resize((shape[1], shape[0]), Image.BICUBIC))


def main():
    if not os.path.isdir(os.path.dirname(OUT)):
        sys.exit("Run from the mobile project folder.")
    ys, xs = np.mgrid[0:TD, 0:TW].astype(np.float32)
    u, v = (xs - TW / 2) / (TW * .46), (ys - TD / 2) / (TD * .44)
    angle = np.arctan2(v, u)
    wobble = 1 + .07 * np.sin(3 * angle + SEED) + .05 * np.sin(5 * angle + 2 * SEED) + .03 * np.sin(8 * angle + 1)
    r = np.hypot(u, v) / wobble

    # Flat top with a rounded shoulder, plus lumps and grain.
    z = np.sin(np.clip((1 - r) / .28, 0, 1) * math.pi / 2) ** .8
    z = z * (1 + .08 * noise(5, SEED) + .03 * noise(18, SEED + 1)) + .004 * noise(70, SEED + 2)
    z = np.where(r < 1, z, 0)
    height = z * OH * .17

    gy, gx = np.gradient(height)
    normal = np.stack([-gx, -gy, np.ones_like(height) * 2.2], -1)
    normal /= np.linalg.norm(normal, axis=-1, keepdims=True)
    light = np.array([-.5, -.35, .8])
    lambert = np.clip(normal @ (light / np.linalg.norm(light)), 0, 1)
    base = np.stack([176, 170, 152], -1) + (noise(12, SEED + 3) * 10)[..., None] + (noise(60, SEED + 4) * 6)[..., None]
    colour = base * (.22 + lambert ** 1.3)[..., None]
    colour *= (.4 + .6 * np.clip(z * 2, 0, 1))[..., None]  # darker toward the ground
    # Moss around the rim of the top, not in the middle where the icon stands.
    moss = np.clip(noise(7, SEED + 5) * .9 + noise(30, SEED + 6) * .4 - .05, 0, 1) * np.clip((normal[..., 2] - .72) * 5, 0, 1)
    moss *= np.clip(1 - np.abs(r - .8) / .3, 0, 1)
    moss_colour = np.stack([92, 150, 52], -1) * (.5 + .7 * lambert)[..., None] + (noise(80, SEED + 7) * 14)[..., None]
    colour = colour * (1 - moss[..., None]) + moss_colour * moss[..., None]

    out = np.zeros((OH, OW, 4), np.float32)
    ground = OH * .80
    shadow = np.zeros((OH, OW), np.float32)
    for row in range(0, TD, 2):
        sy = int(ground - (TD / 2 - row) * TILT + OH * .05)
        if 0 <= sy < OH:
            shadow[sy, r[row] < 1.08] = 1
    shadow = np.array(Image.fromarray((shadow * 255).astype(np.uint8)).filter(ImageFilter.GaussianBlur(7 * SS)), np.float32) / 255
    out[..., :3] = (38, 32, 22)
    out[..., 3] = np.clip(shadow * 2.2, 0, 1) * 210

    # Painter's order: back rows first, each top-view row lifted by its height.
    stone = np.zeros((OH, OW, 4), np.float32)
    columns = np.arange(TW)
    for row in range(TD):
        inside = r[row] < 1
        if not inside.any():
            continue
        sy = (ground - (TD / 2 - row) * TILT - height[row]).astype(int)
        for k in (0, 1, 2):  # close the gaps between projected rows
            yy = np.clip(sy + k, 0, OH - 1)
            stone[yy[inside], columns[inside], :3] = colour[row, inside]
            stone[yy[inside], columns[inside], 3] = 255
    grain = noise(110, 21, (OH, OW)) * .025 + noise(35, 22, (OH, OW)) * .03
    stone[..., :3] *= (1 + grain)[..., None]

    # Dark rim so the stone reads against bright gravel.
    rim = Image.fromarray(stone[..., 3].astype(np.uint8)).filter(ImageFilter.MaxFilter(2 * 2 * SS + 1))
    rim = np.array(rim.filter(ImageFilter.GaussianBlur(SS)), np.float32) / 255
    out[..., :3] = out[..., :3] * (1 - rim[..., None]) + np.array([29, 42, 20]) * rim[..., None]
    out[..., 3] = np.maximum(out[..., 3], rim * 235)
    alpha = stone[..., 3:] / 255
    out[..., :3] = stone[..., :3] * alpha + out[..., :3] * (1 - alpha)
    out[..., 3] = np.maximum(out[..., 3], stone[..., 3])

    image = Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA")
    image = image.crop(image.getbbox())
    image = image.resize((W, round(W * image.height / image.width)), Image.LANCZOS)
    image.save(OUT, optimize=True)
    print("wrote", OUT, image.size)
    glow()


def glow(width=320, height=120):
    """Soft elliptical sage halo, brightest in the middle."""
    ys, xs = np.mgrid[0:height, 0:width].astype(np.float32)
    d = np.hypot((xs - width / 2) / (width / 2), (ys - height / 2) / (height / 2))
    alpha = np.clip(1 - d, 0, 1) ** 1.6
    out = np.zeros((height, width, 4), np.float32)
    out[..., :3] = np.array([233, 247, 200]) * (1 - d[..., None] * .35) + np.array([180, 220, 130]) * d[..., None] * .35
    out[..., 3] = alpha * 255
    Image.fromarray(np.clip(out, 0, 255).astype(np.uint8), "RGBA").save(GLOW, optimize=True)
    print("wrote", GLOW)


if __name__ == "__main__":
    main()
