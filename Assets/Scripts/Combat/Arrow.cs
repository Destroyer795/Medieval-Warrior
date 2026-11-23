using System.Collections.Generic;
using UnityEngine;

// Ballistic arrow: drops, pierces exposed targets and sticks to animated bodies or shields.
[DefaultExecutionOrder(175)]
public class Arrow : MonoBehaviour
{
    readonly struct ContactHit
    {
        public readonly Collider collider;
        public readonly EnemyController enemy;
        public readonly ArrowSurface.Part part;
        public readonly Transform anchor;
        public readonly Vector3 point;
        public readonly float distance;

        public ContactHit(Collider collider, EnemyController enemy, ArrowSurface.Part part, Transform anchor, Vector3 point, float distance)
        {
            this.collider = collider;
            this.enemy = enemy;
            this.part = part;
            this.anchor = anchor;
            this.point = point;
            this.distance = distance;
        }
    }

    static readonly List<ContactHit> contacts = new List<ContactHit>(32);
    Vector3 velocity;
    float damage, knockback;
    Transform owner;
    Vector3 muzzle;
    bool sweepFromMuzzle;
    bool firedByDuelist;
    GraveDuelist duelist;
    bool stuck, falling, stuckInEnemy;
    public bool isFire { get; private set; }
    bool splintered;
    EnemyController stuckIn;
    Vector3 fallVelocity, spin;
    int pierce;
    float age;
    GameObject flame;
    public bool IsFlaming => isFire && flame != null;
    readonly System.Collections.Generic.HashSet<EnemyController> pierced = new System.Collections.Generic.HashSet<EnemyController>();

    const float Length = 0.9f;
    static GameObject template;

    public static Arrow FireBow(Vector3 origin, Vector3 direction, float draw, Transform owner, int pierce = 0, bool fire = false, GraveDuelist duelist = null)
    {
        var arrow = Fire(origin + direction * .2f, direction, Mathf.Lerp(24f, 58f, draw), Mathf.Lerp(1f, 3.4f, draw),
            Mathf.Lerp(2f, 9f, draw), owner, pierce, fire);
        arrow.muzzle = origin;
        arrow.duelist = duelist;
        arrow.firedByDuelist = duelist != null;
        return arrow;
    }

    public static Arrow Fire(Vector3 position, Vector3 direction, float speed, float damage, float knockback, Transform owner, int pierce = 0, bool fire = false)
    {
        var go = Model();
        go.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
        var a = go.AddComponent<Arrow>();
        a.velocity = direction.normalized * speed;
        a.damage = damage;
        a.knockback = knockback;
        a.owner = owner;
        a.muzzle = position;
        a.sweepFromMuzzle = true;
        a.pierce = pierce;
        a.isFire = fire;
        if (fire) a.flame = FX.AttachFlame(go.transform, Vector3.forward * Length, 0.65f, true);

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
        if (MarchwardenVisual.Asset != null)
            return ProceduralModels.Spawn("Marchwarden Arrow", MarchwardenVisual.Asset.arrow, MarchwardenVisual.Asset.materials);
        if (template == null) template = Resources.Load<GameObject>("Prefabs/Arrow");
        if (template != null) return Instantiate(template);
        return ProceduralModels.Spawn("Arrow", ProceduralModels.ArrowMesh(), ProceduralModels.ArrowMaterials());
    }

    void LateUpdate()
    {
        if (firedByDuelist && (duelist == null || !duelist.CanFight)) { Destroy(gameObject); return; }
        age += Time.deltaTime;
        if (isFire && (age > 6f || stuckInEnemy && (stuckIn == null || !stuckIn.IsBurning))) Extinguish();
        if (falling) { UpdateFall(); return; }
        if (stuck)
        {
            // the zombie it was stuck in has died (or been cleaned up): drop to the ground
            if (stuckInEnemy && (stuckIn == null || stuckIn.IsDead)) BeginFall();
            return;
        }
        if (age > 14f) { Destroy(gameObject); return; }
        if (Time.deltaTime <= 0f) return;

        velocity += Physics.gravity * 0.45f * Time.deltaTime;
        Vector3 tip = transform.position + transform.forward * Length;
        Vector3 step = velocity * Time.deltaTime;
        Quaternion nextRotation = Quaternion.LookRotation(velocity);
        Vector3 start = tip;
        if (sweepFromMuzzle)
        {
            // The rendered shaft starts in front of the rest. The first cast
            // must still cover every point from the rest to the moving tip.
            start = muzzle;
            sweepFromMuzzle = false;
            if (ResolveMuzzleOverlap()) return;
        }
        Vector3 sweep = transform.position + step + nextRotation * Vector3.forward * Length - start;
        Trace(new Ray(start, sweep), sweep.magnitude, owner, !firedByDuelist, true);
        foreach (var hit in contacts)
            if (Contact(hit)) return;
        transform.position += step;
        transform.rotation = nextRotation;
    }

