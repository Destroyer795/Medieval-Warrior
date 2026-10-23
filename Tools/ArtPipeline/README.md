# Art pipeline

Scripts that produced the game's textures, UI art, recolour masks and audio. **You don't need them
to play or work on the game** – their output is committed. Use them to change a look or a sound and
regenerate.

Requirements: Python 3.10+, `pip install numpy scipy pillow`. Run from the repository root.

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
