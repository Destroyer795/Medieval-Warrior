# Revenants: Blender zombie roster

`Revenants.blend` contains eight editable, rigged undead characters. The game
ships their Unity meshes and animations, so playing or building does not require
Blender. The original zombie pack supplies the authoring skeleton and source body
motions. Its visible meshes and materials are replaced, and its humanoid motions
are evaluated offline into the new generic clips.

| Form | Model and role |
|---|---|
| Walker | Decayed anatomy, torn burial clothing, clouded eye, claw attacks. |
| Runner | Leaner silhouette and hooked hands; keeps the existing unpredictable rear-attack AI. |
| Brute | Broader torso, layered shoulder armor and anatomical bracers; charges and slams. |
| Spitter | Gaunt body, distended infected throat, hunched gait and a bile-expulsion animation. |
| Bloater | Asymmetric distended abdomen and infected skin; keeps its proximity/death explosion. |
| Warlord | Crowned sallet, ruined armor, crimson mantle and a larger sword with downward strikes; retains charge, enrage and summoning. |
| Swordsman | Fitted breastplate, sallet, greaves and boots, palm-gripped sword; alternates cuts and thrusts. |
| Bulwark | Armored sword fighter with a bowed oak heater, arched iron rim, boss and rear grip; guards frontal weapon contacts. |

## Anatomy, clothing and surfaces

The continuous body topology comes from the existing **MakeHuman CC0 base** in
[Marchwarden/Anatomy](../Marchwarden/Anatomy/README.md). The source and CC0 text are
already retained there. Silhouette changes, tissue damage, skin variation,
clothing, armor, swords, shields and the crown are authored in Blender by the
reproducible pipeline in `Tools/ArtPipeline/revenants.py`.

Palms, thumbs, knuckles, nail beds, ears, feet and facial features retain anatomical
topology. Finger poses are fitted at their native joints. A blend at the wrist
joins each grip to its forearm without rotating or replacing the forearm surface.
The hand frame is reconstructed from this skeleton's index/little MCP positions
and wrist-to-knuckle direction. The dorsal frame has explicit left/right
chirality. Thumb-side alignment is checked against the actual posed finger bones;
the weapon solver also accounts for that frame's handedness.
The power grip surrounds the handle inside the palm. The other forms use a less
closed claw pose. Finger poses remain fixed during each form's combat animations.

Chest, waist, pelvis, arm and leg volume vary independently. The Runner is gaunt,
the Brute is muscular, the Bloater has a distended abdomen, and the armored forms
have heavier builds. The pelvis-to-waist map preserves vertical volume and blends
continuously into the thighs. Gluteal depth is reduced. The trousers have an
independent seat, waistband and leg envelope, fused into a continuous fabric
surface and reweighted after the union. They do not reuse the skin's cleft or
genital topology.

The cuirass is a separate forged model, with one broad chest plane, a central
keel, closed side panels, shoulder bridges, back plate and raised gorget. It is
not extracted from the skin's breast contours. Greaves follow the shins; individual thigh plates
follow their own leg. Covered skin is removed inside the clothing, with a margin
at openings. Short trouser hems remain above the knees. These are skinned crowd
garments; the player's real-time cloth solver is unchanged.
The ragged jerkins have curved neck openings and bounded tears. Flat leather
baldrics share the torso's weights. Armored footwear uses a closed shoe last,
heel and welted sole, so individual toes do not show through the leather.

Four shared skin materials use four detailed 2048px anatomical UV albedos and a
skin normal map, plus a shared equipment atlas. Diffuse lighting gives skin,
cloth, oxidized armor and hair a matte finish. Hits briefly darken the surface
without adding glowing emission. Vertex colors add regional decay and form-specific coloration.
The shader samples each surface tile independently. Armor, skin, eyes, linen,
leather and wood stay in **one skinned draw per enemy**. Each form has two reduced
Blender distance meshes, at roughly 42% and 16% of the authored triangle count.
Runtime switching uses hysteresis around 20m/39m and never instantiates materials.
Each form also has three fitted hair/beard options, with occasional bald spawns.
Grooms sample roots by scalp area and project their guides back onto the head,
following its curvature. Cropped, swept and longer receding styles include
optional beard strands. Root/tip alpha fades and a soft scalp overlay avoid hard
ribbon ends. Strands are batched per style. Hair
uses one additional draw near the camera and is hidden at the farthest detail
level. Skin, cloth and hair tints vary between spawns.

## Rig and animations

All models use the same 55-joint animation skeleton. Rig frames are exported from
Unity; the mesh import uses their exact inverse bind matrices. Torso cross-sections
keep one anatomical forward axis, avoiding shearing at the old rig's slanted neck.
Limb segments and four normalized influences fit the body to the skeleton.

`RevenantAnimationBuilder` bakes **42 generic skeletal clips**:

- Two attacks per form, with a visible preparation, a short release, carried
  follow-through and return to guard.
