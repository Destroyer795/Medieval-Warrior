# Procedural SFX + music for Medieval Warrior. Everything synthesised, no samples.
import sys, wave
import numpy as np
from scipy import signal

OUT = sys.argv[1]
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

# --- combat
sw = sweep_noise(0.32, 500, 2600) * env(int(SR * 0.32), 0.05, 0.2)
save('sword_swing', sw, peak=0.6)
hit = lp(noise(0.25), 900) * expdecay(int(SR * 0.25), 22)
ring = sum(np.sin(2 * np.pi * f * t(0.25)) for f in (1800, 2650, 3900)) * expdecay(int(SR * 0.25), 30) * 0.25
save('sword_hit', hit + ring + 0.6 * np.sin(2 * np.pi * 90 * t(0.25)) * expdecay(int(SR * 0.25), 18))
clank = sum(np.sin(2 * np.pi * f * t(0.4)) * expdecay(int(SR * 0.4), 9 + i * 4) for i, f in enumerate((620, 1340, 2210, 3370)))
save('shield_block', clank + 0.8 * lp(noise(0.4), 1500) * expdecay(int(SR * 0.4), 40))
save('bow_draw', bp(noise(0.7), 300, 1400) * np.linspace(0.2, 1, int(SR * 0.7)) * (1 + 0.5 * np.sin(2 * np.pi * 23 * t(0.7))), peak=0.35)
tw = pluck(110, 0.5, 0.99, 0.9) * expdecay(int(SR * 0.5), 6)
save('bow_release', tw + 0.4 * sweep_noise(0.5, 3000, 600) * env(int(SR * 0.5), 0.01, 0.4))
save('arrow_hit', lp(noise(0.18), 1200) * expdecay(int(SR * 0.18), 35) + 0.5 * np.sin(2 * np.pi * 160 * t(0.18)) * expdecay(int(SR * 0.18), 25))
save('arrow_flesh', lp(noise(0.2), 700) * expdecay(int(SR * 0.2), 25) + 0.3 * bp(noise(0.2), 200, 600) * expdecay(int(SR * 0.2), 12))
save('dodge', sweep_noise(0.3, 1800, 300) * env(int(SR * 0.3), 0.04, 0.2), peak=0.5)
hurt = (signal.sawtooth(2 * np.pi * np.linspace(220, 140, int(SR * 0.3)).cumsum() / SR) * 0.6)
save('player_hurt', bp(hurt, 200, 1400) * env(int(SR * 0.3), 0.01, 0.2) + 0.6 * lp(noise(0.3), 500) * expdecay(int(SR * 0.3), 20))

# --- zombies: formant-filtered sawtooth growls with jitter
def growl(d, f0, f1, formants=(500, 900, 2400), jitter=12):
    n = int(SR * d)
    f = np.linspace(f0, f1, n) + jitter * lp(rng.normal(0, 1, n), 30) * 3
    src = signal.sawtooth(2 * np.pi * np.cumsum(f) / SR) + 0.3 * noise(d)
    out = sum(bp(src, fm * 0.8, fm * 1.2) * w for fm, w in zip(formants, (1, 0.6, 0.25)))
    am = 0.7 + 0.3 * np.sin(2 * np.pi * 7 * t(d) + rng.uniform(0, 6))
    return out * am * env(n, 0.08, d * 0.5)
for i in range(3):
    save(f'zombie_groan{i}', growl(1.1 + i * 0.2, 85 + i * 12, 60 + i * 8, (450 + 60 * i, 1000, 2500)), peak=0.7)
save('zombie_attack', growl(0.45, 140, 90, (650, 1200, 2600), 20), peak=0.8)
save('zombie_die', growl(1.0, 110, 45, (400, 800, 2200), 8), peak=0.8)
save('brute_roar', growl(1.4, 70, 55, (300, 650, 1800), 16) + 0.4 * lp(noise(1.4), 200) * env(int(SR * 1.4), 0.2, 0.8), peak=0.95)
save('spit', bp(noise(0.35), 400, 2500) * env(int(SR * 0.35), 0.02, 0.25) * (1 + np.sin(2 * np.pi * 31 * t(0.35))), peak=0.6)
save('splat', lp(noise(0.4), 1100) * expdecay(int(SR * 0.4), 14) * (1 + 0.6 * np.sin(2 * np.pi * 45 * t(0.4))))
fuse = np.sign(np.sin(2 * np.pi * 880 * t(1.0))) * (np.sin(2 * np.pi * np.linspace(4, 14, int(SR)).cumsum() / SR) > 0) * 0.3
save('bloater_fuse', lp(fuse, 3000) + 0.5 * growl(1.0, 120, 160, (500, 1100, 2500)), peak=0.6)
boom = lp(noise(1.6), 400, 4) * expdecay(int(SR * 1.6), 3.2) + np.sin(2 * np.pi * np.linspace(70, 30, int(SR * 1.6)).cumsum() / SR) * expdecay(int(SR * 1.6), 4)
save('explosion', boom + 0.3 * bp(noise(1.6), 1000, 4000) * expdecay(int(SR * 1.6), 9), peak=0.95)
save('emerge', lp(noise(0.9), 600) * env(int(SR * 0.9), 0.2, 0.5) * (1 + 0.5 * np.sin(2 * np.pi * 9 * t(0.9))), peak=0.6)

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

# --- music: plucked lute arpeggios over a drone in D dorian, 22.05 kHz to keep it light
MSR = 22050
bpm = 84; beat = 60 / bpm
prog = [(62, 69, 74, 77), (60, 67, 72, 76), (58, 65, 70, 74), (60, 67, 72, 76)]  # Dm C Bb C (midi)
def midi(m): return 440 * 2 ** ((m - 69) / 12)
bars = 8; total = int(MSR * beat * 4 * bars)
music = np.zeros(total + MSR * 2)
pattern = [0, 2, 1, 3, 2, 1, 0, 2]
for b in range(bars):
    chord = prog[b % 4]
    for i, p in enumerate(pattern):
        start = int(MSR * (b * 4 * beat + i * beat / 2))
        note = pluck(midi(chord[p]), 1.4, 0.995, 0.35, MSR) * expdecay(int(MSR * 1.4), 2.2) * (0.9 if i % 2 == 0 else 0.6)
        music[start:start + len(note)] += note
    root = midi(chord[0] - 12); n = int(MSR * beat * 4)
    tt = np.arange(n) / MSR
    drone = (np.sin(2 * np.pi * root * tt) + 0.4 * np.sin(2 * np.pi * root * 1.5 * tt)) * env(n, 0.4, 0.6) * 0.35
    s = int(MSR * b * 4 * beat); music[s:s + n] += drone
# soft frame drum on 1 and 3
for b in range(bars):
    for k in (0, 2):
        s = int(MSR * (b * 4 + k) * beat); n = int(MSR * 0.3)
        hit = np.sin(2 * np.pi * np.linspace(110, 55, n).cumsum() / MSR) * np.exp(-np.arange(n) / MSR * 12)
        music[s:s + n] += hit * 0.5
tail = music[total:total + MSR * 2]
music = music[:total]; music[:len(tail)] += tail
save('music_menu', lp(music, 5000, 2), sr=MSR, peak=0.6)
print('audio ok')
