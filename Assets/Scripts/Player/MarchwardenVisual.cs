using UnityEngine;

[DefaultExecutionOrder(105)]
public class MarchwardenVisual : MonoBehaviour
{
    static MarchwardenAsset loaded;
    public static MarchwardenAsset Asset => loaded != null ? loaded : loaded = Resources.Load<MarchwardenAsset>("Marchwarden/Warrior");
    public SkinnedMeshRenderer Body { get; private set; }
    public Transform Sword { get; private set; }
    public Transform Shield { get; private set; }
    public Vector3 LeftPalmPosition=>Body.bones[System.Array.FindIndex(Body.bones,b=>b.name=="Bip001 L Hand")].TransformPoint(data.leftPalm.GetColumn(3));
    public Vector3 RightPalmPosition=>Body.bones[data.swordBone].TransformPoint(data.rightPalm.GetColumn(3));
    public float SwordGripError=>Vector3.Distance(RightPalmPosition,Sword.TransformPoint(new Vector3(-.352f,0,0)));
    public float ShieldGripError=>Vector3.Distance(LeftPalmPosition,Shield.TransformPoint(data.shieldGripMount.GetColumn(3)));
    public MarchwardenCloth Linen { get; private set; }
    public MarchwardenArmor WaistArmor { get; private set; }
    Material[] palette;
    MarchwardenAsset data;
    AnimationClip menuGuard;
    bool stowed;
    Transform displayQuiver, displayBow, pelvis, spine;
    readonly int[] hemShapes = new int[4];
    readonly float[] hemOffsets = new float[4], hemVelocity = new float[4];
    Vector3 previousPosition, previousVelocity;
    Quaternion previousRotation;
    bool motionReady;
    public float GarmentMotion => Linen!=null?Linen.Motion:Mathf.Max(Mathf.Abs(hemOffsets[0]),Mathf.Abs(hemOffsets[1]));

    public static MarchwardenVisual Ensure(GameObject root)
    {
        if (root.TryGetComponent<MarchwardenVisual>(out var visual)) return visual;
        return Asset != null ? root.AddComponent<MarchwardenVisual>() : null;
    }

    void Awake()
    {
        data = Asset;
        Body = GetComponentInChildren<SkinnedMeshRenderer>();
        if (data == null || Body == null) return;
        MarchwardenStature.Ensure(gameObject,data);
        Body.sharedMesh = data.body;
        var animationBounds=data.body.bounds;
        animationBounds.Expand(.25f);
        Body.localBounds = animationBounds;
        // Four influences retain shoulder/clavicle volume and smooth hip folds,
        // including on graphics presets that limit other meshes to one bone.
        Body.quality = SkinQuality.Bone4;
        string[] shapeNames={"HemForwardL","HemForwardR","HemSideL","HemSideR"};
        for(int i=0;i<hemShapes.Length;i++)hemShapes[i]=data.body.GetBlendShapeIndex(shapeNames[i]);
        palette = new Material[data.materials.Length];
        for (int i = 0; i < palette.Length; i++) palette[i] = new Material(data.materials[i]);
        Body.sharedMaterials = palette;
        Linen=MarchwardenCloth.Prepare(gameObject,Body,data);
        WaistArmor=MarchwardenArmor.Prepare(gameObject,Body,data,Linen);
        Sword = Prop("Marchwarden Sword", data.sword);
        Shield = Prop("Marchwarden Shield", data.shield);
        if (GetComponent<PlayerControlSpeed>() == null)
        {
            menuGuard=Resources.Load<AnimationClip>("Animations/Sword/Guard");
            displayQuiver = Prop("Marchwarden Preview Quiver", data.quiver);
            displayBow = Prop("Marchwarden Preview Bow", data.bow);
            var stringLine = new GameObject("Preview Bowstring").AddComponent<LineRenderer>();
            stringLine.transform.SetParent(displayBow, false);
            stringLine.useWorldSpace = false; stringLine.positionCount = 2;
            stringLine.SetPosition(0, new Vector3(0,-.62f,-.03f)); stringLine.SetPosition(1, new Vector3(0,.62f,-.03f));
            stringLine.widthMultiplier = .006f;
            stringLine.sharedMaterial = Resources.Load<Material>("FX/BowString");
            pelvis = System.Array.Find(Body.bones, b => b.name == "Bip001 Pelvis");
            spine = System.Array.Find(Body.bones, b => b.name == "Bip001 Spine");
            QuiverSuspension.Attach(gameObject,displayQuiver,Body,data);
            for (int i = 0; i < 8; i++)
            {
                var arrow = Prop("Preview Arrow " + i, data.arrow);
                arrow.SetParent(displayQuiver, false);
                float a = i * 2.399963f;
                arrow.localPosition = new Vector3(Mathf.Cos(a)*.038f, data.quiver.bounds.max.y+.235f + i*.003f, Mathf.Sin(a)*.038f);
                arrow.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
                arrow.localScale = Vector3.one*.65f;
            }
        }
        ApplyColors(GameSettings.Tabard, GameSettings.Metal, GameSettings.Leather);
        PoseHeld();
    }

