"""ギアメニューのアイコン（32x32 PNG）を書き出す。

使い方: python tools/make-gear-icon.py [拡大プレビューの出力先]
"""
import math
import sys
from pathlib import Path

from PIL import Image

SIZE = 32
SCALE = 8
OUT = Path(__file__).resolve().parent.parent / "source" / "COM3D2.MToonMaid.Plugin" / "Resources" / "gear_icon.png"

CENTER = 16.0
RADIUS = 14.5
OUTLINE_WIDTH = 1.6
LIGHT_DIR = (-0.8, -0.5, 0.35)
SHADE_THRESHOLD = 0.2

LIGHT_COLOR = (255, 222, 200)
SHADE_COLOR = (226, 146, 150)
OUTLINE_COLOR = (74, 40, 52)
RING_COLOR = (255, 255, 255)
RING_SHADE_COLOR = (240, 186, 186)

# 天使の輪は球の上寄りに中心を置いた楕円の弧の上半分
RING_CENTER_Y = -0.05
RING_RADIUS_X = 0.72
RING_RADIUS_Y = 0.52
RING_WIDTH = 0.2


def normalize(v):
    length = math.sqrt(sum(c * c for c in v))
    return tuple(c / length for c in v)


LIGHT = normalize(LIGHT_DIR)


def shade(x, y):
    """(x, y) は 32px 座標系。球の外は None を返す"""
    dx = (x - CENTER) / RADIUS
    dy = (y - CENTER) / RADIUS
    d2 = dx * dx + dy * dy
    if d2 > 1.0:
        return None
    if math.sqrt(d2) * RADIUS > RADIUS - OUTLINE_WIDTH:
        return OUTLINE_COLOR
    nz = math.sqrt(1.0 - d2)
    ndotl = dx * LIGHT[0] + dy * LIGHT[1] + nz * LIGHT[2]
    lit = ndotl > SHADE_THRESHOLD
    ring = math.hypot(dx / RING_RADIUS_X, (dy - RING_CENTER_Y) / RING_RADIUS_Y)
    if dy < RING_CENTER_Y and 1.0 - RING_WIDTH <= ring <= 1.0:
        return RING_COLOR if lit else RING_SHADE_COLOR
    return LIGHT_COLOR if lit else SHADE_COLOR


def main():
    big = SIZE * SCALE
    image = Image.new("RGBA", (big, big), (0, 0, 0, 0))
    pixels = image.load()
    for py in range(big):
        for px in range(big):
            color = shade((px + 0.5) / SCALE, (py + 0.5) / SCALE)
            if color is not None:
                pixels[px, py] = color + (255,)
    # NEAREST で縮小すると縁がギザギザになるため、平均を取る BOX で縮める
    icon = image.resize((SIZE, SIZE), Image.BOX)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    icon.save(OUT)
    if len(sys.argv) > 1:
        icon.resize((SIZE * 8, SIZE * 8), Image.NEAREST).save(sys.argv[1])
    print(OUT)


if __name__ == "__main__":
    main()
