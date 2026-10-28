#!/usr/bin/env python3
"""
Mağaza görsellerini üretir (özgün çizim; harici görsel kullanılmaz):
  Assets/_Project/Art/AppIcon.png            1024×1024  Unity uygulama ikonu (iOS + Android)
  store/app-store/AppIcon-1024.png           1024×1024  App Store Connect ikonu (saydamlık YOK)
  store/google-play/icon-512.png              512×512   Google Play ikonu
  store/google-play/feature-graphic.png      1024×500   Google Play tanıtım görseli

Arka plan, oyunun kendi harita dokusundan (Assets/_Project/Resources/BusTycoon/map.png) alınır.
Kullanım: python3 Tools/make_store_art.py      Gereksinim: Pillow, Inter yazı tipi (veya DejaVu Sans)
"""
import os

from PIL import Image, ImageDraw, ImageEnhance, ImageFilter, ImageFont

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
MAP = os.path.join(REPO, "Assets", "_Project", "Resources", "BusTycoon", "map.png")
SS = 4  # süper örnekleme (kenar yumuşatma)

FONT_CANDIDATES = {
    "bold": ["/usr/share/fonts/opentype/inter/Inter-Bold.otf", "/usr/share/fonts/truetype/dejavu/DejaVuSans-Bold.ttf"],
    "medium": ["/usr/share/fonts/opentype/inter/Inter-Medium.otf", "/usr/share/fonts/truetype/dejavu/DejaVuSans.ttf"],
}


def font(kind, size):
    for path in FONT_CANDIDATES[kind]:
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def map_background(width, height, focus=(0.55, 0.62), zoom=1.0, darken=0.55):
    """Harita dokusundan Türkiye merkezli kırpım, karartılmış ve hafif bulanık."""
    source = Image.open(MAP).convert("RGB")
    sw, sh = source.size
    crop_w = sw / zoom
    crop_h = crop_w * height / width
    if crop_h > sh:
        crop_h = sh
        crop_w = crop_h * width / height
    cx, cy = focus[0] * sw, focus[1] * sh
    left = min(max(0, cx - crop_w / 2), sw - crop_w)
    top = min(max(0, cy - crop_h / 2), sh - crop_h)
    image = source.crop((int(left), int(top), int(left + crop_w), int(top + crop_h))).resize((width, height), Image.LANCZOS)
    image = image.filter(ImageFilter.GaussianBlur(width / 900))
    return ImageEnhance.Brightness(image).enhance(darken)


