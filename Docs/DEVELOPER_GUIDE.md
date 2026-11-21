# Developer guide

How the game is put together, and how to extend it without breaking things.

## Scenes and flow

```
MainMenu.unity ──(Play)──► Semi-Final.unity ──(loads additively)──► Background.unity
     ▲                          │
     └──────(Main Menu)─────────┘
```

- **MainMenu** – `MainController` loads `Background` additively as a live backdrop, hides the old
  canvas and creates `MainMenuUI`. `Menu Camera` (with `MenuCameraOrbit`) circles the castle.
  `Knight Preview` (far below the world at y = −500) is a turntable rendered into a texture for
  the customise page (`MenuPreview`).
- **Semi-Final** – gameplay: the knight (`PlayerControlSpeed`, `PlayerHealth`,
  `KnightAppearance`), the `Main Camera` (`CameraFollow` + post-processing), `GameManager`,
  the `EnemySpawner` (`ZombieSpawner`) and the baked `NavMesh`. `GameManager.Awake` loads
  `Background` if it is not already loaded, so you can press Play on this scene directly.
- **Background** – the world: the castle (everything is under a `Castle` group scaled ×5), and an
  `Environment` object created by the builder (terrain, foundations, props, supply crates).

Lighting (sky, fog, ambient) lives in the *active* scene, so `Semi-Final` and `MainMenu` both carry
the same lighting settings; the builder writes them into all three scenes.

## Core systems

| Script | Responsibility |
|--------|----------------|
| `GameSettings` | Every option and customisation choice, stored in `PlayerPrefs`. Difficulty profiles, mode names, high scores. |
| `GameManager` | The match: mode logic (`UpdateMode`), spawn pacing (`TickSpawner`, `PickType`), waves, bosses, score/combo, stats, pause and end of match. Raises `Killed`, `MatchEnded`, `PauseChanged` for the HUD. |
| `ZombieSpawner` | *Where* to spawn: random NavMesh points 18–46 m from the knight with a complete path, preferring off-screen. Falls back to fixed spawn points. |
| `EnemyController` | Zombie brain (state machine: Emerging → Hunt / Circle / Attack / Stagger / Kite / Fuse → Dead). Archetype stats come from `ZombieTypes`. |
| `ZombieDirector` (in `EnemyController.cs`) | Attack tokens: only 2–4 zombies may engage at once (Brutes and the Warlord always may). The rest circle the knight. |
| `PlayerControlSpeed` | Movement (Rigidbody velocity), stamina, dodge, weapons (sword combo, shield bash, bow draw/fire), procedural arm pose, bow placement on hand/back. |
| `PlayerHealth` | HP, block / perfect block / parry, dodge i-frames, knockback, burns, second-wind regen, death. |
| `CameraFollow` | Orbit camera with mouse look, collision, bow aim (shoulder offset + FOV) and screen shake. |
| `Arrow`, `BileGlob`, `AcidPool`, `Combat.Explode`, `Pickup` | Projectiles, area damage and loot. |
| `FX` | Particle bursts built in code, hit-stop, screen shake. |
| `CombatTime` | Owns the gameplay timescale and coalesces 50–80ms unscaled impact stops; pause, boons and cinematics replace the stop safely. |
| `CombatBoons`, `ClusterVolley` | Contact-driven thunder chains, third-cut pull/bleed, phantom burn, one-shot shrapnel and delayed headshot bombs; bounded effects and physical visibility checks. |
| `HordeDirector`, `HordeTotem` | Every-third-wave events, Blood Moon speed/score/eyes, low-alley gas, reachable bridge guide and a weapon-damageable immortality objective. Scene-owned cleanup keeps retained-world restarts safe. |
| `AudioManager` | `Play2D`, `PlayAt` (3D), `PlayMusic` and `SetBattleIntensity` (war-drum layer driven by how many zombies are near); clips come from `Resources/Audio` by name. |
| `Boons` | Per-match upgrades: definitions, stacks, `Roll` (offer three), `Apply` (one-off effects) and the multipliers gameplay code reads (`SwordDamage`, `DrawSpeed`, `Pierce`, `FireArrows`, ...). `GameManager.OfferBoon` freezes time and `GameHUD.ShowBoons` shows the cards. |
| `GameInput` | Every gameplay input in one place (keyboard/mouse + gamepad via the Input System). Use it instead of `Input.*`. |
| `Feats` | Persistent achievements: `Feats.Add(stat)` for counters, `Feats.Unlock(id)` for events. Disabled while tests run. |
| `GamepadUISelector` | On every UIKit canvas: keeps a control focused on the frontmost screen while a gamepad is used. |
| `BannerFlag`, `ChimneySmoke`, `SkyDrift` | World ambience placed by the builder. |
| `ArcherRig` | Procedural archer stance (no archery clips exist for the knight): torso twist, two-bone IK for both arms, live bowstring, nocked arrow, release follow-through. Runs after animation (`DefaultExecutionOrder(100)`). |
| `KnightGear` | Finds the sword and shield islands inside the knight's single skinned mesh; with the bow out, swaps to a mesh copy without them and shows them as props (sword on the hip, shield on the back). |
| `TintVariation` | Per-house colour wash via MaterialPropertyBlock (set by the builder). |
| `UIKit` | Builds styled UI in code (panels, buttons, sliders, toggles, selectors, input fields). |
| `GameHUD`, `MainMenuUI`, `SharedPanels` | The screens themselves. |

