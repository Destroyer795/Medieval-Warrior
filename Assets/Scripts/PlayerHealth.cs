using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public float maxHealth = 100;
    public float currentHealth;

    [Header("UI")]
    public Image healthBarFill;           // legacy bar; the new HUD reads the values directly

    [Header("Combat Settings")]
    public float parryDuration = 0.5f;
    public ParticleSystem parryEffect;
    public float blockAngleThreshold = 0.2f;
    public float perfectBlockWindow = 0.25f;

    [Header("Second Wind")]
    public float regenDelay = 7f;
    public float regenPerSecond = 2.5f;
    [Range(0, 1)] public float regenCap = 0.5f;

    public bool isShieldBlocking = false;

    public event Action<float> Damaged;
    public event Action<float> Healed;

    public bool IsDead => isDead;
    public float Fraction => maxHealth > 0 ? Mathf.Clamp01(currentHealth / maxHealth) : 0f;
    public Vector3 Velocity => body != null ? body.linearVelocity : Vector3.zero;
    public float LastDamageTime { get; private set; } = -99f;
    public float InvulnerableUntil { get; set; }

    bool isParrying = false;
    bool isDead = false;
    float blockStartTime = -99f;
    bool wasBlocking;
    float hurtSoundCooldown;
    Rigidbody body;
    PlayerControlSpeed controller;

    void Awake()
    {
        body = GetComponent<Rigidbody>();
        controller = GetComponent<PlayerControlSpeed>();
        if (maxHealth < 20) maxHealth = 100;   // old scenes stored 5 "hearts"
    }

    void Start()
    {
        ResetHealth();
    }

    void Update()
    {
        if (isShieldBlocking && !wasBlocking) blockStartTime = Time.time;
        wasBlocking = isShieldBlocking;
        hurtSoundCooldown -= Time.deltaTime;

        if (!isDead && Time.time - LastDamageTime > regenDelay && currentHealth < maxHealth * regenCap)
            currentHealth = Mathf.Min(maxHealth * regenCap, currentHealth + regenPerSecond * Time.deltaTime);
        UpdateHealthBar();
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        UpdateHealthBar();
    }

    public void ActivateParry()
    {
        if (!isParrying && !isDead)
            StartCoroutine(PerformParry());
    }

    IEnumerator PerformParry()
    {
        isParrying = true;
        yield return new WaitForSeconds(parryDuration);
        isParrying = false;
    }

    // Legacy signature
    public void TakeDamage(int amount, Transform attacker) => TakeDamage((float)amount, attacker, 0f);

    public void TakeDamage(float amount, Transform attacker, float knockback, bool unblockable = false)
    {
        if (isDead || amount <= 0f) return;
        if (Time.time < InvulnerableUntil) return;   // dodge roll i-frames

        var enemy = attacker != null ? attacker.GetComponent<EnemyController>() : null;
        bool melee = enemy != null && !unblockable && Vector3.Distance(attacker.position, transform.position) < 4f;
        bool blocked = false;

        if (isParrying && melee && Vector3.Dot(transform.forward,(attacker.position-transform.position).normalized)>blockAngleThreshold)
        {
            if (parryEffect != null) parryEffect.Play();
            enemy.TakeDamage(1f, (attacker.position - transform.position).normalized, 7f);
            SuccessfulParry(enemy);
            GameHUD.Toast("Parried!", UIKit.Gold, "swords");
            return;
        }

        if (isShieldBlocking && attacker != null && !unblockable)
        {
            Vector3 dir = (attacker.position - transform.position).normalized;
            if (Vector3.Dot(transform.forward, dir) > blockAngleThreshold)
            {
                bool perfect = Time.time - blockStartTime < perfectBlockWindow + Boons.PerfectBlockBonus;
                FX.Sparks(transform.position + Vector3.up * 1.2f + transform.forward * 0.5f, transform.forward);
                AudioManager.PlayAt("shield_block", transform.position, 1f);
                if (perfect)
                {
                    SuccessfulParry(enemy);
                    GameHUD.Toast("Perfect block!", UIKit.Gold, "shield");
                    Feats.Add("perfectblocks");
                    return;
                }
                amount *= 0.15f;
                blocked = true;
                knockback = GameManager.instance != null && GameManager.instance.Gate != null ? 0f : knockback * .3f;
                if (controller != null && !Boons.FreeBlocking) controller.DrainStamina(12f);
            }
        }

        Apply(amount);

        if (!isDead && knockback > 0f && body != null && attacker != null)
        {
            Vector3 push = transform.position - attacker.position; push.y = 0;
            body.AddForce(push.normalized * knockback + Vector3.up * knockback * 0.35f, ForceMode.VelocityChange);
            if (controller != null) controller.Stun(0.45f);
        }
        if (hurtSoundCooldown <= 0f)
        {
            AudioManager.PlayPlayerDamage(transform.position, amount, Fraction, blocked);
            hurtSoundCooldown = 0.35f;
        }
        FX.Blood(transform.position + Vector3.up * 1.2f, attacker != null ? transform.position - attacker.position : Vector3.up, 1f, false);
        FX.Shake(Mathf.Clamp(amount / 30f, 0.15f, 0.8f));
    }

    void SuccessfulParry(EnemyController enemy)
    {
        Vector3 contact=transform.position+Vector3.up*1.2f+transform.forward*.6f;
        if(enemy!=null)enemy.Stagger(transform.position,8f,enemy.isBoss?.4f:.65f);
        FX.ParryFlash(contact,transform.forward);AudioManager.PlayAt("parry",contact,1f);
        FX.Impact(transform.forward,.65f,.075f);CombatBoons.Parry(this,contact);
    }

    // Damage over time (acid pools): no hurt sound/knockback spam.
    public void TakeBurn(float amount)
    {
        if (isDead || Time.time < InvulnerableUntil) return;
        Apply(amount);
    }

    internal void TakeCorruption(float amount)
    {
        // Exposure is continuous across a dodge. Only reaching safe ground stops it.
        if (!isDead && amount > 0f) Apply(amount);
    }

    void Apply(float amount)
    {
        currentHealth -= amount;
        LastDamageTime = Time.time;
        Damaged?.Invoke(amount);
        if (GameManager.instance != null) GameManager.instance.RegisterDamageTaken(amount);
        UpdateHealthBar();
        if (currentHealth <= 0)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead) return;
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        Healed?.Invoke(amount);
        UpdateHealthBar();
    }

    void UpdateHealthBar()
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = currentHealth / maxHealth;
    }

    void Die()
    {
        if (isDead) return;
        isDead = true;
        currentHealth = 0;
        if (controller != null) controller.enabled = false;
        if (body != null) { body.linearVelocity = Vector3.zero; body.isKinematic = true; }
        var anim = GetComponent<Animator>();
        if (anim != null) anim.enabled = false;
        StartCoroutine(Fall());
    }

    IEnumerator Fall()
    {
        Vector3 start = transform.position;
        Quaternion facing = transform.rotation;
        Vector3 right = facing * Vector3.right;
        Vector3 forward = facing * Vector3.forward;
        var figure = GetComponent<MarchwardenVisual>();
        var pose = new (Transform bone, Quaternion rest, Vector3 hinge, float bend, float finish)[]
        {
            Joint("Spine", right, 26f, 8f),
            Joint("Head", right, 19f, -8f),
            Joint("L Thigh", right, -42f, -10f),
            Joint("R Thigh", right, -28f, 8f),
            Joint("L Calf", right, 60f, 25f),
            Joint("R Calf", right, 49f, 15f),
            Joint("L UpperArm", forward, 24f, 31f),
            Joint("R UpperArm", forward, -21f, -28f)
        };
        (Transform, Quaternion, Vector3, float, float) Joint(string name, Vector3 axis, float bend, float finish)
        {
            var joint = figure != null && figure.Body != null
                ? Array.Find(figure.Body.bones, b => b != null && b.name == "Bip001 " + name) : null;
            return joint == null ? default : (joint, joint.localRotation, joint.InverseTransformDirection(axis), bend, finish);
        }

        // Commit the knees before pitching forward. Keep the spine above the
        // actual surface during the last part of the fall, including on ramps.
        const float length = 1.05f;
        bool landed = false;
        for (float time = 0f; ; time = Mathf.Min(length, time + Time.deltaTime))
        {
            float buckle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.06f, .41f, time));
            float fall = Mathf.InverseLerp(.34f, .87f, time);
            fall *= fall;
            float settle = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.72f, length, time));
            transform.rotation = facing * Quaternion.Euler(82f * fall, -8f * fall, -11f * buckle);
            Vector3 point = start + forward * (.36f * fall) - Vector3.up * (.27f * buckle * (1f - fall));
            if (fall > .25f)
            {
                Vector3 upperBody = point + forward * .68f;
                foreach (var hit in Physics.RaycastAll(upperBody + Vector3.up * .85f, Vector3.down, 2.2f,
                    ~0, QueryTriggerInteraction.Ignore))
                {
                    if (hit.transform.IsChildOf(transform) || hit.collider.GetComponentInParent<EnemyController>() != null ||
                        hit.normal.y < .5f) continue;
                    point.y = Mathf.Max(point.y, hit.point.y + .16f * fall);
                }
            }
            transform.position = point;
            foreach (var part in pose)
                if (part.bone != null)
                    part.bone.localRotation = part.rest * Quaternion.AngleAxis(
                        Mathf.Lerp(part.bend * buckle, part.finish, settle), part.hinge);
            if (!landed && time >= .84f)
            {
                landed = true;
                FX.Dust(transform.position, .52f, 8);
                FX.Shake(.24f);
            }
            if (time >= length) break;
            yield return null;
        }
        yield return new WaitForSeconds(.8f);
        if (GameManager.instance != null)
            GameManager.instance.GameOver();
    }
}
