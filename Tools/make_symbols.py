#!/usr/bin/env python3
"""
Yeni arayüzün ikonları: Google Material Symbols (Rounded, dolu; Apache-2.0 lisanslı,
https://github.com/marella/material-symbols — npm paketi @material-symbols/svg-400).

Her ikon beyaz ve saydam zeminli 96 px PNG olarak yazılır:
  Assets/_Project/Resources/BusTycoon/UI/Symbols/<ad>.png
Renk arayüzde verilir (USS: -unity-background-image-tint-color). Adlar Scripts/Game/UIToolkit/UiSymbols.cs ile aynı.

Kullanım: python3 Tools/make_symbols.py <svg klasörü (paketin rounded/ klasörü)>
Gereksinim: cairosvg (pip). Paket: npm pack @material-symbols/svg-400 && tar -xzf *.tgz
"""
import os
import re
import sys

import cairosvg
from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon", "UI", "Symbols")
SIZE = 96

NAMES = """
home route directions_bus map shopping_cart bar_chart trophy help settings calendar_month add_road public build
receipt_long location_on chevron_right chevron_left arrow_forward pause play_arrow star payments account_balance trending_up
trending_down my_location add remove storefront warning check_circle swap_horiz sports_score sunny dark_mode target
distance close lock schedule savings layers sell handyman stadium castle mosque museum fort local_mall school
person groups apartment flag badge edit info notifications bolt timer speed local_gas_station toll
location_city palette workspace_premium
""".split()


def main():
    src = sys.argv[1] if len(sys.argv) > 1 else "."
    os.makedirs(OUT, exist_ok=True)
    for name in NAMES:
        path = os.path.join(src, name + "-fill.svg")
        if not os.path.exists(path):
            path = os.path.join(src, name + ".svg")
        svg = open(path, encoding="utf-8").read()
        svg = re.sub(r'<path ', '<path fill="#FFFFFF" ', svg)
        cairosvg.svg2png(bytestring=svg.encode("utf-8"), write_to=os.path.join(OUT, name + ".png"),
                         output_width=SIZE, output_height=SIZE)
    print(f"{len(NAMES)} ikon → {OUT}")
    # Gradyanlar (USS'te gradyan yok): 1×64 dikey doku, arka plan olarak gerilir; köşeleri border-radius kırpar
    grad = os.path.join(os.path.dirname(OUT), "Gradients")
    os.makedirs(grad, exist_ok=True)
    for name, top, bottom in GRADIENTS:
        im = Image.new("RGBA", (4, 64))
        for y in range(64):
            t = y / 63
            c = tuple(round(a + (b - a) * t) for a, b in zip(top, bottom)) + (255,)
            for x in range(4):
                im.putpixel((x, y), c)
        im.save(os.path.join(grad, name + ".png"))
    print(f"{len(GRADIENTS)} gradyan → {grad}")


def hexrgb(h):
    return tuple(int(h[i:i + 2], 16) for i in (1, 3, 5))


GRADIENTS = [
    ("card", hexrgb("#1C363E"), hexrgb("#132A31")),
    ("gold", hexrgb("#FFD66A"), hexrgb("#E5A23A")),
    ("goldbutton", hexrgb("#FFD978"), hexrgb("#E39A2F")),
]


if __name__ == "__main__":
    main()
