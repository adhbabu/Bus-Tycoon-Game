#!/usr/bin/env python3
"""
Otobüs modellerinin yandan çizimleri (özgün çizim): galeri, filo kartları, hat planlayıcı ve mini haritada kullanılır.

  Assets/_Project/Resources/BusTycoon/UI/buses.png   512×512: 2 sütun × 4 satır, 256×128 px hücre; otobüs sağa bakar

Hücre sırası Assets/_Project/Scripts/Game/UI/UIBusArt.cs → ModelIds ile AYNI olmalıdır.
Gövde renkleri haritadaki 3B otobüslerle aynıdır (segment rengi: WorldRenderer / BusModels).
Kullanım: python3 Tools/make_bus_art.py      Gereksinim: Pillow
"""
import os

from PIL import Image, ImageDraw

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
OUT = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon", "UI", "buses.png")
CELL_W, CELL_H, COLUMNS, SS = 256, 128, 2, 4

# Segment renkleri (BusSegment sırası): minibüs, midibüs, şehirlerarası, konfor, lüks VIP, çift katlı
BODY = [(242, 242, 238), (77, 204, 204), (77, 140, 255), (179, 115, 255), (255, 199, 64), (242, 89, 77)]
STRIPE = [(255, 145, 0), (255, 255, 255), (255, 255, 255), (255, 196, 0), (52, 52, 64), (255, 255, 255)]
GLASS = (34, 46, 66)
GLASS_SHINE = (84, 112, 150)
TIRE = (26, 26, 30)
HUB = (188, 192, 198)
HEADLIGHT = (255, 238, 150)
TAILLIGHT = (235, 50, 40)

# (model kimliği, segment, gövde uzunluğu px, gövde yüksekliği px, pencere sayısı, seçenekler)
MODELS = [
    ("KARTAL_M16", 0, 158, 64, 3, {}),
    ("KARTAL_M19", 0, 170, 66, 4, {"ac": True}),
    ("ANKA_27", 1, 186, 64, 5, {"ac": True}),
    ("ANKA_31", 1, 204, 64, 6, {"ac": True}),
    ("TOROS_46", 2, 232, 70, 7, {"ac": True}),
    ("TOROS_42C", 3, 232, 70, 7, {"ac": True, "double_stripe": True}),
    ("ZIRVE_VIP", 4, 238, 66, 6, {"ac": True, "tandem": True, "tinted": True}),
    ("ZIRVE_DD", 5, 236, 100, 7, {"tandem": True, "double_deck": True}),
]


def shade(color, factor):
    return tuple(max(0, min(255, round(c * factor))) for c in color)


