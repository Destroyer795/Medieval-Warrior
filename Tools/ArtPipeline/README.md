# Art pipeline

Scripts that produced the game's textures, UI art, recolour masks and audio. **You don't need them
to play or work on the game** – their output is committed. Use them to change a look or a sound and
regenerate.

Requirements: Python 3.10+, `pip install numpy scipy pillow`. Run from the repository root.

The Marchwarden warrior and equipment use Blender 4.5 LTS instead of
these Python dependencies. See [the editable art source](../../ArtSource/Marchwarden/README.md)
and `marchwarden.py`. Unity's **Marchwarden > Import Blender exports** menu imports
its meshes and PBR surfaces. The outfit evolves from the user's s.jpg study to
the cinematic warrior reference; the head and hands use adapted MakeHuman CC0
anatomy. The demo skeleton and locomotion
clips remain in use, with new upper-body sword clips and a procedural bow rig.

The current [cinematic revision](../../ArtSource/Marchwarden/Cinematic.md) follows
the newer generated warrior reference. `marchwarden_cinematic.py` adds continuous
anatomical garments, overlapping blackened armor, linked mail and the refined
heater. The export retains four skin influences and sparse garment motion shapes;
Unity imports those into its existing character skeleton.

`marchwarden_gladiator.py` replaces the conical helmet with a forged gladiator
helmet and exports the coordinated 1.10 stature multiplier. See the
[helmet and stature notes](../../ArtSource/Marchwarden/Gladiator.md).

`marchwarden_cloth.py` now exports separate linen panels for native runtime cloth.
The [combat and cloth notes](../../ArtSource/Marchwarden/CombatCloth.md) cover the
rapid sword timings, collision fitting, rendering and stress review. The tunic
uses live physics instead of the earlier garment motion shapes.

`marchwarden_structure.py` refines the upper body, creates the 60-joint rigid
hip armor assembly and reduces the quiver. See the [structure and equipment
notes](../../ArtSource/Marchwarden/Structure.md) for runtime contact and suspension.

`marchwarden_hands.py` builds continuous anatomical hands and exports explicit
palm frames for the sword, heater handle and bow. See the [hand and grip notes](../../ArtSource/Marchwarden/Hands.md).
The pipeline saves compressed `.blend` files; intermediate mesh JSON and Blender
backups remain local and are ignored by Git.

| Script | Writes | What it does |
|--------|--------|--------------|
| `download_sources.py <dir>` | `<dir>/*_diff.jpg`, `*_nor.jpg`, `sky.hdr` | Downloads the CC0 source textures and sky HDRI from Poly Haven. |
| `terrain_textures.py <sources> Assets/Environment/Textures` | `Grass_Lush_D/N`, `Grass_Meadow_D/N`, `Dirt_Path_D/N`, `Cobble_D/N`, `Rock_Mossy_D/N` (2048²), `GrassDense/Wild/Short.png` | Colour-grades the scans (grass is gradient-mapped to a vibrant stylised green) and paints the grass detail cards. |
| `castle_and_knight.py .` | `Assets/Environment/Textures/Castle/*`, `Assets/Resources/Customization/*` | Brightens/warms the castle pack textures and derives normal maps; writes the knight atlas copy and the tabard/metal/leather mask. |
| `ui_art.py Assets/Resources/UI` | all UI sprites and icons | Gold-and-iron panels, buttons, bars, banner, slots, crosshair, icons. Flower cards too. |
| `sfx.py Assets/Resources/Audio` | combat, zombie, UI sounds, ambience, menu music | Fully synthesised (Karplus-Strong plucks, filtered noise, formant growls). |
| `extra_audio.py Assets/Resources/Audio` | `music_battle.wav`, `step0-3.wav` | Battle drums layer and footsteps. |

Notes
- `ui_art.py` writes the flower cards (`Flowers*.png`) into the UI folder; copy them to
  `Assets/Environment/Textures/` if you regenerate them.
- After regenerating, open Unity (it reimports), then run **Medieval Warrior → Rebuild Environment**
  if terrain or castle textures changed. Import settings (sprite borders, normal maps, read/write
  for the masks) are applied by the builder.
- Keep file names – the game loads UI sprites and sounds by name.
