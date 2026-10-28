# Gameplay polish and 3D environment verification

Validated with Unity 6000.3.2f1, Built-in Render Pipeline and PostProcessing v2.

## Changes

- Physics-driven turning pivots before translating, and explicitly disables animation root motion. This is the movement implementation approved during playtesting; subsequent foliage and camera work did not change it.
- Camera orbit smooths yaw/pitch and the follow pivot separately from collision distance. Both the shoulder offset and camera boom check obstructions. Retraction is immediate; recovery eases outward. Aim sensitivity transitions continuously.
- Bow pose clamps pitch to +/-45 degrees, smooths hand targets in player space and limits additive torso twist.
- Sword hits use 0.04-second light hitstop and 0.08-second finisher hitstop, directional collider-contact sparks and existing camera shake. Sword trails have a tighter mesh and fade.
- Footsteps follow horizontal speed with alternating pitch variation. Positional voices use logarithmic falloff between 3 and 45 metres. Menu music is unchanged.
- EnvironmentBuilder generates instanced grass meshes with tapered, solid blades, and stem/leaf shrub meshes. Ground cover thins toward paths without hard noise thresholds. Grass shadow casting is disabled to limit rendering cost.
- Diffuse terrain shading removes specular sun glare. Foundations and rocks disable specular highlights and glossy reflections. Building plinths sit lower, and narrow aprons feather into soil. Post-processing uses restrained bloom and saturation.

## Results

The complete smoke suite passed **79 checks** with zero runtime errors or exceptions. It covers A/D/S facing and displacement, camera obstruction in free look and bow aim, bow yaw/pitch and arrow hits, enemies, sword combat, pickups, all 37 house-grounding checks, pause/resume, victory, endless waves, boons and defeat.

The subsequent material-only environment rebuild also exited with code 0. No C# compilation errors, shader errors or unhandled exceptions were found. Final overview, shrub and grass captures were visually inspected. Existing Unity obsolete-API/source-generator warnings are not claimed to be resolved.

Menu music SHA-256: `6C2A1751D11D452E9C205ED2BF22973F3CB048EC9B51854D7641BD64153E3EA0`.

## Reproduce

Close the Unity editor before launching batch mode against this project. From the project root, run:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1-x86_64\Editor\Unity.exe' -batchmode -quit -projectPath . -executeMethod EnvironmentBuilder.BuildBatch -shotDir Artifacts/Foliage3D/Environment -logFile foliage3d-final.log
```

Then run the smoke suite without `-quit`; its runner exits Unity with the test result:

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.3.2f1-x86_64\Editor\Unity.exe' -batchmode -projectPath . -executeMethod SmokeTest.RunBatch -shotDir Artifacts/Foliage3D/Smoke -logFile foliage3d-smoke.log
```

Logs and captures are local verification output and are ignored by Git. Unity-generated terrain, mesh, material, prefab and navigation assets are committed with their metadata so the rebuilt environment is immediately available.

## Limits

The foliage retains a stylized procedural appearance. Mesh grass is static; its material does not apply terrain-card wind animation. Flowers remain texture details. Frame rate on the target hardware has not been measured.

## Latest combat and ammunition regression

The latest Unity CLI run passed **107 checks**, exited 0, and logged zero runtime errors/exceptions. It includes the earlier environment and gameplay coverage plus:

- A runtime upper-body attack layer preserves walking/running foot animation and horizontal movement while six procedural sword variants play. No attack clips or controller assets were added or edited.
- The entire stowed bow and string lie alongside the back plane, rather than touching the torso only at the grip.
- Fully drawn shots with the Fire Arrows upgrade have sustained flame effects. Surviving zombies ignite and take four seconds of burn damage; lethal hits produce a brief ignition burst. Tests cover ignition, ongoing damage, expiry, death cleanup and lethal-hit feedback.
- A runtime right-hip quiver shows one physical arrow per available shot, scales for capacity increases, removes arrows when firing, refills through ammo pickups and is physically empty at zero ammo. Tests count active stored-arrow models as well as checking the displayed count.

Full/empty quiver, burning-zombie and attack captures were visually inspected. Compilation and source whitespace checks passed. Menu music retains the hash above. Existing Unity shutdown allocator diagnostics are not claimed to be resolved by these gameplay changes.

For this suite, use `Artifacts/FireQuiver/Smoke` as the smoke runner's `-shotDir`. Its `report.txt` records all 107 results. Generated logs, captures and runtime-only meshes remain outside the committed asset set.
