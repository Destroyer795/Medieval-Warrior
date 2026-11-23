# Medieval Warrior 1.2

Work in the order below. Each batch ends with a muted check and a checkpoint recording the files, test report and Windows build state. The last verified build before this plan is the repaired south gatehouse from 22 November. Keep that work in the checkout.

## 1. Siege footing and wall tools

- Lower the parapets, widen firing slots, and test both the view and actual bow clearance down to the outer doors. Retain the central drop channel.
- Load five visible stones at the drop station. Each use releases one. The station loads the next automatically after a short reset and marks the supply empty after the fifth.
- Clear stair-foot obstructions, blend the feet into the courtyard, and check that normal walking works in both directions. Use continuous player collision and test a shove on the new cobbles and at their shoulders.
- Capture the parapet and descent in a muted Windows build.

## 2. Siege pressure and objectives

- Start the attackers farther out in the field with several visible approach lanes. Use elapsed active play time to escalate the outer assault and ramp into a large final push. Ordinary runs should force a breach. A player who holds the gate through the final push can earn an outer-wall victory. Keep the countdown and gate condition visible through the assault.
- Let an attacker choose between the knight and the nearest reachable gate. Avoid target flicker and respect walls, height and pathing.
- At breach, transition to a live inner survival phase. Keep ordinary inner spawn pressure at its previous level. Route reinforcements from the open frontage and connected positions along the front of the inner gate, with no rear spawns. Test the time and objective handoff, surviving attackers, and win and defeat conditions.
- Tune the final push on a run where the knight does little and a run that uses arrows and stones well. Retest all difficulty settings.

## 3. Endless Horde encounters and balance

- Allow two seconds of play after clearing a round before showing the draft. Prevent the next round from starting under an open draft.
- Place the goblet inside near the central castle wall by the normal spawn, with a reachable position in supported layouts. Retain the existing score bargain.
- Bring the grave duelist into Endless Horde using its shared reveal, combat and loot. Schedule it for a readable point in a run and account for it in wave completion.
- Every five waves, increase health, movement speed and damage for the full roster, including basic walkers. Show the tier and change the banner color at the same boundaries. Balance ordinary boons and early escalation together. Target wave 20 as attainable, award an achievement for clearing it, and make 25 a steep escalation.
- Test waves 1, 5, 10, 15, 20 and 25, save and achievement behavior, and combinations of tier, boss, goblet and boons.

Use waves 5, 10, 15, 20 and 25 as entry points for each new tier. Apply the tier stats once when an ordinary enemy spawns, with existing temporary effects layered over them. Treat the grave as a separate duel so it can retain the knight's own gear and damage. Try reducing the existing between-wave heal before weakening the more distinctive boons. Compare bow and melee runs on Knight difficulty before settling the multipliers.

## 4. Shared combat and animation

- Diagnose the Brute's head twist and mesh separation in the affected clips and states. Check the repaired model during locomotion, attacks, stun and death.
- Fix close bow shots by comparing the camera ray with the arrow muzzle, including a target between the camera and muzzle, enemies inside contact range, and an intervening wall. Check that ordinary long shots keep their trajectory.
- Improve the knight's death animation and verify it with sword and bow equipped.

## 5. Customization and release

- Display version 1.2 and credit S Sanjeev Srinivas and Pranav Kishan T Y.
- Invert the drag direction in knight customization. Fit the selected crest to the shield face, then inspect its placement and orientation during preview, guarding and movement.
- Run regression and muted standalone checks and rebuild in Builds/Windows.

## Sword selection

Customise Knight previews all seven designs and saves the equipped sword by asset ID. The three starter swords are Northmere, Fenland and Briar. Saltire, Saffron, Ivory and King's Sword use the existing Defender of the Realm, Dawn Breaks, Unbroken and Last Oath feats, respectively. Each design shares the knight’s standard combat stats. The prefabs live under `Assets/Resources/SwordCollection` and the editable Blender collection is `ArtSource/Marchwarden/SwordCollection.blend`.

To regenerate the prefabs, run Python on `Tools/ArtPipeline/sword_collection.py`, then use the Unity menu **Medieval Warrior → Art → Build sword collection**. Running the same Python file inside Blender also regenerates the editable master. **Medieval Warrior → Combat → Review collection sword handling** checks every design against the animated knight and shield.

## Checkpoint

- Batches 1 and 2 are implemented in the working tree and included in Builds/Windows/MedievalWarrior.exe. Blender reopened the source with all five stones and linked textures. The muted regression suite passed 682 checks, including stairs, bow clearance, knockback across the road, the final push, the breach and the inner survival timer. The report is Artifacts/v12-final-20261123-015702/report.txt. The rebuilt player passed 17 checks in Logs/v12-player-20261123-020431.log. The Unity build reported zero errors and 19 warnings. The source changes remain local.
- Siege tuning lives in OuterSiege.cs and GameManager.UpdateSiege. The initial values are a 125-second outer defense, a 12-unit reinforcement column at 90 seconds, and a 100-second inner hold. The standard inner cap and cadence follow the previous difficulty profile. The final-push and deadline tests step the clocks to cover the transition. Run full human balance passes before claiming a breach rate.
- Batch 3 is integrated locally. The focused Horde run passed 100 checks in Artifacts/v12-horde-20261123-021115/report.txt. It covered the delayed draft, central goblet, tier boundaries, grave and recovery. The reviewer raised the provisional pacing score from 5/10 to 7/10 and clarity from 6/10 to 7/10. Those scores use scripted gameplay and screenshots.
- Batches 4 and 5 are integrated. Point-blank bow and thin cover checks passed. The Brute weight correction is present in three Unity meshes and the Blender exporter. The shield crest appears in the guard capture in Artifacts/v12-clean-20261123-022802/10d_heater_defense.png.
- The combined 1.2 update passed 707 muted Unity checks without logged errors or exceptions. See Artifacts/v12-clean-20261123-022802/report.txt. The Windows executable was rebuilt in Builds/Windows and passed 21 muted checks in Logs/v12-release-player-20261123-023510.log. The build completed with 19 warnings and no errors.
- The first local integration checkpoint was 821247b, based on GitHub main at 2dbe817. Full-run difficulty tuning remains open.
- The sword collection passed 1,005 held poses per design, including shield raises, strikes and combo blends with a 4 mm shield margin. See `Artifacts/SwordCollection/sword-handling.txt`. The guard now opens the sword elbow as the shield comes up. An interrupted cut stays cancelled, solid cover blocks melee, and shield bashes hit each enemy once. The grave double carries its equipment through the reveal and waits for control to return before engaging.
- The sword release passed 716 muted play-mode checks in `Artifacts/swords-release/report.txt` and 30 muted built-player checks in `Logs/swords-windows-player.log`, including all seven thrust, stow and redraw checks and restoration of the original sword. The Windows build completed with zero errors and 19 warnings.
- The customization update passed 716 regression checks, 15 focused menu and in-hand checks, and 42 checks in the final Windows executable. Reports are `Artifacts/wardrobe-release/report.txt`, `Artifacts/wardrobe-menu-final/report.txt`, and `Logs/wardrobe-player-final.log`. The [model comparison](screenshots/sword-collection.jpg) includes actual Unity captures of all seven grips.

For the editor suite, use the existing SmokeTest.RunBatch with the -mwMute switch and an absolute -shotDir. For a siege-only check, temporarily create Artifacts/SiegeReviewOnly.txt and remove it afterward. The Windows build command is BuildTool.BuildWindowsBatch and the standalone check uses -batchmode -mwMute -autotest.
