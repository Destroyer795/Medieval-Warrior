using UnityEngine;

// The knight model ships without archery animations, so the archer stance is built procedurally
// on top of whatever the Animator plays (walk, idle, ...), after it has posed the skeleton:
//   * the torso turns side-on to the target and follows the aim's pitch
//   * the bow arm is solved with two-bone IK to point straight down the line of the shot
//   * the draw hand is solved to sit on the string, travelling back to the cheek with draw strength,
//     and snaps through on release
//   * the bowstring is a live line through the draw hand; the nocked arrow rides on it
// The IK works on bone positions only, so it does not depend on the rig's bone axis conventions.
[DefaultExecutionOrder(100)]
public class ArcherRig : MonoBehaviour
{
    public float torsoTurn = 70f;          // degrees the chest opens towards the bow side
    public float fullDrawDistance = 0.62f; // string travel from brace to anchor

    PlayerControlSpeed pc;
    Transform cam;
    CameraFollow orbitCamera;
    Transform spine, neck, head, lUpper, lFore, lHand, rUpper, rFore, rHand;
    Transform bow, nocked;
    LineRenderer bowString;
    float weight, releaseKick, drawSmoothed;
    Vector3 stringPoint;
    Quaternion torsoOffset = Quaternion.identity;
    Vector3 bowTargetLocal, drawTargetLocal;
    float aimYaw, aimPitch;
    bool targetsReady, aimReady;

    const float BowHalf = 0.62f, NockZ = -0.03f;

    public Vector3 ArrowOrigin => bow != null ? bow.position : transform.position + Vector3.up * 1.4f;
    public bool Ready => lUpper != null && rUpper != null;

    public void Init(PlayerControlSpeed owner)
    {
        pc = owner;
        if (Camera.main != null) cam = Camera.main.transform;
        if (cam != null) orbitCamera = cam.GetComponent<CameraFollow>();
        spine = Find("Bip001 Spine1") ?? Find("Bip001 Spine");
        neck = Find("Bip001 Neck");
        head = Find("Bip001 Head");
        lUpper = Find("Bip001 L UpperArm"); lFore = Find("Bip001 L Forearm"); lHand = Find("Bip001 L Hand");
        rUpper = Find("Bip001 R UpperArm"); rFore = Find("Bip001 R Forearm"); rHand = Find("Bip001 R Hand");
        BuildBow();
    }

    Transform Find(string name) => FindBone(transform, name);

    static Transform FindBone(Transform root, string name)
    {
        if (root.name == name) return root;
        foreach (Transform c in root)
        {
            var f = FindBone(c, name);
            if (f != null) return f;
        }
        return null;
    }

    void BuildBow()
    {
        var t = Resources.Load<GameObject>("Prefabs/Bow");
        var go = t != null ? Instantiate(t) : ProceduralModels.Spawn("Bow", ProceduralModels.BowMesh(), ProceduralModels.BowMaterials(0));
        go.name = "Bow (equipment)";
        if (go.TryGetComponent(out MeshRenderer mr)) mr.sharedMaterials = ProceduralModels.BowMaterials(GameSettings.BowFinish);
        bow = go.transform;
        bow.SetParent(transform, true);

        bowString = new GameObject("Bowstring").AddComponent<LineRenderer>();
        bowString.transform.SetParent(bow, false);
        bowString.positionCount = 3;
        bowString.widthMultiplier = 0.012f;
        bowString.numCapVertices = 1;
        bowString.useWorldSpace = true;
        bowString.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var stringMat = Resources.Load<Material>("FX/BowString");
        bowString.sharedMaterial = stringMat != null ? stringMat : new Material(Shader.Find("Sprites/Default"));
        bowString.startColor = bowString.endColor = new Color(0.93f, 0.9f, 0.82f);

        nocked = Arrow.Model().transform;
        nocked.name = "Nocked Arrow";
        nocked.SetParent(transform, true);
        nocked.gameObject.SetActive(false);
    }

    /// <summary>Called when an arrow leaves the bow: the draw hand follows through.</summary>
    public void Release() => releaseKick = 1f;

