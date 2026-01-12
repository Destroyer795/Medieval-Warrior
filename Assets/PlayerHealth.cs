using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections; 

public class PlayerHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public int maxHealth = 5;
    public int currentHealth;

    [Header("UI")]
    public GameObject gameOverUI; 
    public Image healthBarFill; 

    [Header("Combat Settings")]
    public float parryDuration = 0.5f; 
    public ParticleSystem parryEffect; 
    
    // How wide is the block? (0.2 = Roughly 160 degrees in front)
    public float blockAngleThreshold = 0.2f; 

    public bool isShieldBlocking = false; 
    private bool isParrying = false;

    void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthBar(); 
        
        // We must tell Unity to start time again, or the game stays frozen.
        Time.timeScale = 1; 
    }

    public void ActivateParry()
    {
        if (!isParrying) StartCoroutine(PerformParry());
    }

    IEnumerator PerformParry()
    {
        isParrying = true;
        yield return new WaitForSeconds(parryDuration);
        isParrying = false;
    }

    public void TakeDamage(int amount, Transform attacker)
    {
        // CHECK PARRY 
        if (isParrying)
        {
            Debug.Log("PERFECT PARRY!");
            if (parryEffect != null) parryEffect.Play();
            
            if (attacker != null)
            {
                EnemyController enemyScript = attacker.GetComponent<EnemyController>();
                if (enemyScript != null) enemyScript.TakeDamage(3); 
            }
            return; 
        }

        // CHECK SHIELD BLOCK 
        if (isShieldBlocking && attacker != null)
        {
            Vector3 directionToEnemy = (attacker.position - transform.position).normalized;
            float angle = Vector3.Dot(transform.forward, directionToEnemy);

            if (angle > blockAngleThreshold)
            {
                Debug.Log("Blocked Damage from Front!");
                return; 
            }
            else
            {
                Debug.Log("OUCH! Backstabbed through shield!");
            }
        }

        // NORMAL DAMAGE
        currentHealth -= amount;
        UpdateHealthBar();

        if (currentHealth <= 0) Die();
    }

    void UpdateHealthBar() { if (healthBarFill != null) healthBarFill.fillAmount = (float)currentHealth / maxHealth; }
    
    void Die() 
    { 
        if (gameOverUI != null) gameOverUI.SetActive(true); 
        Time.timeScale = 0; // This pauses the game
        Cursor.visible = true; 
        Cursor.lockState = CursorLockMode.None; 
    }
    
    public void RestartGame() 
    { 
        SceneManager.LoadScene(SceneManager.GetActiveScene().name); 
    }
}