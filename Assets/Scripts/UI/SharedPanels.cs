using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Panels used both by the main menu and the in-game pause menu.
public static class SharedPanels
{
    public static UIPage Settings(Transform parent, Action onBack)
    {
        var page = Page(parent, "Settings Page", new Vector2(1640, 820), "Settings");
        var cols = UIKit.HList(page.transform, 70, TextAnchor.UpperCenter);
        cols.childForceExpandHeight = false;
        var crt = (RectTransform)cols.transform;
        crt.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(1500, 540));
        var leftCol = UIKit.VList(cols.transform, 6, null, TextAnchor.UpperLeft); leftCol.Layout(-1, 720);
        var rightCol = UIKit.VList(cols.transform, 6, null, TextAnchor.UpperLeft); rightCol.Layout(-1, 700);
        var list = leftCol.transform;

        Section(list, "Sound");
        UIKit.Slider(list, "Master volume", 0, 1, GameSettings.MasterVolume, v => GameSettings.MasterVolume = v);
        UIKit.Slider(list, "Effects", 0, 1, GameSettings.SfxVolume, v => GameSettings.SfxVolume = v);
        UIKit.Slider(list, "Music & ambience", 0, 1, GameSettings.MusicVolume, v => GameSettings.MusicVolume = v);

        Section(list, "Controls");
        UIKit.Slider(list, "Mouse sensitivity", 0.5f, 10f, GameSettings.MouseSensitivity, v => GameSettings.MouseSensitivity = v, v => v.ToString("0.0"));
        UIKit.Toggle(list, "Invert vertical look", GameSettings.InvertY, v => GameSettings.InvertY = v);
        UIKit.Slider(list, "Field of view", 50f, 90f, GameSettings.FieldOfView, v => GameSettings.FieldOfView = v, v => Mathf.RoundToInt(v) + "°");

        list = rightCol.transform;
        Section(list, "Display");
        var names = QualitySettings.names;
        UIKit.Selector(list, "Graphics quality", names, Mathf.Clamp(QualitySettings.GetQualityLevel(), 0, names.Length - 1), i => GameSettings.Quality = i);
        UIKit.Toggle(list, "Fullscreen", Screen.fullScreen, v => GameSettings.Fullscreen = v);
        UIKit.Toggle(list, "Screen shake", GameSettings.CameraShake, v => GameSettings.CameraShake = v);
        UIKit.Toggle(list, "Damage numbers", GameSettings.ShowDamageNumbers, v => GameSettings.ShowDamageNumbers = v);
        UIKit.Toggle(list, "Show FPS", GameSettings.ShowFps, v => GameSettings.ShowFps = v);

        BackButton(page.transform, () => { GameSettings.Save(); onBack?.Invoke(); });
        return page;
    }

    public static UIPage HowToPlay(Transform parent, Action onBack)
    {
        var page = Page(parent, "Help Page", new Vector2(1500, 900), "How to Play");
        var cols = UIKit.HList(page.transform, 50, TextAnchor.UpperCenter);
        cols.childForceExpandHeight = false;
        cols.childAlignment = TextAnchor.UpperCenter;
        var crt = (RectTransform)cols.transform;
        crt.Fill(); crt.offsetMin = new Vector2(70, 120); crt.offsetMax = new Vector2(-70, -138);

        var left = UIKit.VList(cols.transform, 8, null, TextAnchor.UpperLeft);
        left.Layout(-1, 620);
        Section(left.transform, "Controls");
        foreach (var (key, what) in new[]
        {
            ("W A S D", "Move"), ("Mouse", "Look around"), ("Shift", "Sprint (uses stamina)"), ("Space", "Dodge roll - brief invulnerability"),
            ("Left click", "Sword combo / hold to draw the bow"), ("Right click / E", "Raise shield (sword) or aim (bow)"),
            ("Block + click", "Shield bash - knocks zombies back"), ("Q  or  1 / 2", "Swap sword and bow"), ("Scroll", "Zoom camera"), ("Esc", "Pause"),
        })
        {
            var row = UIKit.HList(left.transform, 12, TextAnchor.MiddleLeft);
            row.Layout(34);
            UIKit.Text(row.transform, key, 24, UIKit.Gold, TextAlignmentOptions.MidlineLeft).Layout(-1, 220);
            UIKit.Text(row.transform, what, 24, UIKit.Parchment, TextAlignmentOptions.MidlineLeft).Layout(-1, 390);
        }
        Section(left.transform, "Tips");
        UIKit.Text(left.transform, "Block just as a blow lands for a Perfect Block. Swing as you're struck to Parry. Arrows wait on supply crates by the houses and drop from the dead. Lure Bloaters into crowds!", 21, UIKit.Parchment, TextAlignmentOptions.TopLeft).Layout(96);

        var right = UIKit.VList(cols.transform, 8, null, TextAnchor.UpperLeft);
        right.Layout(-1, 680);
        Section(right.transform, "Bestiary");
        for (int i = 0; i < ZombieTypes.Count; i++)
        {
            var s = ZombieTypes.Get((ZombieType)i);
            var row = UIKit.HList(right.transform, 14, TextAnchor.MiddleLeft);
            row.Layout(78);
            var sw = UIKit.Icon(row.transform, "skull", 52, Color.Lerp(s.tint, s.glow * 3f, 0.35f));
            sw.Layout(56, 56);
            var col = UIKit.VList(row.transform, 0, null, TextAnchor.MiddleLeft);
            col.Layout(-1, 600);
            UIKit.Text(col.transform, s.name, 26, UIKit.Gold, TextAlignmentOptions.MidlineLeft, true).Layout(32);
            UIKit.Text(col.transform, s.description, 20, UIKit.Parchment, TextAlignmentOptions.MidlineLeft).Layout(44);
        }

        BackButton(page.transform, onBack);
        return page;
    }

    // ------------------------------------------------------------ helpers

    public static UIPage Page(Transform parent, string name, Vector2 size, string title)
    {
        var panel = UIKit.Panel(parent, size, "panel_dark");
        panel.name = name;
        panel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
        var t = UIKit.Text(panel.transform, title, 54, UIKit.Gold, TextAlignmentOptions.Center, true);
        t.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -36), new Vector2(size.x - 120, 72));
        var div = UIKit.Img(panel.transform, "divider", Color.white, "Divider");
        div.rectTransform.Place(new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0, -112), new Vector2(520, 26));
        var page = panel.gameObject.AddComponent<UIPage>();
        page.Hide();
        return page;
    }

    static Transform Content(Transform page, float width)
    {
        var list = UIKit.VList(page, 6, null, TextAnchor.UpperCenter);
        var rt = (RectTransform)list.transform;
        rt.anchorMin = new Vector2(0.5f, 1f); rt.anchorMax = new Vector2(0.5f, 1f); rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -140);
        rt.sizeDelta = new Vector2(width, 640);
        return list.transform;
    }

    public static void Section(Transform list, string title)
    {
        var t = UIKit.Text(list, title.ToUpperInvariant(), 26, UIKit.GoldDim, TextAlignmentOptions.BottomLeft, true);
        t.Layout(40);
    }

    public static Button BackButton(Transform page, Action onBack)
    {
        var b = UIKit.Button(page, "Back", onBack, 260, 64, 30);
        ((RectTransform)b.transform).Place(new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0, 40), new Vector2(260, 64));
        UnityEngine.Object.Destroy(b.GetComponent<LayoutElement>());
        return b;
    }
}