def draw_route(draw, start, end, lift, color, width, dots=True):
    """İki şehir arasında yay (kesikli) ve şehir noktaları."""
    (x0, y0), (x1, y1) = start, end
    points = []
    steps = 60
    for i in range(steps + 1):
        t = i / steps
        x = x0 + (x1 - x0) * t
        y = y0 + (y1 - y0) * t - 4 * t * (1 - t) * lift
        points.append((x, y))
    for i in range(0, steps, 2):
        draw.line([points[i], points[i + 1]], fill=color, width=width)
    if dots:
        for (x, y) in (start, end):
            r = width * 2.2
            draw.ellipse([x - r, y - r, x + r, y + r], fill=(255, 205, 80), outline=(255, 255, 255), width=max(1, width // 2))


def draw_bus(draw, x, y, w):
    """Yandan görünüm şehirlerarası otobüs; (x, y) sol üst, w genişlik."""
    h = w * 0.40
    radius = h * 0.18
    body = (238, 160, 52)
    stripe = (226, 84, 52)
    glass = (196, 228, 255)
    frame = (34, 44, 58)

    # gövde + gölge
    draw.rounded_rectangle([x + w * 0.02, y + h * 0.10, x + w * 1.02, y + h * 1.08], radius=radius, fill=(0, 0, 0, 90))
    draw.rounded_rectangle([x, y, x + w, y + h], radius=radius, fill=body)
    # alt şerit
    draw.rounded_rectangle([x, y + h * 0.62, x + w, y + h], radius=radius, fill=stripe)
    draw.rectangle([x + radius, y + h * 0.62, x + w - radius, y + h * 0.75], fill=stripe)
    # ince beyaz çizgi
    draw.rectangle([x + w * 0.02, y + h * 0.58, x + w * 0.98, y + h * 0.61], fill=(255, 240, 220))
    # yan camlar
    win_top, win_bottom = y + h * 0.13, y + h * 0.47
    draw.rounded_rectangle([x + w * 0.04, win_top - h * 0.02, x + w * 0.82, win_bottom + h * 0.02], radius=h * 0.06, fill=frame)
    count = 6
    span = (w * 0.76) / count
    for i in range(count):
        wx = x + w * 0.055 + i * span
        draw.rounded_rectangle([wx, win_top, wx + span * 0.86, win_bottom], radius=h * 0.04, fill=glass)
        draw.polygon([(wx + span * 0.1, win_top + 2), (wx + span * 0.35, win_top + 2), (wx + span * 0.1, win_bottom - 2)], fill=(232, 245, 255))
    # ön cam (eğik)
    draw.polygon([(x + w * 0.84, win_top - h * 0.02), (x + w * 0.965, win_top + h * 0.02), (x + w * 0.985, win_bottom + h * 0.08), (x + w * 0.84, win_bottom + h * 0.08)], fill=frame)
    draw.polygon([(x + w * 0.85, win_top + h * 0.01), (x + w * 0.955, win_top + h * 0.04), (x + w * 0.972, win_bottom + h * 0.05), (x + w * 0.85, win_bottom + h * 0.05)], fill=glass)
    # far
    draw.ellipse([x + w * 0.955, y + h * 0.66, x + w * 0.99, y + h * 0.78], fill=(255, 236, 140))
    # tekerlekler
    for cx in (x + w * 0.20, x + w * 0.80):
        cy = y + h * 0.98
        r = h * 0.21
        draw.ellipse([cx - r * 1.18, cy - r * 1.18, cx + r * 1.18, cy + r * 1.18], fill=stripe)
        draw.ellipse([cx - r, cy - r, cx + r, cy + r], fill=(26, 28, 32))
        draw.ellipse([cx - r * 0.45, cy - r * 0.45, cx + r * 0.45, cy + r * 0.45], fill=(160, 168, 178))


def save_rgb(image, *path_parts, size=None):
    path = os.path.join(REPO, *path_parts)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    out = image.convert("RGB")
    if size:
        out = out.resize(size, Image.LANCZOS)
    out.save(path, optimize=True)
    print("yazıldı:", os.path.relpath(path, REPO), out.size)


def make_icon():
    size = 1024 * SS
    image = map_background(size, size, focus=(0.6, 0.6), zoom=2.4, darken=0.5).convert("RGBA")
    overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    # alt kısımda koyu degrade (otobüs öne çıksın)
    for i in range(size // 2):
        alpha = int(170 * i / (size // 2))
        draw.line([(0, size // 2 + i), (size, size // 2 + i)], fill=(8, 18, 32, alpha))
    draw_route(draw, (size * 0.16, size * 0.36), (size * 0.84, size * 0.30), size * 0.26, (255, 255, 255, 235), int(size * 0.014))
    draw_bus(draw, size * 0.09, size * 0.47, size * 0.82)
    image = Image.alpha_composite(image, overlay).resize((1024, 1024), Image.LANCZOS)
    save_rgb(image, "Assets", "_Project", "Art", "AppIcon.png")
    save_rgb(image, "store", "app-store", "AppIcon-1024.png")
    save_rgb(image, "store", "google-play", "icon-512.png", size=(512, 512))


def make_feature_graphic():
    width, height = 1024 * SS, 500 * SS
    image = map_background(width, height, focus=(0.58, 0.55), zoom=1.25, darken=0.62).convert("RGBA")
    overlay = Image.new("RGBA", image.size, (0, 0, 0, 0))
    draw = ImageDraw.Draw(overlay)
    for x in range(width):  # metin için soldan sağa koyulaşan degrade
        alpha = int(205 * max(0.0, 1.0 - x / (width * 0.62)))
        draw.line([(x, 0), (x, height)], fill=(6, 14, 26, alpha))

    draw_route(draw, (width * 0.56, height * 0.30), (width * 0.95, height * 0.22), height * 0.22, (255, 255, 255, 220), int(height * 0.012))
    draw_bus(draw, width * 0.56, height * 0.43, width * 0.40)

    title = font("bold", int(height * 0.155))
    subtitle = font("medium", int(height * 0.062))
    tag = font("bold", int(height * 0.045))
    tx = width * 0.055
    label = "STRATEJİ · TYCOON"
    left, top, right, bottom = draw.textbbox((0, 0), label, font=tag)
    pad_x, pad_y = width * 0.012, height * 0.018
    draw.rounded_rectangle([tx, height * 0.15, tx + (right - left) + 2 * pad_x, height * 0.15 + (bottom - top) + 2 * pad_y + top],
                           radius=height * 0.02, fill=(255, 196, 70, 255))
    draw.text((tx + pad_x - left, height * 0.15 + pad_y), label, font=tag, fill=(20, 24, 30))
    draw.text((tx, height * 0.29), "Otobüs", font=title, fill=(255, 255, 255))
    draw.text((tx, height * 0.46), "İmparatoru", font=title, fill=(255, 255, 255))
    draw.text((tx, height * 0.68), "Hat aç · Bileti fiyatla · Filonu büyüt", font=subtitle, fill=(206, 226, 245))
    draw.text((tx, height * 0.78), "81 il · 5 ülke · minibüsten VIP'ye", font=subtitle, fill=(150, 176, 204))

    image = Image.alpha_composite(image, overlay).resize((1024, 500), Image.LANCZOS)
    save_rgb(image, "store", "google-play", "feature-graphic.png")


if __name__ == "__main__":
    make_icon()
    make_feature_graphic()
