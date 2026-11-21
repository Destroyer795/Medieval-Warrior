using UnityEngine;
using UnityEngine.AI;

// The grave double shares the knight's rig, weapon clips and damage rules. A separate
// root stays on the NavMesh while its figure rises, fights and falls.
public sealed class GraveDuelist : MonoBehaviour
{
    enum Action { Ready, Cut, Draw, Recovery }
    const float SwordTell = .48f;
    const float AimCommit = .32f;
    public float StartingHealth { get; private set; }
    public Weapon StartingWeapon { get; private set; }
    public Weapon CurrentWeapon { get; private set; }
    public int StartingArrows { get; private set; }
    public int Arrows { get; private set; }
    public PlayerHealth Knight { get; private set; }
    public KnightGear Gear { get; private set; }
    public bool IsDefeated => enemy != null && enemy.IsDead;
    public bool CanFight => emerged && !IsDefeated && Knight != null && !Knight.IsDead &&
        (GameManager.instance == null || GameManager.instance.State == GameManager.MatchState.Playing &&
        (GameManager.instance.LastStand == null || !GameManager.instance.LastStand.RevealingGrave));
    public bool IsDrawing => action == Action.Draw && CanFight;
    public float DrawAmount => IsDrawing ? Mathf.Clamp01(age / drawTime) : 0f;
    public Vector3 AimDirection => action == Action.Draw ? (aimPoint - bow.ArrowOrigin).normalized : transform.forward;

    EnemyController enemy;
    NavMeshAgent agent;
    CapsuleCollider capsule;
    MarchwardenVisual figure;
    Animator animator;
    ArcherRig bow;
    AnimationClip guard;
    readonly AnimationClip[] cuts = new AnimationClip[3];
    Action action;
    bool emerged, dealt;
    int step;
    float age, cooldown, reach, drawTime, nextPath, fallAge;
    Vector3 aimPoint;

    public static GraveDuelist Spawn(Vector3 point, PlayerHealth knight)
    {
        var prefab = Resources.Load<GameObject>("Prefabs/Marchwarden");
        if (knight == null || prefab == null || !NavMesh.SamplePosition(point, out var floor, 2.5f, NavMesh.AllAreas)) return null;
        var root = new GameObject("Grave Knight");
        root.tag = "Enemy";
        root.transform.position = floor.position;
        var facing = Vector3.ProjectOnPlane(knight.transform.position - floor.position, Vector3.up);
        if (facing.sqrMagnitude > .001f) root.transform.rotation = Quaternion.LookRotation(facing);
        var self = root.AddComponent<GraveDuelist>();
        self.Prepare(prefab, knight);
        return self;
    }

    void Prepare(GameObject prefab, PlayerHealth knight)
    {
        Knight = knight;
        var control = knight.GetComponent<PlayerControlSpeed>();
        StartingHealth = knight.maxHealth * .6f;
        StartingWeapon = CurrentWeapon = control.CurrentWeapon;
        StartingArrows = Arrows = control.Arrows;
        reach = control.attackRange;
        drawTime = Mathf.Max(.72f, control.drawTime / Boons.DrawSpeed) + AimCommit;

        var model = Instantiate(prefab, transform);
        figure = MarchwardenVisual.Ensure(model);
        model.transform.localScale = knight.transform.lossyScale;
        animator = model.GetComponent<Animator>();
        animator.runtimeAnimatorController = knight.GetComponent<Animator>().runtimeAnimatorController;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        guard = Resources.Load<AnimationClip>("Animations/Sword/Guard");
        for (int i = 0; i < cuts.Length; i++) cuts[i] = SwordMoves.Clip(i);
        WeatherFigure(knight.GetComponent<MarchwardenVisual>());
        Gear = model.AddComponent<KnightGear>();
        bow = model.AddComponent<ArcherRig>();
        bow.Init(null, this);
        figure.SetStowed(CurrentWeapon == Weapon.Bow);

        capsule = gameObject.AddComponent<CapsuleCollider>();
        capsule.center = Vector3.up;
        capsule.height = 2f;
        capsule.radius = .34f;
        agent = gameObject.AddComponent<NavMeshAgent>();
        agent.height = capsule.height;
        agent.radius = capsule.radius;
        agent.speed = control.walkSpeed;
        agent.acceleration = 15f;
        agent.stoppingDistance = Mathf.Max(.65f, reach - .55f);
        agent.updateRotation = false;
        enemy = gameObject.AddComponent<EnemyController>();
        enemy.InitGraveDuelist(this, knight);
        SetEmergence(0);
    }

