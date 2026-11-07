# Gameplay frame-time and camera response

The character's per-frame collision work was the principal measured CPU cost.
Armor and linen updates averaged 21.54 ms together before this revision.

The armor solver now caches bind transforms, frame scale and joint reference
rotations. Invalid trial poses stop at the first rejected contact. Constraint
trials use temporary matrices and only accepted poses are written into Unity's
bone hierarchy. Four conservative bisection steps retain a valid contact pose
without the unnecessary extra precision of seven iterations.

The linen updater caches its pelvis bind matrix and each capsule's segment
data. Triangle contact centroids update only after a correction rather than
being recomputed for every capsule. Cloth resolution, the native solver rate,
mesh detail and graphics settings are unchanged.

Mouse look consumes raw mouse displacement in Update, before movement consumes
the view heading. Mouse orbit has no additional angular spring. Analog stick
look retains smoothing. Camera target follow uses a shorter response time and
obstruction queries reuse a growable hit buffer, retaining all hits if crowded.

## Measured comparison

240-frame Unity Editor batch runs on this machine, with sprinting, turns and
sword poses. Values are milliseconds; these are benchmark results rather than
a promised frame rate on other hardware or an exported build.

| Measurement | Before mean / p95 | After mean / p95 |
|---|---:|---:|
| Armor update | 13.057 / 17.438 | 6.450 / 7.934 |
| Cloth update | 8.483 / 8.613 | 5.760 / 5.840 |
| Quiver update | 0.097 / 0.487 | 0.080 / 0.099 |
| Whole frame | 24.821 / 30.517 | 14.649 / 16.504 |

Average frame time fell about 41%. Raw reports are in
`Artifacts/Marchwarden/Performance/before.txt` and `after.txt`.

The complete regression passed 202 gameplay checks and 45 animation/defense
authoring checks after the optimization. Camera wall clearance, movement
directions, bow tracking, combos, and cloth/armor/quiver clearance all passed.

To repeat the benchmark, create `Artifacts/PerformanceReviewOnly.txt` inside the
validation project and run `MarchwardenBuilder.BuildAndSmoke`. It uses the
existing imports, warms up the scene and records the movement sample without
the smoke test's screenshot overhead. Remove the flag for the full regression.
