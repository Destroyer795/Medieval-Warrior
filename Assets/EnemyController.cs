using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;

public class EnemyController : MonoBehaviour
{
    [Header("Stats")]
    public int health = 3;

    [Header("Combat")]
    public float attackRange = 2.0f;
    public float timeBetweenAttacks = 1.5f;

    [Header("Navigation")]
    public float repathRate = 0.25f;
    public float stuckCheckTime = 1.5f;
    public float stuckDistance = 0.3f;

    [Header("References")]
    public Transform player;

    private Animator animator;
    private CapsuleCollider myCollider;
    private NavMeshAgent navAgent;
    private PlayerHealth playerHealthScript;

    private float attackTimer;
    private float repathTimer;

    private Vector3 lastPosition;
    private float stuckTimer;

    private bool isDead = false;   // 🔒 IMPORTANT

    void Start()
    {
        animator = GetComponent<Animator>();
        myCollider = GetComponent<CapsuleCollider>();
        navAgent = GetComponent<NavMeshAgent>();

        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            player = playerObj.transform;
            playerHealthScript = playerObj.GetComponent<PlayerHealth>();
        }

        lastPosition = transform.position;
    }

    void Update()
    {
        if (isDead || health <= 0 || player == null || !navAgent.enabled) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);

        if (distanceToPlayer <= attackRange)
        {
            navAgent.isStopped = true;
            animator.SetBool("isMoving", false);

            if (Time.time >= attackTimer)
            {
                AttackPlayer();
                attackTimer = Time.time + timeBetweenAttacks;
            }
            return;
        }

        navAgent.isStopped = false;
        animator.SetBool("isMoving", true);

        repathTimer += Time.deltaTime;
        if (repathTimer >= repathRate && !navAgent.pathPending)
        {
            navAgent.SetDestination(player.position);
            repathTimer = 0f;
        }

        float movedDistance = Vector3.Distance(transform.position, lastPosition);
        if (movedDistance < stuckDistance)
        {
            stuckTimer += Time.deltaTime;
            if (stuckTimer >= stuckCheckTime)
            {
                navAgent.ResetPath();
                navAgent.SetDestination(player.position);
                stuckTimer = 0f;
            }
        }
        else
        {
            stuckTimer = 0f;
        }

        lastPosition = transform.position;
    }

    void AttackPlayer()
    {
        if (isDead) return;

        transform.LookAt(player);
        animator.SetTrigger("attack");
        StartCoroutine(AttackRoutine());
    }

    IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(0.6f);

        if (isDead) yield break;

        if (player != null &&
            Vector3.Distance(transform.position, player.position) <= attackRange + 0.5f)
        {
            if (playerHealthScript != null)
            {
                playerHealthScript.TakeDamage(1, transform);
            }
        }
    }

    public void TakeDamage(int damageAmount)
    {
        if (isDead) return;   // 🔒 BLOCK DOUBLE DAMAGE

        health -= damageAmount;

        if (health <= 0)
        {
            Die();
        }
    }

    void Die()
    {
        if (isDead) return;   // 🔒 BLOCK DOUBLE DEATH
        isDead = true;

        animator.SetTrigger("die");
        myCollider.enabled = false;
        navAgent.enabled = false;

        if (GameManager.instance != null)
        {
            GameManager.instance.AddKill();
        }

        gameObject.tag = "Untagged";
        Destroy(gameObject, 5f);
    }
}
