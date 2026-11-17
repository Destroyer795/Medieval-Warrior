# Custom medieval architecture

`Buildings.blend` is the compressed, editable Blender 4.5 LTS master. Its collections
contain 18 measured modules, each with three render meshes and a separate collision
shell. The kit replaces the rendering and collision references in the twelve existing
Advance Studios prefabs. The vendor FBXs remain as dimensional references; Unity
does not import the `.blend` file at runtime or require Blender to play/build.

The geometry, atlas images and Python construction are authored for this project.
The original pack's prefab GUIDs and associated licence remain in use. The village
layout now includes the reviewed house-spacing revision described below.
The masonry finish now adds scanned CC0 Poly Haven PBR maps, metric parallax relief,
terrain-aware weathering and baked vertex/ground contact. See the
[weathering pipeline and source credits](Weathering.md).

## Grid, axes and compatibility

Do not center origins, apply a guessed import scale, or move prefab children.
The baseline in [`reference.json`](../../Tools/ArtPipeline/Buildings/reference.json)
contains exact mesh-local bounds, pivots, child matrices, prefab/component IDs and
scene mesh transforms. The legacy import uses `globalScale: 100`; its resulting
Unity mesh coordinates, rather than the FBX's nominal units, are authoritative.

The later pathway fix translates 28 of the 37 placed houses horizontally to open
13 originally obstructed or narrow house pairs. House envelopes, including eaves
and doorway details, are separated by at least 2.04 metres. Rotations, scales,
vertical placement, fortification/bridge placement and all asset IDs remain intact.
[`path-layout.json`](../../Tools/ArtPipeline/Buildings/path-layout.json) records
these intentional translations against the unchanged original baseline; review
continues to reject unrecorded transform changes and non-house layout overrides.

Use **Medieval Warrior → Buildings → Fix house pathways and rebake navigation**
or run `BuildingBuilder.FixHousePaths` in Unity batch mode. This seats local terrain
pads, moves foundation plinths and nearby dressing, clears blocking props, paints
walkable dirt lanes, refreshes terrain-contact AO and preserves the NavMesh asset
GUID while rebaking it. Repeating the command retains the reviewed positions.
The regular environment rebuild also reserves these alleys when scattering props.
`VerifyHousePaths` tests 25 close house passages with the actual 0.3m player capsule
radius: 1,568 clearance probes, sprint-sized movement steps in both directions,
direct navigation through each alley, and connectivity from the player start.
See [`pathways-audit.json`](pathways-audit.json) and the
[pathway validation record](../../Docs/validation/2026-11-17-house-pathways.md).

| Module | Local bounding dimensions X × Y × Z | Connection convention |
|---|---|---|
| House A | 1.253385 × 1.621652 × 2.000000 | Gabled ends at Z ±1; preserve separate door/window nodes |
| House B | 1.253386 × 2.021652 × 2.000000 | Taller house on the same footprint |
| Wall | 2.000000 × 2.127554 × 0.765965 | Repeat at 2 local X units; asymmetric rear wall walk |
| Column | 0.349937 × 1.756422 × 0.274969 | Original wall-end connector origin |
| Tower A | 1.064254 × 5.300000 × 1.064254 | Square, narrow bastion; original corner connections |
| Tower B | 2.221189 × 3.330770 × 2.221188 | Round bastion |
| Castle | 5.154220 × 4.300000 × 4.154406 | Four rounded bastions and recessed gate, original top child |
| Bridge bay | 1.200000 × variable × 0.872942 | Repeat at 1.2 local X units; original Start/Middle/End offsets |

These are **mesh-local** dimensions. Existing prefab/scene scales convert them to
world metres. A scene wall scaled by 2 is a 4 m module. Bridges retain their original
slightly different Start/End Y offsets. Floor probes preserve each bay's measured
deck surface and the wall walk at local Y `0.12565732`.

