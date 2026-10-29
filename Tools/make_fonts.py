#!/usr/bin/env python3
"""
Arayüz yazı tiplerini üretir: Nunito (SIL Open Font License 1.1, ayrılmış ad yok) değişken yazı tipinden
iki sabit kalınlık çıkarır, yalnız oyunun kullandığı Latin karakterlerini bırakır ve satır yüksekliğini
Unity IMGUI'nin sabit yükseklikli etiketlerine sığacak şekilde biraz sıkılaştırır.

  Assets/_Project/Resources/BusTycoon/Fonts/Nunito-SemiBold.ttf   (600) gövde metni
  Assets/_Project/Resources/BusTycoon/Fonts/Nunito-ExtraBold.ttf  (800) başlıklar, butonlar, kalın yazı

Kullanım:
  pip install fonttools
  curl -LO "https://raw.githubusercontent.com/google/fonts/main/ofl/nunito/Nunito%5Bwght%5D.ttf"
  python3 Tools/make_fonts.py "Nunito[wght].ttf"
"""
import os
import sys

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon", "Fonts")
WEIGHTS = {"SemiBold": 600, "ExtraBold": 800}

# Temel Latin, Latin-1, Latin Genişletilmiş-A (Türkçe, Rumence, Lehçe …), tire/tırnak/üç nokta, para birimleri, eksi.
UNICODES = [*range(0x20, 0x7F), *range(0xA0, 0x180), *range(0x2010, 0x2028), *range(0x2030, 0x203B),
            *range(0x20A0, 0x20C0), 0x2122, 0x2212]

# Nunito'nun özgün satır yüksekliği 1,36 em; en yüksek aksanlı harf (Â) 0,954, en derin kuyruk (ş, ç) −0,197.
ASCENT, DESCENT = 960, 240


def build(source, style, weight):
    font = instancer.instantiateVariableFont(TTFont(source), {"wght": weight}, updateFontNames=True, static=True)

    options = subset.Options()
    options.layout_features = ["kern", "liga", "locl"]
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=UNICODES)
    subsetter.subset(font)

    hhea, os2 = font["hhea"], font["OS/2"]
    hhea.ascent, hhea.descent, hhea.lineGap = ASCENT, -DESCENT, 0
    os2.sTypoAscender, os2.sTypoDescender, os2.sTypoLineGap = ASCENT, -DESCENT, 0
    os2.usWinAscent, os2.usWinDescent = ASCENT, DESCENT

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, f"Nunito-{style}.ttf")
    font.save(path)
    print("yazıldı:", os.path.relpath(path, REPO), os.path.getsize(path) // 1024, "KB")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    for style, weight in WEIGHTS.items():
        build(sys.argv[1], style, weight)
