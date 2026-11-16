using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(160)]
public sealed class RevenantInjuries : MonoBehaviour
{
    struct Probe {public Vector3 point,normal;public BoneWeight weights;}
    struct Stain {public Transform bone;public Vector3 point,normal;}
    static readonly Dictionary<Mesh,Probe[]> probes=new Dictionary<Mesh,Probe[]>();
    readonly Stain[] stains=new Stain[3];
    readonly List<Collider> hitZones=new List<Collider>();
    MaterialPropertyBlock properties;
    int[] bloodIds,normalIds;
    Matrix4x4[] matrices;
    RevenantVisual visual;
    Probe[] samples;
    int count,next;
    public int BloodCount=>count;
    public Vector3 LastBloodPoint=>count>0?stains[(next+2)%3].bone.TransformPoint(stains[(next+2)%3].point):transform.position;

    public void Bind(RevenantVisual owner,Dictionary<string,Transform> bones)
    {
        properties=new MaterialPropertyBlock();
        bloodIds=new[]{Shader.PropertyToID("_Blood0"),Shader.PropertyToID("_Blood1"),Shader.PropertyToID("_Blood2")};
        normalIds=new[]{Shader.PropertyToID("_BloodNormal0"),Shader.PropertyToID("_BloodNormal1"),Shader.PropertyToID("_BloodNormal2")};
        visual=owner;var mesh=owner.Form.mesh;
        if(!probes.TryGetValue(mesh,out samples))
        {
            var points=mesh.vertices;var normals=mesh.normals;var weights=mesh.boneWeights;var tiles=mesh.uv2;var list=new List<Probe>();
            int stride=Mathf.Max(1,points.Length/1400);
            for(int i=0;i<points.Length;i+=stride)if(tiles[i].x<4)
                list.Add(new Probe{point=points[i],normal=normals[i],weights=weights[i]});
            samples=list.ToArray();probes.Add(mesh,samples);
        }
        matrices=new Matrix4x4[owner.Body.bones.Length];
        float bulk=owner.Form.type==ZombieType.Brute?1.30f:owner.Form.type==ZombieType.Runner?.80f:1;
        void Capsule(string a,string b,float radius)
        {
            var start=bones["Base Human"+a];var end=bones["Base Human"+b];var go=new GameObject("Hit zone "+a);go.layer=gameObject.layer;
            go.transform.SetPositionAndRotation((start.position+end.position)*.5f,Quaternion.FromToRotation(Vector3.up,end.position-start.position));
            go.transform.SetParent(start,true);go.AddComponent<RevenantHitZone>();var volume=go.AddComponent<CapsuleCollider>();
            volume.isTrigger=true;volume.radius=radius*bulk;volume.height=Vector3.Distance(start.position,end.position)+radius*bulk*2;volume.direction=1;hitZones.Add(volume);
        }
        foreach(string side in new[]{"L","R"})
        {
            Capsule(side+"ArmUpperarm",side+"ArmForearm",.055f);Capsule(side+"ArmForearm",side+"ArmPalm",.038f);
        }
        Capsule("LLegThigh","LLegCalf",.075f);Capsule("LLegCalf","LLegFoot",.052f);
        Capsule("RThigh","RCalf",.075f);Capsule("RCalf","RFoot",.052f);
        var head=new GameObject("Hit zone head");head.layer=gameObject.layer;head.transform.position=bones["Base HumanHead"].position+Vector3.up*.065f;
        head.transform.SetParent(bones["Base HumanHead"],true);head.AddComponent<RevenantHitZone>();var sphere=head.AddComponent<SphereCollider>();sphere.radius=.105f;sphere.isTrigger=true;hitZones.Add(sphere);
    }
    public void DisableHitZones(){foreach(var zone in hitZones)if(zone!=null)zone.enabled=false;}
    public void AddHit(Vector3 contact,Vector3 direction)
    {
        var bones=visual.Body.bones;var bind=visual.Form.mesh.bindposes;
        for(int i=0;i<bones.Length;i++)matrices[i]=bones[i].localToWorldMatrix*bind[i];
        float nearest=float.PositiveInfinity;Vector3 point=contact,normal=-direction;int anchor=0;
        foreach(var sample in samples)
        {
            var w=sample.weights;var p=sample.point;
            var world=matrices[w.boneIndex0].MultiplyPoint3x4(p)*w.weight0+matrices[w.boneIndex1].MultiplyPoint3x4(p)*w.weight1+matrices[w.boneIndex2].MultiplyPoint3x4(p)*w.weight2+matrices[w.boneIndex3].MultiplyPoint3x4(p)*w.weight3;
            float distance=(world-contact).sqrMagnitude;
            if(distance>=nearest)continue;
            nearest=distance;point=world;anchor=w.boneIndex0;normal=matrices[anchor].MultiplyVector(sample.normal).normalized;
        }
        var bone=bones[anchor];stains[next]=new Stain{bone=bone,point=bone.InverseTransformPoint(point),normal=bone.InverseTransformDirection(normal)};
        next=(next+1)%3;count=Mathf.Min(3,count+1);UpdateProperties();
    }
    void LateUpdate(){if(count>0)UpdateProperties();}
    void UpdateProperties()
    {
        visual.Body.GetPropertyBlock(properties);
        for(int i=0;i<count;i++)
        {
            var stain=stains[i];var point=stain.bone.TransformPoint(stain.point);var normal=stain.bone.TransformDirection(stain.normal).normalized;
            properties.SetVector(bloodIds[i],new Vector4(point.x,point.y,point.z,.058f*transform.lossyScale.y));
            properties.SetVector(normalIds[i],new Vector4(normal.x,normal.y,normal.z,0));
        }
        visual.Body.SetPropertyBlock(properties);
    }
}
