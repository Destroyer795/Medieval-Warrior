#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;

public partial class SmokeTestDriver
{
    IEnumerator SiegeBattle()
    {
        GameSettings.Mode = GameMode.DefendTheCastle;
        BattleLoading.EnterBattle();
        float deadline = Time.realtimeSinceStartup + 40;
        while (Time.realtimeSinceStartup < deadline && (BattleLoading.IsTransitioning ||
               GameManager.instance == null || GameManager.instance.State != GameManager.MatchState.Playing))
            yield return null;
    }

    IEnumerator ReviewSiege()
    {
        yield return SiegeBattle();
        var gm = GameManager.instance;
        var gate = gm != null ? gm.Gate : null;
        Check(gate != null && gate.MaxHealth == 1000, "Knight siege starts with a physical 1,000 HP gate");
        if (gate == null) yield break;
        var player = FindFirstObjectByType<PlayerControlSpeed>();
        var health = player.GetComponent<PlayerHealth>();
        health.InvulnerableUntil = float.PositiveInfinity;
        var body = player.GetComponent<Rigidbody>();
        yield return ReviewOuterGate(gm, player, body);
        gm.enabled = false;
        foreach (var enemy in EnemyController.All.ToArray()) Destroy(enemy.gameObject);
        yield return null;
        if (gm.Outer != null)
        {
            yield return WalkForegate(gm.Outer.Structure, player, body);
            yield return ShoveAcrossRoadShoulder(gm.Outer.Structure, player, body);
        }
        body.position = gate.Road;
        player.transform.position = gate.Road;
        Physics.SyncTransforms();
        body.isKinematic = true;
        bool Route(Vector3 from, Vector3 to)
        {
            var path = new NavMeshPath();
            return NavMesh.SamplePosition(to, out var end, 3f, NavMesh.AllAreas) &&
                NavMesh.CalculatePath(from, end.position, NavMesh.AllAreas, path) && path.status == NavMeshPathStatus.PathComplete;
        }
        Check(Route(gate.Road, gate.Approach) && Route(gate.Road, player.transform.position) &&
            gate.Lanes.Count >= 3 && gate.Lanes.All(p => Route(p, gate.Approach) && Route(p, player.transform.position)),
            "road formation and village lanes have complete paths to both Keep and knight");
        var braziers = gate.GetComponentsInChildren<FireBrazier>();
        var barrels = gate.GetComponentsInChildren<ExplosiveBarrel>();
        Check(braziers.Length == 2 && barrels.Length >= 3, "both gate braziers and distributed pitch barrels are present");
        Check(gate.GetComponent<BoxCollider>().enabled, "sanctuary door has a physical hit surface");

        EnemyController Deploy(ZombieType type, Vector3 pos)
        {
            var go = Instantiate(BattleLoading.ZombiePrefab, pos, Quaternion.LookRotation(-gate.Outward));
            var e = go.GetComponent<EnemyController>();
            e.Init(type, false, false);
            return e;
        }
        player.transform.position = gate.Road;
        var breacher = Deploy(ZombieType.Walker, gate.Approach);
        float before = gate.Health;
        float deadline = Time.realtimeSinceStartup + 6;
        while (gate.Health == before && Time.realtimeSinceStartup < deadline) yield return null;
        Check(gate.Health < before, "a Walker lands an animated gate strike while the knight is at the road");
        Check(FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None)
            .Any(label => label.text == "THE KEEP GATE IS UNDER ATTACK!"), "the gate strike displays the priority warning");
        var voices = FindFirstObjectByType<AudioManager>().GetComponentsInChildren<AudioSource>();
        Check(voices.Any(v => v.isPlaying && v.clip != null && v.clip.name == "keep_impact") &&
            voices.Any(v => v.isPlaying && v.clip != null && v.clip.name == "shield_block"),
            "the gate impact and warning clang play on separate voices");
        CaptureFrom("siege_gate", gate.Approach + gate.Outward * 9 + Vector3.up * 3, gate.transform.position + Vector3.up * 1.8f);
        body.position = gate.Approach + gate.Outward * 1.25f;
        player.transform.position = body.position;
        Physics.SyncTransforms();
        float nearbyHealth = gate.Health;
        breacher.ReceiveWeaponHit(.25f, gate.Outward, 0, breacher.transform.position + Vector3.up);
        deadline = Time.realtimeSinceStartup + 8;
        while (gate.Health >= nearbyHealth && Time.realtimeSinceStartup < deadline) yield return null;
        Check(gate.Health < nearbyHealth && Vector3.Distance(breacher.transform.position, gate.Approach) < .75f,
            "a Walker committed to the Keep continues battering it when struck by the nearby knight");
        Destroy(breacher.gameObject);
        body.position = gate.Road;
        player.transform.position = gate.Road;
        Physics.SyncTransforms();