    Transform Prop(string label, Mesh mesh)
    {
        var existing = transform.Find(label);
        var go = existing != null ? existing.gameObject : new GameObject(label);
        go.transform.SetParent(transform, false);
        if (!go.TryGetComponent<MeshFilter>(out var filter)) filter = go.AddComponent<MeshFilter>();
        filter.sharedMesh = mesh;
        if (!go.TryGetComponent<MeshRenderer>(out var renderer)) renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = palette;
        return go.transform;
    }

    public void ApplyColors(int cloth, int metal, int leather)
    {
        if (palette == null) return;
        palette[0].color = new Color(.53f, .57f, .60f) * GameSettings.MetalTints[Mathf.Clamp(metal, 0, GameSettings.MetalTints.Length - 1)].tint;
        palette[2].color = GameSettings.TabardColors[Mathf.Clamp(cloth, 0, GameSettings.TabardColors.Length - 1)].color;
        palette[3].color = new Color(.26f, .16f, .10f) * GameSettings.LeatherTints[Mathf.Clamp(leather, 0, GameSettings.LeatherTints.Length - 1)].tint;
        if(Linen!=null)Linen.SetColor(palette[2].color);
    }

    public void SetStowed(bool value)
    {
        stowed = value;
        Sword.gameObject.SetActive(!value);
        Shield.gameObject.SetActive(!value);
        if (!value) PoseHeld();
    }

    void LateUpdate()
    {
        // Menu previews have no player graph. Apply the same sword/shield guard
        // clip after their idle animation so the fitted straps sit on the arm.
        if(menuGuard!=null)menuGuard.SampleAnimation(gameObject,0);
        if (!stowed) PoseHeld();
        UpdateGarments();
        if (displayQuiver != null && pelvis != null)
        {
            displayBow.SetPositionAndRotation(spine.position + transform.up*.1f - transform.forward*.22f,
                transform.rotation * Quaternion.AngleAxis(32,Vector3.forward) * Quaternion.AngleAxis(90,Vector3.up));
        }
    }

    void UpdateGarments()
    {
        if(Linen!=null)return;
        if(Body==null)return;
        float dt=Time.deltaTime;
        if(dt<=0)return;
        if(!motionReady)
        {
            previousPosition=transform.position;previousRotation=transform.rotation;
            motionReady=true;return;
        }
        Vector3 velocity=(transform.position-previousPosition)/dt;
        previousPosition=transform.position;
        // Respawns/scene placement must never kick the garment springs.
        bool teleported=velocity.sqrMagnitude>400;
        if(teleported){velocity=Vector3.zero;previousVelocity=Vector3.zero;}
        Vector3 localVelocity=transform.InverseTransformDirection(velocity);
        Vector3 acceleration=transform.InverseTransformDirection((velocity-previousVelocity)/Mathf.Max(dt,.008f));
        float turn=Mathf.DeltaAngle(previousRotation.eulerAngles.y,transform.eulerAngles.y)/dt;
        previousVelocity=velocity;previousRotation=transform.rotation;
        float flutter=Mathf.Sin(Time.time*7.5f)*Mathf.Clamp01(localVelocity.magnitude/4)*.11f;
        float fore=Mathf.Clamp(-localVelocity.z*.065f-acceleration.z*.008f,-.75f,.75f);
        float side=Mathf.Clamp(-localVelocity.x*.07f-acceleration.x*.006f-turn*.0009f,-.65f,.65f);
        int steps=Mathf.Max(1,Mathf.CeilToInt(Mathf.Min(dt,.1f)/.008f));
        float h=Mathf.Min(dt,.1f)/steps;
        for(int i=0;i<hemShapes.Length;i++)
        {
            float target=i<2?fore+flutter*(i==0?1:-1):side;
            if(teleported){hemOffsets[i]=0;hemVelocity[i]=0;}
            // Substeps keep the damped spring stable at low and variable FPS.
            for(int step=0;step<steps;step++)
            {
                hemVelocity[i]+=(125*(target-hemOffsets[i])-21*hemVelocity[i])*h;
                hemOffsets[i]=Mathf.Clamp(hemOffsets[i]+hemVelocity[i]*h,-1,1);
            }
            if(hemShapes[i]>=0)Body.SetBlendShapeWeight(hemShapes[i],hemOffsets[i]*100);
        }
    }

    void PoseHeld()
    {
        if (data == null || Body == null || Sword == null) return;
        SetMount(Sword, data.swordBone, data.swordMount);
        SetMount(Shield, data.shieldBone, data.shieldMount);
    }

    void SetMount(Transform prop, int bone, Matrix4x4 mount)
    {
        var m = Body.bones[bone].localToWorldMatrix * mount;
        prop.SetPositionAndRotation(m.GetColumn(3), Quaternion.LookRotation(m.GetColumn(2), m.GetColumn(1)));
    }

    void OnDestroy()
    {
        if (palette != null) foreach (var mat in palette) if (mat != null) Destroy(mat);
    }
}
