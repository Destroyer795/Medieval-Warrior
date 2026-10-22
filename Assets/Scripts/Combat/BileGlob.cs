using UnityEngine;

// Spitter projectile: lobbed in an arc, splashes on impact and leaves a burning acid pool.
public class BileGlob : MonoBehaviour
{
    Vector3 velocity;
    float damage;
    EnemyController owner;

    public static void Throw(Vector3 from, Vector3 target, float damage, EnemyController owner)
    {
        const float flightTime = 1.05f;
        Vector3 v = (target - from) / flightTime - 0.5f * Physics.gravity * flightTime;
        var go = ProceduralModels.Spawn("Bile", ProceduralModels.PotionMesh(), new[] { Combat.ResourceMaterial("Acid", new Color(0.5f, 0.85f, 0.1f)) }, null, false);
        go.transform.position = from;
        go.transform.localScale = Vector3.one * 1.3f;
        var b = go.AddComponent<BileGlob>();
        b.velocity = v;
        b.damage = damage;
        b.owner = owner;
        Destroy(go, 4f);
    }

    void Update()
    {
        velocity += Physics.gravity * Time.deltaTime;
        Vector3 step = velocity * Time.deltaTime;
        transform.Rotate(400f * Time.deltaTime, 220f * Time.deltaTime, 0f);
        foreach (var hit in Physics.RaycastAll(transform.position, step.normalized, step.magnitude + 0.15f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.transform.IsChildOf(owner.transform)) continue;
            if (hit.collider.GetComponentInParent<EnemyController>() != null) continue;
            Splash(hit.point);
            return;
        }
        transform.position += step;
    }

    void Splash(Vector3 at)
    {
        AudioManager.PlayAt("splat", at, 1f);
        FX.Blood(at, Vector3.up, 2f);
        var player = GameObject.FindWithTag("Player");
        if (player != null && Vector3.Distance(player.transform.position + Vector3.up * 0.9f, at) < 2.1f)
        {
            var ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeDamage(damage, owner != null ? owner.transform : null, 0f);
        }
        AcidPool.Create(at, 1.7f, damage * 0.6f, 4.5f);
        Destroy(gameObject);
    }
}

public class AcidPool : MonoBehaviour
{
    float radius, dps, life, age;
    Material mat;
    Color baseColor;

    public static void Create(Vector3 at, float radius, float dps, float life)
    {
        var go = ProceduralModels.Spawn("Acid Pool", DiscMesh(), new[] { Combat.ResourceMaterial("Acid", new Color(0.5f, 0.85f, 0.1f, 0.6f)) }, null, false);
        at = GroundBelow(at);
        go.transform.position = at + Vector3.up * 0.04f;
        go.transform.localScale = new Vector3(radius, 1f, radius);
        var p = go.AddComponent<AcidPool>();
        p.radius = radius; p.dps = dps; p.life = life;
        var mr = go.GetComponent<MeshRenderer>();
        p.mat = mr.material;
        p.baseColor = p.mat.color;
    }

    // the ground under a point, ignoring characters (otherwise the pool lands on someone's head)
    public static Vector3 GroundBelow(Vector3 at)
    {
        var hits = Physics.RaycastAll(at + Vector3.up * 3f, Vector3.down, 8f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (var h in hits)
        {
            if (h.collider.GetComponentInParent<PlayerHealth>() != null || h.collider.GetComponentInParent<EnemyController>() != null) continue;
            return h.point;
        }
        return at;
    }

    static Mesh disc;
    static Mesh DiscMesh()
    {
        if (disc != null) return disc;
        var k = new ProceduralModels.MeshKit(1);
        k.Tube(0, Vector3.zero, Vector3.up * 0.03f, 1f, 0.9f, 14);
        return disc = k.Build("Disc");
    }

    void Update()
    {
        age += Time.deltaTime;
        float fade = Mathf.Clamp01((life - age) / 1f);
        if (mat != null) mat.color = new Color(baseColor.r, baseColor.g, baseColor.b, baseColor.a * fade);
        transform.localScale = new Vector3(radius * (0.6f + 0.4f * Mathf.Clamp01(age * 4f)), 1f, radius * (0.6f + 0.4f * Mathf.Clamp01(age * 4f)));
        if (age >= life) { Destroy(gameObject); return; }

        var player = GameObject.FindWithTag("Player");
        if (player == null) return;
        Vector3 d = player.transform.position - transform.position;
        if (new Vector2(d.x, d.z).magnitude < radius && Mathf.Abs(d.y) < 1.2f)
        {
            var ph = player.GetComponent<PlayerHealth>();
            if (ph != null) ph.TakeBurn(dps * Time.deltaTime);
        }
    }
}
