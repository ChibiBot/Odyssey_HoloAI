#!/usr/bin/env python3
"""HoloAI gizmo (command) icons — white line-art glyphs with electric-cyan
accents, vanilla-command style. Renders Textures/HoloAI/UI/*.png at 75x75
(vanilla command size), supersampled 4x. Run from the repo root.

Icons:
- SwitchPersona: two different portrait busts framed side by side inside a
  circular double-headed swap arrow.
- ToggleProjection: emitter dish casting a light cone with a figure inside.
- Summon: descending beam chevrons onto a target ring.
- Hairstyle: flowing-hair bust with a small cycle arrow.
- Restyle: flowing-hair bust with styling sparkles.
"""
import math

from PIL import Image, ImageDraw, ImageFilter

S = 4
SIZE = 75
W = SIZE * S

WHITE = (255, 255, 255)
CYAN = (110, 210, 255)
WARM = (255, 205, 150)


def s(*v):
    return [x * S for x in v]


def layer():
    im = Image.new("RGBA", (W, W), (0, 0, 0, 0))
    return im, ImageDraw.Draw(im)


def soften(img, radius=0.5):
    return img.filter(ImageFilter.GaussianBlur(radius * S))


def arrowhead(d, tip, angle_deg, length=9, spread=26, color=WHITE + (255,)):
    """Filled triangular head; tip at `tip`, pointing along angle_deg."""
    a = math.radians(angle_deg)
    left = math.radians(angle_deg + 180 - spread)
    right = math.radians(angle_deg + 180 + spread)
    tx, ty = tip
    pts = [
        (tx, ty),
        (tx + length * math.cos(left), ty + length * math.sin(left)),
        (tx + length * math.cos(right), ty + length * math.sin(right)),
    ]
    d.polygon([c * S for p in pts for c in p], fill=color)


def swap_arc(d, center, radius, start, end, width=4, color=WHITE + (255,)):
    cx, cy = center
    box = s(cx - radius, cy - radius, cx + radius, cy + radius)
    d.arc(box, start, end, fill=color, width=width * S)


def bust(d, cx, cy, scale, hair, color):
    """Head-and-shoulders silhouette. hair: 'long' or 'bob'."""
    r = 6.5 * scale
    if hair == "long":
        # Hair falls behind the shoulders on both sides.
        d.polygon(s(cx - r - 2 * scale, cy - 2 * scale,
                    cx - r + 1 * scale, cy - r - 1 * scale,
                    cx, cy - r - 2.5 * scale,
                    cx + r - 1 * scale, cy - r - 1 * scale,
                    cx + r + 2 * scale, cy - 2 * scale,
                    cx + r + 1 * scale, cy + 9 * scale,
                    cx - r - 1 * scale, cy + 9 * scale),
                  fill=color)
    else:
        # Chin-length bob: rounded cap that flares slightly at the jaw.
        d.polygon(s(cx - r - 1.5 * scale, cy + 3 * scale,
                    cx - r, cy - r - 1 * scale,
                    cx, cy - r - 2.5 * scale,
                    cx + r, cy - r - 1 * scale,
                    cx + r + 1.5 * scale, cy + 3 * scale),
                  fill=color)
    # Face.
    d.ellipse(s(cx - r + 1.6 * scale, cy - r + 1.2 * scale,
                cx + r - 1.6 * scale, cy + r - 0.6 * scale),
              fill=(24, 34, 44, 255))
    # Shoulders.
    d.polygon(s(cx - 9 * scale, cy + 12 * scale,
                cx - 5 * scale, cy + 6.5 * scale,
                cx + 5 * scale, cy + 6.5 * scale,
                cx + 9 * scale, cy + 12 * scale),
              fill=color)


def save(img, name):
    out = img.resize((SIZE, SIZE), Image.LANCZOS)
    out.save(f"Textures/HoloAI/UI/{name}.png")
    print(f"saved {name}.png")


# ---------------------------------------------------------------- SwitchPersona
def switch_persona():
    img, _ = layer()

    # Circular double-headed swap arrow: two arcs with rotational symmetry.
    arc, d = layer()
    swap_arc(d, (37.5, 37.5), 32, 205, 325)          # top arc
    swap_arc(d, (37.5, 37.5), 32, 25, 145)           # bottom arc
    # Heads at the arcs' leading ends, tangent to the circle (clockwise).
    for end_deg in (325, 145):
        a = math.radians(end_deg)
        tip = (37.5 + 32 * math.cos(a), 37.5 + 32 * math.sin(a))
        arrowhead(d, tip, end_deg + 100, length=10)
    img.alpha_composite(soften(arc, 0.4))

    # Two portrait frames side by side, one cyan, one warm — different personas.
    for cx, hair, tint in ((26.5, "long", CYAN), (48.5, "bob", WARM)):
        frame, d = layer()
        d.rounded_rectangle(s(cx - 11, 22, cx + 11, 53), 3 * S,
                            fill=(16, 24, 32, 235), outline=tint + (255,), width=S)
        img.alpha_composite(soften(frame, 0.3))
        b, d = layer()
        bust(d, cx, 34, 1.0, hair, tint + (255,))
        # Clip the bust to its frame.
        mask, md = layer()
        md.rounded_rectangle(s(cx - 10.5, 22.5, cx + 10.5, 52.5), 3 * S,
                             fill=(255, 255, 255, 255))
        img.paste(soften(b, 0.3), (0, 0), Image.composite(
            b.split()[3], Image.new("L", (W, W), 0), mask.split()[3]))
    return img


