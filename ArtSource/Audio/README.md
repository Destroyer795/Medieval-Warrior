# Combat and horror audio

All effects are procedurally synthesized; no recorded or third-party samples are
used. Rebuild from the repository root with Python 3.10+ and NumPy/SciPy installed:

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
The existing UI, horn and meadow recipes are retained. The separate
`extra_audio.py` still owns the battle drum loop and footsteps. Forty WAV files
are present in the complete committed bank; a fresh run of `sfx.py` creates 35.
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

The generated [synthesis audit](synthesis-audit.json) records sample rate, duration,
peak, RMS, DC and SHA-256 for each clip. The Unity editor command **Medieval Warrior
→ Combat → Verify audio bank and protected menu** checks all 40 imported clips,
the four new cue references and the protected WAV hash.
