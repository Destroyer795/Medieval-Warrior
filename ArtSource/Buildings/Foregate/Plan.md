# Defend the Castle — outer gatehouse

## Layout

The south gate sits at world X -14.3, Z -60.6 on the sanctuary entrance axis. Short side walls join the existing southern curtain. Both stairs descend inside it. Defend temporarily clears one curtain bay and two houses from the approach, and restores them on exit. The cobbled lane follows the measured terrain up to the keep plaza.

The upper platform uses individual dressed stones and timber boards with metre-scale texture repeats. The player walks on a separate continuous collision surface across each stair and landing. The central drop opening remains open.

## The playable siege

- The knight begins on the wall with a bow. Either armory refills the quiver on demand. At the centre, the lever tips one stone through the opening. The station carries five stones and automatically loads the next after a short reset. Low crenellations leave clear shooting lanes toward the gate.
- Attackers approach the doors along the road. The first rank strikes the gate while the others crowd behind it, giving the defender a choice between aiming at individuals and timing a stone drop.
- When the outer gate falls, both leaves swing back on their hinges. A one-second cut from inside the courtyard shows the attackers coming through. Control then returns to the knight in place. Both staircases lead to the inner defense.
- Attackers begin in the field and build toward a final push. If the gate falls, reinforcements approach through the open frontage or from the front of the Keep. Survive the inner timer with the sanctuary intact.

## Art and validation

The editable model is in OuterGatehouse.blend. The Blender script in Tools/ArtPipeline/outer_gatehouse.py produces the castle study and the FBX used by Unity. The four numbered renders show the original site, the joined frontage, the defender’s side and the stone cradle. The focused SiegeSmoke scenario captures the actual player view, courtyard, front elevation and breach frame. It checks the two stair flights, protected sides, armories, stone impact, one-second handoff and the attackers’ transition onto the village NavMesh.
