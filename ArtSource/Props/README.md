# Arrow supply crate

`SupplyCrate.blend` is the compressed editable Blender source for the arrow supply
boxes. It contains separate bevelled wood planks, visible seams, iron corner
straps, lid bands and studs. UV0 uses the shared architectural atlas and UV1 is
independently packed. Closed source geometry is checked before export; baked
ambient occlusion is stored in mesh colour alpha.

The Unity importer updates the original
`Assets/Environment/Generated/Props/SupplyCrate_Mesh.asset` in place, preserving its
GUID and the original prefab/BoxCollider. The envelope is 1.02 × 0.65 × 0.73 m,
centred at `(0, 0.315, 0)`; arrow bundle/pickup child attachments retain their
existing transforms. `EnvironmentArtAsset` supplies the authored mesh/material
through `ProceduralModels.CrateMesh/CrateMaterials`, including future prop rebuilds.

Textures are linked relatively to the committed architectural atlas. The crate
uses the same dry aged timber/iron PBR response, terrain-aware dirt, crevice
weathering and baked ground-contact finish as the environment.

Rebuild from the repository root:

```powershell
blender --background --python-exit-code 1 --python Tools/ArtPipeline/environment_finishes.py
Unity.exe -batchmode -quit -projectPath . -executeMethod BuildingBuilder.ApplyEnvironmentFinishAndReview
```

See [the full finishing guide](../Buildings/Weathering.md) for source textures,
download/rebuild steps, shader details and validation. [Crate preview](Review/SupplyCrate.png).
