using System;
using UnityEngine;

[CreateAssetMenu(menuName="Medieval Warrior/Revenant roster")]
public sealed class RevenantAsset : ScriptableObject
{
    [Serializable] public class BladeTrack
    {
        public float duration;
        public Vector3[] start,end;
        public void Sample(float time,out Vector3 a,out Vector3 b)
        {
            float f=Mathf.Clamp01(time/duration)*(start.Length-1);int lo=Mathf.FloorToInt(f),hi=Mathf.Min(lo+1,start.Length-1);
            a=Vector3.Lerp(start[lo],start[hi],f-lo);b=Vector3.Lerp(end[lo],end[hi],f-lo);
        }
    }
    [Serializable] public struct Grip
    {
        public Vector3 position,along,normal,distal,dorsal;
        public Quaternion rotation;
    }
    [Serializable] public class Form
    {
        public ZombieType type;
        public Mesh mesh,lod1,lod2;
        public Grip right,left;
        public RuntimeAnimatorController controller;
        public AnimationClip[] attacks;
        public Vector3 bladeBase,bladeTip;
        public Quaternion leftWristRest;
        public Mesh[] hair;
        public Mesh[] helmets,swords;
        public BladeTrack[] blades;
        public float walkSpeed=.25f,runSpeed=3.55f;
    }
    public string[] bones;
    public Material surface;
    public Avatar avatar;
    public Material[] skinSurfaces;
    public Material hairSurface;
    public Form[] forms;
    public Form Get(ZombieType type)=>forms[(int)type];
}
