# Acid puddles and weapon grips

Spitter impacts create independent lobed puddle meshes with varying proportions,
shoreline detail and small satellite splashes. The surface conforms to the ground
and uses wet highlights, mottled colour and animated bubbles. Main-puddle damage
follows the irregular shoreline and initial spread; satellite drops are cosmetic.
The puddles share one material and release their unique meshes on destruction.

`marchwarden_hands.py` fits CC0 anatomical hands in Blender, retaining the native
palm, knuckles, nail beds and finger webs. Actual MCP/PIP/DIP joint transformations
produce the grip, string-hook and release poses. Subdivision smooths the native
topology; fingers are not swept primitives. Wrist weights blend between forearm
and hand, and the established animation skeleton is retained.

Explicit palm frames travel with the export. The sword hilt is fitted to the
right palm and the heater's rear handle to the left palm in the authored guard.
The left wrist is oriented to keep the fingers behind the shield shell. Bow IK
solves for the palm positions, with wrist offsets applied before arm solving.
The draw hand changes from its weapon grip to a string hook and release pose.
The arrow rests above the supporting thumb, and the stowed bow mount fits the
revised grip width. Heater handle anchors terminate behind the plate.

Authoring review samples weapon alignment and shield clearance through all twelve
strikes and defense. The gameplay review captures close-ups of each grip, checks
bow draw, and compares puddle geometry with its damage footprint and the ground.
The saved Blender source also passes an independent topology audit: one closed
skin component per hand, consistent face orientation, and no nonmanifold edges.

The wrist correction reflects the source anatomy into Unity's handed coordinate
frame. Forearm skin extends beneath tapered cuffs, with a longer wrist weight
transition. Pronation is assigned to the forearm; a soft wrist limit prevents
the blade target from folding the palm backwards. The ready sword is upright,
and the bow support hand follows the forearm's direction.

Final wrist revision: 240 gameplay checks and 48 combat/defense authoring checks
passed, with no gameplay errors or exceptions. Each hand/forearm surface has
6,894 vertices and passes the independent closed-topology audit. The sampled
wrist bend reaches at most 54.9 degrees, and the heater shell clears the fingers
by at least 27.2 mm. Reports and actual in-game close-ups are saved in
`Artifacts/AcidAndGrips`.
