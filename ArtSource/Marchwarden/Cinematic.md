# Cinematic warrior revision

The [later gladiator revision](Gladiator.md) replaces the conical helmet and adds
10% to the complete warrior's stature. The following describes the outfit and
animation foundation retained by that revision.

The latest design reference is [the user's warrior render](References/Cinematic-warrior.png).
The outfit is authored as 3D geometry in Blender, then imported into Unity as native
meshes, four-influence skin weights, materials, equipment mounts and garment morphs.
The game's existing built-in rendering pipeline uses Standard PBR materials.

The new silhouette has a broad chest, a tapered waist, shaped shoulders, full linen
sleeves and gathered trousers. Continuous garments replace the old exposed limb
assemblies. Curved blackened iron lamellae overlap a leather foundation and continue
onto articulated shoulder and hip panels. The open-face mail aventail includes real
interlinked geometry over its backing. The conical helmet has a rolled brow and
peened fasteners. Boots have continuous toes, insteps, ankles and calves, with
compression creases and flat leather bindings.

The heater shield has a bowed steel shell, a shallow central ridge, broad fitted
iron reinforcement, rolled edges, flush rivets, restrained strike dents, padded
enarmes and a bound palm grip. Its face remains separate from the armor color
customization. The coordinated default palette is undyed linen, blackened iron,
charred leather and an ebony bow; subsequent customization is saved normally.

## Motion

The licensed animation skeleton and locomotion clips remain in use. The existing
twelve authored sword clips, shield defense IK, bow IK and weapon stowing continue
to operate on that skeleton. No demo character vertices are used by the replacement.
The face remains derived from the documented [CC0 anatomy source](Anatomy/README.md).

Shoulders blend across upper arm, clavicle, spine and neck. Elbows, hips, knees,
ankles and the aventail use continuous weights appropriate to their joints. The
renderer explicitly retains four influences at every graphics quality level.
Linen gores and articulated skirt panels follow the thighs and pelvis, with four
additional sparse morphs for gentle forward and lateral movement. Damped springs
drive those morphs from movement and turns, using substeps and a teleport reset.
This is bounded secondary animation, rather than a full cloth simulation.

The importer measures the supporting-foot speed of the walk and charge cycles.
The runtime adjusts only the locomotion clock to match travel speed; normal travel
uses a jog above the combat-walk cycle's natural range. Sword attack timing uses
its independent clock. Combo blending, full-body shield bracing and bow draw/release
retain their existing checks.

## Rebuild and review

1. Export the joint reference with **Medieval Warrior > Marchwarden > Export animation rig reference**.
2. Run `Artifacts/Tooling/blender-4.5.15-windows-x64/blender.exe --background --python Tools/ArtPipeline/marchwarden.py`.
3. Run **Medieval Warrior > Marchwarden > Import Blender exports** in Unity.
4. Run **Medieval Warrior > Rebuild sword animations**.

`marchwarden_cinematic.py` authors the current outfit on top of the retained surface
and anatomy helpers. `marchwarden_finish.py` packs and bakes the 4K mesh-bound finish.
The Blender source is `Marchwarden.blend`; Unity assets are in
`Assets/Resources/Marchwarden`, and the prefab is `Assets/Resources/Prefabs/Marchwarden.prefab`.
Existing gameplay and menu scenes acquire the model through `KnightAppearance`.
`marchwarden_source_rig.py` gives the editable source its joint hierarchy and
anatomical bone directions. The source-rig check retained the rest mesh within
0.0000002 m while parenting all 18 joints.

For a geometry preview, append `-- --draft` to the Blender command. Drafts are saved
under `Artifacts/Marchwarden/Cinematic` and do not replace the final mesh export or
the editable Blender source. They regenerate the reusable material tiles.

`MarchwardenBuilder.BuildAndSmoke` imports the art, rebuilds attacks, renders the
Unity comparison, samples combat/defense clearance, and runs the full gameplay
smoke test. The cinematic run stores its gameplay report and actual motion frames
in `Artifacts/Marchwarden/Cinematic/Smoke`. The separate validation project writes
its authoring-clearance report in its own `Artifacts/Marchwarden` directory.

`motion_preview.py --folder Artifacts/Marchwarden/Cinematic/Smoke` assembles the
captured sword, bow and shield-defense frames into lossless animated PNGs.

## Verified 2026-11-04

The final revision passed **152 gameplay checks and 45 combat/defense checks**.
The runtime report recorded no gameplay errors or exceptions. Neutral shield/chest
separation measured 0.0471 m; the live block entry/hold/release sequence retained
at least 0.0477 m. Both feet stayed planted during the braced block, and the
authoring transition's largest sampled joint change was 2.23 degrees.

All twelve sword attacks and adjacent combo transitions passed their conservative
weapon-clearance checks. Four-influence skinning and bounded secondary garment
movement were verified in gameplay while walking and running. Bow aim, draw,
release, stowing, quiver ammo and the existing game modes also passed.

[In game](InGame.png) · [Unity model](Warrior-Unity.png) ·
[Blocking](Blocking-InGame.png) · [Bow](Aiming.png)

The actual motion captures are assembled as `Artifacts/Marchwarden/Preview-sword.png`
(42 frames), `Preview-bow.png` (20 frames), and `Preview-block.png` (28 frames).
The source model contains 401,508 body triangles, including the linked mail and
fine surface fittings. These checks cover the captured actions and transitions;
they do not assert that every possible pose is free of garment intersections.
