#!/usr/bin/env python3
"""
Arayüz ikonlarını tek bir dokuda toplar: Twemoji (https://github.com/jdecked/twemoji, grafikler CC BY 4.0).

  Assets/_Project/Resources/BusTycoon/UI/icons.png   8 sütun, 80 px hücre (72 px ikon + 4 px saydam pay)
  (Bayraklar: Twemoji bölge bayrakları — Türkiye, Bulgaristan, Gürcistan, Yunanistan, Romanya)

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
    # bölge bayrakları (DefaultContent.Regions) ve paneller
    ("FlagTR", "1f1f9-1f1f7"), ("FlagBG", "1f1e7-1f1ec"), ("FlagGE", "1f1ec-1f1ea"), ("FlagGR", "1f1ec-1f1f7"),
    ("FlagRO", "1f1f7-1f1f4"), ("Lock", "1f512"), ("Finish", "1f3c1"), ("Coffee", "2615"),
    ("Shield", "1f6e1"), ("Tools", "1f6e0"), ("Gift", "1f381"), ("Pilot", "1f9d1-200d-2708-fe0f"),
    ("Ruler", "1f4cf"), ("Hourglass", "23f3"), ("Bank", "1f3e6"),
    # gece-gündüz göstergesi
    ("Sun", "2600"), ("Moon", "1f319"), ("Sunrise", "1f305"), ("Sunset", "1f307"),
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
