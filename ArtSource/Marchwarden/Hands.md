# Hands and palm attachments

The editable hand meshes are in `Marchwarden.blend`, under the Body collection.
`Tools/ArtPipeline/marchwarden_hands.py` is the reproducible Blender construction.
It runs before the finish bake, so the hands retain the same skin materials,
detail surface and atlas pipeline as the face.

The hands now use the CC0 anatomical mesh in `Anatomy/MakeHuman-base.obj`.
Native palm, nail-bed, knuckle and finger-web topology is retained. The source's
MCP/PIP/DIP joint markers drive the power grip, three-finger string hook and
release, with blended deformation at the joints. They replace the swept-finger
study. `--hand-study` renders the grips before the expensive atlas bake.

The anatomical fitting includes the handed coordinate conversion into Unity.
Skin continues ten centimetres into the forearm beneath a tapered cuff. Wrist
weights blend over the carpal region, while forearm pronation and a soft wrist
bend limit keep the hand aligned during the sword and bow poses.

Hand grip frames are exported in `meshes.json` as `handGrips`. Unity converts
those frames into hand-bone coordinates; the hilt, shield rear handle and bow
use these positions rather than the wrist pivot. The separate ShieldHandle
collection is fitted to the left palm during Unity import and merged into the
shield mesh. The shield plate and forearm enarmes retain their fitted mount.

The existing skeleton has hand joints but no finger joints. Closed gripping
fingers follow the hand, the wrist blends to the forearm, and two exported
morphs retain the joint-authored right-hand string hook and release. These poses
are editable as shape keys in Blender. Locomotion, upper-body
strikes, shield defense and bow IK retain the existing shared skeleton.
