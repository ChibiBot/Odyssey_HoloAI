#!/usr/bin/env python3
"""Matrix item textures from the hand-finished cartridge assets.

Concept/cartridge_assets/<persona>_cartridge_asset.png are the source of
truth: already cropped, transparent, and branded with each persona's
silhouette face. This step just trims to the alpha bounds, centers each on a
square canvas, and resizes to the shipped 256x256 RGBA at
Textures/HoloAI/Items/Matrix_<NAME>.png. Run from the repo root.
"""
from PIL import Image

PERSONAS = ["VESTA", "HERMES", "ATHENA", "ACESO", "IXIA"]

for name in PERSONAS:
    im = Image.open(f"Concept/cartridge_assets/{name.lower()}_cartridge_asset.png") \
        .convert("RGBA")
    bbox = im.split()[3].getbbox()
    im = im.crop(bbox)
    side = max(im.width, im.height)
    square = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    square.alpha_composite(im, ((side - im.width) // 2, (side - im.height) // 2))
    square.resize((256, 256), Image.LANCZOS) \
        .save(f"Textures/HoloAI/Items/Matrix_{name}.png", optimize=True)
    print(f"saved Matrix_{name}.png ({im.width}x{im.height} -> 256x256)")
