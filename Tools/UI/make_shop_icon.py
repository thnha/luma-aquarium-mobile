"""Draws the Cửa hàng dock icon: a kraft shopping bag with the game's own Betta and
java fern renders peeking out, and gold coins in front.

It matches the four action icons (1254 px, transparent, soft contact shadow).
Run from the mobile folder:

    python3 Tools/UI/make_shop_icon.py

Writes Assets/_Project/UI/Icons/action_shop.png.
"""
import os
import sys

import numpy as np
from PIL import Image, ImageChops, ImageDraw, ImageFilter

SIZE = 1254
SS = 2  # supersampling
S = SIZE * SS
OUT = "Assets/_Project/UI/Icons/action_shop.png"
FISH = "Assets/_Project/UI/Portraits/Shop/royal.png"
FERN = "Assets/_Project/Art/Aquarium3D/DecorShop/java-fern.png"


def p(*xy):
    """Scales icon coordinates to the supersampled canvas."""
    return [v * SS for v in xy]


def rgb(value):
    value = value.lstrip("#")
    return np.array([int(value[i:i + 2], 16) for i in (0, 2, 4)], dtype=np.float32)


def layer():
    return Image.new("RGBA", (S, S), (0, 0, 0, 0))


def shape_mask(draw_fn):
    mask = Image.new("L", (S, S), 0)
    draw = ImageDraw.Draw(mask)
    draw.fill, draw.ink = True, 255  # shapes drawn without fill= are filled solid
    draw_fn(draw)
    return mask


