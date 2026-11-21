# Endless Horde

Every cleared wave heals 25% of maximum health, awards its wave bonus and pauses
for a three-card power draft. Select with the mouse, controller UI navigation or
number keys 1–3. The nine-second intermission starts counting down after selection.
A Brute leads every fifth wave; every tenth wave adds a Warlord alongside it.

## Builds and drafting

Horde guarantees at least one signature power in each offer. Remaining choices
favour powers in an existing guard, blade or bow build, while leaving a chance to
pivot. Cards never duplicate within one offer. Owned powers show their next rank;
compatible unowned powers show **BUILD SYNERGY**. All upgrades reset on a new run.

| Signature | Effect | Companions |
|---|---|---|
| Shockwave Parry | Perfect blocks/parries discharge a 4.5m stun burst, then lightning chains to nearby undead. The first target can be within 6m; subsequent jumps reach 4.5m. Rank adds damage and targets, up to 12 targets. | Thunder Guard, Bulwark, Vampiric Riposte |
| Whirlwind Strike | Third cuts pull visible undead toward the sweep for 0.48s, widen the strike, and bleed them for 4s. Bosses resist 80% of the pull. | Reaper's Cleave adds phantom burn; Keen Edge increases cut and bleed damage. |
| Explosive Quiver | A confirmed headshot scatters 3–5 delayed shrapnel bombs. Bursts begin after 0.22s, 0.09s apart; each has a 2.2m damage radius. | Fire transfers ignition; Piercing can reach a head behind the first target; Splinter Volley adds its normal immediate burst. |

These three powers have repeatable ranks, so a long run never exhausts its draft
table. Damage continues to grow while visual counts, chain targets, pull speed and
reach have caps. Bleed refreshes duration and keeps the strongest DPS; it does not
spawn a coroutine or freeze an enemy's brain. A piercing arrow triggers only one
cluster volley. Secondary damage cannot recursively trigger more volleys.
All area attacks check physical cover. A pull uses bounded NavMesh movement rather
than teleporting an enemy through architecture.

## Director events

Waves 3, 6, 9, etc. roll one event for that wave. A shuffled three-event bag prevents
immediate repeats and shows all three before refilling. Every event has a persistent
HUD rule/objective panel and a warning cue. Boss announcements retain their own banner.

| Event | Rules and counterplay |
|---|---|
| Blood Moon | All undead navigation and charge movement gain exactly 25% speed. Kill score gains a separate ×3 multiplier, preserving combo and boss bonuses. Only the eye atlas region glows red. Fog and sunlight blend toward crimson without removing ambient readability. |
| Toxic Fog | Low, verified village passages fill with animated green gas. A 4.5s warning precedes damage; exposure checks run twice per second. Gas uses each passage's local floor height and a 1.45m damaging layer. Elevated decks are safe. A reachable stone bridge receives a direction/distance guide. |
| Cursed Totem | A chamfered, rune-carved basalt obelisk rises over 3.2s in a reachable space with navigation clearance. Enemies within 12m cannot die to weapons, burn, bleed or a Bloater's self-detonation. Violet tethers show lethal-hit protection. Strike the 18-health obelisk with sword or arrows; destruction immediately breaks protection. The objective has its own collider/health and never counts as a zombie. |

Hazard damage, warning clocks, rune emergence, projectiles and ribbons use scaled
game time, respecting pause, drafts and impact stops. Event objects are scene-owned
and cleared on wave completion, defeat, disable and scene exit. The castle world
can remain loaded between runs without retaining gas or immortal enemies.

## Implementation and asset rebuild

- `HordeDirector` owns the event bag, local hazard occupancy, bridge guide and lifetime.
- `HordeTotem` owns objective health, bevelled geometry, collision and curse feedback.
- `CombatBoons` owns contact-driven chains/pull/bleed; `ClusterVolley` owns delayed bombs.
- `EnemyController` applies temporary speed/eye state and the shared lethal-damage guard.
- `GameHUD` presents rules, objectives and synergy cards using the existing visual style.
- `Resources/Horde` contains build-included shaders and the village passage manifest.

Rebuild passage data after changing/revalidating the village layout:

```powershell
python Tools/ArtPipeline/horde_arena.py
```

The exporter consumes the existing `ArtSource/Buildings/slope-audit.json` and building
mesh manifest. Runtime probes resolve the current NavMesh heights. Bridge keys refer
to imported mesh IDs, preserving the existing prefab/scene GUIDs and architecture.
No rendering presets, texture resolutions, mesh detail or shadow settings are reduced.

Particles have a 55-per-passage limit; chains use 15-point timed ribbons; cluster
volleys have a 24-live-volley safety budget. Shared materials avoid per-hit material
instances. The only scene-wide bridge scan happens once during match preparation;
combat and hazard checks do not enumerate the environment every frame.

## Verification

`HordeOverhaulSmoke` runs through real menu loading, twelve wave starts, three
successive drafts, capped-table drafting, actual contact procs, local gas exposure,
bridge safety, and all eight archetypes under the curse. It also fires a ballistic
arrow and performs a sword query against the objective, then checks cleanup.
The normal full smoke suite includes this review in its Endless Horde section.

For a focused run, create `Artifacts/HordeReviewOnly.txt` in the validation project's
working directory, then run:

```powershell
Unity.exe -batchmode -projectPath <validation-project> -executeMethod SmokeTest.RunBatch -shotDir Artifacts/Horde/Smoke -logFile horde-smoke.log
```

Remove the marker before running the full suite. Screenshots and `report.txt` are
written beneath the selected output folder. These are Editor gameplay checks;
release-build frame rates and long-run difficulty still need standalone playtesting.
