# Horror audio and combat feedback — 2026-11-17

## Audio source and protection

Verified synthesis environment: Python 3.11.15, NumPy 1.26.4, SciPy 1.15.3.

The regenerated committed bank contains 40 valid mono 16-bit PCM WAVs. The new
undead/weapon/boon synthesis uses 44.1kHz; existing menu/battle music and footsteps
retain their source sample rates. Every file has a nonempty finite sample stream
and peak below full scale. Per-clip hashes, duration, peak, RMS and DC are recorded
in [the synthesis audit](../../ArtSource/Audio/synthesis-audit.json).

The existing `music_menu.wav` and its Unity importer are unchanged. Before/after
and clean-room regeneration SHA-256 values are identical:

`6c2a1751d11d452e9c205ed2bf22973f3cb048ec9b51854d7641bd64153e3ea0`

The entire pre-overhaul generator was preserved verbatim as
`Tools/ArtPipeline/menu_theme_legacy.py`, including its original RNG preamble,
composition and note sequence. Its source SHA-256 at verification was:

`74ea6d075d7f76d7ad38904da9f4f6400e71368779f98f730d836285b582b988`

The main generator never writes an existing menu WAV. A missing theme is generated
with the frozen recipe in a temporary directory; only its menu file is copied.
The clean-room output reproduced the shipped menu exactly. A repeated normal bank
generation passed its menu-protection assertion and regenerated deterministic
effect hashes. The separate battle/footstep generator was not changed.

Unity's `CombatOverhaulReview.VerifyAudio` resolves all 40 imported mono clips and
checks the protected hash and all four new cue names. Runtime tests cover playback
with relative 0.92–1.08 pitch variation and fixed UI pitch.

## Gameplay coverage

The final full smoke run passed **391 checks**, with **zero gameplay errors or
exceptions**. Both `Assembly-CSharp` and the editor assembly compiled with **zero
C# errors or warnings**. The [complete gameplay report](2026-11-17-combat-audio-gameplay.txt)
includes the new integration checks alongside all previous regressions.

`CombatOverhaulSmoke.cs` extends the existing full smoke driver with actual hit,
parry and projectile-contact fixtures, including physical cover. The checks cover:

- Heavy finisher and arrow critical impact stops; expiry on unscaled time.
- Restoring underlying cinematic time; preserving fixed physics timestep.
- Pause/boon selection retaining control when a pending stop expires.
- Parry and perfect-block stagger, bright flash and capped riposte healing.
- Thunder Guard stunning active nearby enemies while respecting wall cover.
- Reaper's third strike reaching beyond the normal arc and applying phantom burn.
- Splinter cluster damage, one burst per piercing arrow and burning splinters.
- Physical wall cover blocking arrow shrapnel as well as Thunder Guard.
- Transformative stack caps and omission of capped boons from offers.
- Brute champion creation, boss horn, bold banner and progressive Blood Moon fog.
- Existing ordinary-hit continuity, locomotion/cloth/armor, bow, enemy roster,
  mode progression, pause/menu flow, death and ending regressions.

The fixtures keep enemy brains registered in the live horde while disabling only
their navigation. They restore match statistics, player state, boons, atmosphere
and temporary loot before the ordinary regression resumes.

Compilation uses Unity 6000.3.2f1, Built-in Render Pipeline. The unused inference
package's crashing source generator was removed, and the obsolete terrain
`NavigationStatic` flag was replaced by existing physics-based NavMesh collection.
No compiler diagnostics are suppressed.

```powershell
python Tools/ArtPipeline/sfx.py Assets/Resources/Audio
Unity.exe -batchmode -quit -projectPath . -executeMethod CombatOverhaulReview.VerifyAudio -logFile combat-audio-compile.log
Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir Artifacts/CombatOverhaul/Smoke -logFile combat-overhaul-smoke.log
```

For focused iteration, create `Artifacts/CombatReviewOnly.txt` in the validation
project before running the smoke command, then remove it for the full regression.
