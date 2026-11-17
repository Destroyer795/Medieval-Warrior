# Procedural SFX + music for Medieval Warrior. Everything synthesised, no samples.
import sys, wave
import hashlib, json, shutil, subprocess, tempfile
from pathlib import Path
from horror_audio import build_bank
import numpy as np
from scipy import signal

OUT = sys.argv[1]
Path(OUT).mkdir(parents=True, exist_ok=True)
menu_path = Path(OUT) / "music_menu.wav"
menu_before = hashlib.sha256(menu_path.read_bytes()).hexdigest() if menu_path.exists() else None
SR = 44100
rng = np.random.default_rng(7)

def t(d): return np.arange(int(SR * d)) / SR
def env(n, a, r, sustain=None):
    e = np.ones(n)
    na = max(1, int(a * SR)); e[:na] = np.linspace(0, 1, na)
    if r > 0:
        nr = min(n, int(r * SR)); e[-nr:] *= np.linspace(1, 0, nr) ** 2
    return e
def expdecay(n, k): return np.exp(-np.arange(n) / SR * k)
def noise(d): return rng.uniform(-1, 1, int(SR * d))
def bp(x, lo, hi, order=2): return signal.sosfilt(signal.butter(order, [lo, hi], 'bandpass', fs=SR, output='sos'), x)
def lp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, 'lowpass', fs=SR, output='sos'), x)
def hp(x, f, order=2): return signal.sosfilt(signal.butter(order, f, 'highpass', fs=SR, output='sos'), x)
def norm(x, peak=0.9):
    m = np.max(np.abs(x)) or 1; return x / m * peak
def save(name, x, sr=SR, peak=0.9):
    x = norm(x, peak)
    with wave.open(f'{OUT}/{name}.wav', 'wb') as w:
        w.setnchannels(1); w.setsampwidth(2); w.setframerate(sr)
        w.writeframes((x * 32767).astype(np.int16).tobytes())

def pluck(freq, dur, damp=0.996, bright=0.5, sr=SR):
    n = int(sr * dur); N = max(2, int(sr / freq))
    buf = rng.uniform(-1, 1, N) * bright + lp(rng.uniform(-1, 1, N), 2000) * (1 - bright)
    out = np.zeros(n)
    for i in range(n):
        out[i] = buf[i % N]
        buf[i % N] = damp * 0.5 * (buf[i % N] + buf[(i + 1) % N])
    return out

def sweep_noise(d, f0, f1, q=1.5):
    x = noise(d); out = np.zeros_like(x); seg = 256
    for i in range(0, len(x), seg):
        f = f0 + (f1 - f0) * (i / len(x))
        lo, hi = max(40, f / q), min(SR / 2 - 100, f * q)
        out[i:i + seg] = bp(x[i:i + seg], lo, hi)
    return out

# --- weighted combat and aperiodic throat / breath / foley layers
for name, samples in build_bank().items():
    save(name, samples)

save('bow_draw', bp(noise(0.7), 300, 1400) * np.linspace(0.2, 1, int(SR * 0.7)) * (1 + 0.5 * np.sin(2 * np.pi * 23 * t(0.7))), peak=0.35)
# --- pickups / UI
def chime(freqs, d=0.6, gap=0.07):
    out = np.zeros(int(SR * (d + gap * len(freqs))))
    for i, f in enumerate(freqs):
        s = int(SR * gap * i); n = int(SR * d)
        tone = (np.sin(2 * np.pi * f * t(d)) + 0.3 * np.sin(2 * np.pi * 2 * f * t(d)) + 0.1 * np.sin(2 * np.pi * 3.01 * f * t(d))) * expdecay(n, 6)
        out[s:s + n] += tone
    return out
save('pickup', chime([660, 880, 1320]), peak=0.5)
save('heal', chime([523, 659, 784, 1046], 0.9, 0.09), peak=0.5)
save('ui_click', lp(noise(0.05), 2500) * expdecay(int(SR * 0.05), 90) + 0.4 * np.sin(2 * np.pi * 700 * t(0.05)) * expdecay(int(SR * 0.05), 70), peak=0.45)
save('ui_hover', np.sin(2 * np.pi * 1200 * t(0.06)) * expdecay(int(SR * 0.06), 60), peak=0.2)
save('no_ammo', pluck(330, 0.15, 0.95, 0.3) * expdecay(int(SR * 0.15), 30), peak=0.4)