### Zombie animation
`Assets/Animations/ZombieBrain.controller` is generated by `GameplayBuilder.CreateZombieAnimator`:
a 1D blend tree of the **in-place** idle/walk/run clips driven by the agent's real speed
(`Speed`), plus `attack` and two death states. Root motion is **off** – the NavMeshAgent moves
the zombie and the animation only animates. (The old controller used the root-motion run clip
with root motion on, so the agent and the animation fought each other.)

### Current Revenants roster

Battle assets now prepare asynchronously through `BattleLoading`, starting on
the menu. The generated `Resources/Revenants/Preload.json` lists individual clips
and meshes so they integrate in small batches before the roster asset graph is
loaded. Combat audio also loads/decodes in the background. Battle/menu/restart
transitions preserve the existing `Background` scene and change only per-run UI,
actors and navigation. The intro waits for combat preparation to finish.

`RevenantAnimationOptimization` reduces runtime curves with dense pose checks
while retaining the editable Blender actions. The bake refreshes the preload
manifest and [animation budget](../ArtSource/Revenants/animation-budget.json).
See the [startup profile and reproduction steps](validation/2026-11-17-startup-performance.md).

The shipped enemies use `Resources/Revenants/Revenant.prefab`, installed by
`RevenantVisual` during `EnemyController.Init`. The original prefab is an authoring
reference. Eight forms share a 55-bone rig, four skin materials, optional hair,
and two reduced distance meshes. The models and 74 editable rig actions live in
`ArtSource/Revenants/Revenants.blend`.

`RevenantMotionCapture` evaluates source humanoid motion offline.
`RevenantAnimationBuilder` writes 60 generic clips and updates controllers in place
to preserve GUIDs. Playback cadence follows measured stance velocity and agent
speed. `RevenantGrounding` corrects the visible rig against physical floors.

`revenant_dread.py` adds recessed mouths, individual worn teeth, garment facings,
three fitted helmet silhouettes and three sword profiles. All sword options use
the same palm frame and blade endpoints as contact detection. `RevenantVisual`
selects one of eight paired dye palettes, independently tinting cloth, trousers
and facings via property blocks. Optional helmets replace visible scalp hair;
headgear hides at the far LOD. Bodies remain one skinned draw, with at most two
rigid equipment draws. Shared materials and meshes are never cloned per spawn.

Walkers, Runners and Brutes have five unarmed motions. Spitters have three
retches, Bloaters have four fuse poses, and each armed form has four sword
motions. AI selects a different style from the previous strike and rolls a speed
for both the Animator and the attack clock. Blade contact windows are sampled
in clip seconds. The Runner uses a tighter speed range for its rear-only lunge.
Grounding and recoil apply only to a freshly evaluated animation pose; paused,
disabled or culled rigs cannot accumulate additive transforms. See the
[appearance and motion review](validation/2026-11-17-revenant-horror.md) for
the art, editable-source and gameplay evidence.

Armed attacks use cached blade trajectories during a short active window; contact
detection never samples the live skeleton. `ReceiveWeaponHit` applies Bulwark
guard rules before damage. Surviving hits preserve movement, attack clocks and
navigation. Nine query-only anatomical volumes allow exposed limb/head hits;
`RevenantInjuries` places up to three bone-following blood stains at posed surface
contacts. Dead enemies disable those volumes.

Rebuild with Blender's `Tools/ArtPipeline/revenants.py`, then Unity's
`RevenantBuilder.BuildAndSmoke`, then Blender's `revenant_actions.py`. See
[the source notes](../ArtSource/Revenants/README.md) for commands, material behavior,
hand-frame conventions and validation. The shader uses matte diffuse lighting;
the body does not emit a glowing damage flash.

