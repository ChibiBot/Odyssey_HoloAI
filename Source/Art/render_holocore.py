#!/usr/bin/env python3
"""HoloCore building art — Odyssey grav-engine style, all rotations, top-down.

Renders Textures/HoloAI/Buildings/HoloCore_{south,east,north}.png (west is the
game's mirrored east). Run from the repo root. Palette sampled from
Concept/grav_core_sample.png (vanilla grav engine crop).

Perspective: RimWorld's near-overhead camera — the top deck dominates (the
emitter well reads almost circular), with a shallow front strip along the
bottom edge carrying each face's identity (south console / east side profile /
north service back). The deck occupies the bottom 256px of the 320px canvas
(the 2x2 footprint under drawSize (2, 2.5) + drawOffset z 0.25); the headroom
above holds the prism halo.
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


# Deck plate: chamfered slab, top edge pulled in a touch for the hint of
# perspective that keeps it from reading as a flat decal.
DECK = (46, 76, 210, 76, 240, 104, 240, 252, 16, 252, 16, 104)
# Front strip: the shallow south-facing face below the deck lip.
STRIP = (16, 252, 240, 252, 240, 288, 224, 304, 32, 304, 16, 288)


def chassis(img, face):
    sh, d = layer()
    d.ellipse(s(12, 262, 244, 314), fill=(10, 16, 16, 110))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(4 * S)))

    # Front strip first (deck lip overlaps it).
    strip, d = layer()
    d.polygon(s(*STRIP), fill=BODY_LO + (255,))
    grad = Image.new("L", (1, H), 0)
    for y in range(H):
        t = min(max((y - 252 * S) / (52 * S), 0), 1)
        grad.putpixel((0, y), int(80 * t))
    grad = grad.resize((W, H))
    dark = Image.new("RGBA", (W, H), (30, 48, 54, 255))
    dark.putalpha(grad)
    mask, md = layer()
    md.polygon(s(*STRIP), fill=(255, 255, 255, 255))
    strip.alpha_composite(Image.composite(dark, Image.new("RGBA", (W, H), (0, 0, 0, 0)), mask.split()[3]))
    img.alpha_composite(strip)

    # Deck plate with a soft top-lit gradient (brighter toward the top edge).
    deck, d = layer()
    d.polygon(s(*DECK), fill=BODY + (255,))
    grad = Image.new("L", (1, H), 0)
    for y in range(H):
        t = min(max((y - 76 * S) / (176 * S), 0), 1)
        grad.putpixel((0, y), int(55 * (1 - t)))
    grad = grad.resize((W, H))
    lite = Image.new("RGBA", (W, H), BODY_HI + (255,))
    lite.putalpha(grad)
    mask, md = layer()
    md.polygon(s(*DECK), fill=(255, 255, 255, 255))
    deck.alpha_composite(Image.composite(lite, Image.new("RGBA", (W, H), (0, 0, 0, 0)), mask.split()[3]))
    # Deck lip highlight along the bottom edge (catches the light above the strip).
    bez, bd = layer()
    bd.line(s(16, 250, 240, 250), fill=BODY_HI + (200,), width=2 * S)
    deck.alpha_composite(bez.filter(ImageFilter.GaussianBlur(1.5 * S)))
    img.alpha_composite(deck)

    for poly in (DECK, STRIP):
        ol, od = layer()
        od.polygon(s(*poly), outline=OUTLINE + (255,))
        ol = ol.filter(ImageFilter.GaussianBlur(int(0.9 * S)))
        img.alpha_composite(ol)
        img.alpha_composite(ol)

    # Deck furniture: quarter seams + corner bolts shared by all faces.
    det, d = layer()
    d.line(s(128, 80, 128, 96), fill=PANEL + (170,), width=S)
    d.line(s(128, 236, 128, 250), fill=PANEL + (170,), width=S)
    d.line(s(20, 166, 52, 166), fill=PANEL + (150,), width=S)
    d.line(s(204, 166, 236, 166), fill=PANEL + (150,), width=S)
    for bx, by in ((28, 92), (222, 92), (26, 234), (224, 234)):
        d.ellipse(s(bx, by, bx + 7, by + 7), fill=PANEL + (220,), outline=OUTLINE + (160,))

    if face == "south":
        # Console face: vent bank, status lights, readout bar on the strip;
        # two shallow panel outlines on the deck's top corners.
        d.rounded_rectangle(s(26, 84, 96, 118), 4 * S, outline=PANEL + (150,), width=S)
        d.rounded_rectangle(s(160, 84, 230, 118), 4 * S, outline=PANEL + (150,), width=S)
        d.line(s(128, 258, 128, 298), fill=PANEL + (190,), width=S)
        for col in range(3):
            x0 = 40 + col * 26
            d.rounded_rectangle(s(x0, 264, x0 + 18, 278), 2 * S, fill=VENT + (255,))
            d.line(s(x0 + 2, y2 := 266, x0 + 16, y2), fill=(130, 170, 176, 90), width=S)
        for i, cx in enumerate((146, 164, 182)):
            col = GLOW_MID + (210,) if i < 2 else (156, 130, 224, 210)
            d.ellipse(s(cx, 262, cx + 10, 272), fill=col)
        d.rounded_rectangle(s(144, 280, 212, 296), 3 * S, fill=PANEL + (230,))
        d.line(s(150, 288, 206, 288), fill=GLOW_OUT + (150,), width=S)
    elif face == "east":
        # Side profile: conduit running down the deck's right edge, vent
        # slats and a single hatch on the strip.
        d.line(s(216, 82, 216, 248), fill=PANEL + (200,), width=2 * S)
        d.line(s(226, 82, 226, 248), fill=PANEL + (120,), width=S)
        d.rounded_rectangle(s(30, 86, 108, 122), 4 * S, outline=PANEL + (150,), width=S)
        for i in range(3):
            y0 = 262 + i * 13
            d.rounded_rectangle(s(36, y0, 118, y0 + 8), 2 * S, fill=VENT + (230,))
        d.rounded_rectangle(s(140, 262, 208, 296), 3 * S, outline=PANEL + (170,), width=S)
        d.ellipse(s(216, 272, 228, 284), fill=GLOW_MID + (200,))
    else:  # north (service back)
        # Big access hatches on the deck, vents + cable trunk on the strip.
        d.rounded_rectangle(s(24, 82, 100, 132), 5 * S, outline=PANEL + (190,), width=S)
        d.rounded_rectangle(s(156, 82, 232, 132), 5 * S, outline=PANEL + (190,), width=S)
        d.line(s(128, 258, 128, 298), fill=PANEL + (170,), width=S)
        for cx in (38, 60, 82):
            d.rounded_rectangle(s(cx, 264, cx + 14, 292), 2 * S, fill=VENT + (255,))
        d.line(s(150, 268, 214, 288), fill=OUTLINE + (200,), width=3 * S)
        d.line(s(150, 278, 208, 296), fill=PANEL + (200,), width=2 * S)
    img.alpha_composite(det.filter(ImageFilter.GaussianBlur(int(0.5 * S))))


def emitter(img, face):
    # The emitter well seen from almost straight above: a wide, near-circular
    # dish centered on the deck.
    well, d = layer()
    d.ellipse(s(54, 96, 202, 232), fill=(26, 42, 46, 255), outline=OUTLINE + (220,))
    img.alpha_composite(well)

    r1, d = layer()
    d.ellipse(s(64, 106, 192, 222), outline=GLOW_OUT + (255,), width=6 * S)
    img.alpha_composite(r1.filter(ImageFilter.GaussianBlur(2.2 * S)))
    r2, d = layer()
    d.ellipse(s(84, 124, 172, 204), outline=GLOW_MID + (255,), width=4 * S)
    img.alpha_composite(r2.filter(ImageFilter.GaussianBlur(1.4 * S)))
    c1, d = layer()
    d.ellipse(s(106, 144, 150, 184), fill=GLOW_MID + (255,))
    img.alpha_composite(c1.filter(ImageFilter.GaussianBlur(1.6 * S)))
    c2, d = layer()
    d.ellipse(s(117, 154, 139, 174), fill=GLOW_HOT + (255,))
    img.alpha_composite(c2.filter(ImageFilter.GaussianBlur(1.0 * S)))
    halo, d = layer()
    d.ellipse(s(58, 100, 198, 228), fill=GLOW_MID + (70,))
    img.alpha_composite(halo.filter(ImageFilter.GaussianBlur(6 * S)))


def hologram(img, face):
    # The projected prism floats ABOVE the well — drawn a step north of the
    # dish's hot center so the two parallax apart and she reads as levitating,
    # her halo spilling into the headroom.
    pg, d = layer()
    d.polygon(s(128, 68, 162, 132, 128, 196, 94, 132), fill=GLOW_MID + (110,))
    img.alpha_composite(pg.filter(ImageFilter.GaussianBlur(6 * S)))
    pr, d = layer()
    if face == "east":
        pts = s(128, 84, 150, 132, 128, 180, 106, 132)
    else:
        pts = s(128, 80, 156, 132, 128, 184, 100, 132)
    d.polygon(pts, fill=(178, 230, 236, 150))
    d.polygon(pts, outline=GLOW_HOT + (230,))
    d.line(s(128, 84, 128, 180), fill=GLOW_HOT + (150,), width=S)
    d.line(s(104, 132, 152, 132), fill=GLOW_HOT + (110,), width=S)
    img.alpha_composite(pr.filter(ImageFilter.GaussianBlur(int(0.8 * S))))
    # Faint shimmer rising into the headroom so the projector still reads as
    # projecting, even from above.
    sh, d = layer()
    d.ellipse(s(96, 16, 160, 108), fill=GLOW_MID + (46,))
    img.alpha_composite(sh.filter(ImageFilter.GaussianBlur(8 * S)))


for face in ("south", "east", "north"):
    canvas = Image.new("RGBA", (W, H), (0, 0, 0, 0))
    chassis(canvas, face)
    emitter(canvas, face)
    hologram(canvas, face)
    out = canvas.resize((256, 320), Image.LANCZOS)
    out.save(f"Textures/HoloAI/Buildings/HoloCore_{face}.png")
    print(f"saved HoloCore_{face}.png")