        var spitter = Deploy(ZombieType.Spitter, gate.Approach + gate.Outward * 4);
        var battery = gate.Battery(spitter.GetInstanceID());
        Check(Route(gate.Road, battery) && Vector3.Distance(battery, gate.Approach) > 7, "Spitter has a reachable firing position away from the steps");
        spitter.GetComponent<NavMeshAgent>().Warp(battery);
        deadline = Time.realtimeSinceStartup + 7;
        while (!FindObjectsByType<AcidPool>(FindObjectsSortMode.None).Any(p =>
            (p.transform.position - gate.Approach).sqrMagnitude < 64) && Time.realtimeSinceStartup < deadline) yield return null;
        Check(FindObjectsByType<AcidPool>(FindObjectsSortMode.None).Any(p => (p.transform.position - gate.Approach).sqrMagnitude < 64),
            "siege artillery leaves real acid on the Keep approach");
        Destroy(spitter.gameObject);

        if (braziers.Length > 0)
        {
            player.Equip(Weapon.Bow);
            player.transform.position = braziers[0].transform.position + gate.Outward;
            player.DebugHoldDraw(.55f);
            Check(player.BurningNock, "a partial draw at a brazier ignites the nocked arrow");
            player.DebugFireAt(player.transform.position + Vector3.up * 12, .55f);
            Check(FindObjectsByType<Arrow>(FindObjectsSortMode.None).Any(a => a.isFire && a.IsFlaming), "brazier fire carries onto the released arrow");
            player.DebugHoldDraw(0);
            player.transform.position = gate.Road;
            player.DebugHoldDraw(.55f);
            Check(!player.BurningNock, "the next draw away from the brazier starts unlit");
            player.DebugHoldDraw(0);
        }
        if (barrels.Length >= 2)
        {
            var barrel = barrels[0];
            var pos = barrel.transform.position;
            Vector3 dir = (gate.Approach - pos).normalized;
            var brute = Deploy(ZombieType.Brute, pos + dir * 1.2f);
            var bolt = Arrow.Fire(pos - dir * 2.3f + Vector3.up * .6f, dir, 35, 2, 0, player.transform);
            yield return Wait(.45f);
            Check(barrel == null && (brute.IsDead || brute.Health < brute.MaxHealth), "a flying arrow detonates pitch and damages nearby undead");
            if (bolt != null) Destroy(bolt.gameObject);
            Destroy(brute.gameObject);
            var second = barrels[1];
            player.Equip(Weapon.Sword);
            player.transform.SetPositionAndRotation(second.transform.position + gate.Outward * 1.3f, Quaternion.LookRotation(-gate.Outward));
            Physics.SyncTransforms();
            player.DebugSwingStep(0);
            yield return Wait(.85f);
            Check(second == null, "a timed sword stroke detonates a second pitch barrel");
        }
        // Set a stationary shield line on the walkable apron, facing downhill.
        player.Equip(Weapon.Sword);
        var footing = gate.Approach + gate.Outward * 4;
        gate.TryReach(gate.Approach, footing, 2f, out footing);
        player.transform.SetPositionAndRotation(footing, Quaternion.LookRotation(gate.Outward));
        player.DebugBlock(true);
        yield return null;
        var blockers = new EnemyController[2];
        for (int i = 0; i < blockers.Length; i++)
        {
            var point = footing + gate.Outward * 1.25f + gate.transform.right * (i == 0 ? -.5f : .5f);
            gate.TryReach(footing, point, 2f, out point);
            blockers[i] = Deploy(ZombieType.Walker, point);
        }
        yield return Wait(.45f);
        Check(blockers.All(e => Vector3.Dot(e.transform.position - footing, gate.Outward) > .55f),
            "both breachers stop in front of the raised shield");
        var beforeBash = blockers.Select(e => Vector3.Dot(e.transform.position - footing, gate.Outward)).ToArray();
        typeof(PlayerControlSpeed).GetMethod("ShieldBash", System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic).Invoke(player, null);
        Check(blockers.All(e => e.IsStaggered), "one shield bash staggers both enemies in its forward cone");
        yield return Wait(.3f);
        Check(blockers.Select((e,i) => Vector3.Dot(e.transform.position - footing, gate.Outward) - beforeBash[i]).All(d => d > .3f),
            "braced bash physically drives both attackers back on the NavMesh");
        player.DebugBlock(false);
        gate.Strike(gate.Health);
        Check(gm.State == GameManager.MatchState.Lost && gate.Health == 0 && !health.IsDead, "a breached gate loses the match with the knight alive");
        gm.enabled = true;
        var difficulty = GameSettings.Difficulty;
        GameSettings.Difficulty = Difficulty.Warlord;
        yield return SiegeBattle();
        gm = GameManager.instance;
        Check(gm.Gate != null && gm.Gate.MaxHealth == 850f, "Warlord difficulty scales the sanctuary to 850 HP");
        GameSettings.Difficulty = difficulty;
        health = FindFirstObjectByType<PlayerHealth>();
        health.TakeDamage(9999f, null, 0, true);
        yield return Wait(3);
        Check(gm.State == GameManager.MatchState.Lost && gm.Gate != null && gm.Gate.Health > 0,
            "the knight's death loses the siege with the sanctuary intact");
        gm.QuitGame();
        yield return Wait(2);
    }

    IEnumerator ShoveAcrossRoadShoulder(ForegateStructure wall, PlayerControlSpeed knight, Rigidbody body)
    {
        var terrain = Terrain.activeTerrain;
        float Surface(Vector3 at) => terrain.SampleHeight(at) + terrain.transform.position.y;
        bool swept = body.collisionDetectionMode == CollisionDetectionMode.ContinuousDynamic;
        float worstClearance = float.PositiveInfinity;
        foreach (int side in new[] { -1, 1 })
        {
            var point = wall.World(side * 1.7f, 0f, 9f);
            point.y = Surface(point) + .06f;
            body.position = point;
            knight.Stun(.35f);
            body.linearVelocity = wall.transform.right * side * 14f + Vector3.down * 8f;
            for (int step = 0; step < 14; step++)
            {
                yield return new WaitForFixedUpdate();
                worstClearance = Mathf.Min(worstClearance, body.position.y - Surface(body.position));
            }
        }
        Check(swept && worstClearance >= -.35f, $"a downward and outward shove keeps the knight above both road shoulders ({worstClearance:F2} m, {body.collisionDetectionMode})");
        yield return Wait(.12f);
    }

    IEnumerator WalkForegate(ForegateStructure wall, PlayerControlSpeed knight, Rigidbody body)
    {
        var terrain = Terrain.activeTerrain;
        Vector3 Foot(int side)
        {
            var point = wall.World(side * 8.43f, 0, 12);
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            return point;
        }
        var route = new[]
        {
            Foot(1), wall.World(8.43f, 6.23f, .6f), wall.World(0, 6.845f, .6f),
            wall.World(-8.43f, 6.23f, .6f), Foot(-1)
        };
        var cameraRig = Camera.main.GetComponent<CameraFollow>();
        float savedDelta = Time.captureDeltaTime;
        try
        {
            Time.captureDeltaTime = Time.fixedDeltaTime;
            body.position = route[0] + Vector3.up * .08f;
            body.linearVelocity = Vector3.zero;
            Physics.SyncTransforms();
            for (int i = 0; i < 5; i++) yield return null;
            foreach (int direction in new[] { 1, -1 })
            {
                for (int leg = 1; leg < route.Length; leg++)
                {
                    var target = route[direction > 0 ? leg : route.Length - 1 - leg];
                    var delta = Vector3.ProjectOnPlane(target - body.position, Vector3.up);
                    float deadline = Time.time + delta.magnitude / knight.walkSpeed + 2f;
                    float lowestVelocity = 0;
                    while (delta.magnitude > .16f && Time.time < deadline)
                    {
                        var forward = Vector3.ProjectOnPlane(cameraRig.LookDirection, Vector3.up).normalized;
                        var right = Vector3.Cross(Vector3.up, forward);
                        knight.DebugMove(new Vector2(Vector3.Dot(delta.normalized, right), Vector3.Dot(delta.normalized, forward)));
                        yield return null;
                        delta = Vector3.ProjectOnPlane(target - body.position, Vector3.up);
                        lowestVelocity = Mathf.Min(lowestVelocity, body.linearVelocity.y);
                    }
                    bool walked = delta.magnitude <= .16f && Mathf.Abs(body.position.y - target.y) < .4f && lowestVelocity > -5;
                    Check(walked, $"knight walks foregate direction {direction}, leg {leg} " +
                        $"({delta.magnitude:F2} m left, feet at {wall.transform.InverseTransformPoint(body.position):F2}, lowest Y speed {lowestVelocity:F2})");
                    if (!walked) yield break;
                }
            }
        }
        finally
        {
            knight.DebugMove(null);
            Time.captureDeltaTime = savedDelta;
        }
    }

    IEnumerator ReviewOuterGate(GameManager gm, PlayerControlSpeed knight, Rigidbody body)
    {
        var outer = gm.Outer;
        Check(outer != null && outer.Structure != null, "Defend loads the outer gatehouse");
        if (outer == null || outer.Structure == null) yield break;
        var wall = outer.Structure;
        Check(outer.StonesRemaining == 5, "the central cradle has five stones for the defense");
        Check(Vector3.Angle(-wall.Outward, Vector3.ProjectOnPlane(gm.Gate.Approach - wall.transform.position, Vector3.up)) < 2f, "breached gate faces the inner sanctuary");
        float Depth(Vector3 point) => Vector3.Dot(wall.World(0, 0, 0) - point, wall.Outward);
        void Place(Vector3 at)
        {
            body.position = at;
            body.linearVelocity = Vector3.zero;
            knight.transform.position = at;
            Physics.SyncTransforms();
        }
        var start = body.position;
        Check(EnemyController.All.All(e => e == null || Depth(e.transform.position) < -25f),
            "the first assault starts deep in the exterior field");
        Check(start.y > wall.World(0, 4.8f, 0).y && knight.CurrentWeapon == Weapon.Bow,
            "knight begins on the parapet equipped for ranged defense");
        Capture("outer_gate_parapet");
        CaptureFrom("outer_gate_front", wall.World(18, 14, -32), wall.World(0, 3.7f, 1));
        CaptureFrom("outer_gate_courtyard", wall.World(-9, 18, 20), wall.World(1, 3.4f, 2));
        // Reproduce the no-input backstep at the armory, then approach the lever
        // from each stockpile while pushing diagonally toward the courtyard.
        var spawnFacing = body.rotation;
        var dodge = typeof(PlayerControlSpeed).GetMethod("TryDodge",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        float previousCaptureDelta = Time.captureDeltaTime;
        Time.captureDeltaTime = Time.fixedDeltaTime;
        try
        {
            dodge.Invoke(knight, new object[] { Vector3.zero });
            for (int i = 0; i < 44; i++) yield return null;
            Vector3 afterDodge = wall.transform.InverseTransformPoint(body.position);
            Check(afterDodge.y > 6.05f && -afterDodge.z < 1.7f,
                $"stationary dodge at the armory stays on the wall walk (feet {afterDodge:F2})");
            var cameraRig = Camera.main.GetComponent<CameraFollow>();
            foreach (int side in new[] { -1, 1 })
            {
                Place(wall.World(side * 3.82f, 6.88f, 2.02f));
                var heading = (wall.transform.right * -side - wall.Outward * .50f).normalized;
                float lowest = 10;
                for (int frame = 0; frame < 145; frame++)
                {
                    var look = Vector3.ProjectOnPlane(cameraRig.LookDirection, Vector3.up).normalized;
                    knight.DebugMove(new Vector2(Vector3.Dot(heading, Vector3.Cross(Vector3.up, look)), Vector3.Dot(heading, look)));
                    yield return null;
                    var feet = wall.transform.InverseTransformPoint(body.position);
                    lowest = Mathf.Min(lowest, feet.y);
                    if ((feet.x + .9f) * side <= 0) break;
                }
                knight.DebugMove(null);
                var finish = wall.transform.InverseTransformPoint(body.position);
                Check(lowest > 6.65f && Mathf.Abs(finish.x + .9f) < .25f && outer.InteractionPrompt.Contains("Drop stone"),
                    $"walk from stockpile {side} along the courtyard edge reaches the usable lever (feet {finish:F2}, minimum height {lowest:F2}, velocity {body.linearVelocity:F2}, prompt '{outer.InteractionPrompt}')");
            }
        }
        finally
        {
            knight.DebugMove(null);
            Time.captureDeltaTime = previousCaptureDelta;
            Place(start);
            body.rotation = spawnFacing;
            knight.transform.rotation = spawnFacing;
        }
        bool SupportsPlanks(float x, float inward) =>
            Physics.Raycast(wall.World(x, 7.4f, inward), Vector3.down, out var floor, .7f, ~0, QueryTriggerInteraction.Ignore) &&
            Mathf.Abs(floor.point.y - wall.World(x, 6.845f, inward).y) < .025f;
        Check(SupportsPlanks(0, 2.14f) && SupportsPlanks(-1.44f, -.207f) && SupportsPlanks(1.44f, -.207f) &&
            SupportsPlanks(0, -.077f), "both walkways, the plank crossing seam and rear lip support the knight");
        Check(!Physics.Raycast(wall.World(0, 7.0f, -1.43f), Vector3.down, 1.8f, ~0, QueryTriggerInteraction.Ignore),
            "the central stone chute remains open");
        Place(wall.World(-.1f, 6.89f, .75f));
        var forward = Vector3.ProjectOnPlane(Camera.main.GetComponent<CameraFollow>().LookDirection, Vector3.up).normalized;
        var right = Vector3.Cross(Vector3.up, forward);
        knight.DebugMove(new Vector2(Vector3.Dot(wall.Outward, right), Vector3.Dot(wall.Outward, forward)), true);
        yield return Wait(.7f);
        knight.DebugMove(null);
        Check(Depth(body.position) >= -.18f && Mathf.Abs(body.position.y - wall.World(0, 6.845f, 0).y) < .3f,
            "the fixed rear curb keeps a sprinting knight on the boards outside the drop channel");
        Place(start);
        foreach (int side in new[] { -1, 1 })
        {
            var mid = wall.World(0, 4.7f, 12f);
            var direction = (wall.World(side * 16, 4.7f, 12f) - mid).normalized;
            Check(Physics.Raycast(mid, direction, out var hit, 17f, ~0, QueryTriggerInteraction.Ignore) &&
                hit.distance > 9f, "protected stair yard meets a solid return wall on side " + side);
            float lastHeight = float.PositiveInfinity;
            bool clearFlight = true;
            for (float inward = 2f; inward <= 12f; inward += .5f)
            {
                var top = wall.World(side * 8.43f, 10f, inward);
                if (!Physics.Raycast(top, Vector3.down, out var step, 16f, ~0, QueryTriggerInteraction.Ignore) ||
                    step.point.y > lastHeight + .2f)
                {
                    clearFlight = false;
                    break;
                }
                lastHeight = step.point.y;
                if (Physics.CheckCapsule(step.point + Vector3.up * .49f,
                    step.point + Vector3.up * 1.6f, .30f, ~0, QueryTriggerInteraction.Ignore))
                    clearFlight = false;
            }
            Check(clearFlight && Mathf.Abs(lastHeight - (Terrain.activeTerrain.SampleHeight(wall.World(side * 8.43f, 0, 12)) + Terrain.activeTerrain.transform.position.y)) < .4f,
                "a knight-width stair flight descends into the courtyard on side " + side);
            Place(wall.World(side * 11.3f, 6.35f, 1.1f));
            knight.DebugFireAt(body.position + Vector3.up * 20, .5f);
            Check(knight.Arrows < knight.maxArrows && outer.Interact() && knight.Arrows == knight.maxArrows,
                "wall armory refills the quiver on side " + side);
        }
        Place(start);
        float deadline = Time.realtimeSinceStartup + 35f;
        while (outer.Health == outer.MaxHealth && Time.realtimeSinceStartup < deadline) yield return null;
        Check(outer.Health < outer.MaxHealth && !outer.Breached && gm.Gate.Health == gm.Gate.MaxHealth,
            "live road attackers batter only the outer leaves while the keep is protected");
        var underGallery = EnemyController.All.Where(e => e != null && !e.IsDead &&
            Vector3.Distance(e.transform.position, wall.World(0f, 0f, -3.55f)) < 3f).ToArray();
        var shotOrigin = wall.World(1.42f, 8.15f, -2.58f);
        var shotTarget = wall.World(.45f, 0f, -3.9f);
        shotTarget.y = outer.GroundHeight(shotTarget) + 1.38f;
        Check(!Physics.Linecast(shotOrigin, shotTarget, out var block, ~0, QueryTriggerInteraction.Ignore) ||
            block.collider.GetComponentInParent<EnemyController>() != null,
            "an arrow has a clear line from the gallery to the front rank" +
            (block.collider != null ? $" (hit {block.collider.name})" : ""));
        CaptureFrom("outer_gate_firing_lane", shotOrigin + Vector3.up * 1.6f, shotTarget);
        Place(wall.World(-.75f, 6.95f, .24f));
        Check(outer.StoneReady && outer.Interact() && !outer.StoneReady,
            "releasing the loaded stone consumes the ready shot");
        Check(!outer.Interact(), "the cradle cannot drop a second stone while reloading");
        yield return Wait(1.35f);
        Check(underGallery.Any(e => e == null || e.IsDead || e.Health < e.MaxHealth),
            "the stone strikes the enemies gathered beneath the gallery");
        yield return Wait(1.4f);
        Check(outer.StonesRemaining == 4 && outer.StoneReady && wall.ReserveStones == 3,
            "the cradle automatically loads the next stone from the visible stock");
        bool allReleases = true;
        for (int remaining = 4; remaining > 0; --remaining)
        {
            allReleases &= outer.StonesRemaining == remaining && wall.ReadyStone.gameObject.activeSelf &&
                outer.Interact() && !outer.Interact();
            float reloadDeadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < reloadDeadline &&
                (remaining == 1 ? wall.ReadyStone.gameObject.activeSelf : !outer.StoneReady || !wall.ReadyStone.gameObject.activeSelf))
            {
                if (GameHUD.BoonsOpen) GameHUD.DebugPickBoon(0);
                yield return null;
            }
        }
        Check(allReleases && outer.StonesRemaining == 0 && wall.ReserveStones == 0 &&
            !wall.ReadyStone.gameObject.activeSelf && !outer.Interact() && outer.InteractionPrompt.Contains("empty"),
            "one lever releases all five stones, rejects repeat presses during reload and shows the empty cradle");
        Place(start);
        var clockField = typeof(OuterSiege).GetField("<HoldSeconds>k__BackingField",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        int beforePush = EnemyController.All.Count;
        clockField.SetValue(outer, OuterSiege.FinalAssaultAt - .05f);
        deadline = Time.realtimeSinceStartup + 3f;
        while (EnemyController.All.Count < beforePush + 10 && Time.realtimeSinceStartup < deadline)
        {
            if (GameHUD.BoonsOpen) GameHUD.DebugPickBoon(0);
            yield return null;
        }
        Check(outer.FinalAssault && EnemyController.All.Count >= beforePush + 10,
            "the timer launches a large final assault");
        float oldHold = outer.HoldSeconds;
        clockField.SetValue(outer, OuterSiege.OuterDuration);
        int atDeadline = EnemyController.All.Count;
        yield return Wait(.9f);
        Check(outer.ReinforcementsStopped && gm.State == GameManager.MatchState.Playing && atDeadline > 0 &&
            EnemyController.All.Count == atDeadline && gm.Objective.Contains("Clear the survivors"),
            "the outer deadline stops reinforcements while surviving attackers keep the match live");
        clockField.SetValue(outer, oldHold);
        // The clock was advanced without the preceding 90 seconds of
        // structural damage. Stage a weakened gate and let a live hit finish it.
        outer.TakeDamage(Mathf.Max(0f, outer.Health - 6f));
        deadline = Time.realtimeSinceStartup + 37f;
        var attackers = new System.Collections.Generic.HashSet<EnemyController>();
        while (Time.realtimeSinceStartup < deadline && !outer.Breached)
        {
            foreach (var enemy in EnemyController.All)
                if (enemy != null && !enemy.IsDead && Depth(enemy.transform.position) < 0f) attackers.Add(enemy);
            if (GameHUD.BoonsOpen) GameHUD.DebugPickBoon(0);
            yield return null;
        }
        Check(outer.Breached && outer.Cinematic && gm.State == GameManager.MatchState.Playing,
            "breaking the outer gate starts a shot and keeps the siege live");
        float began = Time.time;
        while (outer.Cinematic && Time.time - began < .62f) yield return null;
        if (outer.Cinematic) Capture("outer_gate_breach");
        deadline = Time.realtimeSinceStartup + 4;
        while (outer.Cinematic && Time.realtimeSinceStartup < deadline) yield return null;
        Check(!outer.Cinematic && Time.time - began >= .85f && Time.time - began < 1.5f,
            "the breach shot hands control back after one second");
        Check(knight.enabled && gm.Gate.Health == gm.Gate.MaxHealth, "defense resumes with the knight and keep intact");
        var terrain = Terrain.activeTerrain;
        float length = Depth(gm.Gate.Approach);
        float steepest = 0;
        for (float depth = 1; depth < Mathf.Min(66, length); depth += .5f)
        {
            float a = terrain.SampleHeight(wall.World(0,0,depth));
            float b = terrain.SampleHeight(wall.World(0,0,depth+.5f));
            steepest = Mathf.Max(steepest, Mathf.Abs(b-a)/.5f);
        }
        Check(steepest < .25f, $"the cobbled road has a continuous walkable grade (maximum {steepest:P0})");
        Collider obstacle = null;
        for (float depth = 2; depth < length - 1 && obstacle == null; depth += .4f)
        {
            var centre = wall.World(0f, 0f, depth);
            centre.y = terrain.SampleHeight(centre) + terrain.transform.position.y;
            obstacle = Physics.OverlapCapsule(centre + Vector3.up * .6f, centre + Vector3.up * 1.6f,
                .38f, ~0, QueryTriggerInteraction.Ignore).FirstOrDefault(c => !(c is TerrainCollider) &&
                c.GetComponentInParent<EnemyController>() == null && c.GetComponentInParent<PlayerControlSpeed>() == null);
        }
        Check(obstacle == null, "the entire central lane to the sanctuary is physically clear" +
            (obstacle != null ? $" (blocked by {obstacle.name} at {obstacle.bounds.center})" : ""));
        CaptureFrom("outer_gate_open_route", wall.World(0, 2.1f, -4f),
            gm.Gate.transform.position + Vector3.up * 1.8f);
        deadline = Time.realtimeSinceStartup + 12;
        while (Time.realtimeSinceStartup < deadline && !attackers.Any(e => e != null && Depth(e.transform.position) > 2f))
            yield return null;
        Check(attackers.Any(e => e != null && Depth(e.transform.position) > 2f),
            "a surviving exterior attacker crosses the doorway into the protected yard");
        deadline = Time.realtimeSinceStartup + 14;
        bool Rejoined(EnemyController e) => e != null && !e.IsDead && e.TryGetComponent<NavMeshAgent>(out var agent) &&
            agent.enabled && agent.isOnNavMesh && Depth(e.transform.position) > 14;
        while (Time.realtimeSinceStartup < deadline && !attackers.Any(Rejoined)) yield return null;
        Check(attackers.Any(Rejoined), "the same assault reaches the connected village NavMesh after the breach");
    }
}
#endif
