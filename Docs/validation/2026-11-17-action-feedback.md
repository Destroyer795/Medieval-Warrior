# Creature playback, complete sword cuts, bow release and slopes

This follow-up fixes audible playback and traversal, in addition to regenerating
the requested undead sound textures.

The final full run passed **433 gameplay checks**, with **zero gameplay errors or
exceptions** and **zero C# compilation errors or warnings**. See the
[complete gameplay report](2026-11-17-action-feedback-gameplay.txt) and the
[48-check sword clearance/continuity report](2026-11-17-complete-sword-clearance.txt).

## Audio

- All three groans use irregular glottal-flow closures, continuously moving tract
  resonances, 40–85Hz rumble, breath and wet clicks. Live AI emits them even while
  navigation is pending; rejected voice requests retry promptly.
- Attack, death, Brute roar, regurgitation/acid, bloater and emergence retain their
  individual synthesis layers. The voice bank now retains a stronger audible
  throat band instead of relying mainly on sub-bass and diffuse noise.
- Encounter attenuation uses 9–11m reference distances / 60m range, preserves
  6.5–14kHz rasp and breath, limits ambient overlap to three groans, and makes room
  in the battle/ambience mix. Menu music is excluded from ducking.
- `bow_release` is 0.24s: string snap, damped limb modes and shaft clearance noise.
  The old 110Hz musical pluck and long synthetic whistle are removed.
- All 40 WAVs pass PCM validation. `music_menu.wav` remains byte-identical:
  `6c2a1751d11d452e9c205ed2bf22973f3cb048ec9b51854d7641bd64153e3ea0`.

## Sword motion

Twelve clips were rebuilt in place with their asset GUIDs retained. Contact timing
stays 135–185ms; a completed single stroke including recovery is 0.43–0.54s.
Rising cuts preserve the signed sweep axis, longer follow-through finishes before
combo admission, and recovery goes outside the shield. Continuous forearm roll
preserves the hand endpoint and palm grip. The input buffer accepts presses during
micro hit-stop and retains at most one follow-up.

The independent authoring review passes 48 equipment/contact/continuity checks,
including all 12 strokes and their transitions. Maximum measured wrist bend is
55.2 degrees; sword and shield grips remain in the palms. Live pose sampling uses
the sword clock and a controlled frame interval, with the horde excluded from the
neutral measurement fixture. Thrusts and cuts may share a contact point; their
blade orientation is also tested when checking that variations are distinct.

## Village traversal

The original survey found 44.6- and 42.1-degree surfaces in two house passages,
above the player's 40-degree limit. Three local walking strips were softened using
1,603 terrain height samples; all 56 surveyed corridors are now below 32 degrees.
The player supplies the vertical velocity needed to follow walkable terrain while
preserving horizontal heading/speed. The steep mountain limit remains 40 degrees.

Actual Rigidbody traversal exposed Supply Crate 5 blocking the sloped route between
`House (20)` and `HouseB (9)`. Wider route clearance moved 72 props aside, retaining
supply pickup children and settings. Junction endpoints are checked against actual
collision assets and trimmed away from third-house corners. Ground contact and
navigation were refreshed while retaining the NavMeshData GUID, architecture
transforms and all original prefab/mesh identities. Building verification still
passes 8,055 checks and 260 navigation vicinity samples.

`ActionFeedbackSmoke` checks live AI groans and all eight additional creature cues,
the audible spatial profile, background ducking, damped bow-release tail, completed
combo admission, and 24 real Rigidbody walking/sprinting traversals in both
directions across the steepest/repaired passages.

Rebuild/verify:

```powershell
python Tools/ArtPipeline/sfx.py Assets/Resources/Audio
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.SoftenHouseSlopes -logFile house-slopes.log
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.ClearVillageRoutes -logFile village-circulation.log
Unity.exe -batchmode -quit -projectPath . -executeMethod ActionFeedbackReview.BuildSwordOnly -logFile complete-sword-build.log
Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir Artifacts/ActionFeedback/Smoke -logFile feedback-full-smoke.log
```

`ArtSource/Buildings/slope-audit.json` contains the current route measurements;
`ArtSource/Audio/synthesis-audit.json` contains per-clip audio metrics and hashes.
