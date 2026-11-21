"""Render the sanctuary's oak impact and strained iron hinges into the combat bank."""
import array
import math
import random
import sys
import wave
from pathlib import Path


def render(destination):
    rate = 44100
    rng = random.Random(901)
    samples = []
    wood_noise = 0
    hinge_phase = 0
    for i in range(rate):
        t = i / rate
        wood_noise += .19 * (rng.uniform(-1, 1) - wood_noise)
        thud = sum(gain * math.sin(math.tau * freq * t) * math.exp(-decay * t)
                   for freq, gain, decay in [(76, .7, 15), (132, .38, 19), (211, .18, 23), (374, .12, 30)])
        thud += 2.4 * wood_noise * math.exp(-40 * t)
        age = max(t - .076, 0)
        hinge_phase += math.tau / rate * (340 + 85 * math.sin(age * 12) + 35 * wood_noise)
        strain = math.sin(hinge_phase) + .33 * math.sin(hinge_phase * 3.02)
        hinge = (.12 + .10 * math.sin(68 * age)) * strain * min(1, age * 16) * math.exp(-6 * age)
        samples.append((thud + hinge) * min(1, t * 750) * min(1, (1 - t) * 35))
    gain = 28500 / max(map(abs, samples))
    with wave.open(str(destination), "wb") as out:
        out.setparams((1, 2, rate, 0, "NONE", "not compressed"))
        out.writeframes(array.array("h", (round(v * gain) for v in samples)).tobytes())


if __name__ == "__main__":
    render(Path(sys.argv[1]) / "keep_impact.wav")
