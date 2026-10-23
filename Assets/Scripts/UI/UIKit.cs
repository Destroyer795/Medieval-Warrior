using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Builds the medieval UI in code: gold-trimmed panels, parchment, banners, styled controls.
public static class UIKit
{
    public static readonly Color Gold = new Color(0.96f, 0.8f, 0.45f);
    public static readonly Color GoldDim = new Color(0.78f, 0.6f, 0.3f);
    public static readonly Color Parchment = new Color(0.95f, 0.9f, 0.8f);
    public static readonly Color Ink = new Color(0.22f, 0.13f, 0.08f);
    public static readonly Color Blood = new Color(0.78f, 0.1f, 0.1f);
    public static readonly Color Shade = new Color(0.03f, 0.02f, 0.02f, 0.72f);

    static TMP_FontAsset titleFont, bodyFont;
    static Material titleMat, bodyMat;
    static readonly Dictionary<string, Sprite> sprites = new Dictionary<string, Sprite>();

    public static TMP_FontAsset TitleFont { get { LoadFonts(); return titleFont; } }
    public static TMP_FontAsset BodyFont { get { LoadFonts(); return bodyFont; } }

    static void LoadFonts()
    {
        if (titleFont != null && bodyFont != null) return;
        titleFont = MakeFont("Fonts/CinzelDecorative-Bold");
        bodyFont = MakeFont("Fonts/MedievalSharp");
        if (titleFont == null) titleFont = TMP_Settings.defaultFontAsset;
        if (bodyFont == null) bodyFont = TMP_Settings.defaultFontAsset;
        titleMat = Styled(titleFont, 0.18f, 0.9f);
        bodyMat = Styled(bodyFont, 0.12f, 0.75f);
    }

    static TMP_FontAsset MakeFont(string path)
    {
        var f = Resources.Load<Font>(path);
        if (f == null) return null;
        var fa = TMP_FontAsset.CreateFontAsset(f);
        if (fa != null) fa.name = f.name;
        return fa;
    }

