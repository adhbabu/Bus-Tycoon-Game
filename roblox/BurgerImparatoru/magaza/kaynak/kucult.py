# Kullanım: python3 kucult.py  ->  *_ham.png dosyalarını Roblox boyutlarına küçültüp ../ klasörüne yazar
import os
from PIL import Image
isler = [('ikon_ham.png', '../ikon-512.png', (512, 512))] + [(f'tanitim{i}_ham.png', f'../tanitim-{i}.png', (1920, 1080)) for i in (1, 2, 3)]
for kaynak, hedef, boyut in isler:
    if os.path.exists(kaynak):
        Image.open(kaynak).convert('RGB').resize(boyut, Image.LANCZOS).save(hedef, optimize=True)
        print(kaynak, '->', hedef)