def draw_bus(draw, segment, length, height, windows, options):
    s = SS
    left = (CELL_W - length) / 2 * s
    bottom = (CELL_H - 12) * s                      # tekerleklerin altı
    wheel_r = 13 * s if segment >= 2 else 12 * s
    body_bottom = bottom - wheel_r * 0.95
    top = body_bottom - height * s
    right = left + length * s
    body, stripe = BODY[segment], STRIPE[segment]
    outline = shade(body, 0.62)

    # zemin gölgesi
    draw.ellipse([left + 6 * s, bottom - 5 * s, right - 2 * s, bottom + 5 * s], fill=(0, 0, 0, 70))

    # gövde: minibüste kaputlu burun, diğerlerinde düz ön
    radius = 9 * s
    if segment == 0:
        hood = 26 * s
        cabin_right = right - hood
        draw.rounded_rectangle([left, top, cabin_right + radius, body_bottom], radius=radius, fill=body, outline=outline, width=2 * s)
        draw.rounded_rectangle([cabin_right - radius, top + height * s * 0.45, right, body_bottom], radius=radius, fill=body, outline=outline, width=2 * s)
        draw.polygon([(cabin_right - 2 * s, top + 2 * s), (cabin_right + 2 * s, top + 2 * s), (right - 4 * s, top + height * s * 0.47),
                      (cabin_right - 2 * s, top + height * s * 0.47)], fill=body)
        # ön cam (eğik)
        draw.polygon([(cabin_right - 1 * s, top + 6 * s), (cabin_right + 3 * s, top + 6 * s), (right - 9 * s, top + height * s * 0.44),
                      (cabin_right - 1 * s, top + height * s * 0.44)], fill=GLASS)
        glass_right = cabin_right - 6 * s
    else:
        draw.rounded_rectangle([left, top, right, body_bottom], radius=radius, fill=body, outline=outline, width=2 * s)
        # ön cam: ön yüz boyunca, hafif eğimli
        draw.polygon([(right - 13 * s, top + 5 * s), (right - 3 * s, top + 7 * s), (right - 2 * s, top + height * s * 0.52),
                      (right - 13 * s, top + height * s * 0.52)], fill=GLASS)
        glass_right = right - 17 * s

    # alt etek (koyu) ve şerit(ler)
    skirt_top = body_bottom - height * s * 0.17
    draw.rectangle([left + 3 * s, skirt_top, right - 3 * s, body_bottom - 2 * s], fill=shade(body, 0.82))
    stripe_y = top + height * s * (0.56 if not options.get("double_deck") else 0.5)
    draw.rectangle([left + 2 * s, stripe_y, right - 2 * s, stripe_y + 5 * s], fill=stripe)
    if options.get("double_stripe"):
        draw.rectangle([left + 2 * s, stripe_y + 8 * s, right - 2 * s, stripe_y + 10 * s], fill=stripe)

    # yan pencereler (çift katlıda iki sıra)
    rows = [(top + 6 * s, top + height * s * 0.27), (top + height * s * 0.58, top + height * s * 0.8)] if options.get("double_deck") \
        else [(top + 6 * s, top + height * s * 0.46)]
    glass = shade(GLASS, 0.75) if options.get("tinted") else GLASS
    span_left = left + 9 * s
    span = (glass_right - span_left) / windows
    for row_top, row_bottom in rows:
        for i in range(windows):
            x0 = span_left + i * span + 1.5 * s
            x1 = span_left + (i + 1) * span - 1.5 * s
            draw.rounded_rectangle([x0, row_top, x1, row_bottom], radius=3 * s, fill=glass)
            draw.polygon([(x0 + 2 * s, row_top + 2 * s), (x0 + span * 0.35, row_top + 2 * s), (x0 + 2 * s, row_bottom - 3 * s)], fill=GLASS_SHINE)

    # kapı (ön) ve farlar
    door_x = (glass_right - 12 * s) if segment == 0 else (right - 30 * s)
    if segment != 0:
        draw.rounded_rectangle([door_x, top + 7 * s, door_x + 12 * s, skirt_top - 1 * s], radius=2 * s, fill=shade(glass, 0.9), outline=outline, width=s)
    draw.ellipse([right - 7 * s, body_bottom - height * s * 0.27, right + 1 * s, body_bottom - height * s * 0.12], fill=HEADLIGHT)
    draw.rounded_rectangle([left - 1 * s, body_bottom - height * s * 0.36, left + 4 * s, body_bottom - height * s * 0.18], radius=s, fill=TAILLIGHT)

    # tavan kliması
    if options.get("ac"):
        draw.rounded_rectangle([left + length * s * 0.3, top - 5 * s, left + length * s * 0.55, top + 1 * s], radius=2 * s, fill=(206, 210, 216))

    # tekerlekler: çamurluk kemeri + lastik + jant
    axles = [left + length * s * 0.18, right - length * s * 0.2] if segment >= 1 else [left + length * s * 0.2, right - length * s * 0.24]
    if options.get("tandem"):
        axles.insert(1, left + length * s * 0.18 + wheel_r * 2.3)
    for cx in axles:
        cy = bottom - wheel_r
        arch = wheel_r * 1.22
        draw.pieslice([cx - arch, cy - arch, cx + arch, cy + arch], 180, 360, fill=shade(body, 0.45))
        draw.ellipse([cx - wheel_r, cy - wheel_r, cx + wheel_r, cy + wheel_r], fill=TIRE)
        hub = wheel_r * 0.45
        draw.ellipse([cx - hub, cy - hub, cx + hub, cy + hub], fill=HUB)


def main():
    rows = (len(MODELS) + COLUMNS - 1) // COLUMNS
    atlas = Image.new("RGBA", (COLUMNS * CELL_W, rows * CELL_H), (0, 0, 0, 0))
    for index, (model_id, segment, length, height, windows, options) in enumerate(MODELS):
        cell = Image.new("RGBA", (CELL_W * SS, CELL_H * SS), (0, 0, 0, 0))
        draw_bus(ImageDraw.Draw(cell), segment, length, height, windows, options)
        cell = cell.resize((CELL_W, CELL_H), Image.LANCZOS)
        atlas.alpha_composite(cell, ((index % COLUMNS) * CELL_W, (index // COLUMNS) * CELL_H))
    os.makedirs(os.path.dirname(OUT), exist_ok=True)
    atlas.save(OUT, optimize=True)
    print("yazıldı:", os.path.relpath(OUT, REPO), atlas.size, len(MODELS), "model")


if __name__ == "__main__":
    main()
