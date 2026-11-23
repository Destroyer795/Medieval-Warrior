using System.Collections.Generic;
using UnityEngine;

// Correct the visual rig against the real floor, leaving the NavMesh root and
// the authored swing/flight phases alone. Two nonallocating rays per visible rig.
[DefaultExecutionOrder(140)]
public sealed class RevenantGrounding : MonoBehaviour
{
    readonly RaycastHit[] hits=new RaycastHit[12];
    readonly Transform[] thigh=new Transform[2],calf=new Transform[2],foot=new Transform[2];
    readonly Vector3[] target=new Vector3[2],normal=new Vector3[2];
    readonly Quaternion[] rotation=new Quaternion[2], poseBeforeGrounding=new Quaternion[6];
    readonly float[] delta=new float[2];
    Transform pelvis;
    RevenantVisual visual;
    EnemyController brain;
    Vector3 pelvisBeforeGrounding;
    bool pelvisAdjusted;
    int adjustedLegs;
    public float BodyOffset {get;private set;}
    public bool HasGround {get;private set;}

    public void Bind(RevenantVisual owner,Dictionary<string,Transform> bones)
    {
        visual=owner;brain=GetComponent<EnemyController>();pelvis=bones["Base HumanPelvis"];
        thigh[0]=bones["Base HumanLLegThigh"];calf[0]=bones["Base HumanLLegCalf"];foot[0]=bones["Base HumanLLegFoot"];
        thigh[1]=bones["Base HumanRThigh"];calf[1]=bones["Base HumanRCalf"];foot[1]=bones["Base HumanRFoot"];
    }

    bool Floor(Vector3 point,out RaycastHit closest)
    {
        float scale=transform.lossyScale.y;
        point.y=transform.position.y+.55f*scale;
        int count=Physics.RaycastNonAlloc(point,Vector3.down,hits,1.2f*scale,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
        closest=default;float distance=float.PositiveInfinity;
        for(int i=0;i<count;i++)
        {
            var hit=hits[i];
            if(hit.normal.y<.55f||hit.collider.GetComponentInParent<EnemyController>()!=null||hit.collider.GetComponentInParent<PlayerHealth>()!=null)continue;
            if(hit.distance<distance){distance=hit.distance;closest=hit;}
        }
        return distance<float.PositiveInfinity;
    }

    void Update()
    {
        if ((!pelvisAdjusted && adjustedLegs == 0) || visual == null || !visual.WillAnimate) return;
        if (pelvisAdjusted) pelvis.localPosition = pelvisBeforeGrounding;
        for (int side = 0; side < 2; side++)
        {
            if ((adjustedLegs & (1 << side)) == 0) continue;
            thigh[side].localRotation = poseBeforeGrounding[side * 3];
            calf[side].localRotation = poseBeforeGrounding[side * 3 + 1];
            foot[side].localRotation = poseBeforeGrounding[side * 3 + 2];
        }
        pelvisAdjusted = false;
        adjustedLegs = 0;
    }

    void GroundPelvis()
    {
        if (BodyOffset == 0f) return;
        pelvisBeforeGrounding = pelvis.localPosition;
        pelvisAdjusted = true;
        pelvis.position += Vector3.up * BodyOffset;
    }

    void LateUpdate()
    {
        if (visual == null || !visual.CanAdjustPose) return;
        if(brain!=null&&brain.IsDead)
        {
            GroundPelvis(); // Keep the fall grounded; don't cancel corpse sinking.
            return;
        }
        float scale=transform.lossyScale.y;HasGround=false;
        for(int side=0;side<2;side++)
        {
            target[side]=foot[side].position;rotation[side]=foot[side].rotation;
            if(Floor(target[side],out var hit))
            {
                delta[side]=Mathf.Clamp(hit.point.y-transform.position.y,-.3f*scale,.3f*scale);
                normal[side]=hit.normal;HasGround=true;
            }
            else{delta[side]=0;normal[side]=Vector3.up;}
        }
        BodyOffset=Mathf.Min(delta[0],delta[1]);
        GroundPelvis();
        if(visual.DetailLevel>=2)return;
        for(int side=0;side<2;side++)
        {
            if(Mathf.Abs(delta[side]-BodyOffset)<.001f&&normal[side].y>.999f)continue;
            adjustedLegs |= 1 << side;
            poseBeforeGrounding[side * 3] = thigh[side].localRotation;
            poseBeforeGrounding[side * 3 + 1] = calf[side].localRotation;
            poseBeforeGrounding[side * 3 + 2] = foot[side].localRotation;
            var goal=target[side]+Vector3.up*delta[side];
            // The captured ankle height retains toe-off and swing clearance.
            PoseMath.ArmStable(thigh[side],calf[side],foot[side],goal,calf[side].position+transform.forward*.04f*scale);
            float plant=1-Mathf.SmoothStep(0,1,Mathf.InverseLerp(.10f,.22f,(target[side].y-transform.position.y)/scale));
            var tilt=Quaternion.FromToRotation(Vector3.up,normal[side]);
            foot[side].rotation=Quaternion.Slerp(Quaternion.identity,tilt,plant)*rotation[side];
        }
    }
}
