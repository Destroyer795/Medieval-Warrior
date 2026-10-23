using UnityEngine;

public enum PickupKind { Arrows, Health }

// Bobbing loot: arrow bundles (on supply crates and from fallen zombies) and healing draughts.
// Crate pickups respawn; drops vanish after a while. Walk close and they fly to you.
public class Pickup : MonoBehaviour
{
    public PickupKind kind = PickupKind.Arrows;
    public int amount = 10;
    public bool respawns;
    public float respawnTime = 45f;
    public float lifetime = 30f;
    public bool staticVisual;   // this object *is* the pickup (a spent arrow on the ground): no bobbing, no glow

    Transform visual;
    float age, hiddenUntil = -1f;
    Vector3 basePos;
    bool magnet;

    public static Pickup Spawn(PickupKind kind, Vector3 at, int amount)
    {
        var go = new GameObject(kind == PickupKind.Arrows ? "Arrows" : "Healing Draught");
        at = AcidPool.GroundBelow(at);
        go.transform.position = at;
        var p = go.AddComponent<Pickup>();
        p.kind = kind;
        p.amount = amount;
        return p;
    }

    void Start()
    {
        basePos = transform.position;
        if (staticVisual) { visual = transform; return; }
        string prefab = kind == PickupKind.Arrows ? "ArrowBundle" : "Potion";
        var t = Resources.Load<GameObject>("Prefabs/" + prefab);
        GameObject v = t != null ? Instantiate(t, transform) :
            kind == PickupKind.Arrows ? ProceduralModels.Spawn("Bundle", ProceduralModels.ArrowBundleMesh(), ProceduralModels.BundleMaterials(), transform)
                                      : ProceduralModels.Spawn("Potion", ProceduralModels.PotionMesh(), ProceduralModels.PotionMaterials(), transform);
        visual = v.transform;
        visual.localPosition = Vector3.up * 0.45f;
        if (kind == PickupKind.Arrows) visual.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        visual.localScale = Vector3.one * (kind == PickupKind.Health ? 1.6f : 1.2f);

        var glow = new GameObject("Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = Vector3.up * 0.7f;
        glow.type = LightType.Point;
        glow.range = 2.2f;
        glow.intensity = 1.2f;
        glow.color = kind == PickupKind.Arrows ? new Color(1f, 0.85f, 0.45f) : new Color(1f, 0.3f, 0.3f);
        glow.shadows = LightShadows.None;
    }

    void Update()
    {
        if (hiddenUntil > 0f)
        {
            if (Time.time < hiddenUntil) return;
            hiddenUntil = -1f;
            visual.gameObject.SetActive(true);
            transform.position = basePos;
            FX.Sparkle(basePos + Vector3.up * 0.4f, new Color(1f, 0.9f, 0.5f), 10);
        }

        age += Time.deltaTime;
        if (!respawns && age > lifetime) { Destroy(gameObject); return; }

        if (!staticVisual)
        {
            visual.localPosition = Vector3.up * (0.45f + Mathf.Sin(Time.time * 2.2f) * 0.08f);
            visual.Rotate(Vector3.up, 70f * Time.deltaTime, Space.World);
        }

        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        var weapons = player.GetComponent<PlayerControlSpeed>();
        var health = player.GetComponent<PlayerHealth>();
        float d = Vector3.Distance(player.transform.position + Vector3.up * 0.5f, transform.position);

        bool wanted = kind == PickupKind.Arrows ? weapons != null && weapons.Arrows < weapons.maxArrows
                                                : health != null && health.currentHealth < health.maxHealth;
        if (!wanted) { magnet = false; return; }
        if (d < (staticVisual ? 2.2f : 3.2f)) magnet = true;
        if (magnet && !respawns) transform.position = Vector3.MoveTowards(transform.position, player.transform.position + Vector3.up * 0.5f, 9f * Time.deltaTime);
        if (d < 1.3f) Collect(weapons, health);
    }

    void Collect(PlayerControlSpeed weapons, PlayerHealth health)
    {
        if (kind == PickupKind.Arrows)
        {
            int got = weapons.AddArrows(amount);
            if (got > 0) GameHUD.Toast(got == 1 ? "+1 arrow" : $"+{got} arrows", UIKit.Gold, "arrow", staticVisual ? 0.9f : 1.8f, staticVisual ? 24 : 30);
            AudioManager.Play2D("pickup", 0.8f);
        }
        else
        {
            health.Heal(amount);
            GameHUD.Toast($"+{amount} health", new Color(1f, 0.45f, 0.45f), "potion");
            AudioManager.Play2D("heal", 0.8f);
            FX.Sparkle(health.transform.position + Vector3.up, new Color(1f, 0.4f, 0.4f));
        }

        if (respawns)
        {
            hiddenUntil = Time.time + respawnTime;
            visual.gameObject.SetActive(false);
            magnet = false;
        }
        else Destroy(gameObject);
    }
}
