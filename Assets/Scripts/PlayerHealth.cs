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

        if (isParrying && melee)
        {
            if (parryEffect != null) parryEffect.Play();
            enemy.TakeDamage(1f, (attacker.position - transform.position).normalized, 7f);
            FX.Sparks(transform.position + Vector3.up * 1.2f + transform.forward * 0.6f, transform.forward);
            AudioManager.PlayAt("shield_block", transform.position, 1f);
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
                    if (enemy != null) enemy.Stagger(transform.position, 8f);
                    GameHUD.Toast("Perfect block!", UIKit.Gold, "shield");
                    Feats.Add("perfectblocks");
                    return;
                }
                amount *= 0.15f;
                blocked = true;
                knockback *= 0.3f;
                if (controller != null && !Boons.FreeBlocking) controller.DrainStamina(12f);
            }
        }

        Apply(amount);

        if (knockback > 0f && body != null && attacker != null)
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

    // Damage over time (acid pools): no hurt sound/knockback spam.
    public void TakeBurn(float amount)
    {
        if (isDead || Time.time < InvulnerableUntil) return;
        Apply(amount);
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
        if (anim != null) { anim.SetBool("isMoving", false); anim.SetBool("isRunning", false); }
        StartCoroutine(Fall());
    }

    IEnumerator Fall()
    {
        // topple sideways, then hand over to the game-over screen
        Quaternion start = transform.rotation, end = start * Quaternion.Euler(0, 0, 85f);
        for (float t = 0; t < 1f; t += Time.deltaTime / 0.7f)
        {
            transform.rotation = Quaternion.Slerp(start, end, t * t);
            yield return null;
        }
        yield return new WaitForSeconds(1.2f);
        if (GameManager.instance != null)
            GameManager.instance.GameOver();
    }
}
