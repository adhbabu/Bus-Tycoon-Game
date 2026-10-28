#!/usr/bin/env python3
"""GitHub Pages sitesini üretir: giriş sayfası, gizlilik politikası, destek ve (varsa) web oyunu.

Kullanım:  python3 Tools/build_site.py <çıktı-klasörü> [--game <WebGL-derleme-klasörü>]

  /            site/index.html (oyun yoksa "Oyna" düğmesi yerine "hazırlanıyor" notu)
  /gizlilik/   store/privacy-policy.md  → mağazalardaki "Gizlilik politikası URL'si"
  /destek/     store/support.md         → App Store "Destek URL'si"
  /oyna/       Unity WebGL derlemesi (Assets/WebGLTemplates/BusTycoon şablonuyla)

Bağımlılık yok: sayfalar yalnızca başlık, paragraf, liste, kalın/italik, bağlantı ve ayraç kullanır;
bunlar için küçük bir Markdown dönüştürücüsü yeterli.
"""
import argparse
import html
import re
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

PAGE = """<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>{title}</title>
<link rel="icon" href="../ikon.png">
<link rel="stylesheet" href="../site.css">
</head>
<body>
<main class="doc">
<p class="back"><a href="../">← Otobüs İmparatoru</a></p>
{body}
</main>
</body>
</html>
"""

PLAY_READY = """<a class="button primary" href="oyna/">▶ Tarayıcıda Oyna</a>
      <a class="button" href="oyna/?profil=telefon">Telefon görünümünü dene</a>"""

PLAY_PENDING = """<p class="note">Web sürümü hazırlanıyor — çok yakında burada oynanabilecek.</p>"""


def inline(text):
    text = html.escape(text, quote=False)
    text = re.sub(r"\[([^\]]+)\]\(([^)\s]+)\)", lambda m: f'<a href="{html.escape(m.group(2))}">{m.group(1)}</a>', text)
    text = re.sub(r"\*\*(.+?)\*\*", r"<strong>\1</strong>", text)
    text = re.sub(r"(?<![*\w])\*(?!\s)(.+?)(?<!\s)\*(?![*\w])", r"<em>\1</em>", text)
    return text


def markdown_to_html(source):
    out, paragraph, items = [], [], []

    def flush_paragraph():
        if not paragraph:
            return
        parts = []
        for i, line in enumerate(paragraph):
            if i:
                # Yalnız kalın bir satırdan (soru başlığı) sonra satır sonu, diğerlerinde boşluk.
                parts.append("<br>" if re.fullmatch(r"\*\*.+\*\*", paragraph[i - 1]) else " ")
            parts.append(inline(line))
        out.append("<p>" + "".join(parts) + "</p>")
        paragraph.clear()

    def flush_list():
        if items:
            out.append("<ul>\n" + "\n".join(f"<li>{inline(item)}</li>" for item in items) + "\n</ul>")
            items.clear()

    for raw in source.splitlines():
        stripped = raw.strip()
        if not stripped:
            flush_paragraph()
            flush_list()
            continue
        heading = re.match(r"(#{1,3})\s+(.*)", stripped)
        if heading:
            flush_paragraph()
            flush_list()
            level = len(heading.group(1))
            out.append(f"<h{level}>{inline(heading.group(2))}</h{level}>")
        elif stripped == "---":
            flush_paragraph()
            flush_list()
            out.append("<hr>")
        elif re.match(r"[*-]\s+", stripped):
            flush_paragraph()
            items.append(stripped[2:].strip())
        elif items and raw.startswith("  "):
            items[-1] += " " + stripped          # liste maddesinin devam satırı
        else:
            flush_list()
            paragraph.append(stripped)
    flush_paragraph()
    flush_list()
    return "\n".join(out)


def first_heading(source):
    match = re.search(r"^#\s+(.+)$", source, re.MULTILINE)
    return match.group(1).strip() if match else "Otobüs İmparatoru"


def render_page(markdown_path, target_dir):
    source = markdown_path.read_text(encoding="utf-8")
    target_dir.mkdir(parents=True, exist_ok=True)
    page = PAGE.format(title=html.escape(first_heading(source)), body=markdown_to_html(source))
    (target_dir / "index.html").write_text(page, encoding="utf-8")


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("output", type=Path)
    parser.add_argument("--game", type=Path, help="Unity WebGL derleme klasörü (index.html + Build/)")
    args = parser.parse_args()

    out = args.output
    if out.exists():
        shutil.rmtree(out)
    out.mkdir(parents=True)

    has_game = args.game is not None and (args.game / "index.html").is_file()
    index = (ROOT / "site" / "index.html").read_text(encoding="utf-8")
    (out / "index.html").write_text(index.replace("%%PLAY%%", PLAY_READY if has_game else PLAY_PENDING), encoding="utf-8")
    shutil.copy(ROOT / "site" / "site.css", out / "site.css")
    shutil.copy(ROOT / "store" / "google-play" / "icon-512.png", out / "ikon.png")
    shutil.copy(ROOT / "store" / "google-play" / "feature-graphic.png", out / "tanitim.png")
    (out / ".nojekyll").write_text("", encoding="utf-8")

    render_page(ROOT / "store" / "privacy-policy.md", out / "gizlilik")
    render_page(ROOT / "store" / "support.md", out / "destek")

    if has_game:
        shutil.copytree(args.game, out / "oyna")
    elif args.game is not None:
        print(f"Uyarı: WebGL derlemesi bulunamadı ({args.game}); site oyunsuz üretildi.")

    print(f"Site hazır: {out} (oyun: {'var' if has_game else 'yok'})")


if __name__ == "__main__":
    main()
