# Anatomical head and hand source

The customized head and hands are derived from MakeHuman's hm08 base mesh.
Body-group faces above the neck and the right hand/wrist are extracted; the hand
is mirrored for the opposite side. Helper geometry supplies anatomical joint
centres and is not rendered. The head is fitted to this character, its jaw and brow are
adjusted, its mouth is relaxed, and a small brow mark and complexion variation are
added. Eyes, beard, hairline, helmet and clothing are authored separately.

- [Official source OBJ](https://github.com/makehumancommunity/makehuman/blob/master/makehuman/data/3dobjs/base.obj), downloaded 2026-11-03 as `MakeHuman-base.obj`.
- [Official asset license](https://github.com/makehumancommunity/makehuman/blob/master/LICENSE.ASSETS.md), retained as `CC0.txt`.
- [MakeHuman's license explanation](https://www.makehumancommunity.org/about/license.html).

The OBJ header explicitly releases this asset under CC0 (September 2020), naming
Data Collection AB, Joel Palmius and Jonas Hauquier. The MakeHuman program code has
a separate license; no program code from MakeHuman is included or required here.

The source's coordinate system is converted and fitted in
`Tools/ArtPipeline/marchwarden_anatomy.py` and `marchwarden_hands.py`. The hand
retains native palm, nail-bed, knuckle and finger-web topology. MCP, PIP and DIP
joint transformations produce the power grip, string hook and release poses.
Regeneration requires this source file.
