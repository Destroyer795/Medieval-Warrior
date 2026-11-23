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

    void ReviewWardrobe(MainMenuUI menu)
    {
        var feats = new[] { "defender", "dawn", "unbroken", "last_oath" };
        foreach (var feat in feats) PlayerPrefs.DeleteKey(Feats.Preference(feat));
        PlayerPrefs.SetString(SwordWardrobe.Preference, "retired-design");
        try
        {
            Expect(SwordWardrobe.Selected == 0 &&
                Enumerable.Range(0, 7).All(i => SwordWardrobe.Available(i) == (i < 3)),
                "a new knight has three starter swords and recovers from a missing saved design");
            menu.DebugShow("customise");
            var row = menu.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Sword selection");
            var next = row != null ? row.Find("Next")?.GetComponent<UnityEngine.UI.Button>() : null;
            var preview = FindFirstObjectByType<MenuPreview>();
            Expect(next != null && preview != null, "sword cycling is accessible in Customise Knight");
            if (next != null && preview != null)
            {
                var filter = preview.knight.GetComponent<MarchwardenVisual>().Sword.GetComponent<MeshFilter>();
                for (int i = 1; i <= 3; i++)
                {
                    next.onClick.Invoke();
                    Expect(filter.sharedMesh == SwordWardrobe.Model(i).GetComponent<MeshFilter>().sharedMesh &&
                        SwordWardrobe.Selected == Mathf.Min(i, 2),
                        SwordWardrobe.Names[i] + " previews with the correct ownership state");
                    if (i == 3)
                    {
                        Canvas.ForceUpdateCanvases();
                        var hint = menu.GetComponentsInChildren<TMPro.TextMeshProUGUI>().FirstOrDefault(t => t.name == "Sword status");
                        Expect(hint != null && hint.text.Contains(SwordWardrobe.Challenge(i)) && !hint.isTextOverflowing,
                            "the locked sword shows its complete challenge beside the preview");
                    }
                }
                menu.DebugShow("title");
                menu.DebugShow("customise");
                Expect(filter.sharedMesh == SwordWardrobe.Model(2).GetComponent<MeshFilter>().sharedMesh,
                    "reopening customization restores the equipped sword after inspecting a locked one");
            }
            menu.DebugShow("title");
            for (int i = 3; i < 7; i++)
            {
                bool wasLocked = !SwordWardrobe.TryEquip(i);
                GameSettings.TestMode = false;
                Feats.Unlock(feats[i - 3]);
                GameSettings.TestMode = true;
                Expect(wasLocked && SwordWardrobe.TryEquip(i) && SwordWardrobe.Selected == i &&
                    Enumerable.Range(i+1, 6-i).All(j => !SwordWardrobe.Available(j)),
                    feats[i-3] + " unlocks its sword and keeps later challenges locked");
            }
        }
        finally
        {
            GameSettings.TestMode = true;
            foreach (var feat in feats) PlayerPrefs.DeleteKey(Feats.Preference(feat));
            SwordWardrobe.TryEquip(1);
        }
    }

    IEnumerator Run()
    {
        bool hadMode = PlayerPrefs.HasKey("mw.mode");
        var originalMode = GameSettings.Mode;
        yield return new WaitForSecondsRealtime(3f);
        Expect(FindFirstObjectByType<MainMenuUI>() != null, "menu built");
        Expect(Application.version == "1.2", "Windows player reports version 1.2");
        var menu = FindFirstObjectByType<MainMenuUI>();
        if (menu != null) ReviewWardrobe(menu);

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
            var visual = pc.GetComponent<MarchwardenVisual>();
            var hip = pc.transform.Find("Sheathed Sword");
            Expect(visual != null && hip != null &&
                visual.Sword.GetComponent<MeshFilter>().sharedMesh == SwordWardrobe.Model(1).GetComponent<MeshFilter>().sharedMesh &&
                hip.GetComponent<MeshFilter>().sharedMesh == visual.Sword.GetComponent<MeshFilter>().sharedMesh,
                "the saved Fenland Falchion loads on both battle mounts");
            var designs = Resources.LoadAll<GameObject>("SwordCollection");
            Expect(designs.Length == 7 && hip != null && visual != null, "seven cosmetic swords and both equipment mounts load in the player");
            if (hip != null && visual != null)
            {
                foreach (var design in designs)
                {
                    visual.SetSwordAppearance(design);
                    var mesh = design.GetComponent<MeshFilter>().sharedMesh;
                    pc.DebugSwingVariant(5);
                    yield return new WaitForSeconds(.15f);
                    bool gripped = visual.Sword.gameObject.activeSelf && visual.SwordGripError < .002f &&
                        visual.Sword.GetComponent<MeshFilter>().sharedMesh == mesh;
                    pc.Equip(Weapon.Bow);
                    yield return null;
                    bool stowed = hip.gameObject.activeInHierarchy && !visual.Sword.gameObject.activeSelf &&
                        hip.GetComponent<MeshFilter>().sharedMesh == mesh &&
                        hip.GetComponent<MeshRenderer>().sharedMaterials.SequenceEqual(design.GetComponent<MeshRenderer>().sharedMaterials);
                    pc.Equip(Weapon.Sword);
                    Expect(gripped && stowed && !hip.gameObject.activeSelf && visual.Sword.gameObject.activeSelf,
                        design.name + " fits during thrust, stows on the hip and redraws");
                }
                visual.SetSwordAppearance(null);
                Expect(visual.Sword.GetComponent<MeshFilter>().sharedMesh == MarchwardenVisual.Asset.sword &&
                    hip.GetComponent<MeshFilter>().sharedMesh == MarchwardenVisual.Asset.sword, "restoring the original sword updates both mounts");
            }
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
        if (hadMode) GameSettings.Mode = originalMode; else PlayerPrefs.DeleteKey("mw.mode");
        PlayerPrefs.Save();
        Application.Quit(problems.Count == 0 ? 0 : 1);
    }
}
