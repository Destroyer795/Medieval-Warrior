# Runner rear ambush behavior

The fast Runner commits toward the rear while varying its angular steps, route
radius, burst speed and timing. It keeps its chosen side through a flank instead
of repeatedly reversing across the warrior's front. Most flank goals stay
outside sword reach. Visible preparation of a nearby sword swing prompts a
route adjustment; it does not grant invulnerability or cancel valid damage.
Near the rear quarter it commits to an intercept with a short, bounded movement
lead rather than waiting in a wide stalking circle.
Navigation candidates must have a complete path that does not shortcut through
the player. Decisions occur at intervals, rather than recalculating routes every
frame.

The attack sector is the rear 120-degree arc, relative to the warrior's facing.
Runners request an attack slot only when close to that sector. Both starting an
attack and dealing its damage require rear access. Turning to face a winding-up
Runner cancels its attack and sends it back to flanking. Other zombie archetypes
retain their existing attack behavior.

Flanking uses 1.45–1.75 times base speed, with stronger acceleration and faster
path updates. The rear intercept uses 1.6 times base speed. Strike windup is
0.22 seconds (previously 0.35); its animation playback follows that timing. The
Runner closes distance during early preparation, stops tracking just before
contact, and breaks away 0.18 seconds after contact rather than standing in
recovery for 0.55 seconds. Rear-only damage and turning counterplay still apply.
Locomotion playback follows the actual burst speed to keep the running motion
matched to its travel.

The automated review checks front/side exclusion, valid rear strikes, windup
cancellation and actual NavMesh route diversity. A repeatable 48-encounter sample
produced 21 left flanks, 27 right flanks and 39 distinct route goals after the
aggression refinement. All 48 goals progressed around a flank. A live NavMesh
approach from four metres in front reached the rear at 1.32 seconds and began
its attack at 1.58 seconds. The final gameplay regression report is saved under
`Artifacts/Runner`.

Validation: 218 complete gameplay checks and 45 animation/defense authoring
checks passed. The final focused movement/animation review also passed, including
the moving windup and immediate post-strike breakaway.