Blender's saved mesh coordinates use Z up, with Unity `(x,y,z)` represented as
Blender `(x,-z,y)`. The export buffers use Unity's original local axes and clockwise
triangle winding. Every render LOD fits the original mesh bounds to sub-millimetre
tolerance. **One deliberate exception:** `CastleTop` was a single zero-thickness
surface. It now has a closed 0.2 mm slab below the same top plane, entirely within
the keep. The aggregate prefab and scene envelopes remain unchanged.

The measured scene is `Assets/Scenes/Background.unity`, loaded additively alongside
`Semi-Final.unity`. The audit records 120 prefab roots and 330 architectural mesh
nodes, including unpacked objects and mesh overrides. It checks both scenes.

## Geometry, budgets and surfaces

Each stone, beam, tile, jamb and arch wedge is a closed shell. Masonry assemblies
use overlapping construction shells rather than boolean unions. The Blender audit
requires zero non-manifold edges; quad ratios and actual triangle/vertex counts
for every LOD are recorded in [`mesh-audit.json`](mesh-audit.json). Bevelled edges
and weighted split normals soften the silhouettes without smoothing across walls.

Representative triangle budgets, per reusable module:

| Module | LOD0 | LOD1 | LOD2 |
|---|---:|---:|---:|
| House A / B | 56–59k | 13–14k | <1k |
| Wall | 26k | 11k | <400 |
| Tower A / B | 85–107k | 22–32k | 1.6–2.4k |
| Keep body | <270k | <80k | <10k |
| Bridge bay | <10k | <2k | <600 |

LOD screen thresholds are 0.38, 0.13 and 0.008. Geometry is shared across instances;
all meshes share the architectural atlas and a Built-in **Standard lighting model**
surface shader. Houses use six instancing-enabled
dry material variants (one per form/LOD) with their own contact AO; other modules use
shared variants per module for metric stone projection. LOD children
have no colliders. Physics uses closed, welded, low-detail static MeshCollider
proxies, retaining deck surfaces, merlons, arches and roof silhouettes. Houses have
closed wall/floor shells around an entrance behind the retained hinged door. The keep's
recessed arched gateway is open in its structural collision shell; the existing
door leaves remain separate meshes on their original hinge transforms.

The shared 4096² atlas has 4 × 4 cells, twelve occupied:
ashlar, rubble, dark oak, lime plaster, red clay, slate, iron, oak planks, smoky
glass, mortar, carved limestone and dark reveals. Atlas images are deterministic
procedural originals with chisel pits, wood grain, mineral variation and weathering.

