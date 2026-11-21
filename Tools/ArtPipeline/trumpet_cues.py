"""Arrange recorded trumpet notes into the four gameplay cues."""

from dataclasses import dataclass
from functools import lru_cache
from pathlib import Path
import sys
import wave

import numpy as np

RATE = 44100
SAMPLES = Path(__file__).resolve().parents[2] / "ArtSource/Audio/Trumpet"


@dataclass(frozen=True)
class Note:
    at: float
    take: str
    hold: float
    level: float
    release: float = .085
    semitones: int = 0


# Sharply tongued notes lead into a held call. The heavy warning keeps its
# repeated Gs and tritone, while victory and defeat resolve in opposite registers.
CUES = {
    "wave_horn": (
        .82,
        (
            Note(.02, "G4-short-a", .145, .81, .065),
            Note(.24, "G4-short-b", .147, .91, .067),
            Note(.47, "Bb4-short", .167, 1.04, .075),
            Note(.73, "D5-vibrato", .765, 1.14, .17),
            Note(.765, "G4-vibrato", .72, .28, .16),
        ),
    ),
    "boss_horn": (
        .81,
        (
            Note(.02, "G4-strong", .28, .83, .105),
            Note(.49, "G4-short-b", .153, .70, .082),
            Note(.785, "D5-strong", .87, 1.0, .19, semitones=-1),
            Note(.826, "G4-vibrato", .81, .26, .17),
        ),
    ),
    "victory": (
        .82,
        (
            Note(.02, "Bb4-short", .134, .88, .055),
            Note(.225, "D5-short", .16, .93, .056),
            Note(.455, "F5-short", .129, .99, .060),
            Note(.708, "F5-vibrato", .70, 1.00, .19),
            Note(.734, "D5-vibrato", .66, .34, .17),
            Note(.758, "Bb4-soft", .65, .37, .19),
        ),
    ),
    "defeat": (
        .67,
        (
            Note(.03, "D5-vibrato", .425, .74, .14),
            Note(.625, "Bb4-soft", .44, .70, .15),
            Note(1.24, "G4-vibrato", .85, .78, .25),
        ),
    ),
}


@lru_cache(maxsize=None)
def read_take(name):
    with wave.open(str(SAMPLES / f"{name}.wav"), "rb") as clip:
        if (clip.getnchannels(), clip.getsampwidth(), clip.getframerate()) != (1, 2, RATE):
            raise ValueError(f"{name} must be mono 16-bit PCM at {RATE} Hz")
        frames = clip.readframes(clip.getnframes())
    return np.frombuffer(frames, dtype="<i2").astype(np.float64) / 32768


def phrase(notes):
    end = max(note.at + note.hold + note.release for note in notes)
    dry = np.zeros(round((end + .01) * RATE))
    for note in notes:
        source = read_take(note.take)
        length = round((note.hold + note.release) * RATE)
        # Almost all notes use the original register. The warning's D-flat
        # uses the adjacent recorded D, preserving the player's articulation.
        if note.semitones == 0:
            samples = source[:length].copy()
        else:
            rate = 2 ** (note.semitones / 12)
            samples = np.interp(np.arange(length) * rate, np.arange(len(source)), source)
        if len(samples) != length:
            raise ValueError(f"{note.take} is too short for the arrangement")

        body = samples[round(.036 * RATE):round(.138 * RATE)]
        rms = np.sqrt(np.mean(body ** 2))
        samples *= note.level / max(.001, rms)
        attack = round(.006 * RATE)
        release = round(note.release * RATE)
        samples[:attack] *= np.linspace(0, 1, attack) ** 2
        samples[-release:] *= np.cos(np.linspace(0, np.pi / 2, release)) ** 2
        start = round(note.at * RATE)
        dry[start:start + length] += samples
    return dry


def filter_tone(samples, low_cut, high_cut):
    # Mild band limiting keeps breath and articulation without a piercing edge.
    padded = np.pad(samples, (round(.01 * RATE), round(.01 * RATE)))
    frequencies = np.fft.rfftfreq(len(padded), 1 / RATE)
    high_pass = frequencies ** 2 / np.sqrt(frequencies ** 4 + low_cut ** 4)
    low_pass = 1 / np.sqrt(1 + (frequencies / high_cut) ** 4)
    filtered = np.fft.irfft(np.fft.rfft(padded) * high_pass * low_pass, n=len(padded))
    return filtered[round(.01 * RATE):][:len(samples)]


def render(notes, peak):
    dry = filter_tone(phrase(notes), 125, 8800)
    reflection = filter_tone(dry, 240, 3200)
    # The close recordings remain dominant. A few short stone reflections
    # carry the ends of the notes and give the call some space.
    reflections = ((.036, .10), (.061, .072), (.095, .049), (.147, .034), (.212, .023), (.278, .014))
    cue = np.pad(dry, (0, round(.31 * RATE)))
    for delay, gain in reflections:
        start = round(delay * RATE)
        cue[start:start + len(reflection)] += gain * reflection
    cue[:100] *= np.linspace(0, 1, 100)
    cue *= peak / np.max(np.abs(cue))
    return (cue * 32767).astype("<i2")


def write_cues(destination):
    destination = Path(destination)
    destination.mkdir(parents=True, exist_ok=True)
    for name, (peak, notes) in CUES.items():
        with wave.open(str(destination / f"{name}.wav"), "wb") as clip:
            clip.setparams((1, 2, RATE, 0, "NONE", "not compressed"))
            clip.writeframes(render(notes, peak).tobytes())


if __name__ == "__main__":
    write_cues(sys.argv[1])
