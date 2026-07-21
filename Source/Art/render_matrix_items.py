#!/usr/bin/env python3
"""Matrix item textures from the Concept cartridge art.

Takes Concept/<persona>_cartridge.png (1254x1254 opaque renders, shared
template), removes the background, crops, and rebrands the label: the pixel
pawn and the manufacturer small print are erased, replaced by a closeup
featureless white silhouette face framed by hair in the persona's signature
color. Saves 256x256 RGBA into Textures/HoloAI/Items/Matrix_<NAME>.png.
Run from the repo root.
"""
from collections import deque

from PIL import Image, ImageDraw, ImageFilter

PERSONAS = {
    "VESTA": ("vesta", (255, 184, 128)),
    "HERMES": ("hermes", (102, 255, 184)),
    "ATHENA": ("athena", (184, 140, 255)),
    "ACESO": ("aceso", (217, 247, 255)),
    "IXIA": ("ixia", (209, 20, 31)),
}

# Shared template geometry (source pixels, all five renders line up).
ERASE_SMALLPRINT = (325, 535, 700, 620)
ERASE_PAWN = (620, 380, 935, 895)
LABEL_SAMPLE = (588, 858, 652, 922)
FACE_CENTER = (785, 610)
FACE_SCALE = 1.0


def remove_background(im):
    """Flood-fill transparency from the corners: the backdrop is a uniform
    dark grey, clearly lighter than the cartridge's near-black outline."""
    w, h = im.size
    px = im.load()
    alpha = [[255] * w for _ in range(h)]
    seeds = [(0, 0), (w - 1, 0), (0, h - 1), (w - 1, h - 1)]
    ref = px[4, 4][:3]
    tol = 20
    seen = [[False] * w for _ in range(h)]
    dq = deque(seeds)
    while dq:
        x, y = dq.popleft()
        if x < 0 or y < 0 or x >= w or y >= h or seen[y][x]:
            continue
        seen[y][x] = True
        r, g, b = px[x, y][:3]
        if abs(r - ref[0]) > tol or abs(g - ref[1]) > tol or abs(b - ref[2]) > tol:
            continue
        alpha[y][x] = 0
        dq.extend(((x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1)))

    mask = Image.new("L", (w, h), 255)
    mask.putdata([alpha[y][x] for y in range(h) for x in range(w)])
    # Feather one step so the cut edge is not razor-hard after downscale.
    mask = mask.filter(ImageFilter.MinFilter(3)).filter(ImageFilter.GaussianBlur(1.2))
    out = im.convert("RGBA")
    out.putalpha(mask)
    return out


def label_color(im, box):
    """Median color of a quiet patch of the label backdrop."""
    region = im.crop(box)
    pixels = sorted(region.getdata(), key=lambda p: p[0] + p[1] + p[2])
    return pixels[len(pixels) // 2][:3]


def draw_face(draw, base, hair):
    """Closeup featureless face: white oval framed by persona-colored hair —
    rounded crown, long falls, and center-parted bangs sweeping over the brow.
    The face carries a label-colored rim so even near-white hair (A.C.E.S.O.)
    separates cleanly from the skin."""
    cx, cy = FACE_CENTER
    lite = tuple(min(255, int(c * 1.18 + 14)) for c in hair)

    # Hair mass: crown flowing into full-length falls.
    draw.ellipse((cx - 148, cy - 225, cx + 148, cy + 55), fill=hair)
    draw.polygon((cx - 144, cy - 60, cx - 126, cy + 285,
                  cx + 126, cy + 285, cx + 144, cy - 60), fill=hair)
    # Featureless face, rimmed with the label color for separation.
    draw.ellipse((cx - 94, cy - 140, cx + 94, cy + 118),
                 fill=(240, 240, 244), outline=base, width=7)
    # Center-parted bangs: two swooping wedges over the upper face, meeting
    # near the top center so a small triangle of brow shows the part.
    draw.polygon((cx - 4, cy - 158, cx - 78, cy - 152, cx - 108, cy - 92,
                  cx - 100, cy - 18, cx - 66, cy - 84, cx - 22, cy - 122),
                 fill=lite)
    draw.polygon((cx + 4, cy - 158, cx + 78, cy - 152, cx + 108, cy - 92,
                  cx + 100, cy - 18, cx + 66, cy - 84, cx + 22, cy - 122),
                 fill=lite)
    # Side curtains hugging the jawline.
    draw.polygon((cx - 108, cy - 60, cx - 96, cy + 150, cx - 128, cy + 120,
                  cx - 132, cy - 20), fill=hair)
    draw.polygon((cx + 108, cy - 60, cx + 96, cy + 150, cx + 128, cy + 120,
                  cx + 132, cy - 20), fill=hair)


def process(name, stem, hair):
    im = Image.open(f"Concept/{stem}_cartridge.png").convert("RGB")
    out = remove_background(im)
    draw = ImageDraw.Draw(out)

    bg = label_color(im, LABEL_SAMPLE)
    for box in (ERASE_SMALLPRINT, ERASE_PAWN):
        draw.rectangle(box, fill=bg + (255,))
    draw_face(draw, bg, hair)

    bbox = out.split()[3].getbbox()
    pad = 8
    left = max(bbox[0] - pad, 0)
    top = max(bbox[1] - pad, 0)
    right = min(bbox[2] + pad, out.width)
    bottom = min(bbox[3] + pad, out.height)
    out = out.crop((left, top, right, bottom))
    # Center on a square canvas so the item sits straight in its cell.
    side = max(out.width, out.height)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.alpha_composite(out, ((side - out.width) // 2, (side - out.height) // 2))
    square.resize((256, 256), Image.LANCZOS).save(f"Textures/HoloAI/Items/Matrix_{name}.png")
    print(f"saved Matrix_{name}.png")


for name, (stem, hair) in PERSONAS.items():
    process(name, stem, hair)