    static Material Styled(TMP_FontAsset font, float outline, float underlay)
    {
        if (font == null || font.material == null) return null;
        var m = new Material(font.material) { name = font.name + " Styled" };
        m.SetFloat(ShaderUtilities.ID_OutlineWidth, outline);
        m.SetColor(ShaderUtilities.ID_OutlineColor, new Color(0.08f, 0.04f, 0.02f, 1f));
        m.EnableKeyword(ShaderUtilities.Keyword_Underlay);
        m.SetColor(ShaderUtilities.ID_UnderlayColor, new Color(0, 0, 0, underlay));
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetX, 0.5f);
        m.SetFloat(ShaderUtilities.ID_UnderlayOffsetY, -0.6f);
        m.SetFloat(ShaderUtilities.ID_UnderlaySoftness, 0.4f);
        return m;
    }

    public static Sprite Sprite(string name)
    {
        if (!sprites.TryGetValue(name, out var s) || s == null)
        {
            s = Resources.Load<Sprite>("UI/" + name);
            sprites[name] = s;
        }
        return s;
    }

    // ------------------------------------------------------------ scaffolding

    public static Canvas Canvas(string name, int order)
    {
        EnsureEventSystem();
        var go = new GameObject(name, typeof(RectTransform));
        var c = go.AddComponent<Canvas>();
        c.renderMode = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = order;
        var scaler = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        go.AddComponent<GraphicRaycaster>();
        go.AddComponent<GamepadUISelector>();
        return c;
    }

    public static void EnsureEventSystem()
    {
        if (EventSystem.current != null || UnityEngine.Object.FindFirstObjectByType<EventSystem>() != null) return;
        var es = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        UnityEngine.Object.DontDestroyOnLoad(es);
    }

    public static RectTransform Rect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = pivot;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return rt;
    }

    public static RectTransform Fill(this RectTransform rt, float pad = 0f)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(pad, pad); rt.offsetMax = new Vector2(-pad, -pad);
        return rt;
    }

    public static Image Img(Transform parent, string sprite, Color? color = null, string name = null)
    {
        var rt = Rect(name ?? sprite ?? "Image", parent);
        var img = rt.gameObject.AddComponent<Image>();
        img.sprite = sprite == null ? null : Sprite(sprite);
        img.color = color ?? Color.white;
        if (img.sprite != null && img.sprite.border != Vector4.zero) img.type = Image.Type.Sliced;
        img.raycastTarget = false;
        return img;
    }

    public static TextMeshProUGUI Text(Transform parent, string text, float size, Color? color = null, TextAlignmentOptions align = TextAlignmentOptions.Center, bool title = false)
    {
        LoadFonts();
        var rt = Rect("Text", parent);
        var t = rt.gameObject.AddComponent<TextMeshProUGUI>();
        t.font = title ? titleFont : bodyFont;
        var mat = title ? titleMat : bodyMat;
        if (mat != null) t.fontSharedMaterial = mat;
        t.text = text;
        t.fontSize = size;
        t.color = color ?? Parchment;
        t.alignment = align;
        t.raycastTarget = false;
        t.textWrappingMode = TextWrappingModes.Normal;
        return t;
    }

    public static LayoutElement Layout(this Component c, float prefHeight = -1, float prefWidth = -1, float flexW = -1, float flexH = -1)
    {
        if (!c.TryGetComponent(out LayoutElement le)) le = c.gameObject.AddComponent<LayoutElement>();
        if (prefHeight >= 0) { le.preferredHeight = prefHeight; le.minHeight = prefHeight; }
        if (prefWidth >= 0) { le.preferredWidth = prefWidth; le.minWidth = prefWidth; }
        if (flexW >= 0) le.flexibleWidth = flexW;
        if (flexH >= 0) le.flexibleHeight = flexH;
        return le;
    }

    public static VerticalLayoutGroup VList(Transform parent, float spacing, RectOffset pad = null, TextAnchor align = TextAnchor.UpperCenter)
    {
        var rt = Rect("List", parent);
        var v = rt.gameObject.AddComponent<VerticalLayoutGroup>();
        v.spacing = spacing;
        v.padding = pad ?? new RectOffset(0, 0, 0, 0);
        v.childAlignment = align;
        v.childControlHeight = true; v.childControlWidth = true;
        v.childForceExpandHeight = false; v.childForceExpandWidth = true;
        return v;
    }

    public static HorizontalLayoutGroup HList(Transform parent, float spacing, TextAnchor align = TextAnchor.MiddleCenter)
    {
        var rt = Rect("Row", parent);
        var h = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
        h.spacing = spacing;
        h.childAlignment = align;
        h.childControlHeight = true; h.childControlWidth = true;
        h.childForceExpandHeight = true; h.childForceExpandWidth = false;
        return h;
    }

    public static Image Panel(Transform parent, Vector2 size, string sprite = "panel_dark", float alpha = 0.97f)
    {
        var img = Img(parent, sprite, new Color(1, 1, 1, alpha), "Panel");
        img.raycastTarget = true;
        img.rectTransform.sizeDelta = size;
        return img;
    }

    public static Image Divider(Transform parent, float width = 420)
    {
        var d = Img(parent, "divider", Color.white, "Divider");
        d.preserveAspect = true;
        d.Layout(22, width);
        return d;
    }

    // ------------------------------------------------------------ controls

    public static Button Button(Transform parent, string label, Action onClick, float width = 360, float height = 72, float fontSize = 34, string style = "button")
    {
        var img = Img(parent, style, Color.white, "Button " + label);
        img.raycastTarget = true;
        img.rectTransform.sizeDelta = new Vector2(width, height);
        var b = img.gameObject.AddComponent<Button>();
        b.transition = Selectable.Transition.SpriteSwap;
        // "selected" (gamepad focus) looks like hover so controller players can see where they are
        b.spriteState = new SpriteState { highlightedSprite = Sprite("button_hover"), selectedSprite = Sprite("button_hover"), pressedSprite = Sprite("button_pressed") };
        var nav = b.navigation; nav.mode = Navigation.Mode.Automatic; b.navigation = nav;
        var t = Text(img.transform, label, fontSize, Gold);
        t.rectTransform.Fill(6);
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.enableAutoSizing = true; t.fontSizeMax = fontSize; t.fontSizeMin = 14;
        if (onClick != null) b.onClick.AddListener(() => onClick());
        img.gameObject.AddComponent<UIHoverFx>();
        img.Layout(height, width);
        return b;
    }

    public static TextMeshProUGUI Label(Transform row, string label, float width = 300)
    {
        var t = Text(row, label, 28, Parchment, TextAlignmentOptions.MidlineLeft);
        t.Layout(-1, width);
        return t;
    }

    public static Slider Slider(Transform parent, string label, float min, float max, float value, Action<float> onChange, Func<float, string> format = null)
    {
        var row = HList(parent, 18);
        row.Layout(52);
        Label(row.transform, label);
        var holder = Rect("Slider", row.transform);
        holder.Layout(-1, 360);
        var bg = Img(holder, "bar_bg", Color.white, "Background"); bg.type = Image.Type.Sliced;
        bg.rectTransform.anchorMin = new Vector2(0, 0.5f); bg.rectTransform.anchorMax = new Vector2(1, 0.5f);
        bg.rectTransform.sizeDelta = new Vector2(0, 16);
        var fillArea = Rect("Fill Area", holder);
        fillArea.anchorMin = new Vector2(0, 0.5f); fillArea.anchorMax = new Vector2(1, 0.5f); fillArea.sizeDelta = new Vector2(-8, 12);
        var fill = Img(fillArea, "bar_fill", GoldDim, "Fill");
        fill.type = Image.Type.Simple;
        fill.rectTransform.sizeDelta = Vector2.zero;
        var handleArea = Rect("Handle Area", holder).Fill();
        handleArea.offsetMin = new Vector2(14, 0); handleArea.offsetMax = new Vector2(-14, 0);
        var handle = Img(handleArea, "knob", Color.white, "Handle");
        handle.rectTransform.sizeDelta = new Vector2(38, 38);
        handle.raycastTarget = true;
        var s = holder.gameObject.AddComponent<Slider>();
        s.fillRect = fill.rectTransform; s.handleRect = handle.rectTransform; s.targetGraphic = handle;
        s.minValue = min; s.maxValue = max; s.value = value;
        var nav = s.navigation; nav.mode = Navigation.Mode.Automatic; s.navigation = nav;
        var readout = Text(row.transform, "", 26, Gold, TextAlignmentOptions.MidlineRight);
        readout.Layout(-1, 90);
        format ??= v => Mathf.RoundToInt(v * 100) + "%";
        readout.text = format(value);
        s.onValueChanged.AddListener(v => { readout.text = format(v); onChange?.Invoke(v); });
        return s;
    }

    public static Toggle Toggle(Transform parent, string label, bool value, Action<bool> onChange)
    {
        var row = HList(parent, 18);
        row.Layout(52);
        Label(row.transform, label);
        var box = Img(row.transform, "checkbox", Color.white, "Checkbox");
        box.raycastTarget = true;
        box.Layout(44, 44);
        var check = Img(box.transform, "check", Color.white, "Check");
        check.rectTransform.Fill(4);
        var spacer = Rect("Spacer", row.transform); spacer.Layout(-1, 1, 1);
        var t = box.gameObject.AddComponent<Toggle>();
        t.graphic = check; t.targetGraphic = box; t.isOn = value;
        var nav = t.navigation; nav.mode = Navigation.Mode.Automatic; t.navigation = nav;
        t.onValueChanged.AddListener(v => { AudioManager.Play2D("ui_click", 0.8f); onChange?.Invoke(v); });
        box.gameObject.AddComponent<UIHoverFx>().clickSound = false;
        return t;
    }

    public class SelectorHandle
    {
        public TextMeshProUGUI value;
        public Image swatch;
        public int index;
        public string[] options;
        public Func<int, Color?> swatchColor;
        public Action<int> onChange;

        public void Set(int i, bool notify = true)
        {
            index = (i % options.Length + options.Length) % options.Length;
            value.text = options[index];
            if (swatch != null)
            {
                var c = swatchColor?.Invoke(index);
                swatch.gameObject.SetActive(c.HasValue);
                if (c.HasValue) swatch.color = c.Value;
            }
            if (notify) onChange?.Invoke(index);
        }
    }

    public static SelectorHandle Selector(Transform parent, string label, string[] options, int index, Action<int> onChange, Func<int, Color?> swatch = null, float labelWidth = 300)
    {
        var row = HList(parent, 10);
        row.Layout(56);
        Label(row.transform, label, labelWidth);
        var h = new SelectorHandle { options = options, onChange = onChange, swatchColor = swatch };
        var left = Img(row.transform, "arrow_left", Color.white, "Prev"); left.raycastTarget = true; left.Layout(44, 44);
        var mid = Img(row.transform, "bar_bg", new Color(1, 1, 1, 0.85f), "Value"); mid.Layout(48, 300);
        if (swatch != null)
        {
            h.swatch = Img(mid.transform, "emblem", Color.white, "Swatch");
            h.swatch.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(34, 34));
            h.swatch.preserveAspect = true;
        }
        h.value = Text(mid.transform, "", 26, Gold);
        h.value.rectTransform.Fill(4);
        if (swatch != null) h.value.rectTransform.offsetMin = new Vector2(44, 4);
        var right = Img(row.transform, "arrow_right", Color.white, "Next"); right.raycastTarget = true; right.Layout(44, 44);
        var lb = left.gameObject.AddComponent<Button>(); lb.onClick.AddListener(() => h.Set(h.index - 1));
        var rb = right.gameObject.AddComponent<Button>(); rb.onClick.AddListener(() => h.Set(h.index + 1));
        foreach (var b in new[] { lb, rb }) { var n = b.navigation; n.mode = Navigation.Mode.Automatic; b.navigation = n; b.gameObject.AddComponent<UIHoverFx>(); }
        h.Set(index, false);
        return h;
    }

    public static TMP_InputField InputField(Transform parent, string label, string value, Action<string> onChange)
    {
        var row = HList(parent, 18);
        row.Layout(56);
        Label(row.transform, label);
        var bg = Img(row.transform, "bar_bg", Color.white, "Input");
        bg.raycastTarget = true;
        bg.Layout(48, 420);
        var area = Rect("Text Area", bg.transform).Fill(8);
        area.gameObject.AddComponent<RectMask2D>();
        var text = Text(area, "", 28, Gold, TextAlignmentOptions.MidlineLeft);
        text.rectTransform.Fill();
        text.textWrappingMode = TextWrappingModes.NoWrap;
        var placeholder = Text(area, "Enter a name...", 26, new Color(1, 1, 1, 0.35f), TextAlignmentOptions.MidlineLeft);
        placeholder.rectTransform.Fill();
        var input = bg.gameObject.AddComponent<TMP_InputField>();
        input.textViewport = area;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.fontAsset = BodyFont;
        input.pointSize = 28;
        input.characterLimit = 22;
        input.caretColor = Gold;
        input.customCaretColor = true;
        input.selectionColor = new Color(0.8f, 0.6f, 0.2f, 0.4f);
        input.text = value;
        input.onEndEdit.AddListener(v => onChange?.Invoke(v));
        return input;
    }

    public static Image Icon(Transform parent, string icon, float size, Color? color = null)
    {
        var i = Img(parent, "icon_" + icon, color ?? Color.white, "Icon " + icon);
        i.preserveAspect = true;
        i.rectTransform.sizeDelta = new Vector2(size, size);
        return i;
    }
}

