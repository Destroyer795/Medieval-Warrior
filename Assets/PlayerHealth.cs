using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 5;
    public int currentHealth;

    [Header("UI")]
    public Image healthBarFill;

    [Header("Combat Settings")]
    public float parryDuration = 0.5f;
    public ParticleSystem parryEffect;
    public float blockAngleThreshold = 0.2f;

    public bool isShieldBlocking = false;

    bool isParrying = false;
    bool isDead = false; // 👈 IMPORTANT

    void Start()
    {
        ResetHealth();
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

    public void TakeDamage(int amount, Transform attacker)
    {
        if (isDead) return; // 👈 PREVENT MULTIPLE DEATHS

        if (isParrying)
        {
            if (parryEffect != null)
                parryEffect.Play();

            if (attacker != null)
            {
                EnemyController enemy = attacker.GetComponent<EnemyController>();
                if (enemy != null)
                    enemy.TakeDamage(3);
            }
            return;
        }

        if (isShieldBlocking && attacker != null)
        {
            Vector3 dir = (attacker.position - transform.position).normalized;
            float angle = Vector3.Dot(transform.forward, dir);

            if (angle > blockAngleThreshold)
                return;
        }

        currentHealth -= amount;
        UpdateHealthBar();

        if (currentHealth <= 0)
            Die();
    }

    void UpdateHealthBar()
    {
        if (healthBarFill != null)
            healthBarFill.fillAmount = (float)currentHealth / maxHealth;
    }

    void Die()
    {
        isDead = true; // 👈 LOCK STATE

        if (GameManager.instance != null)
            GameManager.instance.GameOver();
    }
}