    void LateUpdate()
    {
        if (pc == null || bow == null) return;
        bool bowOut = pc.CurrentWeapon == Weapon.Bow;
        bool aiming = bowOut && pc.enabled && (pc.IsAiming || pc.IsDrawing || releaseKick > 0.05f);
        float dt = Time.deltaTime;
        weight = Mathf.MoveTowards(weight, aiming ? 1f : 0f, dt * (aiming ? 7f : 4f));
        float w = weight * weight * (3f - 2f * weight);
        drawSmoothed = Mathf.Lerp(drawSmoothed, pc.IsDrawing ? pc.DrawAmount : 0f, 1f - Mathf.Exp(-18f * dt));
        releaseKick = Mathf.MoveTowards(releaseKick, 0f, dt * 3.5f);

        Vector3 aim = transform.forward;
        if (cam != null && aiming) aim = orbitCamera != null ? orbitCamera.LookDirection : cam.forward;
        else if (cam == null && Camera.main != null) cam = Camera.main.transform;

        Vector3 flatAim = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
        if (flatAim.sqrMagnitude < 0.01f) flatAim = transform.forward;
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(aim.normalized.y, -1f, 1f)) * Mathf.Rad2Deg, -45f, 45f);
        float yaw = Mathf.Atan2(flatAim.x, flatAim.z) * Mathf.Rad2Deg;
        if (!aimReady) { aimYaw = yaw; aimPitch = pitch; aimReady = true; }
        float aimBlend = 1f - Mathf.Exp(-22f * dt);
        aimYaw = Mathf.LerpAngle(aimYaw, yaw, aimBlend);
        aimPitch = Mathf.Clamp(Mathf.Lerp(aimPitch, pitch, aimBlend), -45f, 45f);
        // Smooth angles separately: a spherical interpolation across opposite headings
        // can otherwise take the hands over the pole and exceed the pitch limit.
        aim = Quaternion.Euler(-aimPitch, aimYaw, 0f) * Vector3.forward;
        if (Ready && w > 0.001f) Pose(aim, w);
        else { targetsReady = false; torsoOffset = Quaternion.identity; }
        PlaceBow(bowOut, aim, w);
    }

    void Pose(Vector3 aim, float w)
    {
        Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
        if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, flat).normalized;
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg, -45f, 45f);

        // 1. side-on stance: open the chest away from the bow arm and lean with the shot
        if (spine != null)
        {
            // Let the physics root follow yaw; keep the additive chest twist modest.
            Quaternion targetOffset = Quaternion.AngleAxis(-pitch * 0.35f, right) * Quaternion.AngleAxis(Mathf.Clamp(torsoTurn, -35f, 35f), Vector3.up);
            torsoOffset = Quaternion.Slerp(torsoOffset, targetOffset, 1f - Mathf.Exp(-18f * Time.deltaTime));
            spine.rotation = Quaternion.Slerp(Quaternion.identity, torsoOffset, w) * spine.rotation;
        }

        // 2. bow arm straight down the line of the shot
        float lReach = Vector3.Distance(lUpper.position, lFore.position) + Vector3.Distance(lFore.position, lHand.position);
        Vector3 bowTarget = lUpper.position + aim * lReach * 0.96f;
        Vector3 bowHint = lUpper.position + aim * lReach * 0.5f - Vector3.up * 0.35f - right * 0.15f;
        // Smooth in player space so walking does not drag the hand behind the body.
        float blend = 1f - Mathf.Exp(-24f * Time.deltaTime);
        Vector3 localBow = transform.InverseTransformPoint(bowTarget);
        bowTargetLocal = targetsReady ? Vector3.Lerp(bowTargetLocal, localBow, blend) : localBow;
        SolveTwoBone(lUpper, lFore, lHand, transform.TransformPoint(bowTargetLocal), bowHint, w);

        // 3. draw hand on the string: brace height -> anchor at the cheek -> follow-through
        Vector3 grip = lHand.position;
        Vector3 brace = grip - aim * 0.16f;
        Vector3 anchorBase = head != null ? head.position : neck != null ? neck.position + Vector3.up * 0.12f : grip - aim * fullDrawDistance;
        Vector3 anchor = anchorBase + right * 0.12f - Vector3.up * 0.1f + aim * 0.05f;
        float d = drawSmoothed * drawSmoothed * (3f - 2f * drawSmoothed);
        Vector3 hand = Vector3.Lerp(brace, anchor, d);
        if (releaseKick > 0f) hand += (-aim * 0.14f + right * 0.1f) * releaseKick;
        Vector3 drawHint = rUpper.position - aim * 0.45f + Vector3.up * 0.3f + right * 0.3f;
        Vector3 localHand = transform.InverseTransformPoint(hand);
        drawTargetLocal = targetsReady ? Vector3.Lerp(drawTargetLocal, localHand, blend) : localHand;
        targetsReady = true;
        SolveTwoBone(rUpper, rFore, rHand, transform.TransformPoint(drawTargetLocal), drawHint, w);

        stringPoint = Vector3.Lerp(brace, rHand.position, w);
    }

    // Analytic two-bone IK, blended with the animated pose by weight.
    static void SolveTwoBone(Transform a, Transform b, Transform c, Vector3 target, Vector3 hint, float weight)
    {
        Quaternion a0 = a.rotation, b0 = b.rotation;
        float la = Vector3.Distance(a.position, b.position), lb = Vector3.Distance(b.position, c.position);
        Vector3 toTarget = target - a.position;
        float dist = Mathf.Clamp(toTarget.magnitude, 0.01f, (la + lb) * 0.999f);
        Vector3 dir = toTarget.normalized;
        float cosA = Mathf.Clamp((la * la + dist * dist - lb * lb) / (2f * la * dist), -1f, 1f);
        Vector3 bend = Vector3.ProjectOnPlane(hint - a.position, dir);
        if (bend.sqrMagnitude < 1e-6f) bend = Vector3.ProjectOnPlane(Vector3.down, dir);
        bend.Normalize();
        Vector3 elbow = a.position + (dir * cosA + bend * Mathf.Sqrt(1f - cosA * cosA)) * la;

        a.rotation = Quaternion.FromToRotation(b.position - a.position, elbow - a.position) * a.rotation;
        b.rotation = Quaternion.FromToRotation(c.position - b.position, a.position + dir * dist - b.position) * b.rotation;

        if (weight < 0.999f)
        {
            Quaternion aIK = a.rotation, bIK = b.rotation;
            a.rotation = Quaternion.Slerp(a0, aIK, weight);
            b.rotation = Quaternion.Slerp(b0, bIK, weight);
        }
    }

    void PlaceBow(bool inHand, Vector3 aim, float w)
    {
        Vector3 top, bottom;
        if (inHand && lHand != null)
        {
            // at rest the bow hangs forward and low; aiming raises it onto the line of the shot
            Vector3 rest = Quaternion.AngleAxis(50f, transform.right) * transform.forward;
            Vector3 dir = Vector3.Slerp(rest, aim, w).normalized;
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, dir);
            if (up.sqrMagnitude < 1e-4f) up = transform.up;
            up = Quaternion.AngleAxis(-12f * w, dir) * up.normalized;   // archer's cant
            bow.SetPositionAndRotation(lHand.position + dir * 0.03f, Quaternion.LookRotation(dir, up));
        }
        else
        {
            // slung diagonally across the back
            var gear = pc.Gear;
            Quaternion chest = gear != null && gear.Ready ? gear.ChestRotation : transform.rotation;
            Vector3 back = gear != null && gear.Ready ? gear.BackSurface : transform.position + Vector3.up * 1.25f - transform.forward * 0.12f;
            // Rest the curved grip against the armor; the limb tips arch away.
            bow.SetPositionAndRotation(back + chest * new Vector3(0f, 0.04f, -0.245f),
                chest * Quaternion.Euler(0f, 0f, 32f));
            w = 0f;
        }

        top = bow.TransformPoint(new Vector3(0, BowHalf, NockZ));
        bottom = bow.TransformPoint(new Vector3(0, -BowHalf, NockZ));
        Vector3 mid = (top + bottom) * 0.5f;
        Vector3 pull = w > 0.01f ? Vector3.Lerp(mid, stringPoint, w) : mid;
        bowString.SetPosition(0, top);
        bowString.SetPosition(1, pull);
        bowString.SetPosition(2, bottom);

        bool showArrow = inHand && pc.Arrows > 0 && w > 0.6f && releaseKick < 0.05f;
        if (nocked.gameObject.activeSelf != showArrow) nocked.gameObject.SetActive(showArrow);
        if (showArrow)
        {
            Vector3 shaft = (bow.position - pull).normalized;
            if (shaft.sqrMagnitude < 0.01f) shaft = aim;
            nocked.SetPositionAndRotation(pull, Quaternion.LookRotation(shaft, bow.up));
        }
    }
}
