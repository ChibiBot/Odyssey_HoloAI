#!/usr/bin/env python3
"""HoloCore building art — Odyssey grav-engine style, all rotations.

Renders Textures/HoloAI/Buildings/HoloCore_{south,east,north}.png (west is the
game's mirrored east). Run from the repo root. Palette sampled from
Concept/grav_core_sample.png (vanilla grav engine crop).
"""
from PIL import Image, ImageDraw, ImageFilter

S = 4
W, H = 256 * S, 320 * S
OUTLINE = (18, 29, 29)
BODY_HI = (100, 142, 150)
BODY = (70, 106, 110)
BODY_LO = (52, 80, 88)
PANEL = (46, 71, 74)
VENT = (24, 38, 40)
GLOW_OUT = (80, 172, 189)
GLOW_MID = (132, 213, 220)
GLOW_HOT = (230, 250, 252)


def s(*v):
    return [x * S for x in v]


def layer():
    im = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def cabinet(img, face):
    sh, d = layer()
    d.ellipse(s(14, 286, 242, 316), fill=(10, 16, 16, 110))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(4 * S)))

    body, d = layer()
    poly = s(38, 150, 218, 150, 240, 172, 240, 286, 218, 306, 38, 306, 16, 286, 16, 172)
    d.polygon(poly, fill=BODY + (255,))
    grad = Image.new("L", (1, H), 0)
    for y in range(H):
        t = min(max((y - 148 * S) / (162 * S), 0), 1)
        grad.putpixel((0, y), int(70 * t))
    grad = grad.resize((W, H))
    dark = Image.new("RGBA", (W, H), BODY_LO + (255,))
    dark.putalpha(grad)
    mask, md = layer()
    md.polygon(poly, fill=(255, 255, 255, 255))
    body.alpha_composite(Image.composite(dark, Image.new("RGBA", (W, H), (0, 0, 0, 0)), mask.split()[3]))
    bez, bd = layer()
    bd.polygon(s(38, 150, 218, 150, 240, 172, 16, 172), fill=BODY_HI + (190,))
    body.alpha_composite(bez.filter(ImageFilter.GaussianBlur(2.5 * S)))
    img.alpha_composite(body)

    ol, od = layer()
    od.polygon(poly, outline=OUTLINE + (255,))
    ol = ol.filter(ImageFilter.GaussianBlur(int(0.9 * S)))
    img.alpha_composite(ol)
    img.alpha_composite(ol)

    det, d = layer()
    if face == "south":
        d.line(s(128, 176, 128, 296), fill=PANEL + (190,), width=S)
        d.line(s(24, 236, 232, 236), fill=PANEL + (140,), width=S)
        for row in range(2):
            for col in range(3):
                x0, y0 = 44 + col * 26, 248 + row * 24
                d.rounded_rectangle(s(x0, y0, x0 + 18, y0 + 14), 2 * S, fill=VENT + (255,))
                d.line(s(x0 + 2, y0 + 2, x0 + 16, y0 + 2), fill=(130, 170, 176, 90), width=S)
        for i, cx in enumerate((150, 168, 186)):
            col = GLOW_MID + (210,) if i < 2 else (156, 130, 224, 210)
            d.ellipse(s(cx, 252, cx + 10, 262), fill=col)
        d.rounded_rectangle(s(148, 272, 214, 290), 3 * S, fill=PANEL + (230,))
        d.line(s(154, 281, 208, 281), fill=GLOW_OUT + (150,), width=S)
        d.rounded_rectangle(s(40, 186, 116, 224), 4 * S, outline=PANEL + (150,), width=S)
        d.rounded_rectangle(s(140, 186, 216, 224), 4 * S, outline=PANEL + (150,), width=S)
    elif face == "east":
        # plainer side profile: one hatch, vertical conduit, vent strip low
        d.rounded_rectangle(s(52, 186, 148, 232), 4 * S, outline=PANEL + (160,), width=S)
        d.line(s(184, 160, 184, 300), fill=PANEL + (200,), width=2 * S)
        d.line(s(196, 160, 196, 300), fill=PANEL + (120,), width=S)
        for i in range(4):
            y0 = 246 + i * 14
            d.rounded_rectangle(s(56, y0, 130, y0 + 8), 2 * S, fill=VENT + (230,))
        d.ellipse(s(160, 250, 172, 262), fill=GLOW_MID + (200,))
    else:  # north (back)
        d.line(s(128, 176, 128, 296), fill=PANEL + (170,), width=S)
        # big access hatches
        d.rounded_rectangle(s(34, 184, 120, 260), 5 * S, outline=PANEL + (190,), width=S)
        d.rounded_rectangle(s(136, 184, 222, 260), 5 * S, outline=PANEL + (190,), width=S)
        for cx in (48, 68, 88):
            d.rounded_rectangle(s(cx, 270, cx + 14, 292), 2 * S, fill=VENT + (255,))
        # cable trunk
        d.line(s(160, 268, 214, 288), fill=OUTLINE + (200,), width=3 * S)
        d.line(s(160, 278, 208, 296), fill=PANEL + (200,), width=2 * S)
    img.alpha_composite(det.filter(ImageFilter.GaussianBlur(int(0.5 * S))))


