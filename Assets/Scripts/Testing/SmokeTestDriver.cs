#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// Runtime half of the automated smoke test (editor only, compiled out of builds).
// Only wakes up when Temp/mw_smoke.txt exists, i.e. when SmokeTest started play mode.
[DefaultExecutionOrder(200)]
public class SmokeTestDriver : MonoBehaviour
{
    string shotDir;
    readonly StringBuilder report = new StringBuilder();
    readonly List<string> errors = new List<string>();
    int failures;
    System.Action lateCheck;
    void LateUpdate()
    {
        var check = lateCheck;
        lateCheck = null;
        check?.Invoke();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        string flag = Path.Combine(Application.dataPath, "..", "Temp", "mw_smoke.txt");
        if (!File.Exists(flag) || FindFirstObjectByType<SmokeTestDriver>() != null) return;
        var go = new GameObject("Smoke Test Driver");
        DontDestroyOnLoad(go);
        var d = go.AddComponent<SmokeTestDriver>();
        d.shotDir = File.ReadAllText(flag).Trim();
    }

    void Awake()
    {
        GameSettings.TestMode = true;
        Application.logMessageReceived += OnLog;
    }

    void OnDestroy() => Application.logMessageReceived -= OnLog;

    void OnLog(string msg, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            if (msg.Contains("[SMOKE]")) return;
            errors.Add(type + ": " + msg + (type == LogType.Exception ? "\n" + stack : ""));
        }
    }

    void Check(bool ok, string what)
    {
        string line = (ok ? "PASS  " : "FAIL  ") + what;
        report.AppendLine(line);
        Debug.Log("[SMOKE] " + line);
        if (!ok) failures++;
    }

    void Start() => StartCoroutine(Run());

    IEnumerator Wait(float seconds) { yield return new WaitForSecondsRealtime(seconds); }

    IEnumerator Run()
    {
        var savedMode = GameSettings.Mode;
        var savedDiff = GameSettings.Difficulty;
        GameSettings.Difficulty = Difficulty.Knight;

        // ---------------------------------------------------------- menu
        yield return Wait(2.5f);
        var menu = FindFirstObjectByType<MainMenuUI>();
        Check(menu != null, "main menu UI built");
        Check(Camera.main != null && Camera.main.GetComponent<MenuCameraOrbit>() != null, "menu camera orbits the castle");
        Check(SceneManager.GetSceneByName("Background").isLoaded, "castle world loaded behind the menu");
        Capture("01_menu_title");
        if (menu != null)
        {
            foreach (var page in new[] { "play", "customise", "settings", "help", "feats", "credits" })
            {
                menu.DebugShow(page);
                yield return Wait(0.6f);
                Capture("02_menu_" + page);
            }
            menu.DebugShow("title");
        }
        var preview = FindFirstObjectByType<MenuPreview>();
        Check(preview != null && preview.Appearance != null, "knight preview + customisation component");
        Check(preview != null && preview.knight.GetComponent<MarchwardenVisual>() != null,
            "Marchwarden replaces the demo mesh in the customise preview");
        Check(preview!=null&&preview.knight.GetComponent<MarchwardenStature>()!=null&&
            Mathf.Abs(preview.knight.GetComponent<MarchwardenStature>().Multiplier-1.10f)<.001f,
            "customisation preview loads the ten-percent-taller warrior");

        // ---------------------------------------------------------- Defend the Castle
        GameSettings.Mode = GameMode.DefendTheCastle;
        var mc = FindFirstObjectByType<MainController>();
        if (mc != null) mc.StartGame(); else SceneManager.LoadScene("Semi-Final");
        yield return Wait(1.5f);
        Check(SceneManager.GetSceneByName("Background").isLoaded, "game loads the world scene itself");
        var gm = GameManager.instance;
        Check(gm != null, "GameManager present");
        Check(FindFirstObjectByType<GameHUD>() != null, "HUD built");
        // Keep the neutral art/animation fixture free of horde hit reactions.
        // Damage testing explicitly ends this protection later in the run.
        var introPlayer=GameObject.FindWithTag("Player");
        if(introPlayer!=null&&introPlayer.TryGetComponent<PlayerHealth>(out var introHealth))
            introHealth.InvulnerableUntil=float.PositiveInfinity;
        Capture("10_game_intro");
        yield return Wait(7f);
        Check(gm != null && gm.State == GameManager.MatchState.Playing, "match moved from intro to playing");
        Check(EnemyController.All.Count > 0, $"zombies spawned on their own ({EnemyController.All.Count} alive)");
        Capture("11_game_horde");

        var playerGo = GameObject.FindWithTag("Player");
        var player = playerGo != null ? playerGo.GetComponent<PlayerControlSpeed>() : null;
        var health = playerGo != null ? playerGo.GetComponent<PlayerHealth>() : null;
        Check(player != null && health != null, "player components");
        if (player == null || gm == null) { Finish(savedMode, savedDiff); yield break; }
        var marchwarden = player.GetComponent<MarchwardenVisual>();
        Check(marchwarden != null && marchwarden.Body.sharedMesh == MarchwardenVisual.Asset.body,
            "Marchwarden replaces the demo mesh in gameplay");
        if (marchwarden != null)
        {
            var stature=player.GetComponent<MarchwardenStature>();
            Vector3 statureBefore=player.transform.localScale;
            stature.Apply(MarchwardenVisual.Asset.statureScale);
            Check(Mathf.Abs(stature.Multiplier-1.10f)<.001f&&Vector3.Distance(statureBefore,player.transform.localScale)<.0001f,
                "gameplay stature is ten percent taller and repeated setup does not compound scale");
            Check(marchwarden.Body.sharedMaterials.Length == 14 && marchwarden.Sword != null && marchwarden.Shield != null,
                "Marchwarden has separate PBR surfaces and original equipped weapons");
            var structure=MarchwardenVisual.Asset;
            var chestVertices=structure.body.vertices.Select(v=>structure.meshToCharacter.MultiplyPoint3x4(v)).Where(v=>v.y>.98f&&v.y<1.12f&&Mathf.Abs(v.x)<.29f).ToArray();
            Check(chestVertices.Max(v=>v.x)-chestVertices.Min(v=>v.x)>.50f,"the enlarged upper torso retains a broad anatomical rib cage");
            Check(structure.quiver.bounds.size.y<.41f&&structure.quiver.bounds.size.x<.18f,"quiver bag is compact at realistic belt dimensions");
            Check(marchwarden.WaistArmor!=null&&marchwarden.WaistArmor.JointCount==60&&structure.waistArmor.boneWeights.All(w=>w.weight0==1&&w.weight1==0),
                "sixty metal lamellae use rigid hinge weights instead of thigh deformation");
            float crown=structure.shield.vertices.Where(v=>Mathf.Abs(v.y)<.04f).Max(v=>v.x);
            float shoulder=structure.shield.vertices.Where(v=>Mathf.Abs(v.y)>.20f).Max(v=>v.x);
            Check(crown-shoulder>.035f,$"heater has a distinct raised arch above its shoulders ({crown-shoulder:F3} m)");
            Check(marchwarden.Body.quality==SkinQuality.Bone4 &&
                marchwarden.Body.sharedMesh.boneWeights.Any(w=>w.weight2>.02f&&w.weight3>.02f),
                "cinematic shoulders keep all four anatomical skin influences at runtime");
            Check(marchwarden.Linen!=null&&marchwarden.Linen.Solver!=null&&marchwarden.Linen.Solver.enabled&&
                marchwarden.Linen.ParticleCount<1800&&marchwarden.Linen.PinnedParticles>8,
                "independent native cloth loads a lightweight mesh with a pinned waist");
            Check(marchwarden.Linen.Capsules.Length==13&&marchwarden.Linen.Solver.capsuleColliders.Length==13&&
                marchwarden.Linen.Capsules.Count(c=>c.transform.parent.name.Contains(" Foot"))==4,
                "cloth has explicit thigh, calf, heel, toe and pelvis collision capsules");
            Check(marchwarden.Linen.Solver.enableContinuousCollision&&marchwarden.Linen.Solver.useVirtualParticles>.9f,
                "cloth uses continuous capsule collision and virtual triangle particles");
            var finish=marchwarden.Body.sharedMaterials[0].mainTexture;
            Check(finish != null && finish.width == 4096 && finish.height == 4096 &&
                marchwarden.Body.sharedMaterials[0].GetTexture("_BumpMap") != null,
                "mesh-bound 4K surface atlas and tangent normals are loaded in gameplay");
            Check(marchwarden.Body.sharedMesh.uv.All(uv=>uv.x>=0 && uv.y>=0 && uv.x<=1 && uv.y<=1),
                "warrior surface UVs stay inside the unique texture atlas");
            Check(marchwarden.Body.sharedMesh.uv2.Length==marchwarden.Body.sharedMesh.vertexCount &&
                marchwarden.Body.sharedMaterials.All(m=>m.GetTexture("_DetailAlbedoMap")!=null&&m.IsKeywordEnabled("_DETAIL_MULX2")),
                "all fourteen surfaces retain independent close-up texture detail");
            marchwarden.ApplyColors(2, 3, 1);
            Check(marchwarden.Body.sharedMaterials[2].color == GameSettings.TabardColors[2].color,
                "new cloth and shield support saved palette customisation");
            marchwarden.ApplyColors(GameSettings.Tabard, GameSettings.Metal, GameSettings.Leather);
            lateCheck = () => {
                var posedBody=new Mesh();marchwarden.Body.BakeMesh(posedBody);
                var posedHeights=posedBody.vertices.Select(v=>marchwarden.Body.transform.TransformPoint(v).y).ToArray();
                float height=posedHeights.Max()-posedHeights.Min();Destroy(posedBody);
                Check(height>=1.60f&&height<2.10f,$"visible warrior stands taller with the gladiator crest ({height:F2} m)");
                var head=marchwarden.Body.bones.First(b=>b.name=="Bip001 Head");
                float rim=MarchwardenVisual.Asset.shield.vertices.Max(v=>player.transform.InverseTransformPoint(marchwarden.Shield.TransformPoint(v)).y);
                Check(rim<player.transform.InverseTransformPoint(head.position).y,
                    $"live neutral shield rim stays below the head joint ({player.transform.InverseTransformPoint(head.position).y-rim:F4} m)");
                CaptureFrom("10b_marchwarden_front", player.transform.position + player.transform.forward*2.6f + Vector3.up*1.35f,
                    player.transform.position + Vector3.up*.85f);
                Check(marchwarden.SwordGripError<.002f&&marchwarden.ShieldGripError<.003f,
                    $"sword and heater handles sit in the palms ({marchwarden.SwordGripError:F4}/{marchwarden.ShieldGripError:F4} m)");
                Vector3 palm=marchwarden.RightPalmPosition;
                CaptureFrom("10b_hand_sword",palm+player.transform.forward*.28f+player.transform.right*.16f+Vector3.up*.10f,palm);
                palm=marchwarden.LeftPalmPosition;
                CaptureFrom("10b_hand_shield",palm-player.transform.forward*.05f+player.transform.right*.22f+Vector3.up*.25f,palm);
                Check(marchwarden.Linen.Surface!=null&&marchwarden.Linen.Surface.isVisible,
                    "the simulated linen surface is visible to the rendered close-up camera");
            };
            yield return null;
            lateCheck = () => CaptureFrom("10c_marchwarden_back", player.transform.position - player.transform.forward*2.6f + player.transform.right*.6f + Vector3.up*1.35f,
                player.transform.position + Vector3.up*.85f);
            yield return null;
        }

        // clear the field, then line up one of each type in front of the knight
        foreach (var z in EnemyController.All.ToArray()) Destroy(z.gameObject);
        yield return null;
        health.InvulnerableUntil = float.PositiveInfinity;
        yield return ReviewDamageContinuity(player);
        if(File.Exists("Artifacts/DamageReviewOnly.txt")){Finish(savedMode,savedDiff);yield break;}
        yield return ReviewRunner(player,health);
        if(File.Exists("Artifacts/RunnerReviewOnly.txt")){Finish(savedMode,savedDiff);yield break;}
        if(File.Exists("Artifacts/PerformanceReviewOnly.txt"))
        {
            yield return PerformanceReview(player,marchwarden);Finish(savedMode,savedDiff);yield break;
        }
        if(File.Exists("Artifacts/ClothReviewOnly.txt")||File.Exists("Artifacts/StructureReviewOnly.txt"))
        {
            var reviewBody=player.GetComponent<Rigidbody>();
            yield return ReviewCloth(player,marchwarden,reviewBody,reviewBody.position,reviewBody.rotation);
            lateCheck=()=>{
                marchwarden.Body.enabled=false;
                CaptureFrom("motion_linen_only_live",player.transform.position+player.transform.forward*2.5f+player.transform.right*.95f+Vector3.up*.85f,
                    player.transform.position+Vector3.up*.65f);
                marchwarden.Body.enabled=true;
            };
            yield return null;Finish(savedMode,savedDiff);yield break;
        }
        var defensePelvis=marchwarden.Body.bones.First(b=>b.name=="Bip001 Pelvis");
        Vector3 defenseBaseline=defensePelvis.localPosition;
        player.DebugBlock(true);
        // Let Update consume the input before sampling its LateUpdate pose.
        // Otherwise the first capture reads unevaluated default diagnostics.
        yield return null;
        float defenseGap=float.MaxValue,defenseFeet=0;bool defenseSight=true;
        for(int frame=0;frame<28;frame++)
        {
            if(frame==19)player.DebugBlock(false);
            int capturedFrame=frame;
            lateCheck=()=>{
                var defense=player.DefensePose;
                defenseGap=Mathf.Min(defenseGap,defense.ChestGap);
                defenseFeet=Mathf.Max(defenseFeet,defense.FootDrift);defenseSight&=defense.SightlineClear;
                CaptureFrom($"motion_block_{capturedFrame:000}",player.transform.position+player.transform.forward*2.8f+player.transform.right*.5f+Vector3.up*1.2f,
                    player.transform.position+Vector3.up*.85f);
                if(capturedFrame==18){
                    Check(defense.CoversTorso,"live raised heater protects the sternum");
                    CaptureFrom("10d_heater_defense",player.transform.position+player.transform.forward*2.8f+player.transform.right*.5f+Vector3.up*1.2f,
                        player.transform.position+Vector3.up*.85f);
                }
            };
            yield return null;yield return Wait(.025f);
        }
        Check(defenseGap>=.015f,$"live heater block clears the chest throughout entry/hold/release ({defenseGap:F4} m)");
        Check(defenseFeet<.004f,$"live braced block keeps feet planted ({defenseFeet:F4} m)");
        Check(defenseSight,"live heater block retains clear sightlines");
        yield return Wait(.5f);
        Check(Vector3.Distance(defensePelvis.localPosition,defenseBaseline)<.004f,
            "blocking and release do not accumulate a pelvis offset or sink the character");
        // Exercise the real Update/FixedUpdate path, including the interpolated Rigidbody.
        var playerBody = player.GetComponent<Rigidbody>();
        var inputCamera = Camera.main.GetComponent<CameraFollow>();
        Vector3 inputForward = Vector3.ProjectOnPlane(inputCamera.LookDirection, Vector3.up).normalized;
        Vector3 inputRight = Vector3.Cross(Vector3.up, inputForward);
        Vector3 movementStart = player.transform.position;
        Quaternion movementRotation = player.transform.rotation;
        Check(!player.GetComponent<Animator>().applyRootMotion, "player root motion disabled");
        foreach (var moveCase in new[] { Vector2.left, Vector2.right, Vector2.down })
        {
            playerBody.position = movementStart;
            playerBody.rotation = Quaternion.LookRotation(inputForward);
            playerBody.linearVelocity = Vector3.zero;
            player.DebugMove(moveCase);
            yield return Wait(0.45f);
            Vector3 expected = inputRight * moveCase.x + inputForward * moveCase.y;
            Vector3 travelled = Vector3.ProjectOnPlane(playerBody.position - movementStart, Vector3.up);
            string key = moveCase == Vector2.left ? "A" : moveCase == Vector2.right ? "D" : "S";
            Check(Vector3.Angle(playerBody.rotation * Vector3.forward, expected) < 3f, key + " faces the requested cardinal direction");
            Check(Vector3.Dot(travelled, expected) > 0.25f, key + " moves in the requested direction");
            Check(Mathf.Abs(Vector3.Dot(travelled, Vector3.Cross(Vector3.up, expected))) < 0.12f, key + " does not walk diagonally while pivoting");
            player.DebugMove(Vector2.zero);
            yield return Wait(0.1f);
        }
        player.DebugMove(null);
        playerBody.position = movementStart;
        playerBody.rotation = movementRotation;
        playerBody.linearVelocity = Vector3.zero;
        // Reproduce obstruction handling in both free orbit and shoulder aim.
        Check(player.HasUpperBodySwordLayer, "runtime upper-body attack mask is active");
        var leftFoot = Bone(player.transform, "Bip001 L Foot");
        foreach (bool sprint in new[] { false, true })
        {
            player.DebugMove(Vector2.up, sprint);
            yield return Wait(0.4f);
            var movingCloth=marchwarden.Linen.Inspect();
            Check(movingCloth.finite&&movingCloth.maximumDisplacement>.003f&&movingCloth.maximumDisplacement<.40f,
                (sprint?"running":"walking")+" produces bounded live cloth movement");
            player.DebugSwingVariant(sprint ? 4 : 3);
            yield return Wait(0.07f);
            Quaternion footBefore = leftFoot.localRotation;
            Vector3 beforeAttackMove = playerBody.position;
            yield return Wait(0.2f);
            Check(Quaternion.Angle(footBefore, leftFoot.localRotation) > 2f,
                (sprint ? "running" : "walking") + " legs continue animating during a sword attack");
            Check(Vector3.ProjectOnPlane(playerBody.position - beforeAttackMove, Vector3.up).magnitude > 0.2f,
                (sprint ? "running" : "walking") + " translation continues during a sword attack");
            player.DebugMove(null);
            yield return Wait(0.4f);
            playerBody.position = movementStart;
            playerBody.rotation = movementRotation;
            playerBody.linearVelocity = Vector3.zero;
        }
        var swordHand = Bone(player.transform, "Bip001 R Hand");
        Check(player.SwordAnimationCount == SwordMoves.Count, "12 authored sword attack clips loaded");
        var swingHands = new Vector3[SwordMoves.Count];
        var swingRotations = new Quaternion[SwordMoves.Count];
        for (int swing = 0; swing < SwordMoves.Count; swing++)
        {
            player.DebugSwingVariant(swing);
            int index = swing;
            float strikeStarted=Time.unscaledTime,minimumGap=float.MaxValue,quiverArmGap=float.MaxValue,quiverTorsoGap=float.MaxValue,armorLegGap=float.MaxValue,armorClothGap=float.MaxValue;
            bool captured=false;
            while(Time.unscaledTime-strikeStarted<SwordMoves.All[swing].duration+.14f)
            {
                lateCheck = () =>
                {
                    minimumGap=Mathf.Min(minimumGap,EquippedWeaponClearance.Measure(marchwarden.Sword.localToWorldMatrix,
                        marchwarden.Shield.localToWorldMatrix,MarchwardenVisual.Asset.shield.bounds));
                    quiverArmGap=Mathf.Min(quiverArmGap,player.Gear.QuiverMotion.ArmGap);quiverTorsoGap=Mathf.Min(quiverTorsoGap,player.Gear.QuiverMotion.TorsoGap);
                    armorLegGap=Mathf.Min(armorLegGap,marchwarden.WaistArmor.LegGap);armorClothGap=Mathf.Min(armorClothGap,marchwarden.WaistArmor.ClothGap);
                    if(!captured&&Time.unscaledTime-strikeStarted>=SwordMoves.All[index].contact)
                    {
                        captured=true;
                        swingHands[index] = player.transform.InverseTransformPoint(swordHand.position);
                        swingRotations[index] = Quaternion.Inverse(player.transform.rotation)*swordHand.rotation;
                        CaptureFrom("11_combo_" + index, player.transform.position + player.transform.forward * 2.5f + Vector3.up * 1.4f,
                            player.transform.position + Vector3.up * 1.2f);
                    }
                };
                yield return null;
            }
            Check(minimumGap>=.008f,$"live sword stroke {index:00} clears shield through anticipation, cut and recovery ({minimumGap:F3} m)");
            Check(quiverArmGap>=.008f&&quiverTorsoGap>=.008f,$"quiver and arrows clear arm/torso during strike {index:00} ({quiverArmGap:F3}/{quiverTorsoGap:F3} m)");
            Check(armorLegGap>=.003f&&armorClothGap>=.003f,$"hinged armor clears legs/linen during strike {index:00} ({armorLegGap:F3}/{armorClothGap:F3} m)");
        }
        Check(Vector3.Distance(swingHands[0], swingHands[1]) > 0.04f, "backhand differs visibly from first cut");
        Check(Vector3.Distance(swingHands[0], swingHands[2]) > 0.04f, "overhead finisher differs visibly from first cut");
        for (int variant = 3; variant < 6; variant++)
            Check(Vector3.Distance(swingHands[variant], swingHands[variant - 3]) > 0.04f,
                "alternate sword variation " + variant + " differs from its original combo step");
        for (int variant = 6; variant < SwordMoves.Count; variant++)
            Check(Vector3.Distance(swingHands[variant], swingHands[variant%3]) > .035f ||
                Quaternion.Angle(swingRotations[variant], swingRotations[variant%3]) > 12f,
                "new sword stroke " + SwordMoves.All[variant].name + " has a distinct hand path or blade orientation");
        // Reproduce a real mid-attack combo start. The old graph dropped its
        // upper-body layer to zero here, producing a one-frame snap to idle.
        // Sample the attack clock deterministically: expensive screenshot or
        // import frames must not advance a real-time wait beyond recovery.
        float comboCapture=Time.captureDeltaTime;
        try
        {
            Time.captureDeltaTime=1f/120;
            player.DebugSwingVariant(0);
            int preparationFrames=Mathf.CeilToInt((SwordMoves.All[0].duration-SwordMoves.RecoveryFade-.015f)*120);
            for(int frame=0;frame<preparationFrames;frame++)yield return null;
            float beforeComboWeight=player.SwordLayerWeight;
            player.DebugSwingVariant(1);
            Check(beforeComboWeight>.6f&&player.SwordLayerWeight>=beforeComboWeight-.05f,
                "combo start retains the outgoing attack weight instead of snapping to idle");
            Check(player.SwordCrossfade<.10f,"combo cross-fade begins on the outgoing pose");
            for(int frame=0;frame<Mathf.CeilToInt((SwordMoves.Crossfade+.035f)*120);frame++)yield return null;
            Check(player.SwordCrossfade>.99f,"combo cross-fade reaches the next attack continuously");
        }
        finally{Time.captureDeltaTime=comboCapture;}
        yield return Wait(.65f);
        Check(SwordMoves.All.All(move=>move.contact<=.185f&&move.duration<=.45f&&move.windup>=.065f),
            "rapid attacks retain readable preparation and contact within 185 ms");
        int sequence=player.SwordSequence;
        player.DebugAttackPress();yield return null;
        while(player.SwordAge<SwordMoves.All[player.SwordVariant].chain-.08f)yield return null;
        int firstSequence=player.SwordSequence;
        player.DebugAttackPress();
        yield return Wait(.14f);
        Check(firstSequence==sequence+1&&player.SwordSequence==firstSequence+1,
            "an early attack press is buffered and starts exactly one follow-up at the chain gate");
        yield return Wait(.5f);
        yield return ReviewCloth(player,marchwarden,playerBody,movementStart,movementRotation);
        yield return ReviewSwordMotion(player);
        // The camera sphere must stay outside the wall throughout retraction.
        var cameraWall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cameraWall.name = "Camera regression wall";
        cameraWall.transform.SetPositionAndRotation(movementStart + Vector3.up * 1.5f - inputForward * 1.5f,
            Quaternion.LookRotation(inputForward));
        cameraWall.transform.localScale = new Vector3(4f, 4f, 0.25f);
        var cameraWallCollider = cameraWall.GetComponent<Collider>();
        foreach (bool aimCase in new[] { false, true })
        {
            player.Equip(aimCase ? Weapon.Bow : Weapon.Sword);
            player.DebugAim(aimCase);
            float minimumClearance = float.MaxValue;
            for (int frame = 0; frame < 25; frame++)
            {
                // WaitForEndOfFrame is not dispatched by the Editor in batch mode.
                yield return null;
                minimumClearance = Mathf.Min(minimumClearance,
                    Vector3.Distance(Camera.main.transform.position, cameraWallCollider.ClosestPoint(Camera.main.transform.position)));
            }
            Check(minimumClearance >= inputCamera.collisionRadius - 0.03f,
                (aimCase ? "aim" : "free orbit") + " camera stays outside obstruction");
        }
        Destroy(cameraWall);
        player.DebugAim(false);
        player.Equip(Weapon.Sword);
        yield return Wait(0.5f);
        var spawner = ZombieSpawner.Instance;
        var cam = Camera.main;
        Vector3 fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
        int savedAmmo = player.Arrows, savedCapacity = player.maxArrows;
        var hipQuiver = player.transform.Find("Right Hip Quiver");
        Check(hipQuiver != null && Vector3.Dot(hipQuiver.position - player.transform.position, player.transform.right) > 0.1f,
            "quiver is mounted on the right hip");
        Check(player.Gear.VisibleArrowCount == savedAmmo, "quiver starts with exact ammo count");
        player.DebugFireAt(player.transform.position + Vector3.up * 40f, 1f);
        yield return null;
        yield return null;
        Check(player.Gear.VisibleArrowCount == savedAmmo - 1, "firing removes one physical quiver arrow");
        player.maxArrows += 15;
        player.AddArrows(player.maxArrows);
        yield return null;
        yield return null;
        int physicalArrows = hipQuiver.Cast<Transform>().Count(t => t.name.StartsWith("Stored Arrow") && t.gameObject.activeSelf);
        Check(physicalArrows == player.Arrows && player.Gear.VisibleArrowCount == player.maxArrows,
            "expanded quiver displays every refilled arrow");
        lateCheck = () => CaptureFrom("11q_quiver_full", hipQuiver.position + player.transform.right * 1.5f + Vector3.up * 0.6f, hipQuiver.position + Vector3.up * 0.4f);
        yield return null;
        while (player.Arrows > 0) player.DebugFireAt(player.transform.position + Vector3.up * 40f, 1f);
        yield return null;
        yield return null;
        Check(player.Gear.VisibleArrowCount == 0 && !hipQuiver.Cast<Transform>().Any(t => t.name.StartsWith("Stored Arrow") && t.gameObject.activeSelf),
            "quiver is physically empty at zero ammo");
        lateCheck = () => CaptureFrom("11r_quiver_empty", hipQuiver.position + player.transform.right * 1.5f + Vector3.up * 0.6f, hipQuiver.position + Vector3.up * 0.4f);
        yield return null;
        foreach (var projectile in Object.FindObjectsByType<Arrow>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
        player.maxArrows = savedCapacity;
        player.AddArrows(savedAmmo);
        yield return null;
        yield return null;
        Check(player.Gear.VisibleArrowCount == savedAmmo, "arrow pickup refills the visible quiver");

        yield return ResolveBoons();
        // Keep the ignition fixture deterministic while the normal horde remains
        // in the scene for the following gameplay checks.
        bool managerWasEnabled=gm.enabled; gm.enabled=false;
        var fireOccluders=EnemyController.All.Where(e=>e!=null)
            .SelectMany(e=>e.GetComponentsInChildren<Collider>()).Where(c=>c.enabled).ToArray();
        foreach(var collider in fireOccluders) collider.enabled=false;
        Vector3 burnAt = player.transform.position + fwd * 7f;
        if (UnityEngine.AI.NavMesh.SamplePosition(burnAt, out var burnGround, 5f, UnityEngine.AI.NavMesh.AllAreas)) burnAt = burnGround.position;
        var burningEnemy = Instantiate(spawner.zombiePrefab, burnAt, Quaternion.LookRotation(-fwd)).GetComponent<EnemyController>();
        burningEnemy.Init(ZombieType.Brute, false, false);
        burningEnemy.player = null;
        if(burningEnemy.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var burnAgent)) burnAgent.enabled=false;
        yield return null;
        Physics.SyncTransforms();
        Boons.Apply(BoonId.FireArrows, player.gameObject);
        player.DebugFireAt(burningEnemy.transform.position + Vector3.up * 0.9f * burningEnemy.transform.localScale.y, 1f);
        Check(Object.FindObjectsByType<Arrow>(FindObjectsSortMode.None).Any(a => a.IsFlaming && a.GetComponentInChildren<ParticleSystem>() != null),
            "fully drawn fire arrow has an active flame emitter");
        for(int i=0;i<20 && !burningEnemy.IsBurning;i++) yield return Wait(.04f);
        Check(burningEnemy.IsBurning, "fire arrow impact ignites the zombie");
        Check(burningEnemy.transform.Find("Burning Flames") != null, "burning zombie has sustained flames");
        float burningHealth = burningEnemy.Health;
        yield return Wait(0.8f);
        Check(burningEnemy.Health < burningHealth - 0.3f, "ignited zombie takes ongoing fire damage");
        lateCheck = () => CaptureFrom("11s_zombie_burning", burnAt + player.transform.right * 3f + Vector3.up * 1.7f, burnAt + Vector3.up * 1.1f);
        yield return null;
        yield return Wait(3.5f);
        Check(!burningEnemy.IsBurning && burningEnemy.transform.Find("Burning Flames") == null, "burn expires and removes zombie flames");
        burningEnemy.Ignite(2f, burningEnemy.Health * 2f);
        yield return Wait(0.8f);
        Check(burningEnemy.IsDead && burningEnemy.transform.Find("Burning Flames") == null, "burn damage can kill and cleans up flames");
        Destroy(burningEnemy.gameObject);
        var lethalTarget = Instantiate(spawner.zombiePrefab, burnAt, Quaternion.LookRotation(-fwd)).GetComponent<EnemyController>();
        lethalTarget.Init(ZombieType.Walker, false, false);
        lethalTarget.player = null;
        if(lethalTarget.TryGetComponent<UnityEngine.AI.NavMeshAgent>(out var lethalAgent)) lethalAgent.enabled=false;
        yield return null;
        Physics.SyncTransforms();
        player.DebugFireAt(lethalTarget.transform.position + Vector3.up * 0.9f, 1f);
        for(int i=0;i<20 && !lethalTarget.IsDead;i++) yield return Wait(.04f);
        Check(lethalTarget.IsDead && lethalTarget.transform.Find("Burning Flames") != null,
            $"lethal fire arrow hits still show an ignition burst (health {lethalTarget.Health:F1})");
        foreach(var collider in fireOccluders) if(collider!=null) collider.enabled=true;
        gm.enabled=managerWasEnabled;
        Destroy(lethalTarget.gameObject);
        foreach (var projectile in Object.FindObjectsByType<Arrow>(FindObjectsSortMode.None)) Destroy(projectile.gameObject);
        Boons.Reset();
        player.AddArrows(player.maxArrows);
        yield return null;
        var line = new List<EnemyController>();
        int n = 0;
        foreach (ZombieType t in System.Enum.GetValues(typeof(ZombieType)))
        {
            Vector3 side = Vector3.Cross(Vector3.up, fwd);
            Vector3 at = player.transform.position + fwd * 11f + side * ((n - 2.5f) * 2.6f);
            if (UnityEngine.AI.NavMesh.SamplePosition(at, out var hit, 6f, UnityEngine.AI.NavMesh.AllAreas)) at = hit.position;
            var go = Instantiate(spawner.zombiePrefab, at, Quaternion.LookRotation(-fwd));
            var e = go.GetComponent<EnemyController>();
            e.Init(t, false, true);
            line.Add(e);
            n++;
        }
        yield return Wait(1.6f);
        Capture("12_bestiary_rising");
        var start = line.Select(e => e.transform.position).ToArray();
        var peakSpeed = new float[line.Count];
        for (float t = 0; t < 2.2f; t += 0.1f)
        {
            for (int i = 0; i < line.Count; i++)
                if (line[i] != null && line[i].TryGetComponent(out Animator a)) peakSpeed[i] = Mathf.Max(peakSpeed[i], a.GetFloat("Speed"));
            yield return Wait(0.1f);
        }
        Capture("13_bestiary_charging");
        for (int i = 0; i < line.Count; i++)
        {
            var e = line[i];
            if (e == null) continue;
            float moved = Vector3.Distance(start[i], e.transform.position);
            var anim = e.GetComponent<Animator>();
            if (e.type == ZombieType.Spitter) Check(true, $"Spitter keeps range (moved {moved:F1} m)");
            else
            {
                Check(moved > 0.8f, $"{e.type} moves towards the knight ({moved:F1} m)");
                Check(peakSpeed[i] > 0.4f, $"{e.type} locomotion animation follows speed (peak {peakSpeed[i]:F2})");
            }
            Check(anim == null || !anim.applyRootMotion, $"{e.type} has root motion off");
        }

        // bow
        player.Equip(Weapon.Bow);
        player.DebugAim(true);
        yield return Wait(0.6f);
        Capture("14_bow_aim");
        int arrowsBefore = player.Arrows, firedBefore = gm.ArrowsFired;
        foreach (var e in line.Where(x => x != null && !x.IsDead).Take(3))
        {
            player.DebugFireAt(e.transform.position + Vector3.up * 1.2f, 1f);
            yield return Wait(0.35f);
        }
        yield return Wait(0.8f);
        Check(gm.ArrowsFired - firedBefore == 3, "three shots fired and counted");
        Check(player.Arrows < arrowsBefore || player.Arrows == player.maxArrows, "arrows consumed (unless topped up from a crate)");
        Check(gm.ArrowsHit >= 1, $"arrows hit zombies ({gm.ArrowsHit}/{gm.ArrowsFired})");
        // archer stance: the draw hand travels to the cheek, the bow arm points down the shot
        var lUpper = Bone(player.transform, "Bip001 L UpperArm");
        var lHand = Bone(player.transform, "Bip001 L Hand");
        var rHand = Bone(player.transform, "Bip001 R Hand");
        player.DebugHoldDraw(0.01f);
        yield return Wait(0.8f);
        float braceGap = Vector3.Distance(lHand.position, rHand.position);
        CaptureFrom("14b_archer_brace_side", player.transform.position + player.transform.right * 2.6f + player.transform.forward * 0.6f + Vector3.up * 1.5f, player.transform.position + Vector3.up * 1.3f);
        player.DebugHoldDraw(1f);
        yield return Wait(0.8f);
        float fullGap = Vector3.Distance(lHand.position, rHand.position);
        float armAngle = Vector3.Angle(lHand.position - lUpper.position, Camera.main.transform.forward);
        CaptureFrom("14c_archer_full_side", player.transform.position + player.transform.right * 2.6f + player.transform.forward * 0.6f + Vector3.up * 1.5f, player.transform.position + Vector3.up * 1.3f);
        CaptureFrom("14d_archer_full_front", player.transform.position + player.transform.forward * 2.8f - player.transform.right * 0.8f + Vector3.up * 1.6f, player.transform.position + Vector3.up * 1.3f);
        Capture("14e_archer_full_aim");
        Check(fullGap > braceGap + 0.06f, $"draw hand pulls the string back ({braceGap:F2} m -> {fullGap:F2} m between hands)");
        Check(player.Gear != null && player.Gear.Ready, "sword and shield stowed for the bow (" + (player.Gear != null ? player.Gear.Report : "no gear") + ")");
        Check(armAngle < 30f, $"bow arm points along the aim ({armAngle:F0} deg off)");
        var archer = player.GetComponent<ArcherRig>();
        Check(archer.GripError < .008f, $"bow grip touches palm ({archer.GripError:F3} m)");
        CaptureFrom("14_hand_bow",archer.BowPalmPosition+player.transform.forward*.23f+player.transform.right*.22f+Vector3.up*.08f,archer.BowPalmPosition);
        CaptureFrom("14_hand_draw",archer.DrawPalmPosition+player.transform.forward*.25f+player.transform.right*.24f+Vector3.up*.06f,archer.DrawPalmPosition);
        Check(marchwarden.Body.GetBlendShapeWeight(marchwarden.Body.sharedMesh.GetBlendShapeIndex("BowDrawHand"))>90,
            "draw fingers adopt the string hook pose instead of retaining the sword grip");
        Check(archer.AnchorError < .03f, $"draw hand reaches stable facial anchor ({archer.AnchorError:F3} m)");
        Check(archer.DrawElbowAlignment < 45f, $"draw elbow aligns behind arrow ({archer.DrawElbowAlignment:F1} deg)");
        Check(archer.ShaftAlignment < 8f, $"nocked arrow follows aim ({archer.ShaftAlignment:F1} deg)");
        Check(player.Gear.QuiverMotion.ArmGap>=.008f&&player.Gear.QuiverMotion.TorsoGap>=.008f&&player.Gear.QuiverMotion.SuspensionOffset<.16f,
            "compact quiver clears the full bow draw without leaving its suspension");
        for(int frame=0;frame<20;frame++)
        {
            if(frame<12) player.DebugHoldDraw(frame/11f);
            else if(frame==12){player.DebugHoldDraw(0);archer.Release();}
            yield return Wait(.055f);
            string name="motion_bow_"+frame.ToString("000");
            lateCheck=()=>CaptureFrom(name,player.transform.position+player.transform.right*2.5f+player.transform.forward*.9f+Vector3.up*1.45f,
                player.transform.position+Vector3.up*1.12f);
            yield return null;
        }
        player.DebugHoldDraw(1); yield return Wait(.5f);
        var yawField = typeof(CameraFollow).GetField("currentYaw", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var pitchField = typeof(CameraFollow).GetField("currentPitch", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float savedYaw = (float)yawField.GetValue(inputCamera), savedPitch = (float)pitchField.GetValue(inputCamera);
        foreach (float yawOffset in new[] { -90f, 90f })
        {
            yawField.SetValue(inputCamera, savedYaw + yawOffset);
            pitchField.SetValue(inputCamera, 0f);
            yield return Wait(0.7f);
            Vector3 look = Vector3.ProjectOnPlane(inputCamera.LookDirection, Vector3.up).normalized;
            Check(Vector3.Angle(playerBody.rotation * Vector3.forward, look) < 3f, $"bow body follows yaw {yawOffset}");
            float trackingAngle=Vector3.Angle(lHand.position-lUpper.position,look);
            Check(trackingAngle < 30f, $"bow arm follows yaw {yawOffset} ({trackingAngle:F1} deg)");
            Check(archer.ShaftAlignment < 8f && archer.GripError < .008f,
                $"arrow and grip remain aligned after yaw {yawOffset}");
            Check(player.Gear.QuiverMotion.ArmGap>=.008f&&player.Gear.QuiverMotion.TorsoGap>=.008f&&player.Gear.QuiverMotion.SuspensionOffset<.16f,
                $"quiver and arrows clear the draw arm and torso at yaw {yawOffset}");
            Capture(yawOffset < 0 ? "14f_bow_left_yaw" : "14g_bow_right_yaw");
        }
        foreach (float extremePitch in new[] { -70f, 70f })
        {
            pitchField.SetValue(inputCamera, extremePitch);
            yield return Wait(0.7f);
            var heldBow = player.transform.Find("Bow (equipment)");
            float bowPitch = Mathf.Asin(Mathf.Clamp(heldBow.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            Check(Mathf.Abs(bowPitch) <= 45.1f, $"bow pitch stays clamped at camera pitch {extremePitch}");
        }
        yawField.SetValue(inputCamera, savedYaw);
        pitchField.SetValue(inputCamera, savedPitch);
        player.DebugHoldDraw(0f);
        player.DebugAim(false);

        lateCheck = () =>
        {
            var slungShield = player.transform.Find("Slung Shield");
            Vector3 backNormal = player.Gear.ChestRotation * Vector3.back;
            var shieldMesh = slungShield.GetComponent<MeshFilter>().sharedMesh;
            float shieldGap = shieldMesh.vertices.Min(v => Vector3.Dot(slungShield.TransformPoint(v) - player.Gear.BackSurface, backNormal));
            Check(shieldGap >= -0.01f && shieldGap < 0.035f, $"shield sits flush against chest mount ({shieldGap:F3} m)");
            Check(Vector3.Dot(backNormal, player.transform.forward) < -0.3f, "shield mount faces behind the knight");
            CaptureFrom("14h_shield_stowed", player.Gear.BackSurface + backNormal * 1.7f + Vector3.up * 0.25f, player.Gear.BackSurface);
        };
        yield return null;
        player.Equip(Weapon.Sword);
        yield return Wait(0.7f);
        lateCheck = () =>
        {
            var stowedBow = player.transform.Find("Bow (equipment)");
            Vector3 backNormal = player.Gear.ChestRotation * Vector3.back;
            var bowMesh = stowedBow.GetComponent<MeshFilter>().sharedMesh;
            float bowGap = bowMesh.vertices.Min(v => Vector3.Dot(stowedBow.TransformPoint(v) - player.Gear.BackSurface, backNormal));
            Check(bowGap >= -0.01f && bowGap < 0.035f, $"bow grip sits flush against chest mount ({bowGap:F3} m)");
            float farthestBow = bowMesh.vertices.Max(v => Vector3.Dot(stowedBow.TransformPoint(v) - player.Gear.BackSurface, backNormal));
            Check(farthestBow < 0.11f, "entire stowed bow lies within 11 cm of the back plane");
            var bowString = stowedBow.GetComponentInChildren<LineRenderer>();
            float stringMin = float.MaxValue, stringMax = float.MinValue;
            for (int i = 0; i < bowString.positionCount; i++)
            {
                float depth = Vector3.Dot(bowString.GetPosition(i) - player.Gear.BackSurface, backNormal);
                stringMin = Mathf.Min(stringMin, depth); stringMax = Mathf.Max(stringMax, depth);
            }
            Check(stringMin > 0f && stringMax < 0.08f && stringMax - stringMin < 0.01f,
                "stowed bowstring lies flat and close to the back");
            CaptureFrom("14i_bow_stowed", player.Gear.BackSurface + backNormal * 1.7f + Vector3.up * 0.25f, player.Gear.BackSurface);
        };
        yield return null;

        // rolling terrain inside the walls, and every house seated on it
        if(File.Exists("Artifacts/GripReviewOnly.txt")){Finish(savedMode,savedDiff);yield break;}
        var terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float lo = float.MaxValue, hi = float.MinValue;
            Vector3 c = player.transform.position;
            for (int a = 0; a < 360; a += 15)
                for (float r = 10f; r <= 50f; r += 10f)
                {
                    Vector3 q = c + Quaternion.Euler(0, a, 0) * Vector3.forward * r;
                    float h = terrain.SampleHeight(q) + terrain.transform.position.y;
                    lo = Mathf.Min(lo, h); hi = Mathf.Max(hi, h);
                }
            Check(hi - lo > 2f, $"ground rolls through the castle ({hi - lo:F1} m of relief)");
            int floating = 0, houses = 0;
            foreach (var house in Object.FindObjectsByType<TintVariation>(FindObjectsSortMode.None))
            {
                var rends = house.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) continue;
                var b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float ground = terrain.SampleHeight(b.center) + terrain.transform.position.y;
                houses++;
                if (Mathf.Abs(b.min.y - ground) > 0.5f) floating++;
            }
            Check(houses > 10 && floating == 0, $"houses sit on the terrain ({houses - floating}/{houses} seated)");
        }

        // sword
        player.Equip(Weapon.Sword);
        var target = line.FirstOrDefault(x => x != null && !x.IsDead && x.type != ZombieType.Bloater);
        if (target != null)
        {
            var body = player.GetComponent<Rigidbody>();
            Vector3 dir = target.transform.position - player.transform.position; dir.y = 0; dir.Normalize();
            Vector3 spot = target.transform.position - dir * 1.3f;
            if (body != null) { body.position = spot; body.rotation = Quaternion.LookRotation(dir); }
            player.transform.SetPositionAndRotation(spot, Quaternion.LookRotation(dir));
            Physics.SyncTransforms();
            yield return null;
            float before = target.Health;
            player.DebugSwing();
            yield return Wait(0.5f);
            Check(target == null || target.IsDead || target.Health < before, "sword finisher damages a zombie");
        }
        Capture("15_melee");

        // wound the Warlord past half: it enrages and raises walkers around itself
        var warlord = line.FirstOrDefault(x => x != null && !x.IsDead && x.type == ZombieType.Warlord);
        if (warlord != null)
        {
            int aliveBefore = EnemyController.All.Count;
            warlord.TakeDamage(warlord.Health * 0.6f, fwd, 0f);
            yield return Wait(0.5f);
            Check(EnemyController.All.Count >= aliveBefore + 2, $"enraged Warlord summons the dead ({aliveBefore} -> {EnemyController.All.Count})");
            yield return Wait(1.2f);
            Capture("15b_warlord_summons");
            line.AddRange(EnemyController.All.Where(z => !line.Contains(z)));
        }

        // kill everything; bloater explosion must not throw
        int killsBefore = gm.currentKills, scoreBefore = gm.Score;
        foreach (var e in line.Where(x => x != null && !x.IsDead).ToArray()) e.TakeDamage(999f, fwd, 5f);
        yield return Wait(1.5f);
        Check(gm.currentKills > killsBefore, $"kills counted ({gm.currentKills - killsBefore})");
        int airborne = 0, grounded = 0, embedded = 0;
        foreach (var p in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
        {
            if (!p.staticVisual) continue;
            if (Physics.Raycast(p.transform.position + Vector3.up * 0.5f, Vector3.down, 2.5f,~0,QueryTriggerInteraction.Ignore)) grounded++;
            else if(Physics.RaycastAll(p.transform.position,p.transform.forward,1.05f,~0,QueryTriggerInteraction.Ignore)
                .Any(hit=>hit.collider.GetComponentInParent<EnemyController>()==null&&hit.collider.GetComponentInParent<PlayerHealth>()==null)) embedded++;
            else airborne++;
        }
        Check(airborne == 0, $"spent arrows are supported by ground or embedded in scenery ({grounded} grounded, {embedded} embedded, {airborne} unsupported)");
        Check(gm.Score > scoreBefore, "score increases with kills");
        Capture("16_after_kills");

        // spitter bile hits the knight and leaves acid (stand still first: blasts may have knocked him back)
        yield return ResolveBoons();
        var rbody = player.GetComponent<Rigidbody>();
        if (rbody != null) rbody.linearVelocity = Vector3.zero;
        yield return Wait(1f);
        if (rbody != null) rbody.linearVelocity = Vector3.zero;
        health.InvulnerableUntil = 0f;
        float hp = health.currentHealth;
        BileGlob.Throw(player.transform.position + Vector3.up * 6f + fwd * 4f, player.transform.position, 10f, null);
        yield return Wait(1.8f);
        Check(health.currentHealth < hp, $"bile damages the knight ({hp:F0} -> {health.currentHealth:F0})");
        Check(FindFirstObjectByType<AcidPool>() != null, "acid pool created");
        Capture("17_acid");
        yield return ReviewAcidPools(player);

        // pickups
        player.AddArrows(-10);   // make room in the quiver
        int arrows = player.Arrows;
        Pickup.Spawn(PickupKind.Arrows, player.transform.position + fwd * 0.5f, 6);
        yield return Wait(1.2f);
        Check(player.Arrows > arrows, $"arrow pickup collected ({arrows} -> {player.Arrows})");
        Check(Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).Count(p => p.respawns) >= 5, "supply crates with pickups exist in the world");
        Check(Object.FindObjectsByType<BannerFlag>(FindObjectsSortMode.None).Length >= 6, "banners fly from the towers");
        Check(Object.FindObjectsByType<ChimneySmoke>(FindObjectsSortMode.None).Length >= 3, "village chimneys smoke");

        // pause menu
        yield return ResolveBoons();
        gm.Pause();
        yield return Wait(0.5f);
        Check(Time.timeScale == 0f, "pause freezes time");
        Capture("18_pause");
        gm.Resume();
        yield return Wait(0.3f);
        Check(Time.timeScale > 0.5f, "resume restores time");

        // victory
        health.InvulnerableUntil = Time.time + 30f;
        while (gm.currentKills < gm.targetKills) gm.AddKill(null);
        // the kill milestones offer boons (time freezes until one is picked)
        for (int i = 0; i < 6; i++)
        {
            yield return Wait(0.3f);
            if (GameHUD.BoonsOpen) { if (i == 0) Capture("18b_defend_boon"); GameHUD.DebugPickBoon(i % 3); }
        }
        Check(Boons.Total >= 2, $"Defend the Castle offered boons at 1/3 and 2/3 ({Boons.Total})");
        yield return Wait(2.2f);   // the Warlord appears at 75% and needs a moment to rise
        Check(gm.Boss != null, "the Warlord leads the final assault");
        if (gm.Boss != null && !gm.Boss.IsDead) gm.Boss.TakeDamage(9999f, fwd, 0f);
        yield return Wait(2.5f);
        Check(gm.State == GameManager.MatchState.Won, "Defend the Castle can be won");
        Capture("19_victory");

        gm.QuitGame();
        yield return Wait(3f);
        Check(FindFirstObjectByType<MainMenuUI>() != null, "returns to main menu");
        Check(Time.timeScale == 1f, "time scale reset in menu");
        Capture("20_menu_again");

        // ---------------------------------------------------------- Endless Horde
        GameSettings.Mode = GameMode.EndlessHorde;
        FindFirstObjectByType<MainController>().StartGame();
        yield return Wait(9f);
        gm = GameManager.instance;
        Check(gm != null && gm.Wave >= 1, $"endless horde wave started (wave {gm?.Wave})");
        Capture("30_endless");
        // clear wave 1 by force: a boon choice must appear and freeze time
        for (int i = 0; i < 40 && gm.Wave == 1 && !GameHUD.BoonsOpen; i++)
        {
            foreach (var z in EnemyController.All.ToArray()) if (z != null && !z.IsDead) z.TakeDamage(999f, Vector3.forward, 0f);
            yield return Wait(0.5f);
        }
        Check(GameHUD.BoonsOpen, "clearing a wave offers a boon");
        Check(Time.timeScale == 0f, "time frozen while choosing");
        Capture("32_boons");
        int boonsBefore = Boons.Total;
        GameHUD.DebugPickBoon(0);
        yield return Wait(0.3f);
        Check(Boons.Total == boonsBefore + 1 && !GameHUD.BoonsOpen, "boon applied");
        Check(Time.timeScale > 0.5f, "time resumes after the pick");
        Capture("33_boon_owned");
        health = GameObject.FindWithTag("Player").GetComponent<PlayerHealth>();
        health.TakeDamage(999f, null, 0f, true);
        yield return Wait(4.5f);
        Check(gm.State == GameManager.MatchState.Lost, "death ends the run");
        Capture("31_defeat");

        // ---------------------------------------------------------- Last Stand
        GameSettings.Mode = GameMode.LastStand;
        gm.RestartGame();
        yield return Wait(6f);
        gm = GameManager.instance;
        Check(gm != null && gm.Mode == GameMode.LastStand && gm.Objective.StartsWith("Dawn"), "last stand timer running");
        Capture("40_last_stand");

        Finish(savedMode, savedDiff);
    }

    IEnumerator ReviewSwordMotion(PlayerControlSpeed player)
    {
        float savedCapture=Time.captureDeltaTime;int shot=0;
        try
        {
            Time.captureDeltaTime=1f/60;
            foreach(int variant in new[]{0,5,11})
            {
                player.DebugSwingVariant(variant);
                int frames=Mathf.CeilToInt((SwordMoves.All[variant].duration+.12f)*60);
                for(int frame=0;frame<frames;frame++)
                {
                    if(frame%2==0)
                    {
                        string name=$"motion_sword_{shot++:000}";
                        lateCheck=()=>CaptureFrom(name,player.transform.position+player.transform.forward*2.55f+player.transform.right*.65f+Vector3.up*1.15f,
                            player.transform.position+Vector3.up*.81f);
                    }
                    yield return null;
                }
            }
        }
        finally{Time.captureDeltaTime=savedCapture;}
    }

    IEnumerator ReviewAcidPools(PlayerControlSpeed player)
    {
        var pools=new List<AcidPool>();var meshes=new List<Mesh>();var randomState=UnityEngine.Random.state;
        Vector3 centre=player.transform.position+player.transform.forward*7;
        try
        {
            UnityEngine.Random.InitState(42591);
            for(int i=0;i<4;i++)
            {
                var pool=AcidPool.Create(centre+player.transform.right*((i%2)*3.4f-1.7f)+player.transform.forward*((i/2)*3.4f),1.15f,0,8);
                pools.Add(pool);meshes.Add(pool.GetComponent<MeshFilter>().sharedMesh);
            }
            yield return Wait(.35f);
            bool varied=true,footprint=true,fitted=true;var signatures=new HashSet<Vector3>();
            foreach(var pool in pools)
            {
                var vertices=pool.GetComponent<MeshFilter>().sharedMesh.vertices;
                float shortest=float.MaxValue,longest=0;
                for(int i=0;i<72;i++)
                {
                    Vector3 v=vertices[1+6*72+i];float distance=new Vector2(v.x,v.z).magnitude;
                    shortest=Mathf.Min(shortest,distance);longest=Mathf.Max(longest,distance);
                    Vector3 world=pool.transform.TransformPoint(v);
                    footprint&=pool.Contains(pool.transform.position+new Vector3(v.x,0,v.z)*.8f)&&
                        !pool.Contains(pool.transform.position+new Vector3(v.x,0,v.z)*1.1f);
                    fitted&=Mathf.Abs(world.y-AcidPool.GroundBelow(world).y-.018f)<.006f;
                }
                varied&=longest/shortest>1.3f;signatures.Add(vertices[1+6*72]);
            }
            Check(varied&&signatures.Count==4,"acid impacts have distinct asymmetric lobes and splash silhouettes");
            Check(footprint,"acid damage footprint follows each irregular shoreline");
            Check(fitted,"acid shoreline follows the ground with a thin wet surface");
            Check(pools.All(p=>p.GetComponent<MeshRenderer>().sharedMaterial.shader.name=="MedievalWarrior/AcidPuddle"),
                "acid uses the wet animated puddle surface");
            CaptureFrom("17b_acid_shapes",centre-player.transform.forward*2.5f+Vector3.up*8,centre+player.transform.forward*1.5f);
        }
        finally
        {
            UnityEngine.Random.state=randomState;
            foreach(var pool in pools)if(pool!=null)Destroy(pool.gameObject);
        }
        yield return null;
        Check(meshes.All(mesh=>mesh==null),"expired acid objects release their unique surface meshes");
    }

    IEnumerator ReviewDamageContinuity(PlayerControlSpeed player)
    {
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var brain=typeof(EnemyController);var stateField=brain.GetField("state",flags);var clock=brain.GetField("stateTimer",flags);
        var setState=brain.GetMethod("SetState",flags);var token=brain.GetField("hasToken",flags);
        if(!UnityEngine.AI.NavMesh.SamplePosition(player.transform.position+player.transform.forward*1.5f,out var point,1,UnityEngine.AI.NavMesh.AllAreas))
        {Check(false,"damage continuity fixture has navigation");yield break;}
        foreach(ZombieType kind in System.Enum.GetValues(typeof(ZombieType)))
        {
            var enemy=Instantiate(ZombieSpawner.Instance.zombiePrefab,point.position,Quaternion.LookRotation(-player.transform.forward)).GetComponent<EnemyController>();
            enemy.Init(kind,false,false);enemy.enabled=false;
            var agent=enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();var animation=enemy.GetComponent<Animator>();
            try
            {
                var states=new List<string>{kind==ZombieType.Spitter?"Kite":"Hunt","Attack"};
                if(kind==ZombieType.Bloater)states.Add("Fuse");
                if(kind==ZombieType.Brute||kind==ZombieType.Warlord)states.Add("Charge");
                foreach(string state in states)
                {
                    setState.Invoke(enemy,new[]{System.Enum.Parse(stateField.FieldType,state)});clock.SetValue(enemy,.12f);
                    bool held=ZombieDirector.RequestToken(enemy);token.SetValue(enemy,held);
                    bool stopped=agent.isStopped;float before=enemy.Health,timeScale=Time.timeScale,animationSpeed=animation.speed;
                    enemy.TakeDamage(.125f,player.transform.forward,30f);
                    bool continuous=stateField.GetValue(enemy).ToString()==state&&(float)clock.GetValue(enemy)==.12f&&agent.isStopped==stopped&&
                        (bool)token.GetValue(enemy)==held&&animation.speed==animationSpeed&&Time.timeScale==timeScale;
                    Check(enemy.Health<before&&continuous,$"{kind} takes damage without interrupting {state}, navigation, animation or attack ownership");
                }
                if(kind==ZombieType.Walker)
                {
                    setState.Invoke(enemy,new[]{System.Enum.Parse(stateField.FieldType,"Hunt")});float before=enemy.Health,scale=Time.timeScale;
                    typeof(PlayerControlSpeed).GetMethod("CheckForHits",flags).Invoke(player,new object[]{0,0});
                    Check(enemy.Health<before&&Time.timeScale==scale,"a real sword hit deals damage without global hit-stop");
                }
            }
            finally{ZombieDirector.ReleaseToken(enemy);Destroy(enemy.gameObject);}
            yield return null;
        }
    }

    IEnumerator ReviewRunner(PlayerControlSpeed player,PlayerHealth health)
    {
        var spawner=ZombieSpawner.Instance;var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        var enemyType=typeof(EnemyController);
        object Call(EnemyController e,string name)=>enemyType.GetMethod(name,flags).Invoke(e,null);
        void Field(EnemyController e,string name,object value)=>enemyType.GetField(name,flags).SetValue(e,value);
        string State(EnemyController e)=>enemyType.GetField("state",flags).GetValue(e).ToString();
        bool rear=EnemyController.InRunnerRearArc(player.transform.position-player.transform.forward*2,player.transform.position,player.transform.forward);
        bool front=EnemyController.InRunnerRearArc(player.transform.position+player.transform.forward*2,player.transform.position,player.transform.forward);
        bool side=EnemyController.InRunnerRearArc(player.transform.position+player.transform.right*2,player.transform.position,player.transform.forward);
        Check(rear&&!front&&!side,"Runner attack sector includes the back and excludes front and sides");
        if(!UnityEngine.AI.NavMesh.SamplePosition(player.transform.position+player.transform.forward*4,out var spawn,2,UnityEngine.AI.NavMesh.AllAreas))
        {Check(false,"Runner test has a reachable spawn");yield break;}
        var fixture=Instantiate(spawner.zombiePrefab,spawn.position,Quaternion.identity).GetComponent<EnemyController>();
        fixture.Init(ZombieType.Runner,false,false);fixture.enabled=false;
        var agent=fixture.GetComponent<UnityEngine.AI.NavMeshAgent>();agent.isStopped=true;
        float savedHealth=health.currentHealth,savedInvulnerability=health.InvulnerableUntil;
        Quaternion savedRotation=player.transform.rotation;var randomState=UnityEngine.Random.state;
        bool Place(bool atBack,float distance)
        {
            Vector3 desired=player.transform.position+player.transform.forward*(atBack?-distance:distance);
            if(!UnityEngine.AI.NavMesh.SamplePosition(desired,out var hit,.5f,UnityEngine.AI.NavMesh.AllAreas))return false;
            agent.Warp(hit.position);fixture.transform.rotation=Quaternion.LookRotation(Vector3.ProjectOnPlane(player.transform.position-hit.position,Vector3.up));return true;
        }
        try
        {
            health.InvulnerableUntil=0;health.isShieldBlocking=false;
            Check(Place(false,1.2f),"Runner can be positioned in front for attack rejection");
            Call(fixture,"BeginAttack");Check(State(fixture)!="Attack","Runner refuses to start a frontal attack");
            Call(fixture,"DealHit");Check(health.currentHealth==savedHealth,"Runner cannot damage the warrior from the front");
            Check(Place(true,1.2f),"Runner can reach the rear attack position");
            Call(fixture,"BeginAttack");Check(State(fixture)=="Attack","Runner commits when positioned behind the warrior");
            player.transform.rotation=Quaternion.AngleAxis(180,Vector3.up)*savedRotation;
            Field(fixture,"stateTimer",fixture.Stats.windup+.01f);Call(fixture,"UpdateAttack");
            Check(State(fixture)!="Attack"&&health.currentHealth==savedHealth,"turning to face a winding-up Runner cancels its attack");
            player.transform.rotation=savedRotation;Place(true,1.2f);Call(fixture,"DealHit");
            Check(health.currentHealth<savedHealth,"Runner rear strikes still deal damage");
            Place(false,4);UnityEngine.Random.InitState(8193);
            int left=0,right=0,progressing=0;var goals=new HashSet<Vector3Int>();float minimum=float.MaxValue,maximum=0;
            for(int trial=0;trial<48;trial++)
            {
                Field(fixture,"runnerSide",0);Field(fixture,"runnerSideUntil",0f);Call(fixture,"ChooseRunnerRoute");
                Vector3 goal=(Vector3)enemyType.GetField("runnerGoal",flags).GetValue(fixture);
                Vector3 delta=goal-player.transform.position;float lateral=Vector3.Dot(delta,player.transform.right);
                if(lateral<-.5f)left++;if(lateral>.5f)right++;
                if(Vector3.Angle(Vector3.ProjectOnPlane(player.transform.forward,Vector3.up),Vector3.ProjectOnPlane(delta,Vector3.up))>35)progressing++;
                float radius=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude;minimum=Mathf.Min(minimum,radius);maximum=Mathf.Max(maximum,radius);
                goals.Add(Vector3Int.RoundToInt(goal*5));
            }
            Check(left>4&&right>4,$"Runner encounters choose either flank ({left} left / {right} right)");
            Check(goals.Count>16&&maximum-minimum>1,$"Runner routes vary their position and distance ({goals.Count} goals, {minimum:F1}–{maximum:F1} m)");
            Check(progressing>=36,$"Runner decisions advance around the flank instead of lingering in front ({progressing}/48)");
            Check(fixture.Stats.windup<=.24f&&fixture.Stats.cooldown<.8f,"Runner has a short committed strike and recovery cycle");
            Place(false,4);Field(fixture,"runnerSide",0);Field(fixture,"runnerDecisionAt",0f);Field(fixture,"runnerSideUntil",0f);
            health.InvulnerableUntil=float.PositiveInfinity;fixture.enabled=true;
            float capture=Time.captureDeltaTime,firstRear=-1,firstStrike=-1;
            try
            {
                Time.captureDeltaTime=1f/60;float began=Time.time;
                for(int frame=0;frame<300;frame++)
                {
                    if(firstRear<0&&EnemyController.InRunnerRearArc(fixture.transform.position,player.transform.position,player.transform.forward))firstRear=Time.time-began;
                    if(State(fixture)=="Attack"){firstStrike=Time.time-began;break;}
                    yield return null;
                }
            }
            finally{Time.captureDeltaTime=capture;fixture.enabled=false;agent.isStopped=true;}
            Check(firstRear>=0&&firstRear<4,$"Runner reaches the rear promptly from a frontal approach ({firstRear:F2} s)");
            Check(firstStrike>=0&&firstStrike<5,$"Runner follows the flank with a committed attack ({firstStrike:F2} s)");
            Place(true,1.2f);Call(fixture,"BeginAttack");Field(fixture,"stateTimer",.06f);Call(fixture,"UpdateAttack");
            Check(!agent.isStopped,"Runner closes distance during the early strike windup");
            Field(fixture,"hitDealt",true);Field(fixture,"stateTimer",fixture.Stats.windup+.181f);Call(fixture,"UpdateAttack");
            Check(State(fixture)=="Hunt"&&(float)enemyType.GetField("runnerWithdrawUntil",flags).GetValue(fixture)>Time.time,
                "Runner breaks away promptly after its strike instead of standing still");
        }
        finally
        {
            player.transform.rotation=savedRotation;health.currentHealth=savedHealth;health.InvulnerableUntil=savedInvulnerability;
            UnityEngine.Random.state=randomState;Destroy(fixture.gameObject);
        }
        yield return null;
    }

    IEnumerator PerformanceReview(PlayerControlSpeed player,MarchwardenVisual visual)
    {
        var armor=new List<double>();var cloth=new List<double>();var quiver=new List<double>();var frames=new List<double>();
        for(int frame=0;frame<240;frame++)
        {
            if(frame%60==0)player.DebugMove(frame%120==0?Vector2.up:Vector2.right,true);
            if(frame%45==0)player.DebugSwingVariant((frame/45)%12);
            lateCheck=()=>{armor.Add(visual.WaistArmor.LastStepMilliseconds);cloth.Add(visual.Linen.LastStepMilliseconds);quiver.Add(player.Gear.QuiverMotion.LastStepMilliseconds);frames.Add(Time.unscaledDeltaTime*1000);};
            yield return null;
        }
        player.DebugMove(null);
        string Stats(string name,List<double> values){values.Sort();return $"{name}: mean {values.Average():F3} ms, p95 {values[(int)(values.Count*.95)]:F3} ms";}
        string timing=Stats("armor",armor)+"\n"+Stats("cloth",cloth)+"\n"+Stats("quiver",quiver)+"\n"+Stats("frame",frames);
        Debug.Log("[PERFORMANCE]\n"+timing);File.WriteAllText(Path.Combine(shotDir,"performance.txt"),timing);
    }

    IEnumerator ReviewCloth(PlayerControlSpeed player,MarchwardenVisual visual,Rigidbody body,Vector3 position,Quaternion rotation)
    {
        float savedCapture=Time.captureDeltaTime;
        float worstGap=float.MaxValue,worstPin=0,largestMotion=0,lowestHem=float.MaxValue;
        bool valid=true;int shot=0;int resets=visual.Linen.ResetCount;
        float armorLeg=float.MaxValue,armorCloth=float.MaxValue,armorAnchor=0,quiverArm=float.MaxValue,quiverTorso=float.MaxValue,quiverOffset=0;
        try
        {
            Time.captureDeltaTime=1f/60;
            foreach(string action in new[]{"walk","sprint","turn","dodge","block","cut","thrust"})
            {
                float actionGap=float.MaxValue;string actionCapsule="";float actionHeight=0;
                float actionArmorLeg=float.MaxValue,actionArmorCloth=float.MaxValue,actionQuiverOffset=0,actionArmorAngle=0;
                int closestArmorJoint=-1;
                player.DebugMove(Vector2.zero);player.DebugBlock(false);
                if(action=="walk")player.DebugMove(Vector2.up);
                if(action=="sprint")player.DebugMove(Vector2.up,true);
                if(action=="turn")player.DebugMove(Vector2.right);
                if(action=="dodge")player.DebugDodge();
                if(action=="block")player.DebugBlock(true);
                if(action=="cut")player.DebugSwingVariant(0);
                if(action=="thrust")player.DebugSwingVariant(5);
                for(int frame=0;frame<42;frame++)
                {
                    int sample=frame;string caption=action;
                    lateCheck=()=>{
                        var state=visual.Linen.Inspect();valid&=state.finite;
                        if(sample>5){actionArmorLeg=Mathf.Min(actionArmorLeg,visual.WaistArmor.LegGap);
                            if(visual.WaistArmor.ClothGap<actionArmorCloth){actionArmorCloth=visual.WaistArmor.ClothGap;closestArmorJoint=visual.WaistArmor.ClosestClothJoint;}
                            actionQuiverOffset=Mathf.Max(actionQuiverOffset,player.Gear.QuiverMotion.SuspensionOffset);actionArmorAngle=Mathf.Max(actionArmorAngle,visual.WaistArmor.MaximumAngle);}
                        if(sample>5){armorLeg=Mathf.Min(armorLeg,visual.WaistArmor.LegGap);armorCloth=Mathf.Min(armorCloth,visual.WaistArmor.ClothGap);
                            armorAnchor=Mathf.Max(armorAnchor,visual.WaistArmor.AttachmentError);quiverArm=Mathf.Min(quiverArm,player.Gear.QuiverMotion.ArmGap);
                            quiverTorso=Mathf.Min(quiverTorso,player.Gear.QuiverMotion.TorsoGap);quiverOffset=Mathf.Max(quiverOffset,player.Gear.QuiverMotion.SuspensionOffset);}
                        if(sample>5&&state.minimumGap<actionGap){actionGap=state.minimumGap;actionCapsule=state.closestCapsule;actionHeight=state.penetrationRestHeight;}
                        if(sample>5){worstGap=Mathf.Min(worstGap,state.minimumGap);worstPin=Mathf.Max(worstPin,state.pinError);}
                        largestMotion=Mathf.Max(largestMotion,state.maximumDisplacement);lowestHem=Mathf.Min(lowestHem,state.minimumHeight);
                        if(sample%6==0)
                        {
                            CaptureFrom($"motion_cloth_{shot++:000}",player.transform.position+player.transform.forward*2.5f+player.transform.right*.95f+Vector3.up*.85f,
                                player.transform.position+Vector3.up*.65f);
                        }
                    };
                    yield return null;
                }
                Debug.Log($"[Linen stress] {action}: gap {actionGap:F5} at {actionCapsule}, rest height {actionHeight:F4}");
                Debug.Log($"[Structure stress] {action}: armor {actionArmorLeg:F4}/{actionArmorCloth:F4} m, cloth joint {closestArmorJoint}, hinge {actionArmorAngle:F1} deg, quiver offset {actionQuiverOffset:F3} m");
            }
        }
        finally
        {
            Time.captureDeltaTime=savedCapture;
            player.DebugMove(null);player.DebugBlock(false);
            body.position=position;body.rotation=rotation;body.linearVelocity=Vector3.zero;
        }
        Check(valid,"live cloth particles stay finite through locomotion, dodge, block, cuts and thrusts");
        Check(worstGap>=-.0015f,$"rendered tunic particles and triangle interiors clear leg and foot capsules through the stress sequence ({worstGap:F4} m)");
        Check(worstPin<.015f,$"cloth waist stays attached to the belt ({worstPin:F4} m)");
        Check(largestMotion>.012f&&largestMotion<.40f,$"cloth responds to momentum without runaway stretching ({largestMotion:F3} m)");
        Check(lowestHem>.22f,$"the lower tunic stays above foot level ({lowestHem:F3} m above the root)");
        Check(armorLeg>=.003f&&armorCloth>=.003f,$"rigid hip plates drape outside moving legs and simulated linen ({armorLeg:F4}/{armorCloth:F4} m)");
        Check(armorAnchor<.002f,$"armor hinges remain attached to the belt ({armorAnchor:F4} m)");
        Check(quiverArm>=.008f&&quiverTorso>=.008f,$"compact quiver and arrows clear right arm and torso through movement ({quiverArm:F4}/{quiverTorso:F4} m)");
        Check(quiverOffset<.16f,$"quiver contact response stays within its belt suspension ({quiverOffset:F3} m)");
        yield return Wait(.1f);
        Check(visual.Linen.ResetCount>resets,"cloth resets inertia safely after the test teleport");
    }

    IEnumerator ResolveBoons()
    {
        for (int i = 0; i < 4 && GameHUD.BoonsOpen; i++)
        {
            GameHUD.DebugPickBoon(0);
            yield return Wait(0.2f);
        }
    }

    void Finish(GameMode mode, Difficulty diff)
    {
        GameSettings.Mode = mode;
        GameSettings.Difficulty = diff;
        GameSettings.TestMode = false;
        Check(errors.Count == 0, $"no errors/exceptions logged ({errors.Count})");
        foreach (var e in errors.Distinct().Take(40)) report.AppendLine("  " + e);
        string result = failures == 0 ? "PASS" : $"FAIL ({failures})";
        report.Insert(0, result + "\n");
        Directory.CreateDirectory(shotDir);
        File.WriteAllText(Path.Combine(shotDir, "report.txt"), report.ToString());
        Debug.Log("[SMOKE] REPORT\n" + report);
        File.WriteAllText(Path.Combine(Application.dataPath, "..", "Temp", "mw_smoke_done.txt"), result);
    }

    static Transform Bone(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root) { var f = Bone(c, name); if (f != null) return f; }
        return null;
    }

    // A separate camera, so the gameplay camera (and therefore the aim) is left alone.
    void CaptureFrom(string name, Vector3 position, Vector3 lookAt)
    {
        var go = new GameObject("Capture Camera");
        var cam = go.AddComponent<Camera>();
        cam.allowMSAA = true;
        cam.renderingPath = RenderingPath.Forward;
        cam.fieldOfView = 40f;
        cam.nearClipPlane = 0.05f;
        go.transform.position = position;
        go.transform.LookAt(lookAt);
        // Close-ups must be readable enough to review materials and silhouette.
        var hidden = new List<Renderer>();
        if (name.StartsWith("10b_") || name.StartsWith("10c_") || name.StartsWith("14") || name.StartsWith("motion_"))
            foreach (var enemy in EnemyController.All)
                if (enemy != null)
                    foreach (var renderer in enemy.GetComponentsInChildren<Renderer>())
                        if (renderer.enabled) { hidden.Add(renderer); renderer.enabled = false; }
        int width=name.StartsWith("motion_")?640:1920, height=name.StartsWith("motion_")?640:1080;
        var rt = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB) { antiAliasing = 4 };
        cam.targetTexture = rt;
        cam.Render();
        foreach (var renderer in hidden) if (renderer != null) renderer.enabled = true;
        RenderTexture.active = rt;
        var tex = new Texture2D(width,height,TextureFormat.RGB24,false);
        tex.ReadPixels(new Rect(0,0,width,height),0,0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = null;
        rt.Release();
        Destroy(go);
        Directory.CreateDirectory(shotDir);
        File.WriteAllBytes(Path.Combine(shotDir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }

    // Screen-space overlay UI isn't drawn into camera renders, so temporarily move canvases onto the camera.
    void Capture(string name)
    {
        var cam = Camera.main;
        if (cam == null) return;
        var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None).Where(c => c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay && c.gameObject.activeInHierarchy).ToList();
        var rt = new RenderTexture(1600, 900, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var oldTarget = cam.targetTexture;
        cam.targetTexture = rt;
        foreach (var c in canvases)
        {
            c.renderMode = RenderMode.ScreenSpaceCamera;
            c.worldCamera = cam;
            c.planeDistance = cam.nearClipPlane + 0.05f;
        }
        Canvas.ForceUpdateCanvases();
        var preview = FindFirstObjectByType<MenuPreview>();
        if (preview != null && preview.previewCamera != null && preview.previewCamera.enabled) preview.previewCamera.Render();
        cam.Render();
        RenderTexture.active = rt;
        var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
        tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
        tex.Apply();
        RenderTexture.active = null;
        cam.targetTexture = oldTarget;
        foreach (var c in canvases) { c.renderMode = RenderMode.ScreenSpaceOverlay; c.worldCamera = null; }
        rt.Release();
        Directory.CreateDirectory(shotDir);
        File.WriteAllBytes(Path.Combine(shotDir, name + ".png"), tex.EncodeToPNG());
        Destroy(tex);
    }
}
#endif
