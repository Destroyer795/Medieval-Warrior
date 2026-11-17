# Masonry relief, weathering and environmental contact

The architecture uses a Built-in Render Pipeline surface shader with Unity's
Standard PBR lighting model: `Medieval Warrior/Weathered Architecture`.
The existing atlas still supplies timber, plaster, roof tiles, iron and glass.
Stone faces receive additional scanned albedo, OpenGL normal, AO, displacement
and roughness data. House-body materials retain their disabled specular highlights
and glossy reflections.

## Scanned sources and licence

Both PBR sources are **CC0**, by Rob Tuytel, from Poly Haven:

- [Castle Wall Slates](https://polyhaven.com/a/castle_wall_slates): 4096² masonry maps, 2.5 m repeat.
- [Rock Wall 02](https://polyhaven.com/a/rock_wall_02): rough foundation rock, imported from 4K sources and filtered to 2048², 2 m repeat.
- [Poly Haven asset licence](https://polyhaven.com/license).

Exact source URLs, formats, byte sizes and MD5 checksums are pinned in
[`scanned-materials.json`](../../Tools/ArtPipeline/Buildings/scanned-materials.json).
The downloader verifies every file and caches raw sources under the ignored
`Artifacts/Buildings/Scans` directory. Derived Unity PNGs are committed. The new
masonry images remain linked relatively in the compressed Blender master; the
original architectural atlas remains packed. `SupplyCrate.blend` links its atlas
relatively, keeping the standalone prop source small.

## Relief and weathering

Metric projection follows round towers and the keep's four bastions; integral
circumference repeats close the tower seam. Planar wall/floor projections retain
real-world texel scale at different instance scales. Foundations use rough rock
detail over the existing geometric stones, avoiding a second grid of fake joints.

The shader performs up to **14 parallax-occlusion steps** with a **35 mm maximum
relief**. It fades the march between 11.7 and 18 m; normal detail remains at distance.
Original projection gradients drive texture sampling after the ray march so fine
scanned detail is not blurred by parallax discontinuities. Tangent frames are
derived from the projected UVs and world position, then converted to the existing
mesh tangent space. The scan normals are renormalised after decoding/filtering.

Terrain height is encoded into a linear RG map. A single scene binding supplies
that map and terrain dimensions, with no per-frame work. This anchors soil gradients
to the actual ground. Crevice/AO masks collect patchy moss close to the ground;
elongated vertical noise adds water staining to masonry. These modifiers affect
the placed structures and supply crates while retaining their material identities.

POM alters surface shading only. Existing building vertices, pivots, transforms,
collision hulls and UV0/UV1 layouts retain their original geometry. Mesh colour
alpha stores additional baked ambient occlusion.

## Baked occlusion and shadows

`environment_finishes.py` casts 12/8/6 cosine-weighted rays per source vertex for
LOD0/1/2 and exports byte AO streams. The native Mesh imports retain their GUIDs.
Houses continue to use the detailed per-LOD contact textures from the matte pass,
including face-centre samples. The shader takes the strongest contact estimate
instead of multiplying duplicate estimates into excessively dark facades.

The scene bake casts eight upward hemisphere rays near the actual architecture
and crates. It samples terrain height and collider geometry, omitting raised
objects from ground-contact placement. After the village slope/prop fixes, the bake uses **99,898 terrain
probes**. A filtered 2048² contact map is drawn by a sparse terrain-conforming mesh
of **31,914 triangles**. This mesh has no collider, casts no shadows, uses the
Ignore Raycast layer, and does not enter physics-based NavMesh collection.

Soft directional shadow filtering stays enabled. Shadow bias is reduced to 0.015,
normal bias to 0.06, closing the floating gap at foundations. Screen-space AO is
balanced to intensity 0.55 and thickness 1.0 alongside the baked contact. This is
baked mesh/ground AO with realtime lighting, rather than a full GI lightmap bake.

## Supply crate

[`ArtSource/Props/SupplyCrate.blend`](../Props/SupplyCrate.blend) contains closed,
bevelled planks, gaps, iron corner straps, lid bands and studs. It uses the aged
timber/iron atlas, baked AO and terrain-aware weathering. The mesh is updated in
place at `Assets/Environment/Generated/Props/SupplyCrate_Mesh.asset`; its GUID,
1.02 × 0.65 × 0.73 m envelope, prefab identity, collision size and arrow pickup
attachments remain intact. `EnvironmentArtAsset` supplies the authored mesh and
material to the existing runtime/editor prop accessor.

## Rebuild

Run from the repository root with Blender 4.5 LTS and Unity 6000.3.2f1:

```powershell
blender --background --python-exit-code 1 --python Tools/ArtPipeline/download_building_scans.py
# If changing building geometry, regenerate it before the finishing stage:
blender --background --python-exit-code 1 --python Tools/ArtPipeline/buildings.py
blender --background --python-exit-code 1 --python Tools/ArtPipeline/environment_finishes.py
blender --background --python-exit-code 1 --python Tools/ArtPipeline/audit_buildings.py
# Import building buffers only when geometry was regenerated:
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.Build -logFile buildings-import.log
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.ApplyEnvironmentFinishAndReview -logFile environment-finish.log
Unity.exe -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir Artifacts/Buildings/WeatheringSmoke -logFile environment-smoke.log
```

For scan repacking only, append `-- --textures-only` to `environment_finishes.py`.
For vertex AO or crate edits, run its full command. Reapply the finishing utility
after changing terrain/layout or running the broad environment rebuild, so ground
height and contact shading stay aligned. The utility is also under
**Medieval Warrior → Buildings → Apply environment PBR, weathering and baked contact**.

Evidence: [baked AO measurements](weathering-audit.json),
[masonry close-up](Review/Masonry_Closeup.png),
[foundation close-up](Review/Foundation_Closeup.png),
[supply crate](../Props/Review/SupplyCrate.png).
