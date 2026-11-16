# Custom building kit verification — 16 November 2026

Tested with Unity 6000.3.2f1, Built-in Render Pipeline, and Blender 4.5.15 LTS.
Unity ran in the existing isolated validation project, sharing the repository's
Assets, Tools and ArtSource directories. No environment regeneration or building
re-seating was performed.

| Check | Result |
|---|---|
| C# runtime/editor assembly compilation | PASS; no compiler errors |
| Blender saved-master inspection | PASS; 72 closed meshes, no non-manifold edges or degenerate faces |
| UV0 / UV1 chart overlap | PASS; zero overlapping charts in either channel |
| UV projected edge-scale distortion | PASS; 99th percentile below 3%, including tiny curved bevel faces |
| Unity architecture audit | PASS; 8,055 checks |
| Preserved placed prefab roots | 120, same persistent IDs, world matrices and aggregate bounds |
| Preserved architectural MeshFilter nodes | 330, including unpacked objects and scene mesh overrides |
| Existing prefab identities | All twelve GUIDs and original GameObject/component file IDs retained |
| Mesh envelopes | All LODs within 0.5 mm; CastleTop is a closed 0.2 mm slab below its unchanged surface |
| Bridge/wall physical contacts | Deck raycasts, camera spheres and CharacterController movement pass |
| House physical contacts | Structural door entry, interior floors, side-wall arrows and camera spheres pass |
| Navigation | Rebaked from custom collision meshes; original NavMeshData GUID preserved |
| Navigation vicinity inspection | 260 house/wall samples; original fallback spawn reaches the player |
| Gameplay regression | All 365 checks pass; zero gameplay errors/exceptions |

The gameplay suite includes all three modes, victory/defeat, enemy locomotion and
attacks, warrior combat and equipment, camera obstruction, projectiles, acid,
customisation and menus. Existing editor shutdown allocation warnings are not
gameplay exceptions. No runtime gameplay code was changed by this architecture pass.

The original Castle prefab contained missing window subasset references. These
were replaced with the custom named window modules while preserving their child
transforms and component IDs. The houses and keep now have structural entrance
openings, with the existing hinged door leaf transforms retained.

Evidence:

- [Gameplay report](2026-11-16-buildings-gameplay.txt)
- [Unity architecture/navigation report](../../ArtSource/Buildings/unity-audit.json)
- [Independent saved-source topology/UV audit and SHA-256](../../ArtSource/Buildings/source-audit.json)
- [Per-module triangle/vertex/quad counts](../../ArtSource/Buildings/mesh-audit.json)
- [Finished prefab views](../../ArtSource/Buildings/Review/)
- [Rebuild commands, grid dimensions and compatibility conventions](../../ArtSource/Buildings/README.md)

The main checks were run using:

```text
blender --background --python-exit-code 1 --python Tools/ArtPipeline/buildings.py
blender --background --python-exit-code 1 --python Tools/ArtPipeline/audit_buildings.py
Unity.exe -batchmode -quit -projectPath <validation> -executeMethod BuildingBuilder.BuildAndReview
Unity.exe -batchmode -quit -projectPath <validation> -executeMethod BuildingBuilder.ReviewAndRebake
Unity.exe -batchmode -projectPath <validation> -executeMethod SmokeTest.RunBatch -shotDir <artifacts>
Unity.exe -batchmode -quit -projectPath <validation> -executeMethod BuildingBuilder.InspectNavigation
```
