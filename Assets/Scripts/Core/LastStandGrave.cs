using UnityEngine;
using UnityEngine.AI;

// The grave owns its camera and player locks only while the stone and the double emerge.
internal sealed class LastStandGrave : MonoBehaviour
{
    const float EmergenceAt = 3.45f, ReturnAt = 5.55f, ReleaseAt = 6.85f;
    PlayerHealth knight;
    PlayerControlSpeed control;
    Rigidbody body;
    CameraFollow orbit;
    Transform stone;
    Light rim;
    Vector3 duelistSite;
    bool restoreControl, restoreDynamicBody;
    float previousImmunity;
    internal float Age { get; private set; }
    internal bool Revealing { get; private set; }
    internal GraveDuelist Duelist { get; private set; }
    internal bool Active => Revealing || Duelist != null && !Duelist.IsDefeated;

    internal static LastStandGrave TryRaise(Transform owner, PlayerHealth player, Vector3 center, float radius)
    {
        var cam = Camera.main;
        Vector3 heading = cam != null ? cam.transform.forward : player.transform.forward;
        if (!TrySite(player.transform, center, radius, heading, out var site, out var facing, out var spawn))
            return null;
        var root = new GameObject("The knight's grave");
        root.transform.SetParent(owner, false);
        root.transform.SetPositionAndRotation(site, facing);
        var grave = root.AddComponent<LastStandGrave>();
        grave.knight = player;
        grave.duelistSite = spawn;
        grave.orbit = cam != null ? cam.GetComponent<CameraFollow>() : null;
        grave.stone = GraveMarker.Create(root.transform, GameSettings.HeroName);
        grave.rim = new GameObject("Emergence light").AddComponent<Light>();
        grave.rim.transform.SetParent(root.transform, false);
        grave.rim.transform.localPosition = new Vector3(2.65f, 2.15f, -1.75f);
        grave.rim.color = new Color(.65f,.87f,1f);
        grave.rim.range = 3.5f;
        grave.rim.intensity = 0f;
        grave.stone.localPosition = Vector3.down * 2.35f;
        grave.control = player.GetComponent<PlayerControlSpeed>();
        grave.body = player.GetComponent<Rigidbody>();
        grave.restoreControl = grave.control != null && grave.control.enabled;
        grave.restoreDynamicBody = grave.body != null && !grave.body.isKinematic;
        grave.previousImmunity = player.InvulnerableUntil;
        player.InvulnerableUntil = float.PositiveInfinity;
        if (grave.restoreControl) grave.control.enabled = false;
        if (grave.restoreDynamicBody)
        {
            grave.body.linearVelocity = Vector3.zero;
            grave.body.angularVelocity = Vector3.zero;
            grave.body.isKinematic = true;
        }
        grave.Revealing = true;
        AudioManager.PlayAt("emerge", site, .9f, .03f, .73f);
        FX.Dust(site, 1.2f, 25);
        return grave;
    }

