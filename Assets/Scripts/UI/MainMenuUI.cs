using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Title screen, mode select, knight customisation, settings and help - built in code.
public class MainMenuUI : MonoBehaviour
{
    RectTransform root;
    UIPage titlePage, playPage, customisePage, settingsPage, helpPage;
    CanvasGroup loading;
    MenuPreview preview;
    readonly Image[] modeCards = new Image[3];
    TextMeshProUGUI modeInfo;

    public static MainMenuUI Instance { get; private set; }

    void Awake()
    {
        Instance = this;
        var canvas = UIKit.Canvas("Menu Canvas", 5);
        canvas.transform.SetParent(transform, false);
        root = (RectTransform)canvas.transform;
        preview = FindFirstObjectByType<MenuPreview>();

        // soft dark gradient on the left so the menu reads over the live castle view
        var shade = UIKit.Img(root, "vignette", new Color(0, 0, 0, 0.85f), "Vignette");
        shade.rectTransform.Fill(-200);

        BuildTitle();
        BuildPlay();
        BuildCustomise();
        settingsPage = SharedPanels.Settings(root, () => Show(titlePage));
        helpPage = SharedPanels.HowToPlay(root, () => Show(titlePage));
        BuildLoading();
        Show(titlePage);
        AudioManager.SetBattleIntensity(0f, true);
        AudioManager.PlayMusic("music_menu", 0.8f);
    }

    void Show(UIPage page)
    {
        foreach (var p in new[] { titlePage, playPage, customisePage, settingsPage, helpPage })
            if (p != null && p != page) p.Hide();
        page.Show();
        if (preview != null) preview.SetActive(page == customisePage);
    }

    // ------------------------------------------------------------ title

