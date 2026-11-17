# Environment masonry, weathering and supply crates — 17 November 2026

Tested in Unity 6000.3.2f1 Built-in Render Pipeline and Blender 4.5.15 LTS.

| Verification | Result |
|---|---|
| Runtime/editor assembly compilation | PASS; no compiler errors |
| Weathered architecture surface shader | PASS; compiled/rendered with Standard PBR lighting |
| Imported architecture AO | PASS; 54 render meshes, 1,746,204 live AO vertex entries |
| Existing building compatibility review | PASS; 8,055 checks, same 120 prefab roots and 330 placed mesh nodes |
| Navigation inspection | PASS; 260 vicinity samples and the original connected spawn route |
| Source topology/UV audit | PASS; all 72 saved architectural render/collision meshes |
| Ground contact bake | 99,271 terrain probes; 31,596 sparse triangles; no colliders or NavMesh input |
| Supply crate | 9,504 render vertices; closed bevelled source, UVs, PBR and baked AO |
| Crate compatibility | Original mesh/prefab GUIDs, bounds, BoxCollider and pickup attachment transforms retained |
| Gameplay smoke test | All 365 checks PASS; zero gameplay errors/exceptions |

The final scene uses 4K CC0 Castle Wall Slates photogrammetry maps and rough Rock
Wall 02 foundation detail. Exact source URLs/checksums are pinned in the art
manifest. Normal maps are imported as normals; height/AO/roughness data is linear.
The material uses 35 mm maximum parallax relief, up to 14 near-field steps and a
fade to normal mapping at 18 m. Sampling retains the original texture gradients
after parallax to keep fine scanned detail sharp.

Reviewed full prefabs at their actual placed scales, plus sunlit in-world masonry
and foundation close-ups and the supply crate. Ground dirt follows the baked
terrain height; crevice moss and vertical stains add environmental variation.
Duplicate AO estimates are combined conservatively instead of multiplied into
black facades. PCF soft shadows remain active, with reduced bias and balanced SSAO.

The NavMesh asset is unchanged. Building collision assets and local geometry stay
intact; new mesh colour alpha contains baked AO. The crate mesh is updated at the
existing asset GUID, with its original 1.02 × 0.65 × 0.73 m collision envelope.
The sparse ground-contact rendering has no collider and does not affect camera,
projectile or character collision.

Evidence:

- [Gameplay report](2026-11-17-environment-gameplay.txt)
- [Environment asset verification](../../ArtSource/Buildings/environment-audit.json)
- [Blender vertex AO measurements](../../ArtSource/Buildings/weathering-audit.json)
- [Saved-master source audit](../../ArtSource/Buildings/source-audit.json)
- [Masonry close-up](../../ArtSource/Buildings/Review/Masonry_Closeup.png)
- [Foundation close-up](../../ArtSource/Buildings/Review/Foundation_Closeup.png)
- [Supply crate](../../ArtSource/Props/Review/SupplyCrate.png)
- [Finishing pipeline, commands and CC0 source credits](../../ArtSource/Buildings/Weathering.md)
