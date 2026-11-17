# Village house pathways — 2026-11-17

The additive `Background` scene contained 37 houses. Thirteen neighbouring pairs
had less than two metres between their rotated render envelopes; the worst pair,
`House (6)` / `House B`, overlapped by 0.44m. The runtime warrior's Rigidbody capsule
is 0.3m in radius and 1.8m high.

`BuildingBuilder.FixHousePaths` opened these routes by translating 28 house roots
in XZ. The minimum resulting separation is 2.03999m, including roof overhangs and
door details. Most adjustments are small; the largest is 6.68m for the house in
the overlapping eastern cluster. The solver also preserves existing clearances
to fixed walls, towers and bridges. Building heights, scale, rotation, mesh bounds,
prefab/scene IDs, and player/spawn transforms remain unchanged.

The pass updates local terrain pads (41,259 height samples), carries foundation
plinths and nearby dressing with their houses, seats props back onto the terrain,
and moves blocking props outside the reserved alleys. Supply-crate pickup children
and settings remain attached to their original instances. Dirt paths replace grass
in the narrow passages; terrain detail grass is cleared from the walking strip.
Ground-height weathering and baked terrain-contact AO are refreshed at the new
positions. The existing NavMeshData asset is updated without changing its GUID.

The original art reference remains unchanged. Intentional house translations are
recorded separately in `Tools/ArtPipeline/Buildings/path-layout.json`; compatibility
review still checks all non-house transforms against the original reference.
The regular environment rebuild reserves the same corridors when scattering props.

## Verification

- Unity scripts compile and the batch utility exits successfully.
- All 37 houses have at least 2m conservative envelope separation.
- 25 close alleys pass 1,568 capsule clearance probes with extra radial margin.
- Each alley passes matching-size CharacterController collision movement in both
  directions, using 10m/s sprint steps at 50Hz and the player's 40-degree slope limit.
  The runtime player remains a Rigidbody/capsule; no controller or movement settings
  are changed by the layout fix.
- Each alley has a direct complete NavMesh route and is reachable from the player.
- All 8,055 building compatibility/collision checks pass, with 120 prefab roots
  and 330 architectural mesh nodes preserved.
- The enemy spawn remains connected to the player; 260 house/wall vicinity
  navigation samples resolve successfully.
- The full gameplay smoke suite passes all 365 checks, with zero gameplay errors
  or exceptions. See [the gameplay report](2026-11-17-house-pathways-gameplay.txt).
- The refreshed ground-contact bake contains 99,884 probes and 32,004 sparse
  render-only triangles; the PBR/AO/material reference audit also passes.

Machine-readable result: [pathways-audit.json](../../ArtSource/Buildings/pathways-audit.json).
Full village views: [before](../../ArtSource/Buildings/Review/Village_Pathways_Before.png)
and [after](../../ArtSource/Buildings/Review/Village_Pathways_After.png).
Ground-level views: [eastern alley](../../ArtSource/Buildings/Review/Village_Alley_East.png)
and [western alley](../../ArtSource/Buildings/Review/Village_Alley_West.png).

## Reproduce

```powershell
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.FixHousePaths -logFile house-paths-fix.log
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.VerifyHousePaths -logFile house-paths-verify.log
Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir Artifacts/Buildings/PathSmoke -logFile house-paths-smoke.log
```

The fix command is repeatable and does not regenerate building meshes or the full
environment. Review screenshots are rendered into `Artifacts/Buildings/Paths`.
The repeat run retained all 28 reviewed house positions. Unity's existing tree
billboarding and shutdown allocator warnings remain outside the gameplay error
report; this change adds no per-frame runtime scripts.