- Idle/guard, walk and run for each form, preserving the source motion's weight
  transfer, knee bend, pelvis motion and asymmetrical arm movement.
- Two shared authored falls, forward and backward, preserving joint articulation.

The capture stage evaluates the source humanoid Avatar with Playables at 60Hz,
then bakes ordinary bone position and rotation curves. Weapon grips and combat
timing are layered onto that motion. Walking soles are corrected against the
actual skinned mesh. Blend thresholds are measured from supporting-foot velocity,
and playback cadence follows navigation speed and character scale. Running retains
its airborne phase. Random starting phases
avoid synchronized crowds. At runtime, two nonallocating floor rays correct the
visible rig against terrain; a leg solve handles different floor heights while
retaining authored swing lift. Far meshes skip the leg solve.

There is no humanoid-avatar retargeting in the shipped prefab. Attacks use real
seconds, at `AttackMul = 1`; their contact times match `ZombieStats.windup`.
Armed enemies stop tracking shortly before release. Their cached blade trajectory
must intersect the warrior's collider during a 120ms active window to damage it,
at most once per attack. The sweep samples intermediate times to avoid missing
contact on long frames; it never resamples or snaps the live skeleton. The Warlord's strike aims down to
account for its larger stature. Navigation remains the movement authority.

The reviewed joint-frame samples can be transferred into the `.blend` using
`revenant_actions.py`. It conjugates Unity bind-to-pose matrices into Blender axes,
then keys each rig's local pose. The actions remain editable and named by form.
The `.blend` stores 56 rig actions, including the shared falls on each rig. The
source audit samples idle, walk, run, attack and death playback against Unity's
exported joint frames. The scene range includes the entire longest action.

Saved views: [roster](Preview.png), [back and trouser fit](Back-preview.png),
[groom](Hair-preview.png), [modeled injury](Injury-preview.png),
[blood at a weapon contact](Blood-preview.png).

## Combat integration

Each form has recessed, ragged tissue injuries on different anatomical regions:
cheeks, throat, forearms, shoulder, calf or abdomen. The muscle and clotted margins
share the underlying body's skin weights. These are modeled surface injuries;
the roster does not dismember limbs.

Eight query-only limb capsules and a head sphere supplement the torso collider.
They move with the bones and do not push actors. Arrows include these hit volumes
while ignoring unrelated triggers. Actual weapon contacts project blood onto a
sample of the posed surface; up to three stains follow the selected bones through
movement and death. Blood particles originate at that contact. Shield defense
leaves the exposed sword arm vulnerable, and dead enemies disable the extra volumes.

`RevenantVisual` installs the correct form whenever `EnemyController.Init` runs,
including summoned enemies and testing fixtures. `Revenant.prefab` contains the
animation skeleton, controller, collider, navigation and enemy brain, with no old
renderers. The gameplay scene and spawner use it.

Swordsmen begin appearing after 14% match progress; Bulwarks after 35%. The bestiary
includes all eight forms. The existing Runner, acid, Brute, Bloater and Warlord
special abilities remain in their enemy brain.

Bulwarks guard frontal torso weapon hits. They expose their torso during the
release and recovery of their own attack. Rear hits and shots above the shield can
hurt them; shield bashes open the guard for 1.25 seconds and lower the visible
shield. Guard contact produces sparks, and arrows attach to the animated shield
hand. Piercing arrows stop at a raised shield. Environmental damage bypasses the
weapon guard. Surviving damage adds a small torso recoil without freezing or
interrupting locomotion, the attack clock or the enemy brain.

## Rebuild and review

Run these from the project root, using Blender 4.5 LTS and Unity 6000.3:

1. In Unity, run **Medieval Warrior > Revenants > Export animation skeleton**
   (`RevenantBuilder.ExportRig` in batch mode).
2. Run Blender with `--background --python Tools/ArtPipeline/revenants.py`.
3. Run **Revenants > Import Blender roster** (`RevenantBuilder.Build`). This
   imports all three mesh levels, PBR textures, controllers and clips, and updates
the spawn prefab and gameplay scene reference. Mesh regeneration explicitly uploads
all vertex streams while retaining asset GUIDs; controllers are updated in place.
4. Run Blender with `--background --python Tools/ArtPipeline/revenant_actions.py`
   to attach the exported actions to the editable source and render the lineup.
5. Run **Revenants > Review roster** (`RevenantReview.Run`) for front, head,
   strike and lineup renders and a pose/mesh report. `RevenantBuilder.BuildAndSmoke`
   runs import, authoring review and the full game smoke test together.

The local Blender installation used here is
`Artifacts/Tooling/blender-4.5.15-windows-x64/blender.exe`.

`meshes.json`, `actions.json`, Blender backups, logs and working renders are
reproducible intermediates and are ignored. Commit the `.blend`, pipeline, rig
reference, Unity assets, selected screenshots and validation reports together.
