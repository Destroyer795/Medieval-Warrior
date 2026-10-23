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
        GameObject.FindWithTag("Player").GetComponent<PlayerHealth>().TakeDamage(9999f, null, 0f, true);
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