    void BuildTitle()
    {
        var rt = UIKit.Rect("Title Page", root).Fill();
        titlePage = rt.gameObject.AddComponent<UIPage>();

        var title = UIKit.Text(rt, "Medieval Warrior", 104, UIKit.Gold, TextAlignmentOptions.Left, true);
        title.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(120, -110), new Vector2(1300, 130));
        var sub = UIKit.Text(rt, "The dead have risen. The castle must not fall.", 36, UIKit.Parchment, TextAlignmentOptions.Left);
        sub.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(126, -240), new Vector2(1300, 50));
        var div = UIKit.Img(rt, "divider", Color.white, "Divider");
        div.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(110, -300), new Vector2(560, 28));

        var list = UIKit.VList(rt, 18, null, TextAnchor.UpperLeft);
        ((RectTransform)list.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(130, -350), new Vector2(420, 560));
        list.childForceExpandWidth = false; list.childControlWidth = false;
        UIKit.Button(list.transform, "Play", () => Show(playPage), 400, 80, 40);
        UIKit.Button(list.transform, "Customise Knight", () => Show(customisePage), 400, 80, 36);
        UIKit.Button(list.transform, "Settings", () => Show(settingsPage), 400, 80, 36);
        UIKit.Button(list.transform, "How to Play", () => Show(helpPage), 400, 80, 36);
        UIKit.Button(list.transform, "Quit", Quit, 400, 80, 36, "button_dark");

        var records = UIKit.Text(rt, RecordsLine(), 24, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.BottomLeft);
        records.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(130, 40), new Vector2(1400, 40));
        var credit = UIKit.Text(rt, "v1.0", 20, new Color(1, 1, 1, 0.5f), TextAlignmentOptions.BottomRight);
        credit.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-30, 24), new Vector2(400, 30));
    }

    static string RecordsLine()
    {
        string s = "Best:  ";
        foreach (GameMode m in System.Enum.GetValues(typeof(GameMode)))
            s += $"{GameSettings.ModeName(m)} <color=#F5CB73>{GameSettings.HighScore(m):N0}</color>     ";
        if (GameSettings.BestWave > 0) s += $"Highest wave <color=#F5CB73>{GameSettings.BestWave}</color>";
        return s;
    }

    // ------------------------------------------------------------ play / mode select

    void BuildPlay()
    {
        playPage = SharedPanels.Page(root, "Play Page", new Vector2(1560, 900), "Choose Your Battle");
        var row = UIKit.HList(playPage.transform, 34);
        ((RectTransform)row.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(1440, 440));
        row.childForceExpandWidth = false; row.childControlWidth = false;

        string[] icons = { "swords", "horde", "hourglass" };
        for (int i = 0; i < 3; i++)
        {
            var mode = (GameMode)i;
            var card = UIKit.Img(row.transform, "panel_parchment", Color.white, "Card " + mode);
            card.raycastTarget = true;
            card.rectTransform.sizeDelta = new Vector2(450, 440);
            card.Layout(440, 450);
            modeCards[i] = card;
            var icon = UIKit.Icon(card.transform, icons[i], 110, UIKit.Ink);
            icon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -34), new Vector2(110, 110));
            var name = UIKit.Text(card.transform, GameSettings.ModeName(mode), 36, UIKit.Ink, TextAlignmentOptions.Center, true);
            name.fontSharedMaterial = name.font.material;
            name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(400, 50));
            var blurb = UIKit.Text(card.transform, GameSettings.ModeBlurb(mode), 24, UIKit.Ink, TextAlignmentOptions.Top);
            blurb.fontSharedMaterial = blurb.font.material;
            blurb.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -210), new Vector2(380, 150));
            var best = UIKit.Text(card.transform, "Best  " + GameSettings.HighScore(mode).ToString("N0"), 24, new Color(0.45f, 0.1f, 0.08f), TextAlignmentOptions.Bottom);
            best.fontSharedMaterial = best.font.material;
            best.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(380, 40));
            var b = card.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            int idx = i;
            b.onClick.AddListener(() => SelectMode((GameMode)idx));
            card.gameObject.AddComponent<UIHoverFx>().hoverScale = 1.03f;
        }

        var opts = UIKit.VList(playPage.transform, 10, null, TextAnchor.UpperCenter);
        ((RectTransform)opts.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -620), new Vector2(820, 70));
        UIKit.Selector(opts.transform, "Difficulty", new[] { "Squire (easy)", "Knight (normal)", "Warlord (hard)" }, (int)GameSettings.Difficulty, i => GameSettings.Difficulty = (Difficulty)i);

        var go = UIKit.Button(playPage.transform, "To Battle!", StartMatch, 420, 84, 42);
        ((RectTransform)go.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(170, 40), new Vector2(420, 84));
        var back = SharedPanels.BackButton(playPage.transform, () => Show(titlePage));
        ((RectTransform)back.transform).anchoredPosition = new Vector2(-200, 50);
        SelectMode(GameSettings.Mode);
    }

    void SelectMode(GameMode mode)
    {
        GameSettings.Mode = mode;
        for (int i = 0; i < modeCards.Length; i++)
        {
            bool on = i == (int)mode;
            modeCards[i].color = on ? Color.white : new Color(0.62f, 0.58f, 0.52f);
            modeCards[i].rectTransform.localScale = Vector3.one * (on ? 1.04f : 0.97f);
        }
    }

    // ------------------------------------------------------------ customisation

    void BuildCustomise()
    {
        customisePage = SharedPanels.Page(root, "Customise Page", new Vector2(1600, 940), "Your Knight");

        var view = UIKit.Img(customisePage.transform, "panel_iron", Color.white, "Preview Frame");
        view.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(70, -10), new Vector2(560, 720));
        var raw = UIKit.Rect("Preview", view.transform).Fill(18).gameObject.AddComponent<RawImage>();
        raw.raycastTarget = true;
        if (preview != null) raw.texture = preview.Texture;
        else UIKit.Text(view.transform, "Preview unavailable", 26).rectTransform.Fill();
        raw.gameObject.AddComponent<PreviewDrag>().preview = preview;
        var hint = UIKit.Text(view.transform, "drag to turn", 20, new Color(1, 1, 1, 0.55f), TextAlignmentOptions.Bottom);
        hint.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(400, 30));

        var list = UIKit.VList(customisePage.transform, 14, null, TextAnchor.UpperLeft);
        ((RectTransform)list.transform).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-70, -150), new Vector2(880, 640));

        UIKit.InputField(list.transform, "Hero name", GameSettings.HeroName, v => GameSettings.HeroName = v);
        SharedPanels.Section(list.transform, "Heraldry");
        var tabard = UIKit.Selector(list.transform, "Tabard & shield", Names(GameSettings.TabardColors), GameSettings.Tabard, i => { GameSettings.Tabard = i; Refresh(); }, i => GameSettings.TabardColors[i].color);
        var crest = UIKit.Selector(list.transform, "Crest", GameSettings.Crests, GameSettings.Crest, i => GameSettings.Crest = i);
        SharedPanels.Section(list.transform, "Armour");
        var metal = UIKit.Selector(list.transform, "Metal", Names(GameSettings.MetalTints), GameSettings.Metal, i => { GameSettings.Metal = i; Refresh(); }, i => GameSettings.MetalTints[i].tint * 0.7f);
        var leather = UIKit.Selector(list.transform, "Leather", Names(GameSettings.LeatherTints), GameSettings.Leather, i => { GameSettings.Leather = i; Refresh(); }, i => GameSettings.LeatherTints[i].tint * new Color(0.45f, 0.3f, 0.2f));
        SharedPanels.Section(list.transform, "Bow");
        var bowNames = new string[GameSettings.BowFinishes.Length];
        for (int i = 0; i < bowNames.Length; i++) bowNames[i] = GameSettings.BowFinishes[i].name;
        var bow = UIKit.Selector(list.transform, "Bow finish", bowNames, GameSettings.BowFinish, i => GameSettings.BowFinish = i, i => GameSettings.BowFinishes[i].wood);

        var buttons = UIKit.HList(customisePage.transform, 24);
        ((RectTransform)buttons.transform).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-70, 46), new Vector2(880, 72));
        buttons.childForceExpandWidth = false; buttons.childControlWidth = false;
        UIKit.Button(buttons.transform, "Randomise", () =>
        {
            tabard.Set(Random.Range(0, GameSettings.TabardColors.Length));
            metal.Set(Random.Range(0, GameSettings.MetalTints.Length));
            leather.Set(Random.Range(0, GameSettings.LeatherTints.Length));
            bow.Set(Random.Range(0, GameSettings.BowFinishes.Length));
            crest.Set(Random.Range(0, GameSettings.Crests.Length));
        }, 300, 70, 32, "button_dark");
        UIKit.Button(buttons.transform, "Done", () => { GameSettings.Save(); Show(titlePage); }, 300, 70, 34);
        Refresh();
    }

    static string[] Names((string name, Color c)[] items)
    {
        var n = new string[items.Length];
        for (int i = 0; i < n.Length; i++) n[i] = items[i].name;
        return n;
    }

    void Refresh()
    {
        if (preview != null && preview.Appearance != null) preview.Appearance.Apply();
    }

    // ------------------------------------------------------------ start / quit

    void BuildLoading()
    {
        var img = UIKit.Img(root, null, new Color(0.02f, 0.015f, 0.01f, 1f), "Loading");
        img.rectTransform.Fill();
        img.raycastTarget = true;
        var t = UIKit.Text(img.transform, "The gates open...", 56, UIKit.Gold, TextAlignmentOptions.Center, true);
        t.rectTransform.Fill();
        loading = img.gameObject.AddComponent<CanvasGroup>();
        loading.alpha = 0f;
        loading.blocksRaycasts = false;
        img.transform.SetAsLastSibling();
    }

    void StartMatch()
    {
        GameSettings.Save();
        StartCoroutine(LoadGame());
    }

    IEnumerator LoadGame()
    {
        loading.blocksRaycasts = true;
        for (float t = 0; t < 1f; t += Time.unscaledDeltaTime * 3f) { loading.alpha = t; yield return null; }
        loading.alpha = 1f;
        AudioManager.StopMusic();
        yield return null;
        SceneManager.LoadScene("Semi-Final", LoadSceneMode.Single);
    }

    void Quit()
    {
        GameSettings.Save();
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    // exposed for the automated smoke test
    public void DebugShow(string page)
    {
        Show(page switch { "play" => playPage, "customise" => customisePage, "settings" => settingsPage, "help" => helpPage, _ => titlePage });
    }
}

public class PreviewDrag : MonoBehaviour, IDragHandler
{
    public MenuPreview preview;
    public void OnDrag(PointerEventData e) { if (preview != null) preview.Drag(e.delta.x); }
}
