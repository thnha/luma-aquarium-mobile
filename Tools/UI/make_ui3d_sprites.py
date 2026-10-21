"""Generates the 2.5D nine-slice surfaces used by the redesigned UI.

Each surface is a rounded face over a solid edge: the edge shows on every side
and is thicker at the bottom, giving the raised look approved in
docs/ui-redesign.md. Run from the mobile folder:

    python3 Tools/UI/make_ui3d_sprites.py

Writes PNGs to Assets/_Project/UI/Surfaces and prints the manifest borders.
"""
import json
import os
import sys

from PIL import Image, ImageDraw

OUT = "Assets/_Project/UI/Surfaces"
SS = 4  # supersampling factor for smooth corners

MOSS = "#4A7A3A"
PRIMARY_EDGE = "#3F6B32"
SAND_EDGE = "#BFA978"


def rgb(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def gradient(size, top, bottom):
    w, h = size
    image = Image.new("RGBA", size)
    a, b = rgb(top), rgb(bottom)
    draw = ImageDraw.Draw(image)
    for y in range(h):
        t = y / max(1, h - 1)
        draw.line([(0, y), (w, y)], fill=tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3)) + (255,))
    return image


def mask(size, box, radius):
    m = Image.new("L", size, 0)
    ImageDraw.Draw(m).rounded_rectangle(box, radius=radius, fill=255)
    return m


def surface(name, w, h, radius, edge, bottom, edge_color, top, bottom_color, highlight=0, highlight_alpha=255, pressed=False, highlight_color="#FFFFFF"):
    """Draws an edge-coloured rounded block and the face inset by `edge` (+`bottom` below)."""
    W, H, R, E, B = w * SS, h * SS, radius * SS, edge * SS, bottom * SS
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    # Pressed variants drop the face onto the bottom edge but keep the same outer size.
    shift = B if pressed else 0
    outer_top = shift
    if edge_color:
        canvas.paste(Image.new("RGBA", (W, H), rgb(edge_color) + (255,)), (0, 0), mask((W, H), (0, outer_top, W - 1, H - 1), R))
    face_box = (E, outer_top + E, W - 1 - E, H - 1 - E - (0 if pressed else B))
    face_radius = max(SS, R - E)
    face = gradient((W, H), top, bottom_color)
    canvas.paste(face, (0, 0), mask((W, H), face_box, face_radius))
    if highlight:
        hl = Image.new("RGBA", (W, H), rgb(highlight_color) + (highlight_alpha,))
        band = mask((W, H), face_box, face_radius)
        lower = mask((W, H), (face_box[0], face_box[1] + highlight * SS, face_box[2], face_box[3] + highlight * SS), face_radius)
        band = Image.composite(Image.new("L", (W, H), 0), band, lower)
        canvas.paste(hl, (0, 0), band)
    image = canvas.resize((w, h), Image.LANCZOS)
    image.save(os.path.join(OUT, name + ".png"), optimize=True)
    pad = 2
    return [radius + pad, radius + pad, radius + pad, radius + bottom + pad]


def ring(name, d, inner, outer):
    W = d * SS
    image = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    draw = ImageDraw.Draw(image)
    draw.ellipse((0, 0, W - 1, W - 1), fill=rgb(MOSS) + (255,))
    o = outer * SS
    draw.ellipse((o, o, W - 1 - o, W - 1 - o), fill=rgb("#F6EFDD") + (255,))
    i = (outer + inner) * SS
    draw.ellipse((i, i, W - 1 - i, W - 1 - i), fill=(0, 0, 0, 0))
    image.resize((d, d), Image.LANCZOS).save(os.path.join(OUT, name + ".png"), optimize=True)


