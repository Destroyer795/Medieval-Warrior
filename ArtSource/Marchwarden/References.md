# Warrior and motion references

The earlier frontier silhouette is based on the user's [s.jpg](../../s.jpg): a conical
nasal helmet, visible face, mail hood/sleeves, layered torso protection, long split
tunic, high boots, straight sword and round shield. The material palette and outfit
details are authored for this game. The current cinematic outfit follows
[the newer warrior render](References/Cinematic-warrior.png), with the crested
gladiator helmet and arched heater documented in the revision notes. The
customized anatomical head and hands and their CC0
source/license are documented in [Anatomy/README.md](Anatomy/README.md).

The museum studies below informed the earlier plate-harness version. They are
retained with the source photographs for provenance; the current outfit is not
presented as a reconstruction of those suits.

These references guide the construction of original game geometry and animation.
The character is a game interpretation, not an archaeological reconstruction.

- [The Met, Composed Armor, 50.160a-x](https://www.metmuseum.org/art/collection/search/24814):
  fitted breastplate and plackart, overlapping spaulders, tassets, couters and
  poleyns, narrow vambraces and greaves, mail exposed at the articulation gaps.
  The museum identifies this as a composite of historic elements and restorations;
  it is used for construction study rather than as one complete period suit.
- [The Met, Sallet, 29.150.8a](https://www.metmuseum.org/art/collection/search/23091):
  the forged medial keel, narrow eye opening, rolled visor edge and swept nape.
  The museum notes this particular heavy example may have been for tournaments.
- [Cleveland Museum of Art, Visored Sallet, 2014.393](https://www.clevelandart.org/art/2014.393):
  streamlined skull, visor pivots and teardrop profile.
- [The Met, Visored Bascinet, ca. 1400](https://www.metmuseum.org/art/collection/search/34272):
  the current tapered skull and visor construction, with mail protecting the neck.
- [The Met, Bascinet, ca. 1350–1400](https://www.metmuseum.org/art/collection/search/23238):
  attachment of an aventail around the cheeks and lower rim. The current shorter
  neck and draped mail replace the previous tall sallet/standard combination.
- [World Archery, Intermediate Coaching Manual](https://documents.worldarchery.org/Coaches/Accreditation/Coaching_Levels/Coaching_Manual_Level2.pdf):
  consistent facial anchor, shoulder/draw alignment and follow-through. Applied
  to a game bow pose; the knight keeps armor and the existing movement controls.

The two photographs in `References/` are The Met's public-domain images from the
first two object pages. The earlier surface used a high-pass sallet detail crop;
the current worn-steel source and prompt are documented in [Materials.md](Materials.md).
The geometry, UVs and other surfaces are made for the game.
The Met makes these images available through its
[Open Access program](https://www.metmuseum.org/about-the-met/policies-and-documents/open-access).

Sword clips are newly baked multi-joint motions on the existing animation skeleton:
four three-hit phrases, with separate wind-ups, cutting/thrust phases, blade
orientations, shield counter-movement and recovery. They are game animations,
not a claim of historical fencing instruction or newly captured motion footage.
The current clips also use stable bend-plane IK, continuous quaternion curves and
a retained combo-layer weight to avoid the earlier forearm flips and idle reset.
