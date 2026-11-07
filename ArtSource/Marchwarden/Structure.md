# Warrior structure and equipment

This revision increases upper-body volume, replaces deforming hip armor with a
rigid articulated assembly, arches the heater crown and reduces the quiver.
The editable source remains `Marchwarden.blend`; Unity uses its mesh exports.

## Proportions and shield

The rib cage is up to 15% wider and 14% deeper, with the change fading into the
waist and neckline. The proximal sleeve and shoulder profiles grow with it.
The original locomotion skeleton, hand grips and 1.10 stature factor remain
compatible with the new silhouette.

The heater crown rises in a continuous 65 mm arch from its outer corners.
Its reinforced band, peened fasteners and rolled rim follow that geometry.
The neutral left-arm guard and shield stand-off are fitted to preserve sightlines,
glove clearance and the complete rapid sword set.

## Hip armor

Twenty strips each contain three overlapping rigid lamellae. All 180 source
objects (plates and their rivets) are bound to 60 dedicated joints, with exactly
one influence per vertex. The root pivots sit within the waist belt; lower joints
form three-link chains. Metal no longer inherits blended thigh deformation or
the old cloth morphs.

`MarchwardenArmor` drives the 60-joint renderer after the linen simulation.
Each plate has contact probes covering its thickness, edges and fasteners.
The leg capsules constrain the assembly, while a spatial index of the actual
cloth triangles supports radial contact queries at the current probe height.
This avoids testing a lifted plate against cloth which has moved below it.
The damped suspension returns plates toward a hanging posture; limited lateral
motion accommodates folds and diagonal leg movement.
Parents also keep the following plate's attachment bar outside the cloth and
limbs, preventing an otherwise immovable child pivot from becoming trapped.

The body contains 437,654 triangles, the rigid hip assembly 17,280, and the
simulated linen 2,464. The plates share one additional skinned renderer rather
than using a separate renderer for every hinge.

## Quiver

The bag is 65% of its previous width/depth and 70% of its previous height:
approximately 70% less volume. Its canonical height is about 37 cm, or 40 cm on
the scaled warrior. Existing ammunition counts and customization remain intact;
capacity upgrades pack arrows without enlarging the bag.

The suspension attaches behind the right hip, follows pelvic movement and lets
the bag hang under gravity. Damped sway reacts to acceleration. Contact checks
include the bag, exposed arrows, right arm/hand, torso, legs and stowed shield.
Contact can pivot the bag around the attachment before increasing strap travel.
Twin flat leather hangers visibly connect the belt to the quiver tab and follow
its motion. Their material uses the leather detail tile and live leather tint.

## Rebuild

Run `Tools/ArtPipeline/marchwarden.py` in Blender 4.5 LTS, then import the export
and rebuild sword animations in Unity. `marchwarden_structure.py` creates the
rigid pieces, hinge metadata and compact quiver before the surface bake.
`repair_armor_hinges.py` can update the existing root-pivot positions without
changing geometry, UVs or the baked finish.

`MarchwardenBuilder.BuildAndSmoke` performs the complete review. An optional
`Artifacts/StructureReviewOnly.txt` file in the validation project keeps the
authoring review and stops runtime testing after the structural movement stress
sequence. Remove it for the complete gameplay regression.

## Verified 2026-11-04

The final run passed **202 gameplay checks and 45 animation/defense authoring
checks**, with no runtime errors or exceptions. All twelve sword strikes and the
full bow draw, including both tested yaw directions, retained quiver clearance.
The movement stress sequence covered walking, sprinting, turning, dodging,
blocking, a cut and a thrust.

The sampled rigid plates retained at least **11.2 mm leg clearance and 6.0 mm
cloth clearance** through that sequence. Belt attachment drift measured zero.
The quiver/arrow envelopes retained **15.8 mm arm clearance and 55.0 mm torso
clearance**; suspension contact travel stayed within **108 mm**. The heater
crown measured **48 mm above its outer shoulders**, and its neutral rim stayed
**26.3 mm below the head joint**.

Read-only Blender QA confirmed all 60 pivots match the export and all 180 armor
objects have exactly one matching influence at weight 1. Updating the pivots
changed evaluated resting geometry by less than 0.00000003 m.

- [Front view](../../Artifacts/Marchwarden/Structure/Front.png) and [rear quiver view](../../Artifacts/Marchwarden/Structure/Back.png).
- [Actual Unity movement preview](../../Artifacts/Marchwarden/Structure/Preview-cloth.png).
- [Sword preview](../../Artifacts/Marchwarden/Structure/Preview-sword.png) and [bow preview](../../Artifacts/Marchwarden/Structure/Preview-bow.png).
- [Gameplay report](../../Artifacts/Marchwarden/Structure/gameplay-report.txt) and [authoring report](../../Artifacts/Marchwarden/Structure/combat-clearance.txt).
- [Source rig audit](../../Artifacts/Marchwarden/Structure/source-rig-audit.json).
