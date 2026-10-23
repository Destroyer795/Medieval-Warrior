using UnityEngine;

// Third-person orbit camera: mouse look, scroll zoom, over-the-shoulder aim for the bow,
// wall collision and trauma-based screen shake.
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

    [Header("Collision")]
    public float collisionRadius = 0.25f;

    [Header("Aiming")]
    public float aimDistance = 2.9f;
    public Vector3 aimShoulder = new Vector3(0.8f, 0.25f, 0f);
    public float aimFovDrop = 14f;

    // Internal variables
    private float currentYaw = 0f;
    private float currentPitch = 18f;
    private float currentDistance = 5.0f; // Stores current zoom level
    private Vector3 currentVelocity;
    private float aimBlend, trauma;
    private PlayerControlSpeed player;
    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
        if (target != null)
        {
            currentDistance = Mathf.Clamp(Vector3.Distance(transform.position, target.position), Mathf.Max(minZoom, 4f), maxZoom);
            currentYaw = target.eulerAngles.y;
            player = target.GetComponent<PlayerControlSpeed>();
        }
    }

    public void AddShake(float amount) => trauma = Mathf.Clamp01(trauma + amount);

    void LateUpdate()
    {
        if (target == null) return;
        bool paused = Time.timeScale < 0.01f;

        // ROTATION: mouse look while the cursor is captured (otherwise hold right mouse)
        if (!paused && (Cursor.lockState == CursorLockMode.Locked || Input.GetMouseButton(1)))
        {
            float sens = rotationSpeed * GameSettings.MouseSensitivity / 3.5f * (aimBlend > 0.5f ? 0.6f : 1f);
            currentYaw += Input.GetAxis("Mouse X") * sens;
            currentPitch -= Input.GetAxis("Mouse Y") * sens * (GameSettings.InvertY ? -1f : 1f);
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);
        }

        // ZOOM INPUT (Mouse Scroll)
        float scrollInput = paused ? 0f : Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0)
            currentDistance = Mathf.Clamp(currentDistance - scrollInput * zoomSpeed, minZoom, maxZoom);

        bool aiming = player != null && player.CurrentWeapon == Weapon.Bow && (player.IsAiming || player.IsDrawing);
        aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, Time.unscaledDeltaTime * 5f);
        float e = aimBlend * aimBlend * (3f - 2f * aimBlend);

        // CALCULATE POSITION
        Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0);
        Vector3 focusPoint = target.position + Vector3.up * 1.5f + rotation * (aimShoulder * e);
        float distance = Mathf.Lerp(currentDistance, aimDistance, e);
        Vector3 desiredPosition = focusPoint - (rotation * Vector3.forward * distance);

        // COLLISION: pull the camera in front of walls / terrain instead of clipping through them
        Vector3 toCamera = desiredPosition - focusPoint;
        RaycastHit[] hits = Physics.SphereCastAll(focusPoint, collisionRadius, toCamera.normalized, toCamera.magnitude, ~0, QueryTriggerInteraction.Ignore);
        float closest = toCamera.magnitude;
        foreach (RaycastHit hit in hits)
        {
            if (hit.distance <= 0f || hit.collider.transform.IsChildOf(target)) continue;
            if (hit.collider.GetComponentInParent<EnemyController>() != null) continue;
            closest = Mathf.Min(closest, hit.distance);
        }
        desiredPosition = focusPoint + toCamera.normalized * Mathf.Max(closest, 0.5f);

        // SMOOTH MOVE (snappier when aiming so the crosshair feels precise)
        float smooth = Mathf.Lerp(smoothSpeed, 0.03f, e);
        transform.position = Vector3.SmoothDamp(transform.position, desiredPosition, ref currentVelocity, smooth, Mathf.Infinity, Time.unscaledDeltaTime);
        transform.rotation = Quaternion.LookRotation(focusPoint + rotation * Vector3.forward * 20f * e - transform.position + (1f - e) * (focusPoint - transform.position));

        // SHAKE
        if (trauma > 0f && !paused)
        {
            float s = trauma * trauma;
            float t = Time.unscaledTime * 25f;
            transform.rotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 0) - 0.5f) * 6f * s, (Mathf.PerlinNoise(0, t) - 0.5f) * 6f * s, (Mathf.PerlinNoise(t, t) - 0.5f) * 4f * s);
            trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 1.6f);
        }

        if (cam != null) cam.fieldOfView = Mathf.Lerp(GameSettings.FieldOfView, GameSettings.FieldOfView - aimFovDrop, e);
    }
}
