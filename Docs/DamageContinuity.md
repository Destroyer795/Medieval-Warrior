# Zombie damage continuity

Nonlethal hits reduce health and retain blood, tint, audio and damage indicators,
while preserving the zombie's current navigation, animation, attack ownership and
action timer. This applies to Walkers, Runners, Brutes, Spitters, Bloaters and
Warlords, including attacks, charges and an already running Bloater fuse.

The damage stagger state and its forced knockback have been removed. Sword hits
and shield bashes no longer trigger global hit-stop. The legacy parry hook also
preserves enemy actions. Lethal hits still trigger the existing death behavior or
Bloater explosion sequence.

`SmokeTestDriver.ReviewDamageContinuity` exercises all six archetypes with a
large legacy knockback argument, checking health loss without resetting state,
action clock, navigation, animation speed or attack ownership. It also exercises
the sword hit path and checks that gameplay time is not slowed. Validation
reports are saved in `Artifacts/DamageContinuity`.

Validation passed: 233 gameplay checks, including 16 damage continuity checks,
and 45 combat animation/clearance checks. The full run logged no errors or
exceptions.