    public static Vector3 AimTarget(Ray camera, Vector3 origin, Transform owner)
    {
        const float range = 120f;
        float start = Mathf.Max(0, Vector3.Dot(origin - camera.origin, camera.direction) + .01f);
        Trace(new Ray(camera.GetPoint(start), camera.direction), range - start, owner, true, false);
        return contacts.Count > 0 ? contacts[0].point : camera.GetPoint(range);
    }

    static void Trace(Ray ray, float distance, Transform owner, bool traceEnemies, bool projectile)
    {
        contacts.Clear();
        float stopAt = distance;
        foreach (var hit in Physics.RaycastAll(ray, distance, ~0, QueryTriggerInteraction.Ignore))
        {
            if (owner != null && hit.transform.IsChildOf(owner)) continue;
            var enemy = hit.collider.GetComponentInParent<EnemyController>();
            if (enemy != null && (enemy.IsDead || ArrowSurface.CanTrace(enemy))) continue;
            contacts.Add(new ContactHit(hit.collider, enemy, ArrowSurface.Part.Body, null, hit.point, hit.distance));
            if (enemy == null) stopAt = Mathf.Min(stopAt, hit.distance);
        }
        if (traceEnemies)
        {
            foreach (var enemy in EnemyController.All)
            {
                if (enemy.IsDead || (owner != null && enemy.transform.IsChildOf(owner))) continue;
                var body = enemy.ArrowBody;
                if (body == null) continue;
                // Published skin bounds include every animated pose and fitted prop.
                if (!body.bounds.IntersectRay(ray, out float entry) || entry > stopAt) continue;
                float nearest = stopAt;
                if (ArrowSurface.Trace(enemy, ray, projectile, ref nearest, out var part, out var anchor))
                    contacts.Add(new ContactHit(null, enemy, part, anchor, ray.GetPoint(nearest), nearest));
            }
        }
        contacts.Sort((a, b) => a.distance.CompareTo(b.distance));
    }

    bool ResolveMuzzleOverlap()
    {
        // Rays skip colliders containing their origin. Give solid cover
        // precedence when it and a character overlap the bow at release.
        var overlaps = Physics.OverlapSphere(muzzle, .005f, ~0, QueryTriggerInteraction.Ignore);
        foreach (var collider in overlaps)
        {
            if (owner != null && collider.transform.IsChildOf(owner)) continue;
            if (!TouchesMuzzle(collider)) continue;
            var enemy = collider.GetComponentInParent<EnemyController>();
            if (enemy == null && collider.GetComponentInParent<PlayerHealth>() == null &&
                Contact(new ContactHit(collider, null, ArrowSurface.Part.Body, null, muzzle, 0))) return true;
        }
        foreach (var collider in overlaps)
        {
            if (owner != null && collider.transform.IsChildOf(owner)) continue;
            if (!TouchesMuzzle(collider)) continue;
            var enemy = collider.GetComponentInParent<EnemyController>();
            if ((enemy != null && !ArrowSurface.CanTrace(enemy) || firedByDuelist && collider.GetComponentInParent<PlayerHealth>() != null) &&
                Contact(new ContactHit(collider, enemy, ArrowSurface.Part.Body, null, muzzle, 0))) return true;
        }
        return false;
    }

    bool TouchesMuzzle(Collider collider) =>
        collider is TerrainCollider || collider is MeshCollider mesh && !mesh.convex ||
        (collider.ClosestPoint(muzzle) - muzzle).sqrMagnitude <= 1e-8f;

    bool Contact(ContactHit hit)
    {
        var collider = hit.collider;
        var point = hit.point;
        if (firedByDuelist)
        {
            if (collider.GetComponentInParent<PlayerHealth>() == duelist.Knight)
            {
                bool headshot = point.y > duelist.Knight.transform.position.y + 1.45f * duelist.Knight.transform.lossyScale.y;
                duelist.Knight.TakeDamage(damage * (headshot ? 2f : 1f), owner, knockback);
            }
            else FX.Dust(point, .2f, 3);
            Destroy(gameObject);
            return true;
        }
        var enemy = hit.enemy;
        if (enemy != null && (enemy.IsDead || pierced.Contains(enemy))) return false;
        if (enemy != null && pierce > 0 && hit.part != ArrowSurface.Part.Shield &&
            (hit.anchor != null || !enemy.GuardsContact(velocity.normalized, point)))
        {
            pierce--;
            pierced.Add(enemy);
            DealContact(hit);
            return false;
        }
        Impact(hit);
        return true;
    }

