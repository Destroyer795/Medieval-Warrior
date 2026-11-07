# Marchwarden combat and live linen

The sword set uses compact ready positions, descending and rising cuts, returning
cuts, and direct thrusts. Preparation, contact, follow-through, recovery, damage
and sound share the timings in `SwordMoves.cs`. These are game adaptations
informed by historical fencing material, rather than reconstructions of a
particular sword-and-heater system.

| Strike | Contact | Duration |
|---|---:|---:|
| Descending forehand | 150 ms | 370 ms |
| Returning backhand | 145 ms | 360 ms |
| High descending cut | 180 ms | 440 ms |
| Rising forehand | 145 ms | 360 ms |
| Horizontal cut | 145 ms | 360 ms |
| Straight thrust | 145 ms | 390 ms |
| Descending backhand | 155 ms | 380 ms |
| Low rising return | 145 ms | 360 ms |
| High-line thrust | 160 ms | 410 ms |
| Short outside cut | 135 ms | 340 ms |
| Inside returning cut | 150 ms | 370 ms |
| High backhand finish | 185 ms | 450 ms |

The cutting phase takes 65 ms, or 55 ms for thrusts. The follow-through carries
the hand past contact before recovering. A 160 ms input buffer accepts an early
follow-up; the next attack can begin 85 ms before the current clip ends. Combo
cross-fades last 50 ms and preserve the outgoing layer weight. The sword and
shield have a shared ready guard, while locomotion continues on the lower body.
Quaternion smoothing keeps the rapid hand paths continuous without wrist flips.
The shield opens before the blade and stays open through its recovery.

## Cloth construction

`marchwarden_cloth.py` replaces the old thigh-driven tunic gores with two
continuous linen panels, split at the front and back to allow a stride. The
editable geometry is included in `Marchwarden.blend` and exported as a separate
`Tunic` mesh. It has 2,464 triangles and 1,334 welded simulation particles,
independent of the much denser armored body. The dedicated linen shader draws
both faces and retains the baked finish, tangent normals and woven detail.

`MarchwardenCloth` initializes Unity's native PhysX Cloth on the skinned linen.
The waist edge is pinned (58 particles). Freedom increases to 17 cm at the
hem; tethers, high stretch stiffness and damping retain the fabric's length
while allowing momentum and gravity to shape it. Continuous collision, virtual
triangle particles and self collision are enabled, at 180 solver iterations per
second. Large respawn or test teleports clear inherited motion.

Thirteen fitted capsule proxies follow the actual skeleton: two for each thigh,
two for each calf, a heel/instep and a forefoot/toe capsule on each foot, plus a
pelvis capsule. Their endpoints are authored in the reference pose and converted
into each bone's local space. Thigh envelopes include the gathered trousers,
folds and seam thickness. They occupy the reserved `ClothCollision` layer,
isolated from gameplay contacts. The proxies are assigned explicitly to Cloth;
their use does not add physical forces to the player's controller.

The visible mesh reads the actual native particle output into a small dynamic
surface, with UV seams mapped to welded particles and smooth shared normals.
This avoids the imported rig's native cloth rendering issue. An 8 mm final
capsule contact projection runs after procedural bone poses, so a fast leg does
not pass through the rendered fabric between native simulation updates. The
projection also resolves virtual triangle contacts and preserves the panel's
outward side when a limb moves across it. The fabric's smoothness is capped at
0.12; sharp specular highlights and glossy reflections are disabled for linen.

The collision audit converts native cloth particles from the unscaled root-bone
frame. Native particle positions already include inherited character scale;
applying the imported renderer transform again would produce invalid results.
The waist follows the pelvis and the cloth remains free of thigh skin weights.
The audit checks both rendered particles and triangle centroids; it also checks
native belt attachment, finite positions, bounded motion and teleport recovery.

## Rebuild and review

1. Run Blender 4.5 LTS with `--background --python Tools/ArtPipeline/marchwarden.py`.
2. In Unity, import Blender exports and rebuild sword animations under the
   **Medieval Warrior** menu.
3. Run `MarchwardenBuilder.BuildAndSmoke` in the validation project for the full
   authoring and gameplay checks. The sword audit samples all twelve clips and
   their adjacent blends at 180 Hz. Runtime cloth review exercises walking,
   sprinting, turning, dodging, blocking, a cut and a thrust.

For a focused runtime review using the existing imports, create
`Artifacts/ClothReviewOnly.txt` inside the validation project. This skips art and
sword regeneration and stops after cloth review. Remove it for the full suite.

`motion_preview.py --folder Artifacts/Marchwarden/Cinematic/Smoke --output
Artifacts/Marchwarden/CombatCloth` assembles the real Unity captures. Sword
previews sample every two frames at 60 Hz; cloth previews sample every six frames.

## Verified 2026-11-04

The final complete run passed **167 gameplay checks and 45 combat/defense
authoring checks**, with no runtime errors or exceptions. The cloth stress
sequence used 294 frames at 60 Hz, covering walking, sprinting, turning,
dodging, blocking, a cut and a thrust.

The measured rendered particles and triangle centroids retained approximately
**8 mm of clearance** from the fitted capsule envelopes. Native waist attachment
error peaked at **7.7 mm**, visible displacement peaked at approximately **0.270 m**, and
the lowest hem point remained **0.369 m above the character root**. The teleport
recovery, rendered visibility, moving legs during attacks, input buffering,
weapon clearance and bow regressions all passed.

- [Sword motion, actual Unity frames](../../Artifacts/Marchwarden/CombatCloth/Preview-sword.png): 49 frames at 30 Hz.
- [Cloth motion, actual Unity frames](../../Artifacts/Marchwarden/CombatCloth/Preview-cloth.png): 49 frames sampled at 10 Hz from the 60 Hz stress sequence.
- [Gameplay report](../../Artifacts/Marchwarden/CombatCloth/gameplay-report.txt).
- [Combat/defense authoring report](../../Artifacts/Marchwarden/CombatCloth/combat-clearance.txt).
- [Current Unity front view](../../Artifacts/Marchwarden/CombatCloth/Front.png).

The native simulation remains independent of the 451,814-triangle armored body;
only the 2,464-triangle tunic is simulated and its small display mesh updated.

## Reference material

- [Royal Armouries: medieval German fencing manuscript I.33](https://royalarmouries.org/objects-and-stories/up-close-online-exhibition/medieval-german-fencing-manual).
- [Getty: Fiore dei Liberi, Combat with the Sword](https://www.getty.edu/art/collection/object/108HNH).
- [Unity 6.3 Cloth manual](https://docs.unity.com/en-us/engine/6000.3/manual/physics-section/physics-overview/class-cloth).
- [Unity Cloth bindings](https://raw.githubusercontent.com/Unity-Technologies/UnityCsReference/master/Modules/Cloth/Cloth.bindings.cs).
