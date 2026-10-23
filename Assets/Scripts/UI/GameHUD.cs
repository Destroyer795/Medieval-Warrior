using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// The whole in-game interface, built in code on top of UIKit.
public class GameHUD : MonoBehaviour
{
    static GameHUD instance;

    Canvas canvas;
    RectTransform root;

    // vitals
    Image healthFill, healthGhost, staminaFill, emblemField, emblemCharge, vignette;
    TextMeshProUGUI healthText, heroName;
    float ghost = 1f, vignetteAlpha;
    RectTransform healthFrame;

    // objective / score
    TextMeshProUGUI objectiveText, scoreText, comboText;
    Image objectiveIcon, comboBar;
    RectTransform bossRoot;
    Image bossFill;
    TextMeshProUGUI bossName;

    // weapons
    Image swordSlot, bowSlot;
    TextMeshProUGUI arrowText;
    Image crosshair, hitmarker;
    float hitmarkerTimer;

    // feedback
    RectTransform bannerRoot, toastRoot, feedRoot;
    TextMeshProUGUI bannerTitle, bannerSub, fpsText, hintText;
    float bannerTimer, bannerDuration, hintTimer = 14f;
    readonly List<(RectTransform rt, float born, float life)> transients = new List<(RectTransform, float, float)>();
    readonly List<Image> threatPool = new List<Image>();
    readonly List<(RectTransform root, Image fill)> barPool = new List<(RectTransform, Image)>();
    readonly List<(TextMeshProUGUI t, Vector3 world, float born)> numbers = new List<(TextMeshProUGUI, Vector3, float)>();

    // menus
    UIPage pausePage, settingsPage, helpPage, endPage;
    RectTransform shade;

    PlayerHealth playerHealth;
    PlayerControlSpeed player;
    GameManager gm;
    Camera cam;
    float fpsTimer; int fpsFrames;

    // ------------------------------------------------------------ static API (safe to call any time)

    public static void Banner(string title, string subtitle, float duration)
    {
        if (instance == null) return;
        instance.bannerTitle.text = title;
        instance.bannerSub.text = subtitle;
        instance.bannerSub.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
        instance.bannerDuration = instance.bannerTimer = duration;
        instance.bannerRoot.gameObject.SetActive(true);
    }

    public static void Announce(string text, Color color) => Toast(text, color, "skull", 2.8f, 40);

    public static void Toast(string text, Color color, string icon = null, float life = 1.8f, float size = 30)
    {
        if (instance == null) return;
        // don't stack identical messages
        foreach (var tr in instance.transients)
            if (tr.rt != null && tr.rt.parent == instance.toastRoot && tr.rt.GetComponentInChildren<TextMeshProUGUI>().text == text) return;
        var row = UIKit.HList(instance.toastRoot, 10);
        row.Layout(46);
        row.childForceExpandHeight = false;
        if (icon != null) UIKit.Icon(row.transform, icon, 38).Layout(38, 38);
        var t = UIKit.Text(row.transform, text, size, color);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        var fit = t.gameObject.AddComponent<ContentSizeFitter>(); fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        instance.transients.Add(((RectTransform)row.transform, Time.unscaledTime, life));
    }

    // ---- boon choice: three parchment cards, time frozen until one is picked
    RectTransform boonRoot;
    public static bool BoonsOpen => instance != null && instance.boonRoot != null;
    System.Action<BoonId> boonCallback;
    Boons.Def[] boonChoices;

    public static void ShowBoons(Boons.Def[] choices, System.Action<BoonId> onPick)
    {
        if (instance == null) { onPick?.Invoke(choices[0].id); return; }
        instance.BuildBoonCards(choices, onPick);
    }

    /// <summary>Picks a card as if clicked (used by the automated tests).</summary>
    public static void DebugPickBoon(int index)
    {
        if (instance == null || instance.boonChoices == null) return;
        instance.PickBoon(instance.boonChoices[Mathf.Clamp(index, 0, instance.boonChoices.Length - 1)].id);
    }