| Map | Import | Standard shader use |
|---|---|---|
| Albedo.png | sRGB, mipmapped | `_MainTex` |
| Normal.png | Normal Map, linear, mipmapped | `_BumpMap` |
| Masks.png | Linear RGBA, mipmapped | R metallic, G occlusion, B height reserve, A smoothness |
| HouseAO/*.png | Linear, mipmapped | Geometry-derived per-house/LOD `_OcclusionMap` |
| Masonry/* | Albedo sRGB; normals typed Normal; packed maps linear | Scanned stone/rock albedo, normal, AO/height/smoothness |

UV0 has separate, non-overlapping planar face charts within the appropriate atlas
cell. UV1 independently packs orthonormal face charts across the unit square for
lightmapping. Charts are padded and never stretched to fill their rectangles.
Different **assets** share the same atlas cells intentionally. No vendor bitmap or
mesh vertices from the original castle pack are copied into the new assets.

## Dry half-timbered house finish

Plaster, structural timber, oak planks and red clay have **zero smoothness** in the
packed mask. Plaster uses multiscale trowel relief, aggregate, grit and pinholes.
Timber has warped longitudinal fibres, knots, deep irregular cracks, bleached ridges
and water-darkened patches. Clay is unglazed, granular and mottled with mineral
deposits, erosion and environmental staining. The historical internal palette name
`Oiled dark oak` remains a stable source identifier; its finish is now dry aged timber.

All six house-body materials set `_SpecularHighlights` and `_GlossyReflections` to
zero and enable the matching Standard shader OFF keywords. Separate window/door
materials retain the iron/glass response. The editable Blender palette also sets
roughness to 1 and specular IOR level to 0 for the dry surfaces. Packed masks use
`CHANNEL_PACKED` alpha handling so zero smoothness never destroys their RGB data.

`building_surfaces.py` casts 20 cosine-weighted BVH rays from each mesh vertex and
face centre, with a 0.065 local-unit radius. It rasterises contact occlusion into
each LOD's existing UV0 charts, including roof overlaps and timber/plaster joints.
Centre samples keep open faces bright. Maps are 2048² / 1024² / 512² for LOD0/1/2,
with padded charts and micro-occlusion combined into G. Bakes isolate each house
from the other source collections. Geometry, colliders and both UV layouts stay intact.

For a material-only revision:

```powershell
blender --background --python-exit-code 1 --python Tools/ArtPipeline/buildings.py -- --only Textures
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.RefreshSurfacesAndReview -logFile buildings-surfaces.log
```

The Unity menu **Buildings → Refresh matte house surfaces** updates materials on
prefabs and direct scene overrides without rebuilding meshes or navigation.
The batch review also records a view under the existing world lighting. See
[`surface-audit.json`](surface-audit.json) and
[`House_WorldLighting.png`](Review/House_WorldLighting.png).

## Rebuild and integrate

From the repository root, using Blender 4.5 LTS and Unity 6000.3.2f1:

```powershell
blender --background --python Tools/ArtPipeline/buildings.py
blender --background --python Tools/ArtPipeline/download_building_scans.py
blender --background --python Tools/ArtPipeline/environment_finishes.py
blender --background --python Tools/ArtPipeline/audit_buildings.py
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.BuildAndReview -logFile buildings-import.log
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.ApplyEnvironmentFinishAndReview -logFile environment-finish.log
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.RebakeNavigation -logFile buildings-navigation.log
Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir Artifacts/Buildings/Smoke -logFile buildings-smoke.log
```

The Blender command regenerates the compressed master, images, audit and ignored
binary buffers. Unity imports these buffers to stable `.asset` paths under
`Assets/Resources/Buildings`, updates meshes/materials/colliders inside the existing
prefab assets, repairs missing named windows and keeps their GUIDs/component IDs.
For faster art iteration, append `-- --only Houses` (or a measured model/module
name) to the Blender build command. This updates that collection in the existing
master and preserves the other modules and export buffers. A full build remains
the authoritative clean regeneration path. Use `--python-exit-code 1` before
`--python` in CI so Blender returns a failing exit status on Python exceptions.
`-- --only Textures` refreshes atlas images and the packed master without changing
geometry. Wood grain follows the long axis of the packed beam/plank face charts.
The importer also replaces direct source-mesh overrides in the scenes. Reimporting
updates existing Mesh native streams, retaining their generated asset GUIDs.

The same operations are available under **Medieval Warrior → Buildings**.
`Export reference dimensions` is only for capturing an **unmodified original pack**;
it refuses to overwrite the baseline after integration. Do not run it as a normal
rebuild step. Commit `reference.json`, `manifest.json`, source, textures, generated
Unity assets, changed prefabs/scenes and reports. Ignore binary buffers/backups.

`Review` checks original bounds, component IDs, reviewed world matrices, mesh references,
LOD budgets, normals, UV channels and collision contacts. It exercises raycasts,
camera sphere casts and actual `CharacterController.Move` on every bridge bay and
the wall walk. `RebakeNavigation` reuses the existing NavMeshData GUID and verifies
player/spawn routes; it does not regenerate terrain pads or move buildings/spawns.
The broad environment rebuild is deliberately unnecessary for this replacement.

The verified run passed 8,055 Unity checks and 260 navigation vicinity samples;
the original fallback spawn can still reach the player. The gameplay smoke suite
passed all 365 checks with zero gameplay exceptions. All 72 saved source meshes
have zero non-manifold edges and degenerate faces; both UV channels have zero
overlapping face charts. The UV audit also checks projected edge-scale distortion,
including the small curved bevel faces.

Verification results: [`unity-audit.json`](unity-audit.json), and the independent
saved-master topology/UV inspection in [`source-audit.json`](source-audit.json). Finished prefab views
are in [`Review`](Review/). The gameplay regression is saved with the project docs.
