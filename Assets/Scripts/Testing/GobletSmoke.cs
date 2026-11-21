#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

public partial class SmokeTestDriver
{
    IEnumerator ReviewGoblet(PlayerControlSpeed knight)
    {
        var gm = GameManager.instance;
        var director = gm.Horde;
        var body = knight.GetComponent<Rigidbody>();
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var wave = typeof(GameManager).GetMethod("StartWave", flags);
        wave.Invoke(gm, new object[] { 14 });
        yield return null;
        var goblet = director.Goblet;
        Check(goblet != null, "an unclaimed goblet appears at a castle wall during an ordinary wave");
        if (goblet == null) yield break;

        var path = new NavMeshPath();
        Check(NavMesh.CalculatePath(knight.transform.position, goblet.Approach, NavMesh.AllAreas, path) &&
            path.status == NavMeshPathStatus.PathComplete, "the goblet has a walkable approach from the knight");
        var gate = GameObject.Find("Environment/Siege Road Gate");
        bool wallShelf = Physics.Raycast(goblet.Target, -goblet.transform.forward, out var hit, 1.5f, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.name.StartsWith("Wall");
        Check(wallShelf || (gate != null && Vector3.Distance(gate.transform.position, goblet.Approach) < 17f),
            "the goblet stands by the physical wall or its road gate");
        var saved = body.position;
        var savedRotation = knight.transform.rotation;
        GameObject fixture = null;
        try
        {
            void Place(Vector3 at)
            {
                knight.transform.position = body.position = at;
                Physics.SyncTransforms();
            }
            Place(goblet.Approach + goblet.transform.forward * 6);
            Check(!director.TryClaimGoblet(), "the knight cannot drink from a distance");
            CaptureFrom("horde_05_wall_goblet", goblet.Target + goblet.transform.forward * 2.3f +
                goblet.transform.right * .65f + Vector3.up * .7f, goblet.Target - Vector3.up * .2f);
            Place(goblet.Approach);
            knight.transform.rotation = Quaternion.LookRotation(-goblet.transform.forward);
            yield return null;
            Check(!director.GobletActive && director.CanClaimGoblet, "reaching the shelf offers the choice without consuming it");
            var hud = FindFirstObjectByType<GameHUD>();
            Check(hud.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text.Contains("Drink") && t.text.Contains("goblet")),
                "the approach displays the drink control");
            Capture("horde_06_goblet_offer");
            Check(director.TryClaimGoblet() && !director.TryClaimGoblet(), "one deliberate claim consumes the goblet only once");
            Check(director.SpeedMultiplier == 2 && gm.AmbientSpawnRateMultiplier == 2 && gm.GobletScoreMultiplier == 2,
                "the goblet doubles movement, spawn cadence and kill score");
            var timer = typeof(GameManager).GetField("spawnTimer", flags);
            timer.SetValue(gm, 5f);
            typeof(GameManager).GetMethod("UpdateMode", flags).Invoke(gm, null);
            Check(Mathf.Abs((float)timer.GetValue(gm) - (5f - Time.deltaTime * 2)) < .0001f,
                "ordinary wave spawns consume their countdown at double speed");

            fixture = Instantiate(BattleLoading.ZombiePrefab, saved, Quaternion.identity);
            var enemy = fixture.GetComponent<EnemyController>();
            enemy.Init(ZombieType.Walker, false, false);
            director.Activate(HordeEvent.BloodMoon);
            Check(Mathf.Approximately(director.SpeedMultiplier, 2.5f), "Blood Moon stacks with the goblet speed bonus");
            int before = gm.Score;
            enemy.TakeDamage(999, Vector3.forward, 0);
            Check(gm.Score - before == 2 * enemy.Stats.points * gm.ComboMultiplier * director.ScoreMultiplier,
                "a real goblet kill doubles the kill score including Blood Moon and combo bonuses");
            typeof(GameManager).GetMethod("WaveCleared", flags).Invoke(gm, null);
            Check(gm.RecoveryRemaining > 0 && gm.AmbientSpawnRateMultiplier < 1,
                "finishing a goblet wave eases the next ordinary spawn interval");
            if (gm.ChoosingBoon) GameHUD.DebugPickBoon(0);
            wave.Invoke(gm, new object[] { 16 });
            Check(director.Goblet == null && !director.GobletActive && director.SpeedMultiplier == 1 && gm.GobletScoreMultiplier == 1,
                "subsequent waves lose the bargain and cannot offer a second goblet");
        }
        finally
        {
            if (fixture != null) Destroy(fixture);
            knight.transform.SetPositionAndRotation(saved, savedRotation);
            body.position = saved;
            Physics.SyncTransforms();
        }
    }
}
#endif