    void BuildBoonCards(Boons.Def[] choices, System.Action<BoonId> onPick)
    {
        if (boonRoot != null) Destroy(boonRoot.gameObject);
        bannerRoot.gameObject.SetActive(false);   // the cards take centre stage
        boonCallback = onPick;
        boonChoices = choices;
        boonRoot = UIKit.Rect("Boons", root).Fill();
        var dim = UIKit.Img(boonRoot, null, new Color(0.02f, 0.01f, 0.01f, 0.65f), "Dim");
        dim.rectTransform.Fill(); dim.raycastTarget = true;
        var title = UIKit.Text(boonRoot, "Choose a Boon", 64, UIKit.Gold, TextAlignmentOptions.Center, true);
        title.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 330), new Vector2(1200, 90));
        var row = UIKit.HList(boonRoot, 40);
        ((RectTransform)row.transform).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(1300, 460));
        row.childForceExpandWidth = false; row.childControlWidth = false;
        foreach (var def in choices)
        {
            var card = UIKit.Img(row.transform, "panel_parchment", Color.white, "Boon " + def.name);
            card.raycastTarget = true;
            card.rectTransform.sizeDelta = new Vector2(380, 460);
            card.Layout(460, 380);
            var icon = UIKit.Icon(card.transform, def.icon, 120);
            icon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(120, 120));
            var name = UIKit.Text(card.transform, def.name, 38, UIKit.Ink, TextAlignmentOptions.Center, true);
            name.fontSharedMaterial = name.font.material;
            name.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -180), new Vector2(340, 56));
            var desc = UIKit.Text(card.transform, def.description, 28, UIKit.Ink, TextAlignmentOptions.Top);
            desc.fontSharedMaterial = desc.font.material;
            desc.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -250), new Vector2(320, 140));
            int owned = Boons.Stacks(def.id);
            if (owned > 0)
            {
                var have = UIKit.Text(card.transform, $"Owned: {owned}", 24, new Color(0.45f, 0.1f, 0.08f), TextAlignmentOptions.Bottom);
                have.fontSharedMaterial = have.font.material;
                have.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(320, 34));
            }
            var b = card.gameObject.AddComponent<Button>();
            b.transition = Selectable.Transition.None;
            var id = def.id;
            b.onClick.AddListener(() => PickBoon(id));
            card.gameObject.AddComponent<UIHoverFx>().hoverScale = 1.06f;
        }
    }

    void PickBoon(BoonId id)
    {
        if (boonRoot != null) Destroy(boonRoot.gameObject);
        boonRoot = null;
        boonChoices = null;
        var cb = boonCallback;
        boonCallback = null;
        cb?.Invoke(id);
        RefreshBoonStrip();
    }

    RectTransform boonStrip;

    void RefreshBoonStrip()
    {
        if (boonStrip == null)
        {
            var h = UIKit.HList(root, 6, TextAnchor.UpperLeft);
            boonStrip = (RectTransform)h.transform;
            boonStrip.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(170, -150), new Vector2(600, 40));
            h.childForceExpandWidth = false; h.childControlWidth = false;
        }
        foreach (Transform c in boonStrip) Destroy(c.gameObject);
        foreach (var (def, n) in Boons.Owned)
        {
            var slot = UIKit.Img(boonStrip, "slot", Color.white, def.name);
            slot.rectTransform.sizeDelta = new Vector2(40, 40);
            var i = UIKit.Icon(slot.transform, def.icon, 30);
            i.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            if (n > 1)
            {
                var t = UIKit.Text(slot.transform, n.ToString(), 18, UIKit.Gold, TextAlignmentOptions.BottomRight);
                t.rectTransform.Fill(1);
            }
        }
    }

    public static void HitMarker(bool headshot)
    {
        if (instance == null) return;
        instance.hitmarkerTimer = headshot ? 0.35f : 0.2f;
        instance.hitmarker.color = headshot ? new Color(1f, 0.3f, 0.2f) : Color.white;
        if (headshot) Toast("Headshot!", new Color(1f, 0.45f, 0.3f), "arrow", 1.1f, 28);
    }

    // ------------------------------------------------------------ build

    void Awake()
    {
        instance = this;
        canvas = UIKit.Canvas("HUD Canvas", 10);
        canvas.transform.SetParent(transform, false);
        root = (RectTransform)canvas.transform;

        BuildVignette();
        BuildVitals();
        BuildObjective();
        BuildBossBar();
        BuildWeapons();
        BuildCrosshair();
        BuildFeedback();
        BuildMenus();
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
        EnemyController.Damaged -= OnEnemyDamaged;
        if (gm != null) { gm.Killed -= OnKilled; gm.MatchEnded -= OnMatchEnded; gm.PauseChanged -= OnPauseChanged; }
        if (playerHealth != null) playerHealth.Damaged -= OnPlayerDamaged;
    }

    void Start()
    {
        cam = Camera.main;
        var p = GameObject.FindWithTag("Player");
        if (p != null) { playerHealth = p.GetComponent<PlayerHealth>(); player = p.GetComponent<PlayerControlSpeed>(); }
        gm = GameManager.instance;
        if (gm != null) { gm.Killed += OnKilled; gm.MatchEnded += OnMatchEnded; gm.PauseChanged += OnPauseChanged; }
        if (playerHealth != null) playerHealth.Damaged += OnPlayerDamaged;
        EnemyController.Damaged += OnEnemyDamaged;
        if (gm != null) objectiveIcon.sprite = UIKit.Sprite("icon_" + (gm.Mode == GameMode.DefendTheCastle ? "swords" : gm.Mode == GameMode.EndlessHorde ? "horde" : "hourglass"));
    }

    void BuildVignette()
    {
        vignette = UIKit.Img(root, "vignette", new Color(0.7f, 0f, 0f, 0f), "Damage Vignette");
        vignette.rectTransform.Fill();
    }

    void BuildVitals()
    {
        var group = UIKit.Rect("Vitals", root).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -26), new Vector2(760, 170));

        // heraldic shield in the player's colours
        emblemField = UIKit.Img(group, "emblem", Color.white, "Emblem");
        emblemField.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(128, 128));
        emblemField.preserveAspect = true;
        emblemCharge = UIKit.Img(emblemField.transform, "emblem_charge", Color.white, "Charge");
        emblemCharge.rectTransform.Fill();
        emblemCharge.preserveAspect = true;
        var tab = GameSettings.TabardColors[Mathf.Clamp(GameSettings.Tabard, 0, GameSettings.TabardColors.Length - 1)].color;
        emblemField.color = tab;
        emblemCharge.gameObject.SetActive(GameSettings.Crest != 1);
        if (GameSettings.Crest == 2) emblemCharge.rectTransform.localRotation = Quaternion.Euler(0, 0, 180);
        if (GameSettings.Crest == 3) emblemCharge.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);

        heroName = UIKit.Text(group, GameSettings.HeroName, 30, UIKit.Gold, TextAlignmentOptions.BottomLeft, true);
        heroName.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(140, 0), new Vector2(560, 40));

        // health: trough, ghost (recent damage), fill, ornate frame on top
        healthFrame = UIKit.Rect("Health", group).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(132, -42), new Vector2(480, 58));
        var trough = UIKit.Img(healthFrame, "bar_bg", Color.white, "Trough"); trough.rectTransform.Fill(); trough.rectTransform.offsetMin = new Vector2(30, 14); trough.rectTransform.offsetMax = new Vector2(-30, -14);
        healthGhost = BarFill(trough.transform, new Color(1f, 0.85f, 0.4f, 0.9f));
        healthFill = BarFill(trough.transform, new Color(0.82f, 0.08f, 0.1f));
        var frame = UIKit.Img(healthFrame, "bar_frame", Color.white, "Frame"); frame.rectTransform.Fill();
        var heart = UIKit.Icon(healthFrame, "heart", 52);
        heart.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(4, 0), new Vector2(52, 52));
        healthText = UIKit.Text(healthFrame, "100", 24, Color.white);
        healthText.rectTransform.Fill();

        var stam = UIKit.Rect("Stamina", group).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(146, -98), new Vector2(340, 34));
        var st = UIKit.Img(stam, "bar_bg", Color.white, "Trough"); st.rectTransform.Fill(); st.rectTransform.offsetMin = new Vector2(20, 9); st.rectTransform.offsetMax = new Vector2(-20, -9);
        staminaFill = BarFill(st.transform, new Color(0.9f, 0.75f, 0.25f));
        var sf = UIKit.Img(stam, "bar_frame", Color.white, "Frame"); sf.rectTransform.Fill();
        var bolt = UIKit.Icon(stam, "stamina", 34);
        bolt.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(2, 0), new Vector2(34, 34));
    }

    static Image BarFill(Transform parent, Color color)
    {
        var f = UIKit.Img(parent, "bar_fill", color, "Fill");
        f.type = Image.Type.Filled;
        f.fillMethod = Image.FillMethod.Horizontal;
        f.rectTransform.Fill(2);
        return f;
    }

    void BuildObjective()
    {
        var plaque = UIKit.Img(root, "panel_iron", new Color(1, 1, 1, 0.95f), "Objective");
        plaque.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(40, -20), new Vector2(560, 74));
        objectiveIcon = UIKit.Icon(plaque.transform, "swords", 50);
        objectiveIcon.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(22, 0), new Vector2(50, 50));
        objectiveText = UIKit.Text(plaque.transform, "", 32, UIKit.Parchment);
        objectiveText.rectTransform.Fill(); objectiveText.rectTransform.offsetMin = new Vector2(80, 6); objectiveText.rectTransform.offsetMax = new Vector2(-24, -6);

        var scoreRow = UIKit.Rect("Score", root).Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-34, -26), new Vector2(420, 120));
        var crown = UIKit.Icon(scoreRow, "crown", 46);
        crown.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, 0), new Vector2(46, 46));
        scoreText = UIKit.Text(scoreRow, "0", 42, UIKit.Gold, TextAlignmentOptions.TopRight, true);
        scoreText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-56, 4), new Vector2(360, 52));
        comboText = UIKit.Text(scoreRow, "", 30, new Color(1f, 0.55f, 0.25f), TextAlignmentOptions.TopRight);
        comboText.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -52), new Vector2(400, 40));
        comboBar = UIKit.Img(scoreRow, "bar_fill", new Color(1f, 0.55f, 0.25f), "Combo Timer");
        comboBar.type = Image.Type.Filled; comboBar.fillMethod = Image.FillMethod.Horizontal; comboBar.fillOrigin = 1;
        comboBar.rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(0, -94), new Vector2(220, 8));
    }

    void BuildBossBar()
    {
        bossRoot = UIKit.Rect("Boss", root).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -112), new Vector2(820, 90));
        bossName = UIKit.Text(bossRoot, "The Warlord", 30, new Color(1f, 0.45f, 0.35f), TextAlignmentOptions.Bottom, true);
        bossName.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(800, 38));
        var bar = UIKit.Rect("Bar", bossRoot).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(820, 52));
        var trough = UIKit.Img(bar, "bar_bg", Color.white, "Trough"); trough.rectTransform.Fill(); trough.rectTransform.offsetMin = new Vector2(30, 13); trough.rectTransform.offsetMax = new Vector2(-30, -13);
        bossFill = BarFill(trough.transform, new Color(0.55f, 0.05f, 0.35f));
        UIKit.Img(bar, "bar_frame", Color.white, "Frame").rectTransform.Fill();
        bossRoot.gameObject.SetActive(false);
    }

    void BuildWeapons()
    {
        var group = UIKit.Rect("Weapons", root).Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-34, 30), new Vector2(320, 170));
        swordSlot = Slot(group, "sword", "1", new Vector2(-170, 34));
        bowSlot = Slot(group, "bow", "2", new Vector2(-40, 34));
        var arrowIcon = UIKit.Icon(group, "arrow", 40);
        arrowIcon.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-150, 0), new Vector2(40, 40));
        arrowText = UIKit.Text(group, "20", 30, UIKit.Parchment, TextAlignmentOptions.MidlineLeft);
        arrowText.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), new Vector2(-10, -2), new Vector2(140, 44));
    }

    Image Slot(RectTransform parent, string icon, string key, Vector2 pos)
    {
        var s = UIKit.Img(parent, "slot", Color.white, "Slot " + icon);
        s.rectTransform.Place(new Vector2(1, 0), new Vector2(1, 0), pos, new Vector2(112, 112));
        var i = UIKit.Icon(s.transform, icon, 80);
        i.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80, 80));
        var k = UIKit.Text(s.transform, key, 24, UIKit.Gold, TextAlignmentOptions.TopLeft, true);
        k.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -6), new Vector2(40, 32));
        return s;
    }

    void BuildCrosshair()
    {
        crosshair = UIKit.Img(root, "crosshair", Color.white, "Crosshair");
        crosshair.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56, 56));
        hitmarker = UIKit.Img(root, "hitmarker", Color.white, "Hitmarker");
        hitmarker.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48, 48));
        hitmarker.enabled = false;
    }

    void BuildFeedback()
    {
        bannerRoot = UIKit.Rect("Banner", root).Place(new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(1100, 210));
        var ribbon = UIKit.Img(bannerRoot, "banner", Color.white, "Ribbon");
        ribbon.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(1100, 160));
        bannerTitle = UIKit.Text(ribbon.transform, "", 60, UIKit.Gold, TextAlignmentOptions.Center, true);
        bannerTitle.rectTransform.Fill(); bannerTitle.rectTransform.offsetMin = new Vector2(150, 28); bannerTitle.rectTransform.offsetMax = new Vector2(-150, -8);
        bannerTitle.enableAutoSizing = true; bannerTitle.fontSizeMax = 60; bannerTitle.fontSizeMin = 30;
        bannerSub = UIKit.Text(bannerRoot, "", 32, UIKit.Parchment);
        bannerSub.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, new Vector2(1000, 50));
        bannerRoot.gameObject.SetActive(false);

        var toasts = UIKit.VList(root, 6, null, TextAnchor.LowerCenter);
        toastRoot = (RectTransform)toasts.transform;
        toastRoot.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 230), new Vector2(900, 300));
        toasts.childForceExpandWidth = false;
        toasts.childControlWidth = true;

        var feed = UIKit.VList(root, 4, null, TextAnchor.LowerLeft);
        feedRoot = (RectTransform)feed.transform;
        feedRoot.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(34, 30), new Vector2(520, 260));

        hintText = UIKit.Text(root, "WASD move  ·  Shift sprint  ·  Space dodge  ·  LMB attack  ·  RMB block / aim  ·  Q swap weapon  ·  Esc pause", 22, new Color(1, 1, 1, 0.8f));
        hintText.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 12), new Vector2(1500, 34));

        fpsText = UIKit.Text(root, "", 20, Color.white, TextAlignmentOptions.TopLeft);
        fpsText.rectTransform.Place(new Vector2(0, 0), new Vector2(0, 0), new Vector2(8, 300), new Vector2(200, 30));
    }

    // ------------------------------------------------------------ menus

    void BuildMenus()
    {
        shade = UIKit.Img(root, null, UIKit.Shade, "Shade").rectTransform;
        shade.GetComponent<Image>().raycastTarget = true;
        shade.Fill();
        shade.gameObject.SetActive(false);

        pausePage = SharedPanels.Page(root, "Pause", new Vector2(620, 760), "Paused");
        var list = UIKit.VList(pausePage.transform, 16, null, TextAnchor.UpperCenter);
        ((RectTransform)list.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -160), new Vector2(380, 560));
        list.childForceExpandWidth = false; list.childControlWidth = false;
        UIKit.Button(list.transform, "Resume", () => gm?.Resume());
        UIKit.Button(list.transform, "Settings", () => Swap(pausePage, settingsPage));
        UIKit.Button(list.transform, "How to Play", () => Swap(pausePage, helpPage));
        UIKit.Button(list.transform, "Restart", () => gm?.RestartGame());
        UIKit.Button(list.transform, "Main Menu", () => gm?.QuitGame());

        settingsPage = SharedPanels.Settings(root, () => Swap(settingsPage, pausePage));
        helpPage = SharedPanels.HowToPlay(root, () => Swap(helpPage, pausePage));
    }

    static void Swap(UIPage from, UIPage to) { from.Hide(); to.Show(); }

    void OnPauseChanged(bool paused)
    {
        shade.gameObject.SetActive(paused);
        if (paused) pausePage.Show();
        else { pausePage.Hide(); settingsPage.Hide(); helpPage.Hide(); }
    }

    void OnMatchEnded(bool victory)
    {
        shade.gameObject.SetActive(true);
        bannerRoot.gameObject.SetActive(false);
        string title = victory ? "Victory" : gm.Mode == GameMode.EndlessHorde ? "Your Legend Ends" : "Defeat";
        endPage = SharedPanels.Page(root, "End", new Vector2(1000, 860), title);
        endPage.transform.SetAsLastSibling();

        var sub = UIKit.Text(endPage.transform, GameSettings.ModeName(gm.Mode) + "  ·  " + GameSettings.Difficulty, 28, UIKit.Parchment);
        sub.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -140), new Vector2(900, 40));
        var score = UIKit.Text(endPage.transform, gm.Score.ToString("N0"), 76, UIKit.Gold, TextAlignmentOptions.Center, true);
        score.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -188), new Vector2(900, 90));
        var best = UIKit.Text(endPage.transform, gm.NewRecord ? "New record!" : "Best: " + GameSettings.HighScore(gm.Mode).ToString("N0"), 28, gm.NewRecord ? new Color(1f, 0.6f, 0.3f) : UIKit.GoldDim);
        best.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -280), new Vector2(900, 40));

        var stats = UIKit.Rect("Stats", endPage.transform).Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -340), new Vector2(820, 330));
        var grid = stats.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(400, 44); grid.spacing = new Vector2(20, 4);
        int total = gm.currentKills;
        float acc = gm.ArrowsFired > 0 ? gm.ArrowsHit / (float)gm.ArrowsFired : 0f;
        var lines = new List<(string, string)>
        {
            ("Undead slain", total.ToString()),
            ("Time", $"{Mathf.FloorToInt(gm.MatchTime / 60f)}:{Mathf.FloorToInt(gm.MatchTime % 60f):00}"),
            ("Best combo", "x" + gm.BestCombo),
            ("Arrow accuracy", gm.ArrowsFired > 0 ? Mathf.RoundToInt(acc * 100) + "%" : "-"),
            ("Headshots", gm.Headshots.ToString()),
            ("Damage taken", Mathf.RoundToInt(gm.DamageTaken).ToString()),
        };
        if (gm.Mode == GameMode.EndlessHorde) lines.Insert(1, ("Wave reached", gm.Wave.ToString()));
        foreach (var kv in gm.killsByType)
            if (kv.Value > 0) lines.Add((ZombieTypes.Get(kv.Key).name + "s", kv.Value.ToString()));
        foreach (var (k, v) in lines)
        {
            var cell = UIKit.Rect("Cell", stats);
            var a = UIKit.Text(cell, k, 26, UIKit.Parchment, TextAlignmentOptions.MidlineLeft); a.rectTransform.Fill();
            var b = UIKit.Text(cell, v, 26, UIKit.Gold, TextAlignmentOptions.MidlineRight); b.rectTransform.Fill();
        }

        var buttons = UIKit.HList(endPage.transform, 30);
        ((RectTransform)buttons.transform).Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 46), new Vector2(760, 72));
        buttons.childForceExpandWidth = false; buttons.childControlWidth = false;
        UIKit.Button(buttons.transform, "Play Again", () => gm.RestartGame(), 340);
        UIKit.Button(buttons.transform, "Main Menu", () => gm.QuitGame(), 340);
        endPage.Show();
    }

    // ------------------------------------------------------------ events

    void OnPlayerDamaged(float amount)
    {
        vignetteAlpha = Mathf.Min(0.75f, vignetteAlpha + 0.25f + amount / 60f);
        healthFrame.localScale = Vector3.one * 1.05f;
    }

    void OnKilled(EnemyController e, int points)
    {
        string name = e != null && e.Stats != null ? e.Stats.name : "Zombie";
        var row = UIKit.HList(feedRoot, 8, TextAnchor.MiddleLeft);
        row.Layout(36);
        row.childForceExpandHeight = false;
        UIKit.Icon(row.transform, "skull", 30).Layout(30, 30);
        var t = UIKit.Text(row.transform, $"{name} slain  <color=#F5CB73>+{points}</color>", 24, UIKit.Parchment, TextAlignmentOptions.MidlineLeft);
        t.Layout(36, 440);
        transients.Add(((RectTransform)row.transform, Time.unscaledTime, 4f));
        while (feedRoot.childCount > 6) DestroyImmediate(feedRoot.GetChild(0).gameObject);
        scoreText.transform.localScale = Vector3.one * 1.15f;
    }

    void OnEnemyDamaged(EnemyController e, float amount, Vector3 at)
    {
        if (!GameSettings.ShowDamageNumbers) return;
        var t = UIKit.Text(root, Mathf.Max(1, Mathf.RoundToInt(amount * 10)).ToString(), amount >= 2f ? 38 : 30, amount >= 2f ? new Color(1f, 0.6f, 0.2f) : Color.white, TextAlignmentOptions.Center, true);
        t.rectTransform.sizeDelta = new Vector2(160, 50);
        numbers.Add((t, at + new Vector3(Random.Range(-0.3f, 0.3f), 0.2f, Random.Range(-0.3f, 0.3f)), Time.unscaledTime));
        if (numbers.Count > 24) { Destroy(numbers[0].t.gameObject); numbers.RemoveAt(0); }
    }

    // ------------------------------------------------------------ per frame

    void Update()
    {
        if (cam == null) cam = Camera.main;
        float dt = Time.unscaledDeltaTime;

        if (playerHealth != null)
        {
            float f = playerHealth.Fraction;
            healthFill.fillAmount = Mathf.Lerp(healthFill.fillAmount, f, 1f - Mathf.Exp(-14f * dt));
            ghost = ghost > f ? Mathf.MoveTowards(ghost, f, dt * 0.35f) : f;
            healthGhost.fillAmount = ghost;
            healthText.text = Mathf.CeilToInt(playerHealth.currentHealth) + " / " + Mathf.RoundToInt(playerHealth.maxHealth);
            // low health heartbeat
            float low = f < 0.3f && !playerHealth.IsDead ? (0.25f + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f) * (1f - f / 0.3f) : 0f;
            vignetteAlpha = Mathf.Max(low, vignetteAlpha - dt * 0.9f);
            vignette.color = new Color(0.7f, 0f, 0f, vignetteAlpha);
            healthFrame.localScale = Vector3.Lerp(healthFrame.localScale, Vector3.one, 1f - Mathf.Exp(-10f * dt));
        }

        if (player != null)
        {
            staminaFill.fillAmount = player.Stamina / player.maxStamina;
            staminaFill.color = player.Exhausted ? new Color(0.6f, 0.35f, 0.2f) : new Color(0.9f, 0.75f, 0.25f);
            bool bow = player.CurrentWeapon == Weapon.Bow;
            swordSlot.sprite = UIKit.Sprite(bow ? "slot" : "slot_active");
            bowSlot.sprite = UIKit.Sprite(bow ? "slot_active" : "slot");
            swordSlot.rectTransform.localScale = Vector3.Lerp(swordSlot.rectTransform.localScale, Vector3.one * (bow ? 0.86f : 1f), 1f - Mathf.Exp(-12f * dt));
            bowSlot.rectTransform.localScale = Vector3.Lerp(bowSlot.rectTransform.localScale, Vector3.one * (bow ? 1f : 0.86f), 1f - Mathf.Exp(-12f * dt));
            arrowText.text = player.Arrows + " / " + player.maxArrows;
            arrowText.color = player.Arrows == 0 ? new Color(1f, 0.4f, 0.3f) : UIKit.Parchment;
            crosshair.enabled = bow && !(gm != null && (gm.IsPaused || gm.State == GameManager.MatchState.Won || gm.State == GameManager.MatchState.Lost));
            float spread = bow ? Mathf.Lerp(1.5f, 0.75f, player.DrawAmount) * (player.IsAiming ? 0.85f : 1f) : 1f;
            crosshair.rectTransform.localScale = Vector3.one * spread;
            crosshair.color = player.DrawAmount >= 0.99f ? new Color(1f, 0.85f, 0.4f) : Color.white;
        }

        hitmarkerTimer -= dt;
        hitmarker.enabled = hitmarkerTimer > 0f;

        if (gm != null)
        {
            objectiveText.text = gm.Objective;
            scoreText.text = gm.Score.ToString("N0");
            scoreText.transform.localScale = Vector3.Lerp(scoreText.transform.localScale, Vector3.one, 1f - Mathf.Exp(-8f * dt));
            comboText.text = gm.Combo >= 2 ? $"{gm.Combo} kill streak  x{gm.ComboMultiplier}" : "";
            comboBar.fillAmount = gm.Combo >= 2 ? gm.ComboTimeLeft / Boons.ComboWindow : 0f;
            var boss = gm.Boss;
            bool showBoss = boss != null && !boss.IsDead;
            bossRoot.gameObject.SetActive(showBoss);
            if (showBoss)
            {
                bossName.text = boss.Stats.name == "Warlord" ? "The Warlord" : boss.Stats.name + " Champion";
                bossFill.fillAmount = Mathf.Lerp(bossFill.fillAmount, boss.HealthFraction, 1f - Mathf.Exp(-8f * dt));
            }
        }

        UpdateBanner(dt);
        UpdateTransients();
        UpdateNumbers();
        UpdateEnemyBars();
        UpdateThreats();

        hintTimer -= dt;
        if (hintTimer > 0f) hintText.text = GameInput.ControlsHint;
        hintText.alpha = Mathf.Clamp01(hintTimer / 2f) * 0.85f;

        fpsText.gameObject.SetActive(GameSettings.ShowFps);
        if (GameSettings.ShowFps)
        {
            fpsFrames++; fpsTimer += dt;
            if (fpsTimer > 0.5f) { fpsText.text = Mathf.RoundToInt(fpsFrames / fpsTimer) + " fps"; fpsFrames = 0; fpsTimer = 0; }
        }
    }

    void UpdateBanner(float dt)
    {
        if (!bannerRoot.gameObject.activeSelf) return;
        bannerTimer -= dt;
        float age = bannerDuration - bannerTimer;
        float a = Mathf.Clamp01(age / 0.25f) * Mathf.Clamp01(bannerTimer / 0.4f);
        float s = Mathf.Lerp(1.25f, 1f, Mathf.Clamp01(age / 0.25f));
        bannerRoot.localScale = Vector3.one * s;
        foreach (var g in bannerRoot.GetComponentsInChildren<Graphic>()) { var c = g.color; c.a = a; g.color = c; }
        if (bannerTimer <= 0f) bannerRoot.gameObject.SetActive(false);
    }

    void UpdateTransients()
    {
        for (int i = transients.Count - 1; i >= 0; i--)
        {
            var (rt, born, life) = transients[i];
            if (rt == null) { transients.RemoveAt(i); continue; }
            float age = Time.unscaledTime - born;
            float a = Mathf.Clamp01(age / 0.15f) * Mathf.Clamp01((life - age) / 0.4f);
            foreach (var g in rt.GetComponentsInChildren<Graphic>()) { var c = g.color; c.a = a; g.color = c; }
            if (age > life) { Destroy(rt.gameObject); transients.RemoveAt(i); }
        }
    }

    void UpdateNumbers()
    {
        for (int i = numbers.Count - 1; i >= 0; i--)
        {
            var (t, world, born) = numbers[i];
            float age = Time.unscaledTime - born;
            if (t == null || age > 0.9f || cam == null) { if (t != null) Destroy(t.gameObject); numbers.RemoveAt(i); continue; }
            Vector3 sp = cam.WorldToScreenPoint(world + Vector3.up * age * 1.2f);
            t.gameObject.SetActive(sp.z > 0);
            if (sp.z > 0) t.rectTransform.position = sp;
            t.alpha = Mathf.Clamp01((0.9f - age) / 0.3f);
            t.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.4f, 1f, Mathf.Clamp01(age * 6f));
        }
    }

    // floating health bars above recently hurt zombies
    void UpdateEnemyBars()
    {
        int used = 0;
        if (cam != null)
        {
            foreach (var e in EnemyController.All)
            {
                if (e == null || e.IsDead || e == gm?.Boss || Time.time - e.LastHitTime > 3f) continue;
                Vector3 sp = cam.WorldToScreenPoint(e.HeadPosition + Vector3.up * 0.3f);
                if (sp.z <= 0 || sp.z > 40f) continue;
                if (used >= barPool.Count)
                {
                    var r = UIKit.Rect("Enemy Bar", root);
                    r.sizeDelta = new Vector2(90, 12);
                    var bg = UIKit.Img(r, "bar_bg", new Color(1, 1, 1, 0.9f), "Bg"); bg.rectTransform.Fill();
                    var fill = BarFill(r, new Color(0.85f, 0.15f, 0.12f));
                    r.SetSiblingIndex(1);
                    barPool.Add((r, fill));
                }
                var (rt, f) = barPool[used++];
                rt.gameObject.SetActive(true);
                rt.position = sp;
                rt.localScale = Vector3.one * Mathf.Clamp(12f / sp.z, 0.6f, 1.4f);
                f.fillAmount = e.HealthFraction;
            }
        }
        for (int i = used; i < barPool.Count; i++) barPool[i].root.gameObject.SetActive(false);
    }

    // red chevrons at the screen edge for close zombies you can't see
    void UpdateThreats()
    {
        int used = 0;
        if (cam != null && player != null)
        {
            foreach (var e in EnemyController.All)
            {
                if (e == null || e.IsDead) continue;
                Vector3 to = e.transform.position - player.transform.position;
                if (to.magnitude > 11f) continue;
                Vector3 vp = cam.WorldToViewportPoint(e.transform.position + Vector3.up);
                if (vp.z > 0 && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f) continue;
                if (used >= 6) break;
                if (used >= threatPool.Count)
                {
                    var img = UIKit.Icon(root, "threat", 54);
                    threatPool.Add(img);
                }
                var t = threatPool[used++];
                t.gameObject.SetActive(true);
                Vector3 local = cam.transform.InverseTransformDirection(to);
                float ang = Mathf.Atan2(local.x, local.z);
                Vector2 dir = new Vector2(Mathf.Sin(ang), Mathf.Cos(ang));
                t.rectTransform.anchoredPosition = Vector2.zero;
                t.rectTransform.anchorMin = t.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                t.rectTransform.anchoredPosition = dir * 300f;
                t.rectTransform.localRotation = Quaternion.Euler(0, 0, -ang * Mathf.Rad2Deg);
                t.color = new Color(1f, 0.2f, 0.15f, Mathf.Lerp(1f, 0.35f, to.magnitude / 11f));
            }
        }
        for (int i = used; i < threatPool.Count; i++) threatPool[i].gameObject.SetActive(false);
    }
}
