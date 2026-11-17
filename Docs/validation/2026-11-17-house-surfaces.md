# Matte half-timbered houses — 17 November 2026

The plaster, timber and clay atlas cells now contain zero smoothness. All six
house-body materials (House A/B × three LODs) disable Standard shader specular
highlights and glossy environment reflections. Window/door detail renderers keep
their separate iron/glass response.

The new surfaces include coarse lime stucco, trowel variation, pinholes and grit;
aged timber with longitudinal grain, cracks, knots and bleached/weathered patches;
and unglazed clay with granular relief, mineral variation and environmental staining.

Geometry-derived contact AO is baked independently for each house/LOD using 20
cosine-weighted BVH rays per vertex and face centre. The 2048/1024/512 maps follow
the existing UV0 charts. Face-centre samples keep open panels bright while roof
overlaps, beam joints and plaster edges darken. The maps combine contact and micro-AO.

Verification:

- Unity runtime/editor assemblies compiled without errors.
- Actual PNG channels checked: zero smoothness throughout plaster, timber, clay and oak cells.
- All six house material flags and shader OFF keywords checked after import.
- Existing building review passed 8,055 checks; all 120 prefab roots and 330 mesh nodes retain their measured placements and references.
- Navigation inspection passed 260 vicinity samples; the existing spawn route remains connected.
- Independent saved-master audit passed for all 72 meshes and both UV channels.
- Mesh assets, collision assets, scene files and NavMesh data have no changes in this material pass.
- Reviewed both house forms under studio lighting and in the existing world lighting.

No runtime gameplay scripts changed. The material-only refresh does not regenerate
meshes, UVs, terrain or navigation.

Evidence: [surface/AO measurements](../../ArtSource/Buildings/surface-audit.json),
[source audit](../../ArtSource/Buildings/source-audit.json),
[Unity audit](../../ArtSource/Buildings/unity-audit.json),
[House A](../../ArtSource/Buildings/Review/House%20A.png),
[House B](../../ArtSource/Buildings/Review/House%20B.png),
[world lighting](../../ArtSource/Buildings/Review/House_WorldLighting.png).
