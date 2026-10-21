"""Draws the Cửa hàng dock icon: a small shop house with a green tiled roof, striped
awning, a coin sign and the game's own java fern render in the window and by the door.

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


def poly(points):
    return [tuple(p(*pt)) for pt in points]


def main():
    if not os.path.isfile(FERN):
        sys.exit("Run from the mobile project folder.")
    canvas = layer()
    draw = ImageDraw.Draw(canvas)
    wood = (110, 72, 40, 255)

    ground = shape_mask(lambda d: d.ellipse(p(170, 1050, 1110, 1150)))
    canvas.alpha_composite(shadow(ground, (0, 0), 26, .32))

    # Walls: cream plaster front lit from the left, darker side for depth, wooden plinth.
    front = shape_mask(lambda d: d.polygon(poly([(250, 560), (830, 560), (830, 1080), (250, 1080)])))
    side = shape_mask(lambda d: d.polygon(poly([(830, 560), (990, 500), (990, 1020), (830, 1080)])))
    canvas.alpha_composite(shadow(front, (0, 0), 8, .3, (60, 40, 20)))
    canvas.alpha_composite(fill_gradient(front, "#FBF1D8", "#E4D0A4", *p(560, 1080), light_from_left=.05, noise=4, seed=2))
    canvas.alpha_composite(fill_gradient(side, "#D6BF8E", "#B79E6C", *p(500, 1080), noise=4, seed=3))
    plinth = shape_mask(lambda d: d.polygon(poly([(240, 1010), (830, 1010), (830, 1090), (240, 1090)])))
    canvas.alpha_composite(fill_gradient(plinth, "#9A6A3E", "#6E4524", *p(1010, 1090), noise=5, seed=4))
    side_plinth = shape_mask(lambda d: d.polygon(poly([(830, 1010), (990, 950), (990, 1030), (830, 1090)])))
    canvas.alpha_composite(fill_gradient(side_plinth, "#7A5130", "#5A3A20", *p(950, 1090)))

    # Roof: forest-green tiles on the front slope, shaded side slope.
    roof_front = [(190, 470), (870, 470), (790, 250), (300, 250)]
    roof = shape_mask(lambda d: d.polygon(poly(roof_front)))
    roof_side = shape_mask(lambda d: d.polygon(poly([(870, 470), (1040, 400), (930, 200), (790, 250)])))
    canvas.alpha_composite(shadow(roof, (0, 14), 12, .35, (30, 40, 20)))
    canvas.alpha_composite(fill_gradient(roof, "#6E9A55", "#3F6B32", *p(250, 470), light_from_left=.06, noise=4, seed=5))
    canvas.alpha_composite(fill_gradient(roof_side, "#4E7A3E", "#2F5226", *p(200, 470), noise=4, seed=6))
    for row in range(1, 4):
        y = 250 + row * 55
        t = (y - 250) / 220
        draw.line(poly([(306 - 110 * t, y), (784 + 80 * t, y)]), fill=(52, 88, 40, 200), width=5 * SS)
    draw.line(poly([(300, 250), (790, 250), (930, 200)]), fill=(150, 190, 120, 255), width=8 * SS)
    draw.line(poly(roof_front + roof_front[:1]), fill=(44, 72, 34, 255), width=6 * SS, joint="curve")
    draw.line(poly([(870, 470), (1040, 400), (930, 200)]), fill=(36, 60, 28, 255), width=6 * SS, joint="curve")

    # Striped awning with a scalloped hem, wrapping onto the side.
    top, bottom, stripes = 500, 610, 6
    width = (850 - 230) / stripes
    for i in range(stripes):
        x0 = 230 + i * width
        colours = ("#5E8F4A", "#45703A") if i % 2 == 0 else ("#FBF3DC", "#E8D7AE")
        stripe = shape_mask(lambda d, x0=x0, i=i: (
            d.polygon(poly([(x0 + 10 * (i - 3) / 3, top), (x0 + width + 10 * (i - 2) / 3, top), (x0 + width, bottom), (x0, bottom)])),
            d.ellipse(p(x0, bottom - width / 2, x0 + width, bottom + width / 2))))
        canvas.alpha_composite(fill_gradient(stripe, *colours, *p(top, bottom + width / 2), noise=3, seed=7 + i))
    awning_side = shape_mask(lambda d: d.polygon(poly([(850, top - 30), (1030, 410), (1010, 530), (850, bottom)])))
    canvas.alpha_composite(fill_gradient(awning_side, "#3F6B32", "#2F5226", *p(440, bottom)))
    draw.line(poly([(222, top), (858, top)]), fill=(44, 72, 34, 255), width=7 * SS)

    # Shop window: glass with a reflection, a fern inside, wooden frame and sill.
    window = (300, 700, 540, 900)
    glass = shape_mask(lambda d: d.rounded_rectangle(p(*window), radius=18 * SS))
    canvas.alpha_composite(fill_gradient(glass, "#BFE6EE", "#6FB3C4", *p(700, 900)))
    paste_render(canvas, FERN, 170, (420, 830))
    glint = shape_mask(lambda d: d.polygon(poly([(320, 880), (390, 715), (430, 715), (360, 880)])))
    shine = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    shine.putalpha(glint.point(lambda v: int(v * .45)))
    canvas.alpha_composite(shine)
    draw.rounded_rectangle(p(*window), radius=18 * SS, outline=wood, width=14 * SS)
    draw.line(poly([(420, 704), (420, 896)]), fill=wood, width=10 * SS)
    sill = shape_mask(lambda d: d.rounded_rectangle(p(285, 895, 555, 925), radius=8 * SS))
    canvas.alpha_composite(fill_gradient(sill, "#A9784A", "#7A5130", *p(895, 925)))

    # Arched wooden door with planks and a brass knob.
    door = shape_mask(lambda d: (d.rectangle(p(610, 800, 770, 1015)), d.ellipse(p(610, 720, 770, 880))))
    canvas.alpha_composite(shadow(door, (0, 0), 5, .3))
    canvas.alpha_composite(fill_gradient(door, "#B07A48", "#7E5130", *p(720, 1015), light_from_left=.05, noise=5, seed=11))
    for x in (663, 717):
        draw.line(poly([(x, 745), (x, 1012)]), fill=(96, 60, 32, 200), width=5 * SS)
    draw.ellipse(p(735, 880, 757, 902), fill=(240, 200, 100, 255), outline=(150, 110, 40, 255), width=3 * SS)
    frame = (90, 56, 28, 255)
    draw.line(poly([(610, 1015), (610, 800)]), fill=frame, width=8 * SS)
    draw.line(poly([(770, 800), (770, 1015)]), fill=frame, width=8 * SS)
    draw.arc(p(610, 720, 770, 880), 180, 360, fill=frame, width=8 * SS)

    # Coin sign on the ridge, a potted fern by the door and coins on the step.
    coin(canvas, (545, 150), 108)
    pot = shape_mask(lambda d: d.polygon(poly([(860, 1000), (960, 1000), (945, 1110), (875, 1110)])))
    canvas.alpha_composite(shadow(pot, (0, 10), 10, .3))
    canvas.alpha_composite(fill_gradient(pot, "#D98A5A", "#A85C34", *p(1000, 1110), light_from_left=.05))
    draw.rounded_rectangle(p(850, 985, 970, 1015), radius=8 * SS, fill=(196, 118, 72, 255))
    paste_render(canvas, FERN, 220, (910, 900), angle=4)
    coin(canvas, (230, 1080), 78, tilt=.42)
    coin(canvas, (300, 1040), 70)

    canvas.resize((SIZE, SIZE), Image.LANCZOS).save(OUT, optimize=True)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