## Common tasks

### Combat feedback, run synergies and battle escalation

`PlayerControlSpeed.CheckForHits` only adds finisher hit-stop after a weapon contact
lands; guarded contacts use clank feedback without a flesh hit cue. `Arrow.HitEnemy`
adds a shorter stop on a confirmed critical. `PlayerHealth.SuccessfulParry` handles
both offensive parries and perfect shield blocks: instant defensive stagger,
bright sparks/flash, a dedicated cue, camera kick and boon procs. Offensive parries
must face the attacker. Ordinary damage still preserves the enemy state/attack
clock and does not force knockback or stagger.

`CombatTime.SetScale` replaces direct timescale writes in match/menu flow. Hit-stop
is an unscaled overlay, capped at 80ms even for simultaneous contacts; it never
changes `Time.fixedDeltaTime`, physics velocities or animator update modes. Pause,
boon offers, restarts and menu transitions cancel a pending stop. Boss/end-match
slow motion remains the underlying gameplay scale. The camera can render its
short impact kick through hit-stop while actual menu pauses still freeze it;
camera aiming direction remains independent of the visual kick.

New single-stack boons extend the existing offer table and reset with each match:

| Boon | Contact effect |
|---|---|
| Thunder Guard | Parry/perfect block emits a 4.5m line-of-sight shockwave; ordinary enemies stagger 0.85s, bosses 0.4s. |
| Reaper's Cleave | Third strike uses 1.35× reach and a wider forward arc; landed hits burn for 3s at 0.6 damage/s with phantom flame. |
| Splinter Volley | First arrow impact bursts once, dealing 55% arrow damage (cap 2) within 2.6m; excludes the primary target and respects cover/guards. |
| Vampiric Riposte | Successful parry/perfect block heals 6 HP, capped at maximum health. |

Shrapnel does not recursively spawn more shrapnel. Piercing arrows burst once;
fire-arrow splinters also ignite secondary targets for 2s at 0.5 damage/s.
`EnemyController.Stagger` explicitly interrupts attack ownership and guard; generic
`TakeDamage` remains continuous. `FX` creates bounded, transient bursts rather than
per-frame searches. `GameManager.UpdateBattleAtmosphere` caches the initial lighting
and progressively changes fog, ambient fill and sun colour with wave/progress/boss
pressure, keeping bounded darkening for readable silhouettes. Champion waves play
one boss stinger instead of overlapping wave/boss horns. Scene exit restores captured
settings without overwriting the next scene's menu lighting.

### Endless Horde director and signature builds

Endless Horde adds three repeatable signature ranks to the draft table. Its first
card is always a signature power; the other weighted choices favour existing build
families without duplicating a card or exhausting the pool. A cleared wave pauses
before its intermission countdown. `GameManager` validates the selected ID against
the actual offered choices. See [EndlessHorde.md](EndlessHorde.md) for exact combat
rules, event timing, source data rebuild and focused/full smoke instructions.

`EnemyController` uses the director's speed multiplier at navigation/charge sites
and a shared health floor for curse protection, including fire and bleed. Bloaters
defer self-destruction while protected. The objective is separate from the enemy
roster and is hit through real sword overlap and arrow collision paths. Event HUD
guides and timed VFX use the existing Built-in rendering and UI systems; they do
not change rendering quality settings.

### Regenerate horror and combat audio

The latest playback fix makes groans audible at normal encounter ranges: creature
voices reset their pooled attenuation to 9–11m minimum / 60m maximum, preserve
6.5–14kHz breath detail and briefly duck the battle/ambience bed. Groans retry after
pool/throttle rejection instead of losing the entire 3–6s breathing interval; their
clock works independently of pending navigation. Three concurrent ambient groans
are allowed, while attack/death/roar cues retain higher priority. Menu ducking is
disabled, and a cached missing clip is retried rather than kept null indefinitely.
The generator now uses glottal-flow derivatives and continuous moving resonances
for throat rasp. Bow release uses a short string/limb/shaft transient instead of a
pitched musical pluck.

Sword strokes preserve their original 135–185ms contact timing and finish the cut
before another buffered attack can start. The complete single-stroke gesture is
0.43–0.54s, including a shield-safe recovery; combo links remain earlier than the
end of recovery. Rising cuts continue around their signed sweep axis rather than
reversing when an equivalent blade-face normal flips. Forearm pronation is bounded
during authoring, keeping the hand endpoint and grip fixed. Rebuild with
`CombatAnimationBuilder.Build`, then `SwordClearanceReview.Run`. The 250ms input
buffer also accepts attack presses during micro hit-stop, but not during menus.

