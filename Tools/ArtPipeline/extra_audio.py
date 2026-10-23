"""Battle-music loop and footstep sounds (the rest of the audio comes from sfx.py).

Usage:  python extra_audio.py <Assets/Resources/Audio>
"""
import sys
import wave

import numpy as np
from scipy import signal

OUT = sys.argv[1] if len(sys.argv) > 1 else "."
SR = 22050


def save(name, x):
    x = x / np.max(np.abs(x)) * 0.8
    with wave.open(f"{OUT}/{name}.wav", "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes((x * 32767).astype(np.int16).tobytes())


def lp(x, f): return signal.sosfilt(signal.butter(2, f, "lowpass", fs=SR, output="sos"), x)


# ------------------------------------------------------------------ footsteps
rng = np.random.default_rng(5)
for i in range(4):
    n = int(SR * 0.16)
    t = np.arange(n) / SR
    thump = np.sin(2 * np.pi * (70 + 15 * i) * t) * np.exp(-t * 40)
    crunch = signal.sosfilt(signal.butter(2, [600 + 150 * i, 3200], "bandpass", fs=SR, output="sos"), rng.uniform(-1, 1, n)) * np.exp(-t * 28)
    save(f"step{i}", thump * 0.7 + crunch * 0.9)

# ------------------------------------------------------------------ war drums + low strings (20 s loop)
rng = np.random.default_rng(11)
bpm, bars = 96, 8
beat = 60 / bpm
n = int(SR * beat * 4 * bars)
mix = np.zeros(n + SR * 3)


def drum(freq, dur, amp, noise=0.3):
    t = np.arange(int(SR * dur)) / SR
    f = freq * (1 + 1.5 * np.exp(-t * 30))
    body = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t * 7)
    skin = lp(rng.uniform(-1, 1, len(t)), 1800) * np.exp(-t * 35) * noise
    return (body + skin) * amp


big = [1, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0]
mid = [0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 1, 1]
for b in range(bars):
    for i in range(16):
        s = int(SR * (b * 4 + i / 4) * beat)
        if big[i]:
            d = drum(55, 1.0, 1.0); mix[s:s + len(d)] += d
        if mid[i]:
            d = drum(110, 0.5, 0.45, 0.6); mix[s:s + len(d)] += d
        if i % 2 == 1:
            d = drum(400, 0.08, 0.12, 1.0); mix[s:s + len(d)] += d


def midi(m): return 440 * 2 ** ((m - 69) / 12)


roots = [50, 50, 46, 48, 50, 50, 53, 52]
for b in range(bars):
    for k in range(8):
        s = int(SR * (b * 4 + k * 0.5) * beat)
        d = int(SR * beat * 0.48)
        t = np.arange(d) / SR
        note = roots[b] + (12 if k % 4 == 3 else 0)
        saw = signal.sawtooth(2 * np.pi * midi(note) * t) + 0.5 * signal.sawtooth(2 * np.pi * midi(note) * 1.003 * t)
        mix[s:s + d] += lp(saw, 900) * np.minimum(1, t / 0.02) * np.exp(-t * 4) * 0.22
for b in range(0, bars, 2):
    s = int(SR * b * 4 * beat)
    d = int(SR * beat * 8)
    t = np.arange(d) / SR
    chord = [62, 65, 69] if b % 4 == 0 else [58, 62, 65]
    pad = sum(signal.sawtooth(2 * np.pi * midi(m) * t * (1 + 0.003 * np.sin(2 * np.pi * 5 * t))) for m in chord)
    mix[s:s + d] += lp(pad, 1400) * np.minimum(1, t / 1.2) * np.minimum(1, (d / SR - t) / 1.0) * 0.08
tail = mix[n:n + SR * 3]
mix = mix[:n]
mix[:len(tail)] += tail
save("music_battle", mix)
print("extra audio done")
