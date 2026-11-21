# Battle startup and gameplay profiling

The reported freeze reproduced in a fresh Play-mode session on the RTX 4060
Laptop GPU. The first Walker loaded the complete Revenant asset graph synchronously:
3,560.95 ms inside roster loading, 3,584.84 ms for the complete spawn. The longest
observed startup frame was 3,607.80 ms. Synchronous combat-audio warmup cost another
364.70 ms during scene startup. See the [before capture](2026-11-17-startup-before.json).

## Changes

- `BattleLoading` prepares animation clips and meshes in small asynchronous batches
  while the menu is open. It then loads the roster/prefab and combat audio, retaining
  references across runs. A responsive loading screen waits for preparation.
- Battle, menu and restart transitions use additive asynchronous scene loading.
  The existing `Background` world remains loaded. Scene activation selects the
  new active scene before its `Start` callbacks create gameplay objects.
- Intro pacing waits for the prepared combat bank; the first enemy no longer
  initiates a synchronous roster load. Audio imports decode in the background.
  `music_menu.wav` and its importer are unchanged.
- The runtime animation bake removes redundant keys with dense pose comparisons.
  The 50 clips retain their GUIDs, names, contacts and controllers. Keys fell from
  983,145 to 405,127; serialized animation files fell from 551,392,464 bytes
  (525.85 MiB) to 319,118,438 bytes (304.34 MiB). These are Editor serialization
  sizes, not compressed player-build sizes. Every clip is checked at 120 Hz
  against its dense pose with a 1 mm position / 0.2-degree rotation limit.
  The dense editable Blender actions remain available. See the
  [animation budget](../../ArtSource/Revenants/animation-budget.json) and
  [184-check pose review](2026-11-17-animation-budget-review.txt).
- Repeated bone/bind-pose array reads are cached during warrior/equipment setup.
  Hidden menu-preview simulation stops when its page is closed.
- Cloth projection rejects capsules outside their bounds before computing exact
  contacts. Armor uses precomputed triangle planes and equivalent local transform
  math; its exact clearance diagnostics run when inspected. Collision iterations,
  simulation frequency, contact margins and the rendered cloth remain intact.

## Quality-preserved result

The [final capture](2026-11-17-startup-after.json) uses the original Ultra settings
and building LOD distances. Textures, meshes, shadows, shader settings and output
quality were not reduced. A trial architecture-LOD adjustment was removed.

| Metric | Before | Final |
|---|---:|---:|
| First Walker spawn | 3,584.84 ms | 20.12 ms |
| Longest startup frame, including scene activation | 3,607.80 ms | 390.55 ms |
| Startup-window frame p95 | 25.88 ms | 11.06 ms |
| `GameManager.StartGameplay` | 508.04 ms | 26.01 ms |
| Synchronous roster/audio load during gameplay | Present | Absent |

The remaining longest frame occurs during scene activation behind the loading
screen. An animation-preparation frame took 202.80 ms. Once gameplay began, the
capture found no loading frame of 250 ms or more. The
[startup regression checks](2026-11-17-startup-checks.txt) pass.

The [complete 463-check gameplay regression](2026-11-17-startup-gameplay.txt)
also passes, with no logged errors/exceptions and no C# compilation warnings or
errors. It checks the same world instance survives battle entry, menu return and
restart, and covers cloth/armor/quiver clearance, character attacks, enemies,
arrows, audio, slope traversal, pause/resume, boons and all game modes.

A separate eight-second 1920x1080 Ultra Editor stress capture moved the camera
and player with twelve mixed enemies. It measured 31.17 ms average / 33.64 ms p95,
with armor 6.73 ms, cloth surface correction 2.52 ms and quiver 0.084 ms p95.
Rendering remained substantial: approximately 34.7 million submitted triangles
including passes and 5,112 batches in the sampled frame. This is not a claim that
the entire game is fully optimized or that Ultra holds 60 fps. The capture runs
in the Editor; a standalone player and different camera views can differ.

## Reproduce

Use an isolated Unity project/library when another editor has this workspace open.
Create `Artifacts/StartupReviewOnly.txt` containing a label, then run:

```powershell
Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir Artifacts/Startup/Review -logFile startup-review.log
```

The driver uses wall-clock frames with `Time.captureDeltaTime = 0`, profiles a real
menu-to-battle transition, then orbits/moves at 1080p with every enemy type. The
opt-in `BattleLoadMetrics` scopes identify synchronous setup/spawn work. Remove
the marker before the complete gameplay regression. Rebuild the optimized clips
and preload manifest with `RevenantBuilder.AnimateAndReview`; mesh imports also
refresh them automatically. No Blender mesh rebuild is required for this change.
