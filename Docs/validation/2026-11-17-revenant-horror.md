# Revenant anatomy, outfit variety and attack continuity

The eight shipped forms were rebuilt from the continuous CC0 anatomical source
in Blender 4.5.15 and imported into Unity 6000.3.2f1. Existing mesh, controller,
clip and prefab identities are retained. The compressed editable master is
`ArtSource/Revenants/Revenants.blend`.

## Appearance

- Rounded deltoids, a fuller neck transition, independently varied chest/waist/
  limb mass, reduced trouser seat and articulated calf-weighted trouser legs.
- Sunken facial planes, regional tissue injuries, recessed open mouths, retracted
  gums and separate irregular enamel teeth. No glowing eyes or glossy skin.
- Eight paired dye palettes, with independent primary cloth, trouser and facing
  channels. Neutral textile atlas tiles preserve the dyed colors in night light.
- Three fitted helmet silhouettes per form: sallet, kettle and nasal. Warlords
  retain their broken royal coronet. Helmet selection suppresses scalp hair.
- Three sword profiles per armed form. Every variant uses exactly the existing
  palm mount, ricasso and tip; contact trajectories remain valid for every option.
- Shared materials/meshes and property blocks; one skinned body draw and at most
  two rigid gear draws. Hair/headgear hide at the far LOD. Both body LODs remain.

Near body triangle counts are 56,742 Walker, 52,698 Runner, 63,498 Brute,
53,966 Spitter, 53,406 Bloater, and 63,498 each for Warlord, Swordsman and Bulwark.
Fabric folds are reduced before facial or hand topology to retain the 65k budget.

See the [runtime roster](../../ArtSource/Revenants/Preview.png),
[outfit samples](../../ArtSource/Revenants/Outfit-preview.png),
[equipment silhouettes](../../ArtSource/Revenants/Equipment-1.png) and
[Blender lineup](../../ArtSource/Revenants/Blender-preview.png).

## Motion

The roster now has 50 generic clips: 24 attacks, 24 locomotion loops and two falls.
Walkers, Runners, Brutes and Bloaters each have four unarmed actions: diagonal
claw, left backhand, bilateral grab (Brute hammer slam) and rising rake.
Spitters have two retching actions; armed forms retain cut/thrust combinations.
AI excludes the immediately preceding style when choosing the next attack.

Hermite hand trajectories carry velocity through contact. Baked anatomical wrist
solves, bounded world-hand orientation and forearm pronation eliminate roll
singularities. Preparation/recovery blend with guard; fixed-time transitions and
exactly closed locomotion loops remove pose snaps. Existing contact times,
cooldowns, rear-only Runner attacks and blade-window damage rules are retained.

The [184-check authoring report](2026-11-17-revenant-horror-review.txt) passes.
It samples every attack at 120Hz: maximum measured wrist bend is 52.0 degrees;
maximum arm orientation step is 26.6 degrees. All three armed forms still cross
their gameplay contact corridor. All eight walking soles and locomotion seams
pass, as do mesh budgets, weights, anatomical hand frames and equipment imports.

The [Blender action audit](2026-11-17-revenant-horror-source.json) passes across
all eight rigs and 64 editable actions, including every new attack and both falls.
Maximum transferred joint-matrix error is less than 0.000010.

The [focused 173-check gameplay report](2026-11-17-revenant-horror-pose-gameplay.txt)
passes, including all four unarmed Animator variants, non-repeating AI choices,
physical arrow hits to exposed arms and standing foot contact. Grounding and
recoil now require a newly evaluated pose: disabled/cull-stopped rigs and paused
time cannot accumulate additive offsets. A dedicated pause/disabled-rig check
verifies stable bones and hit volumes. Ordinary damage still preserves AI and
animation clocks; explosion cleanup hides all equipment with the body.

The final [complete 459-check gameplay report](2026-11-17-revenant-horror-gameplay.txt)
passes with zero logged gameplay errors/exceptions. Unity's runtime and Editor
assemblies compile with zero C# errors or warnings. The complete run covers the
new attacks/appearances, ordinary damage continuity, physical arrows, parries,
Runner rear attacks, all 24 sloped-passage traversals, player sword/cloth/equipment
checks, pause/resume, boons and all game modes. The focused test marker was removed
before this run. Menu audio assets are unchanged.

## Rebuild

```powershell
blender --background --python-exit-code 1 --python Tools/ArtPipeline/revenants.py
Unity.exe -batchmode -projectPath . -executeMethod RevenantBuilder.BuildAndSmoke -shotDir Artifacts/Revenants/Smoke -logFile revenant-gameplay.log
blender --background --python-exit-code 1 --python Tools/ArtPipeline/revenant_actions.py
```

For motion-only authoring use `RevenantBuilder.AnimateAndReview`. The full smoke
suite exercises actual Animator attack variants, dye properties and equipment
selection, followed by the existing damage, arrow, guard, navigation and gameplay
checks. Standing fixtures explicitly reset the locomotion speed/phase after
sampling attacks and suspend unrelated horde actors while probing limb contacts.