Village slope fixes use `BuildingBuilder.SoftenHouseSlopes` and
`BuildingBuilder.ClearVillageRoutes`. Three steep passages were locally graded;
all 56 surveyed route surfaces are below 32 degrees. The latter utility trims
route junctions against actual collision hulls, moves obstructing props, preserves
pickup children, refreshes ground contact and rebakes the existing NavMeshData.
The environment rebuild reserves these wider corridors too. `PlayerControlSpeed`
uses a nonallocating support probe and supplies the walkable surface's vertical
velocity while retaining the requested horizontal heading/speed. It still refuses
uphill movement above `maxSlope`; stun/knockback keeps control of the Rigidbody.
`ActionFeedbackSmoke` tests live AI sound triggers, completed combos, and actual
Rigidbody walking/sprinting both ways through the repaired inclines.

```powershell
python Tools/ArtPipeline/sfx.py Assets/Resources/Audio
Unity.exe -batchmode -quit -projectPath . -executeMethod CombatOverhaulReview.VerifyAudio -logFile combat-audio-compile.log
```

`horror_audio.py` replaces regular sawtooth groans with missing/jittered throat
closures, noise-excited resonances, sub-rumble, breath, saliva clicks and foley.
The Brute separately uses distorted octaves and cavern reflections. Combat cues
combine blade drag, steel transients, flesh/bone, damped iron/wood and dry mail/dirt.
The complete bank contains 40 mono PCM WAVs, including four dedicated new cue names:
`parry`, `thunder_guard`, `phantom_cleave`, `splinter`. The match intro preloads
combat clips to avoid first-contact loading hitches, and entering the menu clears
the combat-drum intensity. `PlayAt` multiplies archetype
pitch by a variation up to `Random.Range(0.92f, 1.08f)`; UI/music pitch stays fixed.

The existing `music_menu.wav` is never written by regeneration and its before/after
SHA-256 must match. `menu_theme_legacy.py` preserves the entire old generator verbatim,
including the RNG preamble; only a missing menu is recreated in a temporary folder.
A clean-room rebuild reproduced the shipped theme exactly. See
[the audio source guide](../ArtSource/Audio/README.md) and its synthesis audit.

The unused `com.unity.ai.inference` dependency was removed to eliminate its global
`AttributeBasedFieldGenerator` warning. Its bundled `GraphConverter` indexed
`classSymbols[0]` even when no neural-network operators existed in the gameplay
assembly. No game scripts or assets use that package. Compiler warnings are not
suppressed. Terrain navigation collects physical geometry through `NavMeshSurface`,
so its obsolete `NavigationStatic` flag was also removed.

### Add a zombie type
1. Add a value to `ZombieType` and a row to the table in `ZombieTypes.cs` (stats, tint, glow, points).
2. Give it behaviour in `EnemyController` if it needs any (see how `Spitter` uses `State.Kite`
   and `Bloater` uses `State.Fuse`).
3. Let it spawn: add a weight in `GameManager.PickType`.
It automatically shows up in the How-to-Play bestiary.

### Add a boon
Add a value to `BoonId` and a `Def` to the table in `Boons.cs`; give it a multiplier property or a
case in `Boons.Apply`, and read it where it matters (e.g. `PlayerControlSpeed`, `Arrow`).

### Tune difficulty or modes
`GameSettings.Profiles` sets health, damage, speed, spawn rate, Defend target and alive caps.
`GameManager.UpdateMode` paces the fights. `LastStandDirector` sets the five-minute clock,
corruption bounds and phase timings.

### Add a sound
Drop a `.wav` in `Assets/Resources/Audio` and call `AudioManager.PlayAt("name", position)`.

### Add a UI screen
Use `SharedPanels.Page(parent, name, size, title)` to get a framed, fading page, then fill it
with `UIKit.Button / Slider / Toggle / Selector / InputField`. Look at `SharedPanels.Settings`.

