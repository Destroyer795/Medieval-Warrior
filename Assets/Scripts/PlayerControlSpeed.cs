using UnityEngine;
using System.Collections.Generic;

public enum Weapon { Sword, Bow }

// Knight controller: camera-relative movement, sprint/dodge on a stamina bar,
// a three-hit sword combo, shield block + bash, and a bow that is stowed on the back.
public class PlayerControlSpeed : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5.0f;
    public float runSpeed = 10.0f;
    public float blockSpeed = 2.0f;
    public float aimSpeed = 2.6f;
    public float rotationSpeed = 700.0f;

    [Header("Stamina")]
    public float maxStamina = 100f;
    public float sprintCost = 16f;          // per second
    public float dodgeCost = 26f;
    public float bashCost = 22f;
    public float staminaRegen = 32f;        // per second, after a short pause
    public float dodgeSpeed = 13f;
    public float dodgeTime = 0.32f;

    [Header("Combat Settings")]
    public float attackRange = 2.0f;
    public float attackCooldown = 0.42f;
    public float comboWindow = 0.9f;

    [Header("Bow")]
    public int startArrows = 20;
    public int maxArrows = 45;
    public float drawTime = 0.7f;

    [Header("World bounds (set by the environment builder)")]
    public Vector3 boundsCenter;
    public float boundsRadius;            // 0 = unlimited
    public float maxSlope = 40f;

    [Header("Shield Logic")]
    public Transform shieldArmBone; // <--- DRAG 'Bip001 L UpperArm' HERE
    public Vector3 blockRotation = new Vector3(-30, 60, -20); // The angle to hold the shield

    public bool isBlocking = false;

    // ---- state readable by the HUD
    public Weapon CurrentWeapon { get; private set; } = Weapon.Sword;
    public int Arrows { get; private set; }
    public float Stamina { get; private set; }
    public float DrawAmount { get; private set; }
    public bool IsAiming { get; private set; }
    public bool IsDrawing { get; private set; }
    public int ComboStep { get; private set; }
    public bool Exhausted => exhausted;

    private Animator animator;
    private Transform cameraTransform;
    private PlayerHealth myHealthScript;
    private Rigidbody rb;
    private Vector3 desiredVelocity;

    float lastAttack = -9f, lastStaminaUse = -9f, stunnedUntil, dodgeUntil, bashReady;
    Vector3 dodgeDir;
    bool exhausted;
    float blockWeight, aimWeight, stepTimer, leanWeight;
    int stepIndex;
    Transform leftHand, spine, bowVisual, nockedArrow;
    readonly List<(float time, int step)> pendingHits = new List<(float, int)>();

    void Start()
    {
        animator = GetComponent<Animator>();
        myHealthScript = GetComponent<PlayerHealth>();
        rb = GetComponent<Rigidbody>();
        if (Camera.main != null) cameraTransform = Camera.main.transform;

        if (rb != null)
        {
            // Physics-driven movement so the knight collides with walls and follows the terrain
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }

        Stamina = maxStamina;
        Arrows = startArrows;
        if (shieldArmBone == null) shieldArmBone = FindBone(transform, "Bip001 L UpperArm");
        leftHand = FindBone(transform, "Bip001 L Hand");
        spine = FindBone(transform, "Bip001 Spine1") ?? FindBone(transform, "Bip001 Spine");
        if (!TryGetComponent(out KnightAppearance _)) gameObject.AddComponent<KnightAppearance>();
        BuildBow();
    }

    static Transform FindBone(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var f = FindBone(c, name);
            if (f != null) return f;
        }
        return null;
    }

    void BuildBow()
    {
        var t = Resources.Load<GameObject>("Prefabs/Bow");
        var go = t != null ? Instantiate(t) : ProceduralModels.Spawn("Bow", ProceduralModels.BowMesh(), ProceduralModels.BowMaterials(0));
        go.name = "Bow (equipment)";
        var mr = go.GetComponent<MeshRenderer>();
        if (mr != null) mr.sharedMaterials = ProceduralModels.BowMaterials(GameSettings.BowFinish);
        bowVisual = go.transform;
        bowVisual.SetParent(transform, true);

        nockedArrow = Arrow.Model().transform;
        nockedArrow.name = "Nocked Arrow";
        nockedArrow.SetParent(transform, true);
        nockedArrow.gameObject.SetActive(false);
    }

    bool Paused => Time.timeScale < 0.01f;
    bool Stunned => Time.time < stunnedUntil;
    bool Dodging => Time.time < dodgeUntil;

    void Update()
    {
        if (Paused) return;

        // --- 1. WEAPON SWITCH
        if (!Dodging)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1)) Equip(Weapon.Sword);
            if (Input.GetKeyDown(KeyCode.Alpha2)) Equip(Weapon.Bow);
            if (Input.GetKeyDown(KeyCode.Q)) Equip(CurrentWeapon == Weapon.Sword ? Weapon.Bow : Weapon.Sword);
        }

        // --- 2. BLOCK / AIM INPUT
        bool canAct = !Stunned && !Dodging;
        isBlocking = canAct && CurrentWeapon == Weapon.Sword && (Input.GetKey(KeyCode.E) || Input.GetMouseButton(1));
        IsAiming = canAct && CurrentWeapon == Weapon.Bow && (Input.GetMouseButton(1) || debugAim);

        // --- 3. MOVEMENT
        float moveX = Input.GetAxisRaw("Horizontal");
        float moveZ = Input.GetAxisRaw("Vertical");
        Vector3 movement = CameraRelative(moveX, moveZ);

        bool wantsSprint = Input.GetKey(KeyCode.LeftShift) && movement.sqrMagnitude > 0.01f && !isBlocking && !IsAiming && !IsDrawing;
        bool isRunning = wantsSprint && !exhausted && Stamina > 0f;

        if (Input.GetKeyDown(KeyCode.Space) && canAct) TryDodge(movement);

        desiredVelocity = Vector3.zero;
        if (Dodging)
        {
            float k = (dodgeUntil - Time.time) / dodgeTime;
            desiredVelocity = dodgeDir * dodgeSpeed * Mathf.Lerp(0.35f, 1f, k);
            animator.SetBool("isMoving", true);
            animator.SetBool("isRunning", true);
        }
        else if (!Stunned && movement.sqrMagnitude > 0.01f)
        {
            movement.Normalize();
            float currentSpeed = walkSpeed;
            if (isBlocking) currentSpeed = blockSpeed;
            else if (IsAiming || IsDrawing) currentSpeed = aimSpeed;
            else if (isRunning) currentSpeed = runSpeed;

            currentSpeed *= Boons.MoveSpeed;
            if (rb != null) desiredVelocity = movement * currentSpeed;
            else transform.Translate(movement * currentSpeed * Time.deltaTime, Space.World);

            if (!(IsAiming || IsDrawing))
            {
                Quaternion toRotation = Quaternion.LookRotation(movement, Vector3.up);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);
            }
            animator.SetBool("isMoving", true);
            animator.SetBool("isRunning", isRunning);
        }
        else
        {
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
        }

        // archers face where the camera looks
        if ((IsAiming || IsDrawing) && cameraTransform != null)
        {
            Vector3 f = cameraTransform.forward; f.y = 0;
            if (f.sqrMagnitude > 0.01f) transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(f), rotationSpeed * 1.5f * Time.deltaTime);
        }

        // footsteps, quicker when running
        if (rb != null && desiredVelocity.sqrMagnitude > 0.5f)
        {
            stepTimer -= Time.deltaTime * desiredVelocity.magnitude;
            if (stepTimer <= 0f)
            {
                stepTimer = 1.55f;
                AudioManager.PlayAt("step" + (stepIndex++ % 4), transform.position, Dodging ? 0.5f : 0.32f, 0.1f);
            }
        }

        // --- 4. STAMINA
        if (isRunning) UseStamina(sprintCost * Time.deltaTime);
        else if (Time.time - lastStaminaUse > 0.7f) Stamina = Mathf.Min(maxStamina, Stamina + staminaRegen * Boons.StaminaRegen * Time.deltaTime);
        if (Stamina <= 0.5f) exhausted = true;
        if (exhausted && Stamina > maxStamina * 0.3f) exhausted = false;

        // --- 5. ATTACK
        if (canAct)
        {
            if (CurrentWeapon == Weapon.Sword) UpdateSword();
            else UpdateBow();
        }
        else if (IsDrawing) CancelDraw();
        ProcessPendingHits();

        // Sync with Health Script
        if (myHealthScript != null) myHealthScript.isShieldBlocking = isBlocking;
        animator.SetBool("defend", isBlocking);
    }

    Vector3 CameraRelative(float x, float z)
    {
        if (cameraTransform == null) return new Vector3(x, 0, z);
        Vector3 camForward = cameraTransform.forward, camRight = cameraTransform.right;
        camForward.y = 0; camRight.y = 0;
        camForward.Normalize(); camRight.Normalize();
        return camForward * z + camRight * x;
    }

    void FixedUpdate()
    {
        if (rb == null || rb.isKinematic) return;
        if (Stunned && !Dodging) return;   // let knockback forces play out

        Vector3 v = desiredVelocity;

        // too steep to walk up (mountains frame the valley, they aren't a ladder)
        if (v.sqrMagnitude > 0.01f && Physics.Raycast(rb.position + Vector3.up * 0.6f, Vector3.down, out var ground, 1.6f, ~0, QueryTriggerInteraction.Ignore))
        {
            float slope = Vector3.Angle(ground.normal, Vector3.up);
            Vector3 downhill = Vector3.ProjectOnPlane(ground.normal, Vector3.up);
            if (slope > maxSlope && Vector3.Dot(v, downhill) < 0f)
                v -= Vector3.Project(v, downhill.normalized);
        }

        // soft world edge
        if (boundsRadius > 0f)
        {
            Vector3 off = rb.position - boundsCenter; off.y = 0f;
            if (off.magnitude > boundsRadius)
            {
                Vector3 outward = off.normalized;
                if (Vector3.Dot(v, outward) > 0f) v -= Vector3.Project(v, outward);
                v -= outward * (off.magnitude - boundsRadius) * 2f;
            }
        }

        // Keep gravity on Y, drive X/Z from input
        rb.linearVelocity = new Vector3(v.x, rb.linearVelocity.y, v.z);
    }

    // ------------------------------------------------------------ stamina / dodge

    void UseStamina(float amount)
    {
        Stamina = Mathf.Max(0f, Stamina - amount);
        lastStaminaUse = Time.time;
    }

    public void DrainStamina(float amount) => UseStamina(amount);

    public void Stun(float seconds) => stunnedUntil = Mathf.Max(stunnedUntil, Time.time + seconds);

    void TryDodge(Vector3 movement)
    {
        if (Stamina < dodgeCost || exhausted)
        {
            GameHUD.Toast("Too tired!", new Color(0.9f, 0.8f, 0.4f), "stamina");
            return;
        }
        UseStamina(dodgeCost);
        dodgeDir = movement.sqrMagnitude > 0.01f ? movement.normalized : -transform.forward;
        dodgeUntil = Time.time + dodgeTime;
        if (myHealthScript != null) myHealthScript.InvulnerableUntil = Time.time + dodgeTime * 0.9f;
        if (IsDrawing) CancelDraw();
        AudioManager.PlayAt("dodge", transform.position, 0.8f);
        FX.Dust(transform.position, 0.6f, 8);
    }

    // ------------------------------------------------------------ sword

    void UpdateSword()
    {
        if (!Input.GetMouseButtonDown(0) || Time.time - lastAttack < attackCooldown) return;

        if (isBlocking)
        {
            ShieldBash();
            return;
        }

        ComboStep = Time.time - lastAttack < comboWindow ? (ComboStep + 1) % 3 : 0;
        lastAttack = Time.time;
        animator.SetTrigger("isAttacking");
        if (myHealthScript != null) myHealthScript.ActivateParry();
        AudioManager.PlayAt("sword_swing", transform.position, 0.8f, 0.12f);
        FX.Swipe(transform, ComboStep);
        pendingHits.Add((Time.time + 0.2f, ComboStep));
        if (rb != null) rb.AddForce(transform.forward * 2.5f, ForceMode.VelocityChange); // small lunge
    }

    void ProcessPendingHits()
    {
        for (int i = pendingHits.Count - 1; i >= 0; i--)
        {
            if (Time.time < pendingHits[i].time) continue;
            CheckForHits(pendingHits[i].step);
            pendingHits.RemoveAt(i);
        }
    }

    void CheckForHits(int step)
    {
        Vector3 hitPosition = transform.position + transform.forward + Vector3.up;
        Collider[] hitColliders = Physics.OverlapSphere(hitPosition, attackRange);

        HashSet<EnemyController> hitEnemies = new HashSet<EnemyController>();
        bool finisher = step == 2;
        float damage = (finisher ? 2f : 1f) * Boons.SwordDamage;
        float knock = finisher ? 7f : 2.5f;

        foreach (Collider hit in hitColliders)
        {
            EnemyController enemy = hit.GetComponentInParent<EnemyController>();
            if (enemy == null || enemy.IsDead || hitEnemies.Contains(enemy)) continue;
            Vector3 dir = (enemy.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dir) > 0.3f)
            {
                enemy.TakeDamage(damage, dir, knock);
                hitEnemies.Add(enemy); // one hit per enemy
                if (enemy.IsDead && Boons.LifeSteal > 0f && myHealthScript != null) myHealthScript.Heal(Boons.LifeSteal);
                if (GameManager.instance != null) GameManager.instance.RegisterMeleeHit(enemy);
            }
        }

        if (hitEnemies.Count > 0)
        {
            AudioManager.PlayAt("sword_hit", hitPosition, 1f, 0.1f);
            FX.HitStop(finisher ? 0.07f : 0.04f);
            FX.Shake(finisher ? 0.35f : 0.18f);
        }
    }

    void ShieldBash()
    {
        if (Time.time < bashReady) return;
        if (Stamina < bashCost) { GameHUD.Toast("Too tired!", new Color(0.9f, 0.8f, 0.4f), "stamina"); return; }
        UseStamina(bashCost);
        bashReady = Time.time + 0.9f;
        lastAttack = Time.time;
        AudioManager.PlayAt("shield_block", transform.position, 1f, 0.1f);
        FX.Shake(0.25f);
        if (rb != null) rb.AddForce(transform.forward * 4f, ForceMode.VelocityChange);
        bool any = false;
        foreach (var c in Physics.OverlapSphere(transform.position + transform.forward * 1.1f + Vector3.up, 1.6f))
        {
            var e = c.GetComponentInParent<EnemyController>();
            if (e == null || e.IsDead) continue;
            Vector3 dir = (e.transform.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dir) < 0.2f) continue;
            e.TakeDamage(0.5f, dir, 11f);
            any = true;
        }
        if (any) { FX.Sparks(transform.position + transform.forward + Vector3.up * 1.2f, transform.forward); FX.HitStop(0.05f); }
    }

    // ------------------------------------------------------------ bow

    void UpdateBow()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (Arrows <= 0)
            {
                AudioManager.Play2D("no_ammo");
                GameHUD.Toast("Out of arrows! Find a supply crate", new Color(1f, 0.6f, 0.4f), "arrow");
                return;
            }
            IsDrawing = true;
            DrawAmount = 0f;
            AudioManager.PlayAt("bow_draw", transform.position, 0.7f, 0.05f);
        }
        if (IsDrawing)
        {
            DrawAmount = Mathf.Min(1f, DrawAmount + Time.deltaTime * Boons.DrawSpeed / drawTime);
            if (Input.GetMouseButtonUp(0) || !Input.GetMouseButton(0))
            {
                if (DrawAmount > 0.15f) Fire();
                else CancelDraw();
            }
        }
    }

    void Fire()
    {
        IsDrawing = false;
        float draw = DrawAmount;
        DrawAmount = 0f;
        Arrows--;

        Vector3 origin = bowVisual != null ? bowVisual.position : transform.position + Vector3.up * 1.4f;
        Vector3 target;
        if (cameraTransform != null)
        {
            var ray = new Ray(cameraTransform.position, cameraTransform.forward);
            target = ray.GetPoint(120f);
            foreach (var hit in SortedHits(ray, 120f))
            {
                if (hit.transform.IsChildOf(transform)) continue;
                if (Vector3.Dot(hit.point - origin, cameraTransform.forward) < 0.5f) continue;   // behind the bow
                target = hit.point;
                break;
            }
        }
        else target = origin + transform.forward * 50f;

        Vector3 dir = (target - origin).normalized;
        Arrow.Fire(origin + dir * 0.2f, dir, Mathf.Lerp(24f, 58f, draw), Mathf.Lerp(1f, 3.4f, draw), Mathf.Lerp(2f, 9f, draw), transform,
                   Boons.Pierce, Boons.FireArrows && draw > 0.95f);
        AudioManager.PlayAt("bow_release", transform.position, 1f, 0.08f);
        if (GameManager.instance != null) GameManager.instance.RegisterArrowFired();
        FX.Shake(0.06f);
    }

    static RaycastHit[] SortedHits(Ray ray, float dist)
    {
        var hits = Physics.RaycastAll(ray, dist, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        return hits;
    }

    void CancelDraw()
    {
        IsDrawing = false;
        DrawAmount = 0f;
    }

    public int AddArrows(int amount)
    {
        int before = Arrows;
        Arrows = Mathf.Min(maxArrows, Arrows + amount);
        return Arrows - before;
    }

    // ---- hooks for the automated smoke test (no input devices in batch mode)
    bool debugAim;
    public void DebugAim(bool on) => debugAim = on;
    public void DebugSwing() { lastAttack = -9f; ComboStep = 2; animator.SetTrigger("isAttacking"); pendingHits.Add((Time.time + 0.2f, 2)); }
    public void DebugFireAt(Vector3 target, float draw)
    {
        if (Arrows <= 0) return;
        Arrows--;
        Vector3 origin = bowVisual != null ? bowVisual.position : transform.position + Vector3.up * 1.4f;
        Vector3 dir = (target - origin).normalized;
        Arrow.Fire(origin + dir * 0.2f, dir, Mathf.Lerp(24f, 58f, draw), Mathf.Lerp(1f, 3.4f, draw), Mathf.Lerp(2f, 9f, draw), transform);
        if (GameManager.instance != null) GameManager.instance.RegisterArrowFired();
    }

    public void Equip(Weapon w)
    {
        if (w == CurrentWeapon) return;
        CurrentWeapon = w;
        CancelDraw();
        AudioManager.PlayAt("ui_click", transform.position, 0.7f);
    }

    // ------------------------------------------------------------ procedural posing (after animation)

    void LateUpdate()
    {
        float k = 1f - Mathf.Exp(-14f * Time.deltaTime);
        blockWeight = Mathf.Lerp(blockWeight, isBlocking ? 1f : 0f, k);
        aimWeight = Mathf.Lerp(aimWeight, CurrentWeapon == Weapon.Bow && (IsAiming || IsDrawing) ? 1f : 0f, k);

        // lean into dodge rolls
        leanWeight = Mathf.Lerp(leanWeight, Dodging ? 1f : 0f, 1f - Mathf.Exp(-20f * Time.deltaTime));
        if (spine != null && leanWeight > 0.01f)
        {
            Vector3 axis = Vector3.Cross(Vector3.up, dodgeDir.sqrMagnitude > 0.01f ? dodgeDir : transform.forward);
            spine.rotation = Quaternion.AngleAxis(32f * leanWeight, axis) * spine.rotation;
        }

        // smoothly raise the shield arm (same pose holds the bow out when aiming)
        float raise = Mathf.Max(blockWeight, aimWeight);
        if (shieldArmBone != null && raise > 0.001f)
            shieldArmBone.localRotation *= Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(blockRotation), raise);

        PlaceBow();
    }

    void PlaceBow()
    {
        if (bowVisual == null) return;
        bool inHand = CurrentWeapon == Weapon.Bow && leftHand != null;
        Vector3 aim = transform.forward;
        if (cameraTransform != null && (IsAiming || IsDrawing)) aim = cameraTransform.forward;

        if (inHand)
        {
            Vector3 up = Vector3.Slerp(transform.up, Vector3.Cross(aim, transform.right).normalized, 0.5f);
            bowVisual.SetPositionAndRotation(leftHand.position + aim * 0.05f, Quaternion.LookRotation(aim, up) * Quaternion.Euler(0, 0, -12f));
        }
        else
        {
            // slung diagonally across the back
            Vector3 back = (spine != null ? spine.position : transform.position + Vector3.up * 1.3f) - transform.forward * 0.3f + Vector3.up * 0.1f;
            bowVisual.SetPositionAndRotation(back, Quaternion.LookRotation(-transform.forward, Vector3.up) * Quaternion.Euler(0, 0, 38f));
        }

        bool showArrow = inHand && Arrows > 0 && (IsDrawing || IsAiming);
        if (nockedArrow.gameObject.activeSelf != showArrow) nockedArrow.gameObject.SetActive(showArrow);
        if (showArrow)
        {
            Vector3 stringPos = bowVisual.position - bowVisual.forward * (0.02f + DrawAmount * 0.42f);
            nockedArrow.SetPositionAndRotation(stringPos, Quaternion.LookRotation(aim, bowVisual.up));
        }
    }

    void OnDisable()
    {
        if (animator != null) { animator.SetBool("isMoving", false); animator.SetBool("isRunning", false); }
        isBlocking = false;
        if (myHealthScript != null) myHealthScript.isShieldBlocking = false;
    }
}
