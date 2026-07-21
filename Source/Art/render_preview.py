#!/usr/bin/env python3
"""Mod preview / key art: the HoloCore_Projection concept cropped to 16:9 with
the mod title in RimWorld-logo style (heavy ivory letters, dark outline, soft
shadow) — and a five-point star sitting in the second 'o' of "HoloAI".

Outputs About/Preview.png (640x360) and Concept/HoloCore_Projection_Titled.png
(full-res 1536x864). Run from the repo root.
"""
import math

from PIL import Image, ImageDraw, ImageFilter, ImageFont

FONT_PATH = "/usr/share/fonts/TTF/FiraSans-Heavy.ttf"
TITLE = "Ship HoloAI"
STAR_PREFIX = "Ship Hol"  # the star lives in the very next character's counter

ART_W, ART_H = 1536, 864
TEXT_SCALE = 2  # text layer supersample relative to the 1536x864 canvas

IVORY = (242, 233, 205, 255)
IVORY_DIM = (208, 193, 156, 255)
OUTLINE = (38, 28, 16, 255)


def star_points(cx, cy, r_outer, r_inner, rotation=-90):
    pts = []
    for i in range(10):
        r = r_outer if i % 2 == 0 else r_inner
        a = math.radians(rotation + i * 36)
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def build_title_layer():
    w, h = ART_W * TEXT_SCALE, ART_H * TEXT_SCALE
    font = ImageFont.truetype(FONT_PATH, 300)
    probe = ImageDraw.Draw(Image.new("RGBA", (4, 4)))
    bbox = probe.textbbox((0, 0), TITLE, font=font)
    text_w = bbox[2] - bbox[0]
    x0 = (w - text_w) / 2 - bbox[0]
    y0 = 640 * TEXT_SCALE - bbox[1]

    # The star: centered on the counter of the character after STAR_PREFIX.
    o_left = x0 + probe.textlength(STAR_PREFIX, font=font)
    o_w = probe.textlength("o", font=font)
    o_box = probe.textbbox((0, 0), "o", font=font)
    o_cx = o_left + o_w / 2
    o_cy = y0 + (o_box[1] + o_box[3]) / 2
    star_r = (o_box[3] - o_box[1]) * 0.30

    def draw_title(d, offset, fill, stroke, stroke_fill):
        d.text((x0 + offset[0], y0 + offset[1]), TITLE, font=font, fill=fill,
               stroke_width=stroke, stroke_fill=stroke_fill)

    # Drop shadow.
    shadow = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(shadow)
    draw_title(d, (10, 14), (0, 0, 0, 200), 18, (0, 0, 0, 200))
    d.polygon(star_points(o_cx + 10, o_cy + 14, star_r * 1.45, star_r * 0.62),
              fill=(0, 0, 0, 200))
    shadow = shadow.filter(ImageFilter.GaussianBlur(9))

    # Letters: dark outline pass, then ivory fill with a subtle vertical
    # gradient (lighter top) for the RimWorld-logo sheen.
    letters = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(letters)
    draw_title(d, (0, 0), IVORY, 16, OUTLINE)

    grad = Image.new("L", (1, h), 0)
    for y in range(h):
        t = min(max((y - y0 - 40) / 560, 0), 1)
        grad.putpixel((0, y), int(255 * t))
    grad = grad.resize((w, h))
    dim = Image.new("RGBA", (w, h), IVORY_DIM)
    dim.putalpha(Image.composite(grad, Image.new("L", (w, h), 0), letters.split()[3]))
    fill_mask = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    fd = ImageDraw.Draw(fill_mask)
    draw_title(fd, (0, 0), (255, 255, 255, 255), 0, None)
    letters.alpha_composite(Image.composite(
        dim, Image.new("RGBA", (w, h), (0, 0, 0, 0)), fill_mask.split()[3]))

    # The five-point star in the o's counter — outlined like the letters so it
    # reads as part of the logotype.
    d = ImageDraw.Draw(letters)
    d.polygon(star_points(o_cx, o_cy, star_r * 1.45, star_r * 0.62),
              fill=IVORY, outline=OUTLINE, width=6)

    out = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    out.alpha_composite(shadow)
    out.alpha_composite(letters)
    return out.resize((ART_W, ART_H), Image.LANCZOS)


art = Image.open("Concept/HoloCore_Projection.png").convert("RGBA")
# 1536x1024 -> 16:9 band keeping her face and the projection ring.
art = art.crop((0, 60, 1536, 60 + 864))

# A whisper of darkening behind the title so ivory reads over the base glow.
vign = Image.new("L", (1, ART_H), 0)
for y in range(ART_H):
    t = min(max((y - 560) / 300, 0), 1)
    vign.putpixel((0, y), int(130 * t))
dark = Image.new("RGBA", (ART_W, ART_H), (4, 6, 12, 255))
dark.putalpha(vign.resize((ART_W, ART_H)))
art.alpha_composite(dark)

art.alpha_composite(build_title_layer())

art.save("Concept/HoloCore_Projection_Titled.png")
art.resize((640, 360), Image.LANCZOS).convert("RGB").save("About/Preview.png")
print("saved Concept/HoloCore_Projection_Titled.png and About/Preview.png")
