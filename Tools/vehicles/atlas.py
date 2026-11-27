# shots/*.png (vshots.js çıktısı) -> 2x atlas (3 sütun x 6 satır, 512x256 hücre) + atlas_preview.png
# Kullanım: python3 Tools/vehicles/atlas.py Assets/_Project/Resources/BusTycoon/UI/buses.png  (shots/ bulunduğun klasörde)
import sys
from PIL import Image
IDS = ["KARTAL_M16", "KARTAL_M19", "ANKA_27", "ANKA_31", "TOROS_46", "TOROS_42C", "ZIRVE_VIP", "ZIRVE_DD", "KENT_12", "KENT_12E", "KENT_18",
       "SERCE_K7", "SERCE_9", "SERCE_14", "SERCE_17", "SERCE_19", "SERCE_VIP12", "SERCE_14E"]
LEN = {"KARTAL_M16": 6.2, "KARTAL_M19": 7.0, "ANKA_27": 8.1, "ANKA_31": 9.0, "TOROS_46": 12.2, "TOROS_42C": 12.2, "ZIRVE_VIP": 13.0,
       "ZIRVE_DD": 13.7, "KENT_12": 12.0, "KENT_12E": 12.0, "KENT_18": 18.0, "SERCE_K7": 4.9, "SERCE_9": 5.15, "SERCE_14": 5.95,
       "SERCE_17": 6.95, "SERCE_19": 7.4, "SERCE_VIP12": 6.95, "SERCE_14E": 5.95}
COLS, ROWS = 3, 6
CW, CH = 512, 256
atlas = Image.new("RGBA", (CW * COLS, CH * ROWS), (0, 0, 0, 0))
for i, vid in enumerate(IDS):
    im = Image.open(f"shots/{vid}.png").convert("RGBA")
    a = im.getchannel("A").point(lambda v: 255 if v > 110 else 0)
    bb = a.getbbox()
    full = im.getchannel("A").point(lambda v: 255 if v > 6 else 0).getbbox()
    # araca gore genislik: kisa araclar biraz kucuk (gercek oranin yumusatilmisi)
    frac = 0.70 + 0.24 * min(1.0, max(-0.4, (LEN[vid] - 6.0) / 12.0))
    s = min(CW * frac / (bb[2] - bb[0]), CH * 0.80 / (bb[3] - bb[1]))
    crop = im.crop((full[0], full[1], full[2], full[3]))
    crop = crop.resize((max(1, round(crop.width * s)), max(1, round(crop.height * s))), Image.LANCZOS)
    # tekerlek tabani hucrenin %90'inda; yatayda ortali (araç kutusuna gore)
    bx0 = (bb[0] - full[0]) * s
    bw = (bb[2] - bb[0]) * s
    by1 = (bb[3] - full[1]) * s
    ox = round((CW - bw) / 2 - bx0)
    oy = round(CH * 0.91 - by1)
    cell = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    cell.alpha_composite(crop, (max(0, ox), max(0, oy)), (max(0, -ox), max(0, -oy)))
    atlas.alpha_composite(cell, ((i % COLS) * CW, (i // COLS) * CH))
atlas.save(sys.argv[1] if len(sys.argv) > 1 else "buses@2x.png")
prev = Image.new("RGBA", atlas.size, (17, 30, 38, 255))
prev.alpha_composite(atlas)
prev.convert("RGB").save("atlas_preview.png")
print("ok", atlas.size)
