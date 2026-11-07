using System;
using System.Collections.Generic;
using UnityEngine;

// A small dedicated PhysX cloth, independent of the armored body mesh. All
// collisions are explicitly assigned; the proxy layer never affects gameplay.
[DefaultExecutionOrder(130)]
public sealed class MarchwardenCloth : MonoBehaviour
{
    public const int CollisionLayer=31;
    [SerializeField] MarchwardenAsset data;
    [SerializeField] SkinnedMeshRenderer fabric,rig;
    [SerializeField] CapsuleCollider[] capsules;
    public Cloth Solver { get; private set; }
    public SkinnedMeshRenderer Fabric => fabric;
    public CapsuleCollider[] Capsules => capsules;
    public int PinnedParticles { get; private set; }
    public int ParticleCount => rest!=null?rest.Length:0;
    public int ResetCount { get; private set; }
    public float Motion { get; private set; }
    public MeshRenderer Surface { get; private set; }
    public CapsuleShape[] CollisionShapes=>shapes;
    public Vector3 ParticlePosition(int index)=>surfacePoints[index];
    public Vector3 SurfaceVertexPosition(int index)=>surfacePoints[surfaceParticles[index]];
    public int NearestRestParticle(Vector3 characterPoint)
    {
        int nearest=0;float minimum=float.MaxValue;
        for(int i=0;i<rest.Length;i++)
        {
            float distance=(data.meshToCharacter.MultiplyPoint3x4(rest[i])-characterPoint).sqrMagnitude;
            if(distance<minimum){minimum=distance;nearest=i;}
        }
        return nearest;
    }
    Material instanceMaterial;
    Vector3[] rest;
    Vector3[] surfacePoints,surfaceVertices;
    Vector3[] particleNormals,surfaceNormals;
    Vector3[] restOutward,preferredOutward;
    int[] surfaceParticles;
    int[] surfaceTriangles;
    Mesh surfaceMesh;
    CapsuleShape[] shapes;
    bool[] pinned;
    Transform owner,pelvis;
    int pelvisIndex;
    Matrix4x4 pelvisBind;
    Vector3 lastRoot;
    bool ready;

    public static MarchwardenCloth Prepare(GameObject root,SkinnedMeshRenderer body,MarchwardenAsset asset)
    {
        if(asset.tunic==null)return null;
        var item=root.GetComponentInChildren<MarchwardenCloth>(true);
        if(item==null)
        {
            var child=new GameObject("Marchwarden simulated linen");child.transform.SetParent(body.transform,false);
            var skin=child.AddComponent<SkinnedMeshRenderer>();
            item=child.AddComponent<MarchwardenCloth>();item.fabric=skin;
        }
        item.data=asset;item.rig=body;item.owner=root.transform;
        item.fabric.sharedMesh=asset.tunic;item.fabric.bones=body.bones;item.fabric.rootBone=body.rootBone;
        item.fabric.sharedMaterial=asset.linenMaterial;
        item.fabric.quality=SkinQuality.Bone4;item.fabric.updateWhenOffscreen=true;
        var bounds=asset.tunic.bounds;bounds.Expand(.6f);item.fabric.localBounds=bounds;
        if(item.capsules==null||item.capsules.Length!=asset.clothCapsules.Length)item.CreateCapsules();
        if(Application.isPlaying)item.Initialise();
        return item;
    }

    void Awake(){if(data!=null&&rig!=null){owner=rig.transform.root;Initialise();}}