def main():
    if not os.path.isdir(OUT):
        sys.exit("Run from the mobile project folder.")
    borders = {}
    # Sizes are in 1080-wide canvas pixels: 2.77 x the approved 390px preview.
    borders["ui3d_panel"] = surface("ui3d_panel", 240, 240, 72, 8, 10, MOSS, "#F8F2E2", "#F3EAD3", 8, 255)
    borders["ui3d_dock"] = surface("ui3d_dock", 240, 240, 72, 8, 10, MOSS, "#FBF3DD", "#E6D6AC", 8, 220)
    borders["ui3d_chip"] = surface("ui3d_chip", 160, 156, 70, 7, 7, MOSS, "#FFF9E8", "#EBDDB6", 6, 255)
    borders["ui3d_button_primary"] = surface("ui3d_button_primary", 160, 160, 44, 6, 7, PRIMARY_EDGE, "#5E8F4A", "#45703A", 6, 255, highlight_color="#86B26E")
    borders["ui3d_button_primary_pressed"] = surface("ui3d_button_primary_pressed", 160, 160, 44, 6, 7, PRIMARY_EDGE, "#4F7C3E", "#3E6534", 4, 255, pressed=True, highlight_color="#6E9A5A")
    borders["ui3d_button_secondary"] = surface("ui3d_button_secondary", 160, 160, 44, 6, 6, SAND_EDGE, "#FFF9E8", "#EBDDB6", 6, 255)
    borders["ui3d_button_secondary_pressed"] = surface("ui3d_button_secondary_pressed", 160, 160, 44, 6, 6, SAND_EDGE, "#F1E7CC", "#E1D1A6", 4, 200, pressed=True)
    borders["ui3d_button_disabled"] = surface("ui3d_button_disabled", 160, 160, 44, 6, 6, "#C9C1AA", "#DDD7C6", "#D6CFBC")
    borders["ui3d_tile"] = surface("ui3d_tile", 160, 160, 44, 6, 6, SAND_EDGE, "#FFFCF2", "#F1E5C4", 6, 255)
    borders["ui3d_tile_pressed"] = surface("ui3d_tile_pressed", 160, 160, 44, 6, 6, SAND_EDGE, "#F5EDD6", "#E7D9B2", 4, 200, pressed=True)
    borders["ui3d_tile_selected"] = surface("ui3d_tile_selected", 160, 160, 44, 6, 6, "#5E8F4A", "#E8F6D6", "#C2E0A4", 6, 255, highlight_color="#F7FCF0")
    borders["ui3d_tile_empty"] = surface("ui3d_tile_empty", 160, 160, 44, 6, 6, "#D6CCB0", "#F1EADA", "#E9E1CC")
    borders["ui3d_tab_tray"] = surface("ui3d_tab_tray", 128, 128, 40, 7, 7, MOSS, "#3E5F33", "#2E4A27", 5, 255, highlight_color="#557A48")
    borders["ui3d_tab_tray_light"] = surface("ui3d_tab_tray_light", 128, 128, 40, 5, 4, SAND_EDGE, "#E9DDBC", "#E2D4AE")
    borders["ui3d_tab_on"] = surface("ui3d_tab_on", 128, 128, 32, 0, 5, SAND_EDGE, "#FFF9E8", "#EBDDB6", 5, 255)
    borders["ui3d_track"] = surface("ui3d_track", 64, 48, 22, 5, 0, MOSS, "#D9CCA8", "#E7DCBE")
    borders["ui3d_fill"] = surface("ui3d_fill", 64, 40, 18, 0, 0, None, "#86B86A", "#558A43", 6, 255, highlight_color="#A9D490")
    borders["ui3d_badge"] = surface("ui3d_badge", 64, 52, 24, 0, 0, None, "#4F7C3E", "#45703A")
    borders["ui3d_badge_light"] = surface("ui3d_badge_light", 64, 52, 24, 0, 0, None, "#E9DDBC", "#E9DDBC")
    borders["ui3d_hint"] = surface("ui3d_hint", 96, 96, 34, 0, 0, None, "#2E4A27", "#2E4A27")
    # Circles are drawn whole, not sliced.
    surface("ui3d_round", 128, 128, 64, 7, 7, MOSS, "#86B86A", "#558A43", 6, 255, highlight_color="#A9D490")
    surface("ui3d_round_pressed", 128, 128, 64, 7, 7, MOSS, "#76A65C", "#4B7C3B", 4, 255, pressed=True, highlight_color="#93C27A")
    surface("ui3d_round_light", 128, 128, 64, 6, 6, SAND_EDGE, "#F3E8C9", "#DECDA0", 6, 255)
    ring("ui3d_ring", 160, 14, 9)
    print(json.dumps(borders, indent=1))


if __name__ == "__main__":
    main()
