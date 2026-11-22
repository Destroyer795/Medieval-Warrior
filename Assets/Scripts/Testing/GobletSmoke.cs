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
        Check(goblet != null, "an unclaimed goblet appears in the central village during an ordinary wave");
        if (goblet == null) yield break;

        var path = new NavMeshPath();
        Vector3 spawn = director.OpeningSpawn;
        Check(NavMesh.SamplePosition(spawn, out var start, 5f, NavMesh.AllAreas) &&
            NavMesh.CalculatePath(start.position, goblet.Approach, NavMesh.AllAreas, path) &&
            path.status == NavMeshPathStatus.PathComplete, "the goblet has a walkable approach from the opening spawn");
        var keep = GameObject.Find("Castle/Castle")?.transform;
        bool wallShelf = keep != null && Physics.Raycast(goblet.Target, -goblet.transform.forward, out var hit,
            1.5f, ~0, QueryTriggerInteraction.Ignore) && (hit.transform == keep || hit.transform.IsChildOf(keep));
        float distance = Vector3.Distance(spawn, goblet.Approach);
        bool courtyardPlinth = goblet.transform.Find("Courtyard plinth") != null && distance < 13f;
        Check(distance < 28f && (wallShelf || courtyardPlinth),
            $"the goblet stands {distance:F1} m from spawn at a keep wall or courtyard plinth");
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