// Little bit of juice for every clickable: grow on hover, sounds, all in unscaled time so it works while paused.
public class UIHoverFx : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public bool clickSound = true;
    public float hoverScale = 1.05f;
    float target = 1f;

    public void OnPointerEnter(PointerEventData e)
    {
        target = hoverScale;
        AudioManager.Play2D("ui_hover", 0.6f, UnityEngine.Random.Range(0.95f, 1.05f));
    }

    public void OnPointerExit(PointerEventData e) => target = 1f;

    public void OnPointerClick(PointerEventData e)
    {
        if (clickSound) AudioManager.Play2D("ui_click");
        transform.localScale = Vector3.one * 0.96f;
    }

    void OnDisable() { target = 1f; transform.localScale = Vector3.one; }

    void Update()
    {
        float s = Mathf.Lerp(transform.localScale.x, target, 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime));
        transform.localScale = new Vector3(s, s, 1f);
    }
}

// Simple fade/slide for whole screens.
public class UIPage : MonoBehaviour
{
    CanvasGroup group;
    float targetAlpha;

    CanvasGroup Group
    {
        get
        {
            if (group == null && !TryGetComponent(out group)) group = gameObject.AddComponent<CanvasGroup>();
            return group;
        }
    }

    public void Show(bool instant = false)
    {
        gameObject.SetActive(true);
        targetAlpha = 1f;
        Group.interactable = true; Group.blocksRaycasts = true;
        if (instant) Group.alpha = 1f;
        else { Group.alpha = 0f; transform.localScale = Vector3.one * 0.97f; }
    }

    public void Hide()
    {
        targetAlpha = 0f;
        Group.interactable = false; Group.blocksRaycasts = false;
        Group.alpha = 0f;
        gameObject.SetActive(false);
    }

    void Update()
    {
        float k = 1f - Mathf.Exp(-12f * Time.unscaledDeltaTime);
        Group.alpha = Mathf.Lerp(Group.alpha, targetAlpha, k);
        transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one, k);
    }
}
