using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

// Zombie brain. Every zombie hunts the knight, but they coordinate through ZombieDirector:
// only a few get an "attack token" at a time, the rest circle and wait for an opening,
// so fights feel like being surrounded rather than a conga line. They detour when stuck,
// get knocked back by arrows and shield bashes, and each archetype has its own trick.
public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    public int health = 3;              // legacy inspector values, overwritten by the archetype
    public ZombieType type = ZombieType.Walker;
    public bool isBoss;

    [Header("Combat")]
    public float attackRange = 2.0f;
    public float timeBetweenAttacks = 1.5f;

    [Header("Navigation")]
    public float repathRate = 0.25f;
    public float stuckCheckTime = 1.2f;
    public float stuckDistance = 0.3f;

    [Header("References")]
    public Transform player;

    public static readonly List<EnemyController> All = new List<EnemyController>();
    public static event Action<EnemyController, float, Vector3> Damaged;   // enemy, amount, world position

    public ZombieStats Stats { get; private set; }
    public float Health { get; private set; }
    public float MaxHealth { get; private set; }
    public bool IsDead { get; private set; }
    public float LastHitTime { get; private set; } = -99f;
    public float HealthFraction => MaxHealth > 0 ? Mathf.Clamp01(Health / MaxHealth) : 0f;
    public Vector3 HeadPosition => transform.position + Vector3.up * 1.9f * transform.localScale.y;

    enum State { Emerging, Hunt, Circle, Attack, Stagger, Kite, Fuse, Dead }
    State state = State.Hunt;

    Animator animator;
    CapsuleCollider myCollider;
    NavMeshAgent navAgent;
    PlayerHealth playerHealth;
    Renderer[] renderers;
    MaterialPropertyBlock mpb;

    float baseSpeed, damage, attackTimer, stateTimer, repathTimer, groanTimer, flashTimer;
    float circleAngle, circleRadius;
    bool hasToken, initialised, enraged, hitDealt;
    Vector3 knockVelocity, lastPosition, detourTarget;
    float stuckTimer, detourTimer, unreachableTimer;
    Vector3 deathDirection;

    // animator parameter hashes (only set when the controller has them)
    int hSpeed, hMoveMul, hAttack, hDie, hDieForward, hIsMoving, hAttackMul;
    bool pSpeed, pMoveMul, pAttack, pDie, pDieForward, pIsMoving, pAttackMul;

    // ------------------------------------------------------------ setup

    void Awake()
    {
        animator = GetComponent<Animator>();
        myCollider = GetComponent<CapsuleCollider>();
        navAgent = GetComponent<NavMeshAgent>();
        renderers = GetComponentsInChildren<Renderer>();
        mpb = new MaterialPropertyBlock();
        if (animator != null)
        {
            animator.applyRootMotion = false;   // the agent drives movement; root motion fought it
            foreach (var p in animator.parameters)
            {
                switch (p.name)
                {
                    case "Speed": pSpeed = true; hSpeed = p.nameHash; break;
                    case "MoveMul": pMoveMul = true; hMoveMul = p.nameHash; break;
                    case "AttackMul": pAttackMul = true; hAttackMul = p.nameHash; break;
                    case "attack": pAttack = true; hAttack = p.nameHash; break;
                    case "die": pDie = true; hDie = p.nameHash; break;
                    case "dieForward": pDieForward = true; hDieForward = p.nameHash; break;
                    case "isMoving": pIsMoving = true; hIsMoving = p.nameHash; break;
                }
            }
        }
    }

    void OnEnable() => All.Add(this);

    void OnDisable()
    {
        All.Remove(this);
        ReleaseToken();
    }

    void Start()
    {
        if (!initialised) Init(type, isBoss, false);
    }

    public void Init(ZombieType t, bool boss, bool emerge)
    {
        initialised = true;
        type = t;
        isBoss = boss;
        Stats = ZombieTypes.Get(t);
        var diff = GameSettings.Profile;

        MaxHealth = Health = Stats.health * diff.enemyHealth * (boss && t != ZombieType.Warlord ? 2.5f : 1f);
        health = Mathf.CeilToInt(Health);
        damage = Stats.damage * diff.enemyDamage;
        attackRange = Stats.attackRange;
        timeBetweenAttacks = Stats.cooldown;
        baseSpeed = Stats.speed * diff.enemySpeed * UnityEngine.Random.Range(0.9f, 1.12f);
        transform.localScale = Vector3.one * Stats.scale * (boss && t != ZombieType.Warlord ? 1.15f : 1f);

        if (navAgent != null)
        {
            navAgent.speed = baseSpeed;
            navAgent.acceleration = 14f;
            navAgent.angularSpeed = 540f;
            navAgent.stoppingDistance = 0.2f;
            navAgent.autoBraking = false;
            navAgent.avoidancePriority = UnityEngine.Random.Range(30, 70);
            navAgent.obstacleAvoidanceType = ObstacleAvoidanceType.MedQualityObstacleAvoidance;
        }

        if (animator != null)
        {
            if (pMoveMul) animator.SetFloat(hMoveMul, Stats.animSpeed);
            if (pAttackMul) animator.SetFloat(hAttackMul, Mathf.Clamp(0.6f / Mathf.Max(0.2f, Stats.windup), 0.6f, 1.6f));
        }
        ApplyTint(0f);

        var playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealth = playerObj.GetComponent<PlayerHealth>();
        }

        circleAngle = UnityEngine.Random.Range(0f, 360f);
        circleRadius = UnityEngine.Random.Range(3.6f, 5.2f);
        groanTimer = UnityEngine.Random.Range(1f, 6f);
        lastPosition = transform.position;

        if (emerge) StartEmerging();
        else SetState(type == ZombieType.Spitter ? State.Kite : State.Hunt);

        if (type == ZombieType.Brute || type == ZombieType.Warlord)
            AudioManager.PlayAt("brute_roar", transform.position, 1f, 0.05f);
    }

    // ------------------------------------------------------------ main loop

    void Update()
    {
        if (!initialised || IsDead) return;
        UpdateFlash();
        UpdateAnimator();

        if (state == State.Emerging) { UpdateEmerging(); return; }
        if (player == null || navAgent == null || !navAgent.enabled || !navAgent.isOnNavMesh) return;
        if (playerHealth != null && playerHealth.IsDead) { Idle(); return; }

        stateTimer += Time.deltaTime;
        groanTimer -= Time.deltaTime;
        if (groanTimer <= 0f)
        {
            AudioManager.PlayAt("zombie_groan" + UnityEngine.Random.Range(0, 3), transform.position, 0.55f, 0.15f);
            groanTimer = UnityEngine.Random.Range(5f, 11f);
        }

        if (type == ZombieType.Warlord && !enraged && HealthFraction < 0.5f) Enrage();

        switch (state)
        {
            case State.Hunt: UpdateHunt(); break;
            case State.Circle: UpdateCircle(); break;
            case State.Attack: UpdateAttack(); break;
            case State.Stagger: UpdateStagger(); break;
            case State.Kite: UpdateKite(); break;
            case State.Fuse: UpdateFuse(); break;
        }
    }

    float DistanceToPlayer => Vector3.Distance(Flat(transform.position), Flat(player.position));
    static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0, v.z);

    void SetState(State s)
    {
        state = s;
        stateTimer = 0f;
        hitDealt = false;
        if (navAgent != null && navAgent.enabled && navAgent.isOnNavMesh) navAgent.isStopped = s == State.Attack || s == State.Stagger || s == State.Fuse;
    }

    void Idle()
    {
        if (navAgent.isOnNavMesh) navAgent.isStopped = true;
        ReleaseToken();
    }

    void UpdateHunt()
    {
        float d = DistanceToPlayer;

        if (type == ZombieType.Bloater)
        {
            Chase(player.position, baseSpeed);
            if (d < Stats.attackRange) StartFuse();
            return;
        }

        // ask the director for permission to engage; without it, circle at a respectful distance
        if (!hasToken && d < 9f) hasToken = ZombieDirector.RequestToken(this);
        if (!hasToken && d < 7f) { SetState(State.Circle); return; }

        float speed = baseSpeed;
        if (type == ZombieType.Runner && hasToken && d < 7f && d > 2.5f) speed *= 1.6f;   // lunge
        Chase(player.position, speed);

        if (hasToken && d <= attackRange && Time.time >= attackTimer) BeginAttack();
    }

    void UpdateCircle()
    {
        float d = DistanceToPlayer;
        if (d > 9f || ZombieDirector.RequestToken(this)) { hasToken = ZombieDirector.HasToken(this); SetState(State.Hunt); return; }

        // drift around the knight, occasionally feinting inwards
        circleAngle += (Mathf.Sin(Time.time * 0.7f + GetInstanceID()) * 25f + 18f) * Time.deltaTime;
        float r = circleRadius + Mathf.Sin(Time.time * 1.3f + GetInstanceID() * 0.37f) * 0.6f;
        Vector3 slot = player.position + Quaternion.Euler(0, circleAngle, 0) * Vector3.forward * r;
        Chase(slot, baseSpeed * 0.65f);
        FaceTowards(player.position, 6f);
    }

    void BeginAttack()
    {
        SetState(State.Attack);
        if (pAttack) animator.SetTrigger(hAttack);
        AudioManager.PlayAt(type == ZombieType.Spitter ? "spit" : "zombie_attack", transform.position, 0.8f);
    }

    void UpdateAttack()
    {
        FaceTowards(player.position, hitDealt ? 2f : 10f);
        if (!hitDealt && stateTimer >= Stats.windup)
        {
            hitDealt = true;
            DealHit();
        }
        if (stateTimer >= Stats.windup + 0.55f)
        {
            attackTimer = Time.time + timeBetweenAttacks * UnityEngine.Random.Range(0.85f, 1.2f);
            // give someone else a turn
            if (type != ZombieType.Brute && type != ZombieType.Warlord && UnityEngine.Random.value < 0.5f) ReleaseToken();
            SetState(type == ZombieType.Spitter ? State.Kite : State.Hunt);
        }
    }

    void DealHit()
    {
        if (type == ZombieType.Spitter)
        {
            Vector3 lead = player.position + (playerHealth != null ? playerHealth.Velocity * 0.6f : Vector3.zero);
            BileGlob.Throw(transform.position + Vector3.up * 1.6f * transform.localScale.y + transform.forward * 0.4f, lead, damage, this);
            return;
        }
        Vector3 toPlayer = player.position - transform.position;
        float reach = attackRange + 0.7f;
        bool inFront = Vector3.Angle(transform.forward, Flat(toPlayer)) < 75f;
        if (toPlayer.magnitude <= reach && inFront && playerHealth != null)
        {
            float knock = type == ZombieType.Brute || type == ZombieType.Warlord ? 9f : 0f;
            playerHealth.TakeDamage(damage, transform, knock);
        }
        if (type == ZombieType.Brute || type == ZombieType.Warlord)
        {
            FX.Dust(transform.position + transform.forward * 1.5f, 1.6f, 18);
            FX.Shake(0.5f);
            AudioManager.PlayAt("explosion", transform.position, 0.35f, 0.2f);
        }
    }

    // Spitters hang back at range, retreat when the knight closes in, and lob bile.
    void UpdateKite()
    {
        float d = DistanceToPlayer;
        Vector3 away = Flat(transform.position - player.position).normalized;
        if (d < 6f)
        {
            Vector3 retreat = transform.position + away * 6f;
            if (NavMesh.SamplePosition(retreat, out var hit, 3f, NavMesh.AllAreas)) Chase(hit.position, baseSpeed * 1.1f);
        }
        else if (d > 14f) Chase(player.position, baseSpeed);
        else
        {
            if (navAgent.isOnNavMesh) navAgent.isStopped = true;
            FaceTowards(player.position, 8f);
            if (Time.time >= attackTimer && HasLineOfSight()) BeginAttack();
        }
    }

    bool HasLineOfSight()
    {
        Vector3 eye = transform.position + Vector3.up * 1.6f;
        Vector3 target = player.position + Vector3.up * 1.2f;
        return !Physics.Linecast(eye, target, out var hit, ~0, QueryTriggerInteraction.Ignore) || hit.transform == player || hit.transform.IsChildOf(player);
    }

    void StartFuse()
    {
        SetState(State.Fuse);
        AudioManager.PlayAt("bloater_fuse", transform.position, 1f, 0f);
    }

    void UpdateFuse()
    {
        float t = stateTimer / Stats.windup;
        float pulse = 1f + Mathf.Sin(stateTimer * (10f + t * 30f)) * 0.06f + t * 0.25f;
        transform.localScale = Vector3.one * Stats.scale * pulse;
        flashTimer = Mathf.Max(flashTimer, Mathf.PingPong(stateTimer * 6f, 1f) * 0.4f);
        if (stateTimer >= Stats.windup) Explode();
    }

    void Explode()
    {
        if (exploded) return;
        exploded = true;
        Combat.Explode(transform.position + Vector3.up, 4.2f, damage, this, new Color(0.7f, 0.85f, 0.2f, 0.9f));
        Kill(Vector3.up, true);
    }
    bool exploded, explodePending;

    void UpdateStagger()
    {
        knockVelocity = Vector3.Lerp(knockVelocity, Vector3.zero, 1f - Mathf.Exp(-7f * Time.deltaTime));
        if (navAgent.isOnNavMesh) navAgent.Move(knockVelocity * Time.deltaTime);
        if (stateTimer > 0.45f && knockVelocity.magnitude < 0.6f)
            SetState(type == ZombieType.Spitter ? State.Kite : State.Hunt);
    }

    // ------------------------------------------------------------ movement helpers

    void Chase(Vector3 target, float speed)
    {
        navAgent.isStopped = false;
        navAgent.speed = speed;

        if (detourTimer > 0f)
        {
            detourTimer -= Time.deltaTime;
            target = detourTarget;
        }

        repathTimer += Time.deltaTime;
        if (repathTimer >= repathRate && !navAgent.pathPending)
        {
            repathTimer = 0f;
            navAgent.SetDestination(target);
            if (navAgent.pathStatus != NavMeshPathStatus.PathComplete) unreachableTimer += repathRate;
            else unreachableTimer = 0f;
        }

        // stuck on a corner or another zombie: sidestep along a random detour
        float moved = Vector3.Distance(transform.position, lastPosition);
        lastPosition = transform.position;
        if (moved < stuckDistance * Time.deltaTime * 4f && navAgent.remainingDistance > 1.5f)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer > stuckCheckTime) PickDetour();
        }
        else stuckTimer = 0f;

        if (unreachableTimer > 5f) Relocate();
    }

    void PickDetour()
    {
        stuckTimer = 0f;
        Vector3 side = Vector3.Cross(Vector3.up, (player.position - transform.position).normalized) * (UnityEngine.Random.value < 0.5f ? -1f : 1f);
        Vector3 guess = transform.position + side * UnityEngine.Random.Range(3f, 6f) - transform.forward * 1.5f;
        if (NavMesh.SamplePosition(guess, out var hit, 4f, NavMesh.AllAreas))
        {
            detourTarget = hit.position;
            detourTimer = 1.1f;
            navAgent.ResetPath();
            navAgent.SetDestination(detourTarget);
        }
    }

    // Truly cut off from the knight (walled in, bad spawn): quietly move somewhere sensible while unseen.
    void Relocate()
    {
        unreachableTimer = 0f;
        var cam = Camera.main;
        if (cam != null)
        {
            Vector3 vp = cam.WorldToViewportPoint(transform.position);
            if (vp.z > 0 && vp.x > 0 && vp.x < 1 && vp.y > 0 && vp.y < 1) return;
        }
        if (ZombieSpawner.TryFindSpawnPoint(player.position, 14f, 26f, out Vector3 p)) navAgent.Warp(p);
    }

    void FaceTowards(Vector3 target, float speed)
    {
        Vector3 dir = Flat(target - transform.position);
        if (dir.sqrMagnitude < 0.001f) return;
        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(dir), 1f - Mathf.Exp(-speed * Time.deltaTime));
    }

    void UpdateAnimator()
    {
        if (animator == null) return;
        float v = navAgent != null && navAgent.enabled ? navAgent.velocity.magnitude : 0f;
        if (state == State.Stagger || state == State.Attack || state == State.Fuse) v = 0f;
        if (pSpeed) animator.SetFloat(hSpeed, v / Mathf.Max(0.5f, transform.localScale.y), 0.1f, Time.deltaTime);
        if (pIsMoving) animator.SetBool(hIsMoving, v > 0.2f);
    }

    // ------------------------------------------------------------ emerging from the ground

    float emergeDepth;

    void StartEmerging()
    {
        state = State.Emerging;
        stateTimer = 0f;
        if (navAgent != null) navAgent.enabled = false;
        if (myCollider != null) myCollider.enabled = false;
        emergeDepth = 1.9f * transform.localScale.y;
        transform.position -= Vector3.up * emergeDepth;
        FX.Dust(transform.position + Vector3.up * emergeDepth, 1.1f, 20, new Color(0.38f, 0.28f, 0.18f, 0.8f));
        AudioManager.PlayAt("emerge", transform.position, 0.8f);
    }

    void UpdateEmerging()
    {
        stateTimer += Time.deltaTime;
        const float duration = 1.3f;
        float step = emergeDepth * Time.deltaTime / duration;
        transform.position += Vector3.up * step;
        if (stateTimer >= duration)
        {
            if (myCollider != null) myCollider.enabled = true;
            if (navAgent != null)
            {
                navAgent.enabled = true;
                if (NavMesh.SamplePosition(transform.position, out var hit, 3f, NavMesh.AllAreas)) navAgent.Warp(hit.position);
            }
            SetState(type == ZombieType.Spitter ? State.Kite : State.Hunt);
        }
    }

    // ------------------------------------------------------------ damage

    // Legacy entry point (sword/parry code used ints).
    public void TakeDamage(int damageAmount) => TakeDamage(damageAmount, -transform.forward, 0f);

    public void TakeDamage(float amount, Vector3 hitDirection, float knockback)
    {
        if (IsDead || state == State.Emerging && stateTimer < 0.4f) return;

        Health -= amount;
        health = Mathf.CeilToInt(Health);
        LastHitTime = Time.time;
        flashTimer = 1f;
        Damaged?.Invoke(this, amount, HeadPosition);
        FX.Blood(transform.position + Vector3.up * 1.2f * transform.localScale.y, hitDirection + Vector3.up * 0.3f, Mathf.Clamp(amount, 0.6f, 2.5f));

        if (Health <= 0f)
        {
            if (type == ZombieType.Bloater)
            {
                if (explodePending) return;
                explodePending = true;
                deathDirection = hitDirection;
                Invoke(nameof(Explode), 0.35f);
                SetState(State.Fuse);
                IsDeadPending();
                return;
            }
            Kill(hitDirection, false);
            return;
        }

        float resist = Stats.knockbackResist;
        if (knockback > 0f && resist < 1f && state != State.Fuse)
        {
            knockVelocity = Flat(hitDirection).normalized * knockback * (1f - resist);
            if (knockVelocity.magnitude > 1.2f)
            {
                if (state == State.Attack) ReleaseToken();
                SetState(State.Stagger);
            }
        }
    }

    // Bloaters keep ticking for a moment after a lethal hit so they can blow up their friends.
    void IsDeadPending()
    {
        if (myCollider != null) myCollider.enabled = false;
        ReleaseToken();
    }

    void Kill(Vector3 hitDirection, bool exploded)
    {
        if (IsDead) return;
        IsDead = true;
        state = State.Dead;
        All.Remove(this);   // corpses don't count towards caps or wave progress
        ReleaseToken();
        CancelInvoke();

        if (animator != null)
        {
            bool forward = Vector3.Dot(transform.forward, Flat(hitDirection)) > 0f;
            if (pDieForward) animator.SetBool(hDieForward, forward);
            if (pDie) animator.SetTrigger(hDie);
        }
        if (myCollider != null) myCollider.enabled = false;
        if (navAgent != null) navAgent.enabled = false;
        AudioManager.PlayAt("zombie_die", transform.position, 0.8f, 0.15f);

        if (GameManager.instance != null) GameManager.instance.AddKill(this);
        DropLoot();

        gameObject.tag = "Untagged";
        if (exploded) { foreach (var r in renderers) if (r != null) r.enabled = false; Destroy(gameObject, 0.1f); }
        else { Invoke(nameof(BeginSink), 3.5f); Destroy(gameObject, 6f); }
    }

    void BeginSink() => sinking = true;
    bool sinking;

    void LateUpdate()
    {
        if (sinking) transform.position += Vector3.down * 0.5f * Time.deltaTime;
    }

    void DropLoot()
    {
        float roll = UnityEngine.Random.value;
        Vector3 at = transform.position + Vector3.up * 0.3f;
        if (type == ZombieType.Warlord || type == ZombieType.Brute)
        {
            Pickup.Spawn(PickupKind.Health, at + transform.right * 0.6f, 35);
            Pickup.Spawn(PickupKind.Arrows, at - transform.right * 0.6f, 10);
        }
        else if (roll < 0.28f) Pickup.Spawn(PickupKind.Arrows, at, UnityEngine.Random.Range(3, 7));
        else if (roll < 0.38f) Pickup.Spawn(PickupKind.Health, at, 20);
    }

    void Enrage()
    {
        enraged = true;
        baseSpeed *= 1.45f;
        timeBetweenAttacks *= 0.65f;
        AudioManager.PlayAt("brute_roar", transform.position, 1f, 0f);
        FX.Shake(0.6f);
        GameHUD.Announce("The Warlord is enraged!", UIKit.Blood);
    }

    // ------------------------------------------------------------ visuals

    void ApplyTint(float flash)
    {
        if (renderers == null) return;
        // the zombie albedo is very dark; lift it so they read against the grass, flash red-white when hit
        Color baseTint = (Stats != null ? Stats.tint : Color.white) * 1.7f;
        Color c = Color.Lerp(baseTint, new Color(3f, 1.6f, 1.5f), flash);
        Color glow = (Stats != null ? Stats.glow : Color.black) + new Color(0.9f, 0.25f, 0.2f) * flash;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_Color", c);
            mpb.SetColor("_EmissionColor", glow);
            r.SetPropertyBlock(mpb);
        }
    }

    void UpdateFlash()
    {
        if (flashTimer <= 0f) return;
        flashTimer = Mathf.Max(0f, flashTimer - Time.deltaTime * 6f);
        ApplyTint(flashTimer);
    }

    // ------------------------------------------------------------ attack tokens

    void ReleaseToken()
    {
        if (hasToken) ZombieDirector.ReleaseToken(this);
        hasToken = false;
    }

    // Legacy hook used by the old parry: reflect damage.
    public void Stagger(Vector3 from, float force)
    {
        TakeDamage(0.01f, (transform.position - from).normalized, force);
    }
}

// Decides who gets to swing at the knight. Brutes and the Warlord always may.
public static class ZombieDirector
{
    static readonly HashSet<EnemyController> tokens = new HashSet<EnemyController>();

    static int MaxAttackers => GameSettings.Difficulty switch { Difficulty.Squire => 2, Difficulty.Knight => 3, _ => 4 };

    public static bool RequestToken(EnemyController e)
    {
        tokens.RemoveWhere(t => t == null || t.IsDead);
        if (tokens.Contains(e)) return true;
        bool heavy = e.type == ZombieType.Brute || e.type == ZombieType.Warlord;
        if (!heavy && CountWhere(t => t.type != ZombieType.Brute && t.type != ZombieType.Warlord) >= MaxAttackers) return false;
        tokens.Add(e);
        return true;
    }

    public static bool HasToken(EnemyController e) => tokens.Contains(e);
    public static void ReleaseToken(EnemyController e) => tokens.Remove(e);

    static int CountWhere(Func<EnemyController, bool> pred)
    {
        int n = 0;
        foreach (var t in tokens) if (pred(t)) n++;
        return n;
    }
}