def fill_gradient(mask, top, bottom, y0, y1, light_from_left=0.0, noise=0.0, seed=1):
    """Vertical gradient inside `mask`, optionally lit from the left and grained like paper."""
    ys = np.clip((np.arange(S, dtype=np.float32) - y0) / max(1, y1 - y0), 0, 1)[:, None, None]
    colour = rgb(top) * (1 - ys) + rgb(bottom) * ys
    colour = np.broadcast_to(colour, (S, S, 3)).copy()
    if light_from_left:
        xs = np.linspace(1 + light_from_left, 1 - light_from_left, S, dtype=np.float32)[None, :, None]
        colour *= xs
    if noise:
        rng = np.random.default_rng(seed)
        grain = rng.normal(0, noise, (S // 4, S // 4)).astype(np.float32)
        grain = np.array(Image.fromarray(grain).resize((S, S), Image.BILINEAR))[:, :, None]
        colour += grain
    image = Image.fromarray(np.clip(colour, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    out = layer()
    out.paste(image, (0, 0), mask)
    return out


def shadow(mask, offset, blur, alpha, colour=(70, 45, 20)):
    m = ImageChops.offset(mask, offset[0] * SS, offset[1] * SS).filter(ImageFilter.GaussianBlur(blur * SS))
    out = Image.new("RGBA", (S, S), colour + (0,))
    out.putalpha(m.point(lambda v: int(v * alpha)))
    return out


def paste_render(canvas, path, width, centre, angle=0.0, flip=False):
    image = Image.open(path).convert("RGBA")
    image = image.crop(image.getbbox())
    if flip:
        image = image.transpose(Image.FLIP_LEFT_RIGHT)
    scale = width * SS / image.width
    image = image.resize((round(image.width * scale), round(image.height * scale)), Image.LANCZOS)
    if angle:
        image = image.rotate(angle, resample=Image.BICUBIC, expand=True)
    x = centre[0] * SS - image.width // 2
    y = centre[1] * SS - image.height // 2
    canvas.alpha_composite(image, (x, y))


def rope(canvas, points, width, base, light):
    """A twisted rope handle: dark core, lighter strand and a few twist marks."""
    draw = ImageDraw.Draw(canvas)
    pts = [tuple(p(*pt)) for pt in points]
    draw.line(pts, fill=base, width=width * SS, joint="curve")
    shifted = [(x - 3 * SS, y - 4 * SS) for x, y in pts]
    draw.line(shifted, fill=light, width=max(2, width // 3) * SS, joint="curve")
    for (x0, y0), (x1, y1) in zip(pts[::3], pts[1::3]):
        draw.line([(x0, y0 - width * SS // 3), (x1, y1 + width * SS // 3)], fill=base, width=3 * SS)


def arc_points(start, end, peak, steps=40):
    (x0, y0), (x1, y1) = start, end
    out = []
    for i in range(steps + 1):
        t = i / steps
        x = x0 + (x1 - x0) * t
        y = (1 - t) ** 2 * y0 + 2 * (1 - t) * t * peak + t * t * y1
        out.append((x, y))
    return out


def coin(canvas, centre, radius, tilt=1.0):
    """A gold coin with rim, thickness and an embossed L, like utility_coin.svg."""
    cx, cy = centre
    rx, ry = radius, radius * tilt
    edge = 22 if tilt > .8 else 30
    thick = shape_mask(lambda d: d.ellipse(p(cx - rx, cy - ry + edge, cx + rx, cy + ry + edge)))
    canvas.alpha_composite(shadow(thick, (10, 18), 14, .35))
    canvas.alpha_composite(fill_gradient(thick, "#B98A2E", "#8C6420", *p(cy, cy + ry + edge)))
    face = shape_mask(lambda d: d.ellipse(p(cx - rx, cy - ry, cx + rx, cy + ry)))
    canvas.alpha_composite(fill_gradient(face, "#F9E39A", "#D6A63E", *p(cy - ry, cy + ry), light_from_left=.06))
    draw = ImageDraw.Draw(canvas)
    draw.ellipse(p(cx - rx, cy - ry, cx + rx, cy + ry), outline=(164, 129, 50, 255), width=10 * SS)
    ir, iry = rx * .8, ry * .8
    draw.ellipse(p(cx - ir, cy - iry, cx + ir, cy + iry), outline=(255, 240, 185, 230), width=6 * SS)
    # Embossed L: highlight first, then the darker stroke on top.
    for dx, dy, colour in ((3, 3, (255, 244, 200, 200)), (0, 0, (147, 109, 37, 255))):
        draw.line([tuple(p(cx - rx * .2 + dx, cy - ry * .45 + dy)), tuple(p(cx - rx * .2 + dx, cy + ry * .4 + dy)),
                   tuple(p(cx + rx * .32 + dx, cy + ry * .4 + dy))], fill=colour, width=int(rx * .17) * SS, joint="curve")
    # Specular glint on the upper left.
    glint = shape_mask(lambda d: d.ellipse(p(cx - rx * .62, cy - ry * .72, cx - rx * .1, cy - ry * .42)))
    glow = Image.new("RGBA", (S, S), (255, 255, 240, 0))
    glow.putalpha(glint.filter(ImageFilter.GaussianBlur(10 * SS)).point(lambda v: int(v * .55)))
    canvas.alpha_composite(glow)


def main():
    if not os.path.isfile(FISH):
        sys.exit("Run from the mobile project folder.")
    canvas = layer()

    front = [(300, 500), (840, 500), (868, 1068), (272, 1068)]
    side = [(840, 500), (958, 540), (972, 1030), (868, 1068)]
    bag = shape_mask(lambda d: d.polygon([tuple(p(*pt)) for pt in front + side[1:3]]))

    # Contact shadow under the whole group.
    ground = shape_mask(lambda d: d.ellipse(p(210, 1040, 1100, 1140)))
    canvas.alpha_composite(shadow(ground, (0, 0), 26, .32))

    # Back handle, inside of the bag, then the contents peeking out.
    rope(canvas, arc_points((560, 470), (900, 500), 250), 20, (120, 78, 38, 255), (160, 112, 62, 255))
    inside = shape_mask(lambda d: d.polygon([tuple(p(*pt)) for pt in [(300, 500), (390, 450), (975, 478), (958, 540), (840, 500)]]))
    canvas.alpha_composite(fill_gradient(inside, "#5A3A1E", "#7A5230", *p(450, 540)))
    paste_render(canvas, FERN, 520, (690, 380), angle=-6)
    paste_render(canvas, FISH, 560, (520, 400), angle=16)

    # Bag body: kraft front lit from the left, darker side, folded rim and paper grain.
    canvas.alpha_composite(shadow(bag, (0, 0), 6, .25, (60, 35, 15)))
    front_mask = shape_mask(lambda d: d.polygon([tuple(p(*pt)) for pt in front]))
    canvas.alpha_composite(fill_gradient(front_mask, "#E6BB7C", "#C38C50", *p(500, 1068), light_from_left=.07, noise=5, seed=3))
    side_mask = shape_mask(lambda d: d.polygon([tuple(p(*pt)) for pt in side]))
    canvas.alpha_composite(fill_gradient(side_mask, "#B88148", "#94622F", *p(540, 1068), noise=5, seed=4))
    rim = shape_mask(lambda d: d.polygon([tuple(p(*pt)) for pt in [(300, 500), (840, 500), (843, 556), (297, 556)]]))
    canvas.alpha_composite(fill_gradient(rim, "#F0CD93", "#E2B474", *p(500, 556), light_from_left=.05, noise=4, seed=5))
    draw = ImageDraw.Draw(canvas)
    draw.line([tuple(p(297, 558)), tuple(p(843, 558))], fill=(150, 100, 52, 255), width=5 * SS)
    draw.line([tuple(p(843, 558)), tuple(p(960, 596))], fill=(120, 80, 40, 255), width=4 * SS)
    # Side gusset crease.
    draw.line([tuple(p(900, 520)), tuple(p(918, 1050))], fill=(126, 84, 42, 160), width=4 * SS)
    for outline in (front, side):
        draw.line([tuple(p(*pt)) for pt in outline + outline[:1]], fill=(132, 86, 40, 255), width=6 * SS, joint="curve")

    # Cream label with the same fish mark as the food jar.
    label = shape_mask(lambda d: d.rounded_rectangle(p(430, 690, 710, 880), radius=26 * SS))
    canvas.alpha_composite(shadow(label, (0, 6), 6, .25))
    canvas.alpha_composite(fill_gradient(label, "#FBF3DC", "#EADBB4", *p(690, 880)))
    draw.rounded_rectangle(p(430, 690, 710, 880), radius=26 * SS, outline=(74, 122, 58, 255), width=8 * SS)
    green = (74, 110, 58, 255)
    draw.ellipse(p(500, 750, 620, 822), fill=green)
    draw.polygon([tuple(p(612, 786)), tuple(p(660, 750)), tuple(p(660, 822))], fill=green)
    draw.ellipse(p(522, 772, 536, 786), fill=(251, 243, 220, 255))

    # Front handle over the contents, tied into the rim.
    rope(canvas, arc_points((420, 530), (700, 530), 360), 22, (128, 84, 40, 255), (178, 128, 74, 255))
    for x in (420, 700):
        draw.ellipse(p(x - 16, 514, x + 16, 546), fill=(92, 60, 28, 255))

    # Coins in front: one lying flat, one standing.
    coin(canvas, (330, 1062), 92, tilt=.42)
    coin(canvas, (1010, 1050), 110, tilt=.42)
    coin(canvas, (900, 930), 132)

    canvas.resize((SIZE, SIZE), Image.LANCZOS).save(OUT, optimize=True)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
