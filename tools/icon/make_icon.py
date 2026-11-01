"""Draws the Cleora icon: a teal-to-green rounded tile with a big white sparkle and a small one ("clean and shiny").

Usage: python3 tools/icon/make_icon.py   (needs Pillow)
Writes src/Cleora.App/Assets/cleora.ico, src/Cleora.App/Assets/cleora-256.png and tools/icon/cleora-1024.png.
"""
from pathlib import Path

from PIL import Image, ImageDraw
import math

ROOT = Path(__file__).resolve().parents[2]
ASSETS = ROOT / "src" / "Cleora.App" / "Assets"
SS = 4  # supersampling factor; the 1024 master is drawn at 4096 and scaled down for clean edges
SIZE = 1024 * SS


def lerp(a, b, t):
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def tile_gradient():
    top_left, bottom_right = (0x3E, 0xE0, 0xB0), (0x0B, 0x8A, 0x8F)
    gradient = Image.new("RGB", (SIZE, SIZE))
    pixels = gradient.load()
    step = 16
    for y in range(0, SIZE, step):
        for x in range(0, SIZE, step):
            color = lerp(top_left, bottom_right, (x + y) / (2 * SIZE))
            for dy in range(step):
                for dx in range(step):
                    pixels[x + dx, y + dy] = color
    return gradient


def master():
    image = Image.new("RGBA", (SIZE, SIZE), (0, 0, 0, 0))
    margin, radius = 56 * SS, 232 * SS
    mask = Image.new("L", (SIZE, SIZE), 0)
    ImageDraw.Draw(mask).rounded_rectangle((margin, margin, SIZE - margin, SIZE - margin), radius, fill=255)
    image.paste(tile_gradient(), (0, 0), mask)

    draw = ImageDraw.Draw(image)
    white = (255, 255, 255, 255)

    def sparkle(cx, cy, radius):
        # An astroid: four sharp points joined by inward curves, the classic "shine" mark.
        points = []
        for i in range(720):
            t = 2 * math.pi * i / 720
            points.append((cx + radius * math.cos(t) ** 3, cy + radius * math.sin(t) ** 3))
        draw.polygon(points, fill=white)

    sparkle(SIZE * 0.44, SIZE * 0.56, 300 * SS)
    sparkle(SIZE * 0.70, SIZE * 0.30, 130 * SS)
    shadow = (255, 255, 255, 150)
    r = 34 * SS
    draw.ellipse((SIZE * 0.72 - r, SIZE * 0.72 - r, SIZE * 0.72 + r, SIZE * 0.72 + r), fill=shadow)
    return image.resize((1024, 1024), Image.LANCZOS)


def main():
    ASSETS.mkdir(parents=True, exist_ok=True)
    big = master()
    big.save(ROOT / "tools" / "icon" / "cleora-1024.png")
    big.resize((256, 256), Image.LANCZOS).save(ASSETS / "cleora-256.png")
    sizes = [16, 20, 24, 32, 40, 48, 64, 128, 256]
    big.save(ASSETS / "cleora.ico", sizes=[(s, s) for s in sizes])
    print("wrote", ASSETS / "cleora.ico")


if __name__ == "__main__":
    main()
