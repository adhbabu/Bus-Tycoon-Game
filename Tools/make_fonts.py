#!/usr/bin/env python3
"""
Arayüz yazı tiplerini üretir: Inter (SIL Open Font License 1.1, ayrılmış ad yok) değişken yazı tipinden
üç sabit kalınlık çıkarır ve yalnız oyunun kullandığı Latin karakterlerini bırakır.

  Assets/_Project/Resources/BusTycoon/Fonts/Inter-Medium.ttf     (500) gövde metni
  Assets/_Project/Resources/BusTycoon/Fonts/Inter-Bold.ttf       (700) başlıklar, butonlar, rozetler
  Assets/_Project/Resources/BusTycoon/Fonts/Inter-ExtraBold.ttf  (800) kasa ve büyük sayılar

Unity'nin IMGUI yazısı OpenType özelliklerini uygulamaz; bu yüzden eş genişlikli rakamlar ('tnum') doğrudan
karakter tablosuna işlenir: fiyat kaydırıcısı oynarken sayılar titremez.

Kullanım:
  pip install fonttools
  curl -Lo Inter.ttf "https://raw.githubusercontent.com/google/fonts/main/ofl/inter/Inter%5Bopsz,wght%5D.ttf"
  python3 Tools/make_fonts.py Inter.ttf
"""
import os
import sys

from fontTools import subset
from fontTools.ttLib import TTFont
from fontTools.varLib import instancer

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon", "Fonts")
WEIGHTS = {"Medium": 500, "Bold": 700, "ExtraBold": 800}

# Temel Latin, Latin-1, Latin Genişletilmiş-A (Türkçe, Rumence, Lehçe …), tire/tırnak/üç nokta, para birimleri,
# ok ve eksi işaretleri.
UNICODES = [*range(0x20, 0x7F), *range(0xA0, 0x180), *range(0x2010, 0x2028), *range(0x2030, 0x203B),
            *range(0x20A0, 0x20C0), 0x2122, *range(0x2190, 0x2196), 0x21C4, 0x2212, 0x2605, 0x2606]


def tabular_digits(font):
    """'tnum' özelliğinin tek glif değişimlerini rakamların karakter eşlemesine uygular."""
    gsub = font["GSUB"].table
    mapping = {}
    for record in gsub.FeatureList.FeatureRecord:
        if record.FeatureTag != "tnum":
            continue
        for index in record.Feature.LookupListIndex:
            for table in gsub.LookupList.Lookup[index].SubTable:
                if hasattr(table, "ExtSubTable"):
                    table = table.ExtSubTable
                mapping.update(getattr(table, "mapping", {}))
    for cmap in font["cmap"].tables:
        for code in range(0x30, 0x3A):
            glyph = cmap.cmap.get(code)
            if glyph in mapping:
                cmap.cmap[code] = mapping[glyph]


def build(source, style, weight):
    font = instancer.instantiateVariableFont(TTFont(source), {"wght": weight, "opsz": 14}, updateFontNames=True, static=True)
    tabular_digits(font)

    options = subset.Options()
    options.layout_features = ["kern", "liga", "locl"]
    options.name_IDs = ["*"]
    options.name_languages = ["*"]
    subsetter = subset.Subsetter(options)
    subsetter.populate(unicodes=UNICODES)
    subsetter.subset(font)

    os.makedirs(OUT, exist_ok=True)
    path = os.path.join(OUT, f"Inter-{style}.ttf")
    font.save(path)
    print("yazıldı:", os.path.relpath(path, REPO), os.path.getsize(path) // 1024, "KB")


if __name__ == "__main__":
    if len(sys.argv) != 2:
        sys.exit(__doc__)
    for style, weight in WEIGHTS.items():
        build(sys.argv[1], style, weight)
