"""Synthesize the Last Stand wind, downpour, thunder and final-minute score."""

from array import array
from math import exp, pi, sin, tanh
from pathlib import Path
from random import Random
import sys
import wave

RATE = 32000
TAU = 2 * pi


def save(folder, name, samples, peak, loop=False):
    if loop:
        # Crossfade one second of the noise and drone across the loop seam.
        for i in range(RATE):
            blend = i / RATE
            samples[i] = samples[-RATE + i] * (1 - blend) + samples[i] * blend
        del samples[-RATE:]
    gain = peak / max(abs(sample) for sample in samples)
    pcm = array("h", (int(sample * gain * 32767) for sample in samples))
    if sys.byteorder != "little":
        pcm.byteswap()
    with wave.open(str(folder / (name + ".wav")), "wb") as output:
        output.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
        output.writeframes(pcm.tobytes())


def weather(storm):
    rng = Random(170 if storm else 169)
    slow = wind = droplets = 0
    for i in range(15 * RATE):
        t = i / RATE
        sample = rng.uniform(-1, 1)
        slow += .005 * (sample - slow)
        wind += .039 * (sample - wind)
        droplets += .39 * (sample - droplets)
        gust = .65 + .18 * sin(TAU * t / 7.1) + .14 * sin(TAU * t / 3.7)
        drone = sin(TAU * 55 * t) + .36 * sin(TAU * 82.4 * t + sin(.84 * t))
        if storm:
            yield (.6 * (wind - slow) + .23 * (droplets - wind)) * gust + drone * .022
        else:
            yield (1.4 * slow + .8 * wind) * gust + drone * .039


def thunder():
    rng = Random(171)
    sub = rumble = crack = 0
    for i in range(4 * RATE):
        t = i / RATE
        sample = rng.uniform(-1, 1)
        sub += .009 * (sample - sub)
        rumble += .036 * (sample - rumble)
        crack += .26 * (sample - crack)
        surge = .7 + .2 * sin(13 * t) + .09 * sin(23 * t)
        snap = (crack - rumble) * exp(-17 * t) * .94
        roll = (sub * 3.4 + rumble * .8) * surge * exp(-1.31 * t)
        yield tanh((snap + roll) * min(1, t * 140) * min(1, (4 - t) * 1.6))


def final_minute():
    # A D-minor ostinato over close, uneven drum and breath accents.
    rng = Random(172)
    beat = 60 / 152
    notes = (73.42, 73.42, 110, 73.42, 77.78, 73.42, 130.81, 110,
             73.42, 87.31, 110, 73.42, 77.78, 77.78, 65.41, 69.30)
    smooth = 0
    duration = int(32 * beat * RATE)
    for i in range(duration):
        t = i / RATE
        pulse = t / beat
        tick = pulse * 2
        subdivision = (tick % 1) * beat / 2
        downbeat = (pulse % 1) * beat
        noise = rng.uniform(-1, 1)
        smooth += .17 * (noise - smooth)
        pitch = notes[int(tick) % len(notes)]
        bow = sin(TAU * pitch * subdivision) + .41 * sin(TAU * pitch * 2 * subdivision)
        bow += .21 * sin(TAU * pitch * 3.01 * subdivision)
        bow *= min(1, subdivision * 220) * exp(-12 * subdivision) * .24
        kick = sin(TAU * (51 * downbeat + 19 * (1 - exp(-31 * downbeat)) / 31))
        kick *= min(1, downbeat * 490) * exp(-18 * downbeat) * (.63 if int(pulse) % 4 in (0, 3) else .41)
        breath = (smooth + (noise - smooth) * .09) * min(1, subdivision * 165) * exp(-23 * subdivision)
        breath *= .36 if int(tick) % 4 == 3 else .14
        tremolo = (.52 + .48 * sin(TAU * 10 * t)) * sin(TAU * 146.83 * t + .28 * sin(TAU * 6.7 * t))
        swell = .09 + .075 * sin(TAU * pulse / 8)
        yield tanh(bow + kick + breath + tremolo * swell) * min(1, i / 220, (duration - i) / 220)


if __name__ == "__main__":
    folder = Path(sys.argv[1])
    folder.mkdir(parents=True, exist_ok=True)
    save(folder, "ambient_last_stand", list(weather(False)), .46, loop=True)
    save(folder, "last_stand_rain", list(weather(True)), .49, loop=True)
    save(folder, "last_stand_thunder", list(thunder()), .87)
    save(folder, "music_last_stand", list(final_minute()), .84)
