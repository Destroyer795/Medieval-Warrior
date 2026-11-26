#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public partial class SmokeTestDriver
{
    // Inspect the actual text mesh. TMP's overflow flag can be false when a layout
    // group has made the label wider than the panel that paints its background.
    static float GlyphClearance(TextMeshProUGUI label, RectTransform panel)
    {
        label.ForceMeshUpdate();
        if (label.textInfo.characterCount == 0 || label.isTextTruncated) return float.NegativeInfinity;
        float gap = float.PositiveInfinity;
        var bounds = panel.rect;
        foreach (var glyph in label.textInfo.characterInfo.Take(label.textInfo.characterCount))
        {
            if (!glyph.isVisible) continue;
            var min = panel.InverseTransformPoint(label.transform.TransformPoint(glyph.bottomLeft));
            var max = panel.InverseTransformPoint(label.transform.TransformPoint(glyph.topRight));
            gap = Mathf.Min(gap, min.x - bounds.xMin, bounds.xMax - max.x, min.y - bounds.yMin, bounds.yMax - max.y);
        }
        return gap == float.PositiveInfinity ? float.NegativeInfinity : gap;
    }

    void ReviewSwordCaption(MainMenuUI menu)
    {
        var label = menu.GetComponentsInChildren<TextMeshProUGUI>().First(t => t.name == "Sword status");
        var panel = (RectTransform)label.transform.parent;
        string original = label.text;
        float minimum = float.PositiveInfinity;
        for (int i = 3; i < SwordWardrobe.Names.Length; i++)
        {
            // Exercise the locked copy for every earned sword without touching the player's feats.
            label.text = $"<color=#F5CB73>{SwordWardrobe.Names[i]}</color>\nLocked  |  {SwordWardrobe.Challenge(i)}";
            minimum = Mathf.Min(minimum, GlyphClearance(label, panel));
        }
        label.text = original;
        Check(minimum >= 10f, $"all four locked sword captions clear their frame (minimum {minimum:F1} canvas units)");
    }

    IEnumerator ReviewHordeOpeningHud()
    {
        var deadline = Time.realtimeSinceStartup + 40f;
        while (Time.realtimeSinceStartup < deadline && (GameManager.instance == null ||
               GameManager.instance.Mode != GameMode.EndlessHorde || FindFirstObjectByType<GameHUD>() == null)) yield return null;
        // Read after HUD.Update has actually painted an intro frame.
        yield return null;
        var gm = GameManager.instance;
        var hud = FindFirstObjectByType<GameHUD>();
        if (gm == null || hud == null) { Check(false, "Horde opening HUD loaded"); yield break; }
        var objective = hud.GetComponentsInChildren<RectTransform>().First(t => t.name == "Objective");
        Check(gm.State == GameManager.MatchState.Intro && !gm.Objective.Contains("remain") &&
            objective.GetComponentsInChildren<TextMeshProUGUI>().Any(t => t.text == gm.Objective && t.text.Length > 0),
            $"Horde preparation displays an objective before any wave is counted (displayed '{gm.Objective}')");

        var combat = hud.GetComponentsInChildren<RectTransform>().First(t => t.name == "Combat HUD");
        var scaler = combat.GetComponentInParent<CanvasScaler>();
        Vector2 originalDelta = combat.sizeDelta;
        const string shieldTip = "Hold right mouse to raise your shield - block as a blow lands for a Perfect Block";
        var bulwark = ZombieTypes.Get(ZombieType.Bulwark);
        var runner = ZombieTypes.Get(ZombieType.Runner);
        var samples = new[] { shieldTip, bulwark.name + ": " + bulwark.description, runner.name + ": " + runner.description, "+1 arrow" };
        foreach (var viewport in new[] { new Vector2Int(923, 550), new Vector2Int(1024, 768), new Vector2Int(1920, 1080), new Vector2Int(2560, 1080) })
        {
            // Give the real combat group the logical space that the game's CanvasScaler
            // provides at each resolution. This also works in batch-mode Game Views.
            float scale = Mathf.Pow(viewport.x / scaler.referenceResolution.x, 1f - scaler.matchWidthOrHeight) *
                          Mathf.Pow(viewport.y / scaler.referenceResolution.y, scaler.matchWidthOrHeight);
            combat.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewport.x / scale);
            combat.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewport.y / scale);
            GameHUD.Toast(samples[0], UIKit.Parchment, "shield", 4.5f, 28);
            GameHUD.Announce(samples[1], UIKit.Gold);
            GameHUD.Toast(samples[2], UIKit.Gold, "skull", 4f, 25, wrap: true);
            GameHUD.Toast(samples[3], UIKit.Gold);
            Canvas.ForceUpdateCanvases();
            var plates = combat.GetComponentsInChildren<RectTransform>().Where(t => t.name == "Notification" &&
                samples.Contains(t.GetComponentInChildren<TextMeshProUGUI>()?.text)).ToArray();
            float minimum = float.PositiveInfinity;
            bool onScreen = plates.Length == samples.Length;
            foreach (var plate in plates)
            {
                minimum = Mathf.Min(minimum, GlyphClearance(plate.GetComponentInChildren<TextMeshProUGUI>(), plate));
                var corners = new Vector3[4];
                plate.GetWorldCorners(corners);
                onScreen &= corners.All(p => combat.rect.Contains((Vector2)combat.InverseTransformPoint(p)));
            }
            Check(onScreen && minimum >= 4f, $"combat tip, enemy introductions and pickup fit their plaques at {viewport.x}x{viewport.y} (minimum glyph inset {minimum:F1})");
            if (viewport.x == 1920) Capture("horde_notification_stack");
            foreach (var plate in plates) DestroyImmediate(plate.gameObject);
        }
        combat.sizeDelta = originalDelta;
        while (gm.State == GameManager.MatchState.Intro && Time.realtimeSinceStartup < deadline) yield return null;
        yield return null;
        Check(gm.Wave == 1 && gm.WaveRemaining > 0 && gm.Objective.Contains("1") && gm.Objective.Contains("remain"),
            "the preparation label gives way to the actual wave and remaining enemies");
    }
}
#endif