def horn(notes, dur=0.55):
    out = []
    for f, d in notes:
        n = int(SR * d * dur / 0.55)
        vib = 1 + 0.006 * np.sin(2 * np.pi * 5.5 * np.arange(n) / SR)
        ph = 2 * np.pi * np.cumsum(f * vib) / SR
        tone = signal.sawtooth(ph) * 0.6 + signal.square(ph) * 0.15
        brightness = np.linspace(800, 2600, n) ** 1
        tone = lp(tone, 2200) * env(n, 0.06, 0.15)
        out.append(tone)
    return np.concatenate(out)
save('wave_horn', horn([(147, 0.5), (220, 0.5), (220, 1.4)]))
save('victory', horn([(196, 0.25), (247, 0.25), (294, 0.25), (392, 1.2)]) + 0.0)
save('defeat', horn([(196, 0.6), (185, 0.6), (147, 1.6)]))
save('boss_horn', horn([(98, 0.8), (98, 0.3), (131, 1.6)]))

# --- ambience: wind + occasional birds (loopable, 24s)
D = 24
wind = lp(noise(D), 500) * (0.6 + 0.4 * lp(rng.normal(0, 1, int(SR * D)), 0.3) * 8)
birds = np.zeros(int(SR * D))
for _ in range(9):
    s = int(rng.uniform(0.5, D - 2) * SR)
    for k in range(rng.integers(2, 5)):
        d = rng.uniform(0.06, 0.14); n = int(SR * d)
        f = np.linspace(rng.uniform(2800, 4200), rng.uniform(2200, 5200), n)
        chirp = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.sin(np.linspace(0, np.pi, n))
        o = s + int(k * 0.16 * SR)
        birds[o:o + n] += chirp * 0.25
amb = norm(wind) * 0.6 + birds
fade = int(SR * 1.5)  # crossfade tail into head for a seamless loop
amb[:fade] = amb[:fade] * np.linspace(0, 1, fade) + amb[-fade:] * np.linspace(1, 0, fade)
save('ambient_meadow', amb[:-fade], sr=SR, peak=0.5)


# The menu recipe is frozen byte-for-byte, including its historical random-number
# preamble. Existing menu audio is never opened for writing. A missing menu is
# reproduced in a temporary folder, copying ONLY the unchanged theme into place.
if not menu_path.exists():
    with tempfile.TemporaryDirectory(prefix="mw-menu-") as temporary:
        subprocess.run([sys.executable, str(Path(__file__).with_name("menu_theme_legacy.py")), temporary], check=True)
        shutil.copyfile(Path(temporary)/"music_menu.wav", menu_path)
menu_after = hashlib.sha256(menu_path.read_bytes()).hexdigest()
assert menu_before is None or menu_before == menu_after, "Menu theme changed"

report = {"result": "PASS", "menu_sha256": menu_after, "menu_unchanged": menu_before == menu_after,
          "menu_recipe_sha256": hashlib.sha256(Path(__file__).with_name("menu_theme_legacy.py").read_bytes()).hexdigest(), "clips": []}
for path in sorted(Path(OUT).glob("*.wav")):
    with wave.open(str(path), "rb") as source:
        assert source.getsampwidth() == 2 and source.getnchannels() == 1, path.name
        pcm = np.frombuffer(source.readframes(source.getnframes()), dtype="<i2").astype(float)/32768
        assert len(pcm) and np.isfinite(pcm).all() and np.max(np.abs(pcm)) < .999, path.name
        report["clips"].append({"name": path.stem, "rate": source.getframerate(), "seconds": len(pcm)/source.getframerate(),
            "peak": float(np.max(np.abs(pcm))), "rms": float(np.sqrt(np.mean(pcm**2))), "dc": float(np.mean(pcm)),
            "sha256": hashlib.sha256(path.read_bytes()).hexdigest()})
audit = Path(__file__).resolve().parent.parent.parent/"ArtSource/Audio"
audit.mkdir(parents=True, exist_ok=True)
(audit/"synthesis-audit.json").write_text(json.dumps(report, indent=2)+"\n")
print(f"audio PASS: {len(report['clips'])} valid mono PCM banks; menu unchanged={report['menu_unchanged']}")
