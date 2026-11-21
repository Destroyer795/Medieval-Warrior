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
        body.isKinematic = true;
        gm.enabled = false;
        foreach (var enemy in EnemyController.All.ToArray()) Destroy(enemy.gameObject);
        yield return null;
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
        breacher.ReceiveWeaponHit(.25f, gate.Outward, 0, breacher.transform.position + Vector3.up);
        yield return Wait(.65f);
        var agent = breacher.GetComponent<NavMeshAgent>();
        Check((agent.destination - player.transform.position).sqrMagnitude < 4, "weapon contact diverts a breacher to the knight");
        deadline = Time.realtimeSinceStartup + 8;
        while ((gate.Health >= before - breacher.Stats.damage) && Time.realtimeSinceStartup < deadline) yield return null;
        Check(gate.Health < before - breacher.Stats.damage, "the distracted Walker returns and strikes the gate again");
        Destroy(breacher.gameObject);

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
}
#endif
