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
    public float torsoTurn = 65f;          // chest opens side-on; the head keeps looking downrange
    public float fullDrawDistance = 0.62f; // string travel from brace to anchor

    PlayerControlSpeed pc;
    Transform cam;
    CameraFollow orbitCamera;
    Transform spine, neck, head, lUpper, lFore, lHand, rUpper, rFore, rHand;
    Transform lClavicle, rClavicle;
    Transform bow, nocked;
    LineRenderer bowString;
    float weight, releaseKick, drawSmoothed;
    Vector3 stringPoint;
    Quaternion torsoOffset = Quaternion.identity;
    Vector3 bowTargetLocal, drawTargetLocal;
    float aimYaw, aimPitch;
    bool targetsReady, aimReady;
    GameObject nockedFlame;
    Quaternion leftGrip, rightGrip;
    Vector3 leftPalm, rightPalm;
    Vector3 stringHook;
    SkinnedMeshRenderer bodySkin;
    int drawShape=-1,releaseShape=-1;
    Quaternion leftClavicleRest, rightClavicleRest;
    Vector3 headForward, anchorPoint;
    Mesh flexibleBow;
    Vector3[] restVertices, bentVertices;
    float bend;
    float stowedBowDepth=.055f;
    float Stature => Mathf.Abs(transform.lossyScale.x);

    const float BowHalf = 0.62f, NockZ = -0.03f, ArrowRestY=.067f;

    public Vector3 ArrowOrigin => bow != null ? bow.TransformPoint(new Vector3(0,ArrowRestY,.2f)) : transform.position + Vector3.up * 1.4f;
    public bool Ready => lUpper != null && rUpper != null;
    public Vector3 BowPalmPosition=>lHand!=null?lHand.TransformPoint(leftPalm):transform.position;
    public Vector3 DrawPalmPosition=>rHand!=null?rHand.TransformPoint(rightPalm):transform.position;
    public Vector3 StringContactPosition=>rHand!=null?rHand.TransformPoint(stringHook):transform.position;
    public float GripError => bow == null || lHand == null ? 0 : Vector3.Distance(bow.TransformPoint(new Vector3(0,0,.2f)),BowPalmPosition);
    public float AnchorError => rHand == null ? 0 : Vector3.Distance(StringContactPosition,anchorPoint);
    public float DrawElbowAlignment => rFore == null || rHand == null || bow == null ? 0 : Vector3.Angle(rFore.position-rHand.position,-bow.forward);
    public float ShaftAlignment => nocked == null || bow == null ? 0 : Vector3.Angle(nocked.forward,bow.forward);

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
        lClavicle=Find("Bip001 L Clavicle"); rClavicle=Find("Bip001 R Clavicle");
        if(lClavicle!=null) leftClavicleRest=lClavicle.localRotation;
        if(rClavicle!=null) rightClavicleRest=rClavicle.localRotation;
        if (Ready)
        {
            var asset=MarchwardenVisual.Asset;
            leftGrip=asset!=null?asset.leftPalm.rotation:GripReference(lHand);
            rightGrip=asset!=null?asset.rightPalm.rotation:GripReference(rHand);
            if(asset!=null){leftPalm=asset.leftPalm.GetColumn(3);rightPalm=asset.rightPalm.GetColumn(3);}
            // Nock between the index and middle finger, against the inner hook.
            stringHook=asset!=null?asset.drawHook:rightPalm+rightGrip*new Vector3(.019f,.011f,0);
            bodySkin=GetComponentInChildren<SkinnedMeshRenderer>();
            drawShape=bodySkin.sharedMesh.GetBlendShapeIndex("BowDrawHand");
            releaseShape=bodySkin.sharedMesh.GetBlendShapeIndex("BowReleaseHand");
        }
        if (head != null) headForward = head.InverseTransformDirection(transform.forward);
        BuildBow();
    }

    Transform Find(string name) => FindBone(transform, name);

    Quaternion GripReference(Transform hand)
    {
        var skin=GetComponentInChildren<SkinnedMeshRenderer>();
        int index=System.Array.IndexOf(skin.bones,hand);
        Matrix4x4 toBone=skin.sharedMesh.bindposes[index]*skin.transform.worldToLocalMatrix;
        return PoseMath.AlongBlade(toBone.MultiplyVector(transform.right),toBone.MultiplyVector(transform.forward));
    }

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
        if (MarchwardenVisual.Asset != null && go.TryGetComponent<MeshFilter>(out var bowFilter))
            bowFilter.sharedMesh = MarchwardenVisual.Asset.bow;
        if (go.TryGetComponent(out MeshRenderer mr)) mr.sharedMaterials = ProceduralModels.BowMaterials(GameSettings.BowFinish);
        bow = go.transform;
        bow.SetParent(transform, true);
        bow.localScale=Vector3.one;
        if (go.TryGetComponent<MeshFilter>(out var filter))
        {
            flexibleBow = Instantiate(filter.sharedMesh); flexibleBow.name = "Bow bending with draw";
            flexibleBow.MarkDynamic(); filter.sharedMesh = flexibleBow;
            restVertices = flexibleBow.vertices; bentVertices = new Vector3[restVertices.Length];
            stowedBowDepth=.012f-flexibleBow.bounds.min.x;
        }

        bowString = new GameObject("Bowstring").AddComponent<LineRenderer>();
        bowString.transform.SetParent(bow, false);
        bowString.positionCount = 3;
        bowString.widthMultiplier = 0.004f;
        bowString.numCapVertices = 1;
        bowString.useWorldSpace = true;
        bowString.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        var stringMat = Resources.Load<Material>("FX/BowString");
        bowString.sharedMaterial = stringMat != null ? stringMat : new Material(Shader.Find("Sprites/Default"));
        bowString.startColor = bowString.endColor = new Color(0.93f, 0.9f, 0.82f);

        nocked = Arrow.Model().transform;
        nocked.name = "Nocked Arrow";
        nocked.SetParent(transform, true);
        nocked.localScale=Vector3.one;
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
        if(bodySkin!=null)
        {
            if(drawShape>=0)bodySkin.SetBlendShapeWeight(drawShape,bowOut?w*(1-releaseKick)*100:0);
            if(releaseShape>=0)bodySkin.SetBlendShapeWeight(releaseShape,bowOut?w*releaseKick*100:0);
        }

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
        float stature=Stature;
        // These source clips do not consistently key the clavicles. Start from
        // their reference rotations so the additive shoulder lift cannot build
        // up or retain a previous aim direction after a large camera turn.
        if(lClavicle!=null) lClavicle.localRotation=leftClavicleRest;
        if(rClavicle!=null) rClavicle.localRotation=rightClavicleRest;
        Vector3 flat = Vector3.ProjectOnPlane(aim, Vector3.up).normalized;
        if (flat.sqrMagnitude < 0.01f) flat = transform.forward;
        Vector3 right = Vector3.Cross(Vector3.up, flat).normalized;
        float pitch = Mathf.Clamp(Mathf.Asin(Mathf.Clamp(aim.y, -1f, 1f)) * Mathf.Rad2Deg, -45f, 45f);

        Vector3 up = Vector3.ProjectOnPlane(Vector3.up,aim).normalized;
        // 1. Open the shoulders while keeping the head aimed at the target.
        if (spine != null)
        {
            // Let the physics root follow yaw; keep the additive chest twist modest.
            Quaternion targetOffset = Quaternion.AngleAxis(-pitch * 0.55f, right) * Quaternion.AngleAxis(Mathf.Clamp(torsoTurn, 0f, 65f), Vector3.up);
            torsoOffset = Quaternion.Slerp(torsoOffset, targetOffset, 1f - Mathf.Exp(-18f * Time.deltaTime));
            spine.rotation = Quaternion.Slerp(Quaternion.identity, torsoOffset, w) * spine.rotation;
        }
        if (head != null)
            head.rotation = Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(head.TransformDirection(headForward),aim),w)*head.rotation;
        // Set the shoulder girdle before solving the arms: a raised bow shoulder
        // and retracted draw shoulder avoid a folded bow elbow at full draw.
        if(lClavicle!=null)
        {
            Vector3 delta=lUpper.position-lClavicle.position;
            // Protract the bow shoulder before solving the arm. The wider
            // cheek-guard anchor must not leave the bow arm across the shot.
            Vector3 shoulderTarget=delta+(up*.035f+right*.045f)*stature;
            lClavicle.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(delta,shoulderTarget),w)*lClavicle.rotation;
        }
        if(rClavicle!=null)
        {
            Vector3 delta=rUpper.position-rClavicle.position;
            rClavicle.rotation=Quaternion.Slerp(Quaternion.identity,Quaternion.FromToRotation(delta,delta-aim*(.015f*stature)),w)*rClavicle.rotation;
        }

        // 2. Put the arrow rest on the same line as the facial anchor. The arm
        // lengths constrain the grip distance; neither elbow is forced past reach.
        float lReach = Vector3.Distance(lUpper.position, lFore.position) + Vector3.Distance(lFore.position, lHand.position);
        float rReach = Vector3.Distance(rUpper.position,rFore.position)+Vector3.Distance(rFore.position,rHand.position);
        var appearance=MarchwardenVisual.Asset;
        Vector3 face=head!=null&&appearance!=null?head.TransformPoint(appearance.headEyeLocal):
            head!=null?head.position+up*(.08f*stature):neck.position+up*(.14f*stature);
        // Anchor against the outside of the cheek guard, below the eye slit.
        anchorPoint=face+right*(.114f*stature)-up*(.040f*stature)+aim*(.025f*stature);
        Vector3 bowUp=Quaternion.AngleAxis(-12f*w,aim)*up;
        Vector3 gripLine=anchorPoint-bowUp*(ArrowRestY*stature);
        float leftLateral = Vector3.ProjectOnPlane(gripLine-lUpper.position,aim).sqrMagnitude;
        float leftForward = Vector3.Dot(lUpper.position-gripLine,aim)+Mathf.Sqrt(Mathf.Max(.01f,lReach*lReach*.9801f-leftLateral));
        float rightLateral = Vector3.ProjectOnPlane(anchorPoint-rUpper.position,aim).sqrMagnitude;
        float rightForward = Vector3.Dot(rUpper.position-gripLine,aim)+Mathf.Sqrt(Mathf.Max(.01f,rReach*rReach*.94f-rightLateral))+.23f*stature;
        float extend=Mathf.SmoothStep(0,1,drawSmoothed);
        Vector3 bowTarget = gripLine+aim*Mathf.Lerp(Mathf.Min(leftForward,rightForward),leftForward,extend);
        Vector3 bowHint = lUpper.position+(aim*.24f-up*.18f-right*.14f)*stature;
        // Smooth in player space so walking does not drag the hand behind the body.
        float blend = 1f - Mathf.Exp(-24f * Time.deltaTime);
        Vector3 localBow = transform.InverseTransformPoint(bowTarget);
        bowTargetLocal = targetsReady ? Vector3.Lerp(bowTargetLocal, localBow, blend) : localBow;
        Quaternion leftRotation=PoseMath.AlongBlade(bowUp,right)*Quaternion.Inverse(leftGrip);
        Vector3 leftOffset=leftRotation*Vector3.Scale(leftPalm,lHand.lossyScale);
        PoseMath.Arm(lUpper, lFore, lHand, transform.TransformPoint(bowTargetLocal)-leftOffset, bowHint, w);
        PoseMath.SetWrist(lFore,lHand,Quaternion.Slerp(lHand.rotation,leftRotation,w),leftGrip*Vector3.up,appearance!=null?appearance.leftWristRest:Quaternion.identity);

        // 3. draw hand on the string: brace height -> anchor at the cheek -> follow-through
        Vector3 grip = BowPalmPosition;
        Vector3 brace = grip+(-aim*.23f+bowUp*ArrowRestY)*stature;
        float d = drawSmoothed * drawSmoothed * (3f - 2f * drawSmoothed);
        d = Mathf.Max(d,releaseKick*.98f);
        Vector3 hand = Vector3.Lerp(brace,anchorPoint,d)+up*(.018f*stature*Mathf.Sin(d*Mathf.PI));
        if (releaseKick > 0f) hand += (-aim*.09f+right*.015f)*(releaseKick*stature);
        Vector3 drawHint = Vector3.Lerp(rUpper.position+(right*.22f+aim*.05f)*stature,anchorPoint+(-aim*.32f+right*.10f+up*.025f)*stature,d);
        Vector3 localHand = transform.InverseTransformPoint(hand);
        drawTargetLocal = targetsReady ? Vector3.Lerp(drawTargetLocal, localHand, blend) : localHand;
        targetsReady = true;
        Quaternion rightRotation=PoseMath.AlongBlade(up,right)*Quaternion.Inverse(rightGrip);
        Vector3 rightOffset=rightRotation*Vector3.Scale(stringHook,rHand.lossyScale);
        PoseMath.Arm(rUpper, rFore, rHand, transform.TransformPoint(drawTargetLocal)-rightOffset, drawHint, w);
        PoseMath.SetWrist(rFore,rHand,Quaternion.Slerp(rHand.rotation,rightRotation,w),rightGrip*Vector3.up,appearance!=null?appearance.rightWristRest:Quaternion.identity);

        stringPoint = Vector3.Lerp(brace, StringContactPosition, w);
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
            bow.SetPositionAndRotation(BowPalmPosition-dir*(.2f*Stature), Quaternion.LookRotation(dir, up));
        }
        else
        {
            // slung diagonally across the back
            var gear = pc.Gear;
            Quaternion chest = gear != null && gear.Ready ? gear.ChestRotation : transform.rotation;
            Vector3 back = gear != null && gear.Ready ? gear.BackSurface : transform.position + Vector3.up * 1.25f - transform.forward * 0.12f;
            // The bow and string occupy local YZ. Turn that entire plane onto
            // the back, then roll it diagonally within the chest plane.
            bow.SetPositionAndRotation(back + chest * (new Vector3(0f, 0.04f, -stowedBowDepth)*Stature),
                chest * Quaternion.AngleAxis(32f, Vector3.forward) * Quaternion.AngleAxis(90f, Vector3.up));
            w = 0f;
        }

        FlexBow(inHand ? Mathf.Max(drawSmoothed,releaseKick*.9f) : 0f);
        top = bow.TransformPoint(BendPoint(new Vector3(0, BowHalf, NockZ),bend));
        bottom = bow.TransformPoint(BendPoint(new Vector3(0, -BowHalf, NockZ),bend));
        Vector3 mid = (top + bottom) * 0.5f;
        Vector3 pull = w > 0.01f ? Vector3.Lerp(mid, stringPoint, w) : mid;
        bowString.SetPosition(0, top);
        bowString.SetPosition(1, pull);
        bowString.SetPosition(2, bottom);

        bool showArrow = inHand && pc.enabled && pc.Arrows > 0 && w > 0.6f && releaseKick < 0.05f;
        if (nocked.gameObject.activeSelf != showArrow) nocked.gameObject.SetActive(showArrow);
        if (showArrow)
        {
            Vector3 shaft = (ArrowOrigin - pull).normalized;
            if (shaft.sqrMagnitude < 0.01f) shaft = aim;
            nocked.SetPositionAndRotation(pull, Quaternion.LookRotation(shaft, bow.up));
        }
        bool burningNock = showArrow && pc.IsDrawing && pc.DrawAmount > 0.95f && Boons.FireArrows;
        if (burningNock && nockedFlame == null) nockedFlame = FX.AttachFlame(nocked, Vector3.forward * 0.9f, 0.5f);
        else if (!burningNock && nockedFlame != null) { FX.Extinguish(nockedFlame); nockedFlame = null; }
    }

    static Vector3 BendPoint(Vector3 vertex,float amount)
    {
        float influence=Mathf.Pow(Mathf.Clamp01(Mathf.Abs(vertex.y)/BowHalf),2);
        vertex.z-=.075f*amount*influence;
        vertex.y-=Mathf.Sign(vertex.y)*.018f*amount*influence;
        return vertex;
    }

    void FlexBow(float amount)
    {
        if(flexibleBow==null || Mathf.Abs(amount-bend)<.002f) return;
        bend=amount;
        for(int i=0;i<restVertices.Length;i++) bentVertices[i]=BendPoint(restVertices[i],bend);
        flexibleBow.vertices=bentVertices; flexibleBow.RecalculateBounds();
    }

    void OnDestroy() { if (flexibleBow != null) Destroy(flexibleBow); }
}
