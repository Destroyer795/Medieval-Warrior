using UnityEngine;

// Ballistic arrow: drops a little, sticks into whatever it hits, knocks zombies back.
public class Arrow : MonoBehaviour
{
    Vector3 velocity;
    float damage, knockback;
    Transform owner;
    bool stuck;
    float age;

    const float Length = 0.9f;
    static GameObject template;

    public static Arrow Fire(Vector3 position, Vector3 direction, float speed, float damage, float knockback, Transform owner)
    {
        var go = Model();
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        var a = go.AddComponent<Arrow>();
        a.velocity = direction.normalized * speed;
        a.damage = damage;
        a.knockback = knockback;
        a.owner = owner;

        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Resources.Load<Material>("FX/Particle");
        trail.time = 0.22f;
        trail.startWidth = 0.05f; trail.endWidth = 0f;
        trail.startColor = new Color(1f, 0.95f, 0.85f, 0.55f); trail.endColor = new Color(1f, 1f, 1f, 0f);
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return a;
    }

    public static GameObject Model()
    {
        if (template == null) template = Resources.Load<GameObject>("Prefabs/Arrow");
        if (template != null) return Instantiate(template);
        return ProceduralModels.Spawn("Arrow", ProceduralModels.ArrowMesh(), ProceduralModels.ArrowMaterials());
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age > 14f) { Destroy(gameObject); return; }
        if (stuck) return;

        velocity += Physics.gravity * 0.45f * Time.deltaTime;
        Vector3 tip = transform.position + transform.forward * Length;
        Vector3 step = velocity * Time.deltaTime;

        var hits = Physics.RaycastAll(tip, step.normalized, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (var hit in hits)
        {
            if (owner != null && hit.transform.IsChildOf(owner)) continue;
            Impact(hit);
            return;
        }
        transform.position += step;
        transform.rotation = Quaternion.LookRotation(velocity);
    }

    void Impact(RaycastHit hit)
    {
        stuck = true;
        var trail = GetComponent<TrailRenderer>();
        if (trail != null) trail.emitting = false;
        transform.position = hit.point - transform.forward * (Length - 0.18f);

        var enemy = hit.collider.GetComponentInParent<EnemyController>();
        if (enemy != null && !enemy.IsDead)
        {
            bool headshot = hit.point.y > enemy.transform.position.y + 1.45f * enemy.transform.localScale.y;
            float dmg = damage * (headshot ? 2f : 1f);
            enemy.TakeDamage(dmg, velocity.normalized, knockback);
            transform.SetParent(hit.collider.transform, true);
            AudioManager.PlayAt("arrow_flesh", hit.point, 0.9f);
            if (GameManager.instance != null) GameManager.instance.RegisterArrowHit(enemy, headshot);
            GameHUD.HitMarker(headshot);
            Destroy(gameObject, 4f);
        }
        else
        {
            AudioManager.PlayAt("arrow_hit", hit.point, 0.8f);
            FX.Dust(hit.point, 0.25f, 5);
            Destroy(gameObject, 10f);
        }
    }
}
