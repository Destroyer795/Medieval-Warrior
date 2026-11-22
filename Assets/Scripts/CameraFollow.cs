using UnityEngine;

// Third-person orbit camera: mouse look, scroll zoom, over-the-shoulder aim for the bow,
// wall collision and trauma-based screen shake.
[DefaultExecutionOrder(-50)]
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
    float orbitYaw, orbitPitch, yawVelocity, pitchVelocity, boomDistance, boomVelocity;
    Vector3 followPoint;
    Vector3 impactAngles;
    RaycastHit[] collisionHits=new RaycastHit[32];

    // Input heading is independent of camera follow lag and trauma shake.
    public Vector3 LookDirection => Quaternion.Euler(orbitPitch, orbitYaw, 0f) * Vector3.forward;
    internal float DawnView { get; set; }
    internal bool FramingGrave { get; private set; }
    Vector3 graveFocus, graveEye;
    float graveBlend, graveFov;

    bool siegeShot;
    Vector3 siegeFocus, siegeEye;
    internal void FrameSiege(Vector3 focus, Vector3 eye)
    {
        siegeShot = true;
        siegeFocus = focus;
        siegeEye = eye;
    }

    internal void EndSiegeShot()
    {
        siegeShot = false;
        if (cam != null) cam.fieldOfView = GameSettings.FieldOfView;
    }

    internal void FrameGrave(Vector3 focus, Vector3 eye, float blend, float fieldOfView)
    {
        FramingGrave = true;
        graveFocus = focus;
        graveEye = eye;
        graveBlend = blend;
        graveFov = fieldOfView;
    }

    internal void FaceGraveFromKnight(Vector3 grave)
    {
        if (target == null) return;
        Vector3 heading = grave - target.position;
        currentYaw = orbitYaw = Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg;
        currentPitch = orbitPitch = Mathf.Clamp(13f, minPitch, maxPitch);
        yawVelocity = pitchVelocity = 0f;
    }

    internal void EndGraveShot()
    {
        FramingGrave = false;
        if (cam != null) cam.fieldOfView = GameSettings.FieldOfView;
    }

    void Start()
    {
        cam = GetComponent<Camera>();
        if (target != null)
        {
            currentDistance = Mathf.Clamp(Vector3.Distance(transform.position, target.position), Mathf.Max(minZoom, 4f), maxZoom);
            currentYaw = target.eulerAngles.y;
            player = target.GetComponent<PlayerControlSpeed>();
            orbitYaw = currentYaw;
            orbitPitch = currentPitch;
            boomDistance = currentDistance;
            followPoint = target.position + Vector3.up * 1.5f;
        }
    }

    public void AddShake(float amount) => trauma = Mathf.Clamp01(trauma + amount);
    public void AddImpact(Vector3 direction,float strength)
    {
        var local=Quaternion.Inverse(Quaternion.Euler(orbitPitch,orbitYaw,0))*direction.normalized;
        impactAngles=Vector3.ClampMagnitude(impactAngles+new Vector3(-1.8f,local.x*2.8f,-local.x*2f)*strength,6);
    }

    void Update()
    {
        if (target == null || FramingGrave || siegeShot) return;
        bool paused = Time.timeScale < 0.01f&&!CombatTime.IsHitStopped;
        if (paused) return;
        float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);

        // ROTATION: mouse look while the cursor is captured (otherwise hold right mouse)
        if (!paused && (Cursor.lockState == CursorLockMode.Locked || Input.GetMouseButton(1) || GameInput.UsingGamepad))
        {
            float sens = rotationSpeed * GameSettings.MouseSensitivity / 3.5f * Mathf.Lerp(1f, 0.6f, aimBlend);
            Vector2 look = GameInput.Look;
            currentYaw += look.x * sens;
            currentPitch -= look.y * sens * (GameSettings.InvertY ? -1f : 1f);

        }

        var frontage = GameManager.instance?.Outer?.Structure;
        float pitchLimit = frontage != null && frontage.IsOnParapet(target.position) ? 86f : maxPitch;
        currentPitch = Mathf.Clamp(currentPitch, minPitch, pitchLimit);

        // ZOOM INPUT (Mouse Scroll)
        float scrollInput = paused ? 0f : GameInput.Zoom;
        if (scrollInput != 0)
            currentDistance = Mathf.Clamp(currentDistance - scrollInput * zoomSpeed, minZoom, maxZoom);

        bool aiming = DawnView <= 0f && player != null && player.CurrentWeapon == Weapon.Bow && (player.IsAiming || player.IsDrawing);
        aimBlend = Mathf.MoveTowards(aimBlend, aiming ? 1f : 0f, dt * 4f);
        float e = aimBlend * aimBlend * (3f - 2f * aimBlend);
        // Mouse delta is already a per-frame displacement. Do not add another
        // spring between the mouse and view; retain filtering for analog sticks.
        float angularSmooth = Mathf.Lerp(0.055f, 0.035f, e);
        if(GameInput.UsingGamepad)
        {
            orbitYaw = Mathf.SmoothDampAngle(orbitYaw, currentYaw, ref yawVelocity, angularSmooth, Mathf.Infinity, dt);
            orbitPitch = Mathf.SmoothDampAngle(orbitPitch, currentPitch, ref pitchVelocity, angularSmooth, Mathf.Infinity, dt);
        }
        else{orbitYaw=currentYaw;orbitPitch=currentPitch;yawVelocity=pitchVelocity=0;}
    }

    void LateUpdate()
    {
        if (siegeShot)
        {
            transform.SetPositionAndRotation(siegeEye, Quaternion.LookRotation(siegeFocus - siegeEye));
            if (cam != null) cam.fieldOfView = 64;
            return;
        }
        if(target==null||Time.timeScale<.01f&&!CombatTime.IsHitStopped)return;
        float dt=Mathf.Min(Time.unscaledDeltaTime,.05f),e=aimBlend*aimBlend*(3-2*aimBlend);
        float pitch = Mathf.Lerp(orbitPitch, 6f, DawnView * .8f);
        Quaternion rotation = Quaternion.Euler(pitch, orbitYaw, 0);
        Vector3 targetPoint = target.position + Vector3.up * 1.5f;
        if ((targetPoint - followPoint).sqrMagnitude > 100f)
        { followPoint = targetPoint; currentVelocity = Vector3.zero; }
        followPoint = Vector3.SmoothDamp(followPoint, targetPoint, ref currentVelocity,
            Mathf.Lerp(Mathf.Clamp(smoothSpeed, 0.015f, 0.04f), 0.025f, e), Mathf.Infinity, dt);
        // Resolve the shoulder offset first, so aiming beside a wall cannot place
        // the boom's origin on the other side of it.
        Vector3 shoulder = rotation * (aimShoulder * e);
        float shoulderLength = shoulder.magnitude;
        Vector3 focusPoint = followPoint;
        if (shoulderLength > 0.001f)
            focusPoint += shoulder.normalized * ClearDistance(followPoint, shoulder.normalized, shoulderLength);
        float distance = Mathf.Lerp(currentDistance, aimDistance, e) + 1.8f * DawnView;
        Vector3 back = -(rotation * Vector3.forward);
        float clear = ClearDistance(focusPoint, back, distance);
        // Obstruction retracts immediately; release eases out. Never smooth an
        // already collision-corrected world position back through a wall.
        if (clear < boomDistance) { boomDistance = clear; boomVelocity = 0f; }
        else boomDistance = Mathf.SmoothDamp(boomDistance, clear, ref boomVelocity, 0.18f, Mathf.Infinity, dt);
        transform.position = focusPoint + back * Mathf.Min(boomDistance, clear);
        transform.rotation = rotation;
        transform.rotation*=Quaternion.Euler(impactAngles);
        impactAngles*=Mathf.Exp(-20*dt);

        // SHAKE
        if (trauma > 0f)
        {
            float s = trauma * trauma;
            float t = Time.unscaledTime * 25f;
            transform.rotation *= Quaternion.Euler((Mathf.PerlinNoise(t, 0) - 0.5f) * 6f * s, (Mathf.PerlinNoise(0, t) - 0.5f) * 6f * s, (Mathf.PerlinNoise(t, t) - 0.5f) * 4f * s);
            trauma = Mathf.Max(0f, trauma - Time.unscaledDeltaTime * 1.6f);
        }

        if (cam != null) cam.fieldOfView = Mathf.Lerp(GameSettings.FieldOfView, GameSettings.FieldOfView - aimFovDrop, e);
        if (FramingGrave) ApplyGraveShot();
    }

    void ApplyGraveShot()
    {
        Vector3 toEye = graveEye - graveFocus;
        Vector3 eye = graveFocus + toEye.normalized * ClearDistance(graveFocus, toEye.normalized, toEye.magnitude);
        Vector3 travel = eye - transform.position;
        float blend = graveBlend;
        // A cut across an obstructed lane keeps both views on their own side of the masonry.
        if (travel.sqrMagnitude > .01f && ClearDistance(transform.position, travel.normalized, travel.magnitude) < travel.magnitude - .01f)
            blend = blend < .5f ? 0f : 1f;
        transform.SetPositionAndRotation(Vector3.Lerp(transform.position, eye, blend),
            Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(-toEye, Vector3.up), blend));
        if (cam != null) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, graveFov, blend);
    }

    float ClearDistance(Vector3 origin, Vector3 direction, float distance)
    {
        float clear = distance;
        int count;
        while((count=Physics.SphereCastNonAlloc(origin,collisionRadius,direction,collisionHits,distance,~0,QueryTriggerInteraction.Ignore))==collisionHits.Length)
            System.Array.Resize(ref collisionHits,collisionHits.Length*2);
        for(int i=0;i<count;i++)
        {
            var hit=collisionHits[i];
            if (hit.collider.transform.IsChildOf(target) || hit.collider.GetComponentInParent<EnemyController>() != null) continue;
            clear = Mathf.Min(clear, Mathf.Max(0f, hit.distance - 0.04f));
        }
        return clear;
    }
}