### Terrain and building seating
`EnvironmentBuilder.NaturalHeight` is the ground before buildings: a mound crowned by the keep
(`MoundDrop`), two layers of rolling noise inside the town, then the outer meadow/hills/mountains
(`OuterHeight`). `ComputePads` gives every grounded building a pad (average natural height under its
footprint); `WorldHeight` blends each pad smoothly into the surrounding ground, and `GroundBuildings`
moves each building onto its pad. Pieces standing on another piece ride along with their support;
raised pieces with nothing under them (the keep's bridge) are left exactly where they are.

### Regenerate the world
`Medieval Warrior → Rebuild Environment`. Inputs live in `Assets/Environment/Textures` and the
castle pieces in `Background.unity`. Outputs go to `Assets/Environment/Generated`,
`Assets/Resources/Prefabs`, `Assets/Resources/FX`, `Assets/Animations/ZombieBrain.controller`
and the three scenes. Hand edits to the `Environment` object or to generated assets are lost on
rebuild – change the builder instead (`Assets/Editor/EnvironmentBuilder.cs`, `GameplayBuilder.cs`).

Batch mode (what CI / the smoke test uses):
```
Unity.exe -batchmode -projectPath . -executeMethod EnvironmentBuilder.BuildBatch -shotDir <folder> [-smoke <folder>] -logFile build.log
```

### Rebuild custom architecture

The twelve existing castle-pack prefabs retain their GUIDs and transforms, but their
renderers/colliders now use the custom kit in `Resources/Buildings`. Its editable
master is `ArtSource/Buildings/Buildings.blend`. Run Blender 4.5 headlessly:

```powershell
blender --background --python Tools/ArtPipeline/buildings.py
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.BuildAndReview -logFile buildings-import.log
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.RebakeNavigation -logFile buildings-navigation.log
```

The Buildings editor menu exposes these steps. `BuildingBuilder` captures/measures
the original mesh origins, bounds, child matrices and persistent IDs;
`BuildingImport` reads the Blender binary buffers, configures Standard PBR texture
imports, updates native Mesh streams without changing GUIDs and installs three
LODs per module. It updates original prefabs plus unpacked/direct scene overrides.
`BuildingReview` validates dimensions, component references, collision contacts,
camera spheres, capsule locomotion and navigation routes. Both `Semi-Final` and
the additive `Background` scene are covered; the baseline has 120 prefab roots and
330 architectural mesh nodes. The original castle's missing named window subassets
are repaired during import without moving their child transforms.

UV0 uses unique planar charts within surface atlas cells; UV1 is independently
packed for lightmaps. `Albedo` is sRGB, `Normal` is a Unity Normal Map, and `Masks`
is linear RGBA (metallic, AO, reserved height, smoothness). One shared Standard
material supports instancing; separate closed low-detail physics shells are used
at every LOD. See [the building source guide](../ArtSource/Buildings/README.md) for
exact dimensions, triangle budgets and the 0.2 mm closed CastleTop slab exception.

For house material changes, run `buildings.py -- --only Textures` followed by
`BuildingBuilder.RefreshSurfacesAndReview`. `building_surfaces.py` generates coarse
stucco, aged cracked wood, matte clay and geometry-derived contact AO at every LOD.
`BuildingSurfaces.cs` creates six Standard house-body materials with both specular
highlights and glossy environment reflections disabled. Smoothness is zero for
the dry atlas cells; iron/glass on separate detail renderers keep their own response.
Per-house/LOD occlusion maps use the existing UV0 charts. This refresh preserves
mesh/collider assets, both UV channels, transforms and NavMesh data. The report is
`ArtSource/Buildings/surface-audit.json`; the review includes a world-lighting view.

The current masonry finishing stage adds `environment_finishes.py`, with scanned
CC0 maps pinned by `download_building_scans.py`, baked vertex AO and an authored
supply crate. `WeatheredArchitecture.shader` uses the Built-in Standard lighting
model with metric cylindrical/planar projection and distance-limited POM. The
`ApplyEnvironmentFinishAndReview` utility installs module material variants,
retains crate mesh/prefab/collision identities, bakes sparse terrain-contact shading
and records close-ups. `EnvironmentSurfaceLighting` binds the terrain-height map
once per scene load; it has no Update loop. Ground contact geometry has no collider
and never enters the physics NavMesh bake. Reapply this stage after rebuilding the
world or changing terrain/layout. Full commands, source attribution and performance
limits are in [Weathering.md](../ArtSource/Buildings/Weathering.md).

Use this targeted importer/rebake when changing architecture. The broader environment
builder also rebuilds terrain pads and re-seats buildings. `RebakeNavigation`
preserves the existing NavMeshData GUID and player/spawn transforms. Do not export
a new reference baseline from already-replaced prefabs. Vendor FBXs/licences remain
as layout references; the replacement geometry and atlas images are project-authored.

The village now has a separate, explicit house-spacing revision in
`Tools/ArtPipeline/Buildings/path-layout.json`. `BuildingPaths.cs` opens the 13
originally narrow/overlapping house pairs, using oriented house bounds rather than
centre spacing. Minimum separation is 2.04m including roof edges. Only existing
house roots translate in XZ; elevations, rotation, scale and fortifications stay
fixed. `BuildingReview` applies these recorded translations to root and descendant
mesh expectations, while retaining the immutable source-art reference and all
other compatibility checks.

Run **Buildings → Fix house pathways and rebake navigation** (batch method:
`BuildingBuilder.FixHousePaths`) to refresh local pads, attached plinths/dressing,
dirt paths, prop clearance, terrain contact AO and navigation. Existing supply-crate
pickup children and respawn settings are retained. The normal environment rebuild
calls `PrepareHousePassagesForEnvironment` before baking navigation so newly
scattered rocks, bushes and crates respect the same reserved corridors.
`BuildingBuilder.VerifyHousePaths` performs clearance, bidirectional sprint-step
collision tests and direct NavMesh routes through 25 alleys, also verifying they
connect to the player start. The player uses a Rigidbody capsule at runtime;
the editor locomotion probe uses an actual CharacterController with matching
radius/height and slope limit, supplemented by the full gameplay smoke suite.
Results are in `ArtSource/Buildings/pathways-audit.json` and
[the pathway validation record](validation/2026-11-17-house-pathways.md).

### Run the automated play-test
`Medieval Warrior → Run Smoke Test`, or in batch:
```
Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir <folder> -logFile smoke.log
```
It walks every menu page, plays all three modes, spawns each zombie type, fires arrows, swings
the sword, takes bile damage, collects a pickup, pauses, wins and loses, and fails if any check
fails or any error/exception is logged. Results: `<folder>/report.txt` + screenshots.
Exit code 0 = pass. It never writes high scores.

### Build and self-test the shipped game
`Medieval Warrior → Build Windows Player` (or `-executeMethod BuildTool.BuildWindowsBatch`) builds
`Builds/Windows/MedievalWarrior.exe` (git-ignored). `AutoPlayTest` is compiled into the player but
only runs with `-autotest`:
```
Builds/Windows/MedievalWarrior.exe -batchmode -autotest -logFile autotest.log
```
Look for `[AUTOTEST] RESULT PASS` in the log; the exit code is 0 on success.

## Asset pipeline notes

- **Post-processing** uses the `com.unity.postprocessing` package (Post Processing Stack v2) from
  the Unity registry. If a slow connection makes the download stall, put the extracted package in
  `Packages/com.unity.postprocessing/` – that folder is git-ignored and Unity will use it as an
  embedded package.
- UI sprites in `Resources/UI` must stay *Sprite* type with the 9-slice borders set by
  `GameplayBuilder.ConfigureUiSprites`.
- `Resources/Customization/KnightBase.png` and `KnightMask.png` must be **Read/Write enabled**
  (the builder sets this). The mask channels are R = tabard cloth, G = metal, B = leather.
- Props (bow, arrows, crates, potions) are generated from `ProceduralModels` and saved as prefabs
  in `Resources/Prefabs` so their materials ship in builds.

## Working together

- **Unity version**: everyone on 6000.3.2f1 (any 6000.3.x opens it). Opening with a different
  major version rewrites lots of files – don't commit that.
- **Scenes are YAML and merge badly.** Agree who is editing `Semi-Final`, `MainMenu` or
  `Background` before you start. Most gameplay lives in scripts and in code-built UI, so the
  scenes rarely need hand edits.
- **The world is generated.** Change `EnvironmentBuilder` / `GameplayBuilder`, run
  *Rebuild Environment*, and commit the scripts *and* the regenerated scenes/assets together.
  The builder is deterministic, so an unchanged builder produces identical terrain bytes
  (the 48 MB terrain asset only changes when the terrain recipe changes).
- **Before pushing**: run *Medieval Warrior → Run Smoke Test* (about 2 minutes) and check
  `Temp/SmokeShots/report.txt` says `PASS`.
- **Never commit** `Library/`, `Temp/`, `Logs/`, `UserSettings/`, `Builds/` (already in `.gitignore`).
- If Unity rewrites `Packages/packages-lock.json` to an `"embedded"` post-processing entry
  (because you dropped the package into `Packages/` locally), don't commit that change.
