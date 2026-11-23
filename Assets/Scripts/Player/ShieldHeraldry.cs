using UnityEngine;

// Apply the heraldic field to the existing curved steel submesh. Sampling the
// same front plate in one material pass keeps the pattern attached in every pose.
internal sealed class ShieldHeraldry : MonoBehaviour
{
    const int PlateSlot = 12;
    // Retain the first four indices so existing knight presets load as saved.
    static readonly (string sprite, Color32 pigment, bool dark)[] charges =
    {
        ("emblem_charge", new Color32(236, 203, 127, 255), false),
        (null, new Color32(255, 255, 255, 255), false),
        ("emblem_chevron", new Color32(35, 31, 28, 255), true),
        ("emblem_saltire", new Color32(227, 215, 176, 255), false),
        ("emblem_raven", new Color32(27, 25, 24, 255), true),
        ("emblem_oak_leaf", new Color32(214, 161, 91, 255), false)
    };
    static Material finish;
    MaterialPropertyBlock properties;
    MeshRenderer plate;

    internal static Sprite ChargeSprite(int crest)
    {
        string name = charges[Mathf.Clamp(crest, 0, charges.Length - 1)].sprite;
        return name == null ? null : UIKit.Sprite(name);
    }

    internal static Color ChargeColor(int crest) => charges[Mathf.Clamp(crest, 0, charges.Length - 1)].pigment;

    internal static Color ChargeEdge(int crest, Color field)
    {
        if (charges[Mathf.Clamp(crest, 0, charges.Length - 1)].dark && field.grayscale < .32f)
            return new Color32(202, 167, 113, 255);
        return Color.clear;
    }

    internal static void Attach(Transform shield, Mesh source)
    {
        if (source == null || source.subMeshCount <= PlateSlot || shield.TryGetComponent<ShieldHeraldry>(out _)) return;
        var renderer = shield.GetComponent<MeshRenderer>();
        var materials = renderer.sharedMaterials;
        if (materials.Length <= PlateSlot || materials[PlateSlot] == null) return;
        if (finish == null)
        {
            var shader = Resources.Load<Shader>("Marchwarden/ShieldHeraldry");
            if (shader == null) return;
            finish = new Material(materials[PlateSlot]) { shader = shader, name = "Marchwarden painted heater" };
        }
        materials[PlateSlot] = finish;
        renderer.sharedMaterials = materials;
        var heraldry = shield.gameObject.AddComponent<ShieldHeraldry>();
        heraldry.plate = renderer;
        heraldry.properties = new MaterialPropertyBlock();
        heraldry.Apply(GameSettings.Tabard);
    }

    internal void Apply(int tabardIndex)
    {
        var tabard = GameSettings.TabardColors[Mathf.Clamp(tabardIndex, 0, GameSettings.TabardColors.Length - 1)].color;
        int crest = GameSettings.Crest;
        var charge = ChargeSprite(crest);
        properties.SetColor("_Field", tabard);
        properties.SetColor("_Pigment", ChargeColor(crest));
        properties.SetColor("_ChargeEdge", ChargeEdge(crest, tabard));
        properties.SetFloat("_HasCharge", charge != null ? 1f : 0f);
        properties.SetTexture("_Charge", charge != null ? charge.texture : Texture2D.blackTexture);
        plate.SetPropertyBlock(properties, PlateSlot);
    }
}
