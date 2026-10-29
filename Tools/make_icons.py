#!/usr/bin/env python3
"""
Arayüz ikonlarını tek bir dokuda toplar: Twemoji (https://github.com/jdecked/twemoji, grafikler CC BY 4.0).

  Assets/_Project/Resources/BusTycoon/UI/icons.png   8 sütun, 80 px hücre (72 px ikon + 4 px saydam pay)

Sıra Assets/_Project/Scripts/Game/UI/UIIcons.cs içindeki UIIcon sırasıyla AYNI olmalıdır.
Kullanım: python3 Tools/make_icons.py      Gereksinim: Pillow, internet (jsdelivr)
"""
import io
import os
import urllib.request

from PIL import Image

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon", "UI", "icons.png")
BASE = "https://cdn.jsdelivr.net/gh/jdecked/twemoji@15.1.0/assets/72x72/"
COLUMNS, CELL, ICON = 8, 80, 72

ICONS = [
    # menü
    ("NewRoute", "1f68f"), ("Routes", "1f5fa"), ("Fleet", "1f68c"), ("Dealership", "1f6d2"),
    ("Finance", "1f4b0"), ("Regions", "1f30d"), ("Goals", "1f3af"), ("Log", "1f4dc"),
    ("Help", "2753"), ("Save", "1f4be"), ("Settings", "2699"), ("Star", "2b50"),
    # ayrıntı satırları
    ("Pin", "1f4cd"), ("People", "1f465"), ("Fuel", "26fd"), ("Wrench", "1f527"),
    ("Road", "1f6e3"), ("Seat", "1f4ba"), ("Calendar", "1f4c5"), ("Ticket", "1f39f"),
    ("ChartUp", "1f4c8"), ("ChartDown", "1f4c9"), ("Clock", "23f1"), ("Building", "1f3e2"),
    # bildirimler
    ("Check", "2705"), ("Cross", "274c"), ("Info", "2139"), ("Trophy", "1f3c6"),
    ("Warning", "26a0"), ("Party", "1f389"), ("Unlock", "1f513"), ("Sparkles", "2728"),
]


def fetch(code):
    with urllib.request.urlopen(BASE + code + ".png", timeout=30) as response:
        return Image.open(io.BytesIO(response.read())).convert("RGBA")


def main():
    rows = (len(ICONS) + COLUMNS - 1) // COLUMNS
    atlas = Image.new("RGBA", (COLUMNS * CELL, rows * CELL), (0, 0, 0, 0))
    pad = (CELL - ICON) // 2
    for index, (name, code) in enumerate(ICONS):
        icon = fetch(code)
        if icon.size != (ICON, ICON):
            icon = icon.resize((ICON, ICON), Image.LANCZOS)
        atlas.alpha_composite(icon, ((index % COLUMNS) * CELL + pad, (index // COLUMNS) * CELL + pad))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    atlas.save(OUT, optimize=True)
    print("yazıldı:", os.path.relpath(OUT, REPO), atlas.size, len(ICONS), "ikon")


if __name__ == "__main__":
    main()