    void CreateCapsules()
    {
        if(capsules!=null)foreach(var old in capsules)if(old!=null)
        {if(Application.isPlaying)Destroy(old.gameObject);else DestroyImmediate(old.gameObject);}
        capsules=new CapsuleCollider[data.clothCapsules.Length];
        for(int i=0;i<capsules.Length;i++)
        {
            var spec=data.clothCapsules[i];
            var go=new GameObject("Cloth collision "+spec.name);go.layer=CollisionLayer;
            go.transform.SetParent(rig.bones[spec.bone],false);
            go.transform.localPosition=(spec.start+spec.end)*.5f;
            go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,(spec.end-spec.start).normalized);
            var collider=go.AddComponent<CapsuleCollider>();collider.direction=1;
            collider.radius=spec.radius;collider.height=Vector3.Distance(spec.start,spec.end)+2*spec.radius;
            collider.isTrigger=true;capsules[i]=collider;
        }
    }

    void Initialise()
    {
        if(ready)return;
        owner=rig.GetComponentInParent<MarchwardenStature>().transform;
        // Native cloth reads these shapes directly, independently of PhysX's
        // gameplay contact matrix. No new trigger/hitbox contacts are introduced.
        for(int layer=0;layer<32;layer++)Physics.IgnoreLayerCollision(CollisionLayer,layer,true);
        instanceMaterial=new Material(data.linenMaterial);fabric.sharedMaterial=instanceMaterial;
        instanceMaterial.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");instanceMaterial.EnableKeyword("_GLOSSYREFLECTIONS_OFF");
        pelvisIndex=Array.FindIndex(rig.bones,b=>b.name=="Bip001 Pelvis");pelvis=rig.bones[pelvisIndex];
        pelvisBind=data.tunic.bindposes[pelvisIndex];
        Solver=GetComponent<Cloth>();if(Solver==null)Solver=gameObject.AddComponent<Cloth>();
        Solver.useGravity=true;Solver.useTethers=true;
        Solver.stretchingStiffness=.98f;Solver.bendingStiffness=.60f;Solver.damping=.45f;
        Solver.friction=.35f;Solver.collisionMassScale=.3f;
        Solver.clothSolverFrequency=180;Solver.enableContinuousCollision=true;Solver.useVirtualParticles=1;
        Solver.worldVelocityScale=.25f;Solver.worldAccelerationScale=.10f;
        Solver.randomAcceleration=Vector3.zero;Solver.sleepThreshold=.005f;
        Solver.capsuleColliders=capsules;
        MapParticles();
        CreateSurface();
        lastRoot=owner.position;Solver.ClearTransformMotion();ready=true;
    }

    void MapParticles()
    {
        var particles=Solver.vertices;var vertices=data.tunic.vertices;
        var coefficients=Solver.coefficients;
        if(particles.Length==0||coefficients.Length!=particles.Length)
            throw new InvalidOperationException("Cloth particles were not initialized");
        rest=new Vector3[particles.Length];pinned=new bool[particles.Length];
        Matrix4x4 pose=fabric.transform.worldToLocalMatrix*pelvis.localToWorldMatrix*pelvisBind;
        float Nearest(Vector3 point,out int index)
        {
            index=0;float distance=float.MaxValue;
            for(int k=0;k<vertices.Length;k++){float d=(point-vertices[k]).sqrMagnitude;if(d<distance){distance=d;index=k;}}
            return distance;
        }
        float rawError=0,posedError=0;
        for(int i=0;i<particles.Length;i+=Mathf.Max(1,particles.Length/32))
        {rawError+=Nearest(particles[i],out _);posedError+=Nearest(pose.inverse.MultiplyPoint3x4(particles[i]),out _);}
        Matrix4x4 toRest=posedError<rawError?pose.inverse:Matrix4x4.identity;
        var free=new List<uint>();PinnedParticles=0;
        for(int i=0;i<particles.Length;i++)
        {
            Nearest(toRest.MultiplyPoint3x4(particles[i]),out int at);rest[i]=vertices[at];
            float y=data.meshToCharacter.MultiplyPoint3x4(rest[i]).y;
            float depth=Mathf.Clamp01((.801f-y)/.300f);
            pinned[i]=depth<.025f;
            if(pinned[i])PinnedParticles++;else free.Add((uint)i);
            coefficients[i].maxDistance=pinned[i]?0:Mathf.SmoothStep(.008f,.17f,Mathf.InverseLerp(.025f,1,depth));
            coefficients[i].collisionSphereDistance=0;
        }
        if(PinnedParticles<8||PinnedParticles>=particles.Length)throw new InvalidOperationException("Invalid cloth waist pinning");
        Solver.coefficients=coefficients;
        Solver.selfCollisionDistance=.005f;Solver.selfCollisionStiffness=.65f;
        Solver.SetSelfAndInterCollisionIndices(free);
        Debug.Log($"[Linen] Native cloth: {ParticleCount} particles, {PinnedParticles} waist pins, {capsules.Length} fitted capsules, 180 Hz CCD");
    }

    void CreateSurface()
    {
        // The imported skeleton has a rotated renderer and a differently
        // oriented root bone. Render the native particle output explicitly in
        // this frame instead of relying on Cloth's generated GPU skin buffer.
        var go=new GameObject("Simulated linen surface");go.transform.SetParent(transform,false);
        surfaceMesh=Instantiate(data.tunic);surfaceMesh.name="Marchwarden live linen";surfaceMesh.MarkDynamic();
        go.AddComponent<MeshFilter>().sharedMesh=surfaceMesh;
        Surface=go.AddComponent<MeshRenderer>();Surface.sharedMaterial=instanceMaterial;
        fabric.forceRenderingOff=true;
        var vertices=data.tunic.vertices;
        surfaceParticles=new int[vertices.Length];surfaceVertices=new Vector3[vertices.Length];surfaceTriangles=data.tunic.triangles;
        surfacePoints=new Vector3[rest.Length];particleNormals=new Vector3[rest.Length];surfaceNormals=new Vector3[vertices.Length];
        restOutward=new Vector3[rest.Length];preferredOutward=new Vector3[rest.Length];
        for(int particle=0;particle<rest.Length;particle++)
        {
            Vector3 character=data.meshToCharacter.MultiplyPoint3x4(rest[particle]);
            restOutward[particle]=data.meshToCharacter.inverse.MultiplyVector(new Vector3(character.x,0,character.z+.04f).normalized);
        }
        shapes=new CapsuleShape[capsules.Length];
        for(int vertex=0;vertex<vertices.Length;vertex++)
        {
            float nearest=float.MaxValue;
            for(int particle=0;particle<rest.Length;particle++)
            {
                float distance=(vertices[vertex]-rest[particle]).sqrMagnitude;
                if(distance<nearest){nearest=distance;surfaceParticles[vertex]=particle;}
            }
            if(nearest>.000001f)throw new InvalidOperationException("Unmapped cloth surface vertex");
        }
        UpdateSurface();
    }

    public struct CapsuleShape
    {
        public Vector3 start,end;
        public float radius;
        Vector3 delta;
        float inverseLengthSquared;
        public CapsuleShape(Vector3 a,Vector3 b,float r){start=a;end=b;radius=r;delta=b-a;inverseLengthSquared=1/Mathf.Max(.000001f,delta.sqrMagnitude);}
        public CapsuleShape(CapsuleCollider capsule)
        {
            Vector3 scale=capsule.transform.lossyScale;
            radius=capsule.radius*Mathf.Max(Mathf.Abs(scale.x),Mathf.Abs(scale.z));
            float half=Mathf.Max(0,capsule.height*.5f-capsule.radius)*Mathf.Abs(scale.y);
            Vector3 center=capsule.transform.TransformPoint(capsule.center),axis=capsule.transform.up;
            start=center-axis*half;end=center+axis*half;
            delta=end-start;inverseLengthSquared=1/Mathf.Max(.000001f,delta.sqrMagnitude);
        }
        public Vector3 Closest(Vector3 point)
        {
            return start+delta*Mathf.Clamp01(Vector3.Dot(point-start,delta)*inverseLengthSquared);
        }
        public float Gap(Vector3 point)=>Vector3.Distance(point,Closest(point))-radius;
        public Vector3 Project(Vector3 point,float margin,Vector3 preferred)
        {
            Vector3 closest=Closest(point),delta=point-closest;float square=delta.sqrMagnitude;
            if(square>=(radius+margin)*(radius+margin))return point;
            float distance=Mathf.Sqrt(square);
            Vector3 normal=distance>.000001f?delta/distance:preferred;
            Vector3 outward=Vector3.ProjectOnPlane(preferred,end-start).normalized;
            // A limb can cross a particle between simulation updates. Keep
            // the garment on its original side rather than pushing it through
            // the opposite face of the limb and folding adjacent triangles.
            if(outward.sqrMagnitude>.5f&&Vector3.Dot(normal,outward)<0)normal=outward;
            return closest+normal*(radius+margin);
        }
    }

    Matrix4x4 ParticleFrame
    {
        get
        {
            Transform reference=fabric.rootBone!=null?fabric.rootBone:fabric.transform;
            return Matrix4x4.TRS(reference.position,reference.rotation,Vector3.one);
        }
    }

    public double LastStepMilliseconds { get; private set; }
    void LateUpdate(){long start=System.Diagnostics.Stopwatch.GetTimestamp();if(ready)UpdateSurface();LastStepMilliseconds=(System.Diagnostics.Stopwatch.GetTimestamp()-start)*1000.0/System.Diagnostics.Stopwatch.Frequency;}

    void UpdateSurface()
    {
        var particles=Solver.vertices;Matrix4x4 frame=ParticleFrame;
        Matrix4x4 skin=pelvis.localToWorldMatrix*pelvisBind;
        for(int capsule=0;capsule<capsules.Length;capsule++)shapes[capsule]=new CapsuleShape(capsules[capsule]);
        for(int particle=0;particle<particles.Length;particle++)
        {
            Vector3 point=pinned[particle]?skin.MultiplyPoint3x4(rest[particle]):frame.MultiplyPoint3x4(particles[particle]);
            surfacePoints[particle]=point;preferredOutward[particle]=skin.MultiplyVector(restOutward[particle]).normalized;
        }
        // PhysX supplies gravity, inertia, bending, stretch and self collision.
        // Resolve contacts after bone poses, including virtual triangle points.
        for(int iteration=0;iteration<8;iteration++)
        {
            bool touched=false;
            for(int particle=0;particle<particles.Length;particle++)if(!pinned[particle])
            {
                Vector3 point=surfacePoints[particle];
                for(int capsule=0;capsule<shapes.Length;capsule++)
                {
                    Vector3 projected=shapes[capsule].Project(point,.008f,preferredOutward[particle]);
                    touched|=(projected-point).sqrMagnitude>.0000000001f;point=projected;
                }
                surfacePoints[particle]=point;
            }
            for(int triangle=0;triangle<surfaceTriangles.Length;triangle+=3)
            {
                int a=surfaceParticles[surfaceTriangles[triangle]],b=surfaceParticles[surfaceTriangles[triangle+1]],c=surfaceParticles[surfaceTriangles[triangle+2]];
                int free=(pinned[a]?0:1)+(pinned[b]?0:1)+(pinned[c]?0:1);if(free==0)continue;
                Vector3 preferred=(preferredOutward[a]+preferredOutward[b]+preferredOutward[c]).normalized;
                Vector3 center=(surfacePoints[a]+surfacePoints[b]+surfacePoints[c])/3;
                for(int capsule=0;capsule<shapes.Length;capsule++)
                {
                    Vector3 correction=shapes[capsule].Project(center,.008f,preferred)-center;
                    if(correction.sqrMagnitude<.0000000001f)continue;
                    center+=correction;
                    correction*=3f/free;touched=true;
                    if(!pinned[a])surfacePoints[a]+=correction;
                    if(!pinned[b])surfacePoints[b]+=correction;
                    if(!pinned[c])surfacePoints[c]+=correction;
                }
            }
            if(!touched)break;
        }
        Matrix4x4 toLocal=Surface.transform.worldToLocalMatrix;
        for(int vertex=0;vertex<surfaceVertices.Length;vertex++)surfaceVertices[vertex]=toLocal.MultiplyPoint3x4(surfacePoints[surfaceParticles[vertex]]);
        Array.Clear(particleNormals,0,particleNormals.Length);
        for(int triangle=0;triangle<surfaceTriangles.Length;triangle+=3)
        {
            int a=surfaceTriangles[triangle],b=surfaceTriangles[triangle+1],c=surfaceTriangles[triangle+2];
            Vector3 normal=Vector3.Cross(surfaceVertices[b]-surfaceVertices[a],surfaceVertices[c]-surfaceVertices[a]);
            particleNormals[surfaceParticles[a]]+=normal;particleNormals[surfaceParticles[b]]+=normal;particleNormals[surfaceParticles[c]]+=normal;
        }
        for(int particle=0;particle<particleNormals.Length;particle++)particleNormals[particle].Normalize();
        for(int vertex=0;vertex<surfaceNormals.Length;vertex++)surfaceNormals[vertex]=particleNormals[surfaceParticles[vertex]];
        surfaceMesh.vertices=surfaceVertices;surfaceMesh.normals=surfaceNormals;
        surfaceMesh.RecalculateTangents();surfaceMesh.RecalculateBounds();
    }

    public void SetColor(Color color){if(instanceMaterial!=null)instanceMaterial.color=color;}

    void Update()
    {
        if(!ready||Time.deltaTime<=0)return;
        if((owner.position-lastRoot).sqrMagnitude>.8f*.8f)ResetSimulation();
        lastRoot=owner.position;
        Solver.externalAcceleration=new Vector3(.45f*Mathf.Sin(Time.time*1.7f),0,.20f*Mathf.Sin(Time.time*1.1f));
    }

    public void ResetSimulation()
    {
        if(Solver==null)return;
        Solver.enabled=false;Solver.enabled=true;Solver.ClearTransformMotion();ResetCount++;
    }

    public readonly struct State
    {
        public readonly float minimumGap,pinError,maximumDisplacement,minimumHeight;
        public readonly bool finite;
        public readonly string closestCapsule;
        public readonly float penetrationRestHeight;
        public State(float gap,float pin,float displacement,float height,bool valid,string closest,float restHeight)
        {minimumGap=gap;pinError=pin;maximumDisplacement=displacement;minimumHeight=height;finite=valid;closestCapsule=closest;penetrationRestHeight=restHeight;}
    }

    public State Inspect()
    {
        var points=Solver.vertices;
        float gap=float.MaxValue,pin=0,displacement=0,height=float.MaxValue;bool finite=true;
        string closest="";float restHeight=0;
        Matrix4x4 skin=pelvis.localToWorldMatrix*pelvisBind;
        // Native cloth reports metres in its root-bone frame, with inherited
        // scale already applied. Applying the renderer transform scales and
        // rotates them a second time on this imported Z-up skeleton.
        Matrix4x4 toWorld=ParticleFrame;
        for(int i=0;i<points.Length;i++)
        {
            Vector3 world=surfacePoints[i];
            finite&=float.IsFinite(world.x)&&float.IsFinite(world.y)&&float.IsFinite(world.z);
            float distance=Vector3.Distance(world,skin.MultiplyPoint3x4(rest[i]));
            if(pinned[i])pin=Mathf.Max(pin,Vector3.Distance(toWorld.MultiplyPoint3x4(points[i]),skin.MultiplyPoint3x4(rest[i])));
            else displacement=Mathf.Max(displacement,distance);
            height=Mathf.Min(height,owner.InverseTransformPoint(world).y);
            if(!pinned[i])for(int capsule=0;capsule<shapes.Length;capsule++)
            {
                float separation=shapes[capsule].Gap(world);
                if(separation<gap){gap=separation;closest=capsules[capsule].name;restHeight=data.meshToCharacter.MultiplyPoint3x4(rest[i]).y;}
            }
        }
        // Audit triangle interiors as well as particles: vertices outside a
        // capsule alone cannot prove that the fabric between them clears it.
        for(int triangle=0;triangle<surfaceTriangles.Length;triangle+=3)
        {
            int a=surfaceParticles[surfaceTriangles[triangle]],b=surfaceParticles[surfaceTriangles[triangle+1]],c=surfaceParticles[surfaceTriangles[triangle+2]];
            if(pinned[a]&&pinned[b]&&pinned[c])continue;
            Vector3 center=(surfacePoints[a]+surfacePoints[b]+surfacePoints[c])/3;
            for(int capsule=0;capsule<shapes.Length;capsule++)
            {
                float separation=shapes[capsule].Gap(center);
                if(separation<gap){gap=separation;closest=capsules[capsule].name;restHeight=data.meshToCharacter.MultiplyPoint3x4((rest[a]+rest[b]+rest[c])/3).y;}
            }
        }
        Motion=displacement;
        return new State(gap,pin,displacement,height,finite,closest,restHeight);
    }

    public static float CapsuleGap(Vector3 point,CapsuleCollider capsule)
    {
        return new CapsuleShape(capsule).Gap(point);
    }

    void OnDestroy(){if(instanceMaterial!=null)Destroy(instanceMaterial);if(surfaceMesh!=null)Destroy(surfaceMesh);}
}
