# Combat and horror audio

Combat effects use procedural synthesis. The four trumpet cues use acoustic
recordings. Rebuild from the repository root with Python 3.10+ and NumPy/SciPy
installed.

```powershell
python Tools/ArtPipeline/sfx.py Assets/Resources/Audio
```

`horror_audio.py` uses a separate deterministic random seed for every named effect.
Ordinary groans use irregular glottal closures with missing cycles, noise-excited
throat resonances, 40–85Hz rumble, unstable amplitude envelopes, choking breath and
wet click grains. They have no sawtooth carrier. The Brute deliberately layers
distorted octaves with chest rumble and short cavern reflections. Attack, death,
regurgitation, acid impact, swollen flesh and emergence use their own envelopes
and foley rather than pitching one generic voice across the whole roster.

Combat layers broadband blade drag, a cutting transient, fleshy low impact, bone
snaps, damped inharmonic iron modes, timber thuds, arrow puncture and dry leather,
mail and dirt foley. New `parry`, `thunder_guard`, `phantom_cleave` and `splinter`
cues distinguish defensive timing and the transformative boons.

The output is mono 16-bit PCM at 44.1kHz, suitable for positional playback, with
high-pass/DC cleanup, softened peaks, short boundary fades and 0.9 peak headroom.
The separate `extra_audio.py` generates the battle drum loop and footsteps.
A fresh run of `sfx.py` creates 35 files.
`AudioManager.PlayAt` varies pitch by at most ±8% relative to the archetype's base
register; UI and music playback keep their requested pitch.

The current voice revision uses differentiated glottal-flow closures and continuous
time-varying tract resonators, retaining audible low/mid rasp alongside the sub-bass.
Groans begin 0.5–1.8s after emergence, retry quickly when a voice slot is busy and
continue to work while navigation is pending. At most three ambient groans overlap.
Creature voices use a 9–11m reference distance and 60m range, with a 6.5kHz minimum
distance-filter cutoff so breath and clicks remain audible. Nearby creature cues
briefly lower the battle/ambience bed; menu music is excluded from this ducking.

`bow_release` is a 0.24s string snap, rapidly damped bow-limb resonance and shaft
clearance noise. The old 110Hz musical pluck is no longer used by the effects
generator. Its copy in the frozen menu recipe remains untouched.

## Recorded trumpets

`wave_horn`, `boss_horn`, `victory` and `defeat` use original phrases built from
Versilian Studios' VSCO 2 Community Edition trumpet recordings. Sam Gossner and
Simon Dalzell recorded the library, with sample cutting by Elan Hickler /
Soundemote. The publisher released it under CC0 1.0. [Source
recordings](https://github.com/sgossner/VSCO-2-CE/tree/440300901dfe9275fd84e0b7763af1f8443ae62e/Brass/Trumpet)
and [license](https://github.com/sgossner/VSCO-2-CE/blob/440300901dfe9275fd84e0b7763af1f8443ae62e/LICENSE).

The takes in `Trumpet/` are 44.1 kHz mono excerpts of the stereo originals. Their
names give the sounding pitch. The upstream filenames use octave numbers one
lower. The cue renderer sets the phrases and levels, shapes releases, and blends a
short reflection tail. The boss call uses the adjacent recorded D for its D-flat.

| Local take | Upstream file |
| --- | --- |
| `G4-short-a.wav` | `stac/Sum_SHTrumpet_stac_G3_v2_rr1.wav` |
| `G4-short-b.wav` | `stac/Sum_SHTrumpet_stac_G3_v2_rr2.wav` |
| `Bb4-short.wav` | `stac/Sum_SHTrumpet_stac_A#3_v2_rr1.wav` |
| `D5-short.wav` | `stac/Sum_SHTrumpet_stac_D4_v2_rr1.wav` |
| `F5-short.wav` | `stac/Sum_SHTrumpet_stac_F4_v2_rr1.wav` |
| `G4-vibrato.wav` | `susvib/Sum_SHTrumpet_susvib_G3_v2_rr1.wav` |
| `D5-vibrato.wav` | `susvib/Sum_SHTrumpet_susvib_D4_v2_rr1.wav` |
| `F5-vibrato.wav` | `susvib/Sum_SHTrumpet_susvib_F4_v2_rr1.wav` |
| `Bb4-soft.wav` | `sus/Sum_SHTrumpet_sus_A#3_v1_rr1.wav` |
| `G4-strong.wav` | `sus/Sum_SHTrumpet_sus_G3_v3_rr1.wav` |
| `D5-strong.wav` | `sus/Sum_SHTrumpet_sus_D4_v3_rr1.wav` |

Run the trumpet renderer on its own with NumPy to rebuild these four WAVs.

```powershell
python Tools/ArtPipeline/trumpet_cues.py Assets/Resources/Audio
```

## Protected menu theme

The shipped `music_menu.wav` is never opened for writing during regeneration.
The generator captures its SHA-256 before and after and asserts equality:

`6c2a1751d11d452e9c205ed2bf22973f3cb048ec9b51854d7641bd64153e3ea0`

`menu_theme_legacy.py` is a byte-for-byte copy of the pre-overhaul generator,
including the original composition, notes and historical RNG preamble. Its old
SFX code is retained solely to reproduce that exact menu RNG sequence. Only when
the menu file is absent does the wrapper run this frozen recipe in a temporary
folder and copy just the resulting menu WAV. It never copies those legacy effects
into the game bank. A clean-room rebuild reproduced the shipped menu hash exactly.

The generated [audio audit](synthesis-audit.json) records sample rate, duration,
peak, RMS, DC and SHA-256 for each clip. The Unity editor command **Medieval Warrior
→ Combat → Verify audio bank and protected menu** checks the imported clips,
required combat and trumpet cues, and the protected WAV hash.