    void WeatherFigure(MarchwardenVisual original)
    {
        var palette = figure.Body.sharedMaterials;
        var worn = original.Body.sharedMaterials;
        for (int i = 0; i < palette.Length; i++)
        {
            // MarchwardenVisual makes these materials private to this figure.
            Material material = palette[i];
            Color source = worn[i].color;
            material.color = Color.Lerp(source, new Color(.17f, .19f, .16f), .32f);
            if (material.name.Contains("Skin")) material.color = new Color(.34f, .45f, .34f);
            if (material.name.Contains("Hair")) material.color = new Color(.12f, .14f, .12f);
            if (material.name.Contains("Eyes"))
            {
                material.color = new Color(.80f, .95f, .62f);
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", new Color(.84f, 2.1f, .77f));
            }
        }
        if (figure.Linen != null) figure.Linen.SetColor(palette[2].color);
    }

    public void SetEmergence(float progress)
    {
        progress = Mathf.Clamp01(progress);
        figure.transform.localPosition = Vector3.down * (2.35f * (1f - progress));
        emerged = progress >= 1f;
        capsule.enabled = emerged;
        if (agent.isOnNavMesh) agent.isStopped = true;
    }

    internal void InterruptAttack(float seconds)
    {
        action = Action.Recovery;
        cooldown = Mathf.Max(cooldown, Time.time + seconds);
        if (agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
    }

    void Update()
    {
        if (IsDefeated)
        {
            fallAge += Time.deltaTime;
            figure.transform.localRotation = Quaternion.Euler(-83f * Mathf.SmoothStep(0, 1, fallAge / .6f), 0, 9f * Mathf.Min(1, fallAge));
            return;
        }
        if (!CanFight || Time.deltaTime <= 0f || !agent.isOnNavMesh) return;
        Gear.SetStowed(CurrentWeapon == Weapon.Bow);
        age += Time.deltaTime;
        if (action == Action.Recovery && Time.time >= cooldown) action = Action.Ready;
        Vector3 target = Knight.transform.position + Vector3.up;
        Vector3 flat = Vector3.ProjectOnPlane(Knight.transform.position - transform.position, Vector3.up);
        float distance = flat.magnitude;
        if (action == Action.Ready || action == Action.Recovery || action == Action.Draw && age < drawTime - AimCommit)
        {
            if (flat.sqrMagnitude > .001f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(flat), 480f * Time.deltaTime);
        }
        if (action == Action.Cut)
        {
            if (!dealt && age >= SwordTell)
            {
                dealt = true;
                if (distance <= reach + .3f && Vector3.Dot(flat.normalized, transform.forward) > .45f &&
                    Mathf.Abs(Knight.transform.position.y - transform.position.y) < 1.25f &&
                    CombatBoons.ClearLine(transform.position + Vector3.up, target, Knight.transform))
                {
                    Knight.TakeDamage(SwordMoves.Damage(step), transform, SwordMoves.Knockback(step));
                    AudioManager.PlayAt("sword_hit", target, .8f);
                }
            }
            if (age >= SwordTell + SwordMoves.All[step].duration - SwordMoves.All[step].contact)
            {
                step = (step + 1) % cuts.Length;
                InterruptAttack(.35f);
            }
            return;
        }
        if (action == Action.Draw)
        {
            if (age < drawTime - AimCommit) aimPoint = target;
            if (age >= drawTime)
            {
                Arrows--;
                Arrow.FireBow(bow.ArrowOrigin, AimDirection, 1f, transform, 0, Boons.FireArrows, this);
                bow.Release();
                AudioManager.PlayAt("bow_release", transform.position, .9f);
                InterruptAttack(.85f);
            }
            return;
        }
        if (action != Action.Ready) return;
        bool sight = CombatBoons.ClearLine(transform.position + Vector3.up, target, Knight.transform);
        bool ranged = Arrows > 0 && distance >= 4.2f && distance <= 19f && sight;
        CurrentWeapon = ranged ? Weapon.Bow : Weapon.Sword;
        Gear.SetStowed(ranged);
        if ((ranged || distance <= reach && sight) && Vector3.Dot(transform.forward, flat.normalized) > .85f)
        {
            agent.isStopped = true;
            agent.ResetPath();
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
            animator.speed = 1f;
            age = 0;
            dealt = false;
            aimPoint = target;
            action = ranged ? Action.Draw : Action.Cut;
            AudioManager.PlayAt(ranged ? "bow_draw" : "sword_swing", transform.position, .8f);
            return;
        }
        agent.isStopped = false;
        if (Time.time >= nextPath)
        {
            agent.SetDestination(Knight.transform.position);
            nextPath = Time.time + .18f;
        }
        float speed = agent.velocity.magnitude;
        bool running = speed > 2.5f;
        animator.SetBool("isMoving", speed > .2f);
        animator.SetBool("isRunning", running);
        var rig = MarchwardenVisual.Asset;
        float stride = (running ? rig.nativeRunSpeed : rig.nativeWalkSpeed) * figure.transform.lossyScale.z;
        animator.speed = speed > .2f ? Mathf.Clamp(speed / stride, .65f, 2.7f) : 1f;
    }

    void LateUpdate()
    {
        if (figure == null || IsDefeated) return;
        // Pose the upper body after the locomotion controller, before the bow IK and weapon mounts.
        if (action == Action.Cut)
        {
            var move = SwordMoves.All[step];
            float clipTime = age < SwordTell ? Mathf.Lerp(0, move.contact, age / SwordTell) : move.contact + age - SwordTell;
            cuts[step].SampleAnimation(figure.gameObject, Mathf.Min(clipTime, move.duration));
        }
        else if (CurrentWeapon == Weapon.Sword) guard.SampleAnimation(figure.gameObject, 0);
    }

    internal void DropSpoils()
    {
        Vector3 anchor = transform.position;
        var stand = GameManager.instance != null ? GameManager.instance.LastStand : null;
        if (stand != null && (Vector3.ProjectOnPlane(anchor - stand.Center, Vector3.up).magnitude > stand.Radius - 2f ||
            anchor.y >= stand.Ceiling - 1f)) anchor = stand.Center;
        if (!NavMesh.SamplePosition(anchor, out var floor, 3f, NavMesh.AllAreas) &&
            !NavMesh.SamplePosition(Knight.transform.position, out floor, 5f, NavMesh.AllAreas))
            floor.position = Knight.transform.position;
        Place(PickupKind.Health, 50, -.85f);
        Place(PickupKind.Arrows, 20, .85f);

        void Place(PickupKind kind, int amount, float side)
        {
            Vector3 at = floor.position;
            if (NavMesh.SamplePosition(at + transform.right * side, out var spot, 1f, NavMesh.AllAreas) &&
                !NavMesh.Raycast(at, spot.position, out _, NavMesh.AllAreas)) at = spot.position;
            var loot = Pickup.Spawn(kind, at, amount, onGround: true);
            loot.lifetime = 90f;
            FX.Sparkle(at + Vector3.up * .6f, UIKit.Gold, 12);
        }
    }
}
