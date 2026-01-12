using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    public int health = 3;
    
    [Header("Combat Settings")]
    public float attackRange = 2.0f; // How close to stand
    public float timeBetweenAttacks = 1.5f; // Speed of hits
    private float attackTimer; // Cooldown timer

    [Header("References")]
    public Transform player;
    private Animator animator;
    private CapsuleCollider myCollider;
    private NavMeshAgent navAgent;
    private PlayerHealth playerHealthScript; // Reference to Knight's HP

    void Start()
    {
        animator = GetComponent<Animator>();
        myCollider = GetComponent<CapsuleCollider>();
        navAgent = GetComponent<NavMeshAgent>();

        // Auto-find the player by Tag first
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealthScript = playerObj.GetComponent<PlayerHealth>();
        }
        else
        {
            // Backup method: Find by script if Tag is missing
            PlayerControlSpeed playerScript = FindObjectOfType<PlayerControlSpeed>();
            if (playerScript != null)
            {
                player = playerScript.transform;
                playerHealthScript = playerScript.GetComponent<PlayerHealth>();
            }
        }
    }

    void Update()
    {
        if (health <= 0 || player == null) return;

        // Calculate distance to player
        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        // CHASE
        if (distanceToPlayer > attackRange)
        {
            navAgent.isStopped = false;
            navAgent.SetDestination(player.position);
            animator.SetBool("isMoving", true);
        }
        else 
        // ATTACK
        {
            navAgent.isStopped = true; // Stop moving to hit
            animator.SetBool("isMoving", false);

            // Cooldown logic
            if (Time.time >= attackTimer)
            {
                AttackPlayer();
                attackTimer = Time.time + timeBetweenAttacks; // Reset timer
            }
        }
    }

    void AttackPlayer()
    {
        // Face the player before hitting
        transform.LookAt(player);
        
        // Play the animation immediately so it LOOKS real
        animator.SetTrigger("attack");

        // Don't damage yet! Start the "Wind-Up" timer
        StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        // WAIT: Sync this number with your animation (approx 0.6s)
        yield return new WaitForSeconds(0.6f);

        // CHECK AGAIN: Is the player still close?
        if (player != null)
        {
            float distanceNow = Vector3.Distance(transform.position, player.position);

            // If player is still within range
            if (distanceNow <= attackRange + 0.5f) 
            {
                if (playerHealthScript != null)
                {
                    // SEND ATTACK DATA 
                    // passing 'transform' so player knows where the hit came from (for Blocking)
                    playerHealthScript.TakeDamage(1, transform);
                }
            }
        }
    }

    public void TakeDamage(int damageAmount)
    {
        health -= damageAmount;
        
        // Optional: Play hit animation here if you have one
        // animator.SetTrigger("damage"); 

        if (health <= 0) Die();
    }

    void Die()
    {
        // Visuals
        animator.SetTrigger("die");
        myCollider.enabled = false; // Stop blocking path
        navAgent.enabled = false;   // Stop moving

        // Report Death to Game Manager (For "Kill 10" Victory)
        if (GameManager.instance != null)
        {
            GameManager.instance.AddKill();
        }

        // Cleanup
        this.enabled = false; // Disable this script
        gameObject.tag = "Untagged"; // Prevent finding it again
        Destroy(gameObject, 5.0f); // Remove body after 5 seconds
    }
}