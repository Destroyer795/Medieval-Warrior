using System.Collections.Generic;
using UnityEngine;

// Re-colours the knight's texture atlas from the saved customisation:
// tabard/shield cloth, armour metal and leather are masked separately so the
// knight keeps its hand-painted shading whatever colours are picked.
public class KnightAppearance : MonoBehaviour
{
    static Color[] basePixels, maskPixels;
    static int texWidth, texHeight;
    static readonly Dictionary<int, Texture2D> cache = new Dictionary<int, Texture2D>();

    const float ClothLuminance = 0.354f;   // average brightness of the painted tan cloth

    Material material;
    int appliedKey = -1;

    void Awake() => Apply();

    public void Apply() => Apply(GameSettings.Tabard, GameSettings.Metal, GameSettings.Leather);

    public void Apply(int tabard, int metal, int leather)
    {
        int key = tabard * 10000 + metal * 100 + leather;
        if (key == appliedKey && material != null) return;
        var tex = Build(tabard, metal, leather, key);
        if (tex == null) return;

        if (material == null)
        {
            Renderer r = GetComponentInChildren<SkinnedMeshRenderer>();
            if (r == null) r = GetComponentInChildren<MeshRenderer>();
            if (r == null) return;
            material = new Material(r.sharedMaterial) { name = "Knight (custom)" };
            r.sharedMaterial = material;
        }
        material.mainTexture = tex;
        appliedKey = key;
    }

    static Texture2D Build(int tabard, int metal, int leather, int key)
    {
        if (cache.TryGetValue(key, out var cached) && cached != null) return cached;
        if (basePixels == null)
        {
            var b = Resources.Load<Texture2D>("Customization/KnightBase");
            var m = Resources.Load<Texture2D>("Customization/KnightMask");
            if (b == null || m == null || !b.isReadable || !m.isReadable) return null;
            basePixels = b.GetPixels();
            maskPixels = m.GetPixels();
            texWidth = b.width; texHeight = b.height;
        }

        Color cloth = GameSettings.TabardColors[Mathf.Clamp(tabard, 0, GameSettings.TabardColors.Length - 1)].color;
        Color metalTint = GameSettings.MetalTints[Mathf.Clamp(metal, 0, GameSettings.MetalTints.Length - 1)].tint;
        Color leatherTint = GameSettings.LeatherTints[Mathf.Clamp(leather, 0, GameSettings.LeatherTints.Length - 1)].tint;
        bool keepCloth = tabard == 0;

        var px = new Color[basePixels.Length];
        for (int i = 0; i < px.Length; i++)
        {
            Color c = basePixels[i], mask = maskPixels[i];
            float lum = c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
            if (!keepCloth && mask.r > 0.01f)
            {
                // keep the painted light/shadow, swap the hue
                Color dyed = cloth * (lum / ClothLuminance);
                c = Color.Lerp(c, dyed, mask.r);
            }
            if (mask.g > 0.01f)
            {
                // tint metals but leave specular-ish highlights bright
                Color tinted = new Color(c.r * metalTint.r, c.g * metalTint.g, c.b * metalTint.b);
                tinted = Color.Lerp(tinted, Color.white * lum * 1.1f, Mathf.Clamp01((lum - 0.8f) * 3f));
                c = Color.Lerp(c, tinted, mask.g);
            }
            if (mask.b > 0.01f)
                c = Color.Lerp(c, new Color(c.r * leatherTint.r, c.g * leatherTint.g, c.b * leatherTint.b), mask.b);
            c.a = 1f;
            px[i] = c;
        }
        var tex = new Texture2D(texWidth, texHeight, TextureFormat.RGBA32, true) { name = "KnightCustom_" + key, wrapMode = TextureWrapMode.Clamp, anisoLevel = 4 };
        tex.SetPixels(px);
        tex.Apply(true, true);
        if (cache.Count > 12) cache.Clear();
        cache[key] = tex;
        return tex;
    }
}
