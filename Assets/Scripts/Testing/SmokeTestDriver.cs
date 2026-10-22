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
public class SmokeTestDriver : MonoBehaviour
{
    string shotDir;
    readonly StringBuilder report = new StringBuilder();
    readonly List<string> errors = new List<string>();
    int failures;

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
            foreach (var page in new[] { "play", "customise", "settings", "help" })
            {
                menu.DebugShow(page);
                yield return Wait(0.6f);
                Capture("02_menu_" + page);
            }
            menu.DebugShow("title");
        }
        var preview = FindFirstObjectByType<MenuPreview>();
        Check(preview != null && preview.Appearance != null, "knight preview + customisation component");

        // ---------------------------------------------------------- Defend the Castle
        GameSettings.Mode = GameMode.DefendTheCastle;
        var mc = FindFirstObjectByType<MainController>();
        if (mc != null) mc.StartGame(); else SceneManager.LoadScene("Semi-Final");
        yield return Wait(1.5f);
        Check(SceneManager.GetSceneByName("Background").isLoaded, "game loads the world scene itself");
        var gm = GameManager.instance;
        Check(gm != null, "GameManager present");
        Check(FindFirstObjectByType<GameHUD>() != null, "HUD built");
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

        // clear the field, then line up one of each type in front of the knight
        foreach (var z in EnemyController.All.ToArray()) Destroy(z.gameObject);
        yield return null;
        health.InvulnerableUntil = Time.time + 25f;
        var spawner = ZombieSpawner.Instance;
        var cam = Camera.main;
        Vector3 fwd = cam.transform.forward; fwd.y = 0; fwd.Normalize();
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
        int arrowsBefore = player.Arrows;
        foreach (var e in line.Where(x => x != null && !x.IsDead).Take(3))
        {
            player.DebugFireAt(e.transform.position + Vector3.up * 1.2f, 1f);
            yield return Wait(0.35f);
        }
        yield return Wait(0.8f);
        Check(player.Arrows == arrowsBefore - 3, "arrows consumed");
        Check(gm.ArrowsFired >= 3, "shots counted");
        Check(gm.ArrowsHit >= 1, $"arrows hit zombies ({gm.ArrowsHit}/{gm.ArrowsFired})");
        player.DebugAim(false);

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

        // kill everything; bloater explosion must not throw
        int killsBefore = gm.currentKills, scoreBefore = gm.Score;
        foreach (var e in line.Where(x => x != null && !x.IsDead).ToArray()) e.TakeDamage(999f, fwd, 5f);
        yield return Wait(1.5f);
        Check(gm.currentKills > killsBefore, $"kills counted ({gm.currentKills - killsBefore})");
        Check(gm.Score > scoreBefore, "score increases with kills");
        Capture("16_after_kills");

        // spitter bile hits the knight and leaves acid
        health.InvulnerableUntil = 0f;
        float hp = health.currentHealth;
        BileGlob.Throw(player.transform.position + Vector3.up * 6f + fwd * 4f, player.transform.position, 10f, null);
        yield return Wait(1.8f);
        Check(health.currentHealth < hp, $"bile damages the knight ({hp:F0} -> {health.currentHealth:F0})");
        Check(FindFirstObjectByType<AcidPool>() != null, "acid pool created");
        Capture("17_acid");

        // pickups
        int arrows = player.Arrows;
        Pickup.Spawn(PickupKind.Arrows, player.transform.position + fwd * 0.5f, 6);
        yield return Wait(1.2f);
        Check(player.Arrows > arrows, $"arrow pickup collected ({arrows} -> {player.Arrows})");
        Check(Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None).Count(p => p.respawns) >= 5, "supply crates with pickups exist in the world");

        // pause menu
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
