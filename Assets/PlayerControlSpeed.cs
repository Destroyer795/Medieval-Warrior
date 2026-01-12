using UnityEngine;

public class PlayerControlSpeed : MonoBehaviour
{
    [Header("Movement Settings")]
    public float walkSpeed = 5.0f;
    public float runSpeed = 10.0f;
    public float blockSpeed = 2.0f; 
    public float rotationSpeed = 700.0f;

    [Header("Combat Settings")]
    public float attackRange = 2.0f; 
    
    [Header("Shield Logic")]
    public Transform shieldArmBone; // <--- DRAG 'Bip001 L UpperArm' HERE
    public Vector3 blockRotation = new Vector3(-30, 60, -20); // The angle to hold the shield

    private Animator animator;
    private Transform cameraTransform; 
    private PlayerHealth myHealthScript;

    public bool isBlocking = false; 

    void Start()
    {
        animator = GetComponent<Animator>();
        myHealthScript = GetComponent<PlayerHealth>();
        if (Camera.main != null) cameraTransform = Camera.main.transform;
    }

    void Update()
    {
        // --- 1. BLOCK INPUT ---
        if (Input.GetKey(KeyCode.E))
        {
            isBlocking = true;
        }
        else
        {
            isBlocking = false;
        }

        // --- 2. MOVEMENT ---
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");
        bool isRunning = Input.GetKey(KeyCode.LeftShift);

        if (isBlocking) isRunning = false; 

        Vector3 movement = Vector3.zero;

        if (cameraTransform != null)
        {
            Vector3 camForward = cameraTransform.forward;
            Vector3 camRight = cameraTransform.right;
            camForward.y = 0;
            camRight.y = 0;
            camForward.Normalize();
            camRight.Normalize();
            movement = (camForward * moveZ) + (camRight * moveX);
        }
        else
        {
            movement = new Vector3(moveX, 0, moveZ);
        }

        if (movement.magnitude > 0)
        {
            movement.Normalize();
            float currentSpeed = walkSpeed;
            if (isBlocking) currentSpeed = blockSpeed; 
            else if (isRunning) currentSpeed = runSpeed;

            transform.Translate(movement * currentSpeed * Time.deltaTime, Space.World);
            
            Quaternion toRotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, toRotation, rotationSpeed * Time.deltaTime);

            animator.SetBool("isMoving", true);
            animator.SetBool("isRunning", isRunning);
        }
        else
        {
            animator.SetBool("isMoving", false);
            animator.SetBool("isRunning", false);
        }

        // --- 3. ATTACK ---
        if (Input.GetMouseButtonDown(0) && !isBlocking) 
        {
            animator.SetTrigger("isAttacking");
            if (myHealthScript != null) myHealthScript.ActivateParry(); 
            CheckForHits(); 
        }
        
        // Sync with Health Script
        if(myHealthScript != null) myHealthScript.isShieldBlocking = isBlocking;
    }

    // --- 4. PROCEDURAL ANIMATION (The Magic Part) ---
    // This runs AFTER the normal animation finishes every frame.
    void LateUpdate()
    {
        if (isBlocking && shieldArmBone != null)
        {
            // We rotate the arm relative to its current position
            // You might need to adjust the X, Y, Z numbers in the Inspector!
            shieldArmBone.Rotate(blockRotation);
        }
    }

    void CheckForHits()
    {
        Vector3 hitPosition = transform.position + transform.forward;
        Collider[] hitColliders = Physics.OverlapSphere(hitPosition, attackRange);
        foreach (Collider hitThing in hitColliders)
        {
            if (hitThing.CompareTag("Enemy"))
            {
                EnemyController enemyScript = hitThing.GetComponent<EnemyController>();
                if (enemyScript != null)
                {
                    Vector3 directionToEnemy = (hitThing.transform.position - transform.position).normalized;
                    if (Vector3.Dot(transform.forward, directionToEnemy) > 0.3f)
                    {
                        enemyScript.TakeDamage(1); 
                    }
                }
            }
        }
    }
}