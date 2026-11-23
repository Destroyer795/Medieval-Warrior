using System;
using System.Linq;
using UnityEngine;

public static class SwordWardrobe
{
    internal static string Preference => GameSettings.EquipmentProfile + "sword";
    internal static readonly (string id, string name, string feat)[] Designs =
    {
        ("NorthmereSword", "Northmere Sword", null),
        ("FenlandFalchion", "Fenland Falchion", null),
        ("BriarEstoc", "Briar Estoc", null),
        ("SaltireBroadsword", "Saltire Broadsword", "defender"),
        ("SaffronKilij", "Saffron Kilij", "dawn"),
        ("IvoryCinquedea", "Ivory Cinquedea", "unbroken"),
        ("KingsSword", "King’s Sword", "last_oath")
    };
    public static readonly string[] Names = Designs.Select(d => d.name).ToArray();
    public static bool Available(int i) => i >= 0 && i < Designs.Length &&
        (Designs[i].feat == null || Feats.IsUnlocked(Designs[i].feat));
    public static string Challenge(int i) => Designs[i].feat == null ? "" :
        Feats.All.First(f => f.id == Designs[i].feat).description;
    public static GameObject Model(int i) => Resources.Load<GameObject>("SwordCollection/" + Designs[i].id);
    public static int Selected
    {
        get
        {
            string saved = PlayerPrefs.GetString(Preference);
            int i = Array.FindIndex(Designs, d => d.id == saved);
            return Available(i) ? i : 0;
        }
    }
    public static bool TryEquip(int i)
    {
        if (!Available(i)) return false;
        if (PlayerPrefs.GetString(Preference) != Designs[i].id)
        {
            PlayerPrefs.SetString(Preference, Designs[i].id);
            PlayerPrefs.Save();
        }
        return true;
    }
    public static string RewardFor(string feat) => Array.Find(Designs, d => d.feat == feat).name;
}