# ------------------------------------------------------------- ToggleProjection
def toggle_projection():
    img, _ = layer()
    # Light cone.
    cone, d = layer()
    d.polygon(s(30, 58, 45, 58, 60, 12, 15, 12), fill=CYAN + (70,))
    img.alpha_composite(soften(cone, 1.2))
    # Figure in the beam.
    fig, d = layer()
    d.ellipse(s(33, 15, 42, 24), fill=WHITE + (255,))
    d.polygon(s(29, 44, 31, 27, 37.5, 25, 44, 27, 46, 44), fill=WHITE + (235,))
    for y0 in (46, 50):
        d.line(s(31, y0, 44, y0), fill=CYAN + (200,), width=S)
    img.alpha_composite(soften(fig, 0.4))
    # Emitter dish.
    dish, d = layer()
    d.ellipse(s(22, 55, 53, 65), fill=(16, 24, 32, 240), outline=WHITE + (255,), width=S)
    d.ellipse(s(30, 57.5, 45, 62.5), fill=CYAN + (230,))
    img.alpha_composite(soften(dish, 0.4))
    return img


# ----------------------------------------------------------------------- Summon
def summon():
    img, _ = layer()
    # Target ring on the deck.
    ring, d = layer()
    d.ellipse(s(14, 50, 61, 66), outline=WHITE + (255,), width=2 * S)
    d.ellipse(s(24, 53.5, 51, 62.5), outline=CYAN + (220,), width=S)
    img.alpha_composite(soften(ring, 0.4))
    # Beam column.
    beam, d = layer()
    d.polygon(s(28, 56, 47, 56, 44, 8, 31, 8), fill=CYAN + (60,))
    img.alpha_composite(soften(beam, 1.0))
    # Descending chevrons.
    ch, d = layer()
    for y0, a in ((16, 255), (30, 255), (44, 255)):
        d.polygon(s(28, y0, 37.5, y0 + 8, 47, y0, 37.5, y0 + 4), fill=WHITE + (a,))
    img.alpha_composite(soften(ch, 0.4))
    return img


# ---------------------------------------------------------- Hairstyle / Restyle
def hair_bust():
    img, _ = layer()
    b, d = layer()
    # Big flowing-hair bust, off-center left to leave room for the accent.
    d.polygon(s(12, 30, 16, 14, 31, 8, 46, 14, 50, 30, 48, 62, 14, 62),
              fill=WHITE + (255,))
    d.ellipse(s(21, 18, 41, 42), fill=(24, 34, 44, 255))
    # Deep side-swept fringe (a white ellipse chopping the face's top with a
    # curve) plus a lock falling across the right cheek — this is what makes
    # the silhouette read as hair rather than a hood.
    d.ellipse(s(14, 6, 38, 25), fill=WHITE + (255,))
    d.polygon(s(37, 16, 44, 22, 45, 42, 41, 54, 37, 42, 39, 30),
              fill=WHITE + (255,))
    img.alpha_composite(soften(b, 0.4))
    return img


def hairstyle():
    img = hair_bust()
    # Small cycle arrow, bottom right.
    arc, d = layer()
    swap_arc(d, (58, 50), 11, 300, 200, width=3, color=CYAN + (255,))
    a = math.radians(200)
    tip = (58 + 11 * math.cos(a), 50 + 11 * math.sin(a))
    arrowhead(d, tip, 200 + 100, length=7, color=CYAN + (255,))
    img.alpha_composite(soften(arc, 0.3))
    return img


def restyle():
    img = hair_bust()
    sp, d = layer()
    for cx, cy, r in ((59, 18, 7), (64, 34, 4.5), (54, 44, 3.5)):
        d.polygon(s(cx, cy - r, cx + r * 0.28, cy - r * 0.28,
                    cx + r, cy, cx + r * 0.28, cy + r * 0.28,
                    cx, cy + r, cx - r * 0.28, cy + r * 0.28,
                    cx - r, cy, cx - r * 0.28, cy - r * 0.28),
                  fill=CYAN + (255,))
    img.alpha_composite(soften(sp, 0.3))
    return img


import os

os.makedirs("Textures/HoloAI/UI", exist_ok=True)
save(switch_persona(), "SwitchPersona")
save(toggle_projection(), "ToggleProjection")
save(summon(), "Summon")
save(hairstyle(), "Hairstyle")
save(restyle(), "Restyle")
