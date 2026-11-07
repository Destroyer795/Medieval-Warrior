# Gladiator helmet and taller stature

The conical helmet is replaced with a gladiator-inspired forged helmet: a broad
ridged dome, a swept steel crest, an overhanging brow, narrow eye openings,
perforated face protection, reinforced cheek plates and a flared nape guard.
The eye slits and breathing apertures are actual openings through the steel.
All helmet pieces follow the head bone over the existing mail aventail.

The complete warrior is uniformly **10% taller**. The Blender source presents
the body and its armature beneath `Marchwarden stature`, scaled to 1.10. Unity
receives canonical meshes/bind poses plus a separate `statureScale` value.
`MarchwardenStature` applies that factor to the complete runtime rig, equipment
and collider, retaining the native scale so repeated setup cannot compound it.
The same setting is used by the gameplay character, prefab and menu preview.

Locomotion cadence accounts for the larger stride. Shield bracing, its forearm
mount, stowed equipment and bow grip offsets account for the character's scale.
The bow's facial anchor sits beside the cheek guard and below the eye opening;
the archer opens his shoulders a little further to keep the bow arm downrange.

The current design is authored by `Tools/ArtPipeline/marchwarden_gladiator.py`,
executed after `marchwarden_cinematic.py` and before the finish bake. Change
`STATURE_SCALE` there to alter the coordinated source/runtime size, then follow
the [existing Blender and Unity rebuild workflow](Cinematic.md#rebuild-and-review).

[Helmet close-up](Helmet-Unity.png) · [Helmet side view](Helmet-Side.png) ·
[Full warrior](Warrior-Unity.png) · [In game](InGame.png)

The Unity review frames the helmet from the bounds of its head-weighted geometry,
so the crest is included in the close-ups. The full gameplay smoke test also
checks the menu/gameplay stature, repeated setup, visible posed height, and all
existing locomotion, sword, shield and bow checks.

## Verified 2026-11-04

The final run passed **155 gameplay checks and 45 combat/defense checks**, with
no gameplay errors or exceptions. The posed warrior measured approximately
**1.85 m including the crest**. The menu and gameplay both apply the 1.10 stature
factor, and repeated initialization leaves that scale unchanged.

The fitted bow arm measured 25.9 and 25.8 degrees from the shot direction after
the tested left/right yaw changes. Grip, facial anchor, arrow alignment, sword
combos, shield bracing and stowed equipment checks also passed. Reports are
saved locally as `Artifacts/Marchwarden/Gladiator/gameplay-report.txt` and
`combat-clearance.txt`. The editable body contains 473,574 triangles including
the linked mail, detailed fittings and the new perforated helmet.
