using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Self-test for a built player: run  MedievalWarrior.exe -batchmode -autotest -logFile autotest.log
// It plays a short Defend the Castle match and an Endless Horde run without input, then quits with
// exit code 0 if nothing went wrong. Does nothing unless "-autotest" is on the command line.
public class AutoPlayTest : MonoBehaviour
{
    readonly List<string> problems = new List<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        if (!System.Environment.GetCommandLineArgs().Contains("-autotest")) return;
        var go = new GameObject("Auto Play Test");
        DontDestroyOnLoad(go);
        go.AddComponent<AutoPlayTest>();
    }

    void Awake()
    {
        GameSettings.TestMode = true;
        Application.logMessageReceived += (msg, stack, type) =>
        {
            if ((type == LogType.Error || type == LogType.Exception) && !msg.StartsWith("[AUTOTEST]"))
                problems.Add(type + ": " + msg + " " + stack.Split('\n').FirstOrDefault());
        };
    }

    void Start() => StartCoroutine(Run());

    void Expect(bool ok, string what)
    {
        Debug.Log("[AUTOTEST] " + (ok ? "PASS " : "FAIL ") + what);
        if (!ok) problems.Add("check failed: " + what);
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(3f);
        Expect(FindFirstObjectByType<MainMenuUI>() != null, "menu built");
        Expect(Application.version == "1.2", "Windows player reports version 1.2");

        GameSettings.Mode = GameMode.DefendTheCastle;
        SceneManager.LoadScene("Semi-Final");
        yield return new WaitForSecondsRealtime(12f);
        var gm = GameManager.instance;
        Expect(gm != null && gm.State == GameManager.MatchState.Playing, "match running");
        Expect(EnemyController.All.Count > 0, $"zombies spawned ({EnemyController.All.Count})");

        var player = GameObject.FindWithTag("Player");
        var pc = player != null ? player.GetComponent<PlayerControlSpeed>() : null;
        var ph = player != null ? player.GetComponent<PlayerHealth>() : null;
        if (pc != null && ph != null)
        {
            ph.InvulnerableUntil = Time.time + 60f;
            var outer = gm.Outer;
            Expect(outer != null && outer.Structure != null && !outer.Breached &&
                pc.CurrentWeapon == Weapon.Bow, "Defend opens on the fortified wall with the bow");
            if (outer != null)
            {
                var gateLabel = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.text.StartsWith("OUTER GATE"));
                var objective = FindObjectsByType<TMPro.TextMeshProUGUI>(FindObjectsSortMode.None)
                    .FirstOrDefault(t => t.text == gm.Objective);
                bool Centered(TMPro.TextMeshProUGUI label)
                {
                    if (label == null) return false;
                    var rect = label.rectTransform;
                    var screen = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                    return Mathf.Abs(screen.x - Screen.width * .5f) < 2f && !label.isTextOverflowing;
                }
                Expect(Centered(objective) && Centered(gateLabel), "siege title and integrity label share the screen centre without clipping");
                var gallery = outer.Structure.World(9f, 6.25f, -.1f);
                Expect(Mathf.Abs(pc.transform.position.y - gallery.y) < 1f &&
                    Physics.Raycast(gallery + Vector3.up * 1.2f, Vector3.down, 2f,
                        ~0, QueryTriggerInteraction.Ignore), "the built player's parapet supports the knight");
                outer.TakeDamage(outer.MaxHealth);
                Expect(outer.Cinematic, "outer gate opens into the breach cinematic");
                yield return new WaitForSecondsRealtime(1.6f);
                Expect(outer.Breached && !outer.Cinematic && gm.Gate.Integrity == 1f &&
                    gm.State == GameManager.MatchState.Playing, "control returns for the inner keep defense");
            }
            pc.Equip(Weapon.Bow);
            foreach (var z in EnemyController.All.Take(3).ToArray())
            {
                pc.DebugFireAt(z.transform.position + Vector3.up * 1.2f, 1f);
                yield return new WaitForSecondsRealtime(0.4f);
            }
            pc.Equip(Weapon.Sword);
            pc.DebugSwing();
            foreach (var z in EnemyController.All.ToArray()) z.TakeDamage(999f, Vector3.forward, 4f);
            yield return new WaitForSecondsRealtime(3f);
            if (GameHUD.BoonsOpen) GameHUD.DebugPickBoon(0);
            Expect(gm.currentKills > 0, $"kills registered ({gm.currentKills})");

            // performance sample while the horde regroups (real numbers only when not -batchmode)
            int frames = 0; float worst = 0f, start = Time.realtimeSinceStartup;
            while (Time.realtimeSinceStartup - start < 10f)
            {
                yield return null;
                if (GameHUD.BoonsOpen) GameHUD.DebugPickBoon(0);
                frames++;
                worst = Mathf.Max(worst, Time.unscaledDeltaTime);
            }
            float avg = frames / (Time.realtimeSinceStartup - start);
            Debug.Log($"[AUTOTEST] perf: {avg:F0} fps average, worst frame {worst * 1000f:F0} ms, {EnemyController.All.Count} zombies, {Screen.width}x{Screen.height}, quality {QualitySettings.names[QualitySettings.GetQualityLevel()]}");
            Expect(EnemyController.All.Count > 0, "horde keeps coming");
        }

        GameSettings.Mode = GameMode.EndlessHorde;
        gm.RestartGame();
        yield return new WaitForSecondsRealtime(12f);
        gm = GameManager.instance;
        Expect(gm != null && gm.Wave >= 1, "endless wave started");
        Expect(gm != null && gm.Outer == null && FindFirstObjectByType<ForegateStructure>() == null,
            "switching to Horde removes the siege frontage");
        Expect(GameObject.Find("Wall (7)") != null && GameObject.Find("House (3)") != null &&
            GameObject.Find("House (20)") != null && Terrain.activeTerrain != null &&
            !Terrain.activeTerrain.terrainData.name.EndsWith("(Clone)"),
            "switching modes restores the original curtain, village houses and terrain");
        Expect(gm != null && gm.Horde.Goblet != null, "wall goblet is present in the built Horde arena");
        Expect(gm != null && gm.GobletScoreMultiplier == 1, "Horde begins at ordinary kill score");
        var defender = GameObject.FindWithTag("Player").GetComponent<PlayerHealth>();
        defender.InvulnerableUntil = Time.time + 30f;
        float waveDeadline = Time.realtimeSinceStartup + 25f;
        while (gm.State == GameManager.MatchState.Playing && Time.realtimeSinceStartup < waveDeadline)
        {
            foreach (var enemy in EnemyController.All.ToArray())
                if (enemy != null && !enemy.IsDead) enemy.TakeDamage(999f, Vector3.forward, 0f);
            yield return null;
        }
        Expect(gm.State == GameManager.MatchState.Intermission && !gm.ChoosingBoon,
            "the last kill leaves the player in the arena before the draft");
        float clearedAt = Time.time;
        yield return new WaitForSecondsRealtime(1.2f);
        Expect(!gm.ChoosingBoon && gm.Wave == 1 && Time.timeScale > 0f, "the boon draft does not open early");
        yield return new WaitForSecondsRealtime(1.25f);
        Expect(gm.ChoosingBoon && gm.Wave == 1 && Time.time - clearedAt >= 1.9f,
            "the boon draft opens after two seconds and holds the wave");
        if (gm.ChoosingBoon) GameHUD.DebugPickBoon(0);
        defender.InvulnerableUntil = 0f;
        defender.TakeDamage(9999f, null, 0f, true);
        yield return new WaitForSecondsRealtime(4f);
        Expect(gm.State == GameManager.MatchState.Lost, "run ends on death");
        gm.QuitGame();
        yield return new WaitForSecondsRealtime(3f);
        Expect(FindFirstObjectByType<MainMenuUI>() != null, "back at menu");

        foreach (var p in problems.Distinct().Take(30)) Debug.Log("[AUTOTEST] problem: " + p);
        Debug.Log(problems.Count == 0 ? "[AUTOTEST] RESULT PASS" : $"[AUTOTEST] RESULT FAIL ({problems.Count})");
        GameSettings.TestMode = false;
        Application.Quit(problems.Count == 0 ? 0 : 1);
    }
}
