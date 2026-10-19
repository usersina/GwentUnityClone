#!/usr/bin/env python3
"""UI icons drawn here, as the others are: white on transparent PNGs that the UI tints (gold, muted...), in
Assets/UI/Sprites, drawn four times larger and scaled down:

- IconClose.png (48): a cross, as IconCheck's stroke (the deck builder's trash while a deck is being named: leave it)
- IconNetwork.png (96): Wi-Fi arcs over a dot (Multiplayer: Local network)
- IconGlobe.png (96): a globe (Multiplayer: Online)

tools/ui-icons.py [project dir, default GwentCardGame]
"""
import math
import os
import sys

from PIL import Image, ImageDraw

PROJECT = sys.argv[1] if len(sys.argv) > 1 else "GwentCardGame"
OUT = os.path.join(PROJECT, "Assets/UI/Sprites")
SCALE = 4


def save(alpha, size, name):
    icon = Image.new("RGBA", (size, size), (255, 255, 255, 0))
    icon.putalpha(alpha.resize((size, size), Image.LANCZOS))
    icon.save(os.path.join(OUT, name))


def close(size=48):
    s = SCALE
    big = Image.new("L", (size * s, size * s), 0)
    d = ImageDraw.Draw(big)
    w = int(4.6 * s)
    lo, hi = 13 * s, 35 * s
    for (x1, y1, x2, y2) in ((lo, lo, hi, hi), (hi, lo, lo, hi)):
        d.line((x1, y1, x2, y2), fill=255, width=w)
        for x, y in ((x1, y1), (x2, y2)):
            d.ellipse((x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill=255)  # round ends
    save(big, size, "IconClose.png")


def network(size=96):
    S = size * SCALE
    big = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    d = ImageDraw.Draw(big)
    cx, cy = S / 2, S * 0.80
    w = S * 0.085
    d.ellipse((cx - w * 1.15, cy - w * 1.15, cx + w * 1.15, cy + w * 1.15), fill="white")
    for r in (S * 0.24, S * 0.42, S * 0.60):
        d.arc((cx - r, cy - r, cx + r, cy + r), start=225, end=315, fill="white", width=int(w))
    for r in (S * 0.24, S * 0.42, S * 0.60):  # round ends
        rr = r - w / 2
        for a in (225, 315):
            x, y = cx + rr * math.cos(math.radians(a)), cy + rr * math.sin(math.radians(a))
            d.ellipse((x - w / 2, y - w / 2, x + w / 2, y + w / 2), fill="white")
    save(big.getchannel("A"), size, "IconNetwork.png")


def globe(size=96):
    S = size * SCALE
    big = Image.new("RGBA", (S, S), (255, 255, 255, 0))
    d = ImageDraw.Draw(big)
    m = S * 0.08
    w = int(S * 0.06)
    r = (S - 2 * m) / 2
    c = S / 2
    d.ellipse((m, m, S - m, S - m), outline="white", width=w)
    d.ellipse((c - r * 0.45, m, c + r * 0.45, S - m), outline="white", width=w)  # a meridian
    d.line((c, m + w / 2, c, S - m - w / 2), fill="white", width=w)
    d.line((m + w / 2, c, S - m - w / 2, c), fill="white", width=w)  # the equator
    for dy in (-r * 0.5, r * 0.5):  # two parallels
        half = math.sqrt(r * r - dy * dy) - w * 0.6
        d.line((c - half, c + dy, c + half, c + dy), fill="white", width=w)
    save(big.getchannel("A"), size, "IconGlobe.png")


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    close()
    network()
    globe()
    print("written to", OUT)