def emitter(img, face):
    well, d = layer()
    if face == "east":
        box = s(66, 118, 190, 168)
    else:
        box = s(56, 116, 200, 170)
    d.ellipse(box, fill=(26, 42, 46, 255), outline=OUTLINE + (220,))
    img.alpha_composite(well)

    r1, d = layer()
    d.ellipse(s(66, 122, 190, 164) if face != "east" else s(76, 124, 180, 162),
              outline=GLOW_OUT + (255,), width=6 * S)
    img.alpha_composite(r1.filter(ImageFilter.GaussianBlur(2.2 * S)))
    r2, d = layer()
    d.ellipse(s(84, 130, 172, 158) if face != "east" else s(92, 132, 164, 156),
              outline=GLOW_MID + (255,), width=4 * S)
    img.alpha_composite(r2.filter(ImageFilter.GaussianBlur(1.4 * S)))
    c1, d = layer()
    d.ellipse(s(108, 134, 148, 156), fill=GLOW_MID + (255,))
    img.alpha_composite(c1.filter(ImageFilter.GaussianBlur(1.6 * S)))
    c2, d = layer()
    d.ellipse(s(118, 138, 138, 152), fill=GLOW_HOT + (255,))
    img.alpha_composite(c2.filter(ImageFilter.GaussianBlur(1.0 * S)))
    halo, d = layer()
    d.ellipse(s(70, 118, 186, 168), fill=GLOW_MID + (70,))
    img.alpha_composite(halo.filter(ImageFilter.GaussianBlur(6 * S)))


def hologram(img, face):
    beam, d = layer()
    d.polygon(s(122, 140, 134, 140, 140, 50, 116, 50), fill=GLOW_MID + (80,))
    img.alpha_composite(beam.filter(ImageFilter.GaussianBlur(2.4 * S)))
    pg, d = layer()
    d.polygon(s(128, 18, 154, 54, 128, 90, 102, 54), fill=GLOW_MID + (130,))
    img.alpha_composite(pg.filter(ImageFilter.GaussianBlur(5 * S)))
    pr, d = layer()
    if face == "east":
        # narrower profile of the prism from the side
        d.polygon(s(128, 24, 140, 54, 128, 84, 116, 54), fill=(178, 230, 236, 160))
        d.polygon(s(128, 24, 140, 54, 128, 84, 116, 54), outline=GLOW_HOT + (230,))
    else:
        d.polygon(s(128, 24, 148, 54, 128, 84, 108, 54), fill=(178, 230, 236, 160))
        d.polygon(s(128, 24, 148, 54, 128, 84, 108, 54), outline=GLOW_HOT + (230,))
    d.line(s(128, 24, 128, 84), fill=GLOW_HOT + (150,), width=S)
    img.alpha_composite(pr.filter(ImageFilter.GaussianBlur(int(0.8 * S))))


for face in ("south", "east", "north"):
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    cabinet(canvas, face)
    emitter(canvas, face)
    hologram(canvas, face)
    out = canvas.resize((256, 320), Image.LANCZOS)
    out.save(f"Textures/HoloAI/Buildings/HoloCore_{face}.png")
    print(f"saved HoloCore_{face}.png")