    static bool TrySite(Transform knight, Vector3 courtyard, float radius, Vector3 heading,
        out Vector3 stone, out Quaternion facing, out Vector3 spawn)
    {
        stone = spawn = default;
        facing = Quaternion.identity;
        if (!NavMesh.SamplePosition(knight.position, out var foot, 32f, NavMesh.AllAreas)) return false;
        var path = new NavMeshPath();
        Vector3 forward = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
        if (forward.sqrMagnitude < .1f) forward = Vector3.forward;
        // Close to a wall, look around the knight. If the knight is outside the front,
        // use a court-side patch with a complete route to their nearest walkable ground.
        for (int ring = 0; ring < 3; ring++)
        for (int i = 0; i < 24; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 137.5f, 0f) * forward;
            Vector3 guess = ring == 2 ? courtyard + dir * Mathf.Min(radius - 3f, 8f) :
                foot.position + dir * (ring == 0 ? 6.8f : 4.1f);
            if (!NavMesh.SamplePosition(guess, out var slab, 2.5f, NavMesh.AllAreas) ||
                Vector3.ProjectOnPlane(slab.position - courtyard, Vector3.up).magnitude > radius - 2f) continue;
            Vector3 ahead = Vector3.ProjectOnPlane(slab.position - foot.position, Vector3.up);
            if (ahead.sqrMagnitude < 8f) continue;
            Quaternion rotation = Quaternion.LookRotation(ahead);
            Vector3 emergence = slab.position + rotation * new Vector3(1.45f, 0f, -.35f);
            if (!NavMesh.SamplePosition(emergence, out var target, .65f, NavMesh.AllAreas) ||
                Mathf.Abs(target.position.y - slab.position.y) > .4f ||
                NavMesh.Raycast(slab.position, target.position, out _, NavMesh.AllAreas) ||
                !NavMesh.CalculatePath(target.position, foot.position, NavMesh.AllAreas, path) ||
                path.status != NavMeshPathStatus.PathComplete) continue;
            // Validate the stone's footprint and the lens positions for both the inscription
            // and the wider reveal. NavMesh alone cannot tell us about overhangs.
            Vector3 focus = slab.position + rotation * new Vector3(0f, 1.05f, -.32f);
            Vector3 wide = slab.position + rotation * new Vector3(.7f, 1.03f, -.25f);
            if (Blocked(slab.position + Vector3.up * 1.17f, new Vector3(.95f,.9f,.32f), rotation, knight) ||
                !ClearShot(focus, rotation * new Vector3(0f,.18f,-2.75f), knight) ||
                !ClearShot(wide, rotation * new Vector3(0f,.5f,-4.5f), knight)) continue;
            stone = slab.position;
            facing = rotation;
            spawn = target.position;
            return true;
        }
        return false;
    }

    static bool Blocked(Vector3 center, Vector3 halfSize, Quaternion facing, Transform knight)
    {
        foreach (var collider in Physics.OverlapBox(center, halfSize, facing, ~0, QueryTriggerInteraction.Ignore))
            if (!collider.transform.IsChildOf(knight) && collider.GetComponentInParent<EnemyController>() == null) return true;
        return false;
    }

    static bool ClearShot(Vector3 focus, Vector3 offset, Transform knight)
    {
        foreach (var hit in Physics.SphereCastAll(focus, .26f, offset.normalized, offset.magnitude, ~0, QueryTriggerInteraction.Ignore))
            if (!hit.collider.transform.IsChildOf(knight) && hit.collider.GetComponentInParent<EnemyController>() == null) return false;
        return true;
    }

    static float Ease(float from, float to, float age) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, to, age));

    internal void TickReveal(float deltaTime)
    {
        float previous = Age;
        Age += deltaTime;
        rim.intensity = 3.5f * Ease(EmergenceAt, EmergenceAt + .9f, Age) * (1f - Ease(ReturnAt, ReleaseAt, Age));
        stone.localPosition = Vector3.down * (2.35f * (1f - Ease(0f, 1.75f, Age)));
        if (previous < 1.65f && Age >= 1.65f) FX.Dust(transform.position, .6f, 9);
        if (Age >= EmergenceAt && Duelist == null)
        {
            Duelist = GraveDuelist.Spawn(duelistSite, knight);
            if (Duelist != null)
            {
                AudioManager.PlayAt("emerge", duelistSite, .78f, .03f, .82f);
                FX.Dust(duelistSite, 1.1f, 22);
            }
        }
        if (Duelist != null) Duelist.SetEmergence(Ease(EmergenceAt, ReturnAt - .2f, Age));
        float widen = Ease(EmergenceAt - .45f, EmergenceAt + .9f, Age);
        if (orbit != null)
        {
            Vector3 focus = transform.TransformPoint(Vector3.Lerp(new Vector3(0f,1.05f,-.32f), new Vector3(.7f,1.03f,-.25f), widen));
            Vector3 eye = focus + transform.rotation * Vector3.Lerp(new Vector3(0f,.18f,-2.75f), new Vector3(0f,.5f,-4.5f), widen);
            orbit.FrameGrave(focus, eye, Ease(0f, 1.8f, Age) * (1f - Ease(ReturnAt, ReleaseAt, Age)),
                Mathf.Lerp(32f, 42f, widen));
            if (previous < ReturnAt && Age >= ReturnAt) orbit.FaceGraveFromKnight(duelistSite);
        }
        if (Age >= ReleaseAt) ReleaseKnight(true);
    }

    void Update()
    {
        if (!Active) return;
        var game = GameManager.instance;
        if (knight == null || knight.IsDead || game == null || game.State != GameManager.MatchState.Playing)
            EndEncounter();
    }

    void ReleaseKnight(bool resumeCombat)
    {
        if (!Revealing) return;
        Revealing = false;
        if (orbit != null) orbit.EndGraveShot();
        if (knight == null) return;
        knight.InvulnerableUntil = resumeCombat ? Mathf.Max(previousImmunity, Time.time + 1f) : previousImmunity;
        if (knight.IsDead) return;
        if (restoreDynamicBody && body != null) body.isKinematic = false;
        if (restoreControl && control != null) control.enabled = true;
    }

    internal void EndEncounter()
    {
        ReleaseKnight(false);
        if (Duelist != null)
        {
            // The royal volley takes its target snapshot before deferred destruction runs.
            Duelist.gameObject.SetActive(false);
            Destroy(Duelist.gameObject);
            Duelist = null;
        }
    }

    void OnDisable() => EndEncounter();
}
