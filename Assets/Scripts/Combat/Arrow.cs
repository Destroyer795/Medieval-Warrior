using UnityEngine;

// Ballistic arrow: drops a little, sticks into whatever it hits, knocks zombies back.
public class Arrow : MonoBehaviour
{
    Vector3 velocity;
    float damage, knockback;
    Transform owner;
    bool stuck, fire, falling, stuckInEnemy;
    EnemyController stuckIn;
    Vector3 fallVelocity, spin;
    int pierce;
    float age;
    readonly System.Collections.Generic.HashSet<EnemyController> pierced = new System.Collections.Generic.HashSet<EnemyController>();

    const float Length = 0.9f;
    static GameObject template;

    public static Arrow Fire(Vector3 position, Vector3 direction, float speed, float damage, float knockback, Transform owner, int pierce = 0, bool fire = false)
    {
        var go = Model();
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        var a = go.AddComponent<Arrow>();
        a.velocity = direction.normalized * speed;
        a.damage = damage;
        a.knockback = knockback;
        a.owner = owner;
        a.pierce = pierce;
        a.fire = fire;

        var trail = go.AddComponent<TrailRenderer>();
        trail.sharedMaterial = Resources.Load<Material>("FX/Particle");
        trail.time = 0.22f;
        trail.startWidth = 0.05f; trail.endWidth = 0f;
        trail.startColor = fire ? new Color(1f, 0.55f, 0.1f, 0.9f) : new Color(1f, 0.95f, 0.85f, 0.55f);
        trail.endColor = fire ? new Color(1f, 0.2f, 0f, 0f) : new Color(1f, 1f, 1f, 0f);
        if (fire) trail.startWidth = 0.09f;
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
        if (falling) { UpdateFall(); return; }
        if (stuck)
        {
            // the zombie it was stuck in has died (or been cleaned up): drop to the ground
            if (stuckInEnemy && (stuckIn == null || stuckIn.IsDead)) BeginFall();
            return;
        }
        if (age > 14f) { Destroy(gameObject); return; }

        velocity += Physics.gravity * 0.45f * Time.deltaTime;
        Vector3 tip = transform.position + transform.forward * Length;
        Vector3 step = velocity * Time.deltaTime;

        var hits = Physics.RaycastAll(tip, step.normalized, step.magnitude + 0.05f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (x, y) => x.distance.CompareTo(y.distance));
        foreach (var hit in hits)
        {
            if (owner != null && hit.transform.IsChildOf(owner)) continue;
            var e = hit.collider.GetComponentInParent<EnemyController>();
            if (e != null && pierced.Contains(e)) continue;
            if (e != null && !e.IsDead && pierce > 0)
            {
                // punch straight through and keep flying
                pierce--;
                pierced.Add(e);
                HitEnemy(e, hit.point);
                continue;
            }
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
            HitEnemy(enemy, hit.point);
            // stick to the nearest bone so the arrow moves with the body, not the upright root collider
            transform.SetParent(NearestBone(enemy, hit.point), true);
            stuckIn = enemy;
            stuckInEnemy = true;
            if (enemy.IsDead) BeginFall();
        }
        else
        {
            if (fire) FX.Embers(hit.point, 8);
            AudioManager.PlayAt("arrow_hit", hit.point, 0.8f);
            FX.Dust(hit.point, 0.25f, 5);
            BecomePickup();   // walk over it to get it back
        }
    }

    static Transform NearestBone(EnemyController enemy, Vector3 point)
    {
        Transform best = enemy.transform;
        float bestD = float.MaxValue;
        foreach (var smr in enemy.GetComponentsInChildren<SkinnedMeshRenderer>())
            foreach (var b in smr.bones)
            {
                if (b == null) continue;
                float d = (b.position - point).sqrMagnitude;
                if (d < bestD) { bestD = d; best = b; }
            }
        return best;
    }

    void BeginFall()
    {
        stuck = false;
        falling = true;
        stuckInEnemy = false;
        transform.SetParent(null, true);
        fallVelocity = new Vector3(Random.Range(-1f, 1f), Random.Range(0.5f, 2f), Random.Range(-1f, 1f));
        spin = new Vector3(Random.Range(-360f, 360f), Random.Range(-180f, 180f), 0f);
    }

    void UpdateFall()
    {
        fallVelocity += Physics.gravity * Time.deltaTime;
        Vector3 step = fallVelocity * Time.deltaTime;
        foreach (var hit in Physics.RaycastAll(transform.position + Vector3.up * 0.3f, Vector3.down, 0.3f + Mathf.Max(0.05f, -step.y), ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.collider.GetComponentInParent<EnemyController>() != null || hit.collider.GetComponentInParent<PlayerHealth>() != null) continue;
            // land flat on the ground
            Vector3 along = Vector3.ProjectOnPlane(transform.forward, hit.normal);
            if (along.sqrMagnitude < 0.01f) along = Vector3.ProjectOnPlane(Vector3.forward, hit.normal);
            transform.SetPositionAndRotation(hit.point + hit.normal * 0.02f - along.normalized * 0.45f, Quaternion.LookRotation(along, hit.normal));
            falling = false;
            BecomePickup();
            return;
        }
        transform.position += step;
        transform.Rotate(spin * Time.deltaTime, Space.Self);
        if (transform.position.y < -60f) Destroy(gameObject);
    }

    void BecomePickup()
    {
        var trail = GetComponent<TrailRenderer>();
        if (trail != null) Destroy(trail);
        var p = gameObject.AddComponent<Pickup>();
        p.kind = PickupKind.Arrows;
        p.amount = 1;
        p.lifetime = 40f;
        p.staticVisual = true;
        Destroy(this);
    }

    void HitEnemy(EnemyController enemy, Vector3 point)
    {
        bool headshot = point.y > enemy.transform.position.y + 1.45f * enemy.transform.localScale.y;
        float dmg = damage * (headshot ? 2f : 1f);
        enemy.TakeDamage(dmg, velocity.normalized, knockback);
        if (fire && !enemy.IsDead) enemy.Ignite(4f, 0.6f);
        AudioManager.PlayAt("arrow_flesh", point, 0.9f);
        if (GameManager.instance != null) GameManager.instance.RegisterArrowHit(enemy, headshot);
        GameHUD.HitMarker(headshot);
    }
}
