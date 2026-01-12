using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;

    [Header("Settings")]
    public float smoothSpeed = 0.1f;    
    public float rotationSpeed = 5.0f;  
    
    [Header("Zoom Settings")]
    public float zoomSpeed = 2.0f;      // How fast the scroll works
    public float minZoom = 2.0f;        // Closest you can get (shoulder view)
    public float maxZoom = 15.0f;       // Furthest you can get (sky view)

    [Header("Vertical Limits")]
    public float minPitch = -10f; 
    public float maxPitch = 60f;  

    // Internal variables
    private float currentYaw = 0f;
    private float currentPitch = 20f; 
    private float currentDistance = 5.0f; // Stores current zoom level
    private Vector3 currentVelocity;

    void Start()
    {
        if (target != null)
        {
            // Calculate starting distance based on your Scene View placement
            currentDistance = Vector3.Distance(transform.position, target.position);
            currentDistance = Mathf.Clamp(currentDistance, minZoom, maxZoom);

            // Align rotation
            currentYaw = target.eulerAngles.y;
        }
    }

    void LateUpdate()
    {
        if (target == null) return;

        // ROTATION INPUT (Right Click)
        if (Input.GetMouseButton(1))
        {
            currentYaw += Input.GetAxis("Mouse X") * rotationSpeed;
            currentPitch -= Input.GetAxis("Mouse Y") * rotationSpeed;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        // ZOOM INPUT (Mouse Scroll)
        // GetAxis("Mouse ScrollWheel") returns positive (up) or negative (down)
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");

        if (scrollInput != 0)
        {
            // Subtracting because "Scroll Up" should make distance SMALLER (Zoom In)
            currentDistance -= scrollInput * zoomSpeed;
            
            // Clamp so you don't clip into the player's head or fly into space
            currentDistance = Mathf.Clamp(currentDistance, minZoom, maxZoom);
        }

        // CALCULATE POSITION
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 focusPoint = target.position + Vector3.up * 1.5f; // Look at head
        
        // Use 'currentDistance' to calculate how far back to stand
        Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * currentDistance);

        // SMOOTH MOVE
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smoothSpeed);
        transform.LookAt(focusPoint);
    }
}