# Warrior surface sources

`WornSteel-source.png` was generated with the built-in imagegen tool on 2026-11-02
as a material source. It is not a photograph or a scan. Blender maps its scratches
and mottling onto the authored armor, derives restrained micro-normal variation,
then bakes it into the 4096px `Finish.png` / `Finish_Normal.png` atlas with geometry
occlusion and edge wear. `Finish_Surface.png` carries metalness and smoothness.
The eight separate material regions preserve customization colors. The original
Toon RTS texture atlas is used only by the comparison reference, not the new mesh.

Exact generation prompt:

> Use case: photorealistic-natural. Asset type: seamless tileable PBR game material albedo source, square 2048 px. A flat orthographic evenly lit macro photograph of real worked, worn medium-gray tempered armor steel, filling the entire image edge to edge. Fine directional polishing scratches, scattered slightly deeper small scuffs, subtle hammered waviness and mottled charcoal gray oxidation, a few brownish patina traces in shallow pits. Strong readable natural surface variation at medium scale, not just fine grain. Neutral diffuse illumination only; no gradients, no highlights or reflections, no cast shadows, no objects or seams, no frame, no text, no symbols, no illustration. Restrained realistic surface of maintained medieval plate armor, mostly cool mid-gray with irregular darker rubbed and weathered patches, not rusted scrap, not chrome. This must work as a repeating flat texture wrapped onto a 3D knight.

`WovenLinen-source.png` was generated with the built-in imagegen tool on 2026-11-03
and is used for the cloth regions. It is also a generated material, not a scan.
Exact generation prompt:

> Use case: photorealistic-natural. Asset type: neutral grayscale tileable game material texture for a 3D knight's cloth surcoat, square image. Flat orthographic macro of real sturdy worn linen canvas, filling the image edge to edge. Medium-gray undyed fabric in grayscale, visible irregular woven warp and weft threads, occasional tiny slubs, subtle uneven dye and slightly abraded fibers. A broad shallow mottled gray variation over the sheet so it reads as real fabric from medium distance. Uniform diffuse flat illumination, no folds or drapery, no shadows or gradients, no objects, no labels, no borders, no symbols. Small weave repeats throughout the image, about 90 visible threads across the full width, not a few huge rope strands. Avoid noise speckling, synthetic perfect grids, shiny plastic, plaid, patterns or stains that form recognizable shapes. Neutral source intended to be dyed blue or ivory by a game material.

Other surfaces are authored in Blender: mail rings, hair strands,
wood fibers, plain stitched ivory cloth, brass and recessed dark material. The
museum photographs under `References/` remain construction references; provenance
and historical qualifications are recorded in [References.md](References.md).

`WornLeather-source.png` was generated with the built-in imagegen tool on 2026-11-03
and supplies the leather grain for boots, belts, bracers, straps and quiver. Its
normal detail is derived and baked into the same mesh-bound 4K atlas. Exact prompt:

> Use case: photorealistic-natural. Asset type: seamless tileable grayscale PBR albedo material source for a 3D warrior's boots, belt and leather bracers. Flat orthographic macro photograph of maintained but well-worn full-grain saddle leather filling the entire square image edge to edge. Neutral medium gray grayscale only so the engine can tint it brown. Natural irregular pores, shallow branching creases, softly rubbed lighter grain ridges, a few fine scuffs, subtle broad uneven tone. Grain is leather, not rock or fabric. About 20 small crease cells across the image, clear readable detail at medium distance. Flat diffuse illumination only, no shadows, no reflection highlights, no gradients, no text, no stitching, no borders, no objects, no artificial repeating geometric pattern, no large tears. Not cracked or damaged to the point of disrepair.

The current model follows the user's `s.jpg` rather than the earlier plate harness.
Skin has mesh-bound cheek, lip and orbital color variation. The anatomical source
and CC0 terms are recorded in [Anatomy/README.md](Anatomy/README.md).

The athletic revision retains these sources and adds a separate detail-albedo
tile for every material. The Blender exporter preserves `Surface UV` in Unity's
second UV channel while `Finish UV` continues to address the unique 4K atlas.
Detail tiles are centered on encoded sRGB 0.5 for Standard's multiplicative
detail slot; centering them on linear 0.214 incorrectly darkens the result.
See Unity's [secondary-map documentation](https://docs.unity3d.com/Manual/StandardShaderMaterialParameterDetail.html).

Standalone imports explicitly select BC7 for color/packed surfaces and BC5 for
normals. Fabric and skin no longer receive vertex-curvature color shading, which
was making smooth surfaces look faceted. No additional generated image sources
were introduced for this revision.

The heater revision adds `ForgedSteel` and `Iron` regions. Authored scratch fields
provide vertical/diagonal cuts, scuffed margins and variable smoothness. The five
largest gouges also displace the shield surface; its reinforced rim has a rubbed
steel crown. The metal body, border, rivets, enarmes and grip contain no wooden
core or painted cloth face.

The surface bake now preserves the varying source smoothness in its alpha
channel instead of flattening every region to one roughness value. Steel uses
high metallic values; linen remains a rough dielectric. Mesh-bound dirt darkens
the tunic hem and low boot surfaces, while baked occlusion follows cloth folds.