    void Impact(ContactHit hit)
    {
        var collider = hit.collider;
        var enemy = hit.enemy;
        var point = hit.point;
        stuck = true;
        var trail = GetComponent<TrailRenderer>();
        if (trail != null) trail.emitting = false;
        transform.position = point - transform.forward * (Length - 0.18f);

        if (enemy != null)
        {
            DealContact(hit);
            transform.SetParent(hit.anchor != null ? hit.anchor : NearestBone(enemy, point), true);
            stuckIn = enemy;
            stuckInEnemy = true;
            if (enemy.IsDead) BeginFall();
        }
        else
        {
            if (collider.TryGetComponent<ExplosiveBarrel>(out var barrel))
            {
                barrel.Detonate();
                Destroy(gameObject);
                return;
            }
            var totem=collider.GetComponentInParent<HordeTotem>();
            if(totem!=null)totem.ReceiveHit(damage,point);
            BurstSplinters(point,null);
            if (isFire) FX.Embers(point, 8);
            AudioManager.PlayAt("arrow_hit", point, 0.8f);
            FX.Dust(point, 0.25f, 5);
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
        Extinguish();
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
        Extinguish();
        var trail = GetComponent<TrailRenderer>();
        if (trail != null) Destroy(trail);
        var p = gameObject.AddComponent<Pickup>();
        p.kind = PickupKind.Arrows;
        p.amount = 1;
        p.lifetime = 40f;
        p.staticVisual = true;
        Destroy(this);
    }

    void Extinguish()
    {
        isFire = false;
        FX.Extinguish(flame);
        flame = null;
    }

    bool HitEnemy(EnemyController enemy, Vector3 point)
    {
        bool headshot = point.y > enemy.transform.position.y + 1.45f * enemy.transform.localScale.y;
        return DamageEnemy(enemy, point, headshot, false);
    }

    void DealContact(ContactHit hit)
    {
        if (hit.part == ArrowSurface.Part.Shield)
        {
            AudioManager.PlayAt("shield_block", hit.point, .85f);
            FX.Sparks(hit.point, -velocity.normalized);
            BurstSplinters(hit.point, hit.enemy);
        }
        else if (hit.anchor != null) DamageEnemy(hit.enemy, hit.point, hit.part == ArrowSurface.Part.Head, true);
        else HitEnemy(hit.enemy, hit.point);
    }

    bool DamageEnemy(EnemyController enemy, Vector3 point, bool headshot, bool precise)
    {
        float dmg = damage * (headshot ? 2f : 1f);
        bool landed = precise ? enemy.ReceivePreciseArrowHit(dmg, velocity.normalized, knockback, point) :
            enemy.ReceiveWeaponHit(dmg, velocity.normalized, knockback, point);
        if (!landed) { BurstSplinters(point, enemy); return false; }
        BurstSplinters(point,enemy);
        if(headshot&&!clustered)
        {clustered=true;ClusterVolley.Create(point,velocity.normalized,damage,isFire);}
        if(headshot)FX.Impact(velocity.normalized,.28f,.055f);
        if (isFire)
        {
            FX.Embers(point, 12);
            if (!enemy.IsDead) enemy.Ignite(4f, 1f);
            else
            {
                // Fully drawn arrows kill ordinary walkers outright. They still
                // need visible ignition feedback rather than a silent non-burn.
                var impactFlames = FX.AttachFlame(enemy.transform, Vector3.up * 0.7f, 1.5f);
                Destroy(impactFlames, 1.2f);
            }
        }
        AudioManager.PlayAt("arrow_flesh", point, 0.9f);
        if (GameManager.instance != null) GameManager.instance.RegisterArrowHit(enemy, headshot);
        GameHUD.HitMarker(headshot);
        return true;
    }
    bool clustered;
    void BurstSplinters(Vector3 point,EnemyController primary)
    {
        if(splintered||!Boons.SplinterVolley)return;
        splintered=true;CombatBoons.Splinters(point,velocity.normalized,primary,damage,isFire);
    }
}
