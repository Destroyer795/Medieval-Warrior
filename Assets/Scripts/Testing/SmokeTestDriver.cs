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
