# Oyun sesleri: kısa, yumuşak efektler (tamamen sentez, telifsiz). 22.05 kHz mono 16 bit WAV.
# Kullanım: python3 Tools/make_sounds.py Assets/_Project/Resources/BusTycoon/Audio
import numpy as np, wave, sys, os
SR = 22050
out = sys.argv[1]

def t(d): return np.arange(int(SR * d)) / SR
def env(n, attack=0.004, tau=0.1):
    x = np.arange(n) / SR
    e = np.exp(-x / tau)
    a = int(SR * attack)
    if a > 0: e[:a] *= np.linspace(0, 1, a)
    r = min(n // 3, int(SR * 0.015))   # kısa bırakış: nota sonunda tık olmasın
    if r > 0: e[-r:] *= np.linspace(1, 0, r) ** 2
    return e
def fade(sig, ms=12):
    n = int(SR * ms / 1000); sig[-n:] *= np.linspace(1, 0, n); return sig
def tone(freq, d, harmonics=((1, 1.0),), phase_freq=None):
    x = t(d)
    f = freq if phase_freq is None else phase_freq
    ph = 2 * np.pi * np.cumsum(np.broadcast_to(f, x.shape)) / SR
    return sum(a * np.sin(k * ph) for k, a in harmonics)
def saw(freq, d, n=14):
    return tone(freq, d, tuple((k, ((-1) ** (k + 1)) / k) for k in range(1, n + 1)))
def tri(freq, d, n=7):
    return tone(freq, d, tuple((k, ((-1) ** ((k - 1) // 2)) / (k * k)) for k in range(1, 2 * n, 2)))
def lowpass(sig, cutoff):
    a = np.exp(-2 * np.pi * cutoff / SR); y = np.zeros_like(sig); acc = 0.0
    for i, v in enumerate(sig): acc = (1 - a) * v + a * acc; y[i] = acc
    return y
def place(total, parts):
    buf = np.zeros(int(SR * total))
    for start, sig in parts:
        i = int(SR * start); n = min(len(sig), len(buf) - i); buf[i:i + n] += sig[:n]
    return buf
def save(name, sig, peak=0.6):
    sig = fade(sig / max(1e-9, np.max(np.abs(sig))) * peak)
    data = (np.clip(sig, -1, 1) * 32767).astype('<i2')
    with wave.open(os.path.join(out, name + '.wav'), 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(SR); w.writeframes(data.tobytes())
    print(f'{name}.wav {len(sig) / SR:.2f}s')

rng = np.random.default_rng(7)

# Arayüz tıklaması: kısa, aşağı kayan yumuşak "tık"
d = 0.05
f = np.linspace(1500, 850, int(SR * d))
click = tone(None, d, phase_freq=f) * env(int(SR * d), 0.001, 0.012)
click += rng.normal(0, 1, len(click)) * env(len(click), 0.0005, 0.002) * 0.25
save('click', lowpass(click, 6000), 0.45)

# Para: iki notalı parlak "kaçing" (B5 → E6)
bright = ((1, 1.0), (2, 0.35), (3, 0.18), (4, 0.08))
n1 = tone(987.77, 0.09, bright) * env(int(SR * 0.09), 0.002, 0.05)
n2 = tone(1318.51, 0.45, bright) * env(int(SR * 0.45), 0.002, 0.16)
save('coin', place(0.5, [(0, n1), (0.075, n2)]))

# Korna: otobüs "dıt-dııt" (iki tonlu, kalın)
def honk(d):
    s = saw(277.18, d) + 0.8 * saw(349.23, d)
    e = np.minimum(1, np.arange(len(s)) / (SR * 0.012)) * np.minimum(1, (len(s) - np.arange(len(s))) / (SR * 0.035))
    wobble = 1 + 0.04 * np.sin(2 * np.pi * 7 * np.arange(len(s)) / SR)
    return lowpass(s * e * wobble, 2200)
save('horn', place(0.72, [(0, honk(0.13)), (0.2, honk(0.5))]), 0.5)

# Bildirim: yumuşak iki notalı zil (E5 → A5)
bell = ((1, 1.0), (2, 0.22), (2.76, 0.08))
c1 = tone(659.25, 0.5, bell) * env(int(SR * 0.5), 0.003, 0.15)
c2 = tone(880.0, 0.55, bell) * env(int(SR * 0.55), 0.003, 0.2)
save('notify', place(0.65, [(0, c1), (0.1, c2)]), 0.5)

# Uyarı (arıza): titreşimli, inen iki ton
def wob(freq, d):
    s = tri(freq, d) * (0.65 + 0.35 * np.sin(2 * np.pi * 16 * t(d)))
    return s * env(int(SR * d), 0.005, 0.3) * np.minimum(1, (int(SR * d) - np.arange(int(SR * d))) / (SR * 0.03))
thud = lowpass(rng.normal(0, 1, int(SR * 0.08)), 400) * env(int(SR * 0.08), 0.001, 0.025) * 3
save('warning', place(0.6, [(0, thud), (0.02, wob(523.25, 0.22)), (0.27, wob(392.0, 0.3))]), 0.5)

# Başarı: yükselen arpej C5 E5 G5 C6
notes = [523.25, 659.25, 783.99, 1046.5]
parts = []
for i, fr in enumerate(notes):
    d = 0.75 if i == len(notes) - 1 else 0.35
    s = (tri(fr, d) * 0.8 + tone(fr * 2, d) * 0.15) * env(int(SR * d), 0.004, 0.32 if i == len(notes) - 1 else 0.14)
    parts.append((i * 0.085, s))
save('success', place(1.05, parts), 0.55)

# Hata: iki kısa, kalın "bonk"
def bonk(freq, d):
    s = tone(freq, d, ((1, 1.0), (3, 0.3), (5, 0.12))) * env(int(SR * d), 0.002, 0.06)
    return lowpass(s, 1800)
save('error', place(0.3, [(0, bonk(233.08, 0.12)), (0.12, bonk(185.0, 0.16))]), 0.5)
